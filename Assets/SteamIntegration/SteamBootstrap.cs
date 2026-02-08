using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace BeneathTheFloor.Steam
{
    /// <summary>
    /// Minimal Steam initialization for wishlist overlay functionality.
    /// </summary>
    public class SteamBootstrap : MonoBehaviour
    {
        public static SteamBootstrap Instance { get; private set; }
        public static bool IsSteamInitialized { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if !DISABLESTEAMWORKS
            // NOTE: RestartAppIfNecessary removed - it forces Steam relaunch
            // which prevents local testing and blocks testers without Steam.
            // The game runs standalone; Steam overlay still works if Steam is running.

            try
            {
                IsSteamInitialized = SteamAPI.Init();
                if (IsSteamInitialized)
                {
                    Debug.Log("[Steam] Initialized successfully");
                }
                else
                {
                    Debug.Log("[Steam] Not running via Steam - running standalone");
                }
            }
            catch (System.DllNotFoundException)
            {
                Debug.Log("[Steam] Native libraries not found - running standalone");
            }
#endif
        }

        private void Update()
        {
#if !DISABLESTEAMWORKS
            if (IsSteamInitialized)
            {
                SteamAPI.RunCallbacks();
            }
#endif
        }

        private void OnApplicationQuit()
        {
#if !DISABLESTEAMWORKS
            if (IsSteamInitialized)
            {
                SteamAPI.Shutdown();
            }
#endif
        }
    }
}
