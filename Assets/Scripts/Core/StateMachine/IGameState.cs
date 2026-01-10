namespace Plinko.Core.StateMachine
{
    // Interface for game states. Follows State pattern for clean state management.
    public interface IGameState
    {
        void Enter();
        void Exit();
        void Update(float deltaTime);
        void FixedUpdate(float fixedDeltaTime);
        GameStateType StateType { get; }
    }

    public enum GameStateType
    {
        Initializing,
        Playing,
        LevelTransition,
        RunEnding,
        RunFinished,
        Waiting,
        Paused,
        Error
    }
}