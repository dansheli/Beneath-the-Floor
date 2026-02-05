using UnityEngine;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Marker component for node visuals (BIG embedded nodes).
    /// This is NOT a pickup - it's just a visual that gets destroyed when the node breaks.
    /// Used to distinguish node visuals from collectible pickups.
    /// </summary>
    public class NodeVisualMarker : MonoBehaviour
    {
        [Header("Node Info (Read Only)")]
        [SerializeField] private int nodeId;
        [SerializeField] private int tier;
        [SerializeField] private Vector3Int chunkCoord;

        /// <summary>
        /// Initialize the marker with node data.
        /// </summary>
        public void Initialize(int nodeId, int tier, Vector3Int chunkCoord)
        {
            this.nodeId = nodeId;
            this.tier = tier;
            this.chunkCoord = chunkCoord;

            // Ensure this is tagged/layered as non-collectible
            gameObject.tag = "Untagged"; // NOT "Pickup" or "Collectible"

            // Disable any colliders to prevent interaction
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }

        public int NodeId => nodeId;
        public int Tier => tier;
        public Vector3Int ChunkCoord => chunkCoord;
    }
}
