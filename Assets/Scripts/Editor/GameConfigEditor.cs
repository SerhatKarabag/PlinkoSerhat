#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Plinko.Data;

namespace Plinko.Editor
{
    [CustomEditor(typeof(GameConfig))]
    public class GameConfigEditor : UnityEditor.Editor
    {
        private const int DEFAULT_BUCKET_COUNT = 13;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("Level Generation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates 5 levels with classic Plinko distribution:\n" +
                "• HIGH multipliers on edges (harder to hit)\n" +
                "• LOW multipliers in center (easier to hit)\n" +
                "• Symmetric left/right pattern",
                MessageType.Info);

            if (GUILayout.Button("Generate Default Levels"))
            {
                GenerateDefaultLevels();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create Default Config Asset"))
            {
                CreateDefaultConfig();
            }
        }

        private void GenerateDefaultLevels()
        {
            var config = (GameConfig)target;

            var levelsField = typeof(GameConfig).GetField("_levels",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var levels = new LevelConfig[5];

            levels[0] = CreateLevel("Level 1", 1f, new Color(0.4f, 0.7f, 1f),
                GenerateEdgeHighCenterLowMultipliers(DEFAULT_BUCKET_COUNT, baseMultiplier: 1f));

            levels[1] = CreateLevel("Level 2", 1f, new Color(0.5f, 0.8f, 0.5f),
                GenerateEdgeHighCenterLowMultipliers(DEFAULT_BUCKET_COUNT, baseMultiplier: 1.5f));

            levels[2] = CreateLevel("Level 3", 1f, new Color(1f, 0.8f, 0.3f),
                GenerateEdgeHighCenterLowMultipliers(DEFAULT_BUCKET_COUNT, baseMultiplier: 2f));

            levels[3] = CreateLevel("Level 4", 1f, new Color(1f, 0.5f, 0.3f),
                GenerateEdgeHighCenterLowMultipliers(DEFAULT_BUCKET_COUNT, baseMultiplier: 3f));

            levels[4] = CreateLevel("Level 5", 1f, new Color(1f, 0.3f, 0.5f),
                GenerateEdgeHighCenterLowMultipliers(DEFAULT_BUCKET_COUNT, baseMultiplier: 5f));

            levelsField?.SetValue(config, levels);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Editor] Generated 5 default levels with {DEFAULT_BUCKET_COUNT} buckets each (edge-high/center-low pattern)");
        }

        private float[] GenerateEdgeHighCenterLowMultipliers(int bucketCount, float baseMultiplier)
        {
            float[] multipliers = new float[bucketCount];
            int center = bucketCount / 2;

            float[] tierMultipliers = new float[]
            {
                16f,
                9f,
                2f,
                1.4f,
                1.2f,
                1.1f,
                1f,
                0.5f
            };

            for (int i = 0; i < bucketCount; i++)
            {
                int distanceFromCenter = Mathf.Abs(i - center);
                int maxDistance = center;

                float normalizedDistance = (float)distanceFromCenter / maxDistance;

                int tierIndex = Mathf.RoundToInt((1f - normalizedDistance) * (tierMultipliers.Length - 1));
                tierIndex = Mathf.Clamp(tierIndex, 0, tierMultipliers.Length - 1);

                multipliers[i] = tierMultipliers[tierIndex] * baseMultiplier;
            }

            return multipliers;
        }

        private LevelConfig CreateLevel(string name, float levelMultiplier, Color color, float[] multipliers)
        {
            var buckets = new BucketConfig[multipliers.Length];
            var colors = GetEdgeHighCenterLowColors(multipliers.Length);

            for (int i = 0; i < multipliers.Length; i++)
            {
                float mult = multipliers[i];
                buckets[i] = new BucketConfig
                {
                    BaseReward = Mathf.RoundToInt(mult * 10),
                    Label = FormatMultiplier(mult),
                    Color = colors[i]
                };
            }

            var levelConfig = new LevelConfig();
            var type = typeof(LevelConfig);

            type.GetField("_levelName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValueDirect(__makeref(levelConfig), name);
            type.GetField("_buckets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValueDirect(__makeref(levelConfig), buckets);
            type.GetField("_rewardMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValueDirect(__makeref(levelConfig), levelMultiplier);
            type.GetField("_themeColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValueDirect(__makeref(levelConfig), color);

            return levelConfig;
        }

        private string FormatMultiplier(float mult)
        {
            if (mult >= 10f)
                return $"{Mathf.RoundToInt(mult)}x";
            else if (mult >= 1f)
                return $"{mult:0.#}x";
            else
                return $"{mult:0.0}x";
        }

        private Color[] GetEdgeHighCenterLowColors(int count)
        {
            var colors = new Color[count];
            int center = count / 2;

            Color edgeColor = new Color(1f, 0.2f, 0.3f);
            Color midColor = new Color(1f, 0.7f, 0.2f);
            Color centerColor = new Color(0.3f, 0.8f, 0.3f);

            for (int i = 0; i < count; i++)
            {
                int distanceFromCenter = Mathf.Abs(i - center);
                float normalizedDistance = (float)distanceFromCenter / center;

                if (normalizedDistance > 0.5f)
                {
                    float t = (normalizedDistance - 0.5f) * 2f;
                    colors[i] = Color.Lerp(midColor, edgeColor, t);
                }
                else
                {
                    float t = normalizedDistance * 2f;
                    colors[i] = Color.Lerp(centerColor, midColor, t);
                }
            }

            return colors;
        }

        [MenuItem("Assets/Create/Plinko/Default Game Config", false, 100)]
        private static void CreateDefaultConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Config"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Config");
            }

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Resources/Config/GameConfig.asset");
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorUtility.FocusProjectWindow();

            Debug.Log("[Editor] Created GameConfig at Assets/Resources/Config/GameConfig.asset");
        }
    }
}
#endif
