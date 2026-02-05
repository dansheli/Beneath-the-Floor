using UnityEngine;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// Cable visual using Verlet rope physics.
    /// Uses full available cable length so the rope drapes naturally.
    /// Ground collision via simple downward raycasts — no complex collision
    /// that would cause flickering or snagging.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class WinchCable : MonoBehaviour
    {
        [Header("Rope Simulation")]
        [Tooltip("Total number of rope nodes.")]
        [Range(10, 60)]
        [SerializeField] private int nodeCount = 40;

        [Tooltip("Solver iterations per frame (more = stiffer).")]
        [Range(4, 30)]
        [SerializeField] private int solverIterations = 20;

        [Tooltip("Gravity strength.")]
        [SerializeField] private float gravity = 18f;

        [Tooltip("Velocity damping per frame (higher = less oscillation).")]
        [Range(0.01f, 0.3f)]
        [SerializeField] private float damping = 0.15f;

        [Header("Ground Collision")]
        [Tooltip("Layers the cable collides with (ground/terrain).")]
        [SerializeField] private LayerMask collisionLayers = -1;

        [Tooltip("How far above the surface to keep nodes.")]
        [SerializeField] private float groundOffset = 0.08f;

        [Tooltip("How far above each node to start the downward raycast.")]
        [SerializeField] private float raycastLiftHeight = 3f;

        [Tooltip("Max raycast distance downward.")]
        [SerializeField] private float raycastMaxDist = 6f;

        [Tooltip("Collider name prefixes to ignore for ground checks.")]
        [SerializeField] private string[] ignoreNamePrefixes = { "Ceiling" };

        [Header("Visual")]
        [Tooltip("Cable width.")]
        [SerializeField] private float cableWidth = 0.035f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        [SerializeField] private Color tautColor = new Color(0.35f, 0.06f, 0.04f, 1f);

        // Verlet state
        private Vector3[] pos;
        private Vector3[] prev;
        private float segmentLength;
        private bool simActive;
        private float pathLength;

        // Cached
        private LineRenderer lr;
        private WinchAnchor anchor;

        // Reusable
        private RaycastHit[] rayHits = new RaycastHit[4];

        // ── PUBLIC API ────────────────────────────────────────

        public int PathCount => simActive ? nodeCount : 0;
        public float PathLength => pathLength;

        public Vector3 GetPoint(int i)
        {
            if (!simActive || pos == null || i < 0 || i >= nodeCount) return Vector3.zero;
            return pos[i];
        }

        public Vector3 GetDirectionAtPlayer()
        {
            if (!simActive || nodeCount < 2) return Vector3.up;
            Vector3 dir = pos[nodeCount - 1] - pos[nodeCount - 2];
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up;
        }

        public Vector3 GetLastSegmentOrigin()
        {
            if (!simActive || nodeCount < 2)
                return anchor != null ? anchor.AnchorPosition : transform.position;
            return pos[nodeCount - 2];
        }

        // ── LIFECYCLE ─────────────────────────────────────────

        private void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = cableWidth;
            lr.endWidth = cableWidth;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;

            if (lr.sharedMaterial == null)
                lr.material = new Material(Shader.Find("Sprites/Default"));
        }

        private void LateUpdate()
        {
            if (anchor == null)
                anchor = WinchAnchor.Instance ?? FindObjectOfType<WinchAnchor>();

            if (anchor == null || !anchor.IsAttached || anchor.AttachedPlayer == null)
            {
                if (lr.positionCount > 0) lr.positionCount = 0;
                simActive = false;
                pathLength = 0f;
                return;
            }

            Vector3 anchorPos = anchor.AnchorPosition;
            Vector3 playerPos = anchor.GetPlayerVisualAttachPoint();

            if (!simActive || pos == null || pos.Length != nodeCount)
                InitSim(anchorPos, playerPos);

            float straightDist = Vector3.Distance(anchorPos, playerPos);
            float ropeLen = Mathf.Min(straightDist * (1f + 0.06f), anchor.EffectiveCableLength);
            segmentLength = ropeLen / (nodeCount - 1);

            Simulate(anchorPos, playerPos);
            Render();
        }

        // ── SIMULATION ────────────────────────────────────────

        private void InitSim(Vector3 a, Vector3 b)
        {
            pos = new Vector3[nodeCount];
            prev = new Vector3[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                float t = (float)i / (nodeCount - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * 0.5f;
                pos[i] = p;
                prev[i] = p;
            }
            simActive = true;
        }

        private void Simulate(Vector3 anchorPos, Vector3 playerPos)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.02f);
            float dt2 = dt * dt;

            // Pin endpoints
            pos[0] = anchorPos;
            prev[0] = anchorPos;
            pos[nodeCount - 1] = playerPos;
            prev[nodeCount - 1] = playerPos;

            // Verlet integration with gravity
            for (int i = 1; i < nodeCount - 1; i++)
            {
                Vector3 cur = pos[i];
                Vector3 velocity = (cur - prev[i]) * (1f - damping);
                prev[i] = cur;
                pos[i] = cur + velocity + Vector3.down * (gravity * dt2);
            }

            // Constraints + ground collision
            for (int iter = 0; iter < solverIterations; iter++)
            {
                // Distance constraints
                for (int i = 0; i < nodeCount - 1; i++)
                {
                    Vector3 delta = pos[i + 1] - pos[i];
                    float dist = delta.magnitude;
                    if (dist < 0.0001f) continue;

                    float error = dist - segmentLength;
                    Vector3 correction = (delta / dist) * error;

                    bool pinA = (i == 0);
                    bool pinB = (i + 1 == nodeCount - 1);

                    if (pinA && pinB) continue;
                    else if (pinA) pos[i + 1] -= correction;
                    else if (pinB) pos[i] += correction;
                    else
                    {
                        pos[i] += correction * 0.5f;
                        pos[i + 1] -= correction * 0.5f;
                    }
                }

                // Ground collision: keep nodes above nearest surface below them
                for (int i = 1; i < nodeCount - 1; i++)
                {
                    KeepAboveGround(i);
                }
            }

            // Measure path length
            pathLength = 0f;
            for (int i = 0; i < nodeCount - 1; i++)
                pathLength += Vector3.Distance(pos[i], pos[i + 1]);
        }

        /// <summary>
        /// Simple ground collision: raycast downward from above the node.
        /// If there's ground below, keep the node above it.
        /// This avoids all the complex collision that causes flickering.
        /// </summary>
        private void KeepAboveGround(int i)
        {
            Vector3 origin = pos[i] + Vector3.up * raycastLiftHeight;

            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, rayHits, raycastMaxDist, collisionLayers, QueryTriggerInteraction.Ignore);

            if (hitCount == 0) return;

            // Find the closest valid hit below the node
            float bestY = float.MinValue;
            bool found = false;

            for (int h = 0; h < hitCount; h++)
            {
                if (ShouldIgnoreCollider(rayHits[h].collider)) continue;

                float surfaceY = rayHits[h].point.y + groundOffset;

                // We want the highest surface that is at or below the node's current position + lift
                if (surfaceY > bestY)
                {
                    bestY = surfaceY;
                    found = true;
                }
            }

            if (found && pos[i].y < bestY)
            {
                pos[i].y = bestY;
                // Kill downward velocity — node rests on surface
                prev[i].y = pos[i].y;
            }
        }

        private bool ShouldIgnoreCollider(Collider col)
        {
            if (ignoreNamePrefixes == null || ignoreNamePrefixes.Length == 0) return false;
            string name = col.gameObject.name;
            for (int p = 0; p < ignoreNamePrefixes.Length; p++)
            {
                if (name.StartsWith(ignoreNamePrefixes[p])) return true;
            }
            return false;
        }

        // ── RENDER ────────────────────────────────────────────

        private void Render()
        {
            float cableLen = anchor.EffectiveCableLength;
            float straightDist = Vector3.Distance(pos[0], pos[nodeCount - 1]);
            float tensionRatio = cableLen > 0.1f ? straightDist / cableLen : 0f;
            float colorT = Mathf.Clamp01((tensionRatio - 0.9f) / 0.1f);
            Color col = Color.Lerp(normalColor, tautColor, colorT);
            lr.startColor = col;
            lr.endColor = col;

            lr.positionCount = nodeCount;
            for (int i = 0; i < nodeCount; i++)
                lr.SetPosition(i, pos[i]);
        }
    }
}
