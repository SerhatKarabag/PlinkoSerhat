using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plinko.Data
{
    [Serializable]
    public class PlayerData
    {
        private const string PREFS_KEY = "Plinko_PlayerData";

        public long TotalCoinsEarned;
        public long WalletBalance;
        public List<RewardHistoryEntry> RewardHistory;
        public long LastSessionStartTimeTicks;
        public string PlayerId;

        public int CurrentBallCount;
        public int CurrentLevel;
        public int BallsDroppedThisLevel;
        public int TotalBallsDroppedThisSession;
        public long SessionStartTimeTicks;
        public RunSummarySnapshot CurrentRunSummary;
        public bool ClearHistoryOnNextLaunch;

        public DateTime LastSessionStartTime
        {
            get => new DateTime(LastSessionStartTimeTicks, DateTimeKind.Utc);
            set => LastSessionStartTimeTicks = value.Ticks;
        }

        public DateTime SessionStartTime
        {
            get => new DateTime(SessionStartTimeTicks, DateTimeKind.Utc);
            set => SessionStartTimeTicks = value.Ticks;
        }

        [NonSerialized] private bool _isDirty;
        [NonSerialized] private float _lastSaveTime;
        [NonSerialized] private int _maxHistoryEntries;
        [NonSerialized] private float _minSaveInterval;

        public PlayerData()
        {
            RewardHistory = new List<RewardHistoryEntry>();
            PlayerId = Guid.NewGuid().ToString();
        }

        public void SetConfig(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _maxHistoryEntries = config.MaxRewardHistoryEntries;
            _minSaveInterval = config.MinSaveInterval;
        }

        public static PlayerData Load(GameConfig config)
        {
            PlayerData data = null;

            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                try
                {
                    string json = PlayerPrefs.GetString(PREFS_KEY);
                    data = JsonUtility.FromJson<PlayerData>(json);
                    if (data != null && data.RewardHistory == null)
                        data.RewardHistory = new List<RewardHistoryEntry>();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{nameof(PlayerData)}] Failed to load player data: {ex.Message}");
                }
            }

            data ??= new PlayerData();
            data.SetConfig(config);

            if (data.ClearHistoryOnNextLaunch)
            {
                data.RewardHistory?.Clear();
                data.ClearRunSummary();
                data.ClearHistoryOnNextLaunch = false;
                data.SaveImmediate();
            }
            return data;
        }

        public void Save()
        {
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - _lastSaveTime < _minSaveInterval)
            {
                _isDirty = true;
                return;
            }

            SaveImmediate();
        }

        public void SaveImmediate()
        {
            while (RewardHistory.Count > _maxHistoryEntries)
            {
                RewardHistory.RemoveAt(0);
            }

            string json = JsonUtility.ToJson(this);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();

            _isDirty = false;
            _lastSaveTime = Time.realtimeSinceStartup;
        }

        public void FlushIfDirty()
        {
            if (_isDirty && Time.realtimeSinceStartup - _lastSaveTime >= _minSaveInterval)
            {
                SaveImmediate();
            }
        }

        public bool HasActiveSession => CurrentBallCount > 0 && SessionStartTimeTicks > 0;

        public void ResetSession(int initialBallCount)
        {
            CurrentBallCount = initialBallCount;
            CurrentLevel = 0;
            BallsDroppedThisLevel = 0;
            TotalBallsDroppedThisSession = 0;
            SessionStartTime = DateTime.UtcNow;
            LastSessionStartTime = SessionStartTime;
            ClearRunSummary();
            Save();
        }

        public void ContinueSession()
        {
            LastSessionStartTime = SessionStartTime;
            Save();
        }

        public void AddRewardToHistory(RewardHistoryEntry entry)
        {
            RewardHistory.Add(entry);
            if (RewardHistory.Count > _maxHistoryEntries)
            {
                RewardHistory.RemoveAt(0);
            }
        }

        public void UpdateWallet(long serverVerifiedBalance)
        {
            WalletBalance = serverVerifiedBalance;
            Save();
        }

        public void UpdateRunSummary(RunSummary summary)
        {
            if (summary == null) return;
            CurrentRunSummary = RunSummarySnapshot.FromRunSummary(summary);
        }

        public bool TryGetRunSummary(out RunSummary summary)
        {
            if (CurrentRunSummary.RunStartTimeTicks <= 0)
            {
                summary = null;
                return false;
            }

            summary = CurrentRunSummary.ToRunSummary();
            return true;
        }

        public void ClearRunSummary()
        {
            CurrentRunSummary = default;
        }

        public void MarkHistoryClearOnNextLaunch()
        {
            ClearHistoryOnNextLaunch = true;
        }

        public void ClearHistoryClearFlag()
        {
            ClearHistoryOnNextLaunch = false;
        }
    }

    [Serializable]
    public struct RewardHistoryEntry
    {
        public int BucketIndex;
        public string BucketLabel;
        public long RewardAmount;
        public int Level;
        public long TimestampTicks;

        public DateTime Timestamp => new DateTime(TimestampTicks);

        public static RewardHistoryEntry Create(int bucketIndex, string label, long amount, int level)
        {
            return new RewardHistoryEntry
            {
                BucketIndex = bucketIndex,
                BucketLabel = label,
                RewardAmount = amount,
                Level = level,
                TimestampTicks = DateTime.UtcNow.Ticks
            };
        }
    }

    [Serializable]
    public struct RunSummarySnapshot
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
        public long RunStartTimeTicks;
        public long RunEndTimeTicks;

        public RunSummary ToRunSummary()
        {
            var summary = new RunSummary
            {
                TotalPointsEarned = TotalPointsEarned,
                VerifiedPoints = VerifiedPoints,
                PendingPoints = PendingPoints,
                RejectedBallCount = RejectedBallCount,
                RejectedPoints = RejectedPoints,
                RetryingBatchCount = RetryingBatchCount,
                RetryingPoints = RetryingPoints,
                TotalBallsDropped = TotalBallsDropped,
                BallsScored = BallsScored,
                HighestLevelReached = HighestLevelReached,
                RunStartTime = RunStartTimeTicks > 0
                    ? new DateTime(RunStartTimeTicks, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                RunEndTime = RunEndTimeTicks > 0
                    ? new DateTime(RunEndTimeTicks, DateTimeKind.Utc)
                    : DateTime.MinValue
            };

            return summary;
        }

        public static RunSummarySnapshot FromRunSummary(RunSummary summary)
        {
            return new RunSummarySnapshot
            {
                TotalPointsEarned = summary.TotalPointsEarned,
                VerifiedPoints = summary.VerifiedPoints,
                PendingPoints = summary.PendingPoints,
                RejectedBallCount = summary.RejectedBallCount,
                RejectedPoints = summary.RejectedPoints,
                RetryingBatchCount = summary.RetryingBatchCount,
                RetryingPoints = summary.RetryingPoints,
                TotalBallsDropped = summary.TotalBallsDropped,
                BallsScored = summary.BallsScored,
                HighestLevelReached = summary.HighestLevelReached,
                RunStartTimeTicks = summary.RunStartTime.Ticks,
                RunEndTimeTicks = summary.RunEndTime == DateTime.MinValue
                    ? 0
                    : summary.RunEndTime.Ticks
            };
        }
    }
}
