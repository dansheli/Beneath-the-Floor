using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace BeneathTheFloor.Steam
{
    /// <summary>
    /// Opens Steam store page overlay for wishlisting the main game.
    /// </summary>
    public class WishlistOverlayButton : MonoBehaviour
    {
        private const uint MAIN_GAME_APP_ID = 4372280;
        private const string STORE_URL = "https://store.steampowered.com/app/4372280";

        public void OpenWishlistOverlay()
        {
#if !DISABLESTEAMWORKS
            if (SteamBootstrap.IsSteamInitialized)
            {
                SteamFriends.ActivateGameOverlayToStore(new AppId_t(MAIN_GAME_APP_ID), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
                return;
            }
#endif
            Application.OpenURL(STORE_URL);
        }
    }
}
