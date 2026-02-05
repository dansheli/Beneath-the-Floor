using UnityEngine;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Runtime initializer that automatically sets up FPS Arms when the game starts.
    /// This runs before other scripts and finds any FPS hands prefab instances to configure.
    /// DISABLED: We are using tool-only first-person view instead of arms.
    /// Set ENABLE_FPS_ARMS to true to re-enable arms.
    /// </summary>
    public static class FPArmsRuntimeInitializer
    {
        // Set to true to enable FPS arms, false to disable (using tool-only view)
        private const bool ENABLE_FPS_ARMS = false;

        // Run as early as possible to disable conflicting scripts
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableConflictingScriptsEarly()
        {
            if (!ENABLE_FPS_ARMS)
            {
                // FPS Arms disabled - using tool-only first-person view
                return;
            }

            #pragma warning disable CS0162 // Unreachable code when ENABLE_FPS_ARMS is false
            // Create a helper that will run in the first frame
            var helper = new GameObject("[FPArms_EarlyInit]");
            helper.AddComponent<FPArmsEarlyDisabler>();
            Object.DontDestroyOnLoad(helper);
            #pragma warning restore CS0162
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeFPSArms()
        {
            if (!ENABLE_FPS_ARMS)
            {
                // Clean up any existing FPS arms in scene
                CleanupExistingArms();
                return;
            }

            #pragma warning disable CS0162 // Unreachable code when ENABLE_FPS_ARMS is false
            // Look for FPS arms by common names
            string[] possibleNames = { "PlayerFP_Arms", "v1", "v2", "v3", "v4", "v5", "fps_hands", "FPS_Hands" };

            GameObject armsObject = null;

            foreach (string name in possibleNames)
            {
                armsObject = GameObject.Find(name);
                if (armsObject != null)
                {
                    // Verify it has an Animator (indicating it's the FPS hands)
                    if (armsObject.GetComponent<Animator>() != null ||
                        armsObject.GetComponentInChildren<Animator>() != null)
                    {
                        break;
                    }
                    armsObject = null;
                }
            }

            if (armsObject == null)
            {
                Debug.Log("[FPArmsRuntimeInitializer] No FPS arms found in scene - skipping initialization");
                return;
            }

            Debug.Log($"[FPArmsRuntimeInitializer] Found FPS arms: {armsObject.name}");

            // Disable the asset's built-in scripts (we use our own controllers)
            DisableAssetScripts(armsObject);

            // Remove any extra AudioListeners (asset may have its own)
            RemoveExtraAudioListeners(armsObject);

            // Add PlayerFPArmsController if not present
            var controller = armsObject.GetComponent<PlayerFPArmsController>();
            if (controller == null)
            {
                controller = armsObject.AddComponent<PlayerFPArmsController>();
                Debug.Log("[FPArmsRuntimeInitializer] Added PlayerFPArmsController");
            }

            // Ensure parented to camera
            if (armsObject.transform.parent == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    armsObject.transform.SetParent(mainCam.transform);
                    armsObject.transform.localPosition = new Vector3(0f, -0.5f, 0.3f);
                    armsObject.transform.localRotation = Quaternion.identity;
                    Debug.Log($"[FPArmsRuntimeInitializer] Parented arms to camera: {mainCam.name}");
                }
            }

            // Fix materials and disable shadow casting
            var renderers = armsObject.GetComponentsInChildren<Renderer>();
            Material armsMaterial = Resources.Load<Material>("FPArmsMaterial");

            // If no Resources material, try to find v1 material by searching
            if (armsMaterial == null)
            {
                // Create a simple skin-colored material at runtime
                armsMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (armsMaterial != null)
                {
                    armsMaterial.color = new Color(0.87f, 0.72f, 0.60f); // Skin tone
                    armsMaterial.name = "FPArms_RuntimeMaterial";
                }
            }

            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Always assign material to ensure hands are visible
                if (armsMaterial != null)
                {
                    renderer.material = armsMaterial;
                    Debug.Log($"[FPArmsRuntimeInitializer] Assigned material to: {renderer.gameObject.name}");
                }
            }

            // Rename for clarity
            if (armsObject.name != "PlayerFP_Arms")
            {
                armsObject.name = "PlayerFP_Arms";
            }

            // Fix headlamp position so it doesn't shine on hands
            FixHeadlampPosition(armsObject);

            Debug.Log("[FPArmsRuntimeInitializer] FPS Arms initialization complete!");
            #pragma warning restore CS0162
        }

        /// <summary>
        /// Cleans up any existing FPS arms in the scene when arms are disabled.
        /// Hides them rather than destroying to preserve references.
        /// </summary>
        private static void CleanupExistingArms()
        {
            string[] possibleNames = { "PlayerFP_Arms", "v1", "v2", "v3", "v4", "v5", "fps_hands", "FPS_Hands" };

            foreach (string name in possibleNames)
            {
                GameObject armsObject = GameObject.Find(name);
                if (armsObject != null)
                {
                    // Check if it's actually FPS arms (has Animator)
                    if (armsObject.GetComponent<Animator>() != null ||
                        armsObject.GetComponentInChildren<Animator>() != null)
                    {
                        armsObject.SetActive(false);
                        Debug.Log($"[FPArmsRuntimeInitializer] Disabled FPS arms object: {armsObject.name}");
                    }
                }
            }
        }

        /// <summary>
        /// Removes extra AudioListeners from the FPS arms (keeps only the main camera's).
        /// </summary>
        private static void RemoveExtraAudioListeners(GameObject armsObject)
        {
            // Remove AudioListeners from the arms hierarchy
            var listeners = armsObject.GetComponentsInChildren<AudioListener>(true);
            foreach (var listener in listeners)
            {
                Object.Destroy(listener);
                Debug.Log($"[FPArmsRuntimeInitializer] Removed extra AudioListener from: {listener.gameObject.name}");
            }

            // Also check for extra cameras that might have AudioListeners
            var cameras = armsObject.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras)
            {
                // Disable or destroy extra cameras from the asset
                cam.enabled = false;
                var camListener = cam.GetComponent<AudioListener>();
                if (camListener != null)
                {
                    Object.Destroy(camListener);
                }
                Debug.Log($"[FPArmsRuntimeInitializer] Disabled extra camera: {cam.gameObject.name}");
            }
        }

        /// <summary>
        /// Adjusts headlamp position to not illuminate the FPS hands directly.
        /// </summary>
        private static void FixHeadlampPosition(GameObject armsObject)
        {
            // Find headlamp in scene
            GameObject headlamp = GameObject.Find("Headlamp");
            if (headlamp == null)
            {
                // Try to find any spotlight that might be the headlamp
                var lights = Object.FindObjectsOfType<Light>();
                foreach (var light in lights)
                {
                    if (light.type == LightType.Spot && light.transform.IsChildOf(Camera.main?.transform))
                    {
                        headlamp = light.gameObject;
                        break;
                    }
                }
            }

            if (headlamp != null)
            {
                // Move headlamp forward so light starts past the hands
                // This way the cone of light doesn't hit the arms
                headlamp.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                Debug.Log($"[FPArmsRuntimeInitializer] Adjusted headlamp position to avoid illuminating hands");
            }
        }

        /// <summary>
        /// Disables the asset's built-in scripts that conflict with our systems.
        /// </summary>
        private static void DisableAssetScripts(GameObject armsObject)
        {
            // List of script type names from the asset that we don't need
            string[] scriptsToDisable = {
                "fps_controller",
                "fps_hands_anim_script",
                "mouse_look"
            };

            int disabledCount = 0;

            // Get all MonoBehaviours and disable the ones from the asset
            var allBehaviours = armsObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in allBehaviours)
            {
                if (behaviour == null) continue;

                string typeName = behaviour.GetType().Name;
                foreach (string scriptName in scriptsToDisable)
                {
                    if (typeName.Equals(scriptName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        behaviour.enabled = false;
                        disabledCount++;
                        Debug.Log($"[FPArmsRuntimeInitializer] Disabled asset script: {typeName}");
                        break;
                    }
                }
            }

            if (disabledCount > 0)
            {
                Debug.Log($"[FPArmsRuntimeInitializer] Disabled {disabledCount} asset scripts");
            }
        }
    }

    /// <summary>
    /// Helper component that disables conflicting FPS hands asset scripts as early as possible.
    /// Created automatically by FPArmsRuntimeInitializer.
    /// </summary>
    [DefaultExecutionOrder(-32000)] // Run extremely early - before almost everything
    public class FPArmsEarlyDisabler : MonoBehaviour
    {
        private bool hasDisabled = false;

        private void Awake()
        {
            DisableConflictingScriptsNow();
        }

        private void OnEnable()
        {
            // Also try in OnEnable in case Awake timing doesn't work
            DisableConflictingScriptsNow();
        }

        private void Update()
        {
            // Keep trying for first few frames to catch late-loaded objects
            if (!hasDisabled)
            {
                DisableConflictingScriptsNow();
            }

            // Self-destruct after ensuring scripts are disabled
            if (hasDisabled && Time.frameCount > 3)
            {
                Destroy(gameObject);
            }
        }

        private void DisableConflictingScriptsNow()
        {
            string[] scriptsToDisable = {
                "fps_controller",
                "fps_hands_anim_script",
                "mouse_look"
            };

            // Also disable legacy Animation components (we use Animator instead)
            var legacyAnimations = Resources.FindObjectsOfTypeAll<Animation>();
            foreach (var anim in legacyAnimations)
            {
                if (anim == null) continue;
                if (anim.gameObject.scene.name == null) continue;

                if (anim.enabled)
                {
                    anim.enabled = false;
                    Debug.Log($"[FPArmsEarlyDisabler] Disabled legacy Animation on {anim.gameObject.name}");
                }
            }

            // Find ALL MonoBehaviours including inactive ones
            var allBehaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

            int disabled = 0;

            foreach (var behaviour in allBehaviours)
            {
                if (behaviour == null) continue;

                // Skip if not a scene object (could be asset)
                if (behaviour.gameObject.scene.name == null) continue;

                string typeName = behaviour.GetType().Name;

                foreach (string scriptName in scriptsToDisable)
                {
                    if (typeName.Equals(scriptName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (behaviour.enabled)
                        {
                            behaviour.enabled = false;
                            disabled++;
                            Debug.Log($"[FPArmsEarlyDisabler] Disabled: {typeName} on {behaviour.gameObject.name}");
                        }
                        break;
                    }
                }
            }

            if (disabled > 0)
            {
                hasDisabled = true;
                Debug.Log($"[FPArmsEarlyDisabler] Total disabled: {disabled} conflicting scripts");
            }
        }
    }
}
