using UnityEngine;
using System;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Data model for a hidden node embedded in terrain.
    /// </summary>
    [Serializable]
    public class HiddenNode
    {
        /// <summary>
        /// Unique ID within the chunk.
        /// </summary>
        public int NodeId;

        /// <summary>
        /// World position of the node center.
        /// </summary>
        public Vector3 WorldPosition;

        /// <summary>
        /// Radius of the node in meters.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Node tier (1-4). Higher tier = more valuable.
        /// </summary>
        public int Tier;

        /// <summary>
        /// Index into ResourceSystemConfig.resources array.
        /// -1 means no specific resource (legacy behavior).
        /// </summary>
        public int ResourceIndex = -1;

        /// <summary>
        /// Resource ID string (e.g., "stone", "iron", "copper", "coal").
        /// Used for display and save/load.
        /// </summary>
        public string ResourceId;

        /// <summary>
        /// Type key for resource mapping (legacy).
        /// </summary>
        public string TypeKey;

        /// <summary>
        /// Whether this node has been broken/collected.
        /// </summary>
        public bool IsBroken;

        /// <summary>
        /// Current exposure level (0-1). Computed dynamically.
        /// </summary>
        [NonSerialized]
        public float CurrentExposure;

        /// <summary>
        /// Chunk coordinate this node belongs to.
        /// </summary>
        public Vector3Int ChunkCoord;

        /// <summary>
        /// Reference to the visual GameObject (runtime only).
        /// </summary>
        [NonSerialized]
        public GameObject VisualObject;

        /// <summary>
        /// Whether the visual has been spawned.
        /// </summary>
        [NonSerialized]
        public bool HasVisual;

        /// <summary>
        /// Number of times this node has been hit after being revealed.
        /// </summary>
        [NonSerialized]
        public int HitCount;

        /// <summary>
        /// Whether this is a bonus node (spawned in top layer for early-game).
        /// </summary>
        public bool IsBonusNode;

        /// <summary>
        /// Previous exposure (for detecting changes).
        /// </summary>
        [NonSerialized]
        public float PreviousExposure;

        /// <summary>
        /// Direction the node was revealed from (toward the dig).
        /// Used to position visual so it emerges from terrain wall.
        /// </summary>
        [NonSerialized]
        public Vector3 RevealDirection;

        /// <summary>
        /// Time when the node visual was spawned (for grace period).
        /// </summary>
        [NonSerialized]
        public float RevealTime;

        public HiddenNode() { }

        public HiddenNode(int nodeId, Vector3 worldPosition, float radius, int tier, Vector3Int chunkCoord)
        {
            NodeId = nodeId;
            WorldPosition = worldPosition;
            Radius = radius;
            Tier = tier;
            ResourceIndex = -1;
            ResourceId = null;
            TypeKey = $"tier_{tier}";
            IsBroken = false;
            CurrentExposure = 0f;
            ChunkCoord = chunkCoord;
        }

        /// <summary>
        /// Create a node with specific resource type.
        /// </summary>
        public HiddenNode(int nodeId, Vector3 worldPosition, float radius, int tier, Vector3Int chunkCoord, int resourceIndex, string resourceId)
        {
            NodeId = nodeId;
            WorldPosition = worldPosition;
            Radius = radius;
            Tier = tier;
            ResourceIndex = resourceIndex;
            ResourceId = resourceId;
            TypeKey = !string.IsNullOrEmpty(resourceId) ? resourceId : $"tier_{tier}";
            IsBroken = false;
            CurrentExposure = 0f;
            ChunkCoord = chunkCoord;
        }

        /// <summary>
        /// Get the bounds of this node in world space.
        /// </summary>
        public Bounds GetWorldBounds()
        {
            return new Bounds(WorldPosition, Vector3.one * Radius * 2f);
        }

        /// <summary>
        /// Check if a world position is near this node.
        /// </summary>
        public bool IsNearPosition(Vector3 worldPos, float maxDistance)
        {
            return Vector3.Distance(worldPos, WorldPosition) <= maxDistance + Radius;
        }

        public override string ToString()
        {
            return $"Node({NodeId}, T{Tier}, exp={CurrentExposure:F2}, broken={IsBroken})";
        }
    }

    /// <summary>
    /// Save data for a single node (minimal - just ID, states, and resource info).
    /// </summary>
    [Serializable]
    public class NodeSaveData
    {
        public int NodeId;
        public bool IsBroken;
        public bool IsRevealed;  // True if the node was revealed (has visual)
        public int ResourceIndex;
        public string ResourceId;

        public NodeSaveData() { }

        public NodeSaveData(HiddenNode node)
        {
            NodeId = node.NodeId;
            IsBroken = node.IsBroken;
            IsRevealed = node.HasVisual;
            ResourceIndex = node.ResourceIndex;
            ResourceId = node.ResourceId;
        }
    }

    /// <summary>
    /// Save data for all nodes in a chunk.
    /// </summary>
    [Serializable]
    public class ChunkNodesSaveData
    {
        public int ChunkX;
        public int ChunkY;
        public int ChunkZ;
        public NodeSaveData[] BrokenNodes;

        public ChunkNodesSaveData() { }

        public ChunkNodesSaveData(Vector3Int chunkCoord, NodeSaveData[] brokenNodes)
        {
            ChunkX = chunkCoord.x;
            ChunkY = chunkCoord.y;
            ChunkZ = chunkCoord.z;
            BrokenNodes = brokenNodes;
        }

        public Vector3Int GetChunkCoord()
        {
            return new Vector3Int(ChunkX, ChunkY, ChunkZ);
        }
    }
}
