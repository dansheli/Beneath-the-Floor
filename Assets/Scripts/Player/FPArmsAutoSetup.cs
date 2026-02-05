using UnityEngine;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Auto-setup component for FPS Arms.
    /// Add this to any FPS hands prefab to automatically configure it at runtime.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run early
    public class FPArmsAutoSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [Tooltip("Automatically add PlayerFPArmsController if not present.")]
        [SerializeField] private bool autoAddController = true;

        [Tooltip("Automatically disable shadow casting on arm renderers.")]
        [SerializeField] private bool disableShadows = true;

        [Tooltip("Automatically parent to main camera if not already.")]
        [SerializeField] private bool autoParentToCamera = true;

        [Header("Position Settings")]
        [SerializeField] private Vector3 defaultLocalPosition = new Vector3(0f, -0.5f, 0.3f);
        [SerializeField] private Vector3 defaultLocalRotation = Vector3.zero;

        private void Awake()
        {
            Debug.Log("[FPArmsAutoSetup] Initializing FPS Arms...");

            // Auto-parent to camera if needed
            if (autoParentToCamera && transform.parent == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    transform.SetParent(mainCam.transform);
                    transform.localPosition = defaultLocalPosition;
                    transform.localRotation = Quaternion.Euler(defaultLocalRotation);
                    Debug.Log($"[FPArmsAutoSetup] Parented to camera: {mainCam.name}");
                }
            }

            // Add controller if needed
            if (autoAddController)
            {
                var controller = GetComponent<PlayerFPArmsController>();
                if (controller == null)
                {
                    controller = gameObject.AddComponent<PlayerFPArmsController>();
                    Debug.Log("[FPArmsAutoSetup] Added PlayerFPArmsController");
                }
            }

            // Disable shadows
            if (disableShadows)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Debug.Log($"[FPArmsAutoSetup] Disabled shadows on {renderers.Length} renderers");
            }

            // Rename for clarity
            if (gameObject.name == "v1" || gameObject.name.StartsWith("v1("))
            {
                gameObject.name = "PlayerFP_Arms";
            }

            Debug.Log("[FPArmsAutoSetup] FPS Arms setup complete!");
        }
    }
}
