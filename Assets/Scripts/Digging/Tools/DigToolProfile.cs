using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// ScriptableObject defining a digging tool's properties.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDigTool", menuName = "Beneath The Floor/Digging/Tool Profile")]
    public class DigToolProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this tool.")]
        public string toolId = "tool_default";

        [Tooltip("Display name shown in UI.")]
        public string displayName = "Basic Tool";

        [Header("Dig Properties")]
        [Tooltip("Radius of the spherical dig brush in meters.")]
        [Range(0.1f, 3f)]
        public float digRadius = 0.5f;

        [Tooltip("Power of the dig - how much density is removed per dig.")]
        [Range(0.1f, 2f)]
        public float digPower = 0.8f;

        [Tooltip("Time in seconds to complete one dig action (for Click mode).")]
        [Range(0.1f, 2f)]
        public float digDuration = 0.5f;

        [Tooltip("Input mode - Click for single digs, Hold for continuous.")]
        public DigInputMode inputMode = DigInputMode.Click;

        [Header("Energy")]
        [Tooltip("Energy cost per dig action.")]
        [Range(0f, 50f)]
        public float energyCostPerDig = 8f;

        [Header("Tool Restrictions")]
        [Tooltip("Maximum depth (in meters) this tool can dig. Beyond this depth, digging is blocked until upgrade.\n\n" +
                 "Recommended values:\n" +
                 "- Tier 1: 10m\n" +
                 "- Tier 2: 25m\n" +
                 "- Tier 3: 50m or 0 (unlimited)")]
        public float maxDigDepth = 10f;

        [Tooltip("Tool efficiency multiplier for resource extraction.")]
        [Range(0.5f, 3f)]
        public float resourceEfficiency = 1f;

        [Header("Layer System")]
        [Tooltip("Hardness rating determines what layers this tool can dig. Higher = stronger tool.\n" +
                 "Tier 1 = 1.5, Tier 2 = 2.5, Tier 3 = 4.0")]
        [Range(0.5f, 10f)]
        public float hardnessRating = 1.5f;

        [Header("Visuals")]
        [Tooltip("Icon for UI display.")]
        public Sprite icon;

        /// <summary>
        /// Check if this tool can dig at the specified depth.
        /// </summary>
        public bool CanDigAtDepth(float depth)
        {
            return maxDigDepth <= 0f || depth <= maxDigDepth;
        }
    }
}
