using UnityEngine;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.World
{
    /// <summary>
    /// Always-on radar beacon for the engine in the first room.
    /// Guides the player back from underground — the radar always points here
    /// unless a closer, higher-priority target (like a treasure chest) overrides it.
    /// Deactivates after the engine has been activated (crystal inserted).
    /// </summary>
    [RequireComponent(typeof(EngineActivationInteract))]
    public class EngineRadarTarget : MonoBehaviour
    {
        [Header("DEPRECATED - Radar now uses mode system")]
        [SerializeField] private bool disabled = true;

        [Header("Radar Settings")]
        [Tooltip("Priority for radar detection. Low so treasure chests can override when close.")]
        [SerializeField] private int radarPriority = 5;

        [Tooltip("Display name shown in debug")]
        [SerializeField] private string radarDisplayName = "Engine Core";

        private EngineActivationInteract engine;
        private RadarTarget radarTarget;

        private void Awake()
        {
            engine = GetComponent<EngineActivationInteract>();
        }

        private void Start()
        {
            if (disabled) return;

            // Immediately register as radar target — guide the player from the start
            EnableRadarTarget();
        }

        private void Update()
        {
            // Deactivate after engine is powered on — no longer needed
            if (engine != null && engine.IsActivated)
            {
                DisableRadarTarget();
                enabled = false;
            }
        }

        private void EnableRadarTarget()
        {
            if (radarTarget == null)
            {
                radarTarget = gameObject.AddComponent<RadarTarget>();

                var priorityField = typeof(RadarTarget).GetField("priority",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (priorityField != null)
                    priorityField.SetValue(radarTarget, radarPriority);

                var nameField = typeof(RadarTarget).GetField("targetName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nameField != null)
                    nameField.SetValue(radarTarget, radarDisplayName);
            }

            radarTarget.enabled = true;
            radarTarget.SetAsActiveTarget();
        }

        private void DisableRadarTarget()
        {
            if (radarTarget != null)
                radarTarget.enabled = false;
        }

        private void OnDestroy()
        {
            DisableRadarTarget();
        }
    }
}
