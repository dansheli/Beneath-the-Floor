using UnityEngine;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Simple marker component for placing dig area objectives in the scene.
    /// Place this on an empty GameObject where you want the mission marker to appear.
    /// The mission system will find this by name (markerTargetName in MissionData).
    /// </summary>
    public class DigAreaMarker : MonoBehaviour
    {
        [Header("Editor Visualization")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.9f, 0.3f, 0.8f);
        [SerializeField] private float gizmoRadius = 0.5f;
        [SerializeField] private float gizmoHeight = 2f;

        private void OnDrawGizmos()
        {
            DrawMarkerGizmo(0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            DrawMarkerGizmo(1f);
        }

        private void DrawMarkerGizmo(float alphaMultiplier)
        {
            Color color = gizmoColor;
            color.a *= alphaMultiplier;
            Gizmos.color = color;

            // Draw sphere at position
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);

            // Draw vertical line (beam indicator)
            Vector3 bottom = transform.position;
            Vector3 top = transform.position + Vector3.up * gizmoHeight;
            Gizmos.DrawLine(bottom, top);

            // Draw diamond at top
            float diamondSize = 0.2f;
            Vector3 diamondPos = top + Vector3.up * 0.5f;
            Gizmos.DrawLine(diamondPos + Vector3.up * diamondSize, diamondPos + Vector3.right * diamondSize);
            Gizmos.DrawLine(diamondPos + Vector3.right * diamondSize, diamondPos + Vector3.down * diamondSize);
            Gizmos.DrawLine(diamondPos + Vector3.down * diamondSize, diamondPos + Vector3.left * diamondSize);
            Gizmos.DrawLine(diamondPos + Vector3.left * diamondSize, diamondPos + Vector3.up * diamondSize);

            // Draw ring on ground
            int segments = 24;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 p1 = transform.position + new Vector3(Mathf.Cos(angle1) * gizmoRadius, 0, Mathf.Sin(angle1) * gizmoRadius);
                Vector3 p2 = transform.position + new Vector3(Mathf.Cos(angle2) * gizmoRadius, 0, Mathf.Sin(angle2) * gizmoRadius);

                Gizmos.DrawLine(p1, p2);
            }

            // Draw label
            #if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoHeight + 1f), "Dig Area Marker");
            #endif
        }
    }
}
