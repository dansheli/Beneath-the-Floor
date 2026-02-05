using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Logistics
{
    [System.Serializable]
    public struct ContainerSlot
    {
        public string resourceId;
        public string displayName;
        public int tier;
        public int quantity;
        public int creditValuePerUnit;
        public Sprite icon;
    }

    public class DropOffContainer : MonoBehaviour, IInteractable
    {
        public static DropOffContainer Instance { get; private set; }

        private Dictionary<string, ContainerSlot> inventory = new Dictionary<string, ContainerSlot>();
        private Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();

        private int nextGridSlot;
        private const int MaxGridSlots = 12;
        private const float GridSpacing = 0.35f;

        private ResourceSystemConfig _resourceConfig;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }

            _resourceConfig = Resources.Load<ResourceSystemConfig>("ResourceSystemConfig");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ReceiveCargo(List<CargoEntry> cargo)
        {
            foreach (var entry in cargo)
            {
                if (inventory.ContainsKey(entry.resourceId))
                {
                    var slot = inventory[entry.resourceId];
                    slot.quantity += entry.quantity;
                    inventory[entry.resourceId] = slot;
                }
                else
                {
                    // Look up display name, icon, and credit value from ResourceSystemConfig
                    string displayName = entry.resourceId;
                    Sprite icon = null;
                    int creditVal = entry.creditValuePerUnit;

                    if (_resourceConfig != null)
                    {
                        var resDef = _resourceConfig.GetResourceById(entry.resourceId);
                        if (resDef != null)
                        {
                            displayName = resDef.displayName;
                            icon = resDef.icon;
                            if (resDef.creditValuePerUnit > 0)
                                creditVal = resDef.creditValuePerUnit;
                        }
                    }

                    inventory[entry.resourceId] = new ContainerSlot
                    {
                        resourceId = entry.resourceId,
                        displayName = displayName,
                        tier = entry.tier,
                        quantity = entry.quantity,
                        creditValuePerUnit = creditVal,
                        icon = icon
                    };
                    SpawnVisual(entry.resourceId, entry.tier);
                }
            }
            Debug.Log($"[DropOffContainer] Received cargo, now holding {inventory.Count} types");
        }

        public Dictionary<string, ContainerSlot> GetInventory()
        {
            return new Dictionary<string, ContainerSlot>(inventory);
        }

        public int GetTotalValue()
        {
            int total = 0;
            foreach (var kv in inventory)
                total += kv.Value.quantity * kv.Value.creditValuePerUnit;
            return total;
        }

        public bool TakeAll()
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return false;

            bool anyTaken = false;
            var toRemove = new List<string>();

            foreach (var kv in inventory)
            {
                ResourceType resType = MapResourceIdToType(kv.Key);
                bool added = inv.AddResource(resType, kv.Value.quantity);
                if (added)
                {
                    anyTaken = true;
                    toRemove.Add(kv.Key);
                }
            }

            foreach (var key in toRemove)
            {
                inventory.Remove(key);
                RemoveVisual(key);
            }

            return anyTaken;
        }

        public bool TakeType(string resourceId)
        {
            if (!inventory.ContainsKey(resourceId)) return false;
            var slot = inventory[resourceId];

            var inv = InventorySystem.Instance;
            if (inv == null) return false;

            ResourceType resType = MapResourceIdToType(resourceId);
            bool added = inv.AddResource(resType, slot.quantity);
            if (added)
            {
                inventory.Remove(resourceId);
                RemoveVisual(resourceId);
                return true;
            }
            return false;
        }

        private ResourceType MapResourceIdToType(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return ResourceType.Dirt;
            return resourceId.ToLower() switch
            {
                "stone" => ResourceType.Stone,
                "iron" => ResourceType.IronOre,
                "copper" => ResourceType.Copper,
                "coal" => ResourceType.Coal,
                "silver" => ResourceType.Silver,
                "gold" => ResourceType.Gold,
                _ => ResourceType.Dirt
            };
        }

        // ====== Visual placement inside container ======

        private void SpawnVisual(string resourceId, int tier)
        {
            if (visuals.ContainsKey(resourceId)) return;
            if (nextGridSlot >= MaxGridSlots) return;

            GameObject vis = null;

            // Try to instantiate the actual pickup prefab from ResourceSystemConfig
            if (_resourceConfig != null)
            {
                var resDef = _resourceConfig.GetResourceById(resourceId);
                if (resDef != null && resDef.tiers != null)
                {
                    int tierIdx = Mathf.Clamp(tier - 1, 0, resDef.tiers.Length - 1);
                    var tierCfg = resDef.tiers[tierIdx];
                    if (tierCfg.pickupPrefab != null)
                    {
                        vis = Instantiate(tierCfg.pickupPrefab);
                        vis.transform.localScale = Vector3.one * tierCfg.pickupPrefabScale * 0.8f;
                    }
                }
            }

            // Fallback: simple colored cube
            if (vis == null)
            {
                vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vis.transform.localScale = Vector3.one * 0.15f;
                var renderer = vis.GetComponent<Renderer>();
                if (renderer != null)
                {
                    int hash = resourceId.GetHashCode();
                    renderer.material.color = new Color(
                        ((hash & 0xFF) / 255f) * 0.6f + 0.4f,
                        (((hash >> 8) & 0xFF) / 255f) * 0.6f + 0.4f,
                        (((hash >> 16) & 0xFF) / 255f) * 0.6f + 0.4f
                    );
                }
            }

            vis.name = $"ContainerVisual_{resourceId}";
            vis.transform.SetParent(transform, false);

            // Strip physics/pickup components so it's just a visual
            StripComponents(vis);

            // Grid layout inside the container box
            int col = nextGridSlot % 3;
            int row = nextGridSlot / 3;
            vis.transform.localPosition = new Vector3(
                -0.3f + col * GridSpacing,
                0.15f + row * 0.25f,
                0f
            );
            vis.transform.localRotation = Quaternion.identity;

            visuals[resourceId] = vis;
            nextGridSlot++;
        }

        private void StripComponents(GameObject obj)
        {
            // Remove Rigidbody, pickup scripts, colliders so it's purely visual
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            var nodePickup = obj.GetComponent<NodePickup>();
            if (nodePickup != null) Destroy(nodePickup);

            // Disable colliders (don't destroy - mesh collider might be needed for rendering)
            foreach (var col in obj.GetComponentsInChildren<Collider>())
                col.enabled = false;

            // Remove all custom scripts (Renderers are Components, not MonoBehaviours, so they're kept)
            foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb != null)
                    Destroy(mb);
            }
        }

        private void RemoveVisual(string resourceId)
        {
            if (visuals.TryGetValue(resourceId, out GameObject vis))
            {
                if (vis != null) Destroy(vis);
                visuals.Remove(resourceId);
            }
        }

        // IInteractable
        public bool CanInteract => inventory.Count > 0;

        public string GetInteractionText()
        {
            if (inventory.Count == 0)
                return "[E] Container (Empty)";
            int totalQty = 0;
            foreach (var kv in inventory) totalQty += kv.Value.quantity;
            return $"[E] Open Container ({totalQty} items, ${GetTotalValue()})";
        }

        public void Interact(GameObject interactor)
        {
            var ui = DropOffContainerUI.Instance;
            if (ui != null)
            {
                ui.ShowUI(this);
            }
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
    }
}
