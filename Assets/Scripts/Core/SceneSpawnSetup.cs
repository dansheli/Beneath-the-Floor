using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeneathTheFloor.Core
{
    /// <summary>
    /// Attach this to a GameObject in any scene to handle player spawning.
    /// Automatically positions the player when coming from another scene.
    /// </summary>
    public class SceneSpawnSetup : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private string acceptedSpawnPointId = "BasementEntrance";
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 spawnRotation = Vector3.zero;

        [Header("Safety Settings")]
        [SerializeField] private bool ensureGrounded = true;
        [SerializeField] private float groundCheckDistance = 10f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private void Start()
        {
            CheckAndTeleportPlayer();
        }

        private void CheckAndTeleportPlayer()
        {
            string requestedSpawn = PlayerPrefs.GetString("SpawnPoint", "");

            if (debugMode)
            {
                Debug.Log($"[SceneSpawnSetup] Scene: {SceneManager.GetActiveScene().name}, Requested spawn: '{requestedSpawn}', Accepted: '{acceptedSpawnPointId}'");
            }

            if (string.IsNullOrEmpty(requestedSpawn))
            {
                if (debugMode)
                {
                    Debug.Log("[SceneSpawnSetup] No spawn point requested, using default player position.");
                }
                return;
            }

            if (requestedSpawn == acceptedSpawnPointId)
            {
                TeleportPlayer();
                // Clear the spawn point request
                PlayerPrefs.DeleteKey("SpawnPoint");
                PlayerPrefs.Save();
            }
            else
            {
                if (debugMode)
                {
                    Debug.Log($"[SceneSpawnSetup] Spawn point '{requestedSpawn}' does not match '{acceptedSpawnPointId}', skipping.");
                }
            }
        }

        private void TeleportPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[SceneSpawnSetup] Could not find player with 'Player' tag!");
                return;
            }

            Vector3 targetPosition = spawnPosition;

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

            // Set position and rotation
            player.transform.position = targetPosition;
            player.transform.rotation = Quaternion.Euler(spawnRotation);

            // Re-enable CharacterController
            if (cc != null)
            {
                cc.enabled = true;
            }

            if (debugMode)
            {
                Debug.Log($"[SceneSpawnSetup] Teleported player to {targetPosition}");
            }
        }

        private Vector3 GetGroundedPosition(Vector3 startPosition)
        {
            // Raycast down to find the ground
            Vector3 rayStart = startPosition + Vector3.up * 2f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance + 2f))
            {
                // Place player slightly above the ground
                Vector3 groundedPos = hit.point + Vector3.up * 0.1f;

                if (debugMode)
                {
                    Debug.Log($"[SceneSpawnSetup] Ground found at {hit.point.y}, placing player at {groundedPos.y}");
                }

                return groundedPos;
            }
            else
            {
                Debug.LogWarning($"[SceneSpawnSetup] No ground found below spawn position! Using original position: {startPosition}");
                return startPosition;
            }
        }

        private void OnDrawGizmos()
        {
            // Draw spawn point indicator
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPosition, 0.5f);

            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(spawnPosition, Quaternion.Euler(spawnRotation) * Vector3.forward * 1.5f);

            // Draw player height indicator
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireCube(spawnPosition + Vector3.up * 1f, new Vector3(0.5f, 2f, 0.5f));
        }
    }
}
