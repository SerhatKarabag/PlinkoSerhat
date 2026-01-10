using System;
using System.Threading.Tasks;
using Plinko.Core.Logging;
using Plinko.Core.StateMachine;
using Plinko.Data;
using Plinko.Physics;
using Plinko.Services;
using Plinko.UI;

namespace Plinko.Core
{
    public class GameEventWiring : IDisposable
    {
        private readonly GameManager _gameManager;
        private readonly GameConfig _config;
        private readonly UIManager _uiManager;
        private readonly BallManager _ballManager;
        private readonly PlinkoBoard _board;
        private readonly ISessionManager _sessionManager;
        private readonly IRewardBatchManager _rewardBatchManager;
        private readonly Func<Task> _startNewSessionInternal;
        private readonly ILogger _logger;

        // These are set after construction via UpdateStateReferences
        private PlayerData? _playerData;
        private RunSummary? _currentRunSummary;
        private bool _disposed;

        public GameEventWiring(
            GameManager gameManager,
            GameConfig config,
            UIManager uiManager,
            BallManager ballManager,
            PlinkoBoard board,
            ISessionManager sessionManager,
            IRewardBatchManager rewardBatchManager,
            Func<Task> startNewSessionInternal,
            ILogger logger)
        {
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _ballManager = ballManager ?? throw new ArgumentNullException(nameof(ballManager));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _rewardBatchManager = rewardBatchManager ?? throw new ArgumentNullException(nameof(rewardBatchManager));
            _startNewSessionInternal = startNewSessionInternal ?? throw new ArgumentNullException(nameof(startNewSessionInternal));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void WireEvents(
            InitializingState initState,
            LevelTransitionState levelTransitionState,
            RunEndingState runEndingState,
            RunFinishedState runFinishedState,
            WaitingState waitingState,
            ErrorState errorState)
        {
            initState.OnInitializationComplete += () => _gameManager.StateMachine.TransitionTo(GameStateType.Playing);
            initState.OnInitializationError += HandleInitializationError;

            levelTransitionState.OnLevelTransitionStart += HandleLevelTransitionStart;
            levelTransitionState.OnLevelTransitionComplete += HandleLevelTransitionComplete;

            runEndingState.OnRunEndingStarted += HandleRunEndingStarted;
            runEndingState.OnRunEndingComplete += HandleRunEndingComplete;

            runFinishedState.OnRunFinished += HandleRunFinished;
            runFinishedState.OnRestartRequested += HandleRestartRequested;

            waitingState.OnWaitTimeUpdated += HandleWaitTimeUpdate;
            waitingState.OnSessionResetReady += HandleSessionReset;

            errorState.OnErrorDisplayRequired += HandleError;

            _sessionManager.OnTimerUpdated += HandleTimerUpdate;
            _sessionManager.OnSessionExpired += HandleSessionExpired;

            _rewardBatchManager.OnWalletUpdated += HandleWalletUpdate;
            _rewardBatchManager.OnBatchValidated += HandleBatchValidated;
            _rewardBatchManager.OnBatchFailed += HandleBatchFailed;
            _rewardBatchManager.OnEntriesRejected += HandleEntriesRejected;

            _uiManager.OnRunEndRestartClicked += HandleUIRestartClicked;
            _ballManager.OnBallScored += HandleBallScored;
        }

        public void UpdateStateReferences(PlayerData playerData, RunSummary runSummary)
        {
            _playerData = playerData;
            _currentRunSummary = runSummary;
            RestoreHistoryForSession();
        }

        private void HandleBallScored(PlinkoBall ball, int bucketIndex, long reward)
        {
            int totalBuckets = _board.Buckets?.Count ?? 0;
            _rewardBatchManager.AddReward(
                ball.BallIndex, bucketIndex, totalBuckets,
                reward, ball.SpawnLevel, ball.DropPositionX
            );

            _currentRunSummary?.OnBallScored(reward);
            _currentRunSummary?.OnLevelReached(ball.SpawnLevel);
            if (_currentRunSummary != null)
            {
                _playerData?.UpdateRunSummary(_currentRunSummary);
                _playerData?.Save();
            }

            var levelConfig = _config.GetLevelConfig(ball.SpawnLevel);
            string bucketLabel = levelConfig.Buckets != null && bucketIndex < levelConfig.Buckets.Length
                ? levelConfig.Buckets[bucketIndex].Label
                : $"{reward}x";

            _uiManager.AddHistoryEntry(bucketIndex, bucketLabel, reward, ball.SpawnLevel);

            var historyEntry = RewardHistoryEntry.Create(bucketIndex, bucketLabel, reward, ball.SpawnLevel);
            _playerData?.AddRewardToHistory(historyEntry);

            _uiManager.ShowRewardPopup(ball.transform.position, reward);
        }

        private void RestoreHistoryForSession()
        {
            if (_playerData == null)
            {
                return;
            }

            _uiManager.ClearHistory();

            if (_playerData.RewardHistory == null)
            {
                return;
            }

            foreach (var entry in _playerData.RewardHistory)
            {
                _uiManager.AddHistoryEntry(
                    entry.BucketIndex,
                    entry.BucketLabel,
                    entry.RewardAmount,
                    entry.Level);
            }
        }

        private void HandleWalletUpdate(long _)
        {
            _uiManager.SetWallet(_rewardBatchManager.VerifiedBalance, _rewardBatchManager.PendingRewards);
        }

        private void HandleBatchValidated(RewardBatch batch, BatchValidationResponse response)
        {
            _playerData?.UpdateWallet(response.NewWalletBalance);
            _currentRunSummary?.OnBatchValidated(response.ServerCalculatedReward);
            UpdateRunSummaryPendingState();
            RefreshRunEndUIIfVisible();
        }

        private void HandleBatchFailed(RewardBatch batch, string error)
        {
            UpdateRunSummaryPendingState();
            RefreshRunEndUIIfVisible();
        }

        private void HandleEntriesRejected(int rejectedCount, long rejectedAmount)
        {
            _currentRunSummary?.OnBatchRejected(rejectedCount, rejectedAmount);
            UpdateRunSummaryPendingState();
            RefreshRunEndUIIfVisible();
        }

        private void HandleTimerUpdate(float remainingSeconds)
        {
            _uiManager.SetTimerSeconds(remainingSeconds);
        }

        private void HandleSessionExpired()
        {
            var currentState = _gameManager.StateMachine.CurrentStateType;
            if (currentState == GameStateType.RunEnding || currentState == GameStateType.RunFinished)
            {
                return;
            }

            _gameManager.StateMachine.TransitionTo(GameStateType.RunEnding);
        }

        private void HandleLevelTransitionStart(int newLevel)
        {
            _uiManager.ShowLevelTransition(newLevel);
        }

        private void HandleLevelTransitionComplete(int newLevel)
        {
            _uiManager.HideLevelTransition();
        }

        private void HandleRunEndingStarted() { }

        private void HandleRunEndingComplete()
        {
            UpdateRunSummaryPendingState();
            var runFinishedState = _gameManager.StateMachine.GetState<RunFinishedState>();
            runFinishedState?.SetRunSummary(_currentRunSummary);
        }

        private void HandleRunFinished(RunSummary summary)
        {
            _uiManager.ShowRunEnd(summary);
            _playerData?.UpdateRunSummary(summary);
            _playerData?.MarkHistoryClearOnNextLaunch();
            _playerData?.SaveImmediate();
        }

        private void HandleRestartRequested()
        {
            _ = LogTaskFailureAsync(StartNewRun(), "StartNewRun");
        }

        private void HandleUIRestartClicked()
        {
            var runFinishedState = _gameManager.StateMachine.GetState<RunFinishedState>();
            if (_gameManager.StateMachine.CurrentStateType == GameStateType.RunFinished)
            {
                runFinishedState?.RequestRestart();
            }
        }

        private void HandleWaitTimeUpdate(float remainingSeconds)
        {
            _uiManager.ShowSessionExpired(remainingSeconds);
            _uiManager.UpdateSessionExpiredCountdown(remainingSeconds);
        }

        private void HandleSessionReset()
        {
            _uiManager.HideSessionExpired();
        }

        private void HandleInitializationError(string error)
        {
            var errorState = _gameManager.StateMachine.GetState<ErrorState>();
            errorState?.SetError(error);
            _gameManager.StateMachine.TransitionTo(GameStateType.Error);
        }

        private void HandleError(string error)
        {
            _uiManager.ShowError(error);
        }

        public async Task HandleAppResume()
        {
            if (_playerData == null) return;

            var pausedState = _gameManager.StateMachine.GetState<PausedState>();
            var previousState = pausedState?.GetPreviousState() ?? GameStateType.Playing;

            if (previousState == GameStateType.RunFinished || previousState == GameStateType.RunEnding)
            {
                await StartNewRun();
                return;
            }

            try
            {
                await _sessionManager.SyncSessionAsync(_playerData.PlayerId, _playerData.WalletBalance);
                _ = _rewardBatchManager.ForceSyncWalletAsync();

                if (_sessionManager.IsSessionExpired)
                    _gameManager.StateMachine.TransitionTo(GameStateType.RunEnding);
                else
                    _gameManager.StateMachine.TransitionTo(previousState);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[{nameof(GameEventWiring)}] HandleAppResume sync failed, continuing with local state: {ex.Message}");

                if (_sessionManager.IsSessionExpired)
                    _gameManager.StateMachine.TransitionTo(GameStateType.RunEnding);
                else
                    _gameManager.StateMachine.TransitionTo(GameStateType.Playing);
            }
        }

        private async Task StartNewRun()
        {
            _uiManager.HideRunEnd();
            _uiManager.ShowLoading("Starting new run...");
            _playerData?.ClearHistoryClearFlag();
            _playerData?.SaveImmediate();

            try
            {
                _ballManager.ResetSession();
                await _startNewSessionInternal();
                _gameManager.StateMachine.TransitionTo(GameStateType.Playing);
            }
            catch (Exception e)
            {
                var errorState = _gameManager.StateMachine.GetState<ErrorState>();
                errorState?.SetError(e.Message);
                _gameManager.StateMachine.TransitionTo(GameStateType.Error);
            }
        }

        private async Task LogTaskFailureAsync(Task task, string context)
        {
            if (task == null) return;

            try
            {
                await task;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{nameof(GameEventWiring)}] {context} failed: {ex}");
            }
        }

        private void UpdateRunSummaryPendingState()
        {
            if (_currentRunSummary != null)
            {
                _currentRunSummary.UpdatePendingPoints(_rewardBatchManager.PendingRewards);
                _currentRunSummary.UpdateRetryingBatches(
                    _rewardBatchManager.FailedBatchCount,
                    _rewardBatchManager.FailedBatchRewards
                );
                _playerData?.UpdateRunSummary(_currentRunSummary);
                _playerData?.Save();
            }
        }

        private void RefreshRunEndUIIfVisible()
        {
            if (_gameManager.StateMachine.CurrentStateType == GameStateType.RunFinished)
            {
                _uiManager.RefreshRunEndDisplay();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _sessionManager.OnTimerUpdated -= HandleTimerUpdate;
            _sessionManager.OnSessionExpired -= HandleSessionExpired;

            _rewardBatchManager.OnWalletUpdated -= HandleWalletUpdate;
            _rewardBatchManager.OnBatchValidated -= HandleBatchValidated;
            _rewardBatchManager.OnBatchFailed -= HandleBatchFailed;
            _rewardBatchManager.OnEntriesRejected -= HandleEntriesRejected;

            _uiManager.OnRunEndRestartClicked -= HandleUIRestartClicked;
            _ballManager.OnBallScored -= HandleBallScored;

            _disposed = true;
        }
    }
}
