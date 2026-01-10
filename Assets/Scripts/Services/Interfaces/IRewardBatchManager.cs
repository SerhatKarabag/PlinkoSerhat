using System;
using System.Threading.Tasks;
using Plinko.Data;

namespace Plinko.Services
{
    // Interface for reward batching and validation management.
    public interface IRewardBatchManager
    {
        event Action<RewardBatch>? OnBatchCreated;
        event Action<RewardBatch, BatchValidationResponse>? OnBatchValidated;
        event Action<RewardBatch, string>? OnBatchFailed;
        event Action<long>? OnWalletUpdated;
        event Action<int, long>? OnEntriesRejected;

    
        long OptimisticBalance { get; }
        long VerifiedBalance { get; }
        long PendingRewards { get; }
        int FailedBatchCount { get; }
        int PendingBatchCount { get; }
        long FailedBatchRewards { get; }


        void InitializeSession(string playerId, int sessionId, string gameSeed, long currentBalance);
        void AddReward(int ballIndex, int bucketIndex, int totalBucketCount, long reward, int level, float dropPositionX);
        void Tick(float deltaTime);
        Task FlushAsync();
        Task RetryFailedBatchesAsync();
        Task<bool> ForceSyncWalletAsync();
        void Dispose();
    }
}