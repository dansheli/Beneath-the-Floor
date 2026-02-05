using UnityEngine;
using BeneathTheFloor.Energy;

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// Creates machine prefabs using imported Asset Store models.
    /// Uses models from:
    /// - Workshop Tools (WoodenWorkbench) for Workbench
    /// - Industrial Machine Models (Machine_1, Machine_2, etc.) for Refinery and Energy Generator
    /// - Low Poly Factory Machine Pack Demo (Machn_2) for Upgrade Station
    /// </summary>
    public class AssetMachinePrefabBuilder : MonoBehaviour
    {
        [Header("Asset References")]
        // NOTE: workbenchAsset removed - Workbench system deprecated, using UpgradeStation instead

        [Tooltip("Assets/Industrial_Machine_Models/Prefabs/Machine_1.prefab - heavy industrial machine")]
        [SerializeField] private GameObject refineryAsset;

        [Tooltip("Assets/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Machine/Machn_2.prefab")]
        [SerializeField] private GameObject upgradeStationAsset;

        [Tooltip("Assets/Industrial_Machine_Models/Prefabs/Machine_3.prefab - generator-like machine")]
        [SerializeField] private GameObject energyGeneratorAsset;

        [Header("Fallback Settings")]
        [SerializeField] private bool usePrimitiveFallback = true;

        public static AssetMachinePrefabBuilder Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // NOTE: BuildWorkbench method removed - Workbench system deprecated, using UpgradeStation instead

        /// <summary>
        /// Build a Refinery using Machine_1 from Industrial Machine Models pack
        /// </summary>
        public GameObject BuildRefinery(Transform parent = null)
        {
            GameObject root = new GameObject("Refinery");
            if (parent != null) root.transform.SetParent(parent);

            GameObject modelPrefab = refineryAsset;
            if (modelPrefab == null)
            {
                modelPrefab = LoadAsset("Industrial_Machine_Models/Prefabs/Machine_1");
            }

            if (modelPrefab != null)
            {
                GameObject model = Instantiate(modelPrefab, root.transform);
                model.name = "Model_Machine_1";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * 0.8f;

                Debug.Log("[Machines] Created Refinery prefab using asset: Machine_1 from Industrial Machine Models");
            }
            else if (usePrimitiveFallback)
            {
                CreateFallbackRefinery(root);
                Debug.LogWarning("[Machines] Machine_1 asset not found, using primitive fallback");
            }

            // Add Refinery component
            var refinery = root.AddComponent<Refinery>();

            // Add energy consumer
            root.AddComponent<EnergyConsumer>();

            // Add collider
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(1.5f, 2f, 1.5f);

            // Add visual feedback and audio
            root.AddComponent<MachineVisualFeedback>();
            root.AddComponent<Audio.MachineAudio>();

            return root;
        }

        /// <summary>
        /// Build an Upgrade Station using Machn_2 from Low Poly Factory Machine Pack
        /// </summary>
        public GameObject BuildUpgradeStation(Transform parent = null)
        {
            GameObject root = new GameObject("UpgradeStation");
            if (parent != null) root.transform.SetParent(parent);

            GameObject modelPrefab = upgradeStationAsset;
            if (modelPrefab == null)
            {
                modelPrefab = LoadAsset("EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Machine/Machn_2");
            }

            if (modelPrefab != null)
            {
                GameObject model = Instantiate(modelPrefab, root.transform);
                model.name = "Model_Machn_2";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * 1.2f;

                Debug.Log("[Machines] Created UpgradeStation prefab using asset: Machn_2 from Low Poly Factory Machine Pack");
            }
            else if (usePrimitiveFallback)
            {
                CreateFallbackUpgradeStation(root);
                Debug.LogWarning("[Machines] Machn_2 asset not found, using primitive fallback");
            }

            // Add UpgradeStation component
            var upgradeStation = root.AddComponent<UpgradeStation>();

            // Add collider
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(1.8f, 2f, 1.2f);

            // Add visual feedback and audio
            root.AddComponent<MachineVisualFeedback>();
            root.AddComponent<Audio.MachineAudio>();

            return root;
        }

        /// <summary>
        /// Build an Energy Generator using Machine_3 from Industrial Machine Models pack
        /// </summary>
        public GameObject BuildEnergyGenerator(Transform parent = null)
        {
            GameObject root = new GameObject("EnergyGenerator");
            if (parent != null) root.transform.SetParent(parent);

            GameObject modelPrefab = energyGeneratorAsset;
            if (modelPrefab == null)
            {
                modelPrefab = LoadAsset("Industrial_Machine_Models/Prefabs/Machine_3");
            }

            if (modelPrefab != null)
            {
                GameObject model = Instantiate(modelPrefab, root.transform);
                model.name = "Model_Machine_3";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * 0.7f;

                Debug.Log("[Machines] Created EnergyGenerator prefab using asset: Machine_3 from Industrial Machine Models");
            }
            else if (usePrimitiveFallback)
            {
                CreateFallbackEnergyGenerator(root);
                Debug.LogWarning("[Machines] Machine_3 asset not found, using primitive fallback");
            }

            // Add EnergyGenerator component (which includes EnergySource)
            var generator = root.AddComponent<EnergyGenerator>();

            // Add collider
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(1.2f, 1.6f, 1.2f);

            // Add visual feedback and audio
            root.AddComponent<MachineVisualFeedback>();
            root.AddComponent<Audio.MachineAudio>();

            return root;
        }

        /// <summary>
        /// Try to load an asset from the project
        /// </summary>
        private GameObject LoadAsset(string path)
        {
#if UNITY_EDITOR
            string fullPath = $"Assets/{path}.prefab";
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
#else
            return null;
#endif
        }

        #region Primitive Fallbacks

        // NOTE: CreateFallbackWorkbench removed - Workbench system deprecated

        private void CreateFallbackRefinery(GameObject root)
        {
            Color refineryColor = new Color(0.35f, 0.35f, 0.4f);

            // Main body
            GameObject body = CreatePrimitive("Body", PrimitiveType.Cube, root.transform);
            body.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            SetColor(body, refineryColor);

            // Chimney
            GameObject chimney = CreatePrimitive("Chimney", PrimitiveType.Cylinder, root.transform);
            chimney.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            chimney.transform.localPosition = new Vector3(0.3f, 1.75f, 0.3f);
            SetColor(chimney, refineryColor * 0.8f);
        }

        private void CreateFallbackUpgradeStation(GameObject root)
        {
            Color stationColor = new Color(0.25f, 0.3f, 0.45f);

            // Base
            GameObject basePlat = CreatePrimitive("Base", PrimitiveType.Cube, root.transform);
            basePlat.transform.localScale = new Vector3(1.5f, 0.2f, 1.5f);
            basePlat.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            SetColor(basePlat, stationColor * 0.8f);

            // Pedestal
            GameObject pedestal = CreatePrimitive("Pedestal", PrimitiveType.Cylinder, root.transform);
            pedestal.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            pedestal.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            SetColor(pedestal, stationColor);

            // Console
            GameObject console = CreatePrimitive("Console", PrimitiveType.Cube, root.transform);
            console.transform.localScale = new Vector3(0.8f, 1.2f, 0.3f);
            console.transform.localPosition = new Vector3(0f, 0.8f, -0.5f);
            console.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            SetColor(console, stationColor * 1.1f);
        }

        private void CreateFallbackEnergyGenerator(GameObject root)
        {
            Color generatorColor = new Color(0.3f, 0.35f, 0.3f);

            // Main body
            GameObject body = CreatePrimitive("Body", PrimitiveType.Cube, root.transform);
            body.transform.localScale = new Vector3(1f, 1.2f, 0.8f);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            SetColor(body, generatorColor);

            // Generator cylinder
            GameObject cylinder = CreatePrimitive("Generator", PrimitiveType.Cylinder, root.transform);
            cylinder.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
            cylinder.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            SetColor(cylinder, new Color(0.5f, 0.5f, 0.2f));
        }

        private GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            // Remove collider (we add a single collider to root)
            var col = obj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return obj;
        }

        private void SetColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Use URP shader if available
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                Material mat = new Material(shader);
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                renderer.material = mat;
            }
        }

        #endregion
    }
}
