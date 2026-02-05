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
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(4372280)))
            {
                Application.Quit();
                return;
            }

            try
            {
                IsSteamInitialized = SteamAPI.Init();
                if (IsSteamInitialized)
                {
                    Debug.Log("[Steam] Initialized successfully");
                }
                else
                {
                    Debug.Log("[Steam] Not running via Steam client");
                }
            }
            catch (System.DllNotFoundException)
            {
                Debug.Log("[Steam] Native libraries not found");
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
