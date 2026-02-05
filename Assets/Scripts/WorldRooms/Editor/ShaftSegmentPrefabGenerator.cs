#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BeneathTheFloor.WorldRooms.Editor
{
    /// <summary>
    /// Editor utility for generating modular shaft wall segment prefabs.
    /// Creates both solid and cave-opening versions at multiple heights.
    /// </summary>
    public static class ShaftSegmentPrefabGenerator
    {
        private const string PrefabsPath = "Assets/Prefabs/Shaft";
        private const string ConfigsPath = "Assets/GameData/Shaft";
        private const string MaterialsPath = "Assets/Materials";

        // Standard segment heights
        private static readonly float[] SegmentHeights = { 4f, 6f, 8f, 10f };

        [MenuItem("Tools/Beneath The Floor/Generate Shaft Segment Prefabs")]
        public static void GenerateAllSegmentPrefabs()
        {
            EnsureDirectoriesExist();

            Material wallMaterial = GetOrCreateWallMaterial();

            List<ShaftWallSegmentConfig> configs = new List<ShaftWallSegmentConfig>();

            foreach (float height in SegmentHeights)
            {
                var config = GenerateSegmentSet(height, wallMaterial);
                if (config != null)
                {
                    configs.Add(config);
                }
            }

            // Generate main shaft config
            GenerateModularShaftConfig(configs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ShaftSegmentPrefabGenerator] Generated {configs.Count} segment configurations with prefabs");
            EditorUtility.DisplayDialog("Shaft Segments Generated",
                $"Created {configs.Count * 2} prefabs (solid + opening) and {configs.Count} segment configs.\n\n" +
                "Location: Assets/Prefabs/Shaft/\n" +
                "Configs: Assets/GameData/Shaft/",
                "OK");
        }

        private static void EnsureDirectoriesExist()
        {
            CreateFolderIfNeeded("Assets", "Prefabs");
            CreateFolderIfNeeded("Assets/Prefabs", "Shaft");
            CreateFolderIfNeeded("Assets", "GameData");
            CreateFolderIfNeeded("Assets/GameData", "Shaft");
        }

        private static void CreateFolderIfNeeded(string parentPath, string folderName)
        {
            string fullPath = $"{parentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        private static Material GetOrCreateWallMaterial()
        {
            // Try to find existing shaft wall material
            string[] guids = AssetDatabase.FindAssets("ShaftWall t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) return mat;
            }

            // Try Diggable_Dirt
            guids = AssetDatabase.FindAssets("Diggable_Dirt t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) return mat;
            }

            // Create new material
            string matPath = $"{MaterialsPath}/ShaftWall.mat";
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null) return existingMat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wallMaterial = new Material(shader);
            wallMaterial.name = "ShaftWall";
            wallMaterial.SetColor("_BaseColor", new Color(0.35f, 0.25f, 0.18f, 1f));
            wallMaterial.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(wallMaterial, matPath);
            return wallMaterial;
        }

        private static ShaftWallSegmentConfig GenerateSegmentSet(float height, Material material)
        {
            string heightStr = $"{height}m";

            // Generate solid prefab
            GameObject solidPrefab = GenerateSolidSegment(height, material);
            string solidPath = $"{PrefabsPath}/ShaftSegment_Solid_{heightStr}.prefab";
            SavePrefab(solidPrefab, solidPath);
            Object.DestroyImmediate(solidPrefab);

            // Generate opening prefab
            GameObject openingPrefab = GenerateOpeningSegment(height, material);
            string openingPath = $"{PrefabsPath}/ShaftSegment_Opening_{heightStr}.prefab";
            SavePrefab(openingPrefab, openingPath);
            Object.DestroyImmediate(openingPrefab);

            // Load prefabs back
            GameObject solidAsset = AssetDatabase.LoadAssetAtPath<GameObject>(solidPath);
            GameObject openingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(openingPath);

            // Create config
            return GenerateSegmentConfig(height, solidAsset, openingAsset);
        }

        private static GameObject GenerateSolidSegment(float height, Material material)
        {
            float width = 12f; // Match terrain horizontalExtent
            float thickness = 0.4f;

            GameObject root = new GameObject($"ShaftSegment_Solid_{height}m");

            // Main wall
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(root.transform);
            wall.transform.localPosition = Vector3.zero;
            wall.transform.localScale = new Vector3(width, height, thickness);

            // Apply material
            wall.GetComponent<Renderer>().sharedMaterial = material;

            return root;
        }

        private static GameObject GenerateOpeningSegment(float height, Material material)
        {
            float width = 12f; // Match terrain horizontalExtent
            float thickness = 0.4f;
            float openingWidth = 4.5f;  // Match room entrance width (RoomPrefabGenerator)
            float openingHeight = Mathf.Min(4f, height * 0.9f); // Match room entrance height (4m)

            GameObject root = new GameObject($"ShaftSegment_Opening_{height}m");

            // Calculate segment dimensions around the opening
            float sideWidth = (width - openingWidth) / 2f;
            float topHeight = (height - openingHeight) / 2f;
            float bottomHeight = topHeight;

            // Left wall piece
            if (sideWidth > 0.1f)
            {
                GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
                left.name = "Wall_Left";
                left.transform.SetParent(root.transform);
                left.transform.localPosition = new Vector3(-(openingWidth + sideWidth) / 2f, 0, 0);
                left.transform.localScale = new Vector3(sideWidth, height, thickness);
                left.GetComponent<Renderer>().sharedMaterial = material;
            }

            // Right wall piece
            if (sideWidth > 0.1f)
            {
                GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
                right.name = "Wall_Right";
                right.transform.SetParent(root.transform);
                right.transform.localPosition = new Vector3((openingWidth + sideWidth) / 2f, 0, 0);
                right.transform.localScale = new Vector3(sideWidth, height, thickness);
                right.GetComponent<Renderer>().sharedMaterial = material;
            }

            // Top piece (above opening)
            if (topHeight > 0.1f)
            {
                GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                top.name = "Wall_Top";
                top.transform.SetParent(root.transform);
                top.transform.localPosition = new Vector3(0, (openingHeight + topHeight) / 2f, 0);
                top.transform.localScale = new Vector3(openingWidth, topHeight, thickness);
                top.GetComponent<Renderer>().sharedMaterial = material;
            }

            // Bottom piece (below opening)
            if (bottomHeight > 0.1f)
            {
                GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bottom.name = "Wall_Bottom";
                bottom.transform.SetParent(root.transform);
                bottom.transform.localPosition = new Vector3(0, -(openingHeight + bottomHeight) / 2f, 0);
                bottom.transform.localScale = new Vector3(openingWidth, bottomHeight, thickness);
                bottom.GetComponent<Renderer>().sharedMaterial = material;
            }

            // Cave frame pieces (rough edges)
            CreateCaveFrame(root.transform, openingWidth, openingHeight, thickness, material);

            return root;
        }

        private static void CreateCaveFrame(Transform parent, float openingWidth, float openingHeight, float thickness, Material material)
        {
            // Create irregular rocky edges around the opening
            float frameDepth = 0.3f;
            float frameThickness = 0.4f;

            // Bottom rocky edge
            GameObject bottomRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomRock.name = "CaveFrame_Bottom";
            bottomRock.transform.SetParent(parent);
            bottomRock.transform.localPosition = new Vector3(0, -openingHeight / 2f + 0.1f, -frameDepth / 2f);
            bottomRock.transform.localScale = new Vector3(openingWidth * 0.9f, frameThickness, thickness + frameDepth);
            bottomRock.transform.localRotation = Quaternion.Euler(5, 0, 0);
            bottomRock.GetComponent<Renderer>().sharedMaterial = material;

            // Left rocky edge
            GameObject leftRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftRock.name = "CaveFrame_Left";
            leftRock.transform.SetParent(parent);
            leftRock.transform.localPosition = new Vector3(-openingWidth / 2f + 0.15f, 0, -frameDepth / 2f);
            leftRock.transform.localScale = new Vector3(frameThickness, openingHeight * 0.85f, thickness + frameDepth);
            leftRock.transform.localRotation = Quaternion.Euler(0, 0, -8);
            leftRock.GetComponent<Renderer>().sharedMaterial = material;

            // Right rocky edge
            GameObject rightRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightRock.name = "CaveFrame_Right";
            rightRock.transform.SetParent(parent);
            rightRock.transform.localPosition = new Vector3(openingWidth / 2f - 0.15f, 0, -frameDepth / 2f);
            rightRock.transform.localScale = new Vector3(frameThickness, openingHeight * 0.85f, thickness + frameDepth);
            rightRock.transform.localRotation = Quaternion.Euler(0, 0, 8);
            rightRock.GetComponent<Renderer>().sharedMaterial = material;

            // Top arch (irregular)
            GameObject topRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topRock.name = "CaveFrame_Top";
            topRock.transform.SetParent(parent);
            topRock.transform.localPosition = new Vector3(0, openingHeight / 2f - 0.2f, -frameDepth / 2f);
            topRock.transform.localScale = new Vector3(openingWidth * 0.8f, frameThickness * 0.8f, thickness + frameDepth);
            topRock.transform.localRotation = Quaternion.Euler(-5, 0, 0);
            topRock.GetComponent<Renderer>().sharedMaterial = material;

            // Add some random rock protrusions
            for (int i = 0; i < 4; i++)
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"CaveFrame_Rock_{i}";
                rock.transform.SetParent(parent);

                float angle = (i / 4f) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * (openingWidth / 2f - 0.3f);
                float y = Mathf.Sin(angle) * (openingHeight / 2f - 0.3f);

                rock.transform.localPosition = new Vector3(x, y, -frameDepth * 0.3f);
                rock.transform.localScale = new Vector3(0.3f + Random.Range(0f, 0.2f), 0.3f + Random.Range(0f, 0.2f), 0.3f);
                rock.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));
                rock.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static ShaftWallSegmentConfig GenerateSegmentConfig(float height, GameObject solidPrefab, GameObject openingPrefab)
        {
            string configPath = $"{ConfigsPath}/ShaftSegment_{height}m.asset";

            // Check if exists
            ShaftWallSegmentConfig existing = AssetDatabase.LoadAssetAtPath<ShaftWallSegmentConfig>(configPath);
            if (existing != null)
            {
                existing.height = height;
                existing.solidPrefab = solidPrefab;
                existing.openingPrefab = openingPrefab;
                existing.width = 12f; // Match terrain horizontalExtent
                existing.thickness = 0.4f;
                existing.openingWidth = 4.5f;  // Match room entrance width
                existing.openingHeight = Mathf.Min(4f, height * 0.9f); // Match room entrance height
                EditorUtility.SetDirty(existing);
                return existing;
            }

            // Create new
            ShaftWallSegmentConfig config = ScriptableObject.CreateInstance<ShaftWallSegmentConfig>();
            config.segmentId = $"Segment_{height}m";
            config.height = height;
            config.width = 12f; // Match terrain horizontalExtent
            config.thickness = 0.4f;
            config.solidPrefab = solidPrefab;
            config.openingPrefab = openingPrefab;
            config.openingWidth = 4.5f;  // Match room entrance width
            config.openingHeight = Mathf.Min(4f, height * 0.9f); // Match room entrance height

            AssetDatabase.CreateAsset(config, configPath);
            return config;
        }

        private static void GenerateModularShaftConfig(List<ShaftWallSegmentConfig> segmentConfigs)
        {
            string configPath = $"{ConfigsPath}/ModularShaftConfig.asset";

            ModularShaftConfig existing = AssetDatabase.LoadAssetAtPath<ModularShaftConfig>(configPath);
            if (existing != null)
            {
                existing.segmentConfigs = segmentConfigs;
                if (segmentConfigs.Count > 0)
                {
                    // Use 6m as default
                    existing.defaultSegmentConfig = segmentConfigs.Find(c => c.height == 6f) ?? segmentConfigs[0];
                }
                EditorUtility.SetDirty(existing);
                return;
            }

            ModularShaftConfig config = ScriptableObject.CreateInstance<ModularShaftConfig>();
            config.shaftHalfWidth = 6f; // Match terrain horizontalExtent / 2
            config.shaftHalfLength = 6f; // Match terrain horizontalExtent / 2
            config.maxDepth = 220f;
            config.segmentConfigs = segmentConfigs;
            if (segmentConfigs.Count > 0)
            {
                config.defaultSegmentConfig = segmentConfigs.Find(c => c.height == 6f) ?? segmentConfigs[0];
            }

            AssetDatabase.CreateAsset(config, configPath);
        }

        private static void SavePrefab(GameObject obj, string path)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingPrefab != null)
            {
                PrefabUtility.SaveAsPrefabAsset(obj, path);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(obj, path);
            }
        }

        [MenuItem("Tools/Beneath The Floor/Generate Shaft Segment Prefabs", true)]
        private static bool ValidateGenerateSegmentPrefabs()
        {
            return !Application.isPlaying;
        }
    }
}
#endif
