using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Plinko.Data;
using Plinko.Services.AntiCheat;
using UnityEngine;
using CoreLogger = Plinko.Core.Logging.ILogger;

namespace Plinko.Services
{
    public class MockServerService : IServerService
    {
        public event Action<BatchValidationResponse>? OnBatchValidated;
        public event Action<string>? OnServerError;
        public event Action<SessionData>? OnSessionStarted;
        public event Action<SessionData>? OnSessionSynced;

        private const string SERVER_STATE_KEY = "MockServer_PlayerStates";

        private readonly GameConfig _config;
        private readonly Dictionary<string, ServerPlayerState> _playerStates;
        private readonly System.Random _serverRandom;
        private readonly AntiCheatValidator _antiCheatValidator;
        private readonly AntiCheatConfig _antiCheatConfig;
        private readonly IPreferences _preferences;
        private readonly CoreLogger _logger;

        private float _minLatencyMs;
        private float _maxLatencyMs;
        private float _errorRate;

        public MockServerService(
            GameConfig config,
            IPreferences preferences,
            CoreLogger logger,
            AntiCheatConfig? antiCheatConfig = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerStates = new Dictionary<string, ServerPlayerState>();
            _serverRandom = new System.Random();

            _minLatencyMs = config.MinServerLatencyMs;
            _maxLatencyMs = config.MaxServerLatencyMs;
            _errorRate = config.ServerErrorRate;

            _antiCheatConfig = antiCheatConfig ?? AntiCheatConfig.Default;
            _antiCheatValidator = new AntiCheatValidator(_antiCheatConfig, config, _logger);

            LoadServerState();
        }

        public void SetSpawnBoundaries(float left, float right)
        {
            _antiCheatValidator.SetSpawnBoundaries(left, right);
        }

        public async Task<SessionData> StartSessionAsync(string playerId, CancellationToken ct = default)
        {
            await SimulateLatency(ct);

            if (ShouldSimulateError())
            {
                throw new ServerException("Failed to start session. Please try again.");
            }

            if (!_playerStates.TryGetValue(playerId, out var playerState))
            {
                playerState = new ServerPlayerState
                {
                    PlayerId = playerId,
                    WalletBalance = 0,
                    TotalEarned = 0
                };
                _playerStates[playerId] = playerState;
            }

            var session = new SessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                PlayerId = playerId,
                GameSeed = GenerateGameSeed(),
                StartTime = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddMinutes(_config.SessionDurationMinutes),
                InitialBallCount = _config.InitialBallCount,
                CurrentWalletBalance = playerState.WalletBalance
            };

            playerState.CurrentSessionId = session.SessionId;
            playerState.CurrentGameSeed = session.GameSeed;
            playerState.SessionStartTime = session.StartTime;
            playerState.SessionBallIndex = 0;
            playerState.ValidatedBallIndices.Clear();

            SaveServerState();

            OnSessionStarted?.Invoke(session);
            return session;
        }

        public async Task<SessionData> SyncSessionAsync(string playerId, string sessionId, long clientWalletBalance = 0, CancellationToken ct = default)
        {
            await SimulateLatency(ct);

            bool isNewPlayer = false;
            if (!_playerStates.TryGetValue(playerId, out var playerState))
            {
                isNewPlayer = true;
                playerState = new ServerPlayerState
                {
                    PlayerId = playerId,
                    WalletBalance = clientWalletBalance,
                    TotalEarned = clientWalletBalance,
                    CurrentSessionId = sessionId,
                    CurrentGameSeed = GenerateGameSeed(),
                    SessionStartTime = DateTime.UtcNow
                };
                _playerStates[playerId] = playerState;

                SaveServerState();
            }

            if (isNewPlayer)
            {
                var continuationSession = new SessionData
                {
                    SessionId = sessionId,
                    PlayerId = playerId,
                    GameSeed = playerState.CurrentGameSeed,
                    StartTime = DateTime.UtcNow,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(_config.SessionDurationMinutes),
                    InitialBallCount = _config.InitialBallCount,
                    CurrentWalletBalance = playerState.WalletBalance,
                    RemainingSeconds = -1
                };

                OnSessionSynced?.Invoke(continuationSession);
                return continuationSession;
            }

            var now = DateTime.UtcNow;
            var sessionStart = playerState.SessionStartTime;
            var elapsed = now - sessionStart;

            if (elapsed.TotalMinutes >= _config.SessionDurationMinutes || playerState.CurrentSessionId != sessionId)
            {
                return await StartSessionAsync(playerId, ct);
            }

            var session = new SessionData
            {
                SessionId = playerState.CurrentSessionId,
                PlayerId = playerId,
                GameSeed = playerState.CurrentGameSeed,
                StartTime = playerState.SessionStartTime,
                ExpiryTime = playerState.SessionStartTime.AddMinutes(_config.SessionDurationMinutes),
                InitialBallCount = _config.InitialBallCount,
                CurrentWalletBalance = playerState.WalletBalance,
                RemainingSeconds = (float)(_config.SessionDurationMinutes * 60 - elapsed.TotalSeconds)
            };

            OnSessionSynced?.Invoke(session);
            return session;
        }

        public async Task<BatchValidationResponse> ValidateBatchAsync(RewardBatch batch, CancellationToken ct = default)
        {
            await SimulateLatency(ct);

            var response = new BatchValidationResponse
            {
                BatchId = batch.BatchId,
                InvalidEntryIndices = new List<int>(),
                IsRetryable = false
            };

            if (ShouldSimulateError())
            {
                response.IsValid = false;
                response.IsRetryable = true;
                response.ErrorMessage = "Server validation timeout. Will retry automatically.";
                OnServerError?.Invoke(response.ErrorMessage);
                return response;
            }

            if (!_playerStates.TryGetValue(batch.PlayerId, out var playerState))
            {
                response.IsValid = false;
                response.IsRetryable = false;
                response.ErrorMessage = "Invalid player";
                return response;
            }

            long serverCalculatedTotal = 0;
            var validatedThisBatch = new List<int>();

            for (int i = 0; i < batch.Entries.Count; i++)
            {
                var entry = batch.Entries[i];
                bool isValid = true;
                string invalidReason = null;

                if (playerState.ValidatedBallIndices.Contains(entry.BallIndex))
                {
                    isValid = false;
                    invalidReason = $"Duplicate ball index: {entry.BallIndex} already validated";
                }
                else if (entry.BallIndex < 0)
                {
                    isValid = false;
                    invalidReason = $"Invalid ball index: {entry.BallIndex}";
                }

                int actualBucketCount = entry.TotalBucketCount > 0 ? entry.TotalBucketCount : 13;
                if (entry.BucketIndex < 0 || entry.BucketIndex >= actualBucketCount)
                {
                    isValid = false;
                    invalidReason = $"Invalid bucket index: {entry.BucketIndex} (max: {actualBucketCount - 1})";
                }

                if (isValid)
                {
                    long expectedReward = CalculateExpectedReward(entry.BucketIndex, actualBucketCount, entry.Level);
                    long tolerance = Math.Max(1, (long)(expectedReward * 0.01));

                    if (Math.Abs(entry.RewardAmount - expectedReward) > tolerance)
                    {
                        isValid = false;
                        invalidReason = $"Reward mismatch: claimed {entry.RewardAmount}, expected {expectedReward}";
                    }
                }

                if (isValid)
                {
                    serverCalculatedTotal += entry.RewardAmount;
                    validatedThisBatch.Add(entry.BallIndex);
                }
                else
                {
                    response.InvalidEntryIndices.Add(i);
                }
            }

            foreach (int ballIndex in validatedThisBatch)
            {
                playerState.AddValidatedIndex(ballIndex);
            }

            if (response.InvalidEntryIndices.Count == 0)
            {
                var antiCheatResult = _antiCheatValidator.ValidateBatch(batch, playerState.CurrentSessionId);

                if (antiCheatResult.ShouldReject)
                {
                    response.IsValid = false;
                    response.IsRetryable = false;
                    response.ServerCalculatedReward = 0;
                    response.ErrorMessage = $"Batch rejected by anti-cheat: {string.Join(", ", antiCheatResult.StatisticalFlags ?? antiCheatResult.Flags)}";
                    response.NewWalletBalance = playerState.WalletBalance;
                }
                else
                {
                    response.IsValid = true;
                    response.IsRetryable = false;
                    response.ServerCalculatedReward = serverCalculatedTotal;

                    playerState.WalletBalance += serverCalculatedTotal;
                    playerState.TotalEarned += serverCalculatedTotal;
                    response.NewWalletBalance = playerState.WalletBalance;

                    SaveServerState();
                }
            }
            else if (response.InvalidEntryIndices.Count < batch.Entries.Count)
            {
                response.IsValid = true;
                response.IsRetryable = false;
                response.ServerCalculatedReward = serverCalculatedTotal;
                response.ErrorMessage = $"Partial validation: {response.InvalidEntryIndices.Count}/{batch.Entries.Count} entries rejected";

                playerState.WalletBalance += serverCalculatedTotal;
                playerState.TotalEarned += serverCalculatedTotal;
                response.NewWalletBalance = playerState.WalletBalance;

                SaveServerState();
            }
            else
            {
                response.IsValid = false;
                response.IsRetryable = false;
                response.ServerCalculatedReward = 0;
                response.ErrorMessage = "Batch rejected: all entries invalid (possible tampering)";
                response.NewWalletBalance = playerState.WalletBalance;
            }

            OnBatchValidated?.Invoke(response);
            return response;
        }

        private long CalculateExpectedReward(int bucketIndex, int actualBucketCount, int level)
        {
            var levelConfig = _config.GetLevelConfig(level);
            var buckets = levelConfig.Buckets;

            if (buckets == null || buckets.Length == 0)
                return 1;

            int configIndex = MapBucketIndex(bucketIndex, actualBucketCount, buckets.Length);
            return (long)(buckets[configIndex].BaseReward * levelConfig.RewardMultiplier);
        }

        private int MapBucketIndex(int actualIndex, int actualCount, int configCount)
        {
            if (actualCount == configCount)
                return actualIndex;

            if (actualCount <= 1 || configCount <= 1)
                return 0;

            float normalizedPos = (float)actualIndex / (actualCount - 1);
            return Math.Clamp((int)Math.Round(normalizedPos * (configCount - 1)), 0, configCount - 1);
        }

        public async Task<long> GetWalletBalanceAsync(string playerId, CancellationToken ct = default)
        {
            await SimulateLatency(ct);

            if (_playerStates.TryGetValue(playerId, out var state))
            {
                return state.WalletBalance;
            }
            return 0;
        }

        public async Task<WalletSyncResponse> ForceSyncWalletAsync(string playerId, CancellationToken ct = default)
        {
            await SimulateLatency(ct);

            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                return new WalletSyncResponse { Success = false, ErrorMessage = "Player not found" };
            }

            return new WalletSyncResponse
            {
                Success = true,
                ServerBalance = state.WalletBalance,
                TotalEarned = state.TotalEarned
            };
        }

        private async Task SimulateLatency(CancellationToken ct)
        {
            float latency = (float)(_serverRandom.NextDouble() * (_maxLatencyMs - _minLatencyMs) + _minLatencyMs);
            await Task.Delay(TimeSpan.FromMilliseconds(latency), ct);
        }

        private bool ShouldSimulateError()
        {
            return _serverRandom.NextDouble() < _errorRate;
        }

        private string GenerateGameSeed()
        {
            byte[] seedBytes = new byte[16];
            for (int i = 0; i < seedBytes.Length; i++)
            {
                seedBytes[i] = (byte)_serverRandom.Next(256);
            }
            return Convert.ToBase64String(seedBytes);
        }

        private void SaveServerState()
        {
            try
            {
                var wrapper = new ServerStateWrapper();
                foreach (var kvp in _playerStates)
                {
                    wrapper.Players.Add(SerializablePlayerState.FromServerState(kvp.Value));
                }
                string json = JsonUtility.ToJson(wrapper);
                _preferences.SetString(SERVER_STATE_KEY, json);
                _preferences.Save();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[{nameof(MockServerService)}] SaveServerState failed: {ex.Message}");
            }
        }

        private void LoadServerState()
        {
            if (!_preferences.HasKey(SERVER_STATE_KEY))
            {
                return;
            }

            try
            {
                string json = _preferences.GetString(SERVER_STATE_KEY);
                var wrapper = JsonUtility.FromJson<ServerStateWrapper>(json);

                if (wrapper?.Players != null)
                {
                    foreach (var serialized in wrapper.Players)
                    {
                        var state = serialized.ToServerState();
                        _playerStates[state.PlayerId] = state;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[{nameof(MockServerService)}] LoadServerState failed: {ex.Message}");
            }
        }

        private class ServerPlayerState
        {
            public string PlayerId;
            public long WalletBalance;
            public long TotalEarned;
            public string CurrentSessionId;
            public string CurrentGameSeed;
            public DateTime SessionStartTime;
            public int SessionBallIndex;
            public HashSet<int> ValidatedBallIndices = new HashSet<int>();
            public const int MaxValidatedIndices = 500;

            public void AddValidatedIndex(int ballIndex)
            {
                if (ValidatedBallIndices.Count >= MaxValidatedIndices)
                {
                    ValidatedBallIndices.Clear();
                }
                ValidatedBallIndices.Add(ballIndex);
            }
        }

        [Serializable]
        private class ServerStateWrapper
        {
            public List<SerializablePlayerState> Players = new List<SerializablePlayerState>();
        }

        [Serializable]
        private class SerializablePlayerState
        {
            public string PlayerId;
            public long WalletBalance;
            public long TotalEarned;
            public string CurrentSessionId;
            public string CurrentGameSeed;
            public long SessionStartTimeTicks;
            public int SessionBallIndex;

            public static SerializablePlayerState FromServerState(ServerPlayerState state)
            {
                return new SerializablePlayerState
                {
                    PlayerId = state.PlayerId,
                    WalletBalance = state.WalletBalance,
                    TotalEarned = state.TotalEarned,
                    CurrentSessionId = state.CurrentSessionId,
                    CurrentGameSeed = state.CurrentGameSeed,
                    SessionStartTimeTicks = state.SessionStartTime.Ticks,
                    SessionBallIndex = state.SessionBallIndex
                };
            }

            public ServerPlayerState ToServerState()
            {
                return new ServerPlayerState
                {
                    PlayerId = PlayerId,
                    WalletBalance = WalletBalance,
                    TotalEarned = TotalEarned,
                    CurrentSessionId = CurrentSessionId,
                    CurrentGameSeed = CurrentGameSeed,
                    SessionStartTime = new DateTime(SessionStartTimeTicks, DateTimeKind.Utc),
                    SessionBallIndex = SessionBallIndex,
                    ValidatedBallIndices = new HashSet<int>()
                };
            }
        }
    }

    [Serializable]
    public class SessionData
    {
        public string SessionId;
        public string PlayerId;
        public string GameSeed;
        public DateTime StartTime;
        public DateTime ExpiryTime;
        public int InitialBallCount;
        public long CurrentWalletBalance;
        public float RemainingSeconds;
    }

    [Serializable]
    public class WalletSyncResponse
    {
        public bool Success;
        public long ServerBalance;
        public long TotalEarned;
        public string ErrorMessage;
    }

    public class ServerException : Exception
    {
        public ServerException(string message) : base(message) { }
    }
}
