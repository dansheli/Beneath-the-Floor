using UnityEngine;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Place this on the tip/muzzle of a weapon to visualize the projectile exit point and direction.
    /// The local Z-forward (blue arrow) shows the fire direction.
    /// Adjust the transform's rotation to aim the projectile.
    /// </summary>
    public class ProjectileExitGizmo : MonoBehaviour
    {
        [Header("Visualization")]
        [SerializeField] private float arrowLength = 2f;
        [SerializeField] private float sphereRadius = 0.05f;
        [SerializeField] private Color exitPointColor = Color.red;
        [SerializeField] private Color directionColor = Color.cyan;

        [Header("Charge Ball Position")]
        [Tooltip("Drag a child transform here to set where the charge ball appears during reload. If empty, uses this transform's position.")]
        [SerializeField] private Transform chargeBallPoint;
        [SerializeField] private Color chargeBallColor = Color.yellow;

        [Header("Info (Read Only)")]
        [SerializeField] private Vector3 worldDirection;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Vector3 chargeBallWorldPosition;

        /// <summary>
        /// The world-space fire direction (local Z-forward of this transform).
        /// </summary>
        public Vector3 FireDirection => transform.forward;

        /// <summary>
        /// The world-space exit point position.
        /// </summary>
        public Vector3 ExitPoint => transform.position;

        /// <summary>
        /// The world-space charge ball position. Falls back to exit point if not set.
        /// </summary>
        public Vector3 ChargeBallPoint => chargeBallPoint != null ? chargeBallPoint.position : transform.position;

        /// <summary>
        /// The transform for the charge ball point (for parenting). Falls back to this transform.
        /// </summary>
        public Transform ChargeBallTransform => chargeBallPoint != null ? chargeBallPoint : transform;

        private void OnDrawGizmos()
        {
            // Update read-only fields for inspector visibility
            worldDirection = transform.forward;
            worldPosition = transform.position;

            // Draw exit point sphere
            Gizmos.color = exitPointColor;
            Gizmos.DrawSphere(transform.position, sphereRadius);

            // Draw direction arrow (line + cone tip)
            Gizmos.color = directionColor;
            Vector3 endPoint = transform.position + transform.forward * arrowLength;
            Gizmos.DrawLine(transform.position, endPoint);

            // Draw arrowhead (3 lines forming a cone tip)
            float tipSize = arrowLength * 0.15f;
            Vector3 tipBack = endPoint - transform.forward * tipSize;
            Gizmos.DrawLine(endPoint, tipBack + transform.up * tipSize * 0.5f);
            Gizmos.DrawLine(endPoint, tipBack - transform.up * tipSize * 0.5f);
            Gizmos.DrawLine(endPoint, tipBack + transform.right * tipSize * 0.5f);
            Gizmos.DrawLine(endPoint, tipBack - transform.right * tipSize * 0.5f);

            // Draw a small ring at the exit to show spread area
            Gizmos.color = new Color(directionColor.r, directionColor.g, directionColor.b, 0.4f);
            DrawCircle(transform.position, transform.forward, sphereRadius * 3f, 16);

            // Draw charge ball point
            Vector3 chargePos = ChargeBallPoint;
            chargeBallWorldPosition = chargePos;
            Gizmos.color = chargeBallColor;
            Gizmos.DrawWireSphere(chargePos, sphereRadius * 2f);
            // Line connecting exit point to charge point
            if (chargeBallPoint != null)
            {
                Gizmos.color = new Color(chargeBallColor.r, chargeBallColor.g, chargeBallColor.b, 0.4f);
                Gizmos.DrawLine(transform.position, chargePos);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // When selected, draw a more detailed visualization
            Gizmos.color = new Color(directionColor.r, directionColor.g, directionColor.b, 0.2f);

            // Draw trajectory line extended further
            Vector3 farPoint = transform.position + transform.forward * arrowLength * 3f;
            Gizmos.DrawLine(transform.position, farPoint);

            // Draw intermediate distance markers
            for (int i = 1; i <= 3; i++)
            {
                float dist = arrowLength * i;
                Vector3 markerPos = transform.position + transform.forward * dist;
                float radius = sphereRadius * (1f + i * 0.5f);
                DrawCircle(markerPos, transform.forward, radius, 16);
            }
        }

        private void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
        {
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f)
                right = Vector3.Cross(normal, Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, normal).normalized;

            Vector3 prevPoint = center + right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = (float)i / segments * 360f * Mathf.Deg2Rad;
                Vector3 point = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
