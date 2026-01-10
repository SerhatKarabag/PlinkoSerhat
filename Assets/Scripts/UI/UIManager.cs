using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Plinko.Data;

namespace Plinko.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private UIConfig _config;
        private const string MissingConfigMessage = "Missing UIConfig.";

        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI _walletText;
        [SerializeField] private TextMeshProUGUI _ballCountText;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI _levelText;

        [Header("History Panel")]
        [SerializeField] private Transform _historyContainer;
        [SerializeField] private GameObject _historyItemPrefab;
        [SerializeField] private int _maxHistoryItems = 20;
        [SerializeField] private ScrollRect _historyScrollRect;

        [Header("Overlays")]
        [SerializeField] private GameObject _levelTransitionOverlay;
        [SerializeField] private TextMeshProUGUI _levelTransitionText;
        [SerializeField] private GameObject _sessionExpiredOverlay;
        [SerializeField] private TextMeshProUGUI _sessionExpiredText;
        [SerializeField] private GameObject _loadingOverlay;
        [SerializeField] private TextMeshProUGUI _loadingText;
        [SerializeField] private GameObject _errorOverlay;
        [SerializeField] private TextMeshProUGUI _errorText;
        [SerializeField] private RunEndOverlay _runEndOverlay;

        [Header("Reward Popup")]
        [SerializeField] private GameObject _rewardPopupPrefab;
        [SerializeField] private Transform _rewardPopupContainer;

        private long _displayedWallet;
        private long _displayedPending;
        private int _displayedBallCount;
        private int _displayedLevel;
        private string _displayedTimer;
        private bool _walletDirty;
        private bool _ballCountDirty;
        private bool _levelDirty;

        private Queue<GameObject> _historyPool;
        private List<GameObject> _activeHistoryItems;
        private Queue<RewardPopup> _popupPool;
        private List<RewardPopup> _activePopups;
        private System.Text.StringBuilder _stringBuilder;
        private Camera _cachedCamera;

        private float _uiUpdateTimer;
        private float _uiUpdateInterval;
        private int _rewardPopupPoolSize;
        private int _stringBuilderCapacity;

        public event Action OnRunEndRestartClicked;

        private void Awake()
        {
            if (!TryLoadUIConfig())
            {
                return;
            }

            ApplyUIConfig();
            _historyPool = new Queue<GameObject>();
            _activeHistoryItems = new List<GameObject>();
            _popupPool = new Queue<RewardPopup>();
            _activePopups = new List<RewardPopup>();
            _stringBuilder = new System.Text.StringBuilder(_stringBuilderCapacity);
            _cachedCamera = Camera.main;

            InitializePools();
            HideAllOverlays();

            if (_runEndOverlay != null)
            {
                _runEndOverlay.OnRestartClicked += HandleRunEndRestartClicked;
            }
        }

        private void ApplyUIConfig()
        {
            if (_config == null) return;

            _uiUpdateInterval = _config.UpdateInterval;
            _rewardPopupPoolSize = _config.RewardPopupPoolSize;
            _stringBuilderCapacity = _config.StringBuilderCapacity;
        }

        private void OnDestroy()
        {
            if (_runEndOverlay != null)
            {
                _runEndOverlay.OnRestartClicked -= HandleRunEndRestartClicked;
            }
        }

        private void HandleRunEndRestartClicked()
        {
            OnRunEndRestartClicked?.Invoke();
        }
        private bool TryLoadUIConfig()
        {
            if (_config != null)
            {
                return true;
            }

            _config = Resources.Load<UIConfig>("Config/UIConfig");
            if (_config != null)
            {
                return true;
            }

            ShowError(MissingConfigMessage);
            enabled = false;
            return false;
        }

        private void InitializePools()
        {
            if (_historyItemPrefab != null && _historyContainer != null)
            {
                for (int i = 0; i < _maxHistoryItems; i++)
                {
                    var item = Instantiate(_historyItemPrefab, _historyContainer);
                    item.SetActive(false);
                    _historyPool.Enqueue(item);
                }
            }

            if (_rewardPopupPrefab != null && _rewardPopupContainer != null)
            {
                for (int i = 0; i < _rewardPopupPoolSize; i++)
                {
                    var popup = Instantiate(_rewardPopupPrefab, _rewardPopupContainer);
                    var rewardPopup = popup.GetComponent<RewardPopup>();
                    if (rewardPopup != null)
                    {
                        rewardPopup.OnComplete += () => ReturnPopupToPool(rewardPopup);
                        popup.SetActive(false);
                        _popupPool.Enqueue(rewardPopup);
                    }
                }
            }
        }

        private void Update()
        {
            _uiUpdateTimer += Time.deltaTime;

            if (_uiUpdateTimer >= _uiUpdateInterval)
            {
                _uiUpdateTimer = 0f;
                ApplyDirtyUpdates();
            }

            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                _activePopups[i].Tick(Time.deltaTime);
            }
        }

        private void ApplyDirtyUpdates()
        {
            if (_walletDirty && _walletText != null)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("$");
                _stringBuilder.Append(_displayedWallet.ToString("N0"));
                if (_displayedPending > 0)
                {
                    _stringBuilder.Append("\n<size=70%><color=#B9C2FF>Verifying +");
                    _stringBuilder.Append(_displayedPending.ToString("N0"));
                    _stringBuilder.Append("</color></size>");
                }
                _walletText.text = _stringBuilder.ToString();
                _walletDirty = false;
            }

            if (_ballCountDirty && _ballCountText != null)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(_displayedBallCount);
                _stringBuilder.Append(" Balls");
                _ballCountText.text = _stringBuilder.ToString();
                _ballCountDirty = false;
            }

            if (_levelDirty && _levelText != null)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("Level ");
                _stringBuilder.Append(_displayedLevel + 1);
                _levelText.text = _stringBuilder.ToString();
                _levelDirty = false;
            }
        }

        public void SetWallet(long verifiedAmount, long pendingAmount)
        {
            if (_displayedWallet != verifiedAmount || _displayedPending != pendingAmount)
            {
                _displayedWallet = verifiedAmount;
                _displayedPending = pendingAmount;
                _walletDirty = true;
            }
        }

        public void SetBallCount(int count)
        {
            if (_displayedBallCount != count)
            {
                _displayedBallCount = count;
                _ballCountDirty = true;
            }
        }

        public void SetLevel(int level)
        {
            if (_displayedLevel != level)
            {
                _displayedLevel = level;
                _levelDirty = true;
            }
        }

        public void SetTimer(string formattedTime)
        {
            if (_displayedTimer != formattedTime && _timerText != null)
            {
                _displayedTimer = formattedTime;
                _timerText.text = formattedTime;
            }
        }

        public void SetTimerSeconds(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);

            _stringBuilder.Clear();
            _stringBuilder.Append(minutes.ToString("00"));
            _stringBuilder.Append(":");
            _stringBuilder.Append(secs.ToString("00"));

            SetTimer(_stringBuilder.ToString());
        }

        public void AddHistoryEntry(int bucketIndex, string label, long reward, int level)
        {
            GameObject item;

            if (_historyPool.Count > 0)
            {
                item = _historyPool.Dequeue();
            }
            else if (_activeHistoryItems.Count >= _maxHistoryItems)
            {
                item = _activeHistoryItems[0];
                _activeHistoryItems.RemoveAt(0);
            }
            else
            {
                item = Instantiate(_historyItemPrefab, _historyContainer);
            }

            var historyItem = item.GetComponent<HistoryItem>();
            if (historyItem != null)
            {
                historyItem.Setup(label, reward, level);
            }

            item.SetActive(true);
            item.transform.SetAsFirstSibling();
            _activeHistoryItems.Add(item);

            while (_activeHistoryItems.Count > _maxHistoryItems)
            {
                var oldest = _activeHistoryItems[0];
                _activeHistoryItems.RemoveAt(0);
                oldest.SetActive(false);
                _historyPool.Enqueue(oldest);
            }
        }

        public void ClearHistory()
        {
            foreach (var item in _activeHistoryItems)
            {
                item.SetActive(false);
                _historyPool.Enqueue(item);
            }
            _activeHistoryItems.Clear();
        }

        public void ShowRewardPopup(Vector3 worldPosition, long amount)
        {
            RewardPopup popup;

            if (_popupPool.Count > 0)
            {
                popup = _popupPool.Dequeue();
            }
            else
            {
                var obj = Instantiate(_rewardPopupPrefab, _rewardPopupContainer);
                popup = obj.GetComponent<RewardPopup>();
                if (popup != null)
                {
                    popup.OnComplete += () => ReturnPopupToPool(popup);
                }
            }

            if (popup != null && _cachedCamera != null)
            {
                Vector3 screenPos = _cachedCamera.WorldToScreenPoint(worldPosition);
                popup.Show(screenPos, amount);
                _activePopups.Add(popup);
            }
        }

        private void ReturnPopupToPool(RewardPopup popup)
        {
            _activePopups.Remove(popup);
            popup.gameObject.SetActive(false);
            _popupPool.Enqueue(popup);
        }

        public void ShowLevelTransition(int newLevel)
        {
            HideAllOverlays();
            if (_levelTransitionOverlay != null)
            {
                _levelTransitionOverlay.SetActive(true);
                if (_levelTransitionText != null)
                {
                    _levelTransitionText.text = $"Level {newLevel + 1}!";
                }
            }
        }

        public void HideLevelTransition()
        {
            if (_levelTransitionOverlay != null)
            {
                _levelTransitionOverlay.SetActive(false);
            }
        }

        public void ShowSessionExpired(float countdown)
        {
            HideAllOverlays();
            if (_sessionExpiredOverlay != null)
            {
                _sessionExpiredOverlay.SetActive(true);
                UpdateSessionExpiredCountdown(countdown);
            }
        }

        public void UpdateSessionExpiredCountdown(float seconds)
        {
            if (_sessionExpiredText != null)
            {
                int mins = Mathf.FloorToInt(seconds / 60f);
                int secs = Mathf.FloorToInt(seconds % 60f);
                _sessionExpiredText.text = seconds > 0
                    ? $"Session reset in {mins:00}:{secs:00}"
                    : "Starting new session...";
            }
        }

        public void HideSessionExpired()
        {
            if (_sessionExpiredOverlay != null)
            {
                _sessionExpiredOverlay.SetActive(false);
            }
        }

        public void ShowLoading(string message = "Loading...")
        {
            HideAllOverlays();
            if (_loadingOverlay != null)
            {
                _loadingOverlay.SetActive(true);
                if (_loadingText != null)
                {
                    _loadingText.text = message;
                }
            }
        }

        public void HideLoading()
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.SetActive(false);
            }
        }

        public void ShowError(string message)
        {
            HideAllOverlays();
            if (_errorOverlay != null)
            {
                _errorOverlay.SetActive(true);
                if (_errorText != null)
                {
                    _errorText.text = message;
                }
            }
        }

        public void HideError()
        {
            if (_errorOverlay != null)
            {
                _errorOverlay.SetActive(false);
            }
        }

        private void HideAllOverlays()
        {
            if (_levelTransitionOverlay != null) _levelTransitionOverlay.SetActive(false);
            if (_sessionExpiredOverlay != null) _sessionExpiredOverlay.SetActive(false);
            if (_loadingOverlay != null) _loadingOverlay.SetActive(false);
            if (_errorOverlay != null) _errorOverlay.SetActive(false);
        }

        public void ShowRunEnd(RunSummary summary)
        {
            if (_levelTransitionOverlay != null) _levelTransitionOverlay.SetActive(false);
            if (_sessionExpiredOverlay != null) _sessionExpiredOverlay.SetActive(false);
            if (_loadingOverlay != null) _loadingOverlay.SetActive(false);
            if (_errorOverlay != null) _errorOverlay.SetActive(false);

            if (_runEndOverlay != null)
            {
                _runEndOverlay.Show(summary);
            }
        }

        public void HideRunEnd()
        {
            if (_runEndOverlay != null)
            {
                _runEndOverlay.Hide();
            }
        }

        public void RefreshRunEndDisplay()
        {
            if (_runEndOverlay != null)
            {
                _runEndOverlay.RefreshDisplay();
            }
        }
    }
}
