using UnityEngine;

namespace Plinko.Utils
{
    public static class PhysicsOptimizer
    {
        public static void OptimizeForMobile()
        {
            Time.fixedDeltaTime = 1f / 60f;
            Time.maximumDeltaTime = 1f / 30f;

            ConfigurePhysics2D();
        }

        private static void ConfigurePhysics2D()
        {
            Physics2D.velocityIterations = 6;
            Physics2D.positionIterations = 3;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        public static void ConfigureBallRigidbody(Rigidbody2D rb, float mass, float gravityScale)
        {
            if (rb == null) return;

            rb.mass = mass;
            rb.gravityScale = gravityScale;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.sleepMode = RigidbodySleepMode2D.StartAwake;
            rb.constraints = RigidbodyConstraints2D.None;
        }

        public static PhysicsMaterial2D CreateBouncyMaterial(float bounciness, float friction)
        {
            var material = new PhysicsMaterial2D("PlinkoMaterial")
            {
                bounciness = Mathf.Clamp01(bounciness),
                friction = Mathf.Clamp01(friction)
            };

            return material;
        }

        public static void ReducePhysicsQuality()
        {
            Physics2D.velocityIterations = 4;
            Physics2D.positionIterations = 2;
            Time.fixedDeltaTime = 1f / 50f;
        }

        public static void RestorePhysicsQuality()
        {
            Physics2D.velocityIterations = 6;
            Physics2D.positionIterations = 3;
            Time.fixedDeltaTime = 1f / 60f;
        }
    }
}
