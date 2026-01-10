using System;
using UnityEngine;
using TMPro;

namespace Plinko.UI
{
    /// Floating reward popup that appears when balls score.
    public class RewardPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Animation")]
        [SerializeField] private float _duration = 1f;
        [SerializeField] private float _floatSpeed = 100f;
        [SerializeField] private AnimationCurve _alphaCurve;

        public event Action OnComplete;

        private RectTransform _rectTransform;
        private Vector3 _startPosition;
        private float _timer;
        private bool _isActive;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            if (_alphaCurve == null || _alphaCurve.length == 0)
            {
                _alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
            }
        }

        public void Show(Vector3 screenPosition, long amount)
        {
            gameObject.SetActive(true);
            _isActive = true;
            _timer = 0f;

            _startPosition = screenPosition;
            _rectTransform.position = screenPosition;

            if (_text != null)
            {
                _text.text = $"+{amount:N0}";

                // Color based on amount
                if (amount >= 100)
                    _text.color = new Color(1f, 0.84f, 0f); // Gold
                else if (amount >= 20)
                    _text.color = new Color(0.5f, 0.8f, 1f); // Light blue
                else
                    _text.color = Color.white;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive) return;

            _timer += deltaTime;
            float t = _timer / _duration;

            Vector3 newPos = _startPosition;
            newPos.y += _floatSpeed * _timer;
            _rectTransform.position = newPos;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _alphaCurve.Evaluate(t);
            }

            if (_timer >= _duration)
            {
                _isActive = false;
                OnComplete?.Invoke();
            }
        }
    }
}
