using UnityEngine;
using Plinko.Utils;

namespace Plinko.Core
{
    // Handles initial game setup before other systems.
    public static class GameInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            PhysicsOptimizer.OptimizeForMobile();
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
        }
    }
}
