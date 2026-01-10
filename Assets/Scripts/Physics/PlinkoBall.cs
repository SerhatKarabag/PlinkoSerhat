using System;
using UnityEngine;

namespace Plinko.Physics
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlinkoBall : MonoBehaviour
    {
        public event Action<PlinkoBall, int> OnBucketEntered;
        public event Action<PlinkoBall> OnDespawnRequired;

        private Rigidbody2D _rigidbody;
        private SpriteRenderer _renderer;
        private TrailRenderer _trail;

        public int BallIndex { get; private set; }
        public float DropPositionX { get; private set; }
        public int SpawnLevel { get; private set; }
        public bool IsActive { get; private set; }

        private float _despawnY;
        private bool _hasEnteredBucket;

        private float _maxHorizontalSpeed;
        private float _maxVerticalSpeed;
        private float _lateralDamping;

        private float _softBoundaryStrength;
        private float _pyramidCenterX;
        private float _pyramidHalfWidthAtTop;
        private float _pyramidTopY;
        private float _pyramidBottomY;

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _trail = GetComponent<TrailRenderer>();
        }

        public void Initialize(int ballIndex, float dropX, int level, float despawnY, PhysicsSettings settings)
        {
            BallIndex = ballIndex;
            DropPositionX = dropX;
            SpawnLevel = level;
            _despawnY = despawnY;
            _hasEnteredBucket = false;
            IsActive = true;

            _maxHorizontalSpeed = settings.MaxHorizontalSpeed;
            _maxVerticalSpeed = settings.MaxVerticalSpeed;
            _lateralDamping = settings.LateralDamping;

            _softBoundaryStrength = settings.SoftBoundaryStrength;
            _pyramidCenterX = settings.PyramidCenterX;
            _pyramidHalfWidthAtTop = settings.PyramidHalfWidthAtTop;
            _pyramidTopY = settings.PyramidTopY;
            _pyramidBottomY = settings.PyramidBottomY;

            if (_rigidbody != null)
            {
                _rigidbody.mass = settings.Mass;
                _rigidbody.gravityScale = settings.GravityScale;
                _rigidbody.linearDamping = settings.LinearDrag;
                _rigidbody.angularDamping = settings.AngularDrag;
                _rigidbody.linearVelocity = Vector2.zero;
                _rigidbody.angularVelocity = 0f;
                _rigidbody.WakeUp();
            }

            if (_trail != null)
            {
                _trail.Clear();
                _trail.enabled = true;
            }

            if (_renderer != null)
            {
                _renderer.enabled = true;
            }
        }

        public void Reset()
        {
            IsActive = false;
            _hasEnteredBucket = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                _rigidbody.angularVelocity = 0f;
                _rigidbody.Sleep();
            }

            if (_trail != null)
            {
                _trail.Clear();
                _trail.enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!IsActive || _rigidbody == null) return;

            Vector2 vel = _rigidbody.linearVelocity;
            bool velocityModified = false;

            float absVelX = Mathf.Abs(vel.x);
            float absVelY = Mathf.Abs(vel.y);

            if (absVelX > _maxHorizontalSpeed)
            {
                vel.x = Mathf.Sign(vel.x) * _maxHorizontalSpeed;
                velocityModified = true;
            }

            if (absVelY > _maxVerticalSpeed)
            {
                vel.y = Mathf.Sign(vel.y) * _maxVerticalSpeed;
                velocityModified = true;
            }

            if (_lateralDamping > 0f)
            {
                vel.x *= (1f - _lateralDamping);
                velocityModified = true;
            }

            if (velocityModified)
            {
                _rigidbody.linearVelocity = vel;
            }

            if (_softBoundaryStrength > 0f)
            {
                ApplySoftBoundary();
            }
        }

        private void ApplySoftBoundary()
        {
            Vector3 pos = transform.position;

            float t = Mathf.InverseLerp(_pyramidTopY, _pyramidBottomY, pos.y);
            t = Mathf.Clamp01(t);

            float widthMultiplier = Mathf.Lerp(1f, 3.5f, t);
            float currentHalfWidth = _pyramidHalfWidthAtTop * widthMultiplier;

            float distanceFromCenter = pos.x - _pyramidCenterX;
            float absDistance = Mathf.Abs(distanceFromCenter);

            if (absDistance <= currentHalfWidth)
            {
                return;
            }

            float overshoot = absDistance - currentHalfWidth;
            float forceMagnitude = _softBoundaryStrength * overshoot * overshoot;
            float forceDirection = -Mathf.Sign(distanceFromCenter);

            _rigidbody.AddForce(new Vector2(forceDirection * forceMagnitude, 0f), ForceMode2D.Force);

            Vector2 vel = _rigidbody.linearVelocity;
            if (Mathf.Sign(vel.x) == Mathf.Sign(distanceFromCenter))
            {
                vel.x *= 0.9f;
                _rigidbody.linearVelocity = vel;
            }
        }

        public void CheckDespawn()
        {
            if (!IsActive) return;

            if (transform.position.y < _despawnY)
            {
                OnDespawnRequired?.Invoke(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive || _hasEnteredBucket) return;

            var bucket = other.GetComponent<Bucket>();
            if (bucket != null)
            {
                _hasEnteredBucket = true;
                OnBucketEntered?.Invoke(this, bucket.BucketIndex);
            }
        }
    }

    public struct PhysicsSettings
    {
        public float Mass;
        public float Bounciness;
        public float Friction;
        public float GravityScale;
        public float LinearDrag;
        public float AngularDrag;
        public float MaxHorizontalSpeed;
        public float MaxVerticalSpeed;
        public float LateralDamping;
        public float SoftBoundaryStrength;
        public float PyramidCenterX;
        public float PyramidHalfWidthAtTop;
        public float PyramidTopY;
        public float PyramidBottomY;

        public static PhysicsSettings FromConfig(Data.GameConfig config)
        {
            return new PhysicsSettings
            {
                Mass = config.BallMass,
                Bounciness = config.BallBounciness,
                Friction = config.BallFriction,
                GravityScale = config.GravityScale,
                LinearDrag = config.BallLinearDrag,
                AngularDrag = config.BallAngularDrag,
                MaxHorizontalSpeed = config.MaxHorizontalSpeed,
                MaxVerticalSpeed = config.MaxVerticalSpeed,
                LateralDamping = config.LateralDamping,
                SoftBoundaryStrength = config.SoftBoundaryStrength
            };
        }
    }
}
