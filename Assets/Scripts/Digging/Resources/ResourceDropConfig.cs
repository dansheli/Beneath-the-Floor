using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Configuration asset for world-drop visual feedback when digging yields resources.
    /// Allows designers to customize drop appearance per resource type.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceDropConfig", menuName = "Beneath The Floor/Digging/Resource Drop Config")]
    public class ResourceDropConfig : ScriptableObject
    {
        [Header("Default Drop Settings")]
        [Tooltip("Default prefab for resource drops. Used when no specific prefab is configured for a resource type.")]
        public GameObject defaultPrefab;

        [Tooltip("Default color for drops that don't have a specific color configured.")]
        public Color defaultColor = new Color(0.6f, 0.5f, 0.4f); // Brownish

        [Tooltip("Default scale multiplier for drops.")]
        [Range(0.1f, 3f)]
        public float defaultScale = 1f;

        [Header("Per-Resource Overrides")]
        [Tooltip("Configure specific appearance for each resource type.")]
        public List<ResourceDropEntry> entries = new List<ResourceDropEntry>();

        /// <summary>
        /// Get the drop configuration for a specific resource type.
        /// Returns null if no specific entry exists (use defaults).
        /// </summary>
        public ResourceDropEntry GetEntry(UndergroundResourceType type)
        {
            if (entries == null) return null;

            foreach (var entry in entries)
            {
                if (entry.resourceType == type)
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Get the prefab to use for a resource type (entry-specific or default).
        /// </summary>
        public GameObject GetPrefab(UndergroundResourceType type)
        {
            var entry = GetEntry(type);
            if (entry != null && entry.prefab != null)
                return entry.prefab;
            return defaultPrefab;
        }

        /// <summary>
        /// Get the color to use for a resource type (entry-specific or default).
        /// </summary>
        public Color GetColor(UndergroundResourceType type)
        {
            var entry = GetEntry(type);
            if (entry != null && entry.overrideColor.a > 0f)
                return entry.overrideColor;
            return defaultColor;
        }

        /// <summary>
        /// Get the scale to use for a resource type (entry-specific or default).
        /// </summary>
        public float GetScale(UndergroundResourceType type)
        {
            var entry = GetEntry(type);
            if (entry != null && entry.scale > 0f)
                return entry.scale;
            return defaultScale;
        }

        /// <summary>
        /// Initialize with default entries for common resource types.
        /// </summary>
        [ContextMenu("Initialize Default Entries")]
        public void InitializeDefaultEntries()
        {
            entries.Clear();

            // Layer 1 - Soft Depth
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Dirt, new Color(0.55f, 0.40f, 0.25f), 0.8f)); // Brown
            entries.Add(new ResourceDropEntry(UndergroundResourceType.SoftStone, new Color(0.65f, 0.60f, 0.55f), 0.9f)); // Light gray
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Clay, new Color(0.75f, 0.45f, 0.30f), 0.85f)); // Orange-brown
            entries.Add(new ResourceDropEntry(UndergroundResourceType.IronNugget, new Color(0.70f, 0.55f, 0.40f), 1.0f)); // Rusty
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CopperFragment, new Color(0.80f, 0.50f, 0.30f), 0.95f)); // Copper

            // Layer 2 - Hard Soil
            entries.Add(new ResourceDropEntry(UndergroundResourceType.HardSoil, new Color(0.45f, 0.35f, 0.25f), 0.9f)); // Dark brown
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Stone, new Color(0.55f, 0.55f, 0.55f), 1.0f)); // Gray
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Sandstone, new Color(0.85f, 0.75f, 0.55f), 0.95f)); // Sandy
            entries.Add(new ResourceDropEntry(UndergroundResourceType.IronChunk, new Color(0.50f, 0.45f, 0.40f), 1.1f)); // Iron gray
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CopperPiece, new Color(0.85f, 0.55f, 0.35f), 1.0f)); // Copper
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Coal, new Color(0.20f, 0.20f, 0.20f), 0.9f)); // Black

            // Layer 3 - Heat & Pressure
            entries.Add(new ResourceDropEntry(UndergroundResourceType.HardSoilDeep, new Color(0.40f, 0.30f, 0.25f), 1.0f)); // Dark
            entries.Add(new ResourceDropEntry(UndergroundResourceType.HardStone, new Color(0.45f, 0.45f, 0.50f), 1.1f)); // Blue-gray
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Heatstone, new Color(0.90f, 0.40f, 0.20f), 1.0f)); // Orange-red
            entries.Add(new ResourceDropEntry(UndergroundResourceType.Quartz, new Color(0.95f, 0.95f, 1.0f), 1.1f)); // White
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CrystalDust, new Color(0.80f, 0.70f, 0.95f), 0.7f)); // Light purple
            entries.Add(new ResourceDropEntry(UndergroundResourceType.AncientOre, new Color(0.50f, 0.60f, 0.45f), 1.2f)); // Green-gray

            // Layer 4 - Crystal Roots
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CrystalShard, new Color(0.70f, 0.50f, 0.90f), 1.0f)); // Purple
            entries.Add(new ResourceDropEntry(UndergroundResourceType.PurpleQuartz, new Color(0.60f, 0.30f, 0.80f), 1.1f)); // Deep purple
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CrystalStone, new Color(0.55f, 0.55f, 0.75f), 1.0f)); // Blue-purple
            entries.Add(new ResourceDropEntry(UndergroundResourceType.DeepCrystalVein, new Color(0.45f, 0.35f, 0.70f), 1.2f)); // Dark purple
            entries.Add(new ResourceDropEntry(UndergroundResourceType.LuminousDust, new Color(0.95f, 0.95f, 0.70f), 0.6f)); // Glowing yellow

            // Layer 5 - Crystal Chamber
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CrystalCoreFragment, new Color(0.90f, 0.70f, 1.0f), 1.3f)); // Pink-purple
            entries.Add(new ResourceDropEntry(UndergroundResourceType.BlueGreyOre, new Color(0.50f, 0.60f, 0.75f), 1.1f)); // Blue-gray

            // Layer 6 - Final Depth
            entries.Add(new ResourceDropEntry(UndergroundResourceType.DeepBlackStone, new Color(0.15f, 0.15f, 0.20f), 1.2f)); // Near black
            entries.Add(new ResourceDropEntry(UndergroundResourceType.CoreCrystalChunk, new Color(0.95f, 0.85f, 1.0f), 1.4f)); // Bright purple-white
            entries.Add(new ResourceDropEntry(UndergroundResourceType.RareMachineParts, new Color(0.70f, 0.75f, 0.80f), 1.0f)); // Metallic

            Debug.Log($"[ResourceDropConfig] Initialized {entries.Count} default resource drop entries.");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>
    /// Configuration entry for a single resource type's world drop appearance.
    /// </summary>
    [System.Serializable]
    public class ResourceDropEntry
    {
        [Tooltip("The resource type this entry configures.")]
        public UndergroundResourceType resourceType;

        [Tooltip("Custom prefab for this resource. If null, uses the default prefab.")]
        public GameObject prefab;

        [Tooltip("Color override for this resource's drop. Alpha 0 = use default color.")]
        public Color overrideColor = Color.white;

        [Tooltip("Scale multiplier for this resource's drop. 0 = use default scale.")]
        [Range(0f, 3f)]
        public float scale = 1f;

        public ResourceDropEntry() { }

        public ResourceDropEntry(UndergroundResourceType type, Color color, float scaleMultiplier)
        {
            resourceType = type;
            prefab = null; // Use default
            overrideColor = color;
            scale = scaleMultiplier;
        }
    }
}
