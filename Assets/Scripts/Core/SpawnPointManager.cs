using UnityEngine;

namespace BeneathTheFloor.Core
{
    /// <summary>
    /// Manages player spawn points across scenes.
    /// Place this on a GameObject at the spawn location and set the spawnPointId.
    /// The player will be teleported here when arriving from another scene.
    /// </summary>
    public class SpawnPointManager : MonoBehaviour
    {
        [Header("Spawn Point Settings")]
        [SerializeField] private string spawnPointId = "Default";
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.1f, 0f); // Small offset above ground
        [SerializeField] private bool faceForward = true;

        [Header("Safety Settings")]
        [SerializeField] private bool ensureGrounded = true;
        [SerializeField] private float groundCheckDistance = 5f;
        [SerializeField] private LayerMask groundLayers = -1; // All layers by default

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private Color gizmoColor = Color.green;

        private void Start()
        {
            CheckAndTeleportPlayer();
        }

        private void CheckAndTeleportPlayer()
        {
            // Check if we should teleport the player to this spawn point
            string requestedSpawn = PlayerPrefs.GetString("SpawnPoint", "");

            if (debugMode)
            {
                Debug.Log($"[SpawnPointManager] Checking spawn. Requested: '{requestedSpawn}', This ID: '{spawnPointId}'");
            }

            if (string.IsNullOrEmpty(requestedSpawn))
            {
                if (debugMode)
                {
                    Debug.Log("[SpawnPointManager] No spawn point requested.");
                }
                return;
            }

            if (requestedSpawn == spawnPointId)
            {
                TeleportPlayer();
                // Clear the spawn point request
                PlayerPrefs.DeleteKey("SpawnPoint");
                PlayerPrefs.Save();
            }
        }

        private void TeleportPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[SpawnPointManager] Could not find player with 'Player' tag!");
                return;
            }

            Vector3 targetPosition = transform.position + spawnOffset;

            // Ensure player spawns on ground if enabled
            if (ensureGrounded)
            {
                targetPosition = GetGroundedPosition(targetPosition);
            }

            // Disable CharacterController temporarily to allow position change
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            // Set position
            player.transform.position = targetPosition;

            // Set rotation to face spawn point's forward direction
            if (faceForward)
            {
                player.transform.rotation = transform.rotation;
            }

            // Re-enable CharacterController
            if (cc != null)
            {
                cc.enabled = true;
            }

            if (debugMode)
            {
                Debug.Log($"[SpawnPointManager] Teleported player to {targetPosition} (SpawnPoint: {spawnPointId})");
            }
        }

        private Vector3 GetGroundedPosition(Vector3 startPosition)
        {
            // Raycast down to find the ground
            Vector3 rayStart = startPosition + Vector3.up * 2f; // Start slightly above

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance + 2f, groundLayers))
            {
                // Position player slightly above the ground
                float playerHeight = 2f; // Default CharacterController height
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    CharacterController cc = player.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        playerHeight = cc.height;
                    }
                }

                // Place player so their feet are just above the ground
                Vector3 groundedPos = hit.point + Vector3.up * 0.1f;

                if (debugMode)
                {
                    Debug.Log($"[SpawnPointManager] Ground found at {hit.point.y}, placing player at {groundedPos.y}");
                }

                return groundedPos;
            }
            else
            {
                Debug.LogWarning($"[SpawnPointManager] No ground found below spawn point! Using original position.");
                return startPosition;
            }
        }

        /// <summary>
        /// Call this to manually teleport the player to this spawn point
        /// </summary>
        public void TeleportPlayerHere()
        {
            TeleportPlayer();
        }

        /// <summary>
        /// Static helper to set the spawn point for the next scene load
        /// </summary>
        public static void SetNextSpawnPoint(string spawnId)
        {
            PlayerPrefs.SetString("SpawnPoint", spawnId);
            PlayerPrefs.Save();
        }

        private void OnDrawGizmos()
        {
            // Draw spawn point indicator
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);

            // Draw player height indicator
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(0.5f, 2f, 0.5f));
        }

        private void OnDrawGizmosSelected()
        {
            // Draw ground check ray
            Gizmos.color = Color.yellow;
            Vector3 rayStart = transform.position + Vector3.up * 2f;
            Gizmos.DrawLine(rayStart, rayStart + Vector3.down * (groundCheckDistance + 2f));
        }
    }
}
