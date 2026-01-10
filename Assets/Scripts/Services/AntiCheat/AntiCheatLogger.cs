using System;
using System.Text;
using Plinko.Core.Logging;
using Plinko.Data;

namespace Plinko.Services.AntiCheat
{
    // Logging service for anti-cheat events.
    public class AntiCheatLogger
    {
        private readonly AntiCheatConfig _config;
        private readonly ILogger _logger;
        private readonly StringBuilder _logBuffer;

        public AntiCheatLogger(AntiCheatConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logBuffer = new StringBuilder();
        }

        public void LogBatchValidation(
            RewardBatch batch,
            BatchAntiCheatResult result,
            PlayerAntiCheatStats stats)
        {
            if (!_config.EnableDetailedLogging) return;

            // Skip non-suspicious events if configured
            if (_config.LogOnlySuspicious && !result.IsSuspicious && result.ImplausibleEntries.Count == 0)
                return;

            _logBuffer.Clear();
            _logBuffer.AppendLine("=== Anti-Cheat Batch Validation ===");
            _logBuffer.AppendLine($"Player: {batch.PlayerId}");
            _logBuffer.AppendLine($"Batch: {batch.BatchId}");
            _logBuffer.AppendLine($"Entries: {batch.Entries.Count}");
            _logBuffer.AppendLine($"Claimed Total: {batch.TotalClientCalculatedReward}");

            if (result.ImplausibleEntries.Count > 0)
            {
                _logBuffer.AppendLine($"Implausible Entries: {result.ImplausibleEntries.Count}");
                foreach (int idx in result.ImplausibleEntries)
                {
                    var entry = batch.Entries[idx];
                    _logBuffer.AppendLine($"  [{idx}] dropX={entry.DropPositionX:F2} -> bucket={entry.BucketIndex} (reward={entry.RewardAmount})");
                }
            }

            if (result.Flags.Count > 0)
            {
                _logBuffer.AppendLine("Flags:");
                foreach (var flag in result.Flags)
                {
                    _logBuffer.AppendLine($"  - {flag}");
                }
            }

            if (result.StatisticalFlags != null && result.StatisticalFlags.Count > 0)
            {
                _logBuffer.AppendLine("Statistical Flags:");
                foreach (var flag in result.StatisticalFlags)
                {
                    _logBuffer.AppendLine($"  - {flag}");
                }
            }

            _logBuffer.AppendLine("--- Session Stats ---");
            _logBuffer.AppendLine($"Total Balls: {stats.TotalBallsValidated}");
            _logBuffer.AppendLine($"Avg Reward: {stats.AverageReward:F1}");
            _logBuffer.AppendLine($"High-Value Hits: {stats.HighValueHits} ({stats.HighValueHitRate:P1})");
            _logBuffer.AppendLine($"Implausible: {stats.ImplausibleOutcomes} ({stats.ImplausibleRate:P1})");
            _logBuffer.AppendLine($"Suspicious Flags: {stats.SuspiciousFlags}/{_config.SuspiciousFlagsBeforeReject}");
            _logBuffer.AppendLine($"Recent Avg: {stats.RecentAverageReward:F1}");
            _logBuffer.AppendLine($"Recent HV Rate: {stats.RecentHighValueHitRate:P1}");

            if (result.IsSuspicious)
            {
                _logBuffer.AppendLine("*** SESSION FLAGGED AS SUSPICIOUS ***");
            }

            if (result.ShouldReject)
            {
                _logBuffer.AppendLine("*** BATCH SHOULD BE REJECTED ***");
            }

            _logBuffer.AppendLine("================================");

            if (result.ShouldReject)
            {
                _logger.LogWarning(_logBuffer.ToString());
            }
            else if (result.IsSuspicious || result.ImplausibleEntries.Count > 0)
            {
                _logger.Log(_logBuffer.ToString());
            }
            else
            {
                _logger.Log(_logBuffer.ToString());
            }
        }

        // Log a single suspicious entry
        public void LogSuspiciousEntry(
            string playerId,
            RewardEntry entry,
            string reason)
        {
            if (!_config.EnableDetailedLogging) return;

            _logger.LogWarning(
                $"[AntiCheat] Suspicious entry - Player: {playerId}, " +
                $"dropX: {entry.DropPositionX:F2}, bucket: {entry.BucketIndex}, " +
                $"reward: {entry.RewardAmount}, reason: {reason}");
        }

       // Log rate limit warning.
        public void LogRateLimitWarning(string playerId, int currentRate, int maxRate)
        {
            _logger.LogWarning(
                $"[AntiCheat] Rate limit warning - Player: {playerId}, " +
                $"current: {currentRate}/min, max: {maxRate}/min");
        }

       // Log session summary on session end.
        public void LogSessionSummary(PlayerAntiCheatStats stats)
        {
            if (!_config.EnableDetailedLogging) return;

            var summary = stats.GetSummary();

            _logBuffer.Clear();
            _logBuffer.AppendLine("=== Anti-Cheat Session Summary ===");
            _logBuffer.AppendLine($"Player: {summary.PlayerId}");
            _logBuffer.AppendLine($"Session: {summary.SessionId}");
            _logBuffer.AppendLine($"Total Balls: {summary.TotalBalls}");
            _logBuffer.AppendLine($"Total Rewards: {summary.TotalRewards}");
            _logBuffer.AppendLine($"Average Reward: {summary.AverageReward:F1}");
            _logBuffer.AppendLine($"High-Value Hits: {summary.HighValueHits} ({summary.HighValueRate:P2})");
            _logBuffer.AppendLine($"Implausible Outcomes: {summary.ImplausibleCount} ({summary.ImplausibleRate:P2})");
            _logBuffer.AppendLine($"Suspicious Flags: {summary.SuspiciousFlags}");
            _logBuffer.AppendLine("=================================");

            if (summary.SuspiciousFlags > 0)
            {
                _logger.LogWarning(_logBuffer.ToString());
            }
            else
            {
                _logger.Log(_logBuffer.ToString());
            }
        }

        // Log threshold configuration for debugging.
        public void LogConfiguration()
        {
            if (!_config.EnableDetailedLogging) return;

            _logger.Log(
                $"[AntiCheat] Config: " +
                $"PlausibilityDeviation={_config.PlausibilityDeviationMultiplier:P0}, " +
                $"GracePeriod={_config.PlausibilityGracePeriod}, " +
                $"SampleSize={_config.StatisticalSampleSize}, " +
                $"AvgThreshold={_config.AverageRewardThreshold:F1}x, " +
                $"HVThreshold={_config.HighValueHitRateThreshold:F1}x, " +
                $"FlagsBeforeReject={_config.SuspiciousFlagsBeforeReject}");
        }
    }
}
