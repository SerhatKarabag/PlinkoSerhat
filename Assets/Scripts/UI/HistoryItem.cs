using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Plinko.UI
{
    public class HistoryItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _bucketText;
        [SerializeField] private TextMeshProUGUI _rewardText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private Image _backgroundImage;

        private static readonly Color[] _tierColors = new Color[]
        {
            new Color(0.6f, 0.6f, 0.6f),    // Low tier
            new Color(0.3f, 0.7f, 0.3f),    // Mid tier
            new Color(0.3f, 0.5f, 0.9f),    // High tier
            new Color(0.9f, 0.7f, 0.2f),    // Jackpot
        };

        public void Setup(string bucketLabel, long reward, int level)
        {
            if (_bucketText != null)
            {
                _bucketText.text = bucketLabel;
            }

            if (_rewardText != null)
            {
                _rewardText.text = $"+{reward:N0}";
            }

            if (_levelText != null)
            {
                _levelText.text = $"Lv{level + 1}";
            }

            if (_backgroundImage != null)
            {
                int tier = GetRewardTier(reward);
                _backgroundImage.color = _tierColors[Mathf.Clamp(tier, 0, _tierColors.Length - 1)];
            }
        }

        private int GetRewardTier(long reward)
        {
            if (reward >= 100) return 3;
            if (reward >= 20) return 2;
            if (reward >= 5) return 1;
            return 0;
        }
    }
}