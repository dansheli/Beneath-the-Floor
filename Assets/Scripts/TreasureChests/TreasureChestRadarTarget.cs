using UnityEngine;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.TreasureChests
{
    /// <summary>
    /// Makes a treasure chest detectable by the radar.
    /// Automatically creates/enables a RadarTarget when the chest is revealed.
    /// Can be configured to only activate under certain conditions.
    /// </summary>
    [RequireComponent(typeof(BuriedTreasureChest))]
    public class TreasureChestRadarTarget : MonoBehaviour
    {
        [Header("Radar Settings")]
        [Tooltip("Priority for radar detection (higher = takes precedence over other targets)")]
        [SerializeField] private int radarPriority = 10;

        [Tooltip("Display name shown in debug")]
        [SerializeField] private string radarDisplayName = "Treasure Chest";

        [Header("Activation")]
        [Tooltip("Only appear on radar when chest is revealed (uncovered)")]
        [SerializeField] private bool requireReveal = true;

        [Tooltip("Only appear on radar when player is within this distance (0 = always)")]
        [SerializeField] private float activationDistance = 0f;

        [Tooltip("Only appear on radar when no other higher-priority target exists")]
        [SerializeField] private bool respectOtherTargets = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Components
        private BuriedTreasureChest chest;
        private RadarTarget radarTarget;
        private bool isRadarActive = false;
        private Transform playerTransform;

        private void Awake()
        {
            chest = GetComponent<BuriedTreasureChest>();
        }

        private void Start()
        {
            // Find player
            var player = FindObjectOfType<Player.FirstPersonController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                playerTransform = Camera.main?.transform;
            }

            // Don't add RadarTarget yet - wait for conditions
        }

        private void Update()
        {
            if (chest == null || chest.IsOpened)
            {
                // Chest opened - disable radar target
                DisableRadarTarget();
                return;
            }

            // Check if should be active
            bool shouldBeActive = ShouldBeActive();

            if (shouldBeActive && !isRadarActive)
            {
                EnableRadarTarget();
            }
            else if (!shouldBeActive && isRadarActive)
            {
                DisableRadarTarget();
            }
        }

        private bool ShouldBeActive()
        {
            // Check reveal requirement
            if (requireReveal && !chest.IsRevealed)
            {
                return false;
            }

            // Check distance requirement
            if (activationDistance > 0 && playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, playerTransform.position);
                if (distance > activationDistance)
                {
                    return false;
                }
            }

            // Check other targets
            if (respectOtherTargets && RadarTarget.Current != null && RadarTarget.Current != radarTarget)
            {
                // Only activate if we have higher priority
                if (RadarTarget.Current.Priority >= radarPriority)
                {
                    return false;
                }
            }

            return true;
        }

        private void EnableRadarTarget()
        {
            if (radarTarget == null)
            {
                radarTarget = gameObject.AddComponent<RadarTarget>();
                // Use reflection or serialized field to set priority and name
                SetRadarTargetFields();
            }

            radarTarget.enabled = true;
            radarTarget.SetAsActiveTarget();
            isRadarActive = true;

            if (debugMode)
            {
                Debug.Log($"[TreasureChestRadarTarget] Enabled radar for: {chest.ChestId}");
            }
        }

        private void SetRadarTargetFields()
        {
            // RadarTarget fields are private with serialized backing
            // We need to use reflection to set them, or modify RadarTarget to expose setters
            // For now, we'll create a workaround by modifying the component directly

            // Try to set via reflection
            var priorityField = typeof(RadarTarget).GetField("priority",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (priorityField != null)
            {
                priorityField.SetValue(radarTarget, radarPriority);
            }

            var nameField = typeof(RadarTarget).GetField("targetName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nameField != null)
            {
                nameField.SetValue(radarTarget, radarDisplayName);
            }
        }

        private void DisableRadarTarget()
        {
            if (radarTarget != null)
            {
                radarTarget.enabled = false;
            }

            isRadarActive = false;

            if (debugMode)
            {
                Debug.Log($"[TreasureChestRadarTarget] Disabled radar for: {chest.ChestId}");
            }
        }

        private void OnDestroy()
        {
            DisableRadarTarget();
        }

        /// <summary>
        /// Force enable radar target regardless of conditions.
        /// </summary>
        public void ForceEnableRadar()
        {
            EnableRadarTarget();
        }

        /// <summary>
        /// Check if this chest is currently detectable by radar.
        /// </summary>
        public bool IsRadarActive => isRadarActive;
    }
}
