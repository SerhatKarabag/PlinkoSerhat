using System;
using UnityEngine;

namespace Plinko.Data
{
    // ScriptableObject containing all game configuration.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Plinko/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Game Rules")]
        [SerializeField] private int _initialBallCount = 200;
        [SerializeField] private float _sessionDurationMinutes = 15f;
        [SerializeField] private float _ballSpawnCooldown = 0.1f;
        [SerializeField] private int _maxSimultaneousBalls = 50;

        [Header("Level Progression")]
        [SerializeField] private int _ballsPerLevel = 20;
        [SerializeField] private LevelConfig[] _levels = Array.Empty<LevelConfig>();

        [Header("Ball Physics - Basic")]
        [Tooltip("Mass of the ball (higher = more momentum)")]
        [SerializeField] private float _ballMass = 1f;

        [Tooltip("Bounciness of ball collisions (0 = no bounce, 1 = full bounce). Lower values = more controlled.")]
        [Range(0f, 1f)]
        [SerializeField] private float _ballBounciness = 0.25f;

        [Tooltip("Friction on ball collisions (higher = more grip on pegs)")]
        [Range(0f, 1f)]
        [SerializeField] private float _ballFriction = 0.6f;

        [Tooltip("Gravity multiplier (higher = faster falling)")]
        [SerializeField] private float _gravityScale = 1.0f;

        [Header("Ball Physics - Drag & Damping")]
        [Tooltip("Linear drag applied to ball (air resistance, higher = slower movement)")]
        [Range(0f, 2f)]
        [SerializeField] private float _ballLinearDrag = 0.8f;

        [Tooltip("Angular drag applied to ball rotation")]
        [Range(0f, 2f)]
        [SerializeField] private float _ballAngularDrag = 0.8f;

        [Tooltip("Extra damping on horizontal movement per frame (0 = none, 0.02 = 2% reduction). Helps keep ball in pyramid.")]
        [Range(0f, 0.1f)]
        [SerializeField] private float _lateralDamping = 0.04f;

        [Header("Ball Physics - Speed Limits")]
        [Tooltip("Maximum horizontal speed (prevents extreme lateral bounces)")]
        [SerializeField] private float _maxHorizontalSpeed = 3f;

        [Tooltip("Maximum vertical speed (prevents ball from flying too fast)")]
        [SerializeField] private float _maxVerticalSpeed = 6f;

        [Header("Ball Physics - Soft Boundary")]
        [Tooltip("Strength of invisible boundary force that keeps ball in pyramid (0 = disabled, 50 = strong)")]
        [Range(0f, 100f)]
        [SerializeField] private float _softBoundaryStrength = 30f;

        [Header("Ball Despawn")]
        [SerializeField] private float _ballDespawnY = -10f;

        [Header("Network Simulation")]
        [SerializeField] private float _minServerLatencyMs = 50f;
        [SerializeField] private float _maxServerLatencyMs = 200f;
        [SerializeField] private float _serverErrorRate = 0.01f;

        [Header("Reward Batching")]
        [SerializeField] private int _rewardBatchSize = 10;
        [SerializeField] private float _rewardBatchTimeoutSeconds = 3f;
        [Tooltip("Interval between retry attempts for failed batches (seconds)")]
        [SerializeField] private float _rewardBatchRetryInterval = 1.0f;
        [Tooltip("Maximum number of retry attempts for failed batches")]
        [SerializeField] private int _rewardBatchMaxRetries = 3;

        [Header("State Transitions")]
        [Tooltip("Duration of level transition animation (seconds)")]
        [SerializeField] private float _levelTransitionDuration = 1.5f;

        [Header("Session Management")]
        [Tooltip("Interval for session timer UI updates (seconds)")]
        [SerializeField] private float _sessionTimerUpdateInterval = 0.25f;

        [Header("Data Persistence")]
        [Tooltip("Minimum interval between player data saves (seconds)")]
        [SerializeField] private float _minSaveInterval = 2f;
        [Tooltip("Maximum reward history entries to keep")]
        [SerializeField] private int _maxRewardHistoryEntries = 100;

        [Header("Anti-Cheat Thresholds")]
        [Tooltip("Rate above which implausible outcomes trigger a flag (0.1 = 10%)")]
        [Range(0.05f, 0.3f)]
        [SerializeField] private float _implausibleRateThreshold = 0.1f;

        // Properties - Game Rules
        public int InitialBallCount => _initialBallCount;
        public float SessionDurationMinutes => _sessionDurationMinutes;
        public float SessionDurationSeconds => _sessionDurationMinutes * 60f;
        public float BallSpawnCooldown => _ballSpawnCooldown;
        public int MaxSimultaneousBalls => _maxSimultaneousBalls;

        // Properties - Level Progression
        public int BallsPerLevel => _ballsPerLevel;
        public LevelConfig[] Levels => _levels;

        // Properties - Ball Physics Basic
        public float BallMass => _ballMass;
        public float BallBounciness => _ballBounciness;
        public float BallFriction => _ballFriction;
        public float GravityScale => _gravityScale;

        // Properties - Ball Physics Drag & Damping
        public float BallLinearDrag => _ballLinearDrag;
        public float BallAngularDrag => _ballAngularDrag;
        public float LateralDamping => _lateralDamping;

        // Properties - Ball Physics Speed Limits
        public float MaxHorizontalSpeed => _maxHorizontalSpeed;
        public float MaxVerticalSpeed => _maxVerticalSpeed;

        // Properties - Ball Physics Soft Boundary
        public float SoftBoundaryStrength => _softBoundaryStrength;

        // Properties - Ball Despawn
        public float BallDespawnY => _ballDespawnY;

        // Properties - Network
        public float MinServerLatencyMs => _minServerLatencyMs;
        public float MaxServerLatencyMs => _maxServerLatencyMs;
        public float ServerErrorRate => _serverErrorRate;

        // Properties - Reward Batching
        public int RewardBatchSize => _rewardBatchSize;
        public float RewardBatchTimeoutSeconds => _rewardBatchTimeoutSeconds;
        public float RewardBatchRetryInterval => _rewardBatchRetryInterval;
        public int RewardBatchMaxRetries => _rewardBatchMaxRetries;

        // Properties - State Transitions
        public float LevelTransitionDuration => _levelTransitionDuration;

        // Properties - Session Management
        public float SessionTimerUpdateInterval => _sessionTimerUpdateInterval;

        // Properties - Data Persistence
        public float MinSaveInterval => _minSaveInterval;
        public int MaxRewardHistoryEntries => _maxRewardHistoryEntries;

        // Properties - Anti-Cheat
        public float ImplausibleRateThreshold => _implausibleRateThreshold;

        public LevelConfig GetLevelConfig(int level)
        {
            if (_levels == null || _levels.Length == 0)
                return LevelConfig.Default;

            int index = Mathf.Clamp(level, 0, _levels.Length - 1);
            return _levels[index];
        }
    }

    [Serializable]
    public struct LevelConfig
    {
        [SerializeField] private string _levelName;
        [SerializeField] private BucketConfig[] _buckets;
        [SerializeField] private float _rewardMultiplier;
        [SerializeField] private Color _themeColor;

        public string LevelName => _levelName;
        public BucketConfig[] Buckets => _buckets;
        public float RewardMultiplier => _rewardMultiplier;
        public Color ThemeColor => _themeColor;

        public static LevelConfig Default => new LevelConfig
        {
            _levelName = "Level 1",
            _buckets = new[]
            {
                new BucketConfig { BaseReward = 1, Label = "1x" },
                new BucketConfig { BaseReward = 2, Label = "2x" },
                new BucketConfig { BaseReward = 5, Label = "5x" },
                new BucketConfig { BaseReward = 10, Label = "10x" },
                new BucketConfig { BaseReward = 5, Label = "5x" },
                new BucketConfig { BaseReward = 2, Label = "2x" },
                new BucketConfig { BaseReward = 1, Label = "1x" }
            },
            _rewardMultiplier = 1f,
            _themeColor = Color.white
        };
    }

    [Serializable]
    public struct BucketConfig
    {
        public int BaseReward;
        public string Label;
        public Color Color;
    }
}
