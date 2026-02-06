using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Updates the terrain shader with layer colors from TerrainLayerSettings.
    /// Attach to any GameObject in the scene or it will auto-create.
    /// </summary>
    public class TerrainLayerShaderUpdater : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("TerrainLayerSettings to use. If null, loads from Resources.")]
        [SerializeField] private TerrainLayerSettings layerConfig;

        [Tooltip("Material to update. If null, finds material with 'TriplanarSoil' shader.")]
        [SerializeField] private Material terrainMaterial;

        [Header("Auto-Update")]
        [Tooltip("Automatically update shader when config changes.")]
        [SerializeField] private bool autoUpdateInEditor = true;

        [Tooltip("Update shader every frame (for runtime testing).")]
        [SerializeField] private bool continuousUpdate = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Shader property IDs (cached for performance)
        private static readonly int UseLayerColors = Shader.PropertyToID("_UseLayerColors");
        private static readonly int Layer1Color = Shader.PropertyToID("_Layer1Color");
        private static readonly int Layer2Color = Shader.PropertyToID("_Layer2Color");
        private static readonly int Layer3Color = Shader.PropertyToID("_Layer3Color");
        private static readonly int Layer4Color = Shader.PropertyToID("_Layer4Color");
        private static readonly int Layer5Color = Shader.PropertyToID("_Layer5Color");
        private static readonly int Layer6Color = Shader.PropertyToID("_Layer6Color");
        private static readonly int Layer1Depth = Shader.PropertyToID("_Layer1Depth");
        private static readonly int Layer2Depth = Shader.PropertyToID("_Layer2Depth");
        private static readonly int Layer3Depth = Shader.PropertyToID("_Layer3Depth");
        private static readonly int Layer4Depth = Shader.PropertyToID("_Layer4Depth");
        private static readonly int Layer5Depth = Shader.PropertyToID("_Layer5Depth");
        private static readonly int LayerBlendDistance = Shader.PropertyToID("_LayerBlendDistance");
        private static readonly int LayerColorStrength = Shader.PropertyToID("_LayerColorStrength");
        private static readonly int LayerEmissionStrength = Shader.PropertyToID("_LayerEmissionStrength");

        // Singleton
        private static TerrainLayerShaderUpdater _instance;
        public static TerrainLayerShaderUpdater Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            // Load config if not assigned
            if (layerConfig == null)
            {
                layerConfig = Resources.Load<TerrainLayerSettings>("TerrainLayerSettings");
            }

            // Find terrain material if not assigned
            if (terrainMaterial == null)
            {
                FindTerrainMaterial();
            }

            // Initial update
            UpdateShader();
        }

        private void Update()
        {
            if (continuousUpdate)
            {
                UpdateShader();
            }
        }

        /// <summary>
        /// Find the terrain material in the scene.
        /// </summary>
        private void FindTerrainMaterial()
        {
            // Look for ChunkManager's chunks
            var chunkManager = ChunkManager.Instance;
            if (chunkManager != null)
            {
                // ChunkManager should have reference to terrain material
                var chunksParent = GameObject.Find("Chunks");
                if (chunksParent != null && chunksParent.transform.childCount > 0)
                {
                    var firstChunk = chunksParent.transform.GetChild(0);
                    var renderer = firstChunk.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        if (renderer.sharedMaterial.shader.name.Contains("TriplanarSoil"))
                        {
                            terrainMaterial = renderer.sharedMaterial;
                            Debug.Log($"[TerrainLayerShaderUpdater] Found terrain material: {terrainMaterial.name}");
                            return;
                        }
                    }
                }
            }

            // Fallback: search all materials
            var allRenderers = FindObjectsOfType<MeshRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.shader.name.Contains("TriplanarSoil"))
                {
                    terrainMaterial = renderer.sharedMaterial;
                    Debug.Log($"[TerrainLayerShaderUpdater] Found terrain material: {terrainMaterial.name}");
                    return;
                }
            }

            Debug.LogWarning("[TerrainLayerShaderUpdater] Could not find terrain material with TriplanarSoil shader.");
        }

        /// <summary>
        /// Update the shader with current layer config values.
        /// </summary>
        [ContextMenu("Update Shader Now")]
        public void UpdateShader()
        {
            if (terrainMaterial == null)
            {
                FindTerrainMaterial();
                if (terrainMaterial == null) return;
            }

            if (layerConfig == null)
            {
                layerConfig = TerrainLayerDetector.Config;
                if (layerConfig == null)
                {
                    if (showDebugInfo)
                        Debug.LogWarning("[TerrainLayerShaderUpdater] No TerrainLayerSettings available.");
                    return;
                }
            }

            // Enable layer colors
            terrainMaterial.SetFloat(UseLayerColors, 1f);

            // Get layer data
            var layers = layerConfig.layers;
            if (layers == null || layers.Length == 0)
            {
                if (showDebugInfo)
                    Debug.LogWarning("[TerrainLayerShaderUpdater] No layers defined in config.");
                return;
            }

            // Shader property arrays for clean iteration
            int[] colorProps = { Layer1Color, Layer2Color, Layer3Color, Layer4Color, Layer5Color, Layer6Color };
            int[] depthProps = { Layer1Depth, Layer2Depth, Layer3Depth, Layer4Depth, Layer5Depth };

            // Set layer colors (up to 6 layers)
            for (int i = 0; i < Mathf.Min(layers.Length, 6); i++)
            {
                terrainMaterial.SetColor(colorProps[i], layers[i].primaryColor);
                // Depth boundaries: layers 1-5 have a depthEnd boundary (layer 6 is "everything below")
                if (i < 5 && i < depthProps.Length)
                {
                    terrainMaterial.SetFloat(depthProps[i], layers[i].depthEnd);
                }
            }

            // Set blend distance
            terrainMaterial.SetFloat(LayerBlendDistance, layerConfig.layerBlendDistance);

            // Color strength (how much layer color affects terrain)
            terrainMaterial.SetFloat(LayerColorStrength, 1.0f);

            // Emission strength for deep sci-fi glow
            terrainMaterial.SetFloat(LayerEmissionStrength, 0.8f);

            if (showDebugInfo)
            {
                Debug.Log($"[TerrainLayerShaderUpdater] Updated shader with {layers.Length} layers.");
            }
        }

        /// <summary>
        /// Set the layer config at runtime.
        /// </summary>
        public void SetConfig(TerrainLayerSettings config)
        {
            layerConfig = config;
            UpdateShader();
        }

        /// <summary>
        /// Disable layer colors in shader.
        /// </summary>
        public void DisableLayerColors()
        {
            if (terrainMaterial != null)
            {
                terrainMaterial.SetFloat(UseLayerColors, 0f);
            }
        }

        /// <summary>
        /// Enable layer colors in shader.
        /// </summary>
        public void EnableLayerColors()
        {
            UpdateShader();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (autoUpdateInEditor && Application.isPlaying == false)
            {
                // Delayed call to avoid issues during serialization
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        UpdateShader();
                    }
                };
            }
        }
#endif
    }
}
