using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.World;

namespace BeneathTheFloor.InventoryUI
{
    /// <summary>
    /// Controls the dragging of items between inventory slots.
    /// Tracks the currently held item stack and updates the drag icon.
    /// </summary>
    public class InventoryDragController : MonoBehaviour
    {
        [Header("Drag Icon")]
        [SerializeField] private GameObject dragIconObject;
        [SerializeField] private Image dragIcon;
        [SerializeField] private TextMeshProUGUI dragCountText;

        [Header("Settings")]
        [SerializeField] private Vector2 dragOffset = new Vector2(10, -10);

        private ItemStack heldStack;
        private int sourceSlotIndex = -1;
        private bool isDragging = false;
        private RectTransform inventoryPanelRect;
        private Canvas parentCanvas;

        public bool HasHeldStack => !heldStack.IsEmpty;
        public ItemStack HeldStack => heldStack;
        public int SourceSlotIndex => sourceSlotIndex;
        public bool IsDragging => isDragging;

        private void Start()
        {
            // Find parent canvas
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindObjectOfType<Canvas>();
            }

            // Find inventory panel rect
            if (InventoryUIManager.Instance != null)
            {
                var panel = InventoryUIManager.Instance.transform.Find("InventoryPanel");
                if (panel != null)
                {
                    inventoryPanelRect = panel.GetComponent<RectTransform>();
                }
            }

            heldStack = ItemStack.Empty;
        }

        private void Update()
        {
            // Update drag icon position when holding item
            if (HasHeldStack && dragIconObject != null && dragIconObject.activeSelf)
            {
                UpdateDragIconPosition();
            }

            // NOTE: We no longer drop on click in Update().
            // Dropping is now handled by:
            // 1. EndDrag() when dragging ends outside inventory
            // 2. InventorySlotUI when clicking outside while holding (via OnPointerUp)
            // This prevents accidental drops when clicking on slots.
        }

        public void SetupDragIcon(GameObject iconObj, Image icon, TextMeshProUGUI countText)
        {
            dragIconObject = iconObj;
            dragIcon = icon;
            dragCountText = countText;

            if (dragIconObject != null)
            {
                dragIconObject.SetActive(false);
            }
        }

        public void SetHeldStack(ItemStack stack, int fromSlot)
        {
            heldStack = stack;
            sourceSlotIndex = fromSlot;

            UpdateDragIconVisuals();

            if (HasHeldStack)
            {
                ShowDragIcon();
            }
            else
            {
                HideDragIcon();
            }
        }

        public void ClearHeldStack()
        {
            heldStack = ItemStack.Empty;
            sourceSlotIndex = -1;
            HideDragIcon();
        }

        public void StartDrag()
        {
            isDragging = true;
            ShowDragIcon();
        }

        public void EndDrag(PointerEventData eventData)
        {
            isDragging = false;

            if (!HasHeldStack) return;

            // Check if dropped on a valid target
            GameObject target = eventData.pointerCurrentRaycast.gameObject;

            Debug.Log($"[InventoryDragController] EndDrag - target: {(target != null ? target.name : "null")}, screen pos: {Input.mousePosition}");

            if (target == null)
            {
                // Dropped outside UI - drop to world
                Debug.Log("[InventoryDragController] EndDrag - no raycast target, dropping to world");
                DropItemToWorld();
                return;
            }

            // Check if dropped on trash slot
            var trashSlot = target.GetComponent<InventoryTrashSlot>();
            if (trashSlot == null)
            {
                trashSlot = target.GetComponentInParent<InventoryTrashSlot>();
            }

            if (trashSlot != null)
            {
                Debug.Log("[InventoryDragController] EndDrag - dropped on trash slot");
                trashSlot.RequestDestroy(heldStack);
                return;
            }

            // Check if dropped on inventory slot
            var slotUI = target.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                slotUI = target.GetComponentInParent<InventorySlotUI>();
            }

            if (slotUI != null)
            {
                // Drop handled by the slot's OnDrop
                Debug.Log($"[InventoryDragController] EndDrag - dropped on slot {slotUI.SlotIndex}, letting OnDrop handle it");
                return;
            }

            // Check if outside inventory panel using thorough check - drop to world
            if (!IsPointerOverAnyInventoryUI())
            {
                Debug.Log("[InventoryDragController] EndDrag - outside inventory UI, dropping to world");
                DropItemToWorld();
            }
            else
            {
                // Dropped on some other part of inventory UI (like background, header, etc.)
                // Don't drop to world - keep holding the item
                Debug.Log("[InventoryDragController] EndDrag - dropped on inventory UI but not a slot, keeping item held");
            }
        }

        private void UpdateDragIconVisuals()
        {
            if (dragIcon == null || dragCountText == null) return;

            if (HasHeldStack)
            {
                dragIcon.sprite = heldStack.item.icon;
                dragIcon.enabled = heldStack.item.icon != null;
                dragIcon.color = Color.white;
                dragCountText.text = heldStack.amount > 1 ? heldStack.amount.ToString() : "";
            }
            else
            {
                dragIcon.sprite = null;
                dragIcon.enabled = false;
                dragCountText.text = "";
            }
        }

        private void UpdateDragIconPosition()
        {
            if (dragIconObject == null || parentCanvas == null) return;

            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                Input.mousePosition,
                parentCanvas.worldCamera,
                out mousePos
            );

            dragIconObject.GetComponent<RectTransform>().anchoredPosition = mousePos + dragOffset;
        }

        private void ShowDragIcon()
        {
            if (dragIconObject != null)
            {
                dragIconObject.SetActive(true);
                UpdateDragIconVisuals();
                UpdateDragIconPosition();
            }
        }

        private void HideDragIcon()
        {
            if (dragIconObject != null)
            {
                dragIconObject.SetActive(false);
            }
        }

        /// <summary>
        /// Checks if the pointer is over the inventory panel rectangle.
        /// </summary>
        private bool IsPointerOverInventoryPanel()
        {
            if (inventoryPanelRect == null)
            {
                // Try to find it again
                if (InventoryUIManager.Instance != null)
                {
                    var panel = InventoryUIManager.Instance.transform.Find("InventoryPanel");
                    if (panel != null)
                    {
                        inventoryPanelRect = panel.GetComponent<RectTransform>();
                    }
                }

                if (inventoryPanelRect == null) return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(
                inventoryPanelRect,
                Input.mousePosition,
                parentCanvas?.worldCamera
            );
        }

        /// <summary>
        /// Checks if the pointer is over ANY inventory-related UI element.
        /// This includes slots, trash slot, buttons, etc.
        /// Uses EventSystem raycast to accurately detect UI elements under pointer.
        /// </summary>
        private bool IsPointerOverAnyInventoryUI()
        {
            // First do a quick panel bounds check
            if (IsPointerOverInventoryPanel())
            {
                return true;
            }

            // Use EventSystem to raycast and find what's under the pointer
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            var raycastResults = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, raycastResults);

            foreach (var result in raycastResults)
            {
                if (result.gameObject == null) continue;

                // Check if the hit object is an inventory slot
                if (result.gameObject.GetComponent<InventorySlotUI>() != null ||
                    result.gameObject.GetComponentInParent<InventorySlotUI>() != null)
                {
                    return true;
                }

                // Check if the hit object is a trash slot
                if (result.gameObject.GetComponent<InventoryTrashSlot>() != null ||
                    result.gameObject.GetComponentInParent<InventoryTrashSlot>() != null)
                {
                    return true;
                }

                // Check if the hit object is part of the inventory panel
                if (result.gameObject.GetComponentInParent<InventoryUIManager>() != null)
                {
                    return true;
                }

                // Check if the object or any parent has "Inventory" in its name (fallback)
                Transform current = result.gameObject.transform;
                while (current != null)
                {
                    if (current.name.Contains("Inventory") || current.name.Contains("Slot"))
                    {
                        return true;
                    }
                    current = current.parent;
                }
            }

            return false;
        }

        private void DropItemToWorld()
        {
            if (!HasHeldStack) return;

            // Use WorldDropManager to drop item
            if (WorldDropManager.Instance != null)
            {
                Debug.Log($"[Inventory] Dropped {heldStack.amount}x {heldStack.item.itemName} to world at screen pos {Input.mousePosition}");
                WorldDropManager.Instance.DropItem(heldStack.item, heldStack.amount);
            }
            else
            {
                Debug.LogWarning("[InventoryDragController] WorldDropManager not found, item lost!");
            }

            ClearHeldStack();

            if (InventoryUIManager.Instance != null)
            {
                InventoryUIManager.Instance.RefreshAllSlots();
            }
        }

        /// <summary>
        /// Return held stack to inventory (find first available slot).
        /// Called when closing inventory while holding item.
        /// </summary>
        public void ReturnHeldStackToInventory()
        {
            if (!HasHeldStack) return;

            // Try to add back to source slot first
            if (sourceSlotIndex >= 0)
            {
                ItemStack returned = InventorySystem.Instance.PlaceStackAt(sourceSlotIndex, heldStack);
                if (returned.IsEmpty)
                {
                    ClearHeldStack();
                    return;
                }
                // If couldn't place all, try other slots
                heldStack = returned;
            }

            // Try to add to inventory
            if (InventorySystem.Instance.TryAddItemToGrid(heldStack.item, heldStack.amount))
            {
                Debug.Log($"[InventoryDragController] Returned {heldStack.amount}x {heldStack.item.itemName} to inventory");
            }
            else
            {
                // Inventory full - drop to world
                if (WorldDropManager.Instance != null)
                {
                    WorldDropManager.Instance.DropItem(heldStack.item, heldStack.amount);
                    Debug.Log($"[InventoryDragController] Inventory full, dropped {heldStack.amount}x {heldStack.item.itemName} to world");
                }
                GameEvents.OnInventoryFull?.Invoke();
            }

            ClearHeldStack();
        }

        /// <summary>
        /// Print the inventory interaction report to console.
        /// </summary>
        public static void PrintInteractionReport()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== INVENTORY INTERACTION SYSTEM REPORT ===");
            sb.AppendLine("");
            sb.AppendLine("FILES MODIFIED:");
            sb.AppendLine("  - InventorySlotUI.cs (complete rewrite)");
            sb.AppendLine("  - InventoryDragController.cs");
            sb.AppendLine("  - InventoryUIManager.cs");
            sb.AppendLine("  - InventorySystem.cs");
            sb.AppendLine("");
            sb.AppendLine("INPUT MODEL:");
            sb.AppendLine("  SHORT CLICK (< 0.25s):");
            sb.AppendLine("    → Selects slot, shows detail panel");
            sb.AppendLine("    → Does NOT pick up item");
            sb.AppendLine("    → Does NOT modify inventory");
            sb.AppendLine("");
            sb.AppendLine("  LONG PRESS (>= 0.25s):");
            sb.AppendLine("    → Begins drag operation");
            sb.AppendLine("    → Item follows cursor");
            sb.AppendLine("    → Release on slot = place/merge/swap");
            sb.AppendLine("    → Release outside = drop to world");
            sb.AppendLine("");
            sb.AppendLine("  RIGHT CLICK:");
            sb.AppendLine("    → If holding: place 1 item");
            sb.AppendLine("    → If not holding + stack > 1: split stack");
            sb.AppendLine("");
            sb.AppendLine("SLOT COUNT: 20 slots (expandable via ExpandSlots())");
            sb.AppendLine("");
            sb.AppendLine("DETAIL PANEL:");
            sb.AppendLine("  - Shows item icon, name, amount, description");
            sb.AppendLine("  - Drop button → quantity popup if stack > 1");
            sb.AppendLine("  - Destroy button → quantity popup if stack > 1");
            sb.AppendLine("");
            sb.AppendLine("==========================================");
            Debug.Log(sb.ToString());
        }
    }
}
