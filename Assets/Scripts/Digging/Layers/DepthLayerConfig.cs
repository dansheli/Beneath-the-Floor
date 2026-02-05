using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Configuration for a single underground depth layer.
    /// </summary>
    [System.Serializable]
    public class DepthLayerConfig
    {
        [Tooltip("Display name for this layer.")]
        public string layerName = "Unknown Layer";

        [Tooltip("Starting depth in meters below basement floor.")]
        public float startDepth = 0f;

        [Tooltip("Ending depth in meters below basement floor.")]
        public float endDepth = 20f;

        [Header("Loot Configuration")]
        [Tooltip("Resources that can spawn in this layer with their weights and amount ranges.")]
        public List<ResourceChance> resources = new List<ResourceChance>();

        [Tooltip("Relative weight for getting NO resource at all on a dig in this layer. " +
                 "Set to 0 for guaranteed loot, higher values = more empty digs.")]
        [Min(0f)]
        public float emptyWeight = 0f;

        [Header("Visual")]
        [Tooltip("Color tint for this layer's terrain (for visual variety).")]
        public Color terrainColor = new Color(0.5f, 0.4f, 0.3f);

        /// <summary>
        /// Check if a depth falls within this layer.
        /// </summary>
        public bool ContainsDepth(float depth)
        {
            return depth >= startDepth && depth < endDepth;
        }

        /// <summary>
        /// Get the total weight of all resources plus empty weight.
        /// </summary>
        public float GetTotalWeight()
        {
            float total = emptyWeight;
            foreach (var entry in resources)
            {
                if (entry.weight > 0f)
                    total += entry.weight;
            }
            return total;
        }
    }

    /// <summary>
    /// A resource type with its spawn weight and amount range.
    /// </summary>
    [System.Serializable]
    public class ResourceChance
    {
        [Tooltip("The resource type.")]
        public UndergroundResourceType resourceType;

        [Tooltip("Relative weight for random selection (higher = more common).")]
        [Min(0f)]
        public float weight = 1f;

        [Tooltip("Minimum and maximum amount dropped per dig (inclusive).")]
        public Vector2Int amountRange = new Vector2Int(1, 1);

        public ResourceChance() { }

        public ResourceChance(UndergroundResourceType type, float w)
        {
            resourceType = type;
            weight = w;
            amountRange = new Vector2Int(1, 1);
        }

        public ResourceChance(UndergroundResourceType type, float w, int minAmount, int maxAmount)
        {
            resourceType = type;
            weight = w;
            amountRange = new Vector2Int(minAmount, maxAmount);
        }

        /// <summary>
        /// Roll a random amount within the configured range.
        /// </summary>
        public int RollAmount()
        {
            int min = Mathf.Max(1, amountRange.x);
            int max = Mathf.Max(min, amountRange.y);
            return Random.Range(min, max + 1);
        }
    }
}
