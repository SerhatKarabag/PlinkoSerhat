using System;
using System.Collections.Generic;
using Plinko.Core.Logging;

namespace Plinko.Core.StateMachine
{
    // Central state machine managing game flow.
    public class GameStateMachine
    {
        public event Action<GameStateType, GameStateType> OnStateChanged;

        private readonly Dictionary<GameStateType, IGameState> _states;
        private readonly ILogger _logger;
        private IGameState _currentState;
        private bool _isTransitioning;

        public GameStateType CurrentStateType => _currentState?.StateType ?? GameStateType.Initializing;
        public bool IsPlaying => CurrentStateType == GameStateType.Playing;
        public bool IsTransitioning => _isTransitioning;

        public GameStateMachine(ILogger logger)
        {
            _states = new Dictionary<GameStateType, IGameState>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterState(IGameState state)
        {
            if (state == null)
            {
                _logger.LogError("[StateMachine] Cannot register null state");
                return;
            }

            if (_states.ContainsKey(state.StateType))
            {
                _logger.LogWarning($"[StateMachine] Overwriting existing state: {state.StateType}");
            }

            _states[state.StateType] = state;
        }

        public void SetInitialState(GameStateType stateType)
        {
            if (_states.TryGetValue(stateType, out var state))
            {
                _currentState = state;
                _currentState.Enter();
            }
            else
            {
                _logger.LogError($"[StateMachine] State not registered: {stateType}");
            }
        }

        public void TransitionTo(GameStateType newStateType)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[StateMachine] Already transitioning, ignoring request");
                return;
            }

            if (!_states.TryGetValue(newStateType, out var newState))
            {
                _logger.LogError($"[StateMachine] State not registered: {newStateType}");
                return;
            }

            if (_currentState?.StateType == newStateType)
            {
                return;
            }

            _isTransitioning = true;

            var previousStateType = _currentState?.StateType ?? GameStateType.Initializing;
            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();

            _isTransitioning = false;
            OnStateChanged?.Invoke(previousStateType, newStateType);
        }

        public void Update(float deltaTime)
        {
            if (!_isTransitioning)
            {
                _currentState?.Update(deltaTime);
            }
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            if (!_isTransitioning)
            {
                _currentState?.FixedUpdate(fixedDeltaTime);
            }
        }

        public T GetState<T>() where T : class, IGameState
        {
            foreach (var state in _states.Values)
            {
                if (state is T typedState)
                    return typedState;
            }
            return null;
        }
    }
}