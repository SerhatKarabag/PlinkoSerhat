using System;
using System.Collections.Generic;

namespace Plinko.Services.AntiCheat
{
    // Tracks per-player/session statistics for anomaly detection.
    // Uses a rolling window approach to detect sustained suspicious patterns while allowing natural lucky streaks without false positives.
    public class PlayerAntiCheatStats
    {
        public string PlayerId { get; }
        public string SessionId { get; }
        public DateTime SessionStartTime { get; }

        // Core statistics
        public int TotalBallsValidated { get; private set; }
        public long TotalRewardsEarned { get; private set; }
        public int HighValueHits { get; private set; }
        public int ImplausibleOutcomes { get; private set; }
        public int SuspiciousFlags { get; private set; }

        // Rolling window for recent activity (prevents longterm averages from masking bursts)
        private readonly Queue<BallOutcome> _recentOutcomes;
        private readonly int _windowSize;

        // Rate limiting
        private readonly Queue<DateTime> _dropTimestamps;
        private int _rapidDropFlags;

        // Bucket distribution tracking
        private readonly Dictionary<int, int> _bucketHitCounts;

        public PlayerAntiCheatStats(string playerId, string sessionId, int windowSize = 100)
        {
            PlayerId = playerId;
            SessionId = sessionId;
            SessionStartTime = DateTime.UtcNow;
            _windowSize = windowSize;

            _recentOutcomes = new Queue<BallOutcome>();
            _dropTimestamps = new Queue<DateTime>();
            _bucketHitCounts = new Dictionary<int, int>();

            TotalBallsValidated = 0;
            TotalRewardsEarned = 0;
            HighValueHits = 0;
            ImplausibleOutcomes = 0;
            SuspiciousFlags = 0;
            _rapidDropFlags = 0;
        }

        /// <summary>
        /// Record a ball outcome for statistical tracking.
        /// </summary>
        public void RecordOutcome(
            int bucketIndex,
            long reward,
            bool isPlausible,
            bool isHighValue,
            float dropX)
        {
            TotalBallsValidated++;
            TotalRewardsEarned += reward;

            if (isHighValue)
                HighValueHits++;

            if (!isPlausible)
                ImplausibleOutcomes++;

            if (!_bucketHitCounts.ContainsKey(bucketIndex))
                _bucketHitCounts[bucketIndex] = 0;
            _bucketHitCounts[bucketIndex]++;

            var outcome = new BallOutcome
            {
                BucketIndex = bucketIndex,
                Reward = reward,
                IsPlausible = isPlausible,
                IsHighValue = isHighValue,
                DropX = dropX,
                Timestamp = DateTime.UtcNow
            };

            _recentOutcomes.Enqueue(outcome);

            while (_recentOutcomes.Count > _windowSize)
            {
                _recentOutcomes.Dequeue();
            }
        }

        public void RecordDropTimestamp(DateTime timestamp)
        {
            _dropTimestamps.Enqueue(timestamp);

            // Keep only last 60 seconds of timestamps
            var cutoff = timestamp.AddSeconds(-60);
            while (_dropTimestamps.Count > 0 && _dropTimestamps.Peek() < cutoff)
            {
                _dropTimestamps.Dequeue();
            }
        }

        public void AddSuspiciousFlag(string reason = null)
        {
            SuspiciousFlags++;
        }

        // Increment rapid drop flag count.
        public void AddRapidDropFlag()
        {
            _rapidDropFlags++;
            SuspiciousFlags++;
        }

       

        
        // Average reward per ball (all time in session).
        public double AverageReward => TotalBallsValidated > 0
            ? (double)TotalRewardsEarned / TotalBallsValidated
            : 0;

        // Highvalue hit rate (all time in session).
        public double HighValueHitRate => TotalBallsValidated > 0
            ? (double)HighValueHits / TotalBallsValidated
            : 0;

        // Implausible outcome rate (all time in session).
        public double ImplausibleRate => TotalBallsValidated > 0
            ? (double)ImplausibleOutcomes / TotalBallsValidated
            : 0;

        // Current ball drop rate (balls per minute in last 60 seconds).
        public int CurrentBallsPerMinute => _dropTimestamps.Count;

        // Check if player is dropping balls too fast.
        public bool IsRateLimitExceeded(int maxBallsPerMinute) => CurrentBallsPerMinute > maxBallsPerMinute;

        // Average reward over recent rolling window.
        public double RecentAverageReward
        {
            get
            {
                if (_recentOutcomes.Count == 0) return 0;

                long sum = 0;
                foreach (var outcome in _recentOutcomes)
                {
                    sum += outcome.Reward;
                }
                return (double)sum / _recentOutcomes.Count;
            }
        }

        // High-value hit rate over recent rolling window.
        public double RecentHighValueHitRate
        {
            get
            {
                if (_recentOutcomes.Count == 0) return 0;

                int count = 0;
                foreach (var outcome in _recentOutcomes)
                {
                    if (outcome.IsHighValue) count++;
                }
                return (double)count / _recentOutcomes.Count;
            }
        }

        // Implausible rate over recent rolling window.
        public double RecentImplausibleRate
        {
            get
            {
                if (_recentOutcomes.Count == 0) return 0;

                int count = 0;
                foreach (var outcome in _recentOutcomes)
                {
                    if (!outcome.IsPlausible) count++;
                }
                return (double)count / _recentOutcomes.Count;
            }
        }

        public Dictionary<int, double> GetBucketDistribution()
        {
            var distribution = new Dictionary<int, double>();

            if (TotalBallsValidated == 0) return distribution;

            foreach (var kvp in _bucketHitCounts)
            {
                distribution[kvp.Key] = (double)kvp.Value / TotalBallsValidated;
            }

            return distribution;
        }

        public bool HasSufficientData(int minSampleSize) => _recentOutcomes.Count >= minSampleSize;

        public AntiCheatStatsSummary GetSummary()
        {
            return new AntiCheatStatsSummary
            {
                PlayerId = PlayerId,
                SessionId = SessionId,
                TotalBalls = TotalBallsValidated,
                TotalRewards = TotalRewardsEarned,
                AverageReward = AverageReward,
                HighValueHits = HighValueHits,
                HighValueRate = HighValueHitRate,
                ImplausibleCount = ImplausibleOutcomes,
                ImplausibleRate = ImplausibleRate,
                SuspiciousFlags = SuspiciousFlags,
                RecentAvgReward = RecentAverageReward,
                RecentHighValueRate = RecentHighValueHitRate,
                BallsPerMinute = CurrentBallsPerMinute
            };
        }

      

        private struct BallOutcome
        {
            public int BucketIndex;
            public long Reward;
            public bool IsPlausible;
            public bool IsHighValue;
            public float DropX;
            public DateTime Timestamp;
        }
    }

    // Summary snapshot for logging and analysis.
    [Serializable]
    public class AntiCheatStatsSummary
    {
        public string PlayerId = string.Empty;
        public string SessionId = string.Empty;
        public int TotalBalls;
        public long TotalRewards;
        public double AverageReward;
        public int HighValueHits;
        public double HighValueRate;
        public int ImplausibleCount;
        public double ImplausibleRate;
        public int SuspiciousFlags;
        public double RecentAvgReward;
        public double RecentHighValueRate;
        public int BallsPerMinute;
    }
}
