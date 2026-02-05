using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SceneSetup : Editor
{
    [MenuItem("Tools/Beneath The Floor/Create House Scene")]
    public static void CreateHouseScene()
    {
        // Create new scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Create scene structure
        CreateHouseEnvironment();
        CreatePlayer();
        CreateGameManagers();
        CreateLighting();

        // Save scene
        string scenePath = "Assets/Scenes/HouseScene.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        Debug.Log("House Scene created successfully at: " + scenePath);
    }

    [MenuItem("Tools/Beneath The Floor/Create Basement Scene")]
    public static void CreateBasementScene()
    {
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        CreateBasementEnvironment();
        CreatePlayer();
        CreateGameManagers();
        CreateBasementLighting();

        string scenePath = "Assets/Scenes/BasementScene.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        Debug.Log("Basement Scene created successfully at: " + scenePath);
    }

    private static void CreateHouseEnvironment()
    {
        // House Root
        GameObject house = new GameObject("House");

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.parent = house.transform;
        floor.transform.localScale = new Vector3(12, 0.2f, 10);
        floor.transform.position = new Vector3(0, -0.1f, 0);
        SetLayerRecursively(floor, LayerMask.NameToLayer("Default"));

        // Walls
        CreateWall("Wall_North", house.transform, new Vector3(0, 1.5f, 5), new Vector3(12, 3, 0.2f));
        CreateWall("Wall_South", house.transform, new Vector3(0, 1.5f, -5), new Vector3(12, 3, 0.2f));
        CreateWall("Wall_East", house.transform, new Vector3(6, 1.5f, 0), new Vector3(0.2f, 3, 10));
        CreateWall("Wall_West", house.transform, new Vector3(-6, 1.5f, 0), new Vector3(0.2f, 3, 10));

        // Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.parent = house.transform;
        ceiling.transform.localScale = new Vector3(12, 0.2f, 10);
        ceiling.transform.position = new Vector3(0, 3.1f, 0);

        // Cracked Wall (leads to basement)
        CreateCrackedWall(house.transform);

        // Create some furniture
        CreateFurniture(house.transform);

        Debug.Log("House environment created");
    }

    private static void CreateWall(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.parent = parent;
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    private static void CreateCrackedWall(Transform parent)
    {
        // Cracked wall area
        GameObject crackedWallArea = new GameObject("CrackedWallArea");
        crackedWallArea.transform.parent = parent;
        crackedWallArea.transform.position = new Vector3(-5.8f, 1.5f, 3);

        // Intact wall section
        GameObject intactWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        intactWall.name = "IntactWall";
        intactWall.transform.parent = crackedWallArea.transform;
        intactWall.transform.localPosition = Vector3.zero;
        intactWall.transform.localScale = new Vector3(0.3f, 2.5f, 2);
        intactWall.layer = LayerMask.NameToLayer("Default");

        // Add interactable component placeholder
        // The CrackedWall script would be added manually or via another setup

        // Stairs to basement (hidden initially)
        GameObject stairs = new GameObject("StairsToBasement");
        stairs.transform.parent = crackedWallArea.transform;
        stairs.transform.localPosition = new Vector3(-1, -0.5f, 0);

        // Create stair steps
        for (int i = 0; i < 8; i++)
        {
            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{i}";
            step.transform.parent = stairs.transform;
            step.transform.localPosition = new Vector3(-i * 0.3f, -i * 0.25f, 0);
            step.transform.localScale = new Vector3(0.8f, 0.1f, 1.5f);
        }

        stairs.SetActive(false); // Hidden until wall is broken

        Debug.Log("Cracked wall area created");
    }

    private static void CreateFurniture(Transform parent)
    {
        GameObject furniture = new GameObject("Furniture");
        furniture.transform.parent = parent;

        // Table
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.parent = furniture.transform;
        table.transform.position = new Vector3(2, 0.4f, 2);
        table.transform.localScale = new Vector3(1.5f, 0.1f, 1);

        // Table legs
        CreateTableLeg("TableLeg_1", furniture.transform, new Vector3(1.4f, 0.2f, 1.6f));
        CreateTableLeg("TableLeg_2", furniture.transform, new Vector3(2.6f, 0.2f, 1.6f));
        CreateTableLeg("TableLeg_3", furniture.transform, new Vector3(1.4f, 0.2f, 2.4f));
        CreateTableLeg("TableLeg_4", furniture.transform, new Vector3(2.6f, 0.2f, 2.4f));

        // Chair
        GameObject chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chair.name = "Chair_Seat";
        chair.transform.parent = furniture.transform;
        chair.transform.position = new Vector3(2, 0.25f, 3.2f);
        chair.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);

        // Chair back
        GameObject chairBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chairBack.name = "Chair_Back";
        chairBack.transform.parent = furniture.transform;
        chairBack.transform.position = new Vector3(2, 0.5f, 3.4f);
        chairBack.transform.localScale = new Vector3(0.5f, 0.5f, 0.05f);

        // Bookshelf
        GameObject bookshelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bookshelf.name = "Bookshelf";
        bookshelf.transform.parent = furniture.transform;
        bookshelf.transform.position = new Vector3(4, 1f, -4.5f);
        bookshelf.transform.localScale = new Vector3(2, 2, 0.4f);

        Debug.Log("Furniture created");
    }

    private static void CreateTableLeg(string name, Transform parent, Vector3 position)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = name;
        leg.transform.parent = parent;
        leg.transform.position = position;
        leg.transform.localScale = new Vector3(0.1f, 0.4f, 0.1f);
    }

    private static void CreateBasementEnvironment()
    {
        GameObject basement = new GameObject("Basement");

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "BasementFloor";
        floor.transform.parent = basement.transform;
        floor.transform.localScale = new Vector3(15, 0.2f, 15);
        floor.transform.position = new Vector3(0, -3.1f, 0);

        // Walls
        CreateWall("Basement_Wall_North", basement.transform, new Vector3(0, -1.5f, 7.5f), new Vector3(15, 3, 0.2f));
        CreateWall("Basement_Wall_South", basement.transform, new Vector3(0, -1.5f, -7.5f), new Vector3(15, 3, 0.2f));
        CreateWall("Basement_Wall_East", basement.transform, new Vector3(7.5f, -1.5f, 0), new Vector3(0.2f, 3, 15));
        CreateWall("Basement_Wall_West", basement.transform, new Vector3(-7.5f, -1.5f, 0), new Vector3(0.2f, 3, 15));

        // Ceiling (low ceiling for basement feel)
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "BasementCeiling";
        ceiling.transform.parent = basement.transform;
        ceiling.transform.localScale = new Vector3(15, 0.2f, 15);
        ceiling.transform.position = new Vector3(0, 0.1f, 0);

        // Diggable area
        CreateDiggableArea(basement.transform);

        // Old machinery (placeholder)
        CreateOldMachinery(basement.transform);

        // Stairs back up
        CreateBasementStairs(basement.transform);

        Debug.Log("Basement environment created");
    }

    private static void CreateDiggableArea(Transform parent)
    {
        GameObject diggableArea = new GameObject("DiggableArea");
        diggableArea.transform.parent = parent;
        diggableArea.transform.position = new Vector3(0, -3, 0);

        // Create a grid of diggable tiles
        int gridSize = 5;
        float tileSize = 1.5f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"DiggableTile_{x}_{z}";
                tile.transform.parent = diggableArea.transform;
                tile.transform.localPosition = new Vector3(
                    (x - gridSize / 2) * tileSize,
                    0,
                    (z - gridSize / 2) * tileSize
                );
                tile.transform.localScale = new Vector3(tileSize - 0.1f, 0.2f, tileSize - 0.1f);

                // Set layer for digging raycast
                tile.layer = LayerMask.NameToLayer("Default");
            }
        }

        Debug.Log("Diggable area created");
    }

    private static void CreateOldMachinery(Transform parent)
    {
        GameObject machinery = new GameObject("OldMachinery");
        machinery.transform.parent = parent;
        machinery.transform.position = new Vector3(-5, -2, -5);

        // Main machine body
        GameObject machineBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        machineBody.name = "MachineBody";
        machineBody.transform.parent = machinery.transform;
        machineBody.transform.localPosition = Vector3.zero;
        machineBody.transform.localScale = new Vector3(2, 1.5f, 1);

        // Machine top
        GameObject machineTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        machineTop.name = "MachineTop";
        machineTop.transform.parent = machinery.transform;
        machineTop.transform.localPosition = new Vector3(0, 1, 0);
        machineTop.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);

        // Pipes
        GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = "Pipe";
        pipe.transform.parent = machinery.transform;
        pipe.transform.localPosition = new Vector3(1.5f, 0, 0);
        pipe.transform.localScale = new Vector3(0.2f, 1, 0.2f);
        pipe.transform.rotation = Quaternion.Euler(0, 0, 90);

        Debug.Log("Old machinery placeholder created");
    }

    private static void CreateBasementStairs(Transform parent)
    {
        GameObject stairs = new GameObject("StairsUp");
        stairs.transform.parent = parent;
        stairs.transform.position = new Vector3(-6, -3, 5);

        for (int i = 0; i < 10; i++)
        {
            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{i}";
            step.transform.parent = stairs.transform;
            step.transform.localPosition = new Vector3(0, i * 0.3f, -i * 0.4f);
            step.transform.localScale = new Vector3(1.2f, 0.15f, 0.5f);
        }

        Debug.Log("Basement stairs created");
    }

    private static void CreatePlayer()
    {
        // Find and remove default camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            DestroyImmediate(mainCam.gameObject);
        }

        // Create player
        GameObject player = new GameObject("Player");
        player.tag = "Player";

        // Add character controller
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0, 1f, 0);

        // Create camera
        GameObject cameraHolder = new GameObject("CameraHolder");
        cameraHolder.transform.parent = player.transform;
        cameraHolder.transform.localPosition = new Vector3(0, 1.0f, 0);

        GameObject mainCamera = new GameObject("Main Camera");
        mainCamera.tag = "MainCamera";
        mainCamera.transform.parent = cameraHolder.transform;
        mainCamera.transform.localPosition = Vector3.zero;

        Camera cam = mainCamera.AddComponent<Camera>();
        cam.fieldOfView = 70;
        cam.nearClipPlane = 0.1f;

        mainCamera.AddComponent<AudioListener>();

        // Ground check
        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.parent = player.transform;
        groundCheck.transform.localPosition = new Vector3(0, 0, 0);

        // Set player position
        player.transform.position = new Vector3(0, 0.1f, 0);

        Debug.Log("Player created");
    }

    private static void CreateGameManagers()
    {
        GameObject managers = new GameObject("---MANAGERS---");

        GameObject gameManager = new GameObject("GameManager");
        gameManager.transform.parent = managers.transform;

        GameObject audioManager = new GameObject("AudioManager");
        audioManager.transform.parent = managers.transform;

        GameObject uiManager = new GameObject("UIManager");
        uiManager.transform.parent = managers.transform;

        Debug.Log("Game managers created");
    }

    private static void CreateLighting()
    {
        // Find existing directional light
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                DestroyImmediate(light.gameObject);
            }
        }

        // Create main light
        GameObject mainLight = new GameObject("MainLight");
        Light lightComp = mainLight.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1f;
        lightComp.shadows = LightShadows.Soft;
        mainLight.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Create point lights for interior
        CreatePointLight("Light_Center", new Vector3(0, 2.5f, 0), 1f, 8f);
        CreatePointLight("Light_Corner1", new Vector3(4, 2.5f, 3), 0.7f, 5f);
        CreatePointLight("Light_Corner2", new Vector3(-4, 2.5f, -3), 0.7f, 5f);

        Debug.Log("Lighting setup complete");
    }

    private static void CreateBasementLighting()
    {
        // Find existing directional light
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 0.1f; // Dim ambient light
            }
        }

        // Dim, moody basement lighting
        CreatePointLight("BasementLight_1", new Vector3(0, -0.5f, 0), 0.5f, 10f);
        CreatePointLight("BasementLight_2", new Vector3(-5, -1f, -5), 0.3f, 6f);
        CreatePointLight("BasementLight_Stairs", new Vector3(-6, -1, 5), 0.4f, 5f);

        Debug.Log("Basement lighting setup complete");
    }

    private static void CreatePointLight(string name, Vector3 position, float intensity, float range)
    {
        GameObject lightObj = new GameObject(name);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        lightObj.transform.position = position;
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    [MenuItem("Tools/Beneath The Floor/Setup All")]
    public static void SetupAll()
    {
        FolderStructureSetup.CreateFolderStructure();
        CreateHouseScene();
        CreateBasementScene();
        Debug.Log("All scenes and folder structure created successfully!");
    }
}
