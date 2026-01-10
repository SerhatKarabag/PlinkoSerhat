using UnityEngine;
using TMPro;

namespace Plinko.Physics
{
    // Bucket component for detecting ball landings.
    [RequireComponent(typeof(BoxCollider2D))]
    public class Bucket : MonoBehaviour
    {
        [SerializeField] private int _bucketIndex;
        [SerializeField] private TextMeshPro _labelText;
        [SerializeField] private SpriteRenderer _backgroundRenderer;

        public int BucketIndex => _bucketIndex;
        public int BaseReward { get; private set; }
        public string Label { get; private set; } = string.Empty;

        private BoxCollider2D _collider;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
        }

        public void Configure(int index, Data.BucketConfig config)
        {
            _bucketIndex = index;
            BaseReward = config.BaseReward;
            Label = config.Label;

            if (_labelText != null)
            {
                _labelText.text = Label;
            }

            if (_backgroundRenderer != null && config.Color != default)
            {
                _backgroundRenderer.color = config.Color;
            }
        }
    }
}
