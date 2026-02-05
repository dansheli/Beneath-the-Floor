using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Helper object to visually position where terrain chunks will be generated.
    /// Place this object where you want the terrain center to be.
    /// The gizmo shows the exact bounds where chunks will spawn.
    /// </summary>
    public class TerrainBoundsPositioner : MonoBehaviour
    {
        [Header("Terrain Size")]
        [Tooltip("Width and depth of the terrain area (X and Z)")]
        public float horizontalExtent = 12f;

        [Tooltip("How deep the terrain goes (Y)")]
        public float depth = 50f;

        [Header("Vertical Offset")]
        [Tooltip("How far below this object's Y position the terrain starts")]
        public float startBelowY = 0f;

        [Header("Gizmo Settings")]
        public Color boundsColor = new Color(0f, 1f, 0f, 0.3f);
        public Color wireColor = Color.green;
        public bool alwaysShowGizmo = true;

        /// <summary>
        /// Get the bounds that terrain should be generated in.
        /// </summary>
        public Bounds GetBounds()
        {
            Vector3 center = transform.position;
            center.y = transform.position.y - startBelowY - (depth / 2f);
            Vector3 size = new Vector3(horizontalExtent, depth, horizontalExtent);
            return new Bounds(center, size);
        }

        private void OnDrawGizmos()
        {
            if (alwaysShowGizmo)
                DrawGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo();
        }

        private void DrawGizmo()
        {
            Bounds bounds = GetBounds();

            // Draw filled cube
            Gizmos.color = boundsColor;
            Gizmos.DrawCube(bounds.center, bounds.size);

            // Draw wire cube
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // Draw center marker
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.5f);

            // Draw vertical line showing depth
            Gizmos.color = Color.red;
            Vector3 top = transform.position;
            top.y -= startBelowY;
            Vector3 bottom = top;
            bottom.y -= depth;
            Gizmos.DrawLine(top, bottom);

            // Draw corner markers
            Gizmos.color = Color.cyan;
            float half = horizontalExtent / 2f;
            Vector3 basePos = transform.position;
            basePos.y -= startBelowY;

            // Top corners
            Gizmos.DrawWireSphere(basePos + new Vector3(-half, 0, -half), 0.3f);
            Gizmos.DrawWireSphere(basePos + new Vector3(half, 0, -half), 0.3f);
            Gizmos.DrawWireSphere(basePos + new Vector3(-half, 0, half), 0.3f);
            Gizmos.DrawWireSphere(basePos + new Vector3(half, 0, half), 0.3f);
        }

        [ContextMenu("Apply Position to DigBoundsProvider")]
        public void ApplyToDigBoundsProvider()
        {
            var provider = FindObjectOfType<DigBoundsProvider>();
            if (provider != null)
            {
                Bounds bounds = GetBounds();
                Debug.Log($"[TerrainBoundsPositioner] Applying bounds to DigBoundsProvider: center={bounds.center}, size={bounds.size}");
                // The provider will need to be configured in inspector
            }
            else
            {
                Debug.LogWarning("[TerrainBoundsPositioner] No DigBoundsProvider found in scene!");
            }
        }
    }
}
