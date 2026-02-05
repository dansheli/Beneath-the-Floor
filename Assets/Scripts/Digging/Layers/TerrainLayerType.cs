using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Defines terrain layer types for depth-based terrain variation.
    ///
    /// CURRENT: Used as metadata placeholder for future implementation.
    /// FUTURE: Each layer will have different:
    /// - Visual appearance (color, material)
    /// - Dig resistance (hardness)
    /// - Resource drop tables
    /// - Sound effects
    /// </summary>
    public enum TerrainLayerType : byte
    {
        /// <summary>
        /// Empty/air voxel (density = 0).
        /// </summary>
        Air = 0,

        /// <summary>
        /// Soft topsoil - easy to dig.
        /// </summary>
        Topsoil = 1,

        /// <summary>
        /// Standard dirt/soil.
        /// </summary>
        Soil = 2,

        /// <summary>
        /// Clay layer - slightly harder.
        /// </summary>
        Clay = 3,

        /// <summary>
        /// Gravel/rocky soil - harder to dig.
        /// </summary>
        Gravel = 4,

        /// <summary>
        /// Soft rock layer.
        /// </summary>
        SoftRock = 5,

        /// <summary>
        /// Hard rock - requires strong tools.
        /// </summary>
        HardRock = 6,

        /// <summary>
        /// Bedrock - undiggable protective boundary.
        /// </summary>
        Bedrock = 7
    }

    /// <summary>
    /// Configuration data for a terrain layer.
    /// Use this to define layer-specific properties.
    ///
    /// FUTURE: This will be loaded from ScriptableObject configs.
    /// </summary>
    [System.Serializable]
    public struct TerrainLayerConfig
    {
        /// <summary>
        /// The layer type this config applies to.
        /// </summary>
        public TerrainLayerType LayerType;

        /// <summary>
        /// Display name for UI/debug.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Base color for this layer (before material variation).
        /// </summary>
        public Color BaseColor;

        /// <summary>
        /// Dig resistance (0.0 = instant, 1.0 = normal, 2.0+ = hard).
        /// </summary>
        public float Hardness;

        /// <summary>
        /// Whether this layer can be dug at all.
        /// </summary>
        public bool IsDiggable;

        /// <summary>
        /// Minimum tool hardness required to dig this layer.
        /// </summary>
        public float RequiredToolHardness;

        /// <summary>
        /// Create default config for a layer type.
        /// </summary>
        public static TerrainLayerConfig CreateDefault(TerrainLayerType type)
        {
            switch (type)
            {
                case TerrainLayerType.Air:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Air",
                        BaseColor = Color.clear,
                        Hardness = 0f,
                        IsDiggable = false,
                        RequiredToolHardness = 0f
                    };

                case TerrainLayerType.Topsoil:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Topsoil",
                        BaseColor = new Color(0.35f, 0.25f, 0.15f),
                        Hardness = 0.5f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.1f
                    };

                case TerrainLayerType.Soil:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Soil",
                        BaseColor = new Color(0.45f, 0.30f, 0.18f),
                        Hardness = 1.0f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.2f
                    };

                case TerrainLayerType.Clay:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Clay",
                        BaseColor = new Color(0.6f, 0.45f, 0.35f),
                        Hardness = 1.5f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.3f
                    };

                case TerrainLayerType.Gravel:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Gravel",
                        BaseColor = new Color(0.5f, 0.5f, 0.45f),
                        Hardness = 2.0f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.5f
                    };

                case TerrainLayerType.SoftRock:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Soft Rock",
                        BaseColor = new Color(0.55f, 0.55f, 0.5f),
                        Hardness = 3.0f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.7f
                    };

                case TerrainLayerType.HardRock:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Hard Rock",
                        BaseColor = new Color(0.4f, 0.4f, 0.4f),
                        Hardness = 5.0f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.9f
                    };

                case TerrainLayerType.Bedrock:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Bedrock",
                        BaseColor = new Color(0.2f, 0.2f, 0.2f),
                        Hardness = float.MaxValue,
                        IsDiggable = false,
                        RequiredToolHardness = float.MaxValue
                    };

                default:
                    return new TerrainLayerConfig
                    {
                        LayerType = type,
                        DisplayName = "Unknown",
                        BaseColor = Color.magenta,
                        Hardness = 1.0f,
                        IsDiggable = true,
                        RequiredToolHardness = 0.5f
                    };
            }
        }
    }
}
