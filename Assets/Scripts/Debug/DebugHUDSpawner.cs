using UnityEngine;

namespace BeneathTheFloor.DebugTools
{
    /// <summary>
    /// Automatically spawns the PlayerUpgradeDebugHUD at game start.
    /// Uses RuntimeInitializeOnLoadMethod to ensure it runs without any scene setup.
    /// </summary>
    public static class DebugHUDSpawner
    {
        // DISABLED - Debug HUD no longer auto-spawns. Uncomment to re-enable.
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnDebugHUD()
        {
            // Don't spawn if already exists
            if (PlayerUpgradeDebugHUD.Instance != null)
            {
                UnityEngine.Debug.Log("[DebugHUDSpawner] Debug HUD already exists, skipping spawn.");
                return;
            }

            // Create the debug HUD GameObject
            GameObject hudObject = new GameObject("PlayerUpgradeDebugHUD");
            hudObject.AddComponent<PlayerUpgradeDebugHUD>();

            UnityEngine.Debug.Log("[DebugHUDSpawner] Debug HUD spawned automatically!");
        }
    }
}
