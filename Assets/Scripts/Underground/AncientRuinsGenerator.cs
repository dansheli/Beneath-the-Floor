using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Managers;
using BeneathTheFloor.Story;
using BeneathTheFloor.Audio;

namespace BeneathTheFloor.Underground
{
    public class AncientRuinsGenerator : MonoBehaviour
    {
        [Header("Room Settings")]
        [SerializeField] private int roomWidth = 6;
        [SerializeField] private int roomLength = 6;
        [SerializeField] private int roomHeight = 3;

        [Header("Materials")]
        [SerializeField] private Material stoneWallMaterial;
        [SerializeField] private Material stoneFloorMaterial;
        [SerializeField] private Material glyphMaterial;

        [Header("Props")]
        [SerializeField] private GameObject[] ancientMachineryPrefabs;
        [SerializeField] private GameObject storyItemPrefab;
        [SerializeField] private GameObject glyphPrefab;

        [Header("Lighting")]
        [SerializeField] private Color eerieAmbientColor = new Color(0.2f, 0.1f, 0.3f);
        [SerializeField] private Color eeriePointLightColor = new Color(0.5f, 0.3f, 0.8f);
        [SerializeField] private float eeriePointLightIntensity = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip ruinsDiscoveredSound;
        [SerializeField] private AudioClip ruinsAmbientSound;

        [Header("Spawn Settings")]
        [SerializeField] private float minDepthToSpawn = 45f;
        [SerializeField] private bool hasSpawned = false;

        public static AncientRuinsGenerator Instance { get; private set; }

        private GameObject ruinsRoom;
        // TODO: reconnect to NEW digging system later (old digging code removed).
        // private DepthManager depthManager;
        private StoryManager storyManager;
        private AudioManager audioManager;

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

        private void Start()
        {
            // TODO: reconnect to NEW digging system later (old digging code removed).
            // depthManager = DepthManager.Instance;
            storyManager = StoryManager.Instance;
            audioManager = AudioManager.Instance;

            // Subscribe to Ancient Ruins reached event - DISABLED (DepthManager removed)
            // if (depthManager != null)
            // {
            //     depthManager.OnAncientRuinsReached.AddListener(OnAncientRuinsReached);
            // }
        }

        private void OnDestroy()
        {
            // TODO: reconnect to NEW digging system later (old digging code removed).
            // if (depthManager != null)
            // {
            //     depthManager.OnAncientRuinsReached.RemoveListener(OnAncientRuinsReached);
            // }
        }

        private void OnAncientRuinsReached()
        {
            if (!hasSpawned)
            {
                GenerateRuinsRoom();
            }
        }

        /// <summary>
        /// Generate the Ancient Ruins room procedurally.
        /// </summary>
        public void GenerateRuinsRoom()
        {
            if (hasSpawned) return;

            hasSpawned = true;

            // Create parent object
            ruinsRoom = new GameObject("AncientRuins");
            ruinsRoom.transform.parent = transform;

            // Calculate spawn position based on current depth
            float spawnDepth = minDepthToSpawn; // depthManager removed
            Vector3 roomCenter = CalculateRoomPosition(spawnDepth);

            ruinsRoom.transform.position = roomCenter;

            // Generate room structure
            GenerateFloor(roomCenter);
            GenerateWalls(roomCenter);
            GenerateCeiling(roomCenter);

            // Add props and decorations
            SpawnGlyphs(roomCenter);
            SpawnAncientMachinery(roomCenter);
            SpawnStoryItem(roomCenter);

            // Add atmospheric lighting
            SpawnEerieLighting(roomCenter);

            // Play discovery sound
            PlayDiscoveryEffects();

            // Trigger story event
            TriggerStoryEvent();

            Debug.Log($"[AncientRuinsGenerator] Ancient Ruins room generated at depth {spawnDepth}m");
        }

        private Vector3 CalculateRoomPosition(float depth)
        {
            // Position the room below the current dig grid
            // Centered in X and Z, at the appropriate Y depth
            return new Vector3(0f, -depth, 0f);
        }

        private void GenerateFloor(Vector3 center)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "RuinsFloor";
            floor.transform.parent = ruinsRoom.transform;
            floor.transform.position = center + new Vector3(0f, -roomHeight * 0.5f, 0f);
            floor.transform.localScale = new Vector3(roomWidth, 0.5f, roomLength);

            ApplyMaterial(floor, stoneFloorMaterial, new Color(0.25f, 0.2f, 0.28f));

            // Remove collider so player can walk on it
            Collider col = floor.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;
        }

        private void GenerateWalls(Vector3 center)
        {
            float halfWidth = roomWidth * 0.5f;
            float halfLength = roomLength * 0.5f;
            float halfHeight = roomHeight * 0.5f;

            // Create 4 walls
            CreateWall("Wall_North", center + new Vector3(0f, 0f, halfLength),
                       new Vector3(roomWidth, roomHeight, 0.5f), ruinsRoom.transform);
            CreateWall("Wall_South", center + new Vector3(0f, 0f, -halfLength),
                       new Vector3(roomWidth, roomHeight, 0.5f), ruinsRoom.transform);
            CreateWall("Wall_East", center + new Vector3(halfWidth, 0f, 0f),
                       new Vector3(0.5f, roomHeight, roomLength), ruinsRoom.transform);
            CreateWall("Wall_West", center + new Vector3(-halfWidth, 0f, 0f),
                       new Vector3(0.5f, roomHeight, roomLength), ruinsRoom.transform);
        }

        private GameObject CreateWall(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.parent = parent;
            wall.transform.position = position;
            wall.transform.localScale = scale;

            ApplyMaterial(wall, stoneWallMaterial, new Color(0.2f, 0.18f, 0.22f));

            return wall;
        }

        private void GenerateCeiling(Vector3 center)
        {
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "RuinsCeiling";
            ceiling.transform.parent = ruinsRoom.transform;
            ceiling.transform.position = center + new Vector3(0f, roomHeight * 0.5f + 0.25f, 0f);
            ceiling.transform.localScale = new Vector3(roomWidth, 0.5f, roomLength);

            ApplyMaterial(ceiling, stoneWallMaterial, new Color(0.15f, 0.12f, 0.18f));
        }

        private void SpawnGlyphs(Vector3 center)
        {
            int glyphCount = Random.Range(4, 8);
            float halfWidth = roomWidth * 0.5f - 0.6f;
            float halfLength = roomLength * 0.5f - 0.6f;

            for (int i = 0; i < glyphCount; i++)
            {
                // Place glyphs on walls
                Vector3 glyphPos;
                Quaternion glyphRot;

                int wallIndex = i % 4;
                float offset = Random.Range(-halfWidth * 0.7f, halfWidth * 0.7f);
                float heightOffset = Random.Range(-0.5f, 1f);

                switch (wallIndex)
                {
                    case 0: // North
                        glyphPos = center + new Vector3(offset, heightOffset, halfLength - 0.3f);
                        glyphRot = Quaternion.Euler(0f, 180f, 0f);
                        break;
                    case 1: // South
                        glyphPos = center + new Vector3(offset, heightOffset, -halfLength + 0.3f);
                        glyphRot = Quaternion.identity;
                        break;
                    case 2: // East
                        glyphPos = center + new Vector3(halfWidth - 0.3f, heightOffset, offset);
                        glyphRot = Quaternion.Euler(0f, -90f, 0f);
                        break;
                    default: // West
                        glyphPos = center + new Vector3(-halfWidth + 0.3f, heightOffset, offset);
                        glyphRot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                }

                CreateGlyph(glyphPos, glyphRot);
            }
        }

        private void CreateGlyph(Vector3 position, Quaternion rotation)
        {
            if (glyphPrefab != null)
            {
                Instantiate(glyphPrefab, position, rotation, ruinsRoom.transform);
                return;
            }

            // Create a simple quad with emissive material as glyph
            GameObject glyph = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glyph.name = "Glyph";
            glyph.transform.parent = ruinsRoom.transform;
            glyph.transform.position = position;
            glyph.transform.rotation = rotation;
            glyph.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            // Remove collider
            Collider col = glyph.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Create glowing material
            MeshRenderer renderer = glyph.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (glyphMaterial != null)
                {
                    renderer.material = glyphMaterial;
                }
                else
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = eeriePointLightColor;
                    mat.SetColor("_EmissionColor", eeriePointLightColor * 2f);
                    mat.EnableKeyword("_EMISSION");
                    renderer.material = mat;
                }
            }
        }

        private void SpawnAncientMachinery(Vector3 center)
        {
            // Spawn machinery in corners
            Vector3[] cornerOffsets = new Vector3[]
            {
                new Vector3(roomWidth * 0.3f, -roomHeight * 0.25f, roomLength * 0.3f),
                new Vector3(-roomWidth * 0.3f, -roomHeight * 0.25f, roomLength * 0.3f),
                new Vector3(roomWidth * 0.3f, -roomHeight * 0.25f, -roomLength * 0.3f),
                new Vector3(-roomWidth * 0.3f, -roomHeight * 0.25f, -roomLength * 0.3f)
            };

            int machineCount = Mathf.Min(cornerOffsets.Length, 3);

            for (int i = 0; i < machineCount; i++)
            {
                Vector3 spawnPos = center + cornerOffsets[i];

                if (ancientMachineryPrefabs != null && ancientMachineryPrefabs.Length > 0)
                {
                    int prefabIndex = Random.Range(0, ancientMachineryPrefabs.Length);
                    if (ancientMachineryPrefabs[prefabIndex] != null)
                    {
                        Instantiate(ancientMachineryPrefabs[prefabIndex], spawnPos,
                                   Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), ruinsRoom.transform);
                        continue;
                    }
                }

                // Create placeholder machinery
                CreatePlaceholderMachinery(spawnPos);
            }
        }

        private void CreatePlaceholderMachinery(Vector3 position)
        {
            GameObject machine = new GameObject("AncientMachine");
            machine.transform.parent = ruinsRoom.transform;
            machine.transform.position = position;

            // Base
            GameObject machineBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            machineBase.name = "MachineBase";
            machineBase.transform.parent = machine.transform;
            machineBase.transform.localPosition = Vector3.zero;
            machineBase.transform.localScale = new Vector3(1f, 0.3f, 1f);
            ApplyMaterial(machineBase, null, new Color(0.3f, 0.25f, 0.35f));

            // Column
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "MachineColumn";
            column.transform.parent = machine.transform;
            column.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            column.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
            ApplyMaterial(column, null, new Color(0.25f, 0.2f, 0.3f));

            // Top orb (emissive)
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "MachineOrb";
            orb.transform.parent = machine.transform;
            orb.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            orb.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            MeshRenderer orbRenderer = orb.GetComponent<MeshRenderer>();
            if (orbRenderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = eeriePointLightColor;
                mat.SetColor("_EmissionColor", eeriePointLightColor * 3f);
                mat.EnableKeyword("_EMISSION");
                orbRenderer.material = mat;
            }

            // Add point light to orb
            Light orbLight = orb.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.color = eeriePointLightColor;
            orbLight.intensity = 0.5f;
            orbLight.range = 3f;
        }

        private void SpawnStoryItem(Vector3 center)
        {
            // Place story item in center of room
            Vector3 itemPos = center + new Vector3(0f, -roomHeight * 0.35f, 0f);

            GameObject storyItem;
            if (storyItemPrefab != null)
            {
                storyItem = Instantiate(storyItemPrefab, itemPos, Quaternion.identity, ruinsRoom.transform);
            }
            else
            {
                // Create placeholder story item
                storyItem = new GameObject("RuinsStoryItem");
                storyItem.transform.parent = ruinsRoom.transform;
                storyItem.transform.position = itemPos;

                // Pedestal
                GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pedestal.name = "Pedestal";
                pedestal.transform.parent = storyItem.transform;
                pedestal.transform.localPosition = Vector3.zero;
                pedestal.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
                ApplyMaterial(pedestal, null, new Color(0.25f, 0.22f, 0.28f));

                // Artifact on top
                GameObject artifact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                artifact.name = "AncientArtifact";
                artifact.transform.parent = storyItem.transform;
                artifact.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                artifact.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                MeshRenderer artRenderer = artifact.GetComponent<MeshRenderer>();
                if (artRenderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(1f, 0.9f, 0.5f);
                    mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.3f) * 2f);
                    mat.EnableKeyword("_EMISSION");
                    artRenderer.material = mat;
                }

                // Add collider and interaction trigger
                BoxCollider trigger = storyItem.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2f, 2f, 2f);

                // Add story item pickup component
                Interaction.StoryItemPickup pickup = storyItem.AddComponent<Interaction.StoryItemPickup>();
            }

            // Add spotlight pointing at item
            GameObject spotlightObj = new GameObject("ItemSpotlight");
            spotlightObj.transform.parent = ruinsRoom.transform;
            spotlightObj.transform.position = itemPos + new Vector3(0f, 3f, 0f);
            spotlightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light spotlight = spotlightObj.AddComponent<Light>();
            spotlight.type = LightType.Spot;
            spotlight.color = new Color(1f, 0.95f, 0.8f);
            spotlight.intensity = 2f;
            spotlight.range = 5f;
            spotlight.spotAngle = 45f;
        }

        private void SpawnEerieLighting(Vector3 center)
        {
            // Central ambient light
            GameObject centralLight = new GameObject("EerieAmbientLight");
            centralLight.transform.parent = ruinsRoom.transform;
            centralLight.transform.position = center + new Vector3(0f, roomHeight * 0.3f, 0f);

            Light ambLight = centralLight.AddComponent<Light>();
            ambLight.type = LightType.Point;
            ambLight.color = eerieAmbientColor;
            ambLight.intensity = eeriePointLightIntensity;
            ambLight.range = roomWidth * 1.5f;

            // Corner lights for atmosphere
            Vector3[] corners = new Vector3[]
            {
                new Vector3(roomWidth * 0.4f, 0f, roomLength * 0.4f),
                new Vector3(-roomWidth * 0.4f, 0f, roomLength * 0.4f),
                new Vector3(roomWidth * 0.4f, 0f, -roomLength * 0.4f),
                new Vector3(-roomWidth * 0.4f, 0f, -roomLength * 0.4f)
            };

            foreach (var corner in corners)
            {
                GameObject cornerLight = new GameObject("CornerLight");
                cornerLight.transform.parent = ruinsRoom.transform;
                cornerLight.transform.position = center + corner;

                Light light = cornerLight.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = eeriePointLightColor;
                light.intensity = 0.5f;
                light.range = 4f;
            }
        }

        private void ApplyMaterial(GameObject obj, Material mat, Color fallbackColor)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (mat != null)
                {
                    renderer.material = mat;
                }
                else
                {
                    renderer.material.color = fallbackColor;
                }
            }
        }

        private void PlayDiscoveryEffects()
        {
            // Play discovery sound
            if (audioManager != null && ruinsDiscoveredSound != null)
            {
                audioManager.PlaySFX(ruinsDiscoveredSound);
            }

            // Flash effect - DISABLED (DepthLightingController removed)
            // TODO: reconnect to NEW digging system later (old digging code removed).
            // if (DepthLightingController.Instance != null)
            // {
            //     DepthLightingController.Instance.FlashLight(eeriePointLightColor, 1f);
            // }

            Debug.Log("[AncientRuinsGenerator] Playing discovery effects");
        }

        private void TriggerStoryEvent()
        {
            // Create and trigger story item for ruins discovery
            StoryItem ruinsItem = new StoryItem
            {
                itemId = "ruins_discovered",
                title = "Ancient Ruins Discovered",
                description = "You have discovered the Ancient Ruins. Strange machinery hums with power that has endured for millennia. The Keepers once walked these halls...",
                depthFound = Mathf.FloorToInt(minDepthToSpawn), // depthManager removed
                isCollected = true
            };

            GameEvents.OnStoryItemFound?.Invoke(ruinsItem);

            Debug.Log("[AncientRuinsGenerator] Story event 'Ruins Discovered' triggered - Chapter 2 unlocked");
        }

        /// <summary>
        /// Manually trigger ruins generation (for testing).
        /// </summary>
        [ContextMenu("Generate Ruins Now")]
        public void ForceGenerateRuins()
        {
            hasSpawned = false;
            GenerateRuinsRoom();
        }

        /// <summary>
        /// Destroy generated ruins.
        /// </summary>
        public void DestroyRuins()
        {
            if (ruinsRoom != null)
            {
                Destroy(ruinsRoom);
                ruinsRoom = null;
            }
            hasSpawned = false;
        }
    }
}
