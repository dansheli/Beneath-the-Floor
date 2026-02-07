using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Simplified tool controller - manages which tool tier is active.
    /// Tool positioning is done manually on the prefabs (no ToolHolder).
    /// </summary>
    public class HeldToolController : MonoBehaviour
    {
        [Header("Tool 1: Shovel (Depth 15m)")]
        [SerializeField] private GameObject tool1_Base;   // Base_Shovel
        [SerializeField] private GameObject tool1_Tier1;  // Tier1_Shovel
        [SerializeField] private GameObject tool1_Tier2;  // Tier2_Shovel
        [SerializeField] private GameObject tool1_Tier3;  // Tier3_Shovel

        [Header("Tool 2: Heavy Spade (Depth 22m)")]
        [SerializeField] private GameObject tool2_Base;   // Heavy_Spade_Base
        [SerializeField] private GameObject tool2_Tier1;  // Heavy_Spade_Tier1
        [SerializeField] private GameObject tool2_Tier2;  // Heavy_Spade_Tier2
        [SerializeField] private GameObject tool2_Tier3;  // Heavy_Spade_Tier3

        [Header("Tool 3: Pickaxe (Depth 30m)")]
        [SerializeField] private GameObject tool3_Base;   // pickaxe_Base
        [SerializeField] private GameObject tool3_Tier1;  // pickaxe_Tier1
        [SerializeField] private GameObject tool3_Tier2;  // pickaxe_Tier2
        [SerializeField] private GameObject tool3_Tier3;  // pickaxe_Tier3

        [Header("Tool 4: Drill Pike (Depth 50m)")]
        [SerializeField] private GameObject tool4_Base;   // Drill_Pike_Base
        [SerializeField] private GameObject tool4_Tier1;  // Drill_Pike_Tier1
        [SerializeField] private GameObject tool4_Tier2;  // Drill_Pike_Tier2
        [SerializeField] private GameObject tool4_Tier3;  // Drill_Pike_Tier3

        [Header("Tool 5: Sonic Pulser")]
        [SerializeField] private GameObject tool5_Base;   // Sonic_Pulser_Base
        [SerializeField] private GameObject tool5_Tier1;  // Sonic_Pulser_Tier1
        [SerializeField] private GameObject tool5_Tier2;  // Sonic_Pulser_Tier2
        [SerializeField] private GameObject tool5_Tier3;  // Sonic_Pulser_Tier3

        [Header("Tool 1 Prefab Paths")]
        [SerializeField] private string tool1_BasePath = "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Base_Shovel.prefab";
        [SerializeField] private string tool1_Tier1Path = "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier1_Shovel.prefab";
        [SerializeField] private string tool1_Tier2Path = "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier2_Shovel.prefab";
        [SerializeField] private string tool1_Tier3Path = "Assets/Art/Tools/Tool1/Base_tier1_tier2_tier3/Tier3_Shovel.prefab";

        [Header("Tool 2 Prefab Paths")]
        [SerializeField] private string tool2_BasePath = "Assets/Art/Tools/Tool2/Heavy_Spade_Base.prefab";
        [SerializeField] private string tool2_Tier1Path = "Assets/Art/Tools/Tool2/Heavy_Spade_Tier1.prefab";
        [SerializeField] private string tool2_Tier2Path = "Assets/Art/Tools/Tool2/Heavy_Spade_Tier2.prefab";
        [SerializeField] private string tool2_Tier3Path = "Assets/Art/Tools/Tool2/Heavy_Spade_Tier3.prefab";

        [Header("Tool 3 Prefab Paths")]
        [SerializeField] private string tool3_BasePath = "Assets/Art/Tools/Tool3/pickaxe_Base.prefab";
        [SerializeField] private string tool3_Tier1Path = "Assets/Art/Tools/Tool3/pickaxe_Tier1.prefab";
        [SerializeField] private string tool3_Tier2Path = "Assets/Art/Tools/Tool3/pickaxe_Tier2.prefab";
        [SerializeField] private string tool3_Tier3Path = "Assets/Art/Tools/Tool3/pickaxe_Tier3.prefab";

        [Header("Tool 4 Prefab Paths")]
        [SerializeField] private string tool4_BasePath = "Assets/Art/Tools/Drill Pike/Drill_Pike.prefab";
        [SerializeField] private string tool4_Tier1Path = "Assets/Art/Tools/Drill Pike/Drill_Pike.prefab";
        [SerializeField] private string tool4_Tier2Path = "Assets/Art/Tools/Drill Pike/Drill_Pike.prefab";
        [SerializeField] private string tool4_Tier3Path = "Assets/Art/Tools/Drill Pike/Drill_Pike.prefab";

        [Header("Tool 5 Prefab Paths")]
        [SerializeField] private string tool5_BasePath = "Assets/Art/Tools/Sonic_Pulser/Sonic_Pulser.prefab";
        [SerializeField] private string tool5_Tier1Path = "Assets/Art/Tools/Sonic_Pulser/Sonic_Pulser.prefab";
        [SerializeField] private string tool5_Tier2Path = "Assets/Art/Tools/Sonic_Pulser/Sonic_Pulser.prefab";
        [SerializeField] private string tool5_Tier3Path = "Assets/Art/Tools/Sonic_Pulser/Sonic_Pulser.prefab";

        // Debug input removed for release build

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Render On Top (Prevent Terrain Clipping)")]
        [Tooltip("Make tools render on top of everything to prevent terrain clipping")]
        [SerializeField] private bool renderToolsOnTop = true;
        [Tooltip("Use overlay camera system (requires 'HeldTool' layer). If false or layer missing, uses fallback.")]
        [SerializeField] private bool useOverlayCamera = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip equipSound;

        public static HeldToolController Instance { get; private set; }

        // Events
        public UnityAction<ToolData> OnToolEquipped;
        public UnityAction OnToolUnequipped;
        public UnityAction OnDigAnimationHit;

        private int currentToolIndex = 0;  // 0 = Tool 1 (Shovel), 1 = Tool 2 (Heavy Spade), 2 = Tool 3 (Pickaxe), 3 = Tool 4 (Drill Pike)
        private int currentTier = 1;       // 1-4 within current tool
        private ToolVisual currentToolVisual;
        private ToolData currentToolData;
        private bool toolsVisible = false; // Start with tools hidden

        public int CurrentToolIndex => currentToolIndex;
        public int CurrentTier => currentTier;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            // Subscribe to inventory events
            GameEvents.OnToolEquipped += OnInventoryToolEquipped;

            // Subscribe to upgrade events for visual swapping
            GameEvents.OnToolUpgraded += OnToolUpgraded;

            // Auto-find or instantiate tools (but keep them hidden)
            AutoSetupTools();

            // Check if we're loading from a save - restore tool state from PlayerPrefs
            int savedTier = PlayerPrefs.GetInt("PlayerToolTier", 0);
            int savedToolIndex = PlayerPrefs.GetInt("PlayerToolIndex", 0);
            bool savedToolEquipped = PlayerPrefs.GetInt("PlayerToolEquipped", 0) == 1;

            // Backward compatibility: old saves don't have PlayerToolIndex.
            // Infer tool index from RuntimeUpgrade_tool_tier if available.
            if (savedToolIndex == 0 && PlayerPrefs.HasKey("RuntimeUpgrade_tool_tier"))
            {
                int toolTierLevel = PlayerPrefs.GetInt("RuntimeUpgrade_tool_tier", 0);
                // tool_tier level 0=Tool1, 1=Tool2, 2=Tool3; Tool4 is picked up separately
                savedToolIndex = Mathf.Clamp(toolTierLevel, 0, 4);
            }

            // Check if Sonic Pulser was purchased (separate upgrade, not in tool_tier)
            if (PlayerPrefs.GetInt("RuntimeUpgrade_sonic_pulser", 0) >= 1)
            {
                savedToolIndex = 4; // Tool 5: Sonic Pulser
            }

            if (enableDebugLogs) Debug.Log($"[HeldToolController] Start() - PlayerPrefs: tool={savedToolIndex}, tier={savedTier}, equipped={savedToolEquipped}");

            if (savedTier > 0 && savedToolEquipped)
            {
                // Restoring from save - show the saved tool and tier
                toolsVisible = true;
                SetActiveToolAndTier(savedToolIndex, savedTier);
                if (enableDebugLogs) Debug.Log($"[HeldToolController] Restored from save - tool {savedToolIndex + 1}, tier {savedTier}");
            }
            else
            {
                // Start with NO tool visible - player gets tool after reading first note
                toolsVisible = false;
                HideAllToolObjects();
                if (enableDebugLogs) Debug.Log($"[HeldToolController] Started fresh - no tool visible (savedTier={savedTier}, savedEquipped={savedToolEquipped})");
            }
        }

        private void AutoSetupTools()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[HeldToolController] No Main Camera found!");
                return;
            }

            // Tool 1: Shovel - find existing or instantiate (Shovel animation)
            if (tool1_Base == null) tool1_Base = FindToolUnderCamera(mainCam.transform, "Base_Shovel", "Tool1_Base", ToolAnimationType.Shovel);
            if (tool1_Tier1 == null) tool1_Tier1 = FindToolUnderCamera(mainCam.transform, "Tier1_Shovel", "Tool1_Tier1", ToolAnimationType.Shovel);
            if (tool1_Tier2 == null) tool1_Tier2 = FindToolUnderCamera(mainCam.transform, "Tier2_Shovel", "Tool1_Tier2", ToolAnimationType.Shovel);
            if (tool1_Tier3 == null) tool1_Tier3 = FindToolUnderCamera(mainCam.transform, "Tier3_Shovel", "Tool1_Tier3", ToolAnimationType.Shovel);

            if (tool1_Base == null) tool1_Base = InstantiateToolFromPath(tool1_BasePath, mainCam.transform, "Tool1_Base", ToolAnimationType.Shovel);
            if (tool1_Tier1 == null) tool1_Tier1 = InstantiateToolFromPath(tool1_Tier1Path, mainCam.transform, "Tool1_Tier1", ToolAnimationType.Shovel);
            if (tool1_Tier2 == null) tool1_Tier2 = InstantiateToolFromPath(tool1_Tier2Path, mainCam.transform, "Tool1_Tier2", ToolAnimationType.Shovel);
            if (tool1_Tier3 == null) tool1_Tier3 = InstantiateToolFromPath(tool1_Tier3Path, mainCam.transform, "Tool1_Tier3", ToolAnimationType.Shovel);

            // Tool 2: Heavy Spade - find existing or instantiate (Hoe animation)
            if (tool2_Base == null) tool2_Base = FindToolUnderCamera(mainCam.transform, "Heavy_Spade_Base", "Tool2_Base", ToolAnimationType.Hoe);
            if (tool2_Tier1 == null) tool2_Tier1 = FindToolUnderCamera(mainCam.transform, "Heavy_Spade_Tier1", "Tool2_Tier1", ToolAnimationType.Hoe);
            if (tool2_Tier2 == null) tool2_Tier2 = FindToolUnderCamera(mainCam.transform, "Heavy_Spade_Tier2", "Tool2_Tier2", ToolAnimationType.Hoe);
            if (tool2_Tier3 == null) tool2_Tier3 = FindToolUnderCamera(mainCam.transform, "Heavy_Spade_Tier3", "Tool2_Tier3", ToolAnimationType.Hoe);

            if (tool2_Base == null) tool2_Base = InstantiateToolFromPath(tool2_BasePath, mainCam.transform, "Tool2_Base", ToolAnimationType.Hoe);
            if (tool2_Tier1 == null) tool2_Tier1 = InstantiateToolFromPath(tool2_Tier1Path, mainCam.transform, "Tool2_Tier1", ToolAnimationType.Hoe);
            if (tool2_Tier2 == null) tool2_Tier2 = InstantiateToolFromPath(tool2_Tier2Path, mainCam.transform, "Tool2_Tier2", ToolAnimationType.Hoe);
            if (tool2_Tier3 == null) tool2_Tier3 = InstantiateToolFromPath(tool2_Tier3Path, mainCam.transform, "Tool2_Tier3", ToolAnimationType.Hoe);

            // Tool 3: Pickaxe - find existing or instantiate (Pickaxe animation)
            if (tool3_Base == null) tool3_Base = FindToolUnderCamera(mainCam.transform, "pickaxe_Base", "Tool3_Base", ToolAnimationType.Pickaxe);
            if (tool3_Tier1 == null) tool3_Tier1 = FindToolUnderCamera(mainCam.transform, "pickaxe_Tier1", "Tool3_Tier1", ToolAnimationType.Pickaxe);
            if (tool3_Tier2 == null) tool3_Tier2 = FindToolUnderCamera(mainCam.transform, "pickaxe_Tier2", "Tool3_Tier2", ToolAnimationType.Pickaxe);
            if (tool3_Tier3 == null) tool3_Tier3 = FindToolUnderCamera(mainCam.transform, "pickaxe_Tier3", "Tool3_Tier3", ToolAnimationType.Pickaxe);

            if (tool3_Base == null) tool3_Base = InstantiateToolFromPath(tool3_BasePath, mainCam.transform, "Tool3_Base", ToolAnimationType.Pickaxe);
            if (tool3_Tier1 == null) tool3_Tier1 = InstantiateToolFromPath(tool3_Tier1Path, mainCam.transform, "Tool3_Tier1", ToolAnimationType.Pickaxe);
            if (tool3_Tier2 == null) tool3_Tier2 = InstantiateToolFromPath(tool3_Tier2Path, mainCam.transform, "Tool3_Tier2", ToolAnimationType.Pickaxe);
            if (tool3_Tier3 == null) tool3_Tier3 = InstantiateToolFromPath(tool3_Tier3Path, mainCam.transform, "Tool3_Tier3", ToolAnimationType.Pickaxe);

            // Tool 4: Drill Pike - find existing or instantiate (DrillPike animation with charged attack)
            if (tool4_Base == null) tool4_Base = FindToolUnderCamera(mainCam.transform, "Drill_Pike", "Tool4_Base", ToolAnimationType.DrillPike);
            if (tool4_Tier1 == null) tool4_Tier1 = FindToolUnderCamera(mainCam.transform, "Drill_Pike_Tier1", "Tool4_Tier1", ToolAnimationType.DrillPike);
            if (tool4_Tier2 == null) tool4_Tier2 = FindToolUnderCamera(mainCam.transform, "Drill_Pike_Tier2", "Tool4_Tier2", ToolAnimationType.DrillPike);
            if (tool4_Tier3 == null) tool4_Tier3 = FindToolUnderCamera(mainCam.transform, "Drill_Pike_Tier3", "Tool4_Tier3", ToolAnimationType.DrillPike);

            if (tool4_Base == null) tool4_Base = InstantiateToolFromPath(tool4_BasePath, mainCam.transform, "Tool4_Base", ToolAnimationType.DrillPike);
            if (tool4_Tier1 == null) tool4_Tier1 = InstantiateToolFromPath(tool4_Tier1Path, mainCam.transform, "Tool4_Tier1", ToolAnimationType.DrillPike);
            if (tool4_Tier2 == null) tool4_Tier2 = InstantiateToolFromPath(tool4_Tier2Path, mainCam.transform, "Tool4_Tier2", ToolAnimationType.DrillPike);
            if (tool4_Tier3 == null) tool4_Tier3 = InstantiateToolFromPath(tool4_Tier3Path, mainCam.transform, "Tool4_Tier3", ToolAnimationType.DrillPike);

            // Tool 5: Sonic Pulser - find existing or instantiate (SonicPulser animation - reuses DrillPike for now)
            if (tool5_Base == null) tool5_Base = FindToolUnderCamera(mainCam.transform, "Sonic_Pulser", "Tool5_Base", ToolAnimationType.SonicPulser);
            if (tool5_Tier1 == null) tool5_Tier1 = FindToolUnderCamera(mainCam.transform, "Sonic_Pulser_Tier1", "Tool5_Tier1", ToolAnimationType.SonicPulser);
            if (tool5_Tier2 == null) tool5_Tier2 = FindToolUnderCamera(mainCam.transform, "Sonic_Pulser_Tier2", "Tool5_Tier2", ToolAnimationType.SonicPulser);
            if (tool5_Tier3 == null) tool5_Tier3 = FindToolUnderCamera(mainCam.transform, "Sonic_Pulser_Tier3", "Tool5_Tier3", ToolAnimationType.SonicPulser);

            if (tool5_Base == null) tool5_Base = InstantiateToolFromPath(tool5_BasePath, mainCam.transform, "Tool5_Base", ToolAnimationType.SonicPulser);
            if (tool5_Tier1 == null) tool5_Tier1 = InstantiateToolFromPath(tool5_Tier1Path, mainCam.transform, "Tool5_Tier1", ToolAnimationType.SonicPulser);
            if (tool5_Tier2 == null) tool5_Tier2 = InstantiateToolFromPath(tool5_Tier2Path, mainCam.transform, "Tool5_Tier2", ToolAnimationType.SonicPulser);
            if (tool5_Tier3 == null) tool5_Tier3 = InstantiateToolFromPath(tool5_Tier3Path, mainCam.transform, "Tool5_Tier3", ToolAnimationType.SonicPulser);

            // Disable shadows on all tools (first-person items shouldn't cast shadows)
            DisableShadows(tool1_Base);
            DisableShadows(tool1_Tier1);
            DisableShadows(tool1_Tier2);
            DisableShadows(tool1_Tier3);
            DisableShadows(tool2_Base);
            DisableShadows(tool2_Tier1);
            DisableShadows(tool2_Tier2);
            DisableShadows(tool2_Tier3);
            DisableShadows(tool3_Base);
            DisableShadows(tool3_Tier1);
            DisableShadows(tool3_Tier2);
            DisableShadows(tool3_Tier3);
            DisableShadows(tool4_Base);
            DisableShadows(tool4_Tier1);
            DisableShadows(tool4_Tier2);
            DisableShadows(tool4_Tier3);
            DisableShadows(tool5_Base);
            DisableShadows(tool5_Tier1);
            DisableShadows(tool5_Tier2);
            DisableShadows(tool5_Tier3);

            // Ensure correct animation type for ALL tools (even if already assigned in Inspector)
            EnsureToolVisual(tool1_Base, ToolAnimationType.Shovel);
            EnsureToolVisual(tool1_Tier1, ToolAnimationType.Shovel);
            EnsureToolVisual(tool1_Tier2, ToolAnimationType.Shovel);
            EnsureToolVisual(tool1_Tier3, ToolAnimationType.Shovel);
            EnsureToolVisual(tool2_Base, ToolAnimationType.Hoe);
            EnsureToolVisual(tool2_Tier1, ToolAnimationType.Hoe);
            EnsureToolVisual(tool2_Tier2, ToolAnimationType.Hoe);
            EnsureToolVisual(tool2_Tier3, ToolAnimationType.Hoe);
            EnsureToolVisual(tool3_Base, ToolAnimationType.Pickaxe);
            EnsureToolVisual(tool3_Tier1, ToolAnimationType.Pickaxe);
            EnsureToolVisual(tool3_Tier2, ToolAnimationType.Pickaxe);
            EnsureToolVisual(tool3_Tier3, ToolAnimationType.Pickaxe);
            EnsureToolVisual(tool4_Base, ToolAnimationType.DrillPike);
            EnsureToolVisual(tool4_Tier1, ToolAnimationType.DrillPike);
            EnsureToolVisual(tool4_Tier2, ToolAnimationType.DrillPike);
            EnsureToolVisual(tool4_Tier3, ToolAnimationType.DrillPike);
            EnsureToolVisual(tool5_Base, ToolAnimationType.SonicPulser);
            EnsureToolVisual(tool5_Tier1, ToolAnimationType.SonicPulser);
            EnsureToolVisual(tool5_Tier2, ToolAnimationType.SonicPulser);
            EnsureToolVisual(tool5_Tier3, ToolAnimationType.SonicPulser);

            // Setup overlay camera for rendering tools on top
            if (renderToolsOnTop && useOverlayCamera)
            {
                SetupOverlayCameraSystem();
            }

            // Debug log status
            if (enableDebugLogs)
            {
                Debug.Log($"[HeldToolController] AutoSetupTools complete:");
                Debug.Log($"  Tool1: Base={tool1_Base != null}, T1={tool1_Tier1 != null}, T2={tool1_Tier2 != null}, T3={tool1_Tier3 != null}");
                Debug.Log($"  Tool2: Base={tool2_Base != null}, T1={tool2_Tier1 != null}, T2={tool2_Tier2 != null}, T3={tool2_Tier3 != null}");
                Debug.Log($"  Tool3: Base={tool3_Base != null}, T1={tool3_Tier1 != null}, T2={tool3_Tier2 != null}, T3={tool3_Tier3 != null}");
                Debug.Log($"  Tool4: Base={tool4_Base != null}, T1={tool4_Tier1 != null}, T2={tool4_Tier2 != null}, T3={tool4_Tier3 != null}");
                Debug.Log($"  Tool5: Base={tool5_Base != null}, T1={tool5_Tier1 != null}, T2={tool5_Tier2 != null}, T3={tool5_Tier3 != null}");
            }
        }

        private GameObject FindToolUnderCamera(Transform parent, string tierKeyword, string toolKeyword, ToolAnimationType animType = ToolAnimationType.Shovel)
        {
            foreach (Transform child in parent)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains(tierKeyword.ToLower()) || nameLower.Contains(toolKeyword.ToLower()))
                {
                    EnsureToolVisual(child.gameObject, animType);
                    return child.gameObject;
                }
                // Check nested children
                GameObject found = FindToolUnderCamera(child, tierKeyword, toolKeyword, animType);
                if (found != null) return found;
            }
            return null;
        }

        private GameObject InstantiateToolFromPath(string path, Transform parent, string name, ToolAnimationType animType = ToolAnimationType.Shovel)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, parent);
                instance.name = name;
                // Default position - user will reposition later
                instance.transform.localPosition = new Vector3(0.3f, -0.3f, 0.5f);
                instance.transform.localRotation = Quaternion.Euler(0, -90, 0);
                instance.transform.localScale = Vector3.one * 0.5f;

                // Ensure ToolVisual component exists with correct animation type
                EnsureToolVisual(instance, animType);
                return instance;
            }
#endif
            return null;
        }

        private void EnsureToolVisual(GameObject tool, ToolAnimationType animType = ToolAnimationType.Shovel)
        {
            if (tool == null) return;

            ToolVisual visual = tool.GetComponent<ToolVisual>();
            if (visual == null)
            {
                visual = tool.AddComponent<ToolVisual>();
            }

            // Set animation type based on tool
            visual.SetAnimationType(animType);

            // Disable shadows on first-person tools (they look unnatural)
            DisableShadows(tool);
        }

        /// <summary>
        /// Disable shadow casting on all renderers in a tool.
        /// First-person held items should not cast shadows.
        /// Also sets up materials to render on top if enabled (only as fallback when overlay camera isn't available).
        /// </summary>
        private void DisableShadows(GameObject tool)
        {
            if (tool == null) return;

            // Check if HeldTool layer exists - if so, we'll use overlay camera and don't need material hacks
            int heldToolLayer = LayerMask.NameToLayer("HeldTool");
            bool useOverlayCameraSystem = useOverlayCamera && heldToolLayer != -1;

            var renderers = tool.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Only use material render queue hack as fallback when overlay camera system isn't available
                if (renderToolsOnTop && !useOverlayCameraSystem)
                {
                    SetupRenderOnTop(renderer);
                }
            }
        }

        /// <summary>
        /// Configure a renderer's materials to render on top of everything.
        /// This is the fallback method when overlay camera isn't available.
        /// WARNING: This causes transparency-like artifacts and should only be used as last resort.
        /// </summary>
        private void SetupRenderOnTop(Renderer renderer)
        {
            if (renderer == null) return;

            foreach (var mat in renderer.materials)
            {
                // Set render queue to Overlay (4000+) to render after everything else
                mat.renderQueue = 4000;

                // Enable ZTest Always if the shader supports it
                // This makes the tool render regardless of depth buffer
                if (mat.HasProperty("_ZTest"))
                {
                    mat.SetFloat("_ZTest", 8f); // 8 = Always
                }

                // For URP shaders, try to set depth write off and test always
                if (mat.HasProperty("_ZWrite"))
                {
                    // Keep ZWrite on so tools depth-test against each other
                    mat.SetFloat("_ZWrite", 1f);
                }
            }
        }

        /// <summary>
        /// Reset materials to standard opaque rendering (undo SetupRenderOnTop modifications).
        /// Called when overlay camera system is properly set up.
        /// </summary>
        private void ResetMaterialsToOpaque(GameObject tool)
        {
            if (tool == null) return;

            var renderers = tool.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    // Reset render queue to default opaque (2000 = Geometry)
                    mat.renderQueue = 2000;

                    // Reset ZTest to LessEqual (default - 4)
                    if (mat.HasProperty("_ZTest"))
                    {
                        mat.SetFloat("_ZTest", 4f); // 4 = LessEqual (default)
                    }

                    // Ensure ZWrite is on
                    if (mat.HasProperty("_ZWrite"))
                    {
                        mat.SetFloat("_ZWrite", 1f);
                    }

                    // Reset surface type to Opaque if URP Lit shader
                    if (mat.HasProperty("_Surface"))
                    {
                        mat.SetFloat("_Surface", 0f); // 0 = Opaque
                    }
                }
            }
        }

        /// <summary>
        /// Set up the overlay camera system for proper tool rendering on top.
        /// </summary>
        private void SetupOverlayCameraSystem()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Get or create HeldTool layer
            int heldToolLayer = LayerMask.NameToLayer("HeldTool");
            if (heldToolLayer == -1)
            {
                Debug.LogWarning("[HeldToolController] Layer 'HeldTool' not found. Run 'Beneath The Floor > Setup > Create HeldTool Layer' to fix tool clipping.");
                return;
            }

            // Create overlay camera for URP
            Camera overlayCamera = CreateOrFindOverlayCamera(mainCam);
            if (overlayCamera == null) return;

            // Configure overlay camera
            overlayCamera.cullingMask = 1 << heldToolLayer;
            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.nearClipPlane = 0.01f;
            overlayCamera.farClipPlane = 50f;
            overlayCamera.fieldOfView = mainCam.fieldOfView;

            // Remove HeldTool layer from main camera
            mainCam.cullingMask &= ~(1 << heldToolLayer);

            // Setup URP overlay camera using reflection
            SetupURPOverlay(mainCam, overlayCamera);

            // Remove audio listener from overlay camera
            AudioListener listener = overlayCamera.GetComponent<AudioListener>();
            if (listener != null) Destroy(listener);

            // Assign all tools to HeldTool layer and reset their materials to opaque
            SetLayerRecursively(tool1_Base, heldToolLayer);
            SetLayerRecursively(tool1_Tier1, heldToolLayer);
            SetLayerRecursively(tool1_Tier2, heldToolLayer);
            SetLayerRecursively(tool1_Tier3, heldToolLayer);
            SetLayerRecursively(tool2_Base, heldToolLayer);
            SetLayerRecursively(tool2_Tier1, heldToolLayer);
            SetLayerRecursively(tool2_Tier2, heldToolLayer);
            SetLayerRecursively(tool2_Tier3, heldToolLayer);
            SetLayerRecursively(tool3_Base, heldToolLayer);
            SetLayerRecursively(tool3_Tier1, heldToolLayer);
            SetLayerRecursively(tool3_Tier2, heldToolLayer);
            SetLayerRecursively(tool3_Tier3, heldToolLayer);
            SetLayerRecursively(tool4_Base, heldToolLayer);
            SetLayerRecursively(tool4_Tier1, heldToolLayer);
            SetLayerRecursively(tool4_Tier2, heldToolLayer);
            SetLayerRecursively(tool4_Tier3, heldToolLayer);
            SetLayerRecursively(tool5_Base, heldToolLayer);
            SetLayerRecursively(tool5_Tier1, heldToolLayer);
            SetLayerRecursively(tool5_Tier2, heldToolLayer);
            SetLayerRecursively(tool5_Tier3, heldToolLayer);

            // Reset materials to proper opaque rendering (undo any previous material hacks)
            ResetMaterialsToOpaque(tool1_Base);
            ResetMaterialsToOpaque(tool1_Tier1);
            ResetMaterialsToOpaque(tool1_Tier2);
            ResetMaterialsToOpaque(tool1_Tier3);
            ResetMaterialsToOpaque(tool2_Base);
            ResetMaterialsToOpaque(tool2_Tier1);
            ResetMaterialsToOpaque(tool2_Tier2);
            ResetMaterialsToOpaque(tool2_Tier3);
            ResetMaterialsToOpaque(tool3_Base);
            ResetMaterialsToOpaque(tool3_Tier1);
            ResetMaterialsToOpaque(tool3_Tier2);
            ResetMaterialsToOpaque(tool3_Tier3);
            ResetMaterialsToOpaque(tool4_Base);
            ResetMaterialsToOpaque(tool4_Tier1);
            ResetMaterialsToOpaque(tool4_Tier2);
            ResetMaterialsToOpaque(tool4_Tier3);
            ResetMaterialsToOpaque(tool5_Base);
            ResetMaterialsToOpaque(tool5_Tier1);
            ResetMaterialsToOpaque(tool5_Tier2);
            ResetMaterialsToOpaque(tool5_Tier3);

            if (enableDebugLogs)
                Debug.Log($"[HeldToolController] Overlay camera system set up. Tools on layer {heldToolLayer}");
        }

        private Camera CreateOrFindOverlayCamera(Camera mainCam)
        {
            Transform existing = mainCam.transform.Find("HeldToolCamera");
            if (existing != null)
            {
                Camera cam = existing.GetComponent<Camera>();
                if (cam != null) return cam;
            }

            GameObject camObj = new GameObject("HeldToolCamera");
            camObj.transform.SetParent(mainCam.transform, false);
            camObj.transform.localPosition = Vector3.zero;
            camObj.transform.localRotation = Quaternion.identity;

            return camObj.AddComponent<Camera>();
        }

        private void SetupURPOverlay(Camera mainCam, Camera overlayCam)
        {
            // Get URP camera data via reflection to avoid assembly dependency
            var overlayData = overlayCam.GetComponent("UniversalAdditionalCameraData");
            if (overlayData == null)
            {
                System.Type urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (urpType != null)
                    overlayData = overlayCam.gameObject.AddComponent(urpType);
            }

            if (overlayData != null)
            {
                // Set render type to Overlay (enum value 1)
                var renderTypeProp = overlayData.GetType().GetProperty("renderType");
                if (renderTypeProp != null)
                    renderTypeProp.SetValue(overlayData, 1);
            }

            // Add to main camera stack
            var mainData = mainCam.GetComponent("UniversalAdditionalCameraData");
            if (mainData != null)
            {
                var stackProp = mainData.GetType().GetProperty("cameraStack");
                if (stackProp != null)
                {
                    var stack = stackProp.GetValue(mainData) as System.Collections.IList;
                    if (stack != null && !stack.Contains(overlayCam))
                        stack.Add(overlayCam);
                }
            }
        }

        /// <summary>
        /// Set tool to HeldTool layer so it renders on top of world geometry.
        /// </summary>
        private void SetHeldToolLayer(GameObject tool)
        {
            if (tool == null) return;

            int heldToolLayer = LayerMask.NameToLayer("HeldTool");
            if (heldToolLayer == -1)
            {
                // Layer doesn't exist yet - will be created by editor setup
                return;
            }

            SetLayerRecursively(tool, heldToolLayer);
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnToolEquipped -= OnInventoryToolEquipped;
            GameEvents.OnToolUpgraded -= OnToolUpgraded;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Debug keys disabled for release build
            // U key was: cycle tool tiers
            // Y key was: cycle tools
        }

        private void OnInventoryToolEquipped(ToolData tool)
        {
            EquipTool(tool);
        }

        private void OnToolUpgraded(ToolData upgradedTool)
        {
            // When tool is upgraded, swap to the new tier's visual
            if (upgradedTool != null)
            {
                // Update current tool data so animation speed is correct
                currentToolData = upgradedTool;
                if (enableDebugLogs) Debug.Log($"[HeldToolController] Tool upgraded to tier {upgradedTool.tier}, digSpeed {upgradedTool.digSpeed:F1}x, swapping visual");
                SetActiveTier(upgradedTool.tier);
            }
        }

        public void EquipTool(ToolData toolData)
        {
            currentToolData = toolData;

            // Make tool visible when equipped
            toolsVisible = true;

            SetActiveTier(toolData.tier);

            if (equipSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(equipSound);
            }

            OnToolEquipped?.Invoke(toolData);
        }

        /// <summary>
        /// Sets which tier tool is visible within the current tool.
        /// Only one tool variant is active at a time.
        /// Respects toolsVisible flag - if false, all tools stay hidden.
        /// </summary>
        public void SetActiveTier(int tier)
        {
            currentTier = Mathf.Clamp(tier, 1, 4);

            // Deactivate all tools from both tool sets
            HideAllToolObjects();

            // Only activate if tools are visible
            if (!toolsVisible)
            {
                if (enableDebugLogs) Debug.Log($"[HeldToolController] SetActiveTier({tier}) - tools not visible, skipping");
                return;
            }

            // Activate the correct tier for current tool
            GameObject activeTool = GetToolForCurrentToolAndTier();
            if (activeTool != null)
            {
                activeTool.SetActive(true);
                currentToolVisual = activeTool.GetComponent<ToolVisual>();

                // Set animation speed based on actual dig speed multiplier from upgrade station (speed * tier)
                float digSpeed = UpgradeStation.ToolSpeedMultiplier; // already includes tier
                if (digSpeed < 0.5f) digSpeed = currentToolData?.digSpeed ?? 1f; // Fallback
                currentToolVisual?.SetAnimationSpeed(digSpeed);

                if (enableDebugLogs) Debug.Log($"[HeldToolController] Activated: {activeTool.name} (Tool {currentToolIndex + 1}, Tier {currentTier}, AnimSpeed {digSpeed:F1}x)");
            }
            else
            {
                Debug.LogWarning($"[HeldToolController] No tool found for Tool {currentToolIndex + 1}, Tier {currentTier}");
            }
        }

        /// <summary>
        /// Switch to a different tool (0 = Shovel, 1 = Heavy Spade, 2 = Pickaxe, 3 = Drill Pike, 4 = Sonic Pulser).
        /// Resets tier to 1 (base) for the new tool.
        /// </summary>
        public void SetActiveTool(int toolIndex)
        {
            currentToolIndex = Mathf.Clamp(toolIndex, 0, 4);
            currentTier = 1; // Reset to base tier when switching tools

            if (enableDebugLogs) Debug.Log($"[HeldToolController] Switched to Tool {currentToolIndex + 1}, Tier {currentTier}");

            // Deactivate all and show new tool
            HideAllToolObjects();

            if (toolsVisible)
            {
                GameObject activeTool = GetToolForCurrentToolAndTier();
                if (activeTool != null)
                {
                    activeTool.SetActive(true);
                    currentToolVisual = activeTool.GetComponent<ToolVisual>();

                    // Set animation speed based on actual dig speed multiplier (speed * tier)
                    float digSpeed = UpgradeStation.ToolSpeedMultiplier; // already includes tier
                    if (digSpeed < 0.5f) digSpeed = currentToolData?.digSpeed ?? 1f;
                    currentToolVisual?.SetAnimationSpeed(digSpeed);
                }
            }
        }

        /// <summary>
        /// Set both tool index and tier at once.
        /// </summary>
        public void SetActiveToolAndTier(int toolIndex, int tier)
        {
            currentToolIndex = Mathf.Clamp(toolIndex, 0, 4);
            currentTier = Mathf.Clamp(tier, 1, 4);

            if (enableDebugLogs) Debug.Log($"[HeldToolController] Set to Tool {currentToolIndex + 1}, Tier {currentTier}");

            HideAllToolObjects();

            if (toolsVisible)
            {
                GameObject activeTool = GetToolForCurrentToolAndTier();
                if (activeTool != null)
                {
                    activeTool.SetActive(true);
                    currentToolVisual = activeTool.GetComponent<ToolVisual>();

                    // Set animation speed based on actual dig speed multiplier (speed * tier)
                    float digSpeed = UpgradeStation.ToolSpeedMultiplier; // already includes tier
                    if (digSpeed < 0.5f) digSpeed = currentToolData?.digSpeed ?? 1f;
                    currentToolVisual?.SetAnimationSpeed(digSpeed);
                }
            }
        }

        /// <summary>
        /// Hide all tool GameObjects (all tools, all tiers).
        /// </summary>
        private void HideAllToolObjects()
        {
            // Tool 1: Shovel
            if (tool1_Base != null) tool1_Base.SetActive(false);
            if (tool1_Tier1 != null) tool1_Tier1.SetActive(false);
            if (tool1_Tier2 != null) tool1_Tier2.SetActive(false);
            if (tool1_Tier3 != null) tool1_Tier3.SetActive(false);

            // Tool 2: Heavy Spade
            if (tool2_Base != null) tool2_Base.SetActive(false);
            if (tool2_Tier1 != null) tool2_Tier1.SetActive(false);
            if (tool2_Tier2 != null) tool2_Tier2.SetActive(false);
            if (tool2_Tier3 != null) tool2_Tier3.SetActive(false);

            // Tool 3: Pickaxe
            if (tool3_Base != null) tool3_Base.SetActive(false);
            if (tool3_Tier1 != null) tool3_Tier1.SetActive(false);
            if (tool3_Tier2 != null) tool3_Tier2.SetActive(false);
            if (tool3_Tier3 != null) tool3_Tier3.SetActive(false);

            // Tool 4: Drill Pike
            if (tool4_Base != null) tool4_Base.SetActive(false);
            if (tool4_Tier1 != null) tool4_Tier1.SetActive(false);
            if (tool4_Tier2 != null) tool4_Tier2.SetActive(false);
            if (tool4_Tier3 != null) tool4_Tier3.SetActive(false);

            // Tool 5: Sonic Pulser
            if (tool5_Base != null) tool5_Base.SetActive(false);
            if (tool5_Tier1 != null) tool5_Tier1.SetActive(false);
            if (tool5_Tier2 != null) tool5_Tier2.SetActive(false);
            if (tool5_Tier3 != null) tool5_Tier3.SetActive(false);
        }

        /// <summary>
        /// Hide all held tools (player has no tool in hand).
        /// </summary>
        public void HideAllTools()
        {
            toolsVisible = false;
            HideAllToolObjects();
            currentToolVisual = null;
        }

        /// <summary>
        /// Show the current tier tool (after player picks up a tool).
        /// </summary>
        public void ShowCurrentTool()
        {
            toolsVisible = true;
            SetActiveTier(currentTier);
        }

        /// <summary>
        /// Give the player their first tool (called when first note is opened).
        /// Shows Tool 1 (Shovel) at Base tier.
        /// </summary>
        public void GiveFirstTool()
        {
            if (toolsVisible)
            {
                if (enableDebugLogs) Debug.Log("[HeldToolController] Player already has a tool");
                return;
            }

            if (enableDebugLogs) Debug.Log("[HeldToolController] Giving player their first tool (Shovel Base)");

            // Set to Tool 1, Tier 1 (Base Shovel)
            currentToolIndex = 0;
            currentTier = 1;
            toolsVisible = true;

            // Show the tool
            SetActiveTier(1);

            // Also add to inventory if needed
            var inventory = Inventory.InventorySystem.Instance;
            if (inventory != null && inventory.CurrentTool == null)
            {
                ToolData woodenShovel = new ToolData
                {
                    toolName = "Wooden Shovel",
                    tier = 1,
                    digSpeed = 1f,
                    durability = 100,
                    maxDurability = 100,
                    maxDepth = 15
                };
                inventory.AddTool(woodenShovel);
            }

            // Play equip sound
            if (equipSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(equipSound);
            }
        }

        /// <summary>
        /// Check if player has received their first tool yet.
        /// </summary>
        public bool HasReceivedFirstTool() => toolsVisible;

        /// <summary>
        /// Check if tools are currently visible.
        /// </summary>
        public bool AreToolsVisible() => toolsVisible;

        /// <summary>
        /// Get the tool GameObject for the current tool index and tier.
        /// </summary>
        private GameObject GetToolForCurrentToolAndTier()
        {
            if (currentToolIndex == 0) // Tool 1: Shovel
            {
                return currentTier switch
                {
                    1 => tool1_Base,
                    2 => tool1_Tier1,
                    3 => tool1_Tier2,
                    4 => tool1_Tier3,
                    _ => tool1_Base
                };
            }
            else if (currentToolIndex == 1) // Tool 2: Heavy Spade
            {
                return currentTier switch
                {
                    1 => tool2_Base,
                    2 => tool2_Tier1,
                    3 => tool2_Tier2,
                    4 => tool2_Tier3,
                    _ => tool2_Base
                };
            }
            else if (currentToolIndex == 2) // Tool 3: Pickaxe
            {
                return currentTier switch
                {
                    1 => tool3_Base,
                    2 => tool3_Tier1,
                    3 => tool3_Tier2,
                    4 => tool3_Tier3,
                    _ => tool3_Base
                };
            }
            else if (currentToolIndex == 3) // Tool 4: Drill Pike
            {
                return currentTier switch
                {
                    1 => tool4_Base,
                    2 => tool4_Tier1,
                    3 => tool4_Tier2,
                    4 => tool4_Tier3,
                    _ => tool4_Base
                };
            }
            else // Tool 5: Sonic Pulser
            {
                return currentTier switch
                {
                    1 => tool5_Base,
                    2 => tool5_Tier1,
                    3 => tool5_Tier2,
                    4 => tool5_Tier3,
                    _ => tool5_Base
                };
            }
        }

        public void StartDigging()
        {
            if (currentToolVisual != null)
            {
                currentToolVisual.PlayDigAnimation();
            }
        }

        public void StopDigging()
        {
            currentToolVisual?.PlayIdleAnimation();
        }

        public void TriggerDigHit()
        {
            OnDigAnimationHit?.Invoke();
        }

        /// <summary>
        /// Call this to play the dig animation (e.g., from DiggingSystem).
        /// </summary>
        public void PlayDigAnimation()
        {
            StartDigging();
        }

        public ToolData GetCurrentTool() => currentToolData;
        public bool HasToolEquipped() => currentToolVisual != null;
        public int GetCurrentTier() => currentTier;
        public int GetCurrentToolIndex() => currentToolIndex;
        public float GetDigSpeedModifier() => currentToolData?.digSpeed ?? 1f;
        public ToolVisual GetCurrentToolVisual() => currentToolVisual;

        // ============================================================
        // CHARGED ATTACK METHODS (for Drill Pike)
        // ============================================================

        /// <summary>
        /// Start charging the tool (for Drill Pike super attack).
        /// </summary>
        public void StartCharging()
        {
            currentToolVisual?.StartCharging();
        }

        /// <summary>
        /// Release the charged attack.
        /// </summary>
        public void ReleaseChargedAttack()
        {
            currentToolVisual?.ReleaseChargedAttack();
        }

        /// <summary>
        /// Cancel charging without attacking.
        /// </summary>
        public void CancelCharging()
        {
            currentToolVisual?.CancelCharging();
        }

        /// <summary>
        /// Check if currently charging.
        /// </summary>
        public bool IsCharging()
        {
            return currentToolVisual != null && currentToolVisual.IsCharging();
        }

        /// <summary>
        /// Get current charge progress (0 to 1).
        /// </summary>
        public float GetChargeProgress()
        {
            return currentToolVisual?.GetChargeProgress() ?? 0f;
        }

        /// <summary>
        /// Check if current tool supports charging (Drill Pike).
        /// </summary>
        public bool SupportsCharging()
        {
            return currentToolIndex == 3 || currentToolIndex == 4; // Tool 4: Drill Pike, Tool 5: Sonic Pulser
        }
    }

    [System.Serializable]
    public class ToolVisualData
    {
        public string toolName;
        public int tier;
        public GameObject prefab;
        public bool providesLight;
        public float lightRange = 5f;
        public Color lightColor = Color.yellow;
    }
}
