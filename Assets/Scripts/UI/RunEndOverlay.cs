using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Plinko.Data;

namespace Plinko.UI
{
    // Overlay displayed when a run ends, showing the run summary
    public class RunEndOverlay : MonoBehaviour
    {
        [Header("Main Container")]
        [SerializeField] private GameObject _container;

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Points Display")]
        [SerializeField] private TextMeshProUGUI _totalPointsText;
        [SerializeField] private TextMeshProUGUI _verifiedPointsText;
        [SerializeField] private TextMeshProUGUI _pendingPointsText;

        [Header("Stats Display")]
        [SerializeField] private TextMeshProUGUI _ballsDroppedText;
        [SerializeField] private TextMeshProUGUI _highestLevelText;

        [Header("Rejection Info (optional)")]
        [SerializeField] private GameObject _rejectionContainer;
        [SerializeField] private TextMeshProUGUI _rejectedBallsText;
        [SerializeField] private TextMeshProUGUI _rejectedPointsText;

        [Header("Retry Info (optional)")]
        [SerializeField] private GameObject _retryContainer;
        [SerializeField] private TextMeshProUGUI _retryingBatchesText;

        [Header("Restart Button")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private TextMeshProUGUI _restartButtonText;

        [Header("Status Text (shows while finalizing)")]
        [SerializeField] private TextMeshProUGUI _statusText;

        public event Action OnRestartClicked;
        private RunSummary _currentSummary;
        private bool _isVisible;

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(HandleRestartClick);
            }
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClick);
            }
        }

        public void Show(RunSummary summary)
        {
            _currentSummary = summary;
            _isVisible = true;
            gameObject.SetActive(true);

            if (_container != null && _container != gameObject)
            {
                _container.SetActive(true);
            }

            transform.SetAsLastSibling();
            UpdateDisplay();
        }

        public void Hide()
        {
            _isVisible = false;
            _currentSummary = null;

            if (_container != null && _container != gameObject)
            {
                _container.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void RefreshDisplay()
        {
            if (_isVisible && _currentSummary != null)
            {
                UpdateDisplay();
            }
        }

        public bool IsValidationComplete()
        {
            if (_currentSummary == null) return true;
            return _currentSummary.PendingPoints <= 0 && !_currentSummary.HasRetryingBatches;
        }

        private void UpdateDisplay()
        {
            if (_currentSummary == null)
            {
                Debug.LogWarning("[RunEndOverlay] Summary is null");
                return;
            }

            var summary = _currentSummary;

            SetText(_titleText, "Run Finished!");
            SetText(_totalPointsText, $"Total Points: {summary.TotalPointsEarned:N0}");
            SetText(_verifiedPointsText, $"Verified: {summary.VerifiedPoints:N0}");

            bool showPending = summary.PendingPoints > 0;
            if (showPending)
            {
                string pendingText = summary.HasRetryingBatches
                    ? $"Pending: {summary.PendingPoints:N0} ({summary.RetryingBatchCount} retrying)"
                    : $"Pending: {summary.PendingPoints:N0}";
                SetText(_pendingPointsText, pendingText);
            }
            SetActive(_pendingPointsText, showPending);

            SetText(_ballsDroppedText, $"Balls Dropped: {summary.TotalBallsDropped}");
            SetText(_highestLevelText, $"Highest Level: {summary.HighestLevelReached + 1}");

            bool showRejections = summary.HasRejections;
            SetActive(_rejectionContainer, showRejections);
            if (showRejections)
            {
                SetText(_rejectedBallsText, $"Rejected Balls: {summary.RejectedBallCount}");
                SetText(_rejectedPointsText, $"Rejected Points: {summary.RejectedPoints:N0}");
            }

            SetActive(_retryContainer, false);

            UpdateRestartButtonState();
        }

        private void UpdateRestartButtonState()
        {
            bool validationComplete = IsValidationComplete();

            if (_restartButton != null)
            {
                _restartButton.interactable = validationComplete;
            }

            if (_restartButtonText != null)
            {
                _restartButtonText.text = validationComplete ? "Restart" : "Verifying...";
            }

            if (_statusText != null)
            {
                if (!validationComplete)
                {
                    _statusText.gameObject.SetActive(true);
                    _statusText.text = "Finalizing rewards...";
                }
                else
                {
                    _statusText.gameObject.SetActive(false);
                }
            }
        }

        private void HandleRestartClick()
        {
            if (IsValidationComplete())
            {
                OnRestartClicked?.Invoke();
            }
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text;
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target == null) return;
            target.SetActive(isActive);
        }

        private static void SetActive(Component target, bool isActive)
        {
            if (target == null) return;
            target.gameObject.SetActive(isActive);
        }
    }
}