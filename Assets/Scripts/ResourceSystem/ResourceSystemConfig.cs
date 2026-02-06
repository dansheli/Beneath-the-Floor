using UnityEngine;
using System;
using System.Collections.Generic;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Configuration for a single resource tier (Small, Medium, Large, Huge).
    /// </summary>
    [Serializable]
    public class ResourceTierConfig
    {
        [Tooltip("Display name for this tier (e.g., Small, Medium, Large, Huge)")]
        public string tierName = "Small";

        [Tooltip("Visual prefab for this tier's node (in the wall)")]
        public GameObject nodePrefab;

        [Tooltip("Scale multiplier for the node prefab")]
        [Range(0.1f, 100f)]
        public float nodePrefabScale = 1.0f;

        [Tooltip("Pickup prefab for this tier (dropped items)")]
        public GameObject pickupPrefab;

        [Tooltip("Scale multiplier for the pickup prefab")]
        [Range(0.1f, 100f)]
        public float pickupPrefabScale = 1.0f;

        [Tooltip("How many items drop when the node crumbles")]
        public int yieldAmount = 3;

        [Tooltip("Radius of the node in the terrain")]
        [Range(0.3f, 5f)]
        public float nodeRadius = 0.4f;

        [Tooltip("Radius for exposure calculation")]
        [Range(0.2f, 3f)]
        public float exposureRadius = 0.3f;

        [Tooltip("How much must be exposed before crumbling (0-1)")]
        [Range(0.5f, 1.0f)]
        public float crumbleThreshold = 0.7f;
    }

    /// <summary>
    /// Definition for a single resource type (Stone, Iron, Copper, Coal, etc.).
    /// </summary>
    [Serializable]
    public class ResourceDefinition
    {
        [Tooltip("Unique ID for this resource (e.g., 'stone', 'iron')")]
        public string resourceId = "resource";

        [Tooltip("Display name shown to player")]
        public string displayName = "Resource";

        [Tooltip("Icon for UI")]
        public Sprite icon;

        [Tooltip("Color for node visuals when no prefab is set")]
        public Color resourceColor = Color.gray;

        [Tooltip("Credit value per unit when selling")]
        public int creditValuePerUnit = 1;

        [Tooltip("Minimum depth where this resource can spawn")]
        public float minDepth = 0f;

        [Tooltip("Maximum depth where this resource can spawn")]
        public float maxDepth = 100f;

        [Tooltip("4 tier configurations (Small, Medium, Large, Huge)")]
        public ResourceTierConfig[] tiers = new ResourceTierConfig[4]
        {
            new ResourceTierConfig { tierName = "Small", yieldAmount = 3, nodeRadius = 0.4f, exposureRadius = 0.3f, crumbleThreshold = 0.7f },
            new ResourceTierConfig { tierName = "Medium", yieldAmount = 6, nodeRadius = 0.6f, exposureRadius = 0.4f, crumbleThreshold = 0.75f },
            new ResourceTierConfig { tierName = "Large", yieldAmount = 10, nodeRadius = 0.8f, exposureRadius = 0.5f, crumbleThreshold = 0.8f },
            new ResourceTierConfig { tierName = "Huge", yieldAmount = 15, nodeRadius = 1.0f, exposureRadius = 0.6f, crumbleThreshold = 0.85f }
        };

        [Tooltip("Enable debug logging for this resource")]
        public bool debugLogging = false;

        /// <summary>
        /// Get tier config (tier is 1-indexed).
        /// </summary>
        public ResourceTierConfig GetTierConfig(int tier)
        {
            int index = Mathf.Clamp(tier - 1, 0, tiers.Length - 1);
            return tiers[index];
        }
    }

    /// <summary>
    /// Spawn weight for a resource at a specific depth layer.
    /// </summary>
    [Serializable]
    public class ResourceSpawnWeight
    {
        [Tooltip("Reference to resource definition (by index in resources array)")]
        public int resourceIndex;

        [Tooltip("Spawn weight (higher = more common)")]
        [Range(0f, 100f)]
        public float spawnWeight = 25f;

        [Tooltip("Whether this resource is enabled for this layer")]
        public bool enabled = true;

        [Tooltip("Max tier allowed for this resource in this layer (1-4, 0 = use global)")]
        [Range(0, 4)]
        public int maxTierOverride = 0;
    }

    /// <summary>
    /// Configuration for a depth layer's resource spawning.
    /// </summary>
    [Serializable]
    public class DepthLayerConfig
    {
        [Tooltip("Name for this layer (e.g., 'Surface (0-20m)')")]
        public string layerName = "Layer";

        [Tooltip("Minimum depth for this layer")]
        public float minDepth = 0f;

        [Tooltip("Maximum depth for this layer")]
        public float maxDepth = 20f;

        [Tooltip("Maximum tier allowed in this layer (1-4)")]
        [Range(1, 4)]
        public int maxTierAllowed = 4;

        [Tooltip("Resources available in this layer with their spawn weights")]
        public List<ResourceSpawnWeight> availableResources = new List<ResourceSpawnWeight>();

        [Tooltip("Override nodes per chunk for this layer (0 = use global)")]
        public int nodesPerChunkOverride = 0;
    }

    /// <summary>
    /// Configuration ScriptableObject for the new resource system.
    /// Contains all tunable values for Dust Economy and Hidden Nodes.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSystemConfig", menuName = "BeneathTheFloor/Resource System Config")]
    public class ResourceSystemConfig : ScriptableObject
    {
        [Header("=== RESOURCE DEFINITIONS ===")]
        [Tooltip("All available resource types")]
        public List<ResourceDefinition> resources = new List<ResourceDefinition>();

        [Header("=== DEPTH LAYERS ===")]
        [Tooltip("Resource spawn configuration per depth layer")]
        public List<DepthLayerConfig> depthLayers = new List<DepthLayerConfig>();

        [Header("=== TIER BASE CHANCES ===")]
        [Tooltip("Base chance for Tier 1 (Small) - adjusted by depth")]
        [Range(0f, 100f)]
        public float tier1BaseChance = 50f;

        [Tooltip("Base chance for Tier 2 (Medium)")]
        [Range(0f, 100f)]
        public float tier2BaseChance = 30f;

        [Tooltip("Base chance for Tier 3 (Large)")]
        [Range(0f, 100f)]
        public float tier3BaseChance = 15f;

        [Tooltip("Base chance for Tier 4 (Huge)")]
        [Range(0f, 100f)]
        public float tier4BaseChance = 5f;

        [Header("=== DUST ECONOMY ===")]

        [Tooltip("Base dust gained per unit of removed mass (density).")]
        [Range(1f, 100f)]
        public float baseDustPerMass = 10f;

        [Tooltip("Credits gained per dust unit when selling.")]
        [Range(0.1f, 10f)]
        public float dustSellValue = 1f;

        [Tooltip("Depth multipliers for dust. Index = layer (0-5), Value = multiplier.")]
        public float[] depthDustMultipliers = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 4f };

        [Header("=== HIDDEN NODES ===")]

        [Tooltip("Average number of nodes per chunk. LOW = rare WOW moments. HIGH = common. Recommended: 6-10.")]
        [Range(0, 100)]
        public int nodesPerChunk = 8;

        [Tooltip("Minimum node radius in meters. Larger = more digging to reach 80%.")]
        [Range(0.1f, 3f)]
        public float minNodeRadius = 0.7f;

        [Tooltip("Maximum node radius in meters. Larger = more digging to reach 80%.")]
        [Range(0.1f, 5f)]
        public float maxNodeRadius = 0.85f;

        [Tooltip("Number of sample points for exposure calculation.")]
        [Range(6, 48)]
        public int nodeRevealSampleCount = 12;

        [Tooltip("Exposure threshold for node to crumble into pickups (0.0-1.0). 0.80 = 80% exposed.")]
        [Range(0.1f, 1f)]
        public float breakExposureThreshold = 0.80f;

        [Tooltip("Minimum depth below basement floor for nodes to spawn. Set to 1-2m so nodes start in the dig area.")]
        [Range(0f, 20f)]
        public float minNodeDepth = 2f;

        [Header("=== TIER DEPTH REQUIREMENTS ===")]
        [Tooltip("Minimum depths per tier. Tier 2+ only spawn below these depths.")]
        public float[] tierMinDepths = new float[] { 2f, 10f, 20f, 35f }; // T1=2m, T2=10m, T3=20m, T4=35m

        [Tooltip("World seed for procedural generation. 0 = random.")]
        public int worldSeed = 0;

        [Header("=== NODE PLACEMENT VALIDATION ===")]

        [Tooltip("Maximum placement attempts per node before skipping.")]
        [Range(10, 100)]
        public int nodePlacementAttempts = 40;

        [Tooltip("Minimum distance from chunk/world edges in meters.")]
        [Range(0f, 2f)]
        public float nodeEdgeMargin = 0.5f;

        [Tooltip("Minimum distance from player spawn / dig start area in meters.")]
        [Range(0f, 5f)]
        public float nodeSpawnMargin = 2f;

        [Tooltip("Minimum distance between nodes. Larger = more spread out. Recommended: 4-6m.")]
        [Range(0.5f, 10f)]
        public float minNodeDistance = 5f;

        [Header("=== VISUAL SIZES (From FixedResourceNode) ===")]

        [Tooltip("Scale of revealed node visual. From FixedResourceNode: 5x5x5.")]
        [Range(1f, 10f)]
        public float nodeVisualScale = 5f;

        [Tooltip("Scale of pickup pieces. From FixedResourceNode: 1.5x1.5x1.5.")]
        [Range(0.5f, 3f)]
        public float pickupScale = 1.5f;

        [Header("=== NODE TIERS ===")]

        [Tooltip("Drop counts per tier. Index = tier-1, Value = drop count.")]
        public int[] tierDropCounts = new int[] { 3, 5, 8, 12 };

        [Tooltip("Tier weights by depth layer. Each array is for one layer, values are weights for tiers 1-4.")]
        public TierWeightsByLayer[] tierWeightsByDepth = new TierWeightsByLayer[]
        {
            new TierWeightsByLayer { weights = new float[] { 0.8f, 0.15f, 0.05f, 0f } },    // Layer 0: mostly T1
            new TierWeightsByLayer { weights = new float[] { 0.6f, 0.3f, 0.08f, 0.02f } },  // Layer 1
            new TierWeightsByLayer { weights = new float[] { 0.4f, 0.4f, 0.15f, 0.05f } },  // Layer 2
            new TierWeightsByLayer { weights = new float[] { 0.2f, 0.4f, 0.3f, 0.1f } },    // Layer 3
            new TierWeightsByLayer { weights = new float[] { 0.1f, 0.3f, 0.4f, 0.2f } },    // Layer 4
            new TierWeightsByLayer { weights = new float[] { 0.05f, 0.2f, 0.4f, 0.35f } },  // Layer 5: mostly T3-T4
        };

        [Header("=== TIER 1 PREFABS (Common - e.g. Copper) ===")]
        [Tooltip("Visual prefab for Tier 1 nodes (embedded in wall, NOT collectible)")]
        public GameObject tier1VisualPrefab;
        [Tooltip("Pickup prefab for Tier 1 (small collectible piece)")]
        public GameObject tier1PickupPrefab;

        [Header("=== TIER 2 PREFABS (Uncommon - e.g. Silver) ===")]
        [Tooltip("Visual prefab for Tier 2 nodes (embedded in wall, NOT collectible)")]
        public GameObject tier2VisualPrefab;
        [Tooltip("Pickup prefab for Tier 2 (small collectible piece)")]
        public GameObject tier2PickupPrefab;

        [Header("=== TIER 3 PREFABS (Rare - e.g. Gold) ===")]
        [Tooltip("Visual prefab for Tier 3 nodes (embedded in wall, NOT collectible)")]
        public GameObject tier3VisualPrefab;
        [Tooltip("Pickup prefab for Tier 3 (small collectible piece)")]
        public GameObject tier3PickupPrefab;

        [Header("=== TIER 4 PREFABS (Legendary - e.g. Adamantine) ===")]
        [Tooltip("Visual prefab for Tier 4 nodes (embedded in wall, NOT collectible)")]
        public GameObject tier4VisualPrefab;
        [Tooltip("Pickup prefab for Tier 4 (small collectible piece)")]
        public GameObject tier4PickupPrefab;

        [Header("=== PREFAB SCALE SETTINGS ===")]
        [Tooltip("Scale multiplier for node visuals. Set to 1.0 if prefabs are already properly sized.")]
        [Range(0.1f, 5f)]
        public float nodeVisualScaleMultiplier = 1.0f;

        [Tooltip("Scale multiplier for pickup pieces. Keeps them small and collectible.")]
        [Range(0.05f, 1f)]
        public float pickupPieceScaleMultiplier = 0.15f;

        [Header("=== LEGACY (for backwards compatibility) ===")]
        public GameObject[] nodeVisualPrefabs;
        public GameObject[] pickupPiecePrefabs;

        /// <summary>
        /// Get visual prefab for a specific tier (1-4).
        /// Returns null if prefab is not assigned.
        /// </summary>
        public GameObject GetVisualPrefabForTier(int tier)
        {
            GameObject prefab = tier switch
            {
                1 => tier1VisualPrefab,
                2 => tier2VisualPrefab,
                3 => tier3VisualPrefab,
                4 => tier4VisualPrefab,
                _ => tier1VisualPrefab
            };
            // Unity's fake null check - returns true null if not assigned
            return prefab != null ? prefab : null;
        }

        /// <summary>
        /// Get pickup prefab for a specific tier (1-4).
        /// Returns null if prefab is not assigned.
        /// </summary>
        public GameObject GetPickupPrefabForTier(int tier)
        {
            GameObject prefab = tier switch
            {
                1 => tier1PickupPrefab,
                2 => tier2PickupPrefab,
                3 => tier3PickupPrefab,
                4 => tier4PickupPrefab,
                _ => tier1PickupPrefab
            };
            // Unity's fake null check - returns true null if not assigned
            return prefab != null ? prefab : null;
        }

        /// <summary>
        /// Get depth multiplier for a given depth value.
        /// </summary>
        public float GetDepthMultiplier(float depth)
        {
            int layerIndex = GetLayerIndex(depth);
            if (layerIndex >= 0 && layerIndex < depthDustMultipliers.Length)
            {
                return depthDustMultipliers[layerIndex];
            }
            return depthDustMultipliers.Length > 0 ? depthDustMultipliers[depthDustMultipliers.Length - 1] : 1f;
        }

        /// <summary>
        /// Get layer index for a given depth.
        /// Uses same depth ranges as UndergroundResourceTable.
        /// </summary>
        public int GetLayerIndex(float depth)
        {
            // Layer boundaries matching UndergroundResourceTable
            if (depth < 15f) return 0;
            if (depth < 35f) return 1;
            if (depth < 65f) return 2;
            if (depth < 105f) return 3;
            if (depth < 155f) return 4;
            return 5;
        }

        /// <summary>
        /// Get random tier for a given depth layer.
        /// </summary>
        public int GetRandomTier(int layerIndex)
        {
            if (tierWeightsByDepth == null || layerIndex < 0 || layerIndex >= tierWeightsByDepth.Length)
                return 1;

            var layerWeights = tierWeightsByDepth[layerIndex].weights;
            if (layerWeights == null || layerWeights.Length == 0)
                return 1;

            float totalWeight = 0f;
            for (int i = 0; i < layerWeights.Length; i++)
            {
                totalWeight += layerWeights[i];
            }

            // Roll weighted random
            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < layerWeights.Length; i++)
            {
                cumulative += layerWeights[i];
                if (roll <= cumulative)
                {
                    return i + 1; // Tier is 1-indexed
                }
            }

            return 1;
        }

        /// <summary>
        /// Get drop count for a tier.
        /// </summary>
        public int GetDropCountForTier(int tier)
        {
            int index = tier - 1;
            if (tierDropCounts != null && index >= 0 && index < tierDropCounts.Length)
            {
                return tierDropCounts[index];
            }
            return 3; // Default
        }

        /// <summary>
        /// Get minimum depth required for a tier.
        /// </summary>
        public float GetMinDepthForTier(int tier)
        {
            int index = tier - 1;
            if (tierMinDepths != null && index >= 0 && index < tierMinDepths.Length)
            {
                return tierMinDepths[index];
            }
            return 0f; // No minimum by default
        }

        /// <summary>
        /// Get the highest tier allowed at a given depth.
        /// </summary>
        public int GetMaxTierForDepth(float depth)
        {
            if (tierMinDepths == null || tierMinDepths.Length == 0)
                return 4;

            int maxTier = 1;
            for (int i = 0; i < tierMinDepths.Length; i++)
            {
                if (depth >= tierMinDepths[i])
                    maxTier = i + 1;
            }
            return maxTier;
        }

        /// <summary>
        /// Get actual world seed (generates random if 0).
        /// </summary>
        public int GetEffectiveSeed()
        {
            if (worldSeed == 0)
            {
                return UnityEngine.Random.Range(1, int.MaxValue);
            }
            return worldSeed;
        }

        /// <summary>
        /// Get a random node visual prefab (BIG, NOT collectible).
        /// Returns null if no prefabs configured.
        /// </summary>
        public GameObject GetRandomNodeVisualPrefab()
        {
            if (nodeVisualPrefabs == null || nodeVisualPrefabs.Length == 0)
                return null;
            return nodeVisualPrefabs[UnityEngine.Random.Range(0, nodeVisualPrefabs.Length)];
        }

        /// <summary>
        /// Get a random pickup piece prefab (SMALL, collectible).
        /// Returns null if no prefabs configured.
        /// </summary>
        public GameObject GetRandomPickupPiecePrefab()
        {
            if (pickupPiecePrefabs == null || pickupPiecePrefabs.Length == 0)
                return null;
            return pickupPiecePrefabs[UnityEngine.Random.Range(0, pickupPiecePrefabs.Length)];
        }

        #region New Resource System Methods

        /// <summary>
        /// Get resource definition by index.
        /// </summary>
        public ResourceDefinition GetResourceByIndex(int index)
        {
            if (resources == null || index < 0 || index >= resources.Count)
                return null;
            return resources[index];
        }

        /// <summary>
        /// Get resource definition by ID.
        /// </summary>
        public ResourceDefinition GetResourceById(string resourceId)
        {
            if (resources == null || string.IsNullOrEmpty(resourceId))
                return null;
            return resources.Find(r => string.Equals(r.resourceId, resourceId, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get icon for a resource by its ID (e.g., "stone", "iron", "coal", "copper").
        /// </summary>
        public Sprite GetIconByResourceId(string resourceId)
        {
            var resource = GetResourceById(resourceId);
            return resource?.icon;
        }

        /// <summary>
        /// Get icon for a ResourceType enum by mapping to resource ID.
        /// </summary>
        public Sprite GetIconByResourceType(ResourceType type)
        {
            string resourceId = MapResourceTypeToId(type);
            return GetIconByResourceId(resourceId);
        }

        /// <summary>
        /// Get credit value for a ResourceType enum.
        /// Returns -1 if not found (caller should use fallback).
        /// </summary>
        public int GetCreditValueByResourceType(ResourceType type)
        {
            string resourceId = MapResourceTypeToId(type);
            var resource = GetResourceById(resourceId);
            return resource?.creditValuePerUnit ?? -1;
        }

        /// <summary>
        /// Map ResourceType enum to resource ID string used in config.
        /// </summary>
        private string MapResourceTypeToId(ResourceType type)
        {
            return type switch
            {
                ResourceType.Stone => "stone",
                ResourceType.IronOre => "iron",
                ResourceType.Coal => "coal",
                ResourceType.Copper => "copper",
                ResourceType.Silver => "silver",
                ResourceType.Gold => "gold",
                ResourceType.Dirt => "dirt",
                ResourceType.Clay => "clay",
                _ => type.ToString().ToLower()
            };
        }

        /// <summary>
        /// Get depth layer config for a given depth.
        /// </summary>
        public DepthLayerConfig GetDepthLayerConfig(float depth)
        {
            if (depthLayers == null || depthLayers.Count == 0)
                return null;

            foreach (var layer in depthLayers)
            {
                if (depth >= layer.minDepth && depth < layer.maxDepth)
                    return layer;
            }

            // Return last layer if depth exceeds all
            return depthLayers[depthLayers.Count - 1];
        }

        /// <summary>
        /// Select a random resource for a given depth using spawn weights.
        /// Respects each resource's minDepth/maxDepth restrictions.
        /// </summary>
        public ResourceDefinition SelectRandomResourceForDepth(float depth, System.Random rng = null)
        {
            var layerConfig = GetDepthLayerConfig(depth);
            if (layerConfig == null || layerConfig.availableResources == null || layerConfig.availableResources.Count == 0)
                return null;

            // Calculate total weight (only for resources valid at this depth)
            float totalWeight = 0f;
            foreach (var resourceWeight in layerConfig.availableResources)
            {
                if (!resourceWeight.enabled)
                    continue;

                // Check resource's own depth limits
                var resource = GetResourceByIndex(resourceWeight.resourceIndex);
                if (resource == null)
                    continue;

                // Skip if depth is outside resource's allowed range
                if (depth < resource.minDepth || depth > resource.maxDepth)
                    continue;

                totalWeight += resourceWeight.spawnWeight;
            }

            if (totalWeight <= 0f)
                return null;

            // Roll weighted random
            float roll = rng != null ? (float)rng.NextDouble() * totalWeight : UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var resourceWeight in layerConfig.availableResources)
            {
                if (!resourceWeight.enabled)
                    continue;

                // Check resource's own depth limits
                var resource = GetResourceByIndex(resourceWeight.resourceIndex);
                if (resource == null)
                    continue;

                // Skip if depth is outside resource's allowed range
                if (depth < resource.minDepth || depth > resource.maxDepth)
                    continue;

                cumulative += resourceWeight.spawnWeight;
                if (roll <= cumulative)
                {
                    return resource;
                }
            }

            // Fallback: return first enabled resource valid at this depth
            foreach (var resourceWeight in layerConfig.availableResources)
            {
                if (!resourceWeight.enabled)
                    continue;

                var resource = GetResourceByIndex(resourceWeight.resourceIndex);
                if (resource != null && depth >= resource.minDepth && depth <= resource.maxDepth)
                    return resource;
            }

            return null;
        }

        /// <summary>
        /// Select a random tier based on base chances.
        /// </summary>
        /// <param name="maxTierAllowed">Maximum tier allowed (1-4)</param>
        /// <param name="rng">Optional seeded RNG</param>
        public int SelectRandomTier(int maxTierAllowed = 4, System.Random rng = null)
        {
            // Base chances
            float[] baseChances = new float[] { tier1BaseChance, tier2BaseChance, tier3BaseChance, tier4BaseChance };
            float totalWeight = 0f;

            for (int i = 0; i < 4; i++)
            {
                if (i + 1 <= maxTierAllowed)
                {
                    totalWeight += baseChances[i];
                }
            }

            if (totalWeight <= 0f)
                return 1;

            // Roll
            float roll = rng != null ? (float)rng.NextDouble() * totalWeight : UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < 4; i++)
            {
                if (i + 1 > maxTierAllowed)
                    continue;

                cumulative += baseChances[i];
                if (roll <= cumulative)
                    return i + 1;
            }

            return 1;
        }

        /// <summary>
        /// Get effective nodes per chunk for a depth layer.
        /// </summary>
        public int GetEffectiveNodesPerChunk(float depth)
        {
            var layerConfig = GetDepthLayerConfig(depth);
            return (layerConfig != null && layerConfig.nodesPerChunkOverride > 0)
                ? layerConfig.nodesPerChunkOverride
                : nodesPerChunk;
        }

        /// <summary>
        /// Check if a resource is available at a given depth.
        /// </summary>
        public bool IsResourceAvailableAtDepth(ResourceDefinition resource, float depth)
        {
            if (resource == null)
                return false;

            // Check resource's own depth limits
            if (depth < resource.minDepth || depth > resource.maxDepth)
                return false;

            // Check if resource is in the depth layer's available list
            var layerConfig = GetDepthLayerConfig(depth);
            if (layerConfig == null || layerConfig.availableResources == null)
                return false;

            int resourceIndex = resources.IndexOf(resource);
            foreach (var weight in layerConfig.availableResources)
            {
                if (weight.resourceIndex == resourceIndex && weight.enabled)
                    return true;
            }

            return false;
        }

        #endregion
    }

    [Serializable]
    public class TierWeightsByLayer
    {
        [Tooltip("Weights for tiers 1, 2, 3, 4")]
        public float[] weights = new float[] { 0.5f, 0.3f, 0.15f, 0.05f };
    }
}
