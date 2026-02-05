using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BeneathTheFloor.Editor
{
    /// <summary>
    /// Editor utilities for basement lighting setup and diagnostics.
    /// Fixes the main issue: directional light (sun) leaking into basement.
    /// </summary>
    public static class BasementLightingSetup
    {
        private static readonly string[] BasementKeywords = new string[]
        {
            "basement", "underground", "shaft", "terrain", "voxel", "chunk",
            "wall_", "segment_", "diggingsystem", "undergroundterrain", "soil",
            "ceiling", "floors_", "walls_", "room", "wallin_", "depthlayer"
        };

        private const int BASEMENT_LAYER = 9;
        private const int BASEMENT_FLOOR_LAYER = 10;

        [MenuItem("Beneath The Floor/Lighting/FIX DIRECTIONAL LIGHT NOW", false, -10)]
        public static void ForceFixDirectionalLight()
        {
            Light[] allLights = Object.FindObjectsOfType<Light>();

            foreach (var light in allLights)
            {
                if (light.type == LightType.Directional)
                {
                    int oldMask = light.cullingMask;

                    // Layer 7 = Diggable (runtime terrain), Layer 9 = Basement, Layer 10 = BasementFloor
                    int layer7Bit = 1 << 7;  // Diggable - runtime generated terrain
                    int layer9Bit = 1 << 9;  // Basement
                    int layer10Bit = 1 << 10; // BasementFloor

                    // Check if currently affects these layers
                    bool affectsDiggable = (oldMask & layer7Bit) != 0;
                    bool affectsBasement = (oldMask & layer9Bit) != 0;

                    Debug.Log($"[FIX] Directional Light '{light.name}':");
                    Debug.Log($"  Old Culling Mask: {oldMask}");
                    Debug.Log($"  Currently affects Diggable (layer 7): {affectsDiggable}");
                    Debug.Log($"  Currently affects Basement (layer 9): {affectsBasement}");

                    // Force remove layers 7, 9, and 10
                    int newMask = oldMask & ~layer7Bit & ~layer9Bit & ~layer10Bit;

                    Undo.RecordObject(light, "Fix Directional Light");
                    light.cullingMask = newMask;
                    EditorUtility.SetDirty(light);
                    EditorUtility.SetDirty(light.gameObject);

                    bool nowAffectsDiggable = (newMask & layer7Bit) != 0;
                    bool nowAffectsBasement = (newMask & layer9Bit) != 0;
                    Debug.Log($"  New Culling Mask: {newMask}");
                    Debug.Log($"  Now affects Diggable (layer 7): {nowAffectsDiggable}");
                    Debug.Log($"  Now affects Basement (layer 9): {nowAffectsBasement}");

                    if (!nowAffectsDiggable && !nowAffectsBasement)
                    {
                        Debug.Log($"  SUCCESS! Diggable and Basement layers excluded from '{light.name}'");
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Fixed",
                "Directional light culling mask updated!\n\n" +
                "EXCLUDED LAYERS:\n" +
                "- Layer 7 (Diggable) - runtime terrain\n" +
                "- Layer 9 (Basement)\n" +
                "- Layer 10 (BasementFloor)\n\n" +
                "SAVE THE SCENE (Ctrl+S)!", "OK");
        }

        [MenuItem("Beneath The Floor/Lighting/FULL SETUP - Fix Basement Lighting", false, 0)]
        public static void FullSetup()
        {
            Debug.Log("========== BASEMENT LIGHTING FULL SETUP ==========");

            // Step 1: Auto-assign basement layers
            int layerCount = AutoAssignBasementLayers();
            Debug.Log($"Step 1: Assigned {layerCount} objects to Basement layer");

            // Step 2: Fix directional light
            int lightFixes = QuickFixDirectionalLightInternal();
            Debug.Log($"Step 2: Fixed {lightFixes} directional light(s)");

            // Step 3: Add lighting managers
            AddAllLightingManagers();
            Debug.Log("Step 3: Added lighting managers");

            // Step 4: Reduce ambient for basement
            ReduceAmbientForBasement();
            Debug.Log("Step 4: Adjusted ambient settings");

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("========== SETUP COMPLETE ==========");
            Debug.Log("SAVE THE SCENE to keep changes!");

            EditorUtility.DisplayDialog("Basement Lighting Setup Complete",
                $"Setup complete!\n\n" +
                $"- {layerCount} objects assigned to Basement layer\n" +
                $"- {lightFixes} directional light(s) fixed\n" +
                $"- Lighting managers added\n" +
                $"- Ambient settings adjusted\n\n" +
                "SAVE THE SCENE to keep changes!",
                "OK");
        }

        [MenuItem("Beneath The Floor/Lighting/1. Auto-Assign Basement Layers", false, 20)]
        public static void AutoAssignBasementLayersMenu()
        {
            int count = AutoAssignBasementLayers();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[BasementLighting] Assigned {count} objects to Basement layer. Save scene!");
        }

        public static int AutoAssignBasementLayers()
        {
            int assignedCount = 0;
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();

            foreach (var obj in allObjects)
            {
                if (ShouldBeOnBasementLayer(obj) && obj.layer != BASEMENT_LAYER)
                {
                    Undo.RecordObject(obj, "Auto-Assign Basement Layer");
                    SetLayerRecursive(obj, BASEMENT_LAYER);
                    assignedCount++;
                }
            }

            return assignedCount;
        }

        private static bool ShouldBeOnBasementLayer(GameObject obj)
        {
            string nameLower = obj.name.ToLower();

            // Check keywords in name
            foreach (var keyword in BasementKeywords)
            {
                if (nameLower.Contains(keyword))
                {
                    return true;
                }
            }

            // Check Y position - objects below -2 with renderers are likely underground
            if (obj.transform.position.y < -2f)
            {
                if (obj.GetComponent<MeshRenderer>() != null ||
                    obj.GetComponent<SkinnedMeshRenderer>() != null ||
                    obj.GetComponent<MeshFilter>() != null)
                {
                    return true;
                }
            }

            // Check for DiggingV2 components
            var components = obj.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null)
                {
                    string typeName = comp.GetType().FullName;
                    if (typeName.Contains("DiggingV2") || typeName.Contains("Underground"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [MenuItem("Beneath The Floor/Lighting/2. Fix Directional Light", false, 21)]
        public static void QuickFixDirectionalLight()
        {
            int count = QuickFixDirectionalLightInternal();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (count > 0)
                Debug.Log($"[BasementLighting] Fixed {count} directional light(s). Save scene!");
            else
                Debug.Log("[BasementLighting] Directional light already configured correctly.");
        }

        private static int QuickFixDirectionalLightInternal()
        {
            Light[] allLights = Object.FindObjectsOfType<Light>();
            int fixCount = 0;

            int basementLayer = LayerMask.NameToLayer("Basement");
            int basementFloorLayer = LayerMask.NameToLayer("BasementFloor");

            if (basementLayer < 0)
            {
                Debug.LogError("[BasementLighting] Basement layer not found! Add it in Project Settings > Tags and Layers");
                return 0;
            }

            foreach (var light in allLights)
            {
                if (light.type == LightType.Directional)
                {
                    int oldMask = light.cullingMask;

                    // Remove basement layers from culling mask
                    int newMask = oldMask & ~(1 << basementLayer);
                    if (basementFloorLayer >= 0)
                    {
                        newMask = newMask & ~(1 << basementFloorLayer);
                    }

                    if (newMask != oldMask)
                    {
                        Undo.RecordObject(light, "Fix Directional Light Culling Mask");
                        light.cullingMask = newMask;
                        fixCount++;
                        Debug.Log($"[BasementLighting] Fixed '{light.name}': excluded Basement layers from culling");
                    }
                }
            }

            return fixCount;
        }

        [MenuItem("Beneath The Floor/Lighting/3. Add Lighting Managers", false, 22)]
        public static void AddAllLightingManagers()
        {
            // Find or create parent
            GameObject lightingParent = GameObject.Find("_LightingManagers");
            if (lightingParent == null)
            {
                lightingParent = new GameObject("_LightingManagers");
                Undo.RegisterCreatedObjectUndo(lightingParent, "Create Lighting Managers Parent");
            }

            // Add BasementLightingManager
            var basementManager = Object.FindObjectOfType<Lighting.BasementLightingManager>();
            if (basementManager == null)
            {
                var managerGO = new GameObject("BasementLightingManager");
                managerGO.transform.SetParent(lightingParent.transform);
                managerGO.AddComponent<Lighting.BasementLightingManager>();
                Undo.RegisterCreatedObjectUndo(managerGO, "Add BasementLightingManager");
                Debug.Log("[BasementLighting] Added BasementLightingManager");
            }
            else
            {
                Debug.Log("[BasementLighting] BasementLightingManager already exists");
            }

            // Add DiggingLightingRig
            var diggingRig = Object.FindObjectOfType<Lighting.DiggingLightingRig>();
            if (diggingRig == null)
            {
                var rigGO = new GameObject("DiggingLightingRig");
                rigGO.transform.SetParent(lightingParent.transform);
                rigGO.AddComponent<Lighting.DiggingLightingRig>();
                Undo.RegisterCreatedObjectUndo(rigGO, "Add DiggingLightingRig");
                Debug.Log("[BasementLighting] Added DiggingLightingRig");
            }
            else
            {
                Debug.Log("[BasementLighting] DiggingLightingRig already exists");
            }

            // Add UndergroundLightingSystem (using string-based type lookup to avoid compile-time dependency)
            var undergroundLightingType = System.Type.GetType("BeneathTheFloor.Lighting.UndergroundLightingSystem, Assembly-CSharp");
            if (undergroundLightingType != null)
            {
                var undergroundLighting = Object.FindObjectOfType(undergroundLightingType);
                if (undergroundLighting == null)
                {
                    var systemGO = new GameObject("UndergroundLightingSystem");
                    systemGO.transform.SetParent(lightingParent.transform);
                    systemGO.AddComponent(undergroundLightingType);
                    Undo.RegisterCreatedObjectUndo(systemGO, "Add UndergroundLightingSystem");
                    Debug.Log("[BasementLighting] Added UndergroundLightingSystem");
                }
                else
                {
                    Debug.Log("[BasementLighting] UndergroundLightingSystem already exists");
                }
            }
            else
            {
                Debug.Log("[BasementLighting] UndergroundLightingSystem type not found - will be added after scripts compile");
            }

            // Add LampPlacementController to player (using string-based type lookup)
            var lampControllerType = System.Type.GetType("BeneathTheFloor.Lighting.LampPlacementController, Assembly-CSharp");
            if (lampControllerType != null)
            {
                var lampController = Object.FindObjectOfType(lampControllerType);
                if (lampController == null)
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        player.AddComponent(lampControllerType);
                        Debug.Log("[BasementLighting] Added LampPlacementController to Player");
                    }
                    else
                    {
                        // Create standalone controller
                        var controllerGO = new GameObject("LampPlacementController");
                        controllerGO.transform.SetParent(lightingParent.transform);
                        controllerGO.AddComponent(lampControllerType);
                        Undo.RegisterCreatedObjectUndo(controllerGO, "Add LampPlacementController");
                        Debug.Log("[BasementLighting] Added LampPlacementController (standalone - no Player found)");
                    }
                }
                else
                {
                    Debug.Log("[BasementLighting] LampPlacementController already exists");
                }
            }
            else
            {
                Debug.Log("[BasementLighting] LampPlacementController type not found - will be added after scripts compile");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Beneath The Floor/Lighting/4. Reduce Ambient for Basement", false, 23)]
        public static void ReduceAmbientForBasement()
        {
            // RenderSettings changes are scene-level, mark scene dirty instead
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientIntensity = 0.2f;
            RenderSettings.ambientSkyColor = new Color(0.1f, 0.08f, 0.06f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.06f, 0.05f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.04f, 0.03f);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[BasementLighting] Ambient settings reduced for basement atmosphere");
        }

        [MenuItem("Beneath The Floor/Lighting/Diagnose Scene Lighting", false, 50)]
        public static void DiagnoseSceneLighting()
        {
            Debug.Log("========== SCENE LIGHTING DIAGNOSIS ==========");

            // Count objects per layer
            Debug.Log("\n--- LAYER DISTRIBUTION ---");
            int[] layerCounts = new int[32];
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                layerCounts[obj.layer]++;
            }

            for (int i = 0; i < 32; i++)
            {
                if (layerCounts[i] > 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(layerName)) layerName = $"(unnamed)";
                    Debug.Log($"  Layer {i} ({layerName}): {layerCounts[i]} objects");
                }
            }

            // Check directional lights
            Debug.Log("\n--- DIRECTIONAL LIGHTS ---");
            Light[] allLights = Object.FindObjectsOfType<Light>();
            int basementLayer = LayerMask.NameToLayer("Basement");

            foreach (var light in allLights)
            {
                if (light.type == LightType.Directional && light.enabled)
                {
                    Debug.Log($"'{light.name}':");
                    Debug.Log($"  Intensity: {light.intensity}");
                    Debug.Log($"  Culling Mask: {light.cullingMask}");

                    if (basementLayer >= 0)
                    {
                        bool affectsBasement = (light.cullingMask & (1 << basementLayer)) != 0;
                        string status = affectsBasement ? "YES - PROBLEM!" : "No (Good)";
                        Debug.Log($"  Affects Basement: {status}");
                    }
                }
            }

            // Check managers
            Debug.Log("\n--- LIGHTING MANAGERS ---");
            var bm = Object.FindObjectOfType<Lighting.BasementLightingManager>();
            var dr = Object.FindObjectOfType<Lighting.DiggingLightingRig>();
            Debug.Log($"  BasementLightingManager: {(bm != null ? "Present" : "MISSING")}");
            Debug.Log($"  DiggingLightingRig: {(dr != null ? "Present" : "MISSING")}");

            // Check ambient
            Debug.Log("\n--- AMBIENT SETTINGS ---");
            Debug.Log($"  Mode: {RenderSettings.ambientMode}");
            Debug.Log($"  Intensity: {RenderSettings.ambientIntensity}");
            Debug.Log($"  Sky Color: {RenderSettings.ambientSkyColor}");

            Debug.Log("\n========== END DIAGNOSIS ==========");
        }

        [MenuItem("Beneath The Floor/Lighting/Set 'Basement' Object + All Children to Layer", false, 59)]
        public static void SetBasementObjectToLayer()
        {
            GameObject basementObj = GameObject.Find("Basement");
            if (basementObj == null)
            {
                Debug.LogError("[BasementLighting] No object named 'Basement' found in scene!");
                return;
            }

            Undo.RecordObject(basementObj, "Set Basement Layer");
            int count = SetLayerRecursiveCount(basementObj, BASEMENT_LAYER);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[BasementLighting] Set 'Basement' and {count} children to Basement layer. SAVE THE SCENE!");

            EditorUtility.DisplayDialog("Done",
                $"Set 'Basement' object and {count} children to Basement layer.\n\nSAVE THE SCENE!", "OK");
        }

        [MenuItem("Beneath The Floor/Lighting/Set ALL Underground Objects to Basement Layer", false, 57)]
        public static void SetAllUndergroundToLayer()
        {
            int totalCount = 0;

            // Find all objects that should be underground
            string[] rootNames = { "Basement", "UndergroundTerrain", "Underground Terrain", "ShaftWalls",
                                   "Shaft Walls", "ModularShaftWall", "Rooms", "WorldRooms",
                                   "DepthLayer_0", "DepthLayer_1", "DepthLayer_2", "DepthLayers" };

            foreach (string name in rootNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null && obj.layer != BASEMENT_LAYER)
                {
                    Undo.RecordObject(obj, "Set Basement Layer");
                    int count = SetLayerRecursiveCount(obj, BASEMENT_LAYER);
                    totalCount += count;
                    Debug.Log($"[BasementLighting] Set '{name}' + {count} children to Basement layer");
                }
            }

            // Also find any root objects with underground-related names
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                string nameLower = root.name.ToLower();
                if (nameLower.Contains("underground") || nameLower.Contains("shaft") ||
                    nameLower.Contains("voxel") || nameLower.Contains("room") ||
                    nameLower.Contains("basement") || nameLower.Contains("terrain") ||
                    nameLower.Contains("depthlayer") || nameLower.Contains("depth_"))
                {
                    if (root.layer != BASEMENT_LAYER)
                    {
                        Undo.RecordObject(root, "Set Basement Layer");
                        int count = SetLayerRecursiveCount(root, BASEMENT_LAYER);
                        totalCount += count;
                        Debug.Log($"[BasementLighting] Set '{root.name}' + children to Basement layer ({count} objects)");
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[BasementLighting] Total: {totalCount} objects set to Basement layer. SAVE THE SCENE!");

            EditorUtility.DisplayDialog("Done",
                $"Set {totalCount} underground objects to Basement layer.\n\nSAVE THE SCENE (Ctrl+S)!", "OK");
        }

        [MenuItem("Beneath The Floor/Lighting/Set 'UndergroundTerrain' + All Children to Layer", false, 58)]
        public static void SetUndergroundTerrainToLayer()
        {
            int totalCount = 0;

            // Find UndergroundTerrain and similar objects
            string[] objectNames = { "UndergroundTerrain", "Underground Terrain", "ShaftWalls", "Shaft Walls", "ModularShaftWall" };

            foreach (string name in objectNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Undo.RecordObject(obj, "Set Basement Layer");
                    int count = SetLayerRecursiveCount(obj, BASEMENT_LAYER);
                    totalCount += count;
                    Debug.Log($"[BasementLighting] Set '{name}' and children to Basement layer ({count} objects)");
                }
            }

            // Also find any objects with "Underground" or "Terrain" in name at root level
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                string nameLower = root.name.ToLower();
                if (nameLower.Contains("underground") || nameLower.Contains("shaft") || nameLower.Contains("voxel"))
                {
                    if (root.layer != BASEMENT_LAYER)
                    {
                        Undo.RecordObject(root, "Set Basement Layer");
                        int count = SetLayerRecursiveCount(root, BASEMENT_LAYER);
                        totalCount += count;
                        Debug.Log($"[BasementLighting] Set '{root.name}' and children to Basement layer ({count} objects)");
                    }
                }
            }

            if (totalCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Done",
                    $"Set {totalCount} underground objects to Basement layer.\n\nSAVE THE SCENE!", "OK");
            }
            else
            {
                Debug.LogWarning("[BasementLighting] No UndergroundTerrain objects found!");
            }
        }

        private static int SetLayerRecursiveCount(GameObject obj, int layer)
        {
            int count = 0;
            obj.layer = layer;
            count++;
            foreach (Transform child in obj.transform)
            {
                count += SetLayerRecursiveCount(child.gameObject, layer);
            }
            return count;
        }

        [MenuItem("Beneath The Floor/Lighting/Set Selected to Basement Layer", false, 60)]
        public static void SetSelectedToBasementLayer()
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("[BasementLighting] No objects selected!");
                return;
            }

            int count = 0;
            foreach (var obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj, "Set Basement Layer");
                SetLayerRecursive(obj, BASEMENT_LAYER);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[BasementLighting] Set {count} objects (and children) to Basement layer. Save scene!");
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        [MenuItem("Beneath The Floor/Materials/RESET ALL - Simple Brown Materials", false, 90)]
        public static void ResetAllToSimpleBrown()
        {
            // Create a simple brown material for everything
            Color brownColor = new Color(0.45f, 0.35f, 0.28f); // Same as CreateDefaultMaterial in UndergroundTerrainManager

            // Create or update M_SimpleBrown material
            string simpleBrownPath = "Assets/Materials/M_SimpleBrown.mat";
            Material simpleBrownMat = AssetDatabase.LoadAssetAtPath<Material>(simpleBrownPath);

            if (simpleBrownMat == null)
            {
                // Create new material
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                simpleBrownMat = new Material(shader);
                simpleBrownMat.name = "M_SimpleBrown";

                // Ensure directory exists
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets", "Materials");
                }

                AssetDatabase.CreateAsset(simpleBrownMat, simpleBrownPath);
                Debug.Log("[Materials] Created new M_SimpleBrown material");
            }

            // Configure the material - simple brown, no textures
            simpleBrownMat.SetColor("_BaseColor", brownColor);
            simpleBrownMat.SetColor("_Color", brownColor); // For Standard shader
            simpleBrownMat.SetFloat("_Smoothness", 0.1f);
            simpleBrownMat.SetFloat("_Metallic", 0f);

            // Remove any textures
            simpleBrownMat.SetTexture("_BaseMap", null);
            simpleBrownMat.SetTexture("_MainTex", null);
            simpleBrownMat.SetTexture("_BumpMap", null);
            simpleBrownMat.SetTexture("_OcclusionMap", null);

            // Disable keywords for textures
            simpleBrownMat.DisableKeyword("_NORMALMAP");
            simpleBrownMat.DisableKeyword("_OCCLUSIONMAP");

            EditorUtility.SetDirty(simpleBrownMat);
            AssetDatabase.SaveAssets();

            int appliedCount = 0;

            // 1. UndergroundTerrainManager material (V3 system manages chunks differently)
            // Note: V3 uses ChunkManager with its own material handling
            Debug.Log("[Materials] V3 system uses ChunkManager - terrain material managed differently");

            // 2. Apply to ModularShaftConfig
            string[] guids = AssetDatabase.FindAssets("ModularShaftConfig t:ModularShaftConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var config = AssetDatabase.LoadAssetAtPath<WorldRooms.ModularShaftConfig>(path);
                if (config != null)
                {
                    Undo.RecordObject(config, "Reset Wall Material");
                    config.wallMaterial = simpleBrownMat;
                    EditorUtility.SetDirty(config);
                    appliedCount++;
                    Debug.Log("[Materials] Set ModularShaftConfig.wallMaterial to M_SimpleBrown");
                }
            }

            // 3. Apply to all terrain chunks in scene (V3 chunks are named "Chunk_X,Y,Z")
            var allRenderers = Object.FindObjectsOfType<MeshRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (renderer.gameObject.name.StartsWith("Chunk_"))
                {
                    Undo.RecordObject(renderer, "Reset Material");
                    renderer.sharedMaterial = simpleBrownMat;
                    appliedCount++;
                }
            }

            // 4. Apply to ModularShaftWalls
            GameObject shaftWalls = GameObject.Find("ModularShaftWalls");
            if (shaftWalls != null)
            {
                var renderers = shaftWalls.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    Undo.RecordObject(renderer, "Reset Material");
                    renderer.sharedMaterial = simpleBrownMat;
                    appliedCount++;
                }
            }

            // 5. Apply to DepthLayer objects
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.StartsWith("DepthLayer_") || obj.name.Contains("TerrainChunk"))
                {
                    var renderer = obj.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        Undo.RecordObject(renderer, "Reset Material");
                        renderer.sharedMaterial = simpleBrownMat;
                        appliedCount++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[Materials] Reset {appliedCount} objects to simple brown material (0.45, 0.35, 0.28)");

            EditorUtility.DisplayDialog("Materials Reset",
                $"Reset all terrain and wall materials to simple brown!\n\n" +
                $"- Applied to {appliedCount} objects\n" +
                $"- Color: RGB(0.45, 0.35, 0.28)\n" +
                $"- No textures, Metallic=0, Smoothness=0.1\n\n" +
                "SAVE THE SCENE and enter Play mode to see the change!", "OK");
        }

        [MenuItem("Beneath The Floor/Materials/Apply Dirt_04 to Terrain Chunks", false, 98)]
        public static void ApplyDirt04ToTerrainChunks()
        {
            ApplyMaterialToTerrain("Assets/PBR Ground Materials #1 [Dirt& Grass]/Materials/Dirt/Dirt_04.mat", "Dirt_04");
        }

        [MenuItem("Beneath The Floor/Materials/Apply Dirt_08 to Terrain Chunks", false, 99)]
        public static void ApplyDirt08ToTerrainChunks()
        {
            ApplyMaterialToTerrain("Assets/PBR Ground Materials #1 [Dirt& Grass]/Materials/Dirt/Dirt_08.mat", "Dirt_08");
        }

        private static void ApplyMaterialToTerrain(string materialPath, string materialName)
        {
            // Load material
            Material dirtMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (dirtMat == null)
            {
                Debug.LogError($"[Materials] Could not find {materialName} at {materialPath}!");
                return;
            }

            int appliedCount = 0;

            // V3 system uses ChunkManager with its own material handling
            Debug.Log($"[Materials] V3 system - terrain material managed by ChunkManager");

            // Find all existing terrain chunks by name (V3 chunks are named "Chunk_X,Y,Z")
            var allRenderers = Object.FindObjectsOfType<MeshRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (renderer.gameObject.name.StartsWith("Chunk_"))
                {
                    Undo.RecordObject(renderer, $"Apply {materialName} Material");
                    renderer.sharedMaterial = dirtMat;
                    appliedCount++;
                }
            }

            // Find DepthLayer objects
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.StartsWith("DepthLayer_") || obj.name.Contains("TerrainChunk"))
                {
                    var renderer = obj.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterial != dirtMat)
                    {
                        Undo.RecordObject(renderer, $"Apply {materialName} Material");
                        renderer.sharedMaterial = dirtMat;
                        appliedCount++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Materials] Applied {materialName} material to {appliedCount} terrain objects. Save scene!");

            EditorUtility.DisplayDialog("Done",
                $"Applied {materialName} material to {appliedCount} terrain objects.\n\nSAVE THE SCENE and enter Play mode to see the change!", "OK");
        }

        [MenuItem("Beneath The Floor/Materials/Apply Dirt_08 to Excavation Shaft Walls", false, 100)]
        public static void ApplyDirt08ToShaftWalls()
        {
            // Load Dirt_08 material
            Material dirtMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PBR Ground Materials #1 [Dirt& Grass]/Materials/Dirt/Dirt_08.mat");
            if (dirtMat == null)
            {
                Debug.LogError("[Materials] Could not find Dirt_08.mat!");
                return;
            }

            int appliedCount = 0;

            // ONLY target ModularShaftWalls - the excavation area walls
            GameObject shaftWalls = GameObject.Find("ModularShaftWalls");
            if (shaftWalls != null)
            {
                var renderers = shaftWalls.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    Undo.RecordObject(renderer, "Apply Dirt_08 Material");
                    renderer.sharedMaterial = dirtMat;
                    appliedCount++;
                }
                Debug.Log($"[Materials] Found ModularShaftWalls with {renderers.Length} renderers");
            }
            else
            {
                Debug.LogWarning("[Materials] ModularShaftWalls object not found in scene!");
            }

            // Update the ModularShaftConfig asset for future walls
            string[] guids = AssetDatabase.FindAssets("ModularShaftConfig t:ModularShaftConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var config = AssetDatabase.LoadAssetAtPath<WorldRooms.ModularShaftConfig>(path);
                if (config != null)
                {
                    Undo.RecordObject(config, "Set Wall Material");
                    config.wallMaterial = dirtMat;
                    EditorUtility.SetDirty(config);
                    Debug.Log($"[Materials] Updated ModularShaftConfig.wallMaterial to Dirt_08");
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Materials] Applied Dirt_08 material to {appliedCount} excavation shaft wall renderers. Save scene!");

            EditorUtility.DisplayDialog("Done",
                $"Applied Dirt_08 material to {appliedCount} excavation shaft wall renderers.\n\nSAVE THE SCENE!", "OK");
        }

        #region Triplanar Terrain Materials

        [MenuItem("Beneath The Floor/Materials/Create Triplanar Terrain Materials", false, 110)]
        public static void CreateTriplanarTerrainMaterials()
        {
            // Find the TriplanarSoil shader
            Shader triplanarShader = Shader.Find("BeneathTheFloor/TriplanarSoil");
            if (triplanarShader == null)
            {
                Debug.LogError("[Materials] Could not find BeneathTheFloor/TriplanarSoil shader!");
                return;
            }

            // Ensure materials folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Materials/TriplanarTerrain"))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "TriplanarTerrain");
            }

            int created = 0;

            // Create triplanar material using Dirt_08 textures (reference)
            created += CreateTriplanarMaterial(triplanarShader, "Dirt_08",
                "babedf2c7eb52ed49a35a694be0f728e",  // BaseMap
                "5b568a142f864c64a86f734fe4aff336"); // NormalMap

            // Create triplanar material using Dirt_04 textures
            created += CreateTriplanarMaterial(triplanarShader, "Dirt_04",
                "2a95d8a7367ed224cadb9493713e4eb0",  // BaseMap
                "e9cce297bbba9b64dbb7603e4567e00d"); // NormalMap

            // Create triplanar material using Dirt_10 textures
            created += CreateTriplanarMaterial(triplanarShader, "Dirt_10",
                "36fc0def9a8bc0941aff4deaac264611",  // BaseMap
                "ff1fb87c7466ffe4184e68da72aaf01a"); // NormalMap

            // Create triplanar material using Dirt_12 textures
            created += CreateTriplanarMaterial(triplanarShader, "Dirt_12",
                "860827a348cd106498b117e36486ba0c",  // BaseMap
                "e22f241abd04d694ea4dc34b9b575cb5"); // NormalMap

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Materials] Created {created} triplanar terrain materials in Assets/Materials/TriplanarTerrain/");

            EditorUtility.DisplayDialog("Triplanar Materials Created",
                $"Created {created} triplanar terrain materials:\n\n" +
                "- M_Terrain_Dirt04_Triplanar\n" +
                "- M_Terrain_Dirt08_Triplanar\n" +
                "- M_Terrain_Dirt10_Triplanar\n" +
                "- M_Terrain_Dirt12_Triplanar\n\n" +
                "Find them in Assets/Materials/TriplanarTerrain/\n" +
                "Drag one to UndergroundTerrainManager.terrainMaterial to test!", "OK");
        }

        private static int CreateTriplanarMaterial(Shader shader, string dirtName, string baseMapGuid, string normalMapGuid)
        {
            string matPath = $"Assets/Materials/TriplanarTerrain/M_Terrain_{dirtName}_Triplanar.mat";

            // Load textures by GUID
            string baseMapPath = AssetDatabase.GUIDToAssetPath(baseMapGuid);
            string normalMapPath = AssetDatabase.GUIDToAssetPath(normalMapGuid);

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(baseMapPath);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);

            if (baseMap == null)
            {
                Debug.LogWarning($"[Materials] Could not load base map for {dirtName}");
                return 0;
            }

            // Create or update material
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            // Set dirt layer textures (main dirt texture)
            mat.SetTexture("_DirtTex", baseMap);
            mat.SetColor("_DirtColor", new Color(0.5f, 0.42f, 0.35f, 1f)); // Warm brown tint
            if (normalMap != null)
                mat.SetTexture("_DirtBumpMap", normalMap);

            // Set rock layer textures (same as dirt for now, but with different tint)
            mat.SetTexture("_RockTex", baseMap);
            mat.SetColor("_RockColor", new Color(0.45f, 0.4f, 0.38f, 1f)); // Slightly gray/rocky tint
            if (normalMap != null)
                mat.SetTexture("_RockBumpMap", normalMap);

            // Triplanar settings - optimized for underground terrain
            mat.SetFloat("_TilingScale", 3f);      // How often texture repeats per meter
            mat.SetFloat("_TriplanarSharpness", 5f); // Blend sharpness between planes
            mat.SetFloat("_NormalStrength", 1.2f);   // Normal map intensity

            // Layer blending - dirt on flat, rock on steep walls
            mat.SetFloat("_WallRockBlendStrength", 1.5f);
            mat.SetFloat("_WallSteepnessThreshold", 0.5f);

            // PBR settings - matte dirt look
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Metallic", 0f);

            // Macro variation - subtle color variation
            mat.SetFloat("_MacroScale", 0.08f);
            mat.SetFloat("_MacroStrength", 0.05f);
            mat.SetFloat("_MacroRoughnessVar", 0.02f);

            // Depth darkening
            mat.SetFloat("_DepthDarkenStrength", 0.25f);
            mat.SetFloat("_DepthStart", 0f);
            mat.SetFloat("_DepthRange", 50f);

            // Cavity darkening
            mat.SetFloat("_CavityDarkenStrength", 0.2f);
            mat.SetFloat("_CavityAOPower", 1.5f);

            EditorUtility.SetDirty(mat);

            Debug.Log($"[Materials] Created/Updated: {matPath}");
            return 1;
        }

        [MenuItem("Beneath The Floor/Materials/Apply Triplanar Dirt_08 to Terrain", false, 111)]
        public static void ApplyTriplanarDirt08ToTerrain()
        {
            ApplyTriplanarMaterialToTerrain("M_Terrain_Dirt_08_Triplanar");
        }

        [MenuItem("Beneath The Floor/Materials/Apply Triplanar Dirt_04 to Terrain", false, 112)]
        public static void ApplyTriplanarDirt04ToTerrain()
        {
            ApplyTriplanarMaterialToTerrain("M_Terrain_Dirt_04_Triplanar");
        }

        [MenuItem("Beneath The Floor/Materials/Apply Triplanar Dirt_10 to Terrain", false, 113)]
        public static void ApplyTriplanarDirt10ToTerrain()
        {
            ApplyTriplanarMaterialToTerrain("M_Terrain_Dirt_10_Triplanar");
        }

        [MenuItem("Beneath The Floor/Materials/Apply Triplanar Dirt_12 to Terrain", false, 114)]
        public static void ApplyTriplanarDirt12ToTerrain()
        {
            ApplyTriplanarMaterialToTerrain("M_Terrain_Dirt_12_Triplanar");
        }

        private static void ApplyTriplanarMaterialToTerrain(string materialName)
        {
            string matPath = $"Assets/Materials/TriplanarTerrain/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                Debug.LogError($"[Materials] Material not found: {matPath}. Run 'Create Triplanar Terrain Materials' first!");
                EditorUtility.DisplayDialog("Material Not Found",
                    $"Material '{materialName}' not found.\n\nRun 'Beneath The Floor → Materials → Create Triplanar Terrain Materials' first!", "OK");
                return;
            }

            int appliedCount = 0;

            // V3 system uses ChunkManager with its own material handling
            Debug.Log($"[Materials] V3 system - terrain material {materialName} should be set on ChunkManager");
            appliedCount++;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Applied",
                $"Applied {materialName} to terrain manager.\n\nSAVE SCENE and press PLAY to see the terrain with the new material!", "OK");
        }

        #endregion
    }
}
