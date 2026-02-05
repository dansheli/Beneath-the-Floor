using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Lighting;
using BeneathTheFloor.Machines;
using BeneathTheFloor.World;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Interactable pickup for the Drill Pike tool.
    /// When player presses E, they receive Tool 4 (Drill Pike) replacing their current tool.
    /// Cannot interact if:
    /// - Player is holding the Core Shard
    /// - Engine has not been activated (crystal not inserted)
    /// </summary>
    public class DrillPikePickup : MonoBehaviour, IInteractable
    {
        // Event fired when drill pike is picked up
        public static event System.Action OnDrillPikePickedUp;
        [Header("Requirements")]
        [Tooltip("Reference to the engine - tool can only be picked up after engine is activated")]
        [SerializeField] private EngineActivationInteract engine;

        [Header("Pickup Settings")]
        [Tooltip("Sound to play when picking up the tool")]
        [SerializeField] private AudioClip pickupSound;

        [Tooltip("Volume of the pickup sound")]
        [SerializeField] private float pickupVolume = 1f;

        [Header("Visual Feedback")]
        [Tooltip("Optional: Disable this GameObject after pickup instead of destroying")]
        [SerializeField] private bool disableInsteadOfDestroy = false;

        /// <summary>
        /// IInteractable: Can this object be interacted with?
        /// Returns false if player is holding the Core Shard or engine is not activated.
        /// </summary>
        public bool CanInteract
        {
            get
            {
                // Cannot interact if holding the Core Shard
                if (CoreShardPickup.IsHoldingShard)
                {
                    return false;
                }

                // Cannot interact if engine is not activated (crystal not inserted)
                if (engine != null && !engine.IsActivated)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// IInteractable: Text shown in the interaction prompt.
        /// Returns empty string if conditions not met (hides prompt entirely).
        /// </summary>
        public string GetInteractionText()
        {
            // If holding shard, return empty to hide the prompt completely
            if (CoreShardPickup.IsHoldingShard)
            {
                return "";
            }

            // If engine not activated, return empty to hide the prompt
            if (engine != null && !engine.IsActivated)
            {
                return "";
            }

            return "Press E to take the Drill Pike";
        }

        /// <summary>
        /// IInteractable: Called when player presses E while looking at this object.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            // Double-check shard condition
            if (CoreShardPickup.IsHoldingShard)
            {
                Debug.Log("[DrillPikePickup] Cannot pick up - player is holding Core Shard");
                return;
            }

            // Double-check engine condition
            if (engine != null && !engine.IsActivated)
            {
                Debug.Log("[DrillPikePickup] Cannot pick up - engine not activated yet");
                return;
            }

            // Give the player Tool 4 (Drill Pike)
            if (HeldToolController.Instance != null)
            {
                // Make sure tools are visible
                if (!HeldToolController.Instance.AreToolsVisible())
                {
                    // If player hasn't received first tool yet, enable tool visibility
                    HeldToolController.Instance.ShowCurrentTool();
                }

                // Switch to Tool 4 (Drill Pike) at base tier
                HeldToolController.Instance.SetActiveTool(3); // 3 = Tool 4 (0-indexed)

                // Tool 4 is stronger than max upgrades — max out the basement
                // upgrade station so it can never downgrade the player
                if (UpgradeStation.Instance != null)
                {
                    UpgradeStation.Instance.MaxOutToolUpgrades();
                }

                // Fire event for mission system
                OnDrillPikePickedUp?.Invoke();

                // Play pickup sound
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);
                }

                // Remove this pickup from the scene
                if (disableInsteadOfDestroy)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.LogWarning("[DrillPikePickup] HeldToolController.Instance is null!");
            }
        }

        /// <summary>
        /// IInteractable: Called when player starts looking at this object.
        /// </summary>
        public void OnHoverEnter()
        {
            // Optional: Add highlight effect
        }

        /// <summary>
        /// IInteractable: Called when player stops looking at this object.
        /// </summary>
        public void OnHoverExit()
        {
            // Optional: Remove highlight effect
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw a small icon to show this is a pickup
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
#endif
    }
}
