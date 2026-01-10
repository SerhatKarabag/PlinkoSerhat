using System;
using System.Collections.Generic;

namespace Plinko.Data
{
    // - Rewards are batched and sent to server periodically
    // - Server validates the batch using recorded bucket outcomes
    // - Final wallet balance comes from server
    [Serializable]
    public class RewardBatch
    {
        public string BatchId;
        public string PlayerId;
        public int SessionId;
        public List<RewardEntry> Entries;
        public long TotalClientCalculatedReward;
        public long BatchCreatedTimestamp;
        public RewardBatchStatus Status;

        public string GameSeed;
        public int StartingBallIndex;

        public int RetryCount;

        public RewardBatch(string playerId, int sessionId, string gameSeed, int startingBallIndex)
        {
            BatchId = Guid.NewGuid().ToString();
            PlayerId = playerId;
            SessionId = sessionId;
            GameSeed = gameSeed;
            StartingBallIndex = startingBallIndex;
            Entries = new List<RewardEntry>();
            TotalClientCalculatedReward = 0;
            BatchCreatedTimestamp = DateTime.UtcNow.Ticks;
            Status = RewardBatchStatus.Pending;
            RetryCount = 0;
        }

        public void AddEntry(RewardEntry entry)
        {
            Entries.Add(entry);
            TotalClientCalculatedReward += entry.RewardAmount;
        }

        public bool IsFull(int maxSize) => Entries.Count >= maxSize;
        public bool CanRetry(int maxRetries) => RetryCount < maxRetries;
    }

    [Serializable]
    public struct RewardEntry
    {
        public int BallIndex;        // Which ball in sequence 
        public int BucketIndex;      // Which bucket it landed in
        public int TotalBucketCount; // Total buckets on board
        public long RewardAmount;    // Client-calculated reward
        public int Level;            // Level when this reward was earned
        public float DropPositionX;  // Where ball was dropped
    }

    public enum RewardBatchStatus
    {
        Pending,        // Not yet sent to server
        Sending,        // Currently being sent
        Validated,      // Server validated successfully
        Rejected,       // Server rejected (possible tampering)
        Error           // Network or other error
    }

    // Server response
    [Serializable]
    public class BatchValidationResponse
    {
        public string BatchId;
        public bool IsValid;
        public bool IsRetryable;              // True for transient errors (timeout, network), false for conclusive rejection
        public long ServerCalculatedReward;
        public long NewWalletBalance;
        public string ErrorMessage;
        public List<int> InvalidEntryIndices; // Which entries failed validation
    }
}