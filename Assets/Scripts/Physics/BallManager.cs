using System;
using System.Collections.Generic;
using UnityEngine;
using Plinko.Data;
using Plinko.Pooling;

namespace Plinko.Physics
{
    public class BallManager : MonoBehaviour
    {
        public event Action<PlinkoBall, int, long> OnBallScored;
        public event Action<PlinkoBall> OnBallDespawned;

        [Header("Prefab")]
        [SerializeField] private GameObject _ballPrefab;

        [Header("Pool Settings")]
        [SerializeField] private int _initialPoolSize = 30;
        [SerializeField] private int _maxPoolSize = 100;

        private GameObjectPool _ballPool;
        private List<PlinkoBall> _activeBalls;
        private GameConfig _config;
        private PlinkoBoard _board;
        private PhysicsSettings _physicsSettings;

        private int _ballIndexCounter;
        private int _currentLevel;
        private readonly WaitForSeconds _despawnDelay = new WaitForSeconds(0.3f);

        public int ActiveBallCount => _activeBalls?.Count ?? 0;
        public int PooledBallCount => _ballPool?.CountInactive ?? 0;

        public void Initialize(GameConfig config, PlinkoBoard board)
        {
            _config = config;
            _board = board;

            _physicsSettings = PhysicsSettings.FromConfig(config);
            _physicsSettings.PyramidCenterX = board.PyramidCenterX;
            _physicsSettings.PyramidTopY = board.PyramidTopY;
            _physicsSettings.PyramidBottomY = board.PyramidBottomY;
            _physicsSettings.PyramidHalfWidthAtTop = board.PyramidHalfWidthAtTop;

            _activeBalls = new List<PlinkoBall>(_maxPoolSize);
            _ballIndexCounter = 0;
            _currentLevel = 0;

            if (_ballPrefab != null)
            {
                _ballPool = new GameObjectPool(_ballPrefab, transform, _initialPoolSize, _maxPoolSize);
            }
        }

        public void SetLevel(int level)
        {
            _currentLevel = level;
        }

        public PlinkoBall SpawnBall(Vector2 position)
        {
            if (_ballPool == null) return null;
            if (_activeBalls.Count >= _config.MaxSimultaneousBalls) return null;

            float minX = _board.LeftBoundary;
            float maxX = _board.RightBoundary;
            if (minX > maxX)
            {
                minX = maxX = (minX + maxX) * 0.5f;
            }
            float clampedX = Mathf.Clamp(position.x, minX, maxX);
            Vector3 spawnPos = new Vector3(clampedX, _board.SpawnY, 0);

            var ball = _ballPool.Get<PlinkoBall>(spawnPos, Quaternion.identity);

            if (ball != null)
            {
                int ballIndex = _ballIndexCounter++;

                ball.Initialize(
                    ballIndex,
                    clampedX,
                    _currentLevel,
                    _config.BallDespawnY,
                    _physicsSettings
                );

                ball.OnBucketEntered += HandleBallScored;
                ball.OnDespawnRequired += HandleBallDespawn;

                _activeBalls.Add(ball);
            }

            return ball;
        }

        public void CheckBallsDespawn()
        {
            for (int i = _activeBalls.Count - 1; i >= 0; i--)
            {
                _activeBalls[i].CheckDespawn();
            }
        }

        public void ResetSession()
        {
            for (int i = _activeBalls.Count - 1; i >= 0; i--)
            {
                DespawnBall(_activeBalls[i]);
            }

            _ballIndexCounter = 0;
            _currentLevel = 0;
        }

        private void HandleBallScored(PlinkoBall ball, int bucketIndex)
        {
            long reward = _board.CalculateReward(bucketIndex, ball.SpawnLevel);
            OnBallScored?.Invoke(ball, bucketIndex, reward);
            StartCoroutine(DelayedDespawn(ball));
        }

        private System.Collections.IEnumerator DelayedDespawn(PlinkoBall ball)
        {
            yield return _despawnDelay;
            if (ball != null && ball.IsActive)
            {
                DespawnBall(ball);
            }
        }

        private void HandleBallDespawn(PlinkoBall ball)
        {
            DespawnBall(ball);
        }

        private void DespawnBall(PlinkoBall ball)
        {
            if (ball == null) return;

            ball.OnBucketEntered -= HandleBallScored;
            ball.OnDespawnRequired -= HandleBallDespawn;

            ball.Reset();
            SwapBackRemove(ball);
            _ballPool.Release(ball.gameObject);

            OnBallDespawned?.Invoke(ball);
        }

        private void SwapBackRemove(PlinkoBall ball)
        {
            int index = _activeBalls.IndexOf(ball);
            if (index < 0) return;

            int lastIndex = _activeBalls.Count - 1;
            if (index < lastIndex)
            {
                _activeBalls[index] = _activeBalls[lastIndex];
            }
            _activeBalls.RemoveAt(lastIndex);
        }

        public void DespawnAllBalls()
        {
            for (int i = _activeBalls.Count - 1; i >= 0; i--)
            {
                DespawnBall(_activeBalls[i]);
            }
        }

        private void OnDestroy()
        {
            DespawnAllBalls();
            _ballPool?.Clear();
        }

        public BallManagerStats GetStats()
        {
            return new BallManagerStats
            {
                ActiveBalls = _activeBalls?.Count ?? 0,
                PooledBalls = _ballPool?.CountInactive ?? 0,
                TotalBallsSpawned = _ballIndexCounter
            };
        }
    }

    public struct BallManagerStats
    {
        public int ActiveBalls;
        public int PooledBalls;
        public int TotalBallsSpawned;
    }
}