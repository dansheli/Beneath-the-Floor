using UnityEngine;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Interaction;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// A fixed/pre-placed resource node in the scene.
    /// Place these in the hierarchy where you want resources to appear.
    /// The visual will show when the player digs near this position.
    /// Implements IInteractable to show resource name when aimed at.
    /// </summary>
    public class FixedResourceNode : MonoBehaviour, IInteractable
    {
        [Header("Resource Settings")]
        [Tooltip("Visual prefab to spawn when revealed. If null, uses default sphere.")]
        public GameObject visualPrefab;

        [Tooltip("Resource tier (1-4). Affects value and drop count.")]
        [Range(1, 4)]
        public int tier = 1;

        [Tooltip("Size multiplier for the visual.")]
        [Range(0.1f, 3f)]
        public float size = 1f;

        [Header("Reveal Settings")]
        [Tooltip("How close the player must dig to reveal this resource.")]
        [Range(0.3f, 2f)]
        public float revealDistance = 0.8f;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool isRevealed = false;
        [SerializeField] private bool isCollected = false;
        [SerializeField] private float currentExposure = 0f;

        // Runtime references
        private GameObject spawnedVisual;
        private Vector3 revealDirection = Vector3.up;
        private ChunkManager chunkManager;

        // Constants
        private const float REVEAL_THRESHOLD = 0.08f;
        private const float CRUMBLE_THRESHOLD = 0.70f;

        // Properties
        public bool IsRevealed => isRevealed;
        public bool IsCollected => isCollected;
        public float CurrentExposure => currentExposure;

        private void Start()
        {
            chunkManager = ChunkManager.Instance ?? FindObjectOfType<ChunkManager>();

            // Subscribe to dig events
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted += OnDigCompleted;
            }
            else
            {
                StartCoroutine(DelayedSubscribe());
            }
        }

        private System.Collections.IEnumerator DelayedSubscribe()
        {
            yield return new WaitForSeconds(0.5f);
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted += OnDigCompleted;
            }
        }

        private void OnDestroy()
        {
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.OnDigCompleted -= OnDigCompleted;
            }

            if (spawnedVisual != null)
            {
                Destroy(spawnedVisual);
            }
        }

        private void OnDigCompleted(DigResult result)
        {
            if (!result.Success || isCollected)
                return;

            Vector3 digPos = result.Operation.WorldPosition;
            float digRadius = result.Operation.Radius;

            // Check if dig is near this node
            float distToNode = Vector3.Distance(digPos, transform.position);
            float effectiveRadius = revealDistance * size;

            if (distToNode > digRadius + effectiveRadius + 2f) // Increased margin
                return; // Too far

            // Calculate exposure
            float newExposure = CalculateExposure();
            float prevExposure = currentExposure;
            currentExposure = newExposure;


            // Skip if fully in air (already dug area)
            if (prevExposure == 0f && newExposure >= 0.95f)
            {
                isCollected = true;
                return;
            }

            // REVEAL: Create visual when first exposed (removed upper limit check)
            if (!isRevealed && newExposure >= REVEAL_THRESHOLD)
            {
                revealDirection = (digPos - transform.position).normalized;
                if (revealDirection.sqrMagnitude < 0.01f)
                    revealDirection = Vector3.up;

                CreateVisual();
                isRevealed = true;
            }

            // Update visual as exposure changes
            if (isRevealed && spawnedVisual != null)
            {
                UpdateVisual();
            }

            // CRUMBLE: Break into pickups when fully exposed
            if (newExposure >= CRUMBLE_THRESHOLD && !isCollected)
            {
                Crumble();
            }
        }

        private float CalculateExposure()
        {
            if (chunkManager == null)
                return 0f;

            // Always use parent position (where FixedResourceNode is placed)
            Vector3 centerPos = transform.position;

            // Sample points on sphere surface
            int sampleCount = 12;
            int airSamples = 0;
            float isoLevel = 0.5f;

            // Use a consistent radius based on node settings
            float radius = revealDistance * size;

            for (int i = 0; i < sampleCount; i++)
            {
                // Golden spiral distribution
                float t = (float)i / sampleCount;
                float inclination = Mathf.Acos(1f - 2f * t);
                float azimuth = Mathf.PI * 2f * 1.618033988749f * i;

                Vector3 dir = new Vector3(
                    Mathf.Sin(inclination) * Mathf.Cos(azimuth),
                    Mathf.Sin(inclination) * Mathf.Sin(azimuth),
                    Mathf.Cos(inclination)
                );

                Vector3 samplePos = centerPos + dir * radius;
                float density = chunkManager.GetDensityAt(samplePos);

                if (density < isoLevel)
                    airSamples++;
            }

            float exposure = (float)airSamples / sampleCount;
            return exposure;
        }

        private void CreateVisual()
        {
            // Spawn at parent position (local 0,0,0)
            if (visualPrefab != null)
            {
                spawnedVisual = Instantiate(visualPrefab, transform.position, Quaternion.identity);
            }
            else
            {
                // Default sphere
                spawnedVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spawnedVisual.transform.position = transform.position;

                // Color by tier
                var renderer = spawnedVisual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader == null)
                        urpShader = Shader.Find("Standard");

                    Material mat = new Material(urpShader);
                    Color tierColor = tier switch
                    {
                        1 => new Color(0.72f, 0.45f, 0.20f), // Copper
                        2 => new Color(0.75f, 0.75f, 0.78f), // Silver
                        3 => new Color(1.0f, 0.84f, 0.0f),   // Gold
                        4 => new Color(0.58f, 0.44f, 0.86f), // Purple
                        _ => Color.gray
                    };
                    mat.SetColor("_BaseColor", tierColor);
                    mat.SetFloat("_Metallic", 0.6f);
                    mat.SetFloat("_Smoothness", 0.7f);
                    renderer.material = mat;
                }
            }

            spawnedVisual.name = $"ResourceVisual_T{tier}";
            spawnedVisual.transform.SetParent(transform);

            // Fixed scale 5x5x5
            spawnedVisual.transform.localScale = new Vector3(5f, 5f, 5f);

            // Ensure collider exists for raycast interaction detection
            var col = spawnedVisual.GetComponent<Collider>();
            if (col == null)
            {
                col = spawnedVisual.AddComponent<SphereCollider>();
            }
            col.isTrigger = true; // Trigger so it doesn't affect physics
        }

        private void UpdateVisual()
        {
            if (spawnedVisual == null)
                return;

            // Keep visual at parent position (local 0,0,0)
            spawnedVisual.transform.position = transform.position;

            // Fixed scale 5x5x5
            spawnedVisual.transform.localScale = new Vector3(5f, 5f, 5f);
        }

        private void Crumble()
        {
            isCollected = true;

            // Play crumble animation
            if (spawnedVisual != null)
            {
                StartCoroutine(PlayCrumbleAndSpawnPickups());
            }
            else
            {
                SpawnPickups();
            }
        }

        private System.Collections.IEnumerator PlayCrumbleAndSpawnPickups()
        {
            if (spawnedVisual == null)
            {
                SpawnPickups();
                yield break;
            }


            Vector3 startScale = spawnedVisual.transform.localScale;
            Vector3 startPos = spawnedVisual.transform.position;
            float duration = 0.6f; // Longer duration
            float elapsed = 0f;

            // Get renderer for color flash
            var renderer = spawnedVisual.GetComponentInChildren<Renderer>();
            Color originalColor = Color.white;
            if (renderer != null && renderer.material != null)
            {
                originalColor = renderer.material.color;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Shrink with easing
                float shrinkT = t * t; // Accelerate shrink
                spawnedVisual.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.1f, shrinkT);

                // Bigger shake that decreases over time
                float shakeIntensity = 0.3f * (1f - t);
                Vector3 shake = Random.insideUnitSphere * shakeIntensity;
                spawnedVisual.transform.position = startPos + shake;

                // Flash white then to original
                if (renderer != null && renderer.material != null)
                {
                    float flash = Mathf.PingPong(elapsed * 10f, 1f);
                    renderer.material.color = Color.Lerp(originalColor, Color.white, flash * (1f - t));
                }

                yield return null;
            }


            // Hide visual before spawning pickups
            spawnedVisual.SetActive(false);

            SpawnPickups();

            Destroy(spawnedVisual);
            spawnedVisual = null;
        }

        private void SpawnPickups()
        {
            // Get drop count from config
            var config = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
            int dropCount = config != null ? config.GetDropCountForTier(tier) : 3;

            // Spawn at visual position (where the rock actually is)
            Vector3 spawnCenter = (spawnedVisual != null) ? spawnedVisual.transform.position : transform.position;


            // Find player for direction
            Vector3 playerPos = spawnCenter;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerPos = player.transform.position;

            Vector3 toPlayer = (playerPos - spawnCenter).normalized;

            // Find safe exit points around the node instead of naive random offsets
            Vector3[] safePoints = FindSafeSpawnPositions(spawnCenter, dropCount);

            for (int i = 0; i < dropCount; i++)
            {
                SpawnPickupItem(safePoints[i], i, toPlayer);
            }
        }

        /// <summary>
        /// Find safe spawn positions around a node center.
        /// Generates candidate points on a sphere, raycasts down to find ground,
        /// validates clearance, and returns positions slightly above the surface.
        /// Falls back to upward offset if no safe point is found.
        /// </summary>
        private Vector3[] FindSafeSpawnPositions(Vector3 center, int count)
        {
            const int candidateCount = 12;
            const float sphereRadius = 0.9f;
            const float clearanceRadius = 0.15f;
            const float aboveGround = 0.08f;
            const float rayStartOffset = 1.5f;
            const float rayMaxDistance = 4f;

            Vector3[] results = new Vector3[count];
            bool[] assigned = new bool[count];
            int assignedCount = 0;

            // Generate candidate exit points on a sphere using golden spiral
            Vector3[] candidates = new Vector3[candidateCount];
            for (int i = 0; i < candidateCount; i++)
            {
                float t = (float)i / candidateCount;
                float inclination = Mathf.Acos(1f - 2f * t);
                float azimuth = Mathf.PI * 2f * 1.618033988749f * i;

                Vector3 dir = new Vector3(
                    Mathf.Sin(inclination) * Mathf.Cos(azimuth),
                    Mathf.Sin(inclination) * Mathf.Sin(azimuth),
                    Mathf.Cos(inclination)
                );

                // Add small randomness for natural look
                dir += new Vector3(
                    Random.Range(-0.15f, 0.15f),
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.15f, 0.15f)
                );
                dir.Normalize();

                candidates[i] = center + dir * sphereRadius;
            }

            // Shuffle candidates so we don't always pick the same subset
            for (int i = candidateCount - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector3 tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            // For each candidate: try to find a safe ground position
            for (int c = 0; c < candidateCount && assignedCount < count; c++)
            {
                Vector3 candidate = candidates[c];
                Vector3 rayStart = candidate + Vector3.up * rayStartOffset;

                // Raycast down to find ground surface
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayMaxDistance))
                {
                    Vector3 groundPoint = hit.point + Vector3.up * aboveGround;

                    // Check the point is clear (not inside solid geometry)
                    if (!Physics.CheckSphere(groundPoint, clearanceRadius))
                    {
                        results[assignedCount] = groundPoint;
                        assigned[assignedCount] = true;
                        assignedCount++;
                        continue;
                    }

                    // Try pushing up incrementally if inside geometry
                    for (int nudge = 1; nudge <= 4; nudge++)
                    {
                        Vector3 nudgedPoint = groundPoint + Vector3.up * (nudge * 0.15f);
                        if (!Physics.CheckSphere(nudgedPoint, clearanceRadius))
                        {
                            results[assignedCount] = nudgedPoint;
                            assigned[assignedCount] = true;
                            assignedCount++;
                            break;
                        }
                    }
                }
            }

            // Fill any remaining slots with fallback (upward offset from center)
            for (int i = 0; i < count; i++)
            {
                if (!assigned[i])
                {
                    results[i] = center + new Vector3(
                        Random.Range(-0.2f, 0.2f),
                        0.5f + Random.Range(0f, 0.3f),
                        Random.Range(-0.2f, 0.2f)
                    );
                }
            }

            return results;
        }

        private void SpawnPickupItem(Vector3 position, int index, Vector3 toPlayer)
        {
            GameObject pickup;

            // Use same prefab as ResourceVisual
            if (visualPrefab != null)
            {
                pickup = Instantiate(visualPrefab, position, Random.rotation);
            }
            else
            {
                // Fallback to sphere if no prefab
                pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pickup.transform.position = position;
                pickup.transform.rotation = Random.rotation;

                var renderer = pickup.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                        shader = Shader.Find("Standard");

                    Material mat = new Material(shader);
                    Color tierColor = tier switch
                    {
                        1 => new Color(0.72f, 0.45f, 0.20f),
                        2 => new Color(0.75f, 0.75f, 0.78f),
                        3 => new Color(1.0f, 0.84f, 0.0f),
                        4 => new Color(0.58f, 0.44f, 0.86f),
                        _ => Color.gray
                    };
                    mat.SetColor("_BaseColor", tierColor);
                    mat.SetFloat("_Metallic", 0.6f);
                    mat.SetFloat("_Smoothness", 0.5f);
                    renderer.material = mat;
                }
            }

            pickup.name = $"Pickup_T{tier}_{index}";
            pickup.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f); // Size 1.5x1.5x1.5

            // Set layer to "Interactable" if it exists
            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer >= 0)
                pickup.layer = interactLayer;

            // No tag needed - IInteractable handles interaction

            // Ensure collider exists and is solid
            var collider = pickup.GetComponent<Collider>();
            if (collider == null)
            {
                var sphereCol = pickup.AddComponent<SphereCollider>();
                sphereCol.radius = 0.08f;
                collider = sphereCol;
            }
            else if (collider is SphereCollider sc)
            {
                sc.radius = 0.08f;
            }
            collider.isTrigger = false;

            // Add rigidbody with normal physics
            var rb = pickup.GetComponent<Rigidbody>();
            if (rb == null)
                rb = pickup.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.drag = 2f;
            rb.angularDrag = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.constraints = RigidbodyConstraints.None;

            // Very gentle push towards player
            Vector3 pushDir = toPlayer + new Vector3(
                Random.Range(-0.2f, 0.2f),
                0.1f, // Tiny upward
                Random.Range(-0.2f, 0.2f)
            );
            rb.velocity = pushDir.normalized * Random.Range(0.5f, 1.0f); // Very gentle

            // Add NodePickup component for E interaction
            var existingPickup = pickup.GetComponent<NodePickup>();
            if (existingPickup == null)
            {
                var pickupComp = pickup.AddComponent<NodePickup>();
                pickupComp.Initialize(tier);
            }
            else
            {
                existingPickup.Initialize(tier);
            }

            // Add larger trigger collider for easier interaction detection (doesn't affect physics)
            GameObject interactionZone = new GameObject("InteractionZone");
            interactionZone.transform.SetParent(pickup.transform);
            interactionZone.transform.localPosition = Vector3.zero;
            var interactionCollider = interactionZone.AddComponent<SphereCollider>();
            interactionCollider.radius = 0.5f; // Good balance for interaction
            interactionCollider.isTrigger = true; // Trigger doesn't affect physics

            // Add script to forward interaction to parent
            interactionZone.AddComponent<InteractionForwarder>();

        }

        // Editor visualization
        private void OnDrawGizmos()
        {
            Color gizmoColor = tier switch
            {
                1 => new Color(0.72f, 0.45f, 0.20f, 0.5f),
                2 => new Color(0.75f, 0.75f, 0.78f, 0.5f),
                3 => new Color(1.0f, 0.84f, 0.0f, 0.5f),
                4 => new Color(0.58f, 0.44f, 0.86f, 0.5f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };

            if (isCollected)
                gizmoColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            else if (isRevealed)
                gizmoColor.a = 0.8f;

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, revealDistance * size * 0.5f);

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawWireSphere(transform.position, revealDistance * size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, revealDistance * size);
        }

        // ===== IInteractable Implementation =====

        /// <summary>
        /// Can only interact when revealed (visible) and not yet collected.
        /// </summary>
        public bool CanInteract => isRevealed && !isCollected;

        /// <summary>
        /// Get resource name based on tier.
        /// </summary>
        private string GetResourceName()
        {
            return tier switch
            {
                1 => "Coal",
                2 => "Iron Ore",
                3 => "Silver",
                4 => "Gold",
                _ => "Resource"
            };
        }

        /// <summary>
        /// Show resource name when aiming at revealed node.
        /// </summary>
        public string GetInteractionText()
        {
            return $"{GetResourceName()} (dig more to collect)";
        }

        /// <summary>
        /// Interaction doesn't do anything - player must dig to collect.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            // No direct interaction - must dig to expose and crumble
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
    }
}
