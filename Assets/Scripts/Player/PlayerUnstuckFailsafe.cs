using UnityEngine;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Failsafe component that detects if the player is stuck inside terrain
    /// and automatically carves an escape route.
    /// </summary>
    public class PlayerUnstuckFailsafe : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("How long the player must be stuck before triggering escape (seconds).")]
        [SerializeField] private float stuckThresholdTime = 1.5f;

        [Tooltip("Density threshold to consider player 'inside' terrain.")]
        [SerializeField] private float stuckDensityThreshold = 0.6f;

        [Tooltip("How often to check if player is stuck (seconds).")]
        [SerializeField] private float checkInterval = 0.25f;

        [Header("Escape")]
        [Tooltip("Radius of escape carve around player.")]
        [SerializeField] private float escapeCarveRadius = 0.8f;

        [Tooltip("Strength of escape carve.")]
        [SerializeField] private float escapeCarveStrength = 1.2f;

        [Tooltip("Maximum escape carves per stuck event.")]
        [SerializeField] private int maxEscapeCarves = 3;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private ChunkManager chunkManager;
        private CharacterController characterController;
        private float stuckStartTime = -1f;
        private float lastCheckTime;
        private int escapeCarvesThisEvent;
        private bool wasStuck;

        private void Start()
        {
            chunkManager = ChunkManager.Instance;
            if (chunkManager == null)
            {
                chunkManager = FindObjectOfType<ChunkManager>();
            }

            characterController = GetComponent<CharacterController>();

            if (chunkManager == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[PlayerUnstuckFailsafe] No ChunkManager found - failsafe disabled");
                enabled = false;
            }
        }

        private void Update()
        {
            if (chunkManager == null) return;

            // Throttle checks
            if (Time.time - lastCheckTime < checkInterval) return;
            lastCheckTime = Time.time;

            // Check if player is inside terrain
            bool isStuck = CheckIfStuck();

            if (isStuck)
            {
                if (!wasStuck)
                {
                    // Just became stuck
                    stuckStartTime = Time.time;
                    escapeCarvesThisEvent = 0;
                    wasStuck = true;

                    if (enableDebugLogs)
                        Debug.Log("[PlayerUnstuckFailsafe] Player appears stuck in terrain...");
                }
                else
                {
                    // Already stuck - check if threshold exceeded
                    float stuckDuration = Time.time - stuckStartTime;

                    if (stuckDuration >= stuckThresholdTime && escapeCarvesThisEvent < maxEscapeCarves)
                    {
                        // Trigger escape carve
                        PerformEscapeCarve();
                        escapeCarvesThisEvent++;
                        stuckStartTime = Time.time; // Reset timer for potential follow-up carves
                    }
                }
            }
            else
            {
                // Not stuck
                if (wasStuck && enableDebugLogs)
                {
                    Debug.Log("[PlayerUnstuckFailsafe] Player is no longer stuck");
                }
                wasStuck = false;
                stuckStartTime = -1f;
            }
        }

        /// <summary>
        /// Check if the player is stuck inside terrain.
        /// </summary>
        private bool CheckIfStuck()
        {
            // Sample density at multiple points around the player
            Vector3 playerPos = transform.position;

            // Check center (chest height)
            float centerDensity = chunkManager.GetDensityAt(playerPos + Vector3.up * 0.8f);

            // Check head
            float headDensity = chunkManager.GetDensityAt(playerPos + Vector3.up * 1.6f);

            // Check feet
            float feetDensity = chunkManager.GetDensityAt(playerPos + Vector3.up * 0.1f);

            // Player is stuck if multiple points are inside solid terrain
            int solidPoints = 0;
            if (centerDensity >= stuckDensityThreshold) solidPoints++;
            if (headDensity >= stuckDensityThreshold) solidPoints++;
            if (feetDensity >= stuckDensityThreshold) solidPoints++;

            // Consider stuck if 2+ points are solid (not just feet touching ground)
            return solidPoints >= 2;
        }

        /// <summary>
        /// Carve an escape route around the player.
        /// </summary>
        private void PerformEscapeCarve()
        {
            if (enableDebugLogs)
                Debug.Log($"[PlayerUnstuckFailsafe] Performing escape carve #{escapeCarvesThisEvent + 1}");

            Vector3 playerPos = transform.position;

            // Carve at multiple heights to create a capsule-shaped escape route
            chunkManager.TryDig(playerPos + Vector3.up * 0.3f, escapeCarveRadius, escapeCarveStrength);
            chunkManager.TryDig(playerPos + Vector3.up * 0.9f, escapeCarveRadius, escapeCarveStrength);
            chunkManager.TryDig(playerPos + Vector3.up * 1.5f, escapeCarveRadius, escapeCarveStrength);

            // Also try to find safe ground below and carve there
            Vector3 safePos = FindSafePositionBelow(playerPos);
            if (safePos != playerPos)
            {
                // Found a safe spot below - carve a path there
                chunkManager.TryDig(safePos + Vector3.up * 0.5f, escapeCarveRadius * 0.8f, escapeCarveStrength);
            }
        }

        /// <summary>
        /// Try to find a safe (air) position below the player.
        /// </summary>
        private Vector3 FindSafePositionBelow(Vector3 startPos)
        {
            float voxelSize = chunkManager.VoxelSize;

            // Check downward for an air pocket
            for (float dy = -voxelSize; dy > -3f; dy -= voxelSize)
            {
                Vector3 checkPos = startPos + Vector3.up * dy;
                float density = chunkManager.GetDensityAt(checkPos);

                if (density < 0.3f) // Found air
                {
                    // Verify there's ground below this air pocket
                    float groundDensity = chunkManager.GetDensityAt(checkPos + Vector3.down * 0.5f);
                    if (groundDensity > 0.5f)
                    {
                        return checkPos;
                    }
                }
            }

            return startPos; // No safe spot found
        }

        /// <summary>
        /// Manually trigger an escape carve (for debug/testing).
        /// </summary>
        public void ForceEscapeCarve()
        {
            PerformEscapeCarve();
        }
    }
}
