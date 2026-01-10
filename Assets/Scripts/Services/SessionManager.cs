using System;
using System.Threading.Tasks;
using Plinko.Data;
using UnityEngine;
using CoreLogger = Plinko.Core.Logging.ILogger;

namespace Plinko.Services
{
    // Manages session lifecycle, synchronization, and timer state.
    public class SessionManager : ISessionManager
    {
        public event Action? OnSessionExpired;
        public event Action<float>? OnTimerUpdated;
        public event Action<SessionData>? OnSessionStarted;
        public event Action<SessionData>? OnSessionResumed;

        private const string SESSION_START_KEY = "Plinko_SessionStartTime";
        private const string SESSION_ID_KEY = "Plinko_SessionId";

        private readonly GameConfig _config;
        private readonly IServerService _serverService;
        private readonly IPreferences _preferences;
        private readonly CoreLogger _logger;

        private DateTime _sessionStartTime;
        private string _sessionId = string.Empty;
        private string _gameSeed = string.Empty;
        private bool _isSessionActive;
        private float _updateTimer;

        public float SessionDurationSeconds => _config.SessionDurationSeconds;
        public float RemainingSeconds { get; private set; }
        public bool IsSessionExpired => RemainingSeconds <= 0;
        public string CurrentSessionId => _sessionId;
        public string GameSeed => _gameSeed;
        public DateTime SessionStartTime => _sessionStartTime;

        public SessionManager(
            GameConfig config,
            IServerService serverService,
            IPreferences preferences,
            CoreLogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serverService = serverService ?? throw new ArgumentNullException(nameof(serverService));
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryRestoreSession()
        {
            if (!_preferences.HasKey(SESSION_START_KEY))
            {
                return false;
            }

            try
            {
                long ticks = long.Parse(_preferences.GetString(SESSION_START_KEY));
                _sessionStartTime = new DateTime(ticks, DateTimeKind.Utc);
                _sessionId = _preferences.GetString(SESSION_ID_KEY, "");

                var elapsed = DateTime.UtcNow - _sessionStartTime;
                RemainingSeconds = (float)(_config.SessionDurationSeconds - elapsed.TotalSeconds);

                if (RemainingSeconds > 0)
                {
                    _isSessionActive = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[{nameof(SessionManager)}] TryRestoreSession failed: {ex.Message}");
            }

            return false;
        }

        public async Task<SessionData> StartNewSessionAsync(string playerId)
        {
            try
            {
                var sessionData = await _serverService.StartSessionAsync(playerId);

                _sessionId = sessionData.SessionId;
                _sessionStartTime = sessionData.StartTime;
                _gameSeed = sessionData.GameSeed;
                RemainingSeconds = (float)(sessionData.ExpiryTime - DateTime.UtcNow).TotalSeconds;
                _isSessionActive = true;

                SaveSessionState();

                OnSessionStarted?.Invoke(sessionData);
                return sessionData;
            }
            catch (ServerException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{nameof(SessionManager)}] StartNewSessionAsync failed: {ex.Message}");
                throw new ServerException("Failed to connect to server. Please try again.");
            }
        }

        public async Task<SessionData> SyncSessionAsync(string playerId, long clientWalletBalance)
        {
            try
            {
                var sessionData = await _serverService.SyncSessionAsync(playerId, _sessionId, clientWalletBalance);

                if (sessionData.SessionId != _sessionId)
                {
                    _sessionId = sessionData.SessionId;
                    _sessionStartTime = sessionData.StartTime;
                    _gameSeed = sessionData.GameSeed;
                    OnSessionStarted?.Invoke(sessionData);
                }
                else
                {
                    OnSessionResumed?.Invoke(sessionData);
                }

                if (sessionData.RemainingSeconds < 0)
                {
                    var elapsed = DateTime.UtcNow - _sessionStartTime;
                    RemainingSeconds = (float)(_config.SessionDurationSeconds - elapsed.TotalSeconds);
                }
                else if (sessionData.RemainingSeconds > 0)
                {
                    RemainingSeconds = sessionData.RemainingSeconds;
                }
                else
                {
                    RemainingSeconds = (float)(sessionData.ExpiryTime - DateTime.UtcNow).TotalSeconds;
                }

                _isSessionActive = RemainingSeconds > 0;

                SaveSessionState();
                return sessionData;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[{nameof(SessionManager)}] SyncSessionAsync failed: {ex.Message}");

                if (_isSessionActive)
                {
                    var elapsed = DateTime.UtcNow - _sessionStartTime;
                    RemainingSeconds = (float)(_config.SessionDurationSeconds - elapsed.TotalSeconds);

                    if (RemainingSeconds > 0)
                    {
                        return new SessionData
                        {
                            SessionId = _sessionId,
                            PlayerId = playerId,
                            GameSeed = _gameSeed,
                            StartTime = _sessionStartTime,
                            ExpiryTime = _sessionStartTime.AddSeconds(_config.SessionDurationSeconds),
                            RemainingSeconds = RemainingSeconds
                        };
                    }
                }

                throw;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_isSessionActive) return;

            RemainingSeconds -= deltaTime;

            _updateTimer += deltaTime;
            if (_updateTimer >= _config.SessionTimerUpdateInterval)
            {
                _updateTimer = 0f;
                OnTimerUpdated?.Invoke(Mathf.Max(0, RemainingSeconds));
            }

            if (RemainingSeconds <= 0 && _isSessionActive)
            {
                _isSessionActive = false;
                ClearSessionState();
                OnSessionExpired?.Invoke();
            }
        }

        public float GetTimeUntilNextSession()
        {
            if (_isSessionActive)
            {
                return Mathf.Max(0, RemainingSeconds);
            }
            return 0;
        }

        public string GetFormattedTimeRemaining()
        {
            float seconds = Mathf.Max(0, RemainingSeconds);
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        public void EndSession()
        {
            _isSessionActive = false;
            RemainingSeconds = 0;
            ClearSessionState();
            OnSessionExpired?.Invoke();
        }

        private void SaveSessionState()
        {
            _preferences.SetString(SESSION_START_KEY, _sessionStartTime.Ticks.ToString());
            _preferences.SetString(SESSION_ID_KEY, _sessionId);
            _preferences.Save();
        }

        private void ClearSessionState()
        {
            _preferences.DeleteKey(SESSION_START_KEY);
            _preferences.DeleteKey(SESSION_ID_KEY);
            _preferences.Save();
        }
    }
}
