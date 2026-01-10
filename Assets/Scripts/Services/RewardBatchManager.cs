using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Plinko.Core.Logging;
using Plinko.Data;
using Plinko.Utils;

namespace Plinko.Services
{
    // Manages reward batch accumulation, verification, and retry throughout the game.
    public class RewardBatchManager : IRewardBatchManager
    {
        public event Action<RewardBatch>? OnBatchCreated;
        public event Action<RewardBatch, BatchValidationResponse>? OnBatchValidated;
        public event Action<RewardBatch, string>? OnBatchFailed;
        public event Action<long>? OnWalletUpdated;
        public event Action<int, long>? OnEntriesRejected;

        private readonly IServerService _serverService;
        private readonly GameConfig _config;
        private readonly ILogger _logger;

        private RewardBatch? _currentBatch;
        private readonly Queue<RewardBatch> _pendingBatches;
        private readonly List<RewardBatch> _failedBatches;
        private readonly List<RewardBatch> _retryBuffer;

        private string _playerId = string.Empty;
        private int _sessionId;
        private string _gameSeed = string.Empty;
        private int _nextBallIndex;

        private float _batchTimer;
        private bool _isProcessingBatch;
        private CancellationTokenSource? _cts;

        public long OptimisticBalance { get; private set; }
        public long VerifiedBalance { get; private set; }
        public long PendingRewards { get; private set; }
        public int FailedBatchCount => _failedBatches.Count;
        public int PendingBatchCount => _pendingBatches.Count + (_currentBatch?.Entries.Count > 0 ? 1 : 0);

        public long FailedBatchRewards
        {
            get
            {
                long total = 0;
                foreach (var batch in _failedBatches)
                {
                    total += batch.TotalClientCalculatedReward;
                }
                return total;
            }
        }

        public RewardBatchManager(IServerService serverService, GameConfig config, ILogger logger)
        {
            _serverService = serverService ?? throw new ArgumentNullException(nameof(serverService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pendingBatches = new Queue<RewardBatch>();
            _failedBatches = new List<RewardBatch>();
            _retryBuffer = new List<RewardBatch>();
            _cts = new CancellationTokenSource();
        }

        public void InitializeSession(string playerId, int sessionId, string gameSeed, long currentBalance)
        {
            _playerId = playerId;
            _sessionId = sessionId;
            _gameSeed = gameSeed;
            _nextBallIndex = 0;

            VerifiedBalance = currentBalance;
            OptimisticBalance = currentBalance;
            PendingRewards = 0;

            _currentBatch = CreateNewBatch();
            _batchTimer = 0f;

            ResetCancellationToken();
        }

        private void ResetCancellationToken()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Expected when CTS was already disposed
            }
            _cts = new CancellationTokenSource();
        }

        public void AddReward(int ballIndex, int bucketIndex, int totalBucketCount, long reward, int level, float dropPositionX)
        {
            if (_currentBatch == null)
            {
                _currentBatch = CreateNewBatch();
            }

            var entry = new RewardEntry
            {
                BallIndex = ballIndex,
                BucketIndex = bucketIndex,
                TotalBucketCount = totalBucketCount,
                RewardAmount = reward,
                Level = level,
                DropPositionX = dropPositionX
            };

            _currentBatch.AddEntry(entry);

            OptimisticBalance += reward;
            PendingRewards += reward;

            OnWalletUpdated?.Invoke(OptimisticBalance);

            if (_currentBatch.IsFull(_config.RewardBatchSize))
            {
                SubmitCurrentBatch();
            }

            _nextBallIndex = ballIndex + 1;
        }

        public void Tick(float deltaTime)
        {
            if (_currentBatch != null && _currentBatch.Entries.Count > 0)
            {
                _batchTimer += deltaTime;

                if (_batchTimer >= _config.RewardBatchTimeoutSeconds)
                {
                    SubmitCurrentBatch();
                }
            }
            else
            {
                _batchTimer = 0f;
            }

            _ = ProcessPendingBatchesAsync().WithLogging("RewardBatchManager.ProcessPendingBatches");
            RetryFailedBatchesIfNeeded(deltaTime);
        }

        private float _retryTimer;

        private void RetryFailedBatchesIfNeeded(float deltaTime)
        {
            if (_failedBatches.Count == 0) return;

            _retryTimer += deltaTime;
            if (_retryTimer >= _config.RewardBatchRetryInterval)
            {
                _retryTimer = 0f;

                _retryBuffer.Clear();
                _retryBuffer.AddRange(_failedBatches);
                _failedBatches.Clear();

                for (int i = 0; i < _retryBuffer.Count; i++)
                {
                    var batch = _retryBuffer[i];

                    if (batch.RetryCount >= _config.RewardBatchMaxRetries)
                    {
                        long lostAmount = batch.TotalClientCalculatedReward;
                        OptimisticBalance -= lostAmount;
                        PendingRewards -= lostAmount;
                        OnEntriesRejected?.Invoke(batch.Entries.Count, lostAmount);
                        OnWalletUpdated?.Invoke(OptimisticBalance);
                        continue;
                    }

                    batch.Status = RewardBatchStatus.Pending;
                    _pendingBatches.Enqueue(batch);
                }

                RecalculatePendingRewards();
            }
        }

        public async Task FlushAsync()
        {
            if (_currentBatch != null && _currentBatch.Entries.Count > 0)
            {
                SubmitCurrentBatch();
            }

            while (_pendingBatches.Count > 0 || _isProcessingBatch)
            {
                await Task.Delay(100);
            }
        }

        public async Task RetryFailedBatchesAsync()
        {
            if (_failedBatches.Count == 0) return;

            _retryBuffer.Clear();
            _retryBuffer.AddRange(_failedBatches);
            _failedBatches.Clear();

            for (int i = 0; i < _retryBuffer.Count; i++)
            {
                var batch = _retryBuffer[i];
                batch.Status = RewardBatchStatus.Pending;
                _pendingBatches.Enqueue(batch);
            }

            await Task.CompletedTask;
        }

        private void SubmitCurrentBatch()
        {
            if (_currentBatch == null || _currentBatch.Entries.Count == 0) return;

            _currentBatch.Status = RewardBatchStatus.Sending;
            OnBatchCreated?.Invoke(_currentBatch);

            _pendingBatches.Enqueue(_currentBatch);
            _currentBatch = CreateNewBatch();
            _batchTimer = 0f;
        }

        private async Task ProcessPendingBatchesAsync()
        {
            if (_isProcessingBatch || _pendingBatches.Count == 0) return;

            _isProcessingBatch = true;

            try
            {
                while (_pendingBatches.Count > 0)
                {
                    var batch = _pendingBatches.Dequeue();
                    await ValidateBatchAsync(batch);
                }
            }
            finally
            {
                _isProcessingBatch = false;
            }
        }

        private async Task ValidateBatchAsync(RewardBatch batch)
        {
            try
            {
                var response = await _serverService.ValidateBatchAsync(batch, _cts.Token);

                if (response.IsRetryable)
                {
                    batch.Status = RewardBatchStatus.Pending;
                    batch.RetryCount++;
                    _failedBatches.Add(batch);
                    return;
                }

                if (response.IsValid)
                {
                    batch.Status = RewardBatchStatus.Validated;
                    VerifiedBalance = response.NewWalletBalance;

                    int rejectedCount = response.InvalidEntryIndices?.Count ?? 0;
                    if (rejectedCount > 0)
                    {
                        long rejectedAmount = 0;
                        foreach (int idx in response.InvalidEntryIndices)
                        {
                            if (idx >= 0 && idx < batch.Entries.Count)
                            {
                                rejectedAmount += batch.Entries[idx].RewardAmount;
                            }
                        }

                        if (rejectedAmount > 0)
                        {
                            OptimisticBalance -= rejectedAmount;
                            PendingRewards -= rejectedAmount;
                            OnEntriesRejected?.Invoke(rejectedCount, rejectedAmount);
                        }
                    }

                    RecalculatePendingRewards();

                    OnBatchValidated?.Invoke(batch, response);
                    OnWalletUpdated?.Invoke(OptimisticBalance);
                }
                else
                {
                    batch.Status = RewardBatchStatus.Rejected;

                    long validAmount = response.ServerCalculatedReward;
                    long rejectedAmount = batch.TotalClientCalculatedReward - validAmount;

                    if (rejectedAmount > 0)
                    {
                        OptimisticBalance -= rejectedAmount;
                        PendingRewards -= rejectedAmount;

                        int rejectedEntryCount = response.InvalidEntryIndices?.Count ?? 0;
                        if (rejectedEntryCount == 0 && rejectedAmount > 0)
                        {
                            rejectedEntryCount = batch.Entries.Count;
                        }

                        OnEntriesRejected?.Invoke(rejectedEntryCount, rejectedAmount);
                    }

                    if (response.NewWalletBalance > 0)
                    {
                        VerifiedBalance = response.NewWalletBalance;
                    }

                    PendingRewards -= validAmount;
                    OptimisticBalance = VerifiedBalance + PendingRewards;

                    OnBatchFailed?.Invoke(batch, response.ErrorMessage ?? "Partial validation");
                    OnWalletUpdated?.Invoke(OptimisticBalance);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled (e.g., session ended) - queue for retry without incrementing retry count
                batch.Status = RewardBatchStatus.Pending;
                _failedBatches.Add(batch);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{nameof(RewardBatchManager)}] ValidateBatchAsync failed (retry {batch.RetryCount}): {ex.Message}");
                batch.Status = RewardBatchStatus.Pending;
                batch.RetryCount++;
                _failedBatches.Add(batch);
            }
        }

        private RewardBatch CreateNewBatch()
        {
            return new RewardBatch(_playerId, _sessionId, _gameSeed, _nextBallIndex);
        }

        private void RecalculatePendingRewards()
        {
            PendingRewards = 0;

            if (_currentBatch != null)
            {
                PendingRewards += _currentBatch.TotalClientCalculatedReward;
            }

            foreach (var batch in _pendingBatches)
            {
                PendingRewards += batch.TotalClientCalculatedReward;
            }

            foreach (var batch in _failedBatches)
            {
                PendingRewards += batch.TotalClientCalculatedReward;
            }

            OptimisticBalance = VerifiedBalance + PendingRewards;
        }

        public async Task<bool> ForceSyncWalletAsync()
        {
            try
            {
                var response = await _serverService.ForceSyncWalletAsync(_playerId, _cts.Token);

                if (response.Success)
                {
                    VerifiedBalance = response.ServerBalance;
                    RecalculatePendingRewards();
                    OnWalletUpdated?.Invoke(OptimisticBalance);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{nameof(RewardBatchManager)}] ForceSyncWalletAsync failed: {ex.Message}");
            }

            return false;
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Expected when CTS was already disposed - safe to ignore
            }
            _cts = null;
        }
    }
}
