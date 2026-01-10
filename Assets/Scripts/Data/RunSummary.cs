using System;

namespace Plinko.Data
{
    [Serializable]
    public class RunSummary
    {
        public long TotalPointsEarned;
        public long VerifiedPoints;
        public long PendingPoints;
        public int RejectedBallCount;
        public long RejectedPoints;
        public int RetryingBatchCount;
        public long RetryingPoints;
        public int TotalBallsDropped;
        public int BallsScored;
        public int HighestLevelReached;
        public DateTime RunStartTime;
        public DateTime RunEndTime;

        public RunSummary()
        {
            Reset();
        }

        public void Reset()
        {
            TotalPointsEarned = 0;
            VerifiedPoints = 0;
            PendingPoints = 0;
            RejectedBallCount = 0;
            RejectedPoints = 0;
            RetryingBatchCount = 0;
            RetryingPoints = 0;
            TotalBallsDropped = 0;
            BallsScored = 0;
            HighestLevelReached = 0;
            RunStartTime = DateTime.UtcNow;
            RunEndTime = DateTime.MinValue;
        }

        public void OnBallDropped()
        {
            TotalBallsDropped++;
        }

        public void OnBallScored(long reward)
        {
            BallsScored++;
            TotalPointsEarned += reward;
        }

        public void OnLevelReached(int level)
        {
            if (level > HighestLevelReached)
            {
                HighestLevelReached = level;
            }
        }

        public void OnBatchValidated(long verifiedAmount)
        {
            VerifiedPoints += verifiedAmount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[RunSummary] OnBatchValidated: +{verifiedAmount}, cumulative VerifiedPoints={VerifiedPoints}");
#endif
        }

        public void OnBatchRejected(int rejectedEntryCount, long rejectedAmount)
        {
            RejectedBallCount += rejectedEntryCount;
            RejectedPoints += rejectedAmount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[RunSummary] OnBatchRejected: +{rejectedEntryCount} balls, +{rejectedAmount} points. " +
                $"Cumulative: RejectedBallCount={RejectedBallCount}, RejectedPoints={RejectedPoints}");
#endif
        }

        public void UpdatePendingPoints(long pending)
        {
            PendingPoints = pending;
        }

        public void UpdateRetryingBatches(int count, long points)
        {
            RetryingBatchCount = count;
            RetryingPoints = points;
        }

        public void FinalizeRun()
        {
            RunEndTime = DateTime.UtcNow;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long accounted = VerifiedPoints + PendingPoints + RejectedPoints;
            long gap = TotalPointsEarned - accounted;
            UnityEngine.Debug.Log($"[RunSummary] FinalizeRun: Total={TotalPointsEarned}, " +
                $"Verified={VerifiedPoints}, Pending={PendingPoints} (incl. {RetryingBatchCount} retrying batches), " +
                $"RejectedBalls={RejectedBallCount}, RejectedPts={RejectedPoints}, " +
                $"Accounted={accounted}, Gap={gap}");
#endif
        }

        public TimeSpan RunDuration => RunEndTime > RunStartTime
            ? RunEndTime - RunStartTime
            : DateTime.UtcNow - RunStartTime;

        public bool HasRejections => RejectedBallCount > 0;
        public bool HasRetryingBatches => RetryingBatchCount > 0;
    }
}