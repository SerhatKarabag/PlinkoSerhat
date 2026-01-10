using System;
using System.Threading.Tasks;
using Plinko.Data;
using Plinko.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Plinko.Core.StateMachine
{
    public abstract class BaseGameState : IGameState
    {
        protected readonly GameManager GameManager;
        protected readonly GameConfig Config;

        public abstract GameStateType StateType { get; }

        protected BaseGameState(GameManager gameManager, GameConfig config)
        {
            GameManager = gameManager;
            Config = config;
        }

        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update(float deltaTime) { }
        public virtual void FixedUpdate(float fixedDeltaTime) { }
    }

    public class InitializingState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.Initializing;

        public event Action OnInitializationComplete;
        public event Action<string> OnInitializationError;

        public InitializingState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public override void Enter()
        {
            _ = InitializeAsync().WithLogging(nameof(InitializingState));
        }

        private async Task InitializeAsync()
        {
            try
            {
                GameManager.LoadPlayerData();
                await GameManager.InitializeServerSession();
                OnInitializationComplete?.Invoke();
            }
            catch (Exception e)
            {
                OnInitializationError?.Invoke(e.Message);
                throw;
            }
        }
    }

    public class PlayingState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.Playing;

        private float _holdTime;
        private float _lastSpawnTime;
        private bool _isHolding;
        private Camera _cachedCamera;

        public PlayingState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public override void Enter()
        {
            _holdTime = 0f;
            _lastSpawnTime = 0f;
            _isHolding = false;
            _cachedCamera = Camera.main;

            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }

            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        public override void Exit()
        {
            _isHolding = false;
        }

        public override void Update(float deltaTime)
        {
            HandleInput(deltaTime);

            if (GameManager.ShouldLevelUp())
            {
                GameManager.StateMachine.TransitionTo(GameStateType.LevelTransition);
                return;
            }

            if (GameManager.CurrentBallCount <= 0 && GameManager.ActiveBallCount == 0)
            {
                GameManager.StateMachine.TransitionTo(GameStateType.RunEnding);
                return;
            }

            if (GameManager.IsSessionExpired())
            {
                GameManager.StateMachine.TransitionTo(GameStateType.RunEnding);
            }
        }

        public override void FixedUpdate(float fixedDeltaTime)
        {
            GameManager.CheckBallsForDespawn();
        }

        private void HandleInput(float deltaTime)
        {
            bool inputHeld = false;

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                inputHeld = true;
            }

            if (Touch.activeTouches.Count > 0)
            {
                inputHeld = true;
            }

            if (inputHeld)
            {
                _holdTime += deltaTime;

                if (_holdTime - _lastSpawnTime >= Config.BallSpawnCooldown)
                {
                    if (GameManager.CanSpawnBall())
                    {
                        Vector2 spawnPos = GetSpawnPosition();
                        GameManager.SpawnBall(spawnPos);
                        _lastSpawnTime = _holdTime;
                    }
                }

                _isHolding = true;
            }
            else
            {
                if (_isHolding)
                {
                    _holdTime = 0f;
                    _lastSpawnTime = 0f;
                }
                _isHolding = false;
            }
        }

        private Vector2 GetSpawnPosition()
        {
            Vector2 inputPos = Vector2.zero;

            if (Touch.activeTouches.Count > 0)
            {
                inputPos = Touch.activeTouches[0].screenPosition;
            }
            else if (Mouse.current != null)
            {
                inputPos = Mouse.current.position.ReadValue();
            }

            if (_cachedCamera != null)
            {
                Vector3 worldPos = _cachedCamera.ScreenToWorldPoint(new Vector3(inputPos.x, inputPos.y, 0));
                return new Vector2(worldPos.x, GameManager.SpawnYPosition);
            }

            return new Vector2(0, GameManager.SpawnYPosition);
        }
    }

    public class LevelTransitionState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.LevelTransition;

        public event Action<int> OnLevelTransitionStart;
        public event Action<int> OnLevelTransitionComplete;

        private float _transitionTimer;
        private int _newLevel;
        private bool _transitionComplete;

        public LevelTransitionState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public override void Enter()
        {
            _newLevel = GameManager.CurrentLevel + 1;
            _transitionTimer = 0f;
            _transitionComplete = false;

            OnLevelTransitionStart?.Invoke(_newLevel);
        }

        public override void Update(float deltaTime)
        {
            _transitionTimer += deltaTime;

            if (_transitionTimer >= Config.LevelTransitionDuration && GameManager.ActiveBallCount == 0 && !_transitionComplete)
            {
                _transitionComplete = true;
                GameManager.ApplyLevelUp(_newLevel);
                OnLevelTransitionComplete?.Invoke(_newLevel);
                GameManager.StateMachine.TransitionTo(GameStateType.Playing);
            }
        }

        public override void FixedUpdate(float fixedDeltaTime)
        {
            GameManager.CheckBallsForDespawn();
        }
    }

    public class WaitingState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.Waiting;

        public event Action<float> OnWaitTimeUpdated;
        public event Action OnSessionResetReady;

        private float _waitTimer;
        private bool _resetTriggered;

        public WaitingState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public override void Enter()
        {
            _waitTimer = 0f;
            _resetTriggered = false;
            _ = GameManager.FlushPendingRewardsAsync().WithLogging($"{nameof(WaitingState)}.Enter");
        }

        public override void Update(float deltaTime)
        {
            _waitTimer += deltaTime;

            float remainingTime = GameManager.GetTimeUntilNextSession();

            if (remainingTime <= 0 && !_resetTriggered)
            {
                _resetTriggered = true;
                OnSessionResetReady?.Invoke();
                _ = GameManager.StartNewSessionAsync().WithLogging($"{nameof(WaitingState)}.StartNewSession");
                return;
            }

            if (!_resetTriggered)
            {
                OnWaitTimeUpdated?.Invoke(remainingTime);
            }
        }
    }

    public class PausedState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.Paused;

        private GameStateType _previousState;

        public PausedState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public void SetPreviousState(GameStateType stateType)
        {
            _previousState = stateType;
        }

        public GameStateType GetPreviousState() => _previousState;

        public override void Enter()
        {
            Time.timeScale = 0f;
        }

        public override void Exit()
        {
            Time.timeScale = 1f;
        }
    }

    public class ErrorState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.Error;

        public event Action<string> OnErrorDisplayRequired;

        private string _errorMessage = string.Empty;

        public ErrorState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public void SetError(string message)
        {
            _errorMessage = message;
        }

        public override void Enter()
        {
            OnErrorDisplayRequired?.Invoke(_errorMessage);
        }
    }

    public class RunEndingState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.RunEnding;

        public event Action OnRunEndingStarted;
        public event Action OnRunEndingComplete;

        private bool _transitionPending;

        public RunEndingState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public override void Enter()
        {
            GameManager.PauseSessionTimer();
            _ = GameManager.FlushPendingRewardsAsync()
                .WithLogging($"{nameof(RunEndingState)}.Enter");

            OnRunEndingStarted?.Invoke();
            OnRunEndingComplete?.Invoke();

            _transitionPending = true;
        }

        public override void Update(float deltaTime)
        {
            if (_transitionPending)
            {
                _transitionPending = false;
                GameManager.StateMachine.TransitionTo(GameStateType.RunFinished);
            }
        }

        public override void Exit()
        {
            _transitionPending = false;
        }
    }

    public class RunFinishedState : BaseGameState
    {
        public override GameStateType StateType => GameStateType.RunFinished;

        public event Action<RunSummary> OnRunFinished;
        public event Action OnRestartRequested;

        private RunSummary _summary;

        public RunFinishedState(GameManager gameManager, GameConfig config) : base(gameManager, config) { }

        public void SetRunSummary(RunSummary summary)
        {
            _summary = summary;
        }

        public override void Enter()
        {
            _summary?.FinalizeRun();
            OnRunFinished?.Invoke(_summary);
        }

        public void RequestRestart()
        {
            OnRestartRequested?.Invoke();
        }
    }
}
