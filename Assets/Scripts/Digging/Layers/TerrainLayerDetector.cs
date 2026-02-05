using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Static helper class for detecting terrain layers at any position.
    /// Provides easy access to layer information throughout the codebase.
    /// </summary>
    public static class TerrainLayerDetector
    {
        private static TerrainLayerSettings _cachedConfig;
        private static bool _configLoaded;

        /// <summary>
        /// Get or load the terrain layer configuration.
        /// </summary>
        public static TerrainLayerSettings Config
        {
            get
            {
                if (!_configLoaded)
                {
                    LoadConfig();
                }
                return _cachedConfig;
            }
        }

        /// <summary>
        /// Force reload of the config (call after changing config at runtime).
        /// </summary>
        public static void ReloadConfig()
        {
            _configLoaded = false;
            _cachedConfig = null;
            LoadConfig();
        }

        /// <summary>
        /// Set the config directly (useful for testing or custom setups).
        /// </summary>
        public static void SetConfig(TerrainLayerSettings config)
        {
            _cachedConfig = config;
            _configLoaded = true;
        }

        private static void LoadConfig()
        {
            _cachedConfig = Resources.Load<TerrainLayerSettings>("TerrainLayerSettings");
            if (_cachedConfig == null)
            {
                // Try alternative paths
                _cachedConfig = Resources.Load<TerrainLayerSettings>("Config/TerrainLayerSettings");
            }
            _configLoaded = true;

            if (_cachedConfig == null)
            {
                Debug.LogWarning("[TerrainLayerDetector] No TerrainLayerSettings found in Resources. " +
                               "Create one via 'Create > Beneath The Floor > Terrain Layer Config' and place in Resources folder.");
            }
        }

        // Reset statics when entering play mode (Editor)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cachedConfig = null;
            _configLoaded = false;
        }

        /// <summary>
        /// Get the layer at a world position.
        /// </summary>
        public static TerrainLayerSettings.LayerDefinition GetLayerAt(Vector3 worldPosition)
        {
            return GetLayerAtDepth(worldPosition.y);
        }

        /// <summary>
        /// Get the layer at a specific Y depth.
        /// </summary>
        public static TerrainLayerSettings.LayerDefinition GetLayerAtDepth(float worldY)
        {
            if (Config == null) return null;
            return Config.GetLayerAtDepth(worldY);
        }

        /// <summary>
        /// Get layer index at a world position.
        /// </summary>
        public static int GetLayerIndexAt(Vector3 worldPosition)
        {
            return GetLayerIndexAtDepth(worldPosition.y);
        }

        /// <summary>
        /// Get layer index at a specific Y depth.
        /// </summary>
        public static int GetLayerIndexAtDepth(float worldY)
        {
            if (Config == null) return -1;
            return Config.GetLayerIndexAtDepth(worldY);
        }

        /// <summary>
        /// Get layer hardness at a world position.
        /// </summary>
        public static float GetHardnessAt(Vector3 worldPosition)
        {
            var layer = GetLayerAt(worldPosition);
            return layer?.hardness ?? 1f;
        }

        /// <summary>
        /// Get blended color at a world position (with smooth transitions).
        /// </summary>
        public static Color GetColorAt(Vector3 worldPosition)
        {
            if (Config == null) return Color.gray;
            return Config.GetBlendedColorAtDepth(worldPosition.y);
        }

        /// <summary>
        /// Calculate tool effectiveness at a position.
        /// Returns >= 1.0 for full effectiveness, < 1.0 for weakened.
        /// </summary>
        public static float CalculateEffectiveness(float toolHardness, Vector3 worldPosition)
        {
            var layer = GetLayerAt(worldPosition);
            if (layer == null || Config == null) return 1f;
            return Config.CalculateEffectiveness(toolHardness, layer.hardness);
        }

        /// <summary>
        /// Check if a tool can dig at a position.
        /// </summary>
        public static bool CanToolDigAt(float toolHardness, Vector3 worldPosition)
        {
            var layer = GetLayerAt(worldPosition);
            if (layer == null || Config == null) return true;
            return Config.CanToolDigLayer(toolHardness, layer);
        }

        /// <summary>
        /// Check if a tool is weakened at a position.
        /// </summary>
        public static bool IsToolWeakenedAt(float toolHardness, Vector3 worldPosition)
        {
            var layer = GetLayerAt(worldPosition);
            if (layer == null || Config == null) return false;
            return Config.IsToolWeakened(toolHardness, layer);
        }

        /// <summary>
        /// Get dig result info for a tool at a position.
        /// </summary>
        public static DigLayerResult GetDigResult(float toolHardness, Vector3 worldPosition)
        {
            var result = new DigLayerResult();
            var layer = GetLayerAt(worldPosition);

            if (layer == null || Config == null)
            {
                result.CanDig = true;
                result.IsWeakened = false;
                result.Effectiveness = 1f;
                result.LayerName = "Unknown";
                result.LayerColor = Color.gray;
                return result;
            }

            result.Effectiveness = Config.CalculateEffectiveness(toolHardness, layer.hardness);
            result.CanDig = result.Effectiveness >= Config.blockedEffectivenessThreshold;
            result.IsWeakened = result.Effectiveness < Config.weakEffectivenessThreshold && result.CanDig;
            result.LayerName = layer.layerName;
            result.LayerColor = layer.primaryColor;
            result.LayerHardness = layer.hardness;
            result.ParticleColor = layer.particleColor;

            if (result.IsWeakened && Config != null)
            {
                result.SpeedMultiplier = Config.weakenedSpeedMultiplier;
            }
            else
            {
                result.SpeedMultiplier = 1f;
            }

            return result;
        }
    }

    /// <summary>
    /// Result of checking if a tool can dig at a position.
    /// </summary>
    public struct DigLayerResult
    {
        /// <summary>True if tool can dig this layer at all.</summary>
        public bool CanDig;

        /// <summary>True if tool is weakened (slower, causes wear).</summary>
        public bool IsWeakened;

        /// <summary>Effectiveness ratio (tool hardness / layer hardness).</summary>
        public float Effectiveness;

        /// <summary>Speed multiplier for dig action.</summary>
        public float SpeedMultiplier;

        /// <summary>Name of the layer being dug.</summary>
        public string LayerName;

        /// <summary>Color of the layer.</summary>
        public Color LayerColor;

        /// <summary>Hardness of the layer.</summary>
        public float LayerHardness;

        /// <summary>Color for dig particles.</summary>
        public Color ParticleColor;
    }
}
