using UnityEngine;
using Plinko.Core.Logging;
using Plinko.Data;
using Plinko.Services;

namespace Plinko.Core
{
    public static class GameBootstrapper
    {
        private const string ConfigResourcePath = "Config/GameConfig";
        private static bool _bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped)
            {
                return;
            }

            var logger = new UnityLogger();

            var gameManager = Object.FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                logger.LogError($"[{nameof(GameBootstrapper)}] GameManager not found in scene.");
                return;
            }

            var config = gameManager.Config ?? Resources.Load<GameConfig>(ConfigResourcePath);
            if (config == null)
            {
                logger.LogError($"[{nameof(GameBootstrapper)}] Missing GameConfig at {ConfigResourcePath}.");
                return;
            }

            var preferences = new UnityPreferences();
            var serverService = new MockServerService(config, preferences, logger);
            var sessionManager = new SessionManager(config, serverService, preferences, logger);
            var rewardBatchManager = new RewardBatchManager(serverService, config, logger);

            gameManager.Initialize(config, serverService, sessionManager, rewardBatchManager, logger);
            _bootstrapped = true;
        }
    }
}
