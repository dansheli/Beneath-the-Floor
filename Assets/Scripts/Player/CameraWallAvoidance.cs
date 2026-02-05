using UnityEngine;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Prevents the first-person camera from clipping through walls.
    /// Uses a sphere cast from the player body center toward the camera to detect walls,
    /// and pulls the camera forward if it would penetrate geometry.
    /// </summary>
    public class CameraWallAvoidance : MonoBehaviour
    {
        [Tooltip("Radius of the sphere cast used to detect walls near the camera")]
        [SerializeField] private float checkRadius = 0.15f;

        [Tooltip("Layers that the camera should not clip through")]
        [SerializeField] private LayerMask wallMask = ~0; // Everything by default

        [Tooltip("Minimum distance from wall surface to camera")]
        [SerializeField] private float wallOffset = 0.05f;

        private Transform playerBody;
        private Camera cam;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.01f;
            }

            // Find the player body (root with CharacterController)
            playerBody = transform;
            while (playerBody.parent != null)
            {
                if (playerBody.parent.GetComponent<CharacterController>() != null)
                {
                    playerBody = playerBody.parent;
                    break;
                }
                playerBody = playerBody.parent;
            }
        }

        private void LateUpdate()
        {
            if (playerBody == null) return;

            // Cast a sphere from the player's eye-level center outward in the camera's forward direction
            // This detects if the camera's near plane would intersect a wall
            Vector3 camPos = transform.position;
            Vector3 camForward = transform.forward;

            // Check directly around the camera position for nearby walls
            // Use 4 directions: forward, left, right, up to catch corner cases
            PushAwayFrom(camPos, camForward);
            PushAwayFrom(camPos, transform.right);
            PushAwayFrom(camPos, -transform.right);
            PushAwayFrom(camPos, transform.up);
        }

        private void PushAwayFrom(Vector3 origin, Vector3 direction)
        {
            float checkDist = checkRadius + wallOffset;
            RaycastHit hit;

            if (Physics.Raycast(origin, direction, out hit, checkDist, wallMask, QueryTriggerInteraction.Ignore))
            {
                // Camera is too close to a wall — push it back along the hit normal
                float penetration = checkDist - hit.distance;
                if (penetration > 0)
                {
                    transform.position += hit.normal * penetration;
                }
            }
        }
    }
}
