using UnityEngine;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Marks a GameObject as a target for the radar/compass.
    /// Place this on the room or objective you want the radar to point to.
    /// </summary>
    public class RadarTarget : MonoBehaviour
    {
        public static RadarTarget Current { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Target Settings")]
        [Tooltip("Priority level - higher priority targets override lower ones")]
        [SerializeField] private int priority = 0;

        [Tooltip("Display name for this target (for debug/UI)")]
        [SerializeField] private string targetName = "Underground Room";

        public int Priority => priority;
        public string TargetName => targetName;
        public Vector3 Position => transform.position;

        private void OnEnable()
        {
            // Register as current target if no target exists or this has higher priority
            if (Current == null || priority > Current.priority)
            {
                Current = this;
                if (enableDebugLogs) Debug.Log($"[RadarTarget] New target registered: {targetName} at {transform.position}");
            }
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
                if (enableDebugLogs) Debug.Log($"[RadarTarget] Target unregistered: {targetName}");
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// Manually set this as the active target.
        /// </summary>
        public void SetAsActiveTarget()
        {
            Current = this;
            if (enableDebugLogs) Debug.Log($"[RadarTarget] Target manually set: {targetName}");
        }
    }
}
