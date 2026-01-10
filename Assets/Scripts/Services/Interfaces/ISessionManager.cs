using System;
using System.Threading.Tasks;

namespace Plinko.Services
{
    // Interface for session management.
    // Handles session timing, persistence, and server synchronization
    public interface ISessionManager
    {
        event Action? OnSessionExpired;
        event Action<float>? OnTimerUpdated;
        event Action<SessionData>? OnSessionStarted;
        event Action<SessionData>? OnSessionResumed;

        float SessionDurationSeconds { get; }
        float RemainingSeconds { get; }
        bool IsSessionExpired { get; }
        string CurrentSessionId { get; }
        string GameSeed { get; }
        DateTime SessionStartTime { get; }

        // Check if there's an existing valid session from before app close.
        bool TryRestoreSession();

        // Start a new session via server.
        Task<SessionData> StartNewSessionAsync(string playerId);

        // Sync session with server (used on app resume)
        // Client's locally stored wallet balance because there is no real server for this case study (restore after app restart)
        Task<SessionData> SyncSessionAsync(string playerId, long clientWalletBalance);

        // Update timer. Call from Update().
        void Tick(float deltaTime);

        // Calculate time until next session can start.
        float GetTimeUntilNextSession();

        // Format remaining time as MM:SS string.
        string GetFormattedTimeRemaining();

        // End current session.
        void EndSession();
    }
}