using UnityEngine;

namespace Plinko.Utils
{
    public class FPSMonitor
    {
        private readonly float _lowFPSThreshold;
        private readonly float _recoveryFPSThreshold;
        private readonly float _sampleInterval;

        private float _currentFPS;
        private float _sampleTimer;
        private int _frameCount;
        private bool _isQualityReduced;
        private float _qualityReducedTime;

        private const float MIN_REDUCED_QUALITY_DURATION = 5f;

        public event System.Action<float> OnFPSUpdated;
        public event System.Action<bool> OnQualityChanged;

        public float CurrentFPS => _currentFPS;
        public bool IsQualityReduced => _isQualityReduced;

        public FPSMonitor(
            float lowFPSThreshold = 45f,
            float recoveryFPSThreshold = 55f,
            float sampleInterval = 1f)
        {
            _lowFPSThreshold = lowFPSThreshold;
            _recoveryFPSThreshold = recoveryFPSThreshold;
            _sampleInterval = sampleInterval;

            _currentFPS = 60f;
            _isQualityReduced = false;
        }

        public void Tick(float deltaTime)
        {
            _frameCount++;
            _sampleTimer += deltaTime;

            if (_sampleTimer >= _sampleInterval)
            {
                _currentFPS = _frameCount / _sampleTimer;
                _frameCount = 0;
                _sampleTimer = 0f;

                OnFPSUpdated?.Invoke(_currentFPS);

                EvaluateQuality();
            }
        }

        private void EvaluateQuality()
        {
            if (!_isQualityReduced && _currentFPS < _lowFPSThreshold)
            {
                ReduceQuality();
            }
            else if (_isQualityReduced && _currentFPS > _recoveryFPSThreshold)
            {
                float timeInReducedMode = Time.time - _qualityReducedTime;
                if (timeInReducedMode >= MIN_REDUCED_QUALITY_DURATION)
                {
                    RestoreQuality();
                }
            }
        }

        private void ReduceQuality()
        {
            _isQualityReduced = true;
            _qualityReducedTime = Time.time;

            PhysicsOptimizer.ReducePhysicsQuality();
            OnQualityChanged?.Invoke(true);
        }

        private void RestoreQuality()
        {
            _isQualityReduced = false;

            PhysicsOptimizer.RestorePhysicsQuality();
            OnQualityChanged?.Invoke(false);
        }

        public void ForceRestoreQuality()
        {
            if (_isQualityReduced)
            {
                RestoreQuality();
            }
        }

        public void Reset()
        {
            _frameCount = 0;
            _sampleTimer = 0f;
            _currentFPS = 60f;

            if (_isQualityReduced)
            {
                RestoreQuality();
            }
        }
    }
}
