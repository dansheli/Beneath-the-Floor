using UnityEngine;

namespace BeneathTheFloor.WorldRooms
{
    /// <summary>
    /// Simple marker component for the dig shaft center.
    /// Displays a gizmo in the Scene view for easy identification.
    /// </summary>
    public class DigShaftCenterMarker : MonoBehaviour
    {
        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = Color.yellow;
        [SerializeField] private float gizmoRadius = 1.5f;
        [SerializeField] private bool showLabel = true;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);

            // Draw cross lines
            float crossSize = gizmoRadius * 0.8f;
            Gizmos.DrawLine(transform.position + Vector3.left * crossSize,
                           transform.position + Vector3.right * crossSize);
            Gizmos.DrawLine(transform.position + Vector3.forward * crossSize,
                           transform.position + Vector3.back * crossSize);
            Gizmos.DrawLine(transform.position + Vector3.up * crossSize,
                           transform.position + Vector3.down * crossSize);

            if (showLabel)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoRadius + 0.5f),
                    "Dig Shaft Center");
            }
        }
#endif
    }
}
