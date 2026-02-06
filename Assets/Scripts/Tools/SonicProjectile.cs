using UnityEngine;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Projectile fired by the Sonic Pulser weapon.
    /// Flies in a direction, raycasts for terrain collision each frame,
    /// and digs a hole on impact. Dig radius = ball visual radius.
    /// </summary>
    public class SonicProjectile : MonoBehaviour
    {
        private Vector3 moveDirection;
        private float speed;
        private float maxLifetime;
        private float digRadius;
        private float digStrength;
        private float elapsed;
        private bool hasHit;

        /// <summary>
        /// Initialize the projectile after spawning.
        /// </summary>
        /// <param name="direction">World-space direction to fly</param>
        /// <param name="speed">Movement speed in units/sec</param>
        /// <param name="lifetime">Max lifetime before auto-destroy</param>
        /// <param name="digRadius">Dig radius on impact (= visual ball radius)</param>
        /// <param name="digStrength">Dig strength on impact</param>
        public void Init(Vector3 direction, float speed, float lifetime, float digRadius, float digStrength)
        {
            moveDirection = direction.normalized;
            this.speed = speed;
            maxLifetime = lifetime;
            this.digRadius = digRadius;
            this.digStrength = digStrength;
        }

        private void Update()
        {
            if (hasHit) return;

            elapsed += Time.deltaTime;
            if (elapsed >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 oldPos = transform.position;
            Vector3 movement = moveDirection * speed * Time.deltaTime;
            transform.position += movement;

            // RaycastAll from old position - skip non-terrain objects (resources, etc.)
            float rayLength = movement.magnitude + digRadius;
            RaycastHit[] hits = Physics.RaycastAll(oldPos, moveDirection, rayLength);

            // Sort by distance and find the first terrain chunk hit
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsTerrainChunk(hits[i].collider))
                {
                    OnTerrainImpact(hits[i].point);
                    break;
                }
                // Skip non-terrain hits (resources, props, etc.)
            }
        }

        private void OnTerrainImpact(Vector3 hitPoint)
        {
            hasHit = true;

            // Dig at impact point - radius matches ball visual radius exactly
            if (DiggingSystem.Instance != null)
            {
                DiggingSystem.Instance.ExecuteDigCustom(hitPoint, digRadius, digStrength);

                // Flush colliders immediately so the hole appears instantly
                var chunkManager = ChunkManager.Instance;
                if (chunkManager != null)
                {
                    chunkManager.FlushDirtyChunksWithColliders();
                }
            }

            Destroy(gameObject);
        }

        private bool IsTerrainChunk(Collider collider)
        {
            if (collider == null) return false;
            string objName = collider.gameObject.name;
            if (objName.StartsWith("Chunk_")) return true;
            Transform parent = collider.transform.parent;
            if (parent != null && parent.name == "Chunks") return true;
            return false;
        }
    }
}
