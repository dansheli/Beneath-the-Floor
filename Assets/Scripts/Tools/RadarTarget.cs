using System.Collections.Generic;
using UnityEngine;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Marks a GameObject as a target for the radar/compass.
    /// Place this on the room or objective you want the radar to point to.
    /// Supports mode filtering so different radar modes show different targets.
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

        [Tooltip("Radar mode this target belongs to (0 = unfiltered/legacy, 1 = TreasureChests, 2 = CoreShard)")]
        [SerializeField] private int targetMode = 0;

        public int Priority => priority;
        public string TargetName => targetName;
        public int TargetMode => targetMode;
        public Vector3 Position => transform.position;

        // Track all enabled targets for mode-based refresh
        private static readonly List<RadarTarget> allEnabled = new List<RadarTarget>();

        private void OnEnable()
        {
            allEnabled.Add(this);

            // Register as current target if mode matches and priority is higher
            if (ShouldBeCurrentTarget())
            {
                Current = this;
                if (enableDebugLogs) Debug.Log($"[RadarTarget] New target registered: {targetName} (mode={targetMode}) at {transform.position}");
            }
        }

        private void OnDisable()
        {
            allEnabled.Remove(this);

            if (Current == this)
            {
                Current = null;
                if (enableDebugLogs) Debug.Log($"[RadarTarget] Target unregistered: {targetName}");
                // Find next best target
                FindBestTarget();
            }
        }

        private void OnDestroy()
        {
            allEnabled.Remove(this);

            if (Current == this)
            {
                Current = null;
            }
        }

        private bool ShouldBeCurrentTarget()
        {
            // Mode 0 = unfiltered (legacy), always eligible
            // Otherwise, must match the current radar mode
            if (targetMode != 0)
            {
                if (RadarTool.Instance != null && targetMode != RadarTool.Instance.CurrentMode)
                    return false;
            }

            // Check priority against current
            if (Current == null) return true;

            // If current is wrong mode, we take over
            if (Current.targetMode != 0 && RadarTool.Instance != null && Current.targetMode != RadarTool.Instance.CurrentMode)
                return true;

            return priority > Current.priority;
        }

        /// <summary>
        /// Re-evaluate all enabled RadarTargets and pick the best one for the active mode.
        /// Call this when the radar mode changes.
        /// </summary>
        public static void RefreshActiveTarget()
        {
            Current = null;
            FindBestTarget();
        }

        private static void FindBestTarget()
        {
            int activeMode = RadarTool.Instance != null ? RadarTool.Instance.CurrentMode : 1;
            RadarTarget best = null;

            for (int i = allEnabled.Count - 1; i >= 0; i--)
            {
                if (allEnabled[i] == null || !allEnabled[i].isActiveAndEnabled)
                {
                    allEnabled.RemoveAt(i);
                    continue;
                }

                var t = allEnabled[i];
                // Mode 0 = unfiltered (always eligible), otherwise must match active mode
                if (t.targetMode != 0 && t.targetMode != activeMode)
                    continue;

                if (best == null || t.priority > best.priority)
                    best = t;
            }

            if (best != null)
                Current = best;
        }

        /// <summary>
        /// Manually set this as the active target.
        /// </summary>
        public void SetAsActiveTarget()
        {
            Current = this;
            if (enableDebugLogs) Debug.Log($"[RadarTarget] Target manually set: {targetName}");
        }

        // Public setters for runtime configuration (avoids reflection)
        public void SetPriority(int p) { priority = p; }
        public void SetTargetName(string n) { targetName = n; }
        public void SetTargetMode(int mode) { targetMode = mode; }
    }
}
