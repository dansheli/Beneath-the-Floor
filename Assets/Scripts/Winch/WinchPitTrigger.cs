using UnityEngine;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// Marker component for pit trigger volumes.
    /// Place this on a trigger collider at the pit opening.
    /// When player enters this trigger, the winch cable attaches.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WinchPitTrigger : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, draw the trigger bounds in editor.")]
        public bool showGizmos = true;

        [Tooltip("Gizmo color for pit trigger area.")]
        public Color gizmoColor = new Color(0f, 1f, 0.5f, 0.3f);

        private Collider triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();

            // Ensure it's a trigger
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
                Debug.LogWarning($"[WinchPitTrigger] Set {gameObject.name} collider to trigger mode.");
            }

            // Tag-based detection is optional - WinchPitTrigger component is sufficient
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Collider col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                // Approximate with sphere
                Gizmos.DrawSphere(capsule.center, capsule.radius);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
