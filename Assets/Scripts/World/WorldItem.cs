using UnityEngine;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Interaction;

// Alias for ResourceType which is in the root BeneathTheFloor namespace
using ResourceType = BeneathTheFloor.ResourceType;

namespace BeneathTheFloor.World
{
    /// <summary>
    /// A world object representing a dropped item that can be picked up by the player.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class WorldItem : MonoBehaviour, IInteractable
    {
        [Header("Item Data")]
        [SerializeField] private ItemSO item;
        [SerializeField] private int amount = 1;

        [Header("Visuals")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.1f;
        [SerializeField] private float rotateSpeed = 30f;
        [SerializeField] private bool enableBobbing = true;
        [SerializeField] private bool enableRotation = true;

        [Header("Pickup Settings")]
        [SerializeField] private bool pickupOnTouch = false;
        [SerializeField] private float pickupCooldown = 0.5f;

        private Vector3 startPosition;
        private float spawnTime;
        private bool canPickup = false;
        private Rigidbody rb;

        public ItemSO Item => item;
        public int Amount => amount;

        // IInteractable implementation
        public bool CanInteract => canPickup && item != null;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            startPosition = transform.position;
            spawnTime = Time.time;

            // Setup rigidbody
            if (rb != null)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.drag = 1f;
            }

            // Setup collider
            var collider = GetComponent<Collider>();
            if (collider != null && pickupOnTouch)
            {
                collider.isTrigger = true;
            }

            // Apply pickup cooldown
            Invoke(nameof(EnablePickup), pickupCooldown);

            // Setup visuals based on item
            SetupVisuals();
        }

        private void Update()
        {
            if (!enableBobbing && !enableRotation) return;

            // Only bob and rotate after landing
            if (rb != null && rb.velocity.magnitude < 0.1f)
            {
                // Bobbing
                if (enableBobbing)
                {
                    float bob = Mathf.Sin((Time.time - spawnTime) * bobSpeed) * bobHeight;
                    transform.position = new Vector3(
                        transform.position.x,
                        startPosition.y + bob,
                        transform.position.z
                    );
                }

                // Rotation
                if (enableRotation)
                {
                    transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
                }
            }
            else if (rb != null)
            {
                // Update start position while falling
                startPosition = transform.position;
            }
        }

        private void EnablePickup()
        {
            canPickup = true;
            Debug.Log($"[WorldItem] Pickup enabled for {(item != null ? item.itemName : "unknown")}");
        }

        public void Initialize(ItemSO itemData, int itemAmount)
        {
            item = itemData;
            amount = itemAmount;

            // If Start already ran but item wasn't set, schedule pickup enable now
            if (!canPickup && !IsInvoking(nameof(EnablePickup)))
            {
                Invoke(nameof(EnablePickup), pickupCooldown);
            }

            SetupVisuals();

            Debug.Log($"[WorldItem] Initialized: {amount}x {(item != null ? item.itemName : "NULL")} (canPickup will enable in {pickupCooldown}s)");
        }

        private void SetupVisuals()
        {
            if (item == null) return;

            // Try to find mesh renderer if not assigned
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            // Try to use item's world prefab
            if (item.worldPrefab != null)
            {
                // Could instantiate the world prefab as a child
                // For now, just use the existing mesh
            }

            // Set material color based on item - try resource-specific color first
            if (meshRenderer != null)
            {
                Color itemColor = GetItemColor(item);
                meshRenderer.material.color = itemColor;

                // Add slight emission for visibility
                meshRenderer.material.EnableKeyword("_EMISSION");
                meshRenderer.material.SetColor("_EmissionColor", itemColor * 0.3f);
            }

            // Set name
            gameObject.name = $"WorldItem_{item.itemName}_{amount}";
        }

        /// <summary>
        /// Get a color for the item, using resource-specific colors for materials.
        /// </summary>
        private Color GetItemColor(ItemSO item)
        {
            // Check if it's a MaterialItemSO with a source resource
            if (item is MaterialItemSO matItem && matItem.sourceResource.HasValue)
            {
                return GetResourceColor(matItem.sourceResource.Value);
            }

            // Fall back to category color
            return GetCategoryColor(item.category);
        }

        /// <summary>
        /// Get color based on resource type for better visual identification.
        /// </summary>
        private Color GetResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                // Raw materials
                ResourceType.Dirt => new Color(0.55f, 0.40f, 0.25f),      // Brown dirt
                ResourceType.Clay => new Color(0.75f, 0.55f, 0.40f),      // Orange-brown clay
                ResourceType.Coal => new Color(0.15f, 0.15f, 0.15f),      // Dark gray/black
                ResourceType.IronOre => new Color(0.60f, 0.45f, 0.40f),   // Rusty brown
                ResourceType.Copper => new Color(0.85f, 0.55f, 0.30f),    // Copper orange
                ResourceType.Silver => new Color(0.80f, 0.80f, 0.85f),    // Silver gray
                ResourceType.Gold => new Color(1.0f, 0.85f, 0.30f),       // Gold yellow

                // Refined materials
                ResourceType.IronIngot => new Color(0.55f, 0.55f, 0.60f), // Steel gray
                ResourceType.CopperIngot => new Color(0.90f, 0.60f, 0.35f), // Polished copper
                ResourceType.SilverIngot => new Color(0.90f, 0.90f, 0.95f), // Bright silver
                ResourceType.GoldIngot => new Color(1.0f, 0.90f, 0.45f),  // Bright gold

                // Processed
                ResourceType.CompressedDirt => new Color(0.45f, 0.35f, 0.20f),
                ResourceType.HardenedClay => new Color(0.70f, 0.45f, 0.30f),
                ResourceType.RefinedCoal => new Color(0.25f, 0.25f, 0.30f),

                // Special
                ResourceType.AncientArtifact => new Color(0.70f, 0.50f, 0.90f), // Purple

                _ => new Color(0.65f, 0.55f, 0.45f) // Default tan
            };
        }

        private Color GetCategoryColor(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Tool:
                    return new Color(0.4f, 0.7f, 1f); // Blue
                case ItemCategory.Material:
                    return new Color(0.8f, 0.6f, 0.4f); // Brown
                case ItemCategory.Consumable:
                    return new Color(0.4f, 1f, 0.4f); // Green
                case ItemCategory.Equipment:
                    return new Color(1f, 0.8f, 0.4f); // Orange
                default:
                    return new Color(0.7f, 0.7f, 0.7f); // Gray
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!pickupOnTouch || !canPickup) return;

            // Check if player
            if (other.CompareTag("Player"))
            {
                TryPickup();
            }
        }

        public void TryPickup()
        {
            if (!canPickup || item == null) return;

            // Try to add to inventory
            if (InventorySystem.Instance != null)
            {
                if (InventorySystem.Instance.TryAddItemToGrid(item, amount))
                {
                    Debug.Log($"[WorldItem] Picked up {amount}x {item.itemName}");

                    // Play pickup sound if available
                    // AudioManager.Instance?.PlaySFX("ItemPickup");

                    // Destroy this world item
                    Destroy(gameObject);
                }
                else
                {
                    Debug.Log($"[WorldItem] Inventory full, cannot pick up {item.itemName}");
                    GameEvents.OnInventoryFull?.Invoke();
                }
            }
        }

        #region IInteractable Implementation

        public string GetInteractionText()
        {
            if (item == null) return "Pick up";
            return $"Pick up {item.itemName}" + (amount > 1 ? $" x{amount}" : "");
        }

        public void Interact(GameObject interactor)
        {
            TryPickup();
        }

        public void OnHoverEnter()
        {
            // Could add highlight effect
        }

        public void OnHoverExit()
        {
            // Remove highlight effect
        }

        #endregion
    }
}
