using System;
using UnityEngine;

namespace Plinko.Services.AntiCheat
{
    // Configuration for anti-cheat plausibility checks and statistical detection.
    [Serializable]
    public class AntiCheatConfig
    {
        [Header("Plausibility Check Settings")]
        [Tooltip("How much deviation to allow from expected bucket (as fraction of total buckets). " +
                 "0.4 = allow ±40% of board width deviation. Higher = more lenient.")]
        [Range(0.2f, 0.6f)]
        public float PlausibilityDeviationMultiplier = 0.4f;

        [Tooltip("Minimum balls before plausibility checks affect validation. " +
                 "First N balls are always accepted to build baseline.")]
        public int PlausibilityGracePeriod = 10;

        [Header("Statistical Detection Settings")]
        [Tooltip("Minimum balls before statistical anomaly detection kicks in. " +
                 "Need enough data for reliable statistics.")]
        public int StatisticalSampleSize = 100;

        [Tooltip("How many times the expected average reward before flagging. " +
                 "2.5 = flag if player averages 2.5x expected over sample size.")]
        [Range(1.5f, 4.0f)]
        public float AverageRewardThreshold = 2.5f;

        [Tooltip("How many times the expected high-value bucket hit rate before flagging. " +
                 "3.0 = flag if hitting jackpots 3x more often than expected.")]
        [Range(2.0f, 5.0f)]
        public float HighValueHitRateThreshold = 3.0f;

        [Tooltip("Reward multiplier that counts as 'high value' for tracking.")]
        public int HighValueRewardThreshold = 9;

        [Header("Rejection Thresholds")]
        [Tooltip("Number of suspicious flags before actually rejecting entries. " +
                 "High value prevents false positives from occasional lucky streaks.")]
        public int SuspiciousFlagsBeforeReject = 20;

        [Tooltip("Number of implausible outcomes before flagging (within a session).")]
        public int ImplausibleOutcomesBeforeFlag = 5;

        [Header("Rate Limiting")]
        [Tooltip("Maximum balls per minute before flagging as speed hack.")]
        public int MaxBallsPerMinute = 120;

        [Tooltip("Minimum milliseconds between ball drops (bot detection).")]
        public int MinMillisecondsBetweenDrops = 80;

        [Header("Logging")]
        [Tooltip("Log all validation results for analysis.")]
        public bool EnableDetailedLogging = true;

        [Tooltip("Log only suspicious/flagged events.")]
        public bool LogOnlySuspicious = false;

        public static AntiCheatConfig Default => new AntiCheatConfig();

       // Calculate expected bucket range for a given drop position.
        public (int minBucket, int maxBucket) GetPlausibleBucketRange(
            float dropX,
            float spawnLeftBoundary,
            float spawnRightBoundary,
            int totalBuckets)
        {
            float spawnWidth = spawnRightBoundary - spawnLeftBoundary;
            if (spawnWidth <= 0) spawnWidth = 1f;

            float normalizedDrop = Mathf.Clamp01((dropX - spawnLeftBoundary) / spawnWidth);

            // Expected bucket center
            float expectedBucketCenter = normalizedDrop * (totalBuckets - 1);

            // Allow wide variance
            float allowedDeviation = totalBuckets * PlausibilityDeviationMultiplier;

            int minBucket = Mathf.Max(0, Mathf.FloorToInt(expectedBucketCenter - allowedDeviation));
            int maxBucket = Mathf.Min(totalBuckets - 1, Mathf.CeilToInt(expectedBucketCenter + allowedDeviation));

            return (minBucket, maxBucket);
        }

        // Check if a bucket outcome is plausible given drop position.
        public bool IsBucketPlausible(
            float dropX,
            int bucketIndex,
            float spawnLeftBoundary,
            float spawnRightBoundary,
            int totalBuckets)
        {
            var (minBucket, maxBucket) = GetPlausibleBucketRange(
                dropX, spawnLeftBoundary, spawnRightBoundary, totalBuckets);

            return bucketIndex >= minBucket && bucketIndex <= maxBucket;
        }
    }
}
