using UnityEngine;
using UnityEditor;
using System.IO;

public class MaterialSetup : Editor
{
    [MenuItem("Tools/Beneath The Floor/Create Materials")]
    public static void CreateMaterials()
    {
        // Ensure Materials folder exists
        string materialsPath = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(materialsPath))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        // Create House Materials
        CreateMaterial("House_Wall", new Color(0.85f, 0.82f, 0.75f), materialsPath);
        CreateMaterial("House_Floor", new Color(0.55f, 0.35f, 0.2f), materialsPath);
        CreateMaterial("House_Ceiling", new Color(0.9f, 0.88f, 0.85f), materialsPath);
        CreateMaterial("House_CrackedWall", new Color(0.6f, 0.55f, 0.5f), materialsPath);
        CreateMaterial("House_Stairs", new Color(0.45f, 0.3f, 0.2f), materialsPath);

        // Create Basement Materials
        CreateMaterial("Basement_Wall", new Color(0.35f, 0.32f, 0.3f), materialsPath);
        CreateMaterial("Basement_Floor", new Color(0.25f, 0.22f, 0.2f), materialsPath);
        CreateMaterial("Basement_Ceiling", new Color(0.3f, 0.28f, 0.25f), materialsPath);

        // Create Diggable Materials
        CreateMaterial("Diggable_Dirt", new Color(0.45f, 0.3f, 0.15f), materialsPath);
        CreateMaterial("Diggable_Dug", new Color(0.2f, 0.15f, 0.1f), materialsPath);

        // Create Furniture Materials
        CreateMaterial("Furniture_Wood", new Color(0.5f, 0.35f, 0.2f), materialsPath);
        CreateMaterial("Furniture_Metal", new Color(0.5f, 0.5f, 0.55f), materialsPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("All materials created successfully in " + materialsPath);
    }

    private static void CreateMaterial(string name, Color color, string path)
    {
        string fullPath = path + "/" + name + ".mat";

        // Check if material already exists
        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
        if (existingMat != null)
        {
            existingMat.color = color;
            EditorUtility.SetDirty(existingMat);
            Debug.Log("Updated existing material: " + name);
            return;
        }

        // Create new material using URP Lit shader
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Smoothness", 0.3f);

        AssetDatabase.CreateAsset(mat, fullPath);
        Debug.Log("Created material: " + name);
    }

    [MenuItem("Tools/Beneath The Floor/Assign Materials to Scene")]
    public static void AssignMaterialsToScene()
    {
        string materialsPath = "Assets/Materials";

        // Load materials
        Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/House_Wall.mat");
        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/House_Floor.mat");
        Material ceilingMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/House_Ceiling.mat");
        Material crackedWallMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/House_CrackedWall.mat");
        Material stairsMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/House_Stairs.mat");

        // Find and assign to House objects
        GameObject house = GameObject.Find("House");
        if (house != null)
        {
            AssignMaterialToChild(house, "Floor", floorMat);
            AssignMaterialToChild(house, "Wall_North", wallMat);
            AssignMaterialToChild(house, "Wall_South", wallMat);
            AssignMaterialToChild(house, "Wall_East", wallMat);
            AssignMaterialToChild(house, "Wall_West", wallMat);
            AssignMaterialToChild(house, "Ceiling", ceilingMat);

            // Cracked wall area
            Transform crackedArea = house.transform.Find("CrackedWallArea");
            if (crackedArea != null)
            {
                AssignMaterialToChild(crackedArea.gameObject, "IntactWall", crackedWallMat);

                Transform stairs = crackedArea.Find("StairsToBasement");
                if (stairs != null)
                {
                    foreach (Transform step in stairs)
                    {
                        MeshRenderer mr = step.GetComponent<MeshRenderer>();
                        if (mr != null) mr.material = stairsMat;
                    }
                }
            }
        }

        // Find and assign to Basement objects (if in basement scene)
        GameObject basement = GameObject.Find("Basement");
        if (basement != null)
        {
            Material basementWallMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/Basement_Wall.mat");
            Material basementFloorMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/Basement_Floor.mat");
            Material basementCeilingMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/Basement_Ceiling.mat");
            Material dirtMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/Diggable_Dirt.mat");
            Material metalMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/Furniture_Metal.mat");

            AssignMaterialToChild(basement, "BasementFloor", basementFloorMat);
            AssignMaterialToChild(basement, "Basement_Wall_North", basementWallMat);
            AssignMaterialToChild(basement, "Basement_Wall_South", basementWallMat);
            AssignMaterialToChild(basement, "Basement_Wall_East", basementWallMat);
            AssignMaterialToChild(basement, "Basement_Wall_West", basementWallMat);
            AssignMaterialToChild(basement, "BasementCeiling", basementCeilingMat);

            // Assign materials to DiggableArea tiles
            Transform diggableArea = basement.transform.Find("DiggableArea");
            if (diggableArea != null)
            {
                foreach (Transform tile in diggableArea)
                {
                    MeshRenderer mr = tile.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material = dirtMat;
                }
                Debug.Log("Assigned Diggable_Dirt to all diggable tiles");
            }

            // Assign materials to StairsUp
            Transform stairsUp = basement.transform.Find("StairsUp");
            if (stairsUp != null)
            {
                foreach (Transform step in stairsUp)
                {
                    MeshRenderer mr = step.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material = stairsMat;
                }
                Debug.Log("Assigned House_Stairs to basement stairs");
            }

            // Assign materials to OldMachinery
            Transform machinery = basement.transform.Find("OldMachinery");
            if (machinery != null)
            {
                foreach (Transform part in machinery)
                {
                    MeshRenderer mr = part.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material = metalMat;
                }
                Debug.Log("Assigned Furniture_Metal to old machinery");
            }
        }

        // Assign materials to House Furniture
        GameObject house2 = GameObject.Find("House");
        if (house2 != null)
        {
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath + "/Furniture_Wood.mat");
            Transform furniture = house2.transform.Find("Furniture");
            if (furniture != null)
            {
                foreach (Transform item in furniture)
                {
                    MeshRenderer mr = item.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material = woodMat;
                }
                Debug.Log("Assigned Furniture_Wood to house furniture");
            }
        }

        Debug.Log("Materials assigned to scene objects");
    }

    private static void AssignMaterialToChild(GameObject parent, string childName, Material mat)
    {
        if (mat == null) return;

        Transform child = parent.transform.Find(childName);
        if (child != null)
        {
            MeshRenderer mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = mat;
                Debug.Log($"Assigned {mat.name} to {childName}");
            }
        }
    }
}
