using System.Collections.Generic;
using UnityEngine;
using Plinko.Data;

namespace Plinko.Physics
{
    public class PlinkoBoard : MonoBehaviour
    {
        [Header("Triangle Board Configuration")]
        [Tooltip("Number of peg rows")]
        [SerializeField] private int _rows = 12;

        [Tooltip("Number of pegs in the first (top) row")]
        [SerializeField] private int _topRowPegCount = 3;

        [Tooltip("Number of pegs in the last (bottom) row")]
        [SerializeField] private int _bottomRowPegCount = 14;

        [Tooltip("Horizontal spacing between pegs in the same row")]
        [SerializeField] private float _horizontalSpacing = 0.55f;

        [Tooltip("Vertical spacing between rows")]
        [SerializeField] private float _verticalSpacing = 0.6f;

        [Header("Prefabs")]
        [SerializeField] private GameObject _pegPrefab;
        [SerializeField] private GameObject _bucketPrefab;

        [Header("Board Position")]
        [Tooltip("Y position of the top row of pegs")]
        [SerializeField] private float _topY = 4f;

        [Tooltip("X position of the board center")]
        [SerializeField] private float _centerX = 0f;

        [Header("Spawn Area Settings")]
        [Tooltip("How much of the TOP row width to use for spawn area (0.5 = half, 1.0 = full width). Balls enter through the top 3-peg region.")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _spawnWidthRatio = 0.95f;

        [Header("Bucket Settings")]
        [Tooltip("Gap between bucket top and bottom peg row")]
        [SerializeField] private float _bucketGap = 0.8f;

        [Tooltip("Height of the buckets")]
        [SerializeField] private float _bucketHeight = 1.2f;

        [Tooltip("Additional spacing between bucket centers (0 = no extra gap, 0.1 = 10% extra spacing)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _bucketSpacingMultiplier = 0.1f;

        [Tooltip("Scale of bucket width relative to spacing (1 = fill entire space, 0.8 = 80% with visible gaps)")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _bucketWidthScale = 0.85f;

        private List<GameObject> _pegs = new List<GameObject>();
        private List<Bucket> _buckets = new List<Bucket>();
        private GameConfig _config;

        private float _spawnAreaLeft;
        private float _spawnAreaRight;

        public float SpawnY => _topY + 1.5f;
        public float LeftBoundary => _spawnAreaLeft;
        public float RightBoundary => _spawnAreaRight;

        public float PyramidCenterX => _centerX;
        public float PyramidTopY => _topY;
        public float PyramidBottomY => _topY - ((_rows - 1) * _verticalSpacing);
        public float PyramidHalfWidthAtTop => (_topRowPegCount - 1) * _horizontalSpacing / 2f;

        public IReadOnlyList<Bucket> Buckets => _buckets;

        public void Initialize(GameConfig config)
        {
            _config = config;
            _pegs = new List<GameObject>();
            _buckets = new List<Bucket>();

            GenerateBoard();
        }

        private int GetPegCountForRow(int rowIndex)
        {
            if (_rows <= 1) return _topRowPegCount;

            float t = (float)rowIndex / (_rows - 1);
            return Mathf.RoundToInt(Mathf.Lerp(_topRowPegCount, _bottomRowPegCount, t));
        }

        private int GetBucketCount()
        {
            int bottomPegCount = GetPegCountForRow(_rows - 1);
            return bottomPegCount - 1;
        }

        private void GenerateBoard()
        {
            ClearBoard();
            GenerateTriangularPegs();
            GenerateAlignedBuckets();
        }

        private void ClearBoard()
        {
            if (_pegs != null)
            {
                foreach (var peg in _pegs)
                {
                    if (peg != null) Destroy(peg);
                }
                _pegs.Clear();
            }

            if (_buckets != null)
            {
                foreach (var bucket in _buckets)
                {
                    if (bucket != null) Destroy(bucket.gameObject);
                }
                _buckets.Clear();
            }
        }

        private void GenerateTriangularPegs()
        {
            if (_pegPrefab == null)
            {
                return;
            }

            float maxRowWidth = 0f;

            for (int row = 0; row < _rows; row++)
            {
                int pegCount = GetPegCountForRow(row);
                float y = _topY - (row * _verticalSpacing);

                float rowWidth = (pegCount - 1) * _horizontalSpacing;
                float startX = _centerX - (rowWidth / 2f);

                if (rowWidth > maxRowWidth)
                {
                    maxRowWidth = rowWidth;
                }

                for (int col = 0; col < pegCount; col++)
                {
                    float x = startX + (col * _horizontalSpacing);
                    var peg = Instantiate(_pegPrefab, new Vector3(x, y, 0), Quaternion.identity, transform);
                    peg.name = $"Peg_R{row}_C{col}";
                    _pegs.Add(peg);
                }
            }

            int topPegCount = GetPegCountForRow(0);
            float topRowWidth = (topPegCount - 1) * _horizontalSpacing;

            float spawnWidth = topRowWidth * _spawnWidthRatio;
            _spawnAreaLeft = _centerX - (spawnWidth / 2f);
            _spawnAreaRight = _centerX + (spawnWidth / 2f);
        }

        private void GenerateAlignedBuckets()
        {
            if (_bucketPrefab == null)
            {
                return;
            }

            var levelConfig = _config != null ? _config.GetLevelConfig(0) : LevelConfig.Default;
            var bucketConfigs = levelConfig.Buckets ?? LevelConfig.Default.Buckets;

            int bottomPegCount = GetPegCountForRow(_rows - 1);
            int bucketCount = GetBucketCount();

            float bottomPegY = _topY - ((_rows - 1) * _verticalSpacing);
            float bucketY = bottomPegY - _bucketGap;

            float baseBucketSpacing = _horizontalSpacing;

            float bucketSpacing = baseBucketSpacing * (1f + _bucketSpacingMultiplier);

            float totalBucketRowWidth = (bucketCount - 1) * bucketSpacing;

            float firstBucketX = _centerX - (totalBucketRowWidth / 2f);

            float visualBucketWidth = baseBucketSpacing * _bucketWidthScale;

            for (int i = 0; i < bucketCount; i++)
            {
                float x = firstBucketX + (i * bucketSpacing);

                var bucketObj = Instantiate(_bucketPrefab, new Vector3(x, bucketY, 0), Quaternion.identity, transform);
                bucketObj.name = $"Bucket_{i}";

                bucketObj.transform.localScale = new Vector3(visualBucketWidth, _bucketHeight, 1f);

                var bucket = bucketObj.GetComponent<Bucket>();
                if (bucket != null)
                {
                    int configIndex = MapBucketIndex(i, bucketCount, bucketConfigs.Length);
                    bucket.Configure(i, bucketConfigs[configIndex]);
                    _buckets.Add(bucket);
                }
            }
        }

        private int MapBucketIndex(int actualIndex, int actualCount, int configCount)
        {
            if (actualCount == configCount)
                return actualIndex;

            if (actualCount <= 1 || configCount <= 1)
                return 0;

            float normalizedPos = (float)actualIndex / (actualCount - 1);
            return Mathf.Clamp(Mathf.RoundToInt(normalizedPos * (configCount - 1)), 0, configCount - 1);
        }

        public void ApplyLevelConfig(int level)
        {
            var levelConfig = _config.GetLevelConfig(level);
            var bucketConfigs = levelConfig.Buckets;

            if (bucketConfigs == null || bucketConfigs.Length == 0)
            {
                Debug.LogWarning($"[PlinkoBoard] No bucket config for level {level}");
                return;
            }

            for (int i = 0; i < _buckets.Count; i++)
            {
                int configIndex = MapBucketIndex(i, _buckets.Count, bucketConfigs.Length);
                _buckets[i].Configure(i, bucketConfigs[configIndex]);
            }
        }

        public long CalculateReward(int bucketIndex, int level)
        {
            var levelConfig = _config.GetLevelConfig(level);
            var buckets = levelConfig.Buckets;

            if (buckets == null || bucketIndex < 0 || bucketIndex >= _buckets.Count)
            {
                return 1;
            }

            int configIndex = MapBucketIndex(bucketIndex, _buckets.Count, buckets.Length);
            return (long)(buckets[configIndex].BaseReward * levelConfig.RewardMultiplier);
        }

        private void OnDestroy()
        {
            ClearBoard();
        }
    }
}
