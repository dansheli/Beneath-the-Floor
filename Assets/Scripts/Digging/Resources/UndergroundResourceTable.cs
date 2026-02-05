using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Manages the 6 underground depth layers and their resource distributions.
    /// </summary>
    [CreateAssetMenu(fileName = "UndergroundResourceTable", menuName = "Beneath The Floor/Digging/Resource Table")]
    public class UndergroundResourceTable : ScriptableObject
    {
        [Header("Depth Layers")]
        [Tooltip("Configure all 6 underground layers.")]
        public List<DepthLayerConfig> layers = new List<DepthLayerConfig>();

        [Header("Settings")]
        [Tooltip("Minimum ADJUSTED depth where underground layers begin. " +
                 "Set to 0 since adjusted depth already accounts for the soil start offset.")]
        public float undergroundStartDepth = 0f;

        [Tooltip("Enable debug logging for resource selection.")]
        public bool debugLogging = false;

        /// <summary>
        /// Get a random resource type for the given depth.
        /// </summary>
        /// <param name="depth">Depth in meters below basement floor.</param>
        /// <returns>Selected resource type, or None if no layer matches.</returns>
        public UndergroundResourceType GetResourceForDepth(float depth)
        {
            // Below underground start depth, no resources yet
            if (depth < undergroundStartDepth)
            {
                if (debugLogging)
                    Debug.Log($"[ResourceTable] Depth {depth:F1}m is above underground start ({undergroundStartDepth}m), returning None");
                return UndergroundResourceType.None;
            }

            // Find the matching layer
            DepthLayerConfig layer = GetLayerForDepth(depth);
            if (layer == null || layer.resources.Count == 0)
            {
                if (debugLogging)
                    Debug.LogWarning($"[ResourceTable] No layer found for depth {depth:F1}m");
                return UndergroundResourceType.None;
            }

            // Calculate total weight
            float totalWeight = 0f;
            foreach (var rc in layer.resources)
            {
                totalWeight += rc.weight;
            }

            if (totalWeight <= 0f)
            {
                return UndergroundResourceType.None;
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < layer.resources.Count; i++)
            {
                cumulative += layer.resources[i].weight;
                if (roll <= cumulative)
                {
                    if (debugLogging)
                        Debug.Log($"[ResourceTable] Depth {depth:F1}m (Layer: {layer.layerName}) -> {layer.resources[i].resourceType}");
                    return layer.resources[i].resourceType;
                }
            }

            // Fallback (shouldn't reach here)
            return layer.resources[layer.resources.Count - 1].resourceType;
        }

        /// <summary>
        /// Get the layer configuration for a given depth.
        /// </summary>
        public DepthLayerConfig GetLayerForDepth(float depth)
        {
            foreach (var layer in layers)
            {
                if (layer.ContainsDepth(depth))
                    return layer;
            }
            return null;
        }

        /// <summary>
        /// Get the layer index (1-6) for a given depth.
        /// </summary>
        public int GetLayerIndex(float depth)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].ContainsDepth(depth))
                    return i + 1; // 1-based index
            }
            return 0;
        }

        /// <summary>
        /// Initialize with default 6-layer configuration.
        /// Call this from editor to set up initial data.
        /// </summary>
        [ContextMenu("Initialize Default Layers")]
        public void InitializeDefaultLayers()
        {
            layers.Clear();

            // NOTE: All depth values are ADJUSTED depth (rawDepth - soilStartOffset).
            // Adjusted depth 0 = raw depth 5m (when player first reaches soil zone).

            // Layer 1 - Soft Depth (0-15m adjusted = 5-20m raw)
            // Easiest layer with highest empty chance and common resources
            layers.Add(new DepthLayerConfig
            {
                layerName = "Soft Depth",
                startDepth = 0f,
                endDepth = 15f,
                emptyWeight = 25f, // 25% chance of no loot
                terrainColor = new Color(0.55f, 0.45f, 0.35f),
                resources = new List<ResourceChance>
                {
                    new ResourceChance(UndergroundResourceType.Dirt, 35f, 1, 3),
                    new ResourceChance(UndergroundResourceType.SoftStone, 20f, 1, 2),
                    new ResourceChance(UndergroundResourceType.Clay, 15f, 1, 2),
                    new ResourceChance(UndergroundResourceType.IronNugget, 4f, 1, 1),
                    new ResourceChance(UndergroundResourceType.CopperFragment, 1f, 1, 1)
                }
            });

            // Layer 2 - Hard Soil (15-35m adjusted = 20-40m raw)
            // Less empty, better resources start appearing
            layers.Add(new DepthLayerConfig
            {
                layerName = "Hard Soil",
                startDepth = 15f,
                endDepth = 35f,
                emptyWeight = 20f, // 20% chance of no loot
                terrainColor = new Color(0.45f, 0.38f, 0.30f),
                resources = new List<ResourceChance>
                {
                    new ResourceChance(UndergroundResourceType.HardSoil, 25f, 1, 2),
                    new ResourceChance(UndergroundResourceType.Stone, 20f, 1, 2),
                    new ResourceChance(UndergroundResourceType.Sandstone, 12f, 1, 2),
                    new ResourceChance(UndergroundResourceType.IronChunk, 10f, 1, 1),
                    new ResourceChance(UndergroundResourceType.CopperPiece, 8f, 1, 1),
                    new ResourceChance(UndergroundResourceType.Coal, 5f, 1, 2)
                }
            });

            // Layer 3 - Heat & Pressure (35-65m adjusted = 40-70m raw)
            // Lower empty chance, more valuable resources
            layers.Add(new DepthLayerConfig
            {
                layerName = "Heat & Pressure",
                startDepth = 35f,
                endDepth = 65f,
                emptyWeight = 15f, // 15% chance of no loot
                terrainColor = new Color(0.50f, 0.35f, 0.28f),
                resources = new List<ResourceChance>
                {
                    new ResourceChance(UndergroundResourceType.HardSoilDeep, 20f, 1, 2),
                    new ResourceChance(UndergroundResourceType.HardStone, 20f, 1, 2),
                    new ResourceChance(UndergroundResourceType.Heatstone, 12f, 1, 1),
                    new ResourceChance(UndergroundResourceType.Quartz, 12f, 1, 2),
                    new ResourceChance(UndergroundResourceType.CrystalDust, 10f, 1, 1),
                    new ResourceChance(UndergroundResourceType.AncientOre, 6f, 1, 1)
                }
            });

            // Layer 4 - Crystal Roots (65-105m adjusted = 70-110m raw)
            // Rare resources, even lower empty chance
            layers.Add(new DepthLayerConfig
            {
                layerName = "Crystal Roots",
                startDepth = 65f,
                endDepth = 105f,
                emptyWeight = 10f, // 10% chance of no loot
                terrainColor = new Color(0.40f, 0.35f, 0.50f),
                resources = new List<ResourceChance>
                {
                    new ResourceChance(UndergroundResourceType.CrystalShard, 22f, 1, 2),
                    new ResourceChance(UndergroundResourceType.PurpleQuartz, 18f, 1, 1),
                    new ResourceChance(UndergroundResourceType.CrystalStone, 18f, 1, 2),
                    new ResourceChance(UndergroundResourceType.DeepCrystalVein, 12f, 1, 1),
                    new ResourceChance(UndergroundResourceType.AncientOre, 10f, 1, 1),
                    new ResourceChance(UndergroundResourceType.LuminousDust, 6f, 1, 1)
                }
            });

            // Layer 5 - Crystal Chamber (105-155m adjusted = 110-160m raw)
            // Valuable resources, minimal empty
            layers.Add(new DepthLayerConfig
            {
                layerName = "Crystal Chamber",
                startDepth = 105f,
                endDepth = 155f,
                emptyWeight = 5f, // 5% chance of no loot
                terrainColor = new Color(0.35f, 0.40f, 0.60f),
                resources = new List<ResourceChance>
                {
                    new ResourceChance(UndergroundResourceType.CrystalCoreFragment, 20f, 1, 1),
                    new ResourceChance(UndergroundResourceType.LuminousDust, 18f, 1, 2),
                    new ResourceChance(UndergroundResourceType.BlueGreyOre, 18f, 1, 1),
                    new ResourceChance(UndergroundResourceType.DeepCrystalVein, 18f, 1, 2),
                    new ResourceChance(UndergroundResourceType.CrystalStone, 16f, 1, 2)
                }
            });

            // Layer 6 - Final Depth (155-215m adjusted = 160-220m raw)
            // Best resources, almost always drop something
            layers.Add(new DepthLayerConfig
            {
                layerName = "Final Depth",
                startDepth = 155f,
                endDepth = 215f,
                emptyWeight = 2f, // 2% chance of no loot - deepest layer rewards effort
                terrainColor = new Color(0.25f, 0.25f, 0.35f),
                resources = new List<ResourceChance>
                {
                    new ResourceChance(UndergroundResourceType.DeepBlackStone, 22f, 1, 2),
                    new ResourceChance(UndergroundResourceType.CrystalStone, 18f, 1, 2),
                    new ResourceChance(UndergroundResourceType.CoreCrystalChunk, 16f, 1, 1),
                    new ResourceChance(UndergroundResourceType.RareMachineParts, 10f, 1, 1),
                    new ResourceChance(UndergroundResourceType.AncientOre, 14f, 1, 1),
                    new ResourceChance(UndergroundResourceType.BlueGreyOre, 8f, 1, 1)
                }
            });

        }
    }
}
