using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Audio;

namespace BeneathTheFloor.Machines
{
    public class MachinePrefabBuilder : MonoBehaviour
    {
        [Header("Default Materials")]
        [SerializeField] private Material machineMaterial;
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Material metalMaterial;

        [Header("Default Colors")]
        // NOTE: workbenchColor removed - Workbench system deprecated
        [SerializeField] private Color refineryColor = new Color(0.35f, 0.35f, 0.4f);
        [SerializeField] private Color upgradeStationColor = new Color(0.25f, 0.3f, 0.45f);


        public static MachinePrefabBuilder Instance { get; private set; }

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

        public GameObject BuildRefinery(Transform parent = null)
        {
            GameObject root = new GameObject("Refinery");
            if (parent != null) root.transform.SetParent(parent);

            // Main furnace body
            GameObject body = CreateBox("Body", root.transform, new Vector3(1.2f, 1.5f, 1.2f), refineryColor);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            // Furnace opening (darker inset)
            GameObject opening = CreateBox("Opening", root.transform, new Vector3(0.5f, 0.6f, 0.15f), Color.black);
            opening.transform.localPosition = new Vector3(0f, 0.6f, -0.55f);

            // Chimney
            GameObject chimney = CreateCylinder("Chimney", root.transform, 0.2f, 0.8f, refineryColor * 0.8f);
            chimney.transform.localPosition = new Vector3(0.3f, 1.9f, 0.2f);

            // Pipe elements
            CreateCylinder("Pipe1", root.transform, 0.08f, 0.5f, refineryColor * 0.7f)
                .transform.SetPositionAndRotation(
                    root.transform.position + new Vector3(-0.5f, 1.3f, 0f),
                    Quaternion.Euler(0f, 0f, 90f)
                );

            // Control panel
            GameObject panel = CreateBox("ControlPanel", root.transform, new Vector3(0.3f, 0.4f, 0.1f), new Color(0.2f, 0.2f, 0.25f));
            panel.transform.localPosition = new Vector3(-0.55f, 1f, -0.3f);

            // Indicator lights on panel
            CreateIndicatorLight("Light1", root.transform, Color.green)
                .transform.localPosition = new Vector3(-0.58f, 1.15f, -0.32f);
            CreateIndicatorLight("Light2", root.transform, Color.yellow)
                .transform.localPosition = new Vector3(-0.52f, 1.15f, -0.32f);
            CreateIndicatorLight("Light3", root.transform, Color.red)
                .transform.localPosition = new Vector3(-0.58f, 1.05f, -0.32f);

            // Processing glow (inside furnace)
            GameObject glow = new GameObject("ProcessingGlow");
            glow.transform.SetParent(root.transform);
            glow.transform.localPosition = new Vector3(0f, 0.6f, -0.3f);
            Light glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.5f, 0.2f);
            glowLight.range = 2f;
            glowLight.intensity = 0f; // Off by default

            // Smoke particle spawn point
            GameObject smokePoint = new GameObject("SmokePoint");
            smokePoint.transform.SetParent(root.transform);
            smokePoint.transform.localPosition = new Vector3(0.3f, 2.3f, 0.2f);

            // Add components
            AddMachineComponents(root, "Refinery");

            // Add collider
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(1.3f, 1.8f, 1.3f);

            return root;
        }

        public GameObject BuildUpgradeStation(Transform parent = null)
        {
            GameObject root = new GameObject("UpgradeStation");
            if (parent != null) root.transform.SetParent(parent);

            // Base platform
            GameObject basePlat = CreateBox("Base", root.transform, new Vector3(1.5f, 0.2f, 1.5f), upgradeStationColor * 0.8f);
            basePlat.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            // Central pedestal
            GameObject pedestal = CreateCylinder("Pedestal", root.transform, 0.3f, 0.8f, upgradeStationColor);
            pedestal.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            // Item platform on top
            GameObject itemPlatform = CreateCylinder("ItemPlatform", root.transform, 0.4f, 0.1f, upgradeStationColor * 1.2f);
            itemPlatform.transform.localPosition = new Vector3(0f, 1.05f, 0f);

            // Corner pillars with lights
            float pillarDist = 0.6f;
            Vector3[] pillarPositions = new Vector3[]
            {
                new Vector3(-pillarDist, 0f, -pillarDist),
                new Vector3(pillarDist, 0f, -pillarDist),
                new Vector3(-pillarDist, 0f, pillarDist),
                new Vector3(pillarDist, 0f, pillarDist)
            };

            for (int i = 0; i < pillarPositions.Length; i++)
            {
                GameObject pillar = CreateBox($"Pillar{i}", root.transform, new Vector3(0.15f, 1.2f, 0.15f), upgradeStationColor * 0.9f);
                pillar.transform.localPosition = pillarPositions[i] + new Vector3(0f, 0.6f, 0f);

                // Light on top of pillar
                GameObject pillarLight = CreateIndicatorLight($"PillarLight{i}", root.transform, new Color(0.3f, 0.6f, 1f));
                pillarLight.transform.localPosition = pillarPositions[i] + new Vector3(0f, 1.25f, 0f);

                // Add actual light
                Light light = pillarLight.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.3f, 0.6f, 1f);
                light.range = 1.5f;
                light.intensity = 0.5f;
            }

            // Central glow light
            GameObject centerGlow = new GameObject("CenterGlow");
            centerGlow.transform.SetParent(root.transform);
            centerGlow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light centerLight = centerGlow.AddComponent<Light>();
            centerLight.type = LightType.Point;
            centerLight.color = new Color(0.5f, 0.8f, 1f);
            centerLight.range = 2f;
            centerLight.intensity = 0f; // Off by default

            // Holographic ring (visual element)
            GameObject ring = CreateRing("HoloRing", root.transform, 0.5f, 0.02f, new Color(0.3f, 0.6f, 1f, 0.5f));
            ring.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            // Add components
            AddMachineComponents(root, "UpgradeStation");

            // Add collider
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.7f, 0f);
            col.size = new Vector3(1.5f, 1.4f, 1.5f);

            return root;
        }

        private GameObject CreateBox(string name, Transform parent, Vector3 size, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localScale = size;

            Renderer renderer = obj.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;

            // Remove collider (we add a single collider to root)
            Destroy(obj.GetComponent<Collider>());

            return obj;
        }

        private GameObject CreateCylinder(string name, Transform parent, float radius, float height, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);

            Renderer renderer = obj.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;

            Destroy(obj.GetComponent<Collider>());

            return obj;
        }

        private GameObject CreateIndicatorLight(string name, Transform parent, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localScale = Vector3.one * 0.06f;

            Renderer renderer = obj.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2f);
            renderer.material = mat;

            Destroy(obj.GetComponent<Collider>());

            return obj;
        }

        private GameObject CreateRing(string name, Transform parent, float radius, float thickness, Color color)
        {
            // Create a torus-like shape using a cylinder as approximation
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localScale = new Vector3(radius * 2, thickness, radius * 2);

            Renderer renderer = obj.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            renderer.material = mat;

            Destroy(obj.GetComponent<Collider>());

            return obj;
        }

        private void AddMachineComponents(GameObject root, string machineType)
        {
            // Add visual feedback
            MachineVisualFeedback visualFeedback = root.AddComponent<MachineVisualFeedback>();

            // Add audio
            MachineAudio machineAudio = root.AddComponent<MachineAudio>();

            // Find and assign lights
            Light[] lights = root.GetComponentsInChildren<Light>();
            // Visual feedback will find these automatically

            // Find and assign renderers for emission
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            // Could assign emissive renderers here

            Debug.Log($"[MachinePrefabBuilder] Built {machineType} with {lights.Length} lights and {renderers.Length} renderers");
        }
    }
}
