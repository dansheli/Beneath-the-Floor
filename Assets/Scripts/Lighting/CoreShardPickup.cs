using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Tools;
using BeneathTheFloor.Missions;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Allows the player to pick up the Core Shard crystal.
    /// When picked up, it replaces the player's current tool in hand.
    /// </summary>
    public class CoreShardPickup : MonoBehaviour, IInteractable
    {
        [Header("Pickup Settings")]
        [SerializeField] private string pickupText = "Press E to pick up Core Shard";

        [Header("Held Position")]
        [SerializeField] private Vector3 heldPosition = new Vector3(0.25f, -0.2f, 0.4f);
        [SerializeField] private Vector3 heldRotation = new Vector3(15f, -30f, 10f);
        [SerializeField] private float heldScale = 0.15f;

        [Header("Visual Feedback")]
        [SerializeField] private Color highlightColor = new Color(1.2f, 1.2f, 1.2f);

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private float pickupVolume = 0.7f;

        // State
        private bool isHighlighted = false;
        private MeshRenderer meshRenderer;
        private Color[] originalColors;

        // Static reference to currently held shard
        public static CoreShardPickup HeldShard { get; private set; }
        public static bool IsHoldingShard => HeldShard != null;

        // IInteractable implementation
        public bool CanInteract => !IsHoldingShard; // Can't pick up if already holding one

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            // Store original colors
            if (meshRenderer != null && meshRenderer.materials != null)
            {
                originalColors = new Color[meshRenderer.materials.Length];
                for (int i = 0; i < meshRenderer.materials.Length; i++)
                {
                    if (meshRenderer.materials[i].HasProperty("_BaseColor"))
                    {
                        originalColors[i] = meshRenderer.materials[i].GetColor("_BaseColor");
                    }
                    else if (meshRenderer.materials[i].HasProperty("_Color"))
                    {
                        originalColors[i] = meshRenderer.materials[i].GetColor("_Color");
                    }
                }
            }
        }

        public string GetInteractionText()
        {
            return pickupText;
        }

        public void Interact(GameObject interactor)
        {
            PickUp();
        }

        public void OnHoverEnter()
        {
            if (!isHighlighted)
            {
                SetHighlighted(true);
            }
        }

        public void OnHoverExit()
        {
            if (isHighlighted)
            {
                SetHighlighted(false);
            }
        }

        private void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;

            if (meshRenderer != null && meshRenderer.materials != null)
            {
                for (int i = 0; i < meshRenderer.materials.Length; i++)
                {
                    Color targetColor = highlighted ? originalColors[i] * highlightColor : originalColors[i];

                    if (meshRenderer.materials[i].HasProperty("_BaseColor"))
                    {
                        meshRenderer.materials[i].SetColor("_BaseColor", targetColor);
                    }
                    else if (meshRenderer.materials[i].HasProperty("_Color"))
                    {
                        meshRenderer.materials[i].SetColor("_Color", targetColor);
                    }
                }
            }
        }

        private void PickUp()
        {
            if (IsHoldingShard)
            {
                Debug.Log("[CoreShardPickup] Already holding a shard!");
                return;
            }

            // Play pickup sound
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);
            }

            // Hide player's current tool
            if (HeldToolController.Instance != null)
            {
                HeldToolController.Instance.HideAllTools();
            }

            // Parent to camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                transform.SetParent(mainCam.transform, false);
                transform.localPosition = heldPosition;
                transform.localRotation = Quaternion.Euler(heldRotation);
                transform.localScale = Vector3.one * heldScale;
            }

            // Disable collider so it doesn't interfere
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            // Disable the CrystalGlow pulsing when held (optional - looks cleaner)
            CrystalGlow glow = GetComponent<CrystalGlow>();
            if (glow != null)
            {
                // Keep it enabled for the glow effect
            }

            // Set as held shard
            HeldShard = this;

            // Hide the ring marker
            if (RingMarkerTarget.Instance != null)
            {
                RingMarkerTarget.Instance.HideMarker();
            }
            if (SubtleRingMarker.Instance != null)
            {
                SubtleRingMarker.Instance.Hide();
            }

            // Complete the crystal mission
            if (MissionManager.Instance != null && MissionManager.Instance.CurrentMission != null)
            {
                var mission = MissionManager.Instance.CurrentMission;
                if (mission.useSubtleMarker || mission.missionId == "the_crystal")
                {
                    MissionManager.Instance.CompleteMission();
                }
            }

            Debug.Log("[CoreShardPickup] Picked up Core Shard!");
        }

        /// <summary>
        /// Drop the shard back into the world.
        /// </summary>
        public void Drop(Vector3 position)
        {
            if (HeldShard != this) return;

            // Unparent from camera
            transform.SetParent(null);
            transform.position = position;
            transform.localScale = Vector3.one; // Reset scale

            // Re-enable collider
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }

            // Show player's tool again
            if (HeldToolController.Instance != null)
            {
                HeldToolController.Instance.ShowCurrentTool();
            }

            HeldShard = null;

            Debug.Log("[CoreShardPickup] Dropped Core Shard!");
        }

        /// <summary>
        /// Use the shard (e.g., insert into generator). Destroys the shard.
        /// </summary>
        public void Use()
        {
            if (HeldShard != this) return;

            // Show player's tool again
            if (HeldToolController.Instance != null)
            {
                HeldToolController.Instance.ShowCurrentTool();
            }

            HeldShard = null;

            Debug.Log("[CoreShardPickup] Used Core Shard!");

            // Destroy the shard
            Destroy(gameObject);
        }

        /// <summary>
        /// Release the shard from player's hand without destroying it.
        /// Used when another system (like the generator) takes control of the shard.
        /// </summary>
        public static void ReleaseHeldShard()
        {
            if (HeldShard != null)
            {
                // Show player's tool again
                if (HeldToolController.Instance != null)
                {
                    HeldToolController.Instance.ShowCurrentTool();
                }

                HeldShard = null;
            }
        }

        private void OnDestroy()
        {
            if (HeldShard == this)
            {
                HeldShard = null;

                // Show player's tool again
                if (HeldToolController.Instance != null)
                {
                    HeldToolController.Instance.ShowCurrentTool();
                }
            }
        }
    }
}
