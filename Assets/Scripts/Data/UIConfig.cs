using UnityEngine;

namespace Plinko.Data
{
    [CreateAssetMenu(fileName = "UIConfig", menuName = "Plinko/UI Config")]
    public class UIConfig : ScriptableObject
    {
        [Header("Update Settings")]
        [Tooltip("Interval for batched UI updates (seconds)")]
        [SerializeField] private float _updateInterval = 0.05f;

        [Header("Reward Popup")]
        [Tooltip("Initial pool size for reward popups")]
        [SerializeField] private int _rewardPopupPoolSize = 10;

        [Header("Text Formatting")]
        [Tooltip("Capacity for StringBuilder used in UI")]
        [SerializeField] private int _stringBuilderCapacity = 32;

        public float UpdateInterval => _updateInterval;
        public int RewardPopupPoolSize => _rewardPopupPoolSize;
        public int StringBuilderCapacity => _stringBuilderCapacity;
    }
}
