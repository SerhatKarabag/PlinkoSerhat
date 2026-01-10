using System;
using System.Threading;
using System.Threading.Tasks;
using Plinko.Data;

namespace Plinko.Services
{
    // Interface for server communication services.
    public interface IServerService
    {
        event Action<BatchValidationResponse>? OnBatchValidated;
        event Action<string>? OnServerError;
        event Action<SessionData>? OnSessionStarted;
        event Action<SessionData>? OnSessionSynced;

        // Set spawn boundaries for anti-cheat plausibility checks.
        void SetSpawnBoundaries(float left, float right);

        // Start a new game session.
        Task<SessionData> StartSessionAsync(string playerId, CancellationToken ct = default);

        // Sync session state with server.
        // Client's locally stored wallet balance because there is no real serverfor this case study (restore after app restart)
        Task<SessionData> SyncSessionAsync(string playerId, string sessionId, long clientWalletBalance = 0, CancellationToken ct = default);

        // Validate a batch of rewards.
        Task<BatchValidationResponse> ValidateBatchAsync(RewardBatch batch, CancellationToken ct = default);

        // Get current wallet balance.
        Task<long> GetWalletBalanceAsync(string playerId, CancellationToken ct = default);

        // Force sync wallet balance with server.
        Task<WalletSyncResponse> ForceSyncWalletAsync(string playerId, CancellationToken ct = default);
    }
}
