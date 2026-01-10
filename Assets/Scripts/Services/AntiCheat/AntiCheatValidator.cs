using System;
using System.Collections.Generic;
using Plinko.Core.Logging;
using Plinko.Data;

namespace Plinko.Services.AntiCheat
{
    public class AntiCheatValidator
    {
        private readonly AntiCheatConfig _config;
        private readonly GameConfig _gameConfig;
        private readonly Dictionary<string, PlayerAntiCheatStats> _playerStats;
        private readonly AntiCheatLogger _logger;

        private float _spawnLeftBoundary;
        private float _spawnRightBoundary;

        public AntiCheatValidator(AntiCheatConfig config, GameConfig gameConfig, ILogger logger)
        {
            _gameConfig = gameConfig ?? throw new ArgumentNullException(nameof(gameConfig));
            _config = config ?? AntiCheatConfig.Default;
            _playerStats = new Dictionary<string, PlayerAntiCheatStats>();
            _logger = new AntiCheatLogger(_config, logger);
        }

        public void SetSpawnBoundaries(float left, float right)
        {
            _spawnLeftBoundary = left;
            _spawnRightBoundary = right;
        }

        public PlayerAntiCheatStats GetOrCreatePlayerStats(string playerId, string sessionId)
        {
            string key = $"{playerId}:{sessionId}";

            if (!_playerStats.TryGetValue(key, out var stats))
            {
                stats = new PlayerAntiCheatStats(playerId, sessionId, _config.StatisticalSampleSize);
                _playerStats[key] = stats;
            }

            return stats;
        }

        public void ClearPlayerStats(string playerId, string sessionId)
        {
            string key = $"{playerId}:{sessionId}";
            _playerStats.Remove(key);
        }

        public EntryValidationResult ValidateEntry(RewardEntry entry, PlayerAntiCheatStats stats)
        {
            var result = new EntryValidationResult
            {
                IsPlausible = true,
                IsHighValue = false,
                Flags = new List<string>()
            };

            int totalBuckets = entry.TotalBucketCount > 0 ? entry.TotalBucketCount : 13;

            var levelConfig = _gameConfig.GetLevelConfig(entry.Level);
            int configBucketIndex = MapBucketIndex(entry.BucketIndex, totalBuckets, levelConfig.Buckets.Length);
            int baseReward = levelConfig.Buckets[configBucketIndex].BaseReward;
            result.IsHighValue = baseReward >= _config.HighValueRewardThreshold;

            if (stats.TotalBallsValidated >= _config.PlausibilityGracePeriod)
            {
                bool isPlausible = _config.IsBucketPlausible(
                    entry.DropPositionX,
                    entry.BucketIndex,
                    _spawnLeftBoundary,
                    _spawnRightBoundary,
                    totalBuckets);

                if (!isPlausible)
                {
                    result.IsPlausible = false;
                    result.Flags.Add($"Implausible: dropX={entry.DropPositionX:F2} → bucket={entry.BucketIndex}");

                    var (minB, maxB) = _config.GetPlausibleBucketRange(
                        entry.DropPositionX, _spawnLeftBoundary, _spawnRightBoundary, totalBuckets);
                    result.ExpectedBucketRange = (minB, maxB);
                }
            }

            stats.RecordOutcome(
                entry.BucketIndex,
                entry.RewardAmount,
                result.IsPlausible,
                result.IsHighValue,
                entry.DropPositionX);

            return result;
        }

        public StatisticalAnalysisResult AnalyzeStatistics(PlayerAntiCheatStats stats)
        {
            var result = new StatisticalAnalysisResult
            {
                IsSuspicious = false,
                Flags = new List<string>()
            };

            if (!stats.HasSufficientData(_config.StatisticalSampleSize))
            {
                result.InsufficientData = true;
                return result;
            }

            var expectedStats = CalculateExpectedStats();

            double recentAvg = stats.RecentAverageReward;
            double expectedAvg = expectedStats.ExpectedAverageReward;
            double avgRatio = expectedAvg > 0 ? recentAvg / expectedAvg : 0;

            if (avgRatio > _config.AverageRewardThreshold)
            {
                result.Flags.Add($"High avg reward: {recentAvg:F1} ({avgRatio:F1}x expected {expectedAvg:F1})");
                stats.AddSuspiciousFlag("HighAvgReward");
            }

            double recentHVRate = stats.RecentHighValueHitRate;
            double expectedHVRate = expectedStats.ExpectedHighValueRate;
            double hvRatio = expectedHVRate > 0 ? recentHVRate / expectedHVRate : 0;

            if (hvRatio > _config.HighValueHitRateThreshold)
            {
                result.Flags.Add($"High jackpot rate: {recentHVRate:P1} ({hvRatio:F1}x expected {expectedHVRate:P1})");
                stats.AddSuspiciousFlag("HighJackpotRate");
            }

            if (stats.ImplausibleOutcomes >= _config.ImplausibleOutcomesBeforeFlag)
            {
                double implausibleRate = stats.ImplausibleRate;
                if (implausibleRate > _gameConfig.ImplausibleRateThreshold)
                {
                    result.Flags.Add($"High implausible rate: {implausibleRate:P1} ({stats.ImplausibleOutcomes} outcomes)");
                    stats.AddSuspiciousFlag("HighImplausibleRate");
                }
            }

            if (stats.IsRateLimitExceeded(_config.MaxBallsPerMinute))
            {
                result.Flags.Add($"Rate limit exceeded: {stats.CurrentBallsPerMinute}/min (max: {_config.MaxBallsPerMinute})");
                stats.AddSuspiciousFlag("RateLimitExceeded");
            }

            result.IsSuspicious = stats.SuspiciousFlags >= _config.SuspiciousFlagsBeforeReject;
            result.TotalFlags = stats.SuspiciousFlags;
            result.FlagsUntilReject = Math.Max(0, _config.SuspiciousFlagsBeforeReject - stats.SuspiciousFlags);

            return result;
        }

        public BatchAntiCheatResult ValidateBatch(RewardBatch batch, string sessionId)
        {
            var stats = GetOrCreatePlayerStats(batch.PlayerId, sessionId);
            var result = new BatchAntiCheatResult
            {
                ImplausibleEntries = new List<int>(),
                Flags = new List<string>()
            };

            for (int i = 0; i < batch.Entries.Count; i++)
            {
                var entry = batch.Entries[i];
                var entryResult = ValidateEntry(entry, stats);

                if (!entryResult.IsPlausible)
                {
                    result.ImplausibleEntries.Add(i);
                    result.Flags.AddRange(entryResult.Flags);
                }
            }

            var statsResult = AnalyzeStatistics(stats);
            result.StatisticalFlags = statsResult.Flags;
            result.IsSuspicious = statsResult.IsSuspicious;

            _logger.LogBatchValidation(batch, result, stats);

            result.ShouldReject =
                result.IsSuspicious ||
                (result.ImplausibleEntries.Count == batch.Entries.Count && batch.Entries.Count >= 3);

            return result;
        }

        private ExpectedStats CalculateExpectedStats()
        {
            var levelConfig = _gameConfig.GetLevelConfig(1);
            var buckets = levelConfig.Buckets;

            if (buckets == null || buckets.Length == 0)
            {
                return new ExpectedStats { ExpectedAverageReward = 1, ExpectedHighValueRate = 0.01 };
            }

            int n = buckets.Length - 1;
            double totalProb = 0;
            double weightedReward = 0;
            double highValueProb = 0;

            for (int k = 0; k <= n; k++)
            {
                double prob = BinomialCoefficient(n, k) / Math.Pow(2, n);
                int reward = buckets[k].BaseReward;

                weightedReward += prob * reward * levelConfig.RewardMultiplier;
                totalProb += prob;

                if (reward >= _config.HighValueRewardThreshold)
                {
                    highValueProb += prob;
                }
            }

            return new ExpectedStats
            {
                ExpectedAverageReward = weightedReward / totalProb,
                ExpectedHighValueRate = highValueProb
            };
        }

        private double BinomialCoefficient(int n, int k)
        {
            if (k > n) return 0;
            if (k == 0 || k == n) return 1;

            double result = 1;
            for (int i = 0; i < k; i++)
            {
                result *= (n - i);
                result /= (i + 1);
            }
            return result;
        }

        private int MapBucketIndex(int actualIndex, int actualCount, int configCount)
        {
            if (actualCount == configCount)
                return actualIndex;

            if (actualCount <= 1 || configCount <= 1)
                return 0;

            float normalizedPos = (float)actualIndex / (actualCount - 1);
            return Math.Clamp((int)Math.Round(normalizedPos * (configCount - 1)), 0, configCount - 1);
        }

        private struct ExpectedStats
        {
            public double ExpectedAverageReward;
            public double ExpectedHighValueRate;
        }
    }

    public class EntryValidationResult
    {
        public bool IsPlausible;
        public bool IsHighValue;
        public List<string> Flags = new List<string>();
        public (int min, int max) ExpectedBucketRange;
    }

    public class StatisticalAnalysisResult
    {
        public bool IsSuspicious;
        public bool InsufficientData;
        public int TotalFlags;
        public int FlagsUntilReject;
        public List<string> Flags = new List<string>();
    }

    public class BatchAntiCheatResult
    {
        public List<int> ImplausibleEntries = new List<int>();
        public List<string> Flags = new List<string>();
        public List<string> StatisticalFlags = new List<string>();
        public bool IsSuspicious;
        public bool ShouldReject;
    }
}
