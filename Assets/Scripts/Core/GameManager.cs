using System;
using System.Threading.Tasks;
using UnityEngine;
using Plinko.Core.StateMachine;
using Plinko.Data;
using Plinko.Physics;
using Plinko.Services;
using Plinko.UI;
using Plinko.Utils;
using CoreLogger = Plinko.Core.Logging.ILogger;

namespace Plinko.Core
{
    public class GameManager : MonoBehaviour
    {
        private bool _isInitialized;

        [Header("Configuration")]
        [SerializeField] private GameConfig _config;
        private const string MissingConfigMessage = "Missing GameConfig.";

        [Header("References")]
        [SerializeField] private PlinkoBoard _board;
        [SerializeField] private BallManager _ballManager;
        [SerializeField] private UIManager _uiManager;

        public GameStateMachine StateMachine { get; private set; }
        public GameConfig Config => _config;

        private IServerService _serverService;
        private ISessionManager _sessionManager;
        private IRewardBatchManager _rewardBatchManager;
        private CoreLogger _logger;
        private PlayerData _playerData;
        private GameEventWiring _eventWiring;
        private FPSMonitor _fpsMonitor;

        private string _gameSeed;
        private int _sessionIdCounter;
        private RunSummary _currentRunSummary;
        private bool _sessionTimerPaused;

        public int CurrentLevel => _playerData?.CurrentLevel ?? 0;
        public int CurrentBallCount => _playerData?.CurrentBallCount ?? 0;
        public int ActiveBallCount => _ballManager?.ActiveBallCount ?? 0;
        public float SpawnYPosition => _board?.SpawnY ?? 5f;

        public void Initialize(
            GameConfig config,
            IServerService serverService,
            ISessionManager sessionManager,
            IRewardBatchManager rewardBatchManager,
            CoreLogger logger)
        {
            if (_isInitialized)
            {
                _logger?.LogWarning($"[{nameof(GameManager)}] Already initialized.");
                return;
            }

            if (config == null)
            {
                _uiManager?.ShowError(MissingConfigMessage);
                enabled = false;
                return;
            }

            if (serverService == null || sessionManager == null || rewardBatchManager == null)
            {
                logger?.LogError($"[{nameof(GameManager)}] Missing injected services.");
                enabled = false;
                return;
            }

            if (logger == null)
            {
                enabled = false;
                return;
            }

            _config = config;
            _serverService = serverService;
            _sessionManager = sessionManager;
            _rewardBatchManager = rewardBatchManager;
            _logger = logger;

            InitializeSystems();
            _isInitialized = true;
        }

        private void InitializeSystems()
        {
            StateMachine = new GameStateMachine(_logger);

            var initState = new InitializingState(this, _config);
            var playingState = new PlayingState(this, _config);
            var levelTransitionState = new LevelTransitionState(this, _config);
            var runEndingState = new RunEndingState(this, _config);
            var runFinishedState = new RunFinishedState(this, _config);
            var waitingState = new WaitingState(this, _config);
            var pausedState = new PausedState(this, _config);
            var errorState = new ErrorState(this, _config);

            RegisterStates(
                initState,
                playingState,
                levelTransitionState,
                runEndingState,
                runFinishedState,
                waitingState,
                pausedState,
                errorState
            );

            _eventWiring = new GameEventWiring(
                this, _config, _uiManager, _ballManager, _board,
                _sessionManager, _rewardBatchManager, StartNewSessionInternal, _logger
            );

            _eventWiring.WireEvents(
                initState, levelTransitionState, runEndingState,
                runFinishedState, waitingState, errorState
            );

            InitializeBoard();
            InitializeBallManager();

            _fpsMonitor = new FPSMonitor(45f, 55f, 1f);
        }

        private void RegisterStates(params IGameState[] states)
        {
            foreach (var state in states)
            {
                StateMachine.RegisterState(state);
            }
        }

        private void InitializeBoard()
        {
            if (_board == null) return;

            _board.Initialize(_config);
            _serverService.SetSpawnBoundaries(_board.LeftBoundary, _board.RightBoundary);
        }

        private void InitializeBallManager()
        {
            if (_ballManager == null) return;

            _ballManager.Initialize(_config, _board);
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                _logger?.LogError($"[{nameof(GameManager)}] Not initialized. Ensure GameBootstrapper runs.");
                enabled = false;
                return;
            }

            if (_config == null)
            {
                _uiManager?.ShowError(MissingConfigMessage);
                enabled = false;
                return;
            }

            _uiManager?.ShowLoading("Connecting...");
            StateMachine.SetInitialState(GameStateType.Initializing);
        }

        private void Update()
        {
            if (StateMachine == null) return;

            float deltaTime = Time.deltaTime;
            StateMachine.Update(deltaTime);

            if (!_sessionTimerPaused)
            {
                _sessionManager?.Tick(deltaTime);
            }

            _rewardBatchManager?.Tick(Time.unscaledDeltaTime);
            _fpsMonitor?.Tick(deltaTime);
        }

        private void FixedUpdate()
        {
            StateMachine?.FixedUpdate(Time.fixedDeltaTime);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (StateMachine == null) return;

            if (pauseStatus)
            {
                _ = HandleAppPause().WithLogging("GameManager.HandleAppPause");
                return;
            }

            _ = HandleAppResume().WithLogging("GameManager.HandleAppResume");
        }

        private async Task HandleAppPause()
        {
            _playerData?.UpdateRunSummary(_currentRunSummary);
            _playerData?.SaveImmediate();
            var flushTask = FlushPendingRewardsAsync();

            var pausedState = StateMachine.GetState<PausedState>();
            pausedState?.SetPreviousState(StateMachine.CurrentStateType);
            StateMachine.TransitionTo(GameStateType.Paused);

            await flushTask;
        }

        private async Task HandleAppResume()
        {
            _fpsMonitor?.Reset();
            if (_eventWiring != null)
            {
                await _eventWiring.HandleAppResume();
            }
        }

        private void OnApplicationQuit()
        {
            _playerData?.UpdateRunSummary(_currentRunSummary);
            _playerData?.SaveImmediate();
            _rewardBatchManager?.Dispose();
            _eventWiring?.Dispose();
        }

        private void OnDestroy()
        {
            _eventWiring?.Dispose();
        }

        public void LoadPlayerData()
        {
            _playerData = PlayerData.Load(_config);
        }

        public async Task InitializeServerSession()
        {
            bool hasExistingSession = _sessionManager.TryRestoreSession();

            if (hasExistingSession)
            {
                try
                {
                    var session = await _sessionManager.SyncSessionAsync(_playerData.PlayerId, _playerData.WalletBalance);
                    OnSessionReady(session, isRestoredSession: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[{nameof(GameManager)}] Failed to sync existing session, starting new: {ex.Message}");
                    await StartNewSessionInternal();
                }
            }
            else
            {
                await StartNewSessionInternal();
            }
        }

        private async Task StartNewSessionInternal()
        {
            var session = await _sessionManager.StartNewSessionAsync(_playerData.PlayerId);
            OnSessionReady(session);
        }

        private void OnSessionReady(SessionData session, bool isRestoredSession = false)
        {
            _gameSeed = session.GameSeed;
            _sessionIdCounter++;

            long walletBalance;
            if (isRestoredSession && _playerData.HasActiveSession)
            {
                _playerData.ContinueSession();
                walletBalance = _playerData.WalletBalance;
            }
            else
            {
                _playerData.ResetSession(_config.InitialBallCount);
                walletBalance = session.CurrentWalletBalance;
            }

            _rewardBatchManager.InitializeSession(
                _playerData.PlayerId, _sessionIdCounter, _gameSeed, walletBalance
            );

            if (isRestoredSession && _playerData.TryGetRunSummary(out var restoredSummary))
            {
                _currentRunSummary = restoredSummary;
            }
            else
            {
                _currentRunSummary = new RunSummary();
            }
            _playerData.UpdateRunSummary(_currentRunSummary);
            _playerData.Save();
            _sessionTimerPaused = false;

            _eventWiring?.UpdateStateReferences(_playerData, _currentRunSummary);

            _ballManager?.SetLevel(_playerData.CurrentLevel);
            _board?.ApplyLevelConfig(_playerData.CurrentLevel);

            _uiManager?.HideLoading();
            _uiManager?.HideRunEnd();
            _uiManager?.SetBallCount(_playerData.CurrentBallCount);
            _uiManager?.SetLevel(_playerData.CurrentLevel);
            _uiManager?.SetWallet(_rewardBatchManager.VerifiedBalance, _rewardBatchManager.PendingRewards);
        }

        public bool CanSpawnBall()
        {
            return StateMachine.IsPlaying
                && _playerData.CurrentBallCount > 0
                && ActiveBallCount < _config.MaxSimultaneousBalls;
        }

        public void SpawnBall(Vector2 position)
        {
            if (!CanSpawnBall()) return;

            var ball = _ballManager.SpawnBall(position);
            if (ball != null)
            {
                _playerData.CurrentBallCount--;
                _playerData.BallsDroppedThisLevel++;
                _playerData.TotalBallsDroppedThisSession++;

                _currentRunSummary?.OnBallDropped();
                _playerData.UpdateRunSummary(_currentRunSummary);
                _playerData.Save();
                _uiManager?.SetBallCount(_playerData.CurrentBallCount);
            }
        }

        public bool ShouldLevelUp()
        {
            return _playerData.BallsDroppedThisLevel >= _config.BallsPerLevel
                && _playerData.CurrentLevel < _config.Levels.Length - 1;
        }

        public void ApplyLevelUp(int newLevel)
        {
            _playerData.CurrentLevel = newLevel;
            _playerData.BallsDroppedThisLevel = 0;

            _ballManager.SetLevel(newLevel);
            _board.ApplyLevelConfig(newLevel);
            _uiManager?.SetLevel(newLevel);
        }

        public bool IsSessionExpired() => _sessionManager.IsSessionExpired;
        public float GetTimeUntilNextSession() => _sessionManager.GetTimeUntilNextSession();
        public void CheckBallsForDespawn() => _ballManager?.CheckBallsDespawn();
        public Task FlushPendingRewards()
        {
            return FlushPendingRewardsAsync().WithLogging("GameManager.FlushPendingRewards");
        }

        public async Task FlushPendingRewardsAsync()
        {
            if (_rewardBatchManager != null)
            {
                await _rewardBatchManager.FlushAsync();
            }
        }

        public void PauseSessionTimer() => _sessionTimerPaused = true;

        public async Task StartNewSessionAsync()
        {
            _uiManager?.ShowLoading("Starting new session...");

            try
            {
                await StartNewSessionInternal();
                StateMachine.TransitionTo(GameStateType.Playing);
            }
            catch (Exception e)
            {
                var errorState = StateMachine.GetState<ErrorState>();
                errorState?.SetError(e.Message);
                StateMachine.TransitionTo(GameStateType.Error);
                throw;
            }
        }
    }
}
