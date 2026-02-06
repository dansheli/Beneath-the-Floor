using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Configuration for terrain layers. All settings accessible via Inspector.
    /// Each layer has depth range, hardness, and color properties.
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainLayerSettings", menuName = "Beneath The Floor/Terrain Layer Settings")]
    public class TerrainLayerSettings : ScriptableObject
    {
        [System.Serializable]
        public class LayerDefinition
        {
            [Tooltip("Display name for this layer")]
            public string layerName = "New Layer";

            [Tooltip("World Y position where this layer starts (top of layer)")]
            public float depthStart = 0f;

            [Tooltip("World Y position where this layer ends (bottom of layer)")]
            public float depthEnd = -10f;

            [Tooltip("Hardness rating. Tool needs hardness >= this to dig at full speed")]
            [Range(0.5f, 10f)]
            public float hardness = 1f;

            [Tooltip("Primary color for this layer in the terrain shader")]
            public Color primaryColor = new Color(0.36f, 0.25f, 0.2f, 1f);

            [Tooltip("Secondary color for variation/blending")]
            public Color secondaryColor = new Color(0.3f, 0.2f, 0.15f, 1f);

            [Tooltip("How much rock texture shows through (0=pure soil, 1=pure rock)")]
            [Range(0f, 1f)]
            public float rockBlend = 0f;

            [Tooltip("Particle effect color when digging this layer")]
            public Color particleColor = new Color(0.4f, 0.3f, 0.2f, 1f);

            [Tooltip("Optional audio clip for digging this layer")]
            public AudioClip digSound;
        }

        [Header("Layer Definitions")]
        [Tooltip("Define each terrain layer from top to bottom. First layer should start at highest Y.")]
        public LayerDefinition[] layers = new LayerDefinition[]
        {
            new LayerDefinition
            {
                layerName = "Topsoil",
                depthStart = 0f,
                depthEnd = -20f,
                hardness = 1.0f,
                primaryColor = new Color(0.55f, 0.35f, 0.18f, 1f),   // Warm brown
                secondaryColor = new Color(0.45f, 0.28f, 0.14f, 1f),
                rockBlend = 0f,
                particleColor = new Color(0.6f, 0.4f, 0.2f, 1f)
            },
            new LayerDefinition
            {
                layerName = "Clay",
                depthStart = -20f,
                depthEnd = -40f,
                hardness = 1.8f,
                primaryColor = new Color(0.72f, 0.38f, 0.12f, 1f),   // Orange-brown
                secondaryColor = new Color(0.60f, 0.30f, 0.10f, 1f),
                rockBlend = 0.15f,
                particleColor = new Color(0.75f, 0.42f, 0.15f, 1f)
            },
            new LayerDefinition
            {
                layerName = "Slate",
                depthStart = -40f,
                depthEnd = -60f,
                hardness = 2.5f,
                primaryColor = new Color(0.45f, 0.45f, 0.48f, 1f),   // Cool gray stone
                secondaryColor = new Color(0.38f, 0.38f, 0.42f, 1f),
                rockBlend = 0.5f,
                particleColor = new Color(0.50f, 0.50f, 0.52f, 1f)
            },
            new LayerDefinition
            {
                layerName = "Deep Rock",
                depthStart = -60f,
                depthEnd = -80f,
                hardness = 3.5f,
                primaryColor = new Color(0.25f, 0.28f, 0.38f, 1f),   // Dark blue-gray
                secondaryColor = new Color(0.20f, 0.22f, 0.32f, 1f),
                rockBlend = 0.7f,
                particleColor = new Color(0.30f, 0.32f, 0.42f, 1f)
            },
            new LayerDefinition
            {
                layerName = "Crystal Caverns",
                depthStart = -80f,
                depthEnd = -100f,
                hardness = 4.5f,
                primaryColor = new Color(0.35f, 0.15f, 0.50f, 1f),   // Deep purple / violet
                secondaryColor = new Color(0.28f, 0.10f, 0.42f, 1f),
                rockBlend = 0.8f,
                particleColor = new Color(0.50f, 0.20f, 0.65f, 1f)
            },
            new LayerDefinition
            {
                layerName = "The Core",
                depthStart = -100f,
                depthEnd = -120f,
                hardness = 6.0f,
                primaryColor = new Color(0.10f, 0.55f, 0.55f, 1f),   // Neon teal / cyan
                secondaryColor = new Color(0.05f, 0.45f, 0.48f, 1f),
                rockBlend = 0.9f,
                particleColor = new Color(0.15f, 0.65f, 0.60f, 1f)
            }
        };

        [Header("Tool Exhaustion Settings")]
        [Tooltip("Number of 'weak' digs before tool becomes exhausted")]
        [Range(1, 50)]
        public int weakDigsBeforeExhaustion = 8;

        [Tooltip("Effectiveness threshold below which dig is considered 'weak' (tool hardness / layer hardness)")]
        [Range(0.1f, 1f)]
        public float weakEffectivenessThreshold = 1.0f;

        [Tooltip("Effectiveness threshold below which digging is completely blocked")]
        [Range(0f, 0.9f)]
        public float blockedEffectivenessThreshold = 0.5f;

        [Tooltip("Speed multiplier when tool is weakened")]
        [Range(0.1f, 0.9f)]
        public float weakenedSpeedMultiplier = 0.5f;

        [Header("Layer Transition Settings")]
        [Tooltip("Blend distance between layers (in world units)")]
        [Range(0.5f, 5f)]
        public float layerBlendDistance = 1.5f;

        [Header("Visual Feedback")]
        [Tooltip("Show particle effect when hitting harder layer")]
        public bool showBlockedParticles = true;

        [Tooltip("Color of sparks when tool is weakened")]
        public Color weakenedSparkColor = new Color(1f, 0.8f, 0f, 1f); // Yellow

        [Tooltip("Color of sparks when tool is blocked")]
        public Color blockedSparkColor = new Color(1f, 0.2f, 0.1f, 1f); // Red

        [Header("Audio")]
        [Tooltip("Sound when tool hits layer it cannot dig")]
        public AudioClip blockedSound;

        [Tooltip("Sound when tool is exhausted")]
        public AudioClip exhaustedSound;

        /// <summary>
        /// Get the layer at a given world Y position.
        /// </summary>
        public LayerDefinition GetLayerAtDepth(float worldY)
        {
            if (layers == null || layers.Length == 0)
                return null;

            foreach (var layer in layers)
            {
                if (worldY <= layer.depthStart && worldY > layer.depthEnd)
                {
                    return layer;
                }
            }

            // Return deepest layer if below all defined layers
            return layers[layers.Length - 1];
        }

        /// <summary>
        /// Get layer index at a given world Y position.
        /// </summary>
        public int GetLayerIndexAtDepth(float worldY)
        {
            if (layers == null || layers.Length == 0)
                return -1;

            for (int i = 0; i < layers.Length; i++)
            {
                if (worldY <= layers[i].depthStart && worldY > layers[i].depthEnd)
                {
                    return i;
                }
            }

            return layers.Length - 1;
        }

        /// <summary>
        /// Calculate tool effectiveness against a layer.
        /// Returns value >= 1.0 for full effectiveness, < 1.0 for weakened.
        /// </summary>
        public float CalculateEffectiveness(float toolHardness, float layerHardness)
        {
            if (layerHardness <= 0) return 1f;
            return toolHardness / layerHardness;
        }

        /// <summary>
        /// Check if tool can dig a layer at all.
        /// </summary>
        public bool CanToolDigLayer(float toolHardness, LayerDefinition layer)
        {
            if (layer == null) return true;
            float effectiveness = CalculateEffectiveness(toolHardness, layer.hardness);
            return effectiveness >= blockedEffectivenessThreshold;
        }

        /// <summary>
        /// Check if tool is weakened against a layer.
        /// </summary>
        public bool IsToolWeakened(float toolHardness, LayerDefinition layer)
        {
            if (layer == null) return false;
            float effectiveness = CalculateEffectiveness(toolHardness, layer.hardness);
            return effectiveness < weakEffectivenessThreshold && effectiveness >= blockedEffectivenessThreshold;
        }

        /// <summary>
        /// Get blended color at a world Y position (handles transitions).
        /// </summary>
        public Color GetBlendedColorAtDepth(float worldY)
        {
            if (layers == null || layers.Length == 0)
                return Color.gray;

            int layerIndex = GetLayerIndexAtDepth(worldY);
            if (layerIndex < 0) return Color.gray;

            var currentLayer = layers[layerIndex];

            // Check if we're in a transition zone
            if (layerIndex < layers.Length - 1)
            {
                float distanceToNext = Mathf.Abs(worldY - currentLayer.depthEnd);
                if (distanceToNext < layerBlendDistance)
                {
                    var nextLayer = layers[layerIndex + 1];
                    float t = 1f - (distanceToNext / layerBlendDistance);
                    return Color.Lerp(currentLayer.primaryColor, nextLayer.primaryColor, t);
                }
            }

            return currentLayer.primaryColor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure layers are sorted by depth (highest first)
            if (layers != null && layers.Length > 1)
            {
                for (int i = 0; i < layers.Length - 1; i++)
                {
                    if (layers[i].depthStart < layers[i + 1].depthStart)
                    {
                        Debug.LogWarning($"[TerrainLayerConfig] Layer '{layers[i].layerName}' should have higher depthStart than '{layers[i + 1].layerName}'");
                    }
                }
            }
        }
#endif
    }
}
