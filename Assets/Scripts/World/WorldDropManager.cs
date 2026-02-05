using UnityEngine;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Digging;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.World
{
    /// <summary>
    /// Manages spawning dropped items in the world.
    /// Uses pickup prefabs from ResourceSystemConfig for material items.
    /// </summary>
    public class WorldDropManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float dropDistance = 2f;
        [SerializeField] private float dropHeight = 1.5f;
        [SerializeField] private float dropForce = 3f;
        [SerializeField] private float upwardForce = 2f;

        [Header("Resource System Config")]
        [Tooltip("Reference to ResourceSystemConfig - loads from Resources if not assigned")]
        [SerializeField] private ResourceSystemConfig resourceSystemConfig;

        [Header("Prefab")]
        [SerializeField] private GameObject worldItemPrefab;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        public static WorldDropManager Instance { get; private set; }

        private Transform playerTransform;
        private Camera playerCamera;

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
            // Find player
            FindPlayer();

            // Load ResourceSystemConfig from Resources if not assigned
            if (resourceSystemConfig == null)
            {
                resourceSystemConfig = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
                if (debugMode && resourceSystemConfig != null)
                {
                    Debug.Log("[WorldDropManager] Loaded ResourceSystemConfig from Resources");
                }
            }

            // Create default prefab if not assigned
            if (worldItemPrefab == null)
            {
                CreateDefaultPrefab();
            }
        }

        private void FindPlayer()
        {
            // Try to find player by tag
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // Find main camera
            playerCamera = Camera.main;
        }

        private void CreateDefaultPrefab()
        {
            // Create a simple world item prefab (used as template)
            worldItemPrefab = new GameObject("WorldItemPrefab");

            // Add mesh - use sphere for a more natural look
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(worldItemPrefab.transform);
            visual.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            visual.transform.localPosition = Vector3.zero;

            // Remove the default collider from visual
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }

            // Setup rigidbody
            Rigidbody rb = worldItemPrefab.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.drag = 1f;
            rb.angularDrag = 0.5f;

            // Add collider
            SphereCollider collider = worldItemPrefab.AddComponent<SphereCollider>();
            collider.radius = 0.25f;

            // Add WorldItem component
            WorldItem worldItem = worldItemPrefab.AddComponent<WorldItem>();

            // Deactivate the prefab
            worldItemPrefab.SetActive(false);

            if (debugMode)
            {
                Debug.Log("[WorldDropManager] Created default WorldItem prefab");
            }
        }

        /// <summary>
        /// Creates a visual pickup object for an item using its icon as a billboard sprite.
        /// </summary>
        private GameObject CreateItemVisual(ItemSO item, Vector3 position)
        {
            GameObject obj = new GameObject($"DroppedItem_{item.itemName}");
            obj.transform.position = position;

            // Create visual based on icon
            if (item.icon != null)
            {
                // Create a sprite renderer for the icon
                GameObject spriteObj = new GameObject("IconSprite");
                spriteObj.transform.SetParent(obj.transform);
                spriteObj.transform.localPosition = Vector3.zero;
                spriteObj.transform.localScale = Vector3.one * 0.5f;

                SpriteRenderer spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = item.icon;
                spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));

                // Add billboard behavior to always face camera
                spriteObj.AddComponent<BillboardSprite>();
            }
            else
            {
                // Fallback: Create a colored sphere based on item category
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                visual.transform.SetParent(obj.transform);
                visual.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                visual.transform.localPosition = Vector3.zero;

                // Remove collider from visual
                var visualCollider = visual.GetComponent<Collider>();
                if (visualCollider != null) Destroy(visualCollider);

                // Set color based on item name/category
                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Color itemColor = GetColorForItem(item);
                    renderer.material.color = itemColor;
                }
            }

            // Add physics
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.drag = 1f;
            rb.angularDrag = 0.5f;

            // Add collider
            SphereCollider collider = obj.AddComponent<SphereCollider>();
            collider.radius = 0.3f;

            // Add WorldItem
            WorldItem worldItem = obj.AddComponent<WorldItem>();
            worldItem.Initialize(item, 1);

            return obj;
        }

        /// <summary>
        /// Tries to map an ItemSO to an UndergroundResourceType based on its itemId.
        /// Returns true if a matching resource type was found.
        /// </summary>
        private bool TryGetResourceType(ItemSO item, out UndergroundResourceType resourceType)
        {
            resourceType = UndergroundResourceType.None;

            if (item == null || string.IsNullOrEmpty(item.itemId))
                return false;

            string id = item.itemId.ToLower();

            // Map itemId patterns to resource types
            // Items are named like "underground_stone", "underground_coal", etc.
            if (id.Contains("dirt")) { resourceType = UndergroundResourceType.Dirt; return true; }
            if (id.Contains("clay")) { resourceType = UndergroundResourceType.Clay; return true; }
            if (id.Contains("softstone") || id.Contains("soft_stone")) { resourceType = UndergroundResourceType.SoftStone; return true; }
            if (id.Contains("hardstone") || id.Contains("hard_stone")) { resourceType = UndergroundResourceType.HardStone; return true; }
            if (id.Contains("deepblackstone") || id.Contains("deep_black_stone")) { resourceType = UndergroundResourceType.DeepBlackStone; return true; }
            if (id.Contains("stone")) { resourceType = UndergroundResourceType.Stone; return true; }
            if (id.Contains("coal")) { resourceType = UndergroundResourceType.Coal; return true; }
            if (id.Contains("ironchunk") || id.Contains("iron_chunk")) { resourceType = UndergroundResourceType.IronChunk; return true; }
            if (id.Contains("ironnugget") || id.Contains("iron_nugget")) { resourceType = UndergroundResourceType.IronNugget; return true; }
            if (id.Contains("iron")) { resourceType = UndergroundResourceType.IronChunk; return true; }
            if (id.Contains("copperpiece") || id.Contains("copper_piece")) { resourceType = UndergroundResourceType.CopperPiece; return true; }
            if (id.Contains("copperfragment") || id.Contains("copper_fragment")) { resourceType = UndergroundResourceType.CopperFragment; return true; }
            if (id.Contains("copper")) { resourceType = UndergroundResourceType.CopperFragment; return true; }
            if (id.Contains("quartz")) { resourceType = UndergroundResourceType.Quartz; return true; }
            if (id.Contains("crystalshard") || id.Contains("crystal_shard")) { resourceType = UndergroundResourceType.CrystalShard; return true; }
            if (id.Contains("crystaldust") || id.Contains("crystal_dust")) { resourceType = UndergroundResourceType.CrystalDust; return true; }
            if (id.Contains("crystal")) { resourceType = UndergroundResourceType.CrystalShard; return true; }
            if (id.Contains("ancientore") || id.Contains("ancient_ore")) { resourceType = UndergroundResourceType.AncientOre; return true; }
            if (id.Contains("hardsoil") || id.Contains("hard_soil")) { resourceType = UndergroundResourceType.HardSoil; return true; }
            if (id.Contains("sandstone") || id.Contains("sand_stone")) { resourceType = UndergroundResourceType.Sandstone; return true; }
            if (id.Contains("heatstone") || id.Contains("heat_stone")) { resourceType = UndergroundResourceType.Heatstone; return true; }

            return false;
        }

        /// <summary>
        /// Gets the pickup prefab from ResourceSystemConfig based on the item name.
        /// Searches the resources list for a matching resourceId or displayName,
        /// then returns the pickupPrefab from tier 1 (or first available tier).
        /// </summary>
        private GameObject GetPickupPrefabFromConfig(ItemSO item)
        {
            if (item == null || resourceSystemConfig == null)
                return null;

            string itemName = item.itemName;
            string itemId = item.itemId?.ToLower() ?? "";

            // Search through all resources in the config
            foreach (var resource in resourceSystemConfig.resources)
            {
                if (resource == null)
                    continue;

                // Match by displayName (e.g., "Stone", "Coal", "Iron Chunk")
                bool nameMatch = resource.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase);

                // Match by resourceId (e.g., "stone", "coal", "iron")
                bool idMatch = !string.IsNullOrEmpty(resource.resourceId) &&
                              (itemId.Contains(resource.resourceId.ToLower()) ||
                               resource.resourceId.ToLower().Equals(itemId.Replace("underground_", "")));

                if (nameMatch || idMatch)
                {
                    // Found matching resource - get pickup prefab from tiers
                    if (resource.tiers != null && resource.tiers.Length > 0)
                    {
                        // Try to find first tier with a pickup prefab
                        foreach (var tier in resource.tiers)
                        {
                            if (tier != null && tier.pickupPrefab != null)
                            {
                                if (debugMode)
                                {
                                    Debug.Log($"[WorldDropManager] Found pickup prefab for '{itemName}' " +
                                             $"from resource '{resource.displayName}' tier '{tier.tierName}'");
                                }
                                return tier.pickupPrefab;
                            }
                        }
                    }

                    if (debugMode)
                    {
                        Debug.Log($"[WorldDropManager] Found resource '{resource.displayName}' but no pickup prefab in tiers");
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log($"[WorldDropManager] No matching resource found in config for item: {itemName} (id: {itemId})");
            }

            return null;
        }

        /// <summary>
        /// Creates a resource pickup using the loaded prefab.
        /// </summary>
        private GameObject CreateResourcePickup(GameObject prefab, ItemSO item, int amount, Vector3 position)
        {
            if (prefab == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[WorldDropManager] No prefab provided for {item.itemName}");
                return null;
            }

            // Instantiate the prefab
            GameObject drop = Instantiate(prefab, position, Quaternion.identity);
            drop.SetActive(true);
            drop.name = $"DroppedItem_{item.itemName}";
            drop.transform.localScale = Vector3.one;

            // Setup rigidbody
            Rigidbody rb = drop.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = drop.AddComponent<Rigidbody>();
            }
            rb.mass = 0.5f;
            rb.drag = 0.5f;
            rb.angularDrag = 0.5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Setup colliders
            var allColliders = drop.GetComponentsInChildren<Collider>(true);
            foreach (var col in allColliders)
            {
                col.isTrigger = false;
                col.enabled = true;

                if (col is SphereCollider sphereCol)
                {
                    sphereCol.radius = 0.15f;
                    sphereCol.center = Vector3.zero;
                }
            }

            // If no collider exists, add one
            if (allColliders.Length == 0)
            {
                var sphereCol = drop.AddComponent<SphereCollider>();
                sphereCol.radius = 0.15f;
            }

            // Add WorldItem component for pickup (not ResourcePickup - we want ItemSO-based pickup)
            WorldItem worldItem = drop.GetComponent<WorldItem>();
            if (worldItem == null)
            {
                worldItem = drop.AddComponent<WorldItem>();
            }
            worldItem.Initialize(item, amount);

            // Remove any ResourcePickup component that might be on the prefab
            var resourcePickup = drop.GetComponent<ResourcePickup>();
            if (resourcePickup != null)
            {
                Destroy(resourcePickup);
            }

            // Set layer for raycast detection
            drop.layer = 0;

            if (debugMode)
            {
                Debug.Log($"[WorldDropManager] Created pickup for {item.itemName} using prefab {prefab.name}");
            }

            return drop;
        }

        /// <summary>
        /// Gets a color for an item based on its name/type.
        /// </summary>
        private Color GetColorForItem(ItemSO item)
        {
            string lowerName = item.itemName.ToLower();

            if (lowerName.Contains("stone") || lowerName.Contains("rock"))
                return new Color(0.5f, 0.5f, 0.5f); // Gray
            if (lowerName.Contains("iron"))
                return new Color(0.6f, 0.55f, 0.5f); // Iron gray
            if (lowerName.Contains("copper"))
                return new Color(0.8f, 0.5f, 0.3f); // Copper orange
            if (lowerName.Contains("coal"))
                return new Color(0.15f, 0.15f, 0.15f); // Dark gray/black
            if (lowerName.Contains("gold"))
                return new Color(1f, 0.84f, 0f); // Gold
            if (lowerName.Contains("silver"))
                return new Color(0.75f, 0.75f, 0.8f); // Silver
            if (lowerName.Contains("crystal") || lowerName.Contains("quartz"))
                return new Color(0.7f, 0.85f, 1f); // Light blue crystal
            if (lowerName.Contains("dirt") || lowerName.Contains("soil"))
                return new Color(0.4f, 0.3f, 0.2f); // Brown
            if (lowerName.Contains("clay"))
                return new Color(0.6f, 0.4f, 0.3f); // Clay brown

            // Default brownish color for materials
            return new Color(0.5f, 0.4f, 0.35f);
        }

        /// <summary>
        /// Drop an item into the world in front of the player.
        /// For material items, uses the same pickup prefabs as the digging system.
        /// </summary>
        public void DropItem(ItemSO item, int amount)
        {
            if (item == null || amount <= 0)
            {
                Debug.LogWarning("[WorldDropManager] Cannot drop null or zero-amount item");
                return;
            }

            // Make sure we have player reference
            if (playerTransform == null || playerCamera == null)
            {
                FindPlayer();
            }

            // Calculate drop position
            Vector3 dropPosition = CalculateDropPosition();

            GameObject droppedObj = null;

            // First priority: Get pickup prefab from ResourceSystemConfig
            // This ensures dropped items look identical to pickups from digging
            if (item.category == ItemCategory.Material)
            {
                GameObject pickupPrefab = GetPickupPrefabFromConfig(item);
                if (pickupPrefab != null)
                {
                    droppedObj = CreateResourcePickup(pickupPrefab, item, amount, dropPosition);
                    if (debugMode && droppedObj != null)
                    {
                        Debug.Log($"[WorldDropManager] Used pickup prefab from ResourceSystemConfig for {item.itemName}");
                    }
                }
            }

            // Second priority: Use item's worldPrefab if available
            if (droppedObj == null && item.worldPrefab != null)
            {
                droppedObj = Instantiate(item.worldPrefab, dropPosition, Quaternion.identity);
                droppedObj.name = $"DroppedItem_{item.itemName}";

                // Ensure it has required components for pickup
                EnsurePickupComponents(droppedObj, item, amount);
            }

            // Last resort: Create a visual pickup based on item's icon or color
            if (droppedObj == null)
            {
                droppedObj = CreateItemVisual(item, dropPosition);
            }

            // Apply force to throw it forward
            Rigidbody rb = droppedObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 throwDirection = GetThrowDirection();
                rb.AddForce(throwDirection * dropForce + Vector3.up * upwardForce, ForceMode.Impulse);

                // Add some random rotation
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
            }

            if (debugMode)
            {
                string method = "generated visual";
                if (GetPickupPrefabFromConfig(item) != null) method = "ResourceSystemConfig pickup";
                else if (item.worldPrefab != null) method = "item prefab";
                Debug.Log($"[WorldDropManager] Dropped {amount}x {item.itemName} at {dropPosition} (using {method})");
            }
        }

        /// <summary>
        /// Ensures a dropped object has all required components for pickup.
        /// </summary>
        private void EnsurePickupComponents(GameObject obj, ItemSO item, int amount)
        {
            // Ensure rigidbody
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = obj.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.drag = 1f;
                rb.angularDrag = 0.5f;
            }
            // Make sure rigidbody isn't kinematic so physics works
            rb.isKinematic = false;
            rb.useGravity = true;

            // Ensure collider - prefer a reasonably-sized collider for raycast detection
            Collider col = obj.GetComponent<Collider>();
            if (col == null)
            {
                // Add a sphere collider as default
                SphereCollider sphereCol = obj.AddComponent<SphereCollider>();
                sphereCol.radius = 0.4f; // Larger for easier detection
            }
            else
            {
                // Make sure existing collider is NOT a trigger (so physics works)
                // WorldItem/InteractionSystem works with both, but non-trigger is safer
                col.isTrigger = false;
            }

            // Remove any ResourcePickup component to avoid conflicts
            var resourcePickup = obj.GetComponent<Digging.ResourcePickup>();
            if (resourcePickup != null)
            {
                Destroy(resourcePickup);
                if (debugMode) Debug.Log($"[WorldDropManager] Removed conflicting ResourcePickup component");
            }

            // Ensure WorldItem component for IInteractable pickup
            WorldItem worldItem = obj.GetComponent<WorldItem>();
            if (worldItem == null)
            {
                worldItem = obj.AddComponent<WorldItem>();
            }
            worldItem.Initialize(item, amount);

            // Set layer to Default (0) to ensure raycast can hit it
            obj.layer = 0;

            obj.SetActive(true);

            if (debugMode)
            {
                Debug.Log($"[WorldDropManager] EnsurePickupComponents: {item.itemName} - WorldItem added, collider={col?.GetType().Name ?? "SphereCollider"}, layer={obj.layer}");
            }
        }

        private Vector3 CalculateDropPosition()
        {
            Vector3 dropPos;

            if (playerCamera != null)
            {
                // Drop in front of camera
                dropPos = playerCamera.transform.position + playerCamera.transform.forward * dropDistance;
            }
            else if (playerTransform != null)
            {
                // Drop in front of player
                dropPos = playerTransform.position + playerTransform.forward * dropDistance + Vector3.up * dropHeight;
            }
            else
            {
                // Fallback - drop at origin with some height
                dropPos = Vector3.up * 2f;
            }

            return dropPos;
        }

        private Vector3 GetThrowDirection()
        {
            if (playerCamera != null)
            {
                return playerCamera.transform.forward;
            }
            else if (playerTransform != null)
            {
                return playerTransform.forward;
            }
            return Vector3.forward;
        }

        /// <summary>
        /// Spawn a world item at a specific position.
        /// </summary>
        public void SpawnItemAt(ItemSO item, int amount, Vector3 position)
        {
            if (item == null || amount <= 0) return;

            if (worldItemPrefab == null)
            {
                CreateDefaultPrefab();
            }

            GameObject droppedObj = Instantiate(worldItemPrefab, position, Quaternion.identity);
            droppedObj.SetActive(true);

            WorldItem worldItem = droppedObj.GetComponent<WorldItem>();
            if (worldItem != null)
            {
                worldItem.Initialize(item, amount);
            }

            if (debugMode)
            {
                Debug.Log($"[WorldDropManager] Spawned {amount}x {item.itemName} at {position}");
            }
        }
    }
}
