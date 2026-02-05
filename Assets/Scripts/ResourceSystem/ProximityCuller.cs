using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Disables rendering, lights, and colliders on distant NodeVisuals and ResourcePickups.
    /// Objects are never destroyed — their data stays intact. When the player returns,
    /// everything re-enables and looks exactly as it was.
    /// Runs checks in batches to spread CPU cost across frames.
    /// </summary>
    public class ProximityCuller : MonoBehaviour
    {
        public static ProximityCuller Instance { get; private set; }

        [Header("Culling Distances")]
        [Tooltip("NodeVisuals beyond this distance get culled (renderers off)")]
        [SerializeField] private float nodeCullDistance = 20f;

        [Tooltip("ResourcePickups beyond this distance get culled (renderers + colliders off)")]
        [SerializeField] private float pickupCullDistance = 25f;

        [Header("Performance")]
        [Tooltip("How many objects to check per frame (spread work over time)")]
        [SerializeField] private int checksPerFrame = 30;

        [Tooltip("How often to do a full scan for new objects (seconds)")]
        [SerializeField] private float fullScanInterval = 2f;

        // Tracked objects
        private readonly List<CulledObject> trackedObjects = new List<CulledObject>();
        private int currentCheckIndex = 0;
        private float fullScanTimer = 0f;
        private Transform playerTransform;

        private struct CulledObject
        {
            public GameObject gameObject;
            public Renderer[] renderers;
            public Collider[] colliders;
            public Light[] lights;
            public Rigidbody rigidbody;
            public MonoBehaviour[] tickScripts; // Scripts with Update that should pause
            public float cullDistanceSqr;
            public bool isCulled;
            public bool isPickup; // Pickups also disable colliders + rigidbody
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void Start()
        {
            // One-time fix: ensure all existing ResourcePickups have physics enabled
            // This fixes any resources that got stuck from old code
            FixAllPickupPhysics();
        }

        /// <summary>
        /// Ensure all ResourcePickups have physics enabled (not kinematic).
        /// Call this to fix resources that got frozen by old culling code.
        /// </summary>
        public void FixAllPickupPhysics()
        {
            var pickups = ResourcePickup.ActivePickups;
            for (int i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (pickup == null) continue;

                var rb = pickup.GetComponent<Rigidbody>();
                if (rb != null && rb.isKinematic)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.WakeUp();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private float physicsCheckTimer = 0f;

        private void LateUpdate()
        {
            if (playerTransform == null)
            {
                var cam = Camera.main;
                if (cam != null) playerTransform = cam.transform;
                else return;
            }

            // Process a batch of tracked objects each frame
            ProcessBatch();

            // Periodically scan for new untracked objects
            fullScanTimer += Time.deltaTime;
            if (fullScanTimer >= fullScanInterval)
            {
                fullScanTimer = 0f;
                ScanForNewObjects();
            }

            // Periodically ensure all pickups have physics active (every 2 seconds)
            physicsCheckTimer += Time.deltaTime;
            if (physicsCheckTimer >= 2f)
            {
                physicsCheckTimer = 0f;
                EnsurePickupPhysicsActive();
            }
        }

        /// <summary>
        /// Ensure all tracked pickups have active physics (not kinematic).
        /// Called periodically to catch any that got frozen.
        /// </summary>
        private void EnsurePickupPhysicsActive()
        {
            for (int i = 0; i < trackedObjects.Count; i++)
            {
                var obj = trackedObjects[i];
                if (obj.isPickup && obj.gameObject != null && obj.rigidbody != null)
                {
                    if (obj.rigidbody.isKinematic)
                    {
                        obj.rigidbody.isKinematic = false;
                        obj.rigidbody.useGravity = true;
                        obj.rigidbody.WakeUp();
                    }
                }
            }
        }

        private void ProcessBatch()
        {
            if (trackedObjects.Count == 0) return;

            Vector3 playerPos = playerTransform.position;
            int processed = 0;

            while (processed < checksPerFrame && trackedObjects.Count > 0)
            {
                if (currentCheckIndex >= trackedObjects.Count)
                    currentCheckIndex = 0;

                var obj = trackedObjects[currentCheckIndex];

                // Clean up destroyed objects
                if (obj.gameObject == null)
                {
                    trackedObjects.RemoveAt(currentCheckIndex);
                    continue;
                }

                float sqrDist = (obj.gameObject.transform.position - playerPos).sqrMagnitude;
                bool shouldCull = sqrDist > obj.cullDistanceSqr;

                if (shouldCull != obj.isCulled)
                {
                    obj.isCulled = shouldCull;
                    SetObjectCulled(ref obj, shouldCull);
                    trackedObjects[currentCheckIndex] = obj;
                }

                currentCheckIndex++;
                processed++;
            }
        }

        private void SetObjectCulled(ref CulledObject obj, bool culled)
        {
            // Renderers — always toggle
            if (obj.renderers != null)
            {
                for (int i = 0; i < obj.renderers.Length; i++)
                {
                    if (obj.renderers[i] != null)
                        obj.renderers[i].enabled = !culled;
                }
            }

            // Lights — always toggle
            if (obj.lights != null)
            {
                for (int i = 0; i < obj.lights.Length; i++)
                {
                    if (obj.lights[i] != null)
                        obj.lights[i].enabled = !culled;
                }
            }

            // Pickups: ensure physics stays active
            // When un-culling, force isKinematic=false to fix any resources that got frozen
            // When culling, do NOT disable physics - let them fall naturally
            if (obj.isPickup && !culled && obj.rigidbody != null)
            {
                obj.rigidbody.isKinematic = false;
                obj.rigidbody.useGravity = true;
            }
        }

        private void ScanForNewObjects()
        {
            // Scan NodeVisuals (via HiddenNodeManager)
            if (HiddenNodeManager.Instance != null)
            {
                var nodeVisuals = HiddenNodeManager.Instance.GetActiveNodeVisuals();
                if (nodeVisuals != null)
                {
                    foreach (var kvp in nodeVisuals)
                    {
                        if (kvp.Value != null && !IsTracked(kvp.Value))
                        {
                            TrackObject(kvp.Value, nodeCullDistance, false);
                        }
                    }
                }
            }

            // Scan ResourcePickups (via static registry)
            var activePickups = ResourcePickup.ActivePickups;
            for (int i = 0; i < activePickups.Count; i++)
            {
                var pickup = activePickups[i];
                if (pickup != null && pickup.gameObject != null && !IsTracked(pickup.gameObject))
                {
                    TrackObject(pickup.gameObject, pickupCullDistance, true);
                }
            }
        }

        private bool IsTracked(GameObject go)
        {
            for (int i = 0; i < trackedObjects.Count; i++)
            {
                if (trackedObjects[i].gameObject == go) return true;
            }
            return false;
        }

        /// <summary>
        /// Register an object for proximity culling.
        /// Called automatically during scans, or can be called manually when spawning.
        /// </summary>
        public void TrackObject(GameObject go, float cullDistance, bool isPickup)
        {
            if (go == null) return;

            var culled = new CulledObject
            {
                gameObject = go,
                renderers = go.GetComponentsInChildren<Renderer>(true),
                colliders = null, // No longer used - pickups keep colliders active
                lights = go.GetComponentsInChildren<Light>(true),
                rigidbody = isPickup ? go.GetComponent<Rigidbody>() : null, // Track for ensuring physics on un-cull
                cullDistanceSqr = cullDistance * cullDistance,
                isCulled = false,
                isPickup = isPickup
            };

            trackedObjects.Add(culled);
        }

        /// <summary>
        /// Immediately register a newly spawned node visual for culling.
        /// </summary>
        public void TrackNodeVisual(GameObject go)
        {
            if (go != null && !IsTracked(go))
            {
                TrackObject(go, nodeCullDistance, false);
            }
        }

        /// <summary>
        /// Immediately register a newly spawned resource pickup for culling.
        /// </summary>
        public void TrackPickup(GameObject go)
        {
            if (go != null && !IsTracked(go))
            {
                TrackObject(go, pickupCullDistance, true);
            }
        }

        /// <summary>
        /// Force un-cull all objects (e.g. when player teleports back).
        /// </summary>
        public void ForceUnCullAll()
        {
            for (int i = 0; i < trackedObjects.Count; i++)
            {
                var obj = trackedObjects[i];
                if (obj.isCulled && obj.gameObject != null)
                {
                    obj.isCulled = false;
                    SetObjectCulled(ref obj, false);
                    trackedObjects[i] = obj;
                }
            }
        }

        /// <summary>
        /// Debug: Show current tracking stats.
        /// </summary>
        public int TrackedCount => trackedObjects.Count;
        public int CulledCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < trackedObjects.Count; i++)
                    if (trackedObjects[i].isCulled) count++;
                return count;
            }
        }
    }
}
