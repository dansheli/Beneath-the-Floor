using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Links a tool visual/prefab to its DigToolProfile.
    /// Attach this to tool GameObjects used by HeldToolController.
    /// </summary>
    public class ToolDigProfileLink : MonoBehaviour
    {
        [Header("Dig Profile")]
        [Tooltip("The dig profile for this tool.")]
        public DigToolProfile profile;

        [Header("Debug")]
        public bool showDebugInfo = false;

        /// <summary>
        /// Check if this tool has a valid profile.
        /// </summary>
        public bool HasValidProfile => profile != null;

        private void OnValidate()
        {
            // Don't warn - profiles are assigned at runtime by PlayerToolVisualController
        }

        private void OnDrawGizmosSelected()
        {
            if (showDebugInfo && profile != null)
            {
                // Draw dig radius sphere at tool position
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, profile.digRadius);
            }
        }
    }
}
