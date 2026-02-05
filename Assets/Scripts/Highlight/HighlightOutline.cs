using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Highlight
{
    [RequireComponent(typeof(Renderer))]
    public class HighlightOutline : MonoBehaviour
    {
        [Header("Outline Settings")]
        [SerializeField] private Color outlineColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField] private float outlineWidth = 0.02f;

        [Header("Mode")]
        [SerializeField] private OutlineMode mode = OutlineMode.OutlineVisible;

        public enum OutlineMode
        {
            OutlineAll,
            OutlineVisible,
            OutlineOccluded
        }

        private Renderer targetRenderer;
        private List<GameObject> outlineObjects = new List<GameObject>();
        private Material outlineMaterial;
        private bool isInitialized = false;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            Initialize();
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                Initialize();
            }
            ShowOutline(true);
        }

        private void OnDisable()
        {
            ShowOutline(false);
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            CreateOutlineMaterial();
            CreateOutlineMeshes();

            isInitialized = true;
        }

        private void CreateOutlineMaterial()
        {
            // Create a simple unlit material for the outline
            outlineMaterial = new Material(Shader.Find("Unlit/Color"));
            outlineMaterial.color = outlineColor;

            // Enable rendering on both sides
            outlineMaterial.SetInt("_Cull", 0);
        }

        private void CreateOutlineMeshes()
        {
            // Get all mesh filters in the object
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null) continue;

                // Create outline object
                GameObject outlineObj = new GameObject($"{meshFilter.gameObject.name}_Outline");
                outlineObj.transform.SetParent(meshFilter.transform);
                outlineObj.transform.localPosition = Vector3.zero;
                outlineObj.transform.localRotation = Quaternion.identity;
                outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth);

                // Add mesh filter with same mesh
                MeshFilter outlineMeshFilter = outlineObj.AddComponent<MeshFilter>();
                outlineMeshFilter.sharedMesh = meshFilter.sharedMesh;

                // Add mesh renderer with outline material
                MeshRenderer outlineRenderer = outlineObj.AddComponent<MeshRenderer>();
                outlineRenderer.material = outlineMaterial;
                outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outlineRenderer.receiveShadows = false;

                outlineObjects.Add(outlineObj);
            }

            // Also handle skinned mesh renderers
            SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var skinnedRenderer in skinnedRenderers)
            {
                if (skinnedRenderer.sharedMesh == null) continue;

                GameObject outlineObj = new GameObject($"{skinnedRenderer.gameObject.name}_Outline");
                outlineObj.transform.SetParent(skinnedRenderer.transform);
                outlineObj.transform.localPosition = Vector3.zero;
                outlineObj.transform.localRotation = Quaternion.identity;
                outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth);

                // Create static mesh version for outline
                MeshFilter outlineMeshFilter = outlineObj.AddComponent<MeshFilter>();
                outlineMeshFilter.sharedMesh = skinnedRenderer.sharedMesh;

                MeshRenderer outlineRenderer = outlineObj.AddComponent<MeshRenderer>();
                outlineRenderer.material = outlineMaterial;
                outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outlineRenderer.receiveShadows = false;

                outlineObjects.Add(outlineObj);
            }

            // Initially hide outline
            ShowOutline(false);
        }

        private void ShowOutline(bool show)
        {
            foreach (var outlineObj in outlineObjects)
            {
                if (outlineObj != null)
                {
                    outlineObj.SetActive(show);
                }
            }
        }

        public void SetColor(Color color)
        {
            outlineColor = color;

            if (outlineMaterial != null)
            {
                outlineMaterial.color = color;
            }
        }

        public void SetWidth(float width)
        {
            outlineWidth = Mathf.Max(0.001f, width);

            // Update scale of outline objects
            foreach (var outlineObj in outlineObjects)
            {
                if (outlineObj != null)
                {
                    outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth);
                }
            }
        }

        public void SetMode(OutlineMode newMode)
        {
            mode = newMode;
            // Could implement different rendering modes here
        }

        private void Cleanup()
        {
            foreach (var outlineObj in outlineObjects)
            {
                if (outlineObj != null)
                {
                    Destroy(outlineObj);
                }
            }
            outlineObjects.Clear();

            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }
        }

        public void Refresh()
        {
            Cleanup();
            isInitialized = false;
            Initialize();
            ShowOutline(enabled);
        }
    }
}
