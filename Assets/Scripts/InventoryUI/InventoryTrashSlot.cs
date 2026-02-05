using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.InventoryUI
{
    /// <summary>
    /// A special slot that destroys items dropped on it after confirmation.
    /// </summary>
    public class InventoryTrashSlot : MonoBehaviour,
        IDropHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject confirmationPopup;
        [SerializeField] private TextMeshProUGUI confirmationText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.5f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.7f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color activeColor = new Color(0.9f, 0.4f, 0.4f, 1f);

        private ItemStack pendingDestroyStack;
        private bool isHovered = false;

        private void Awake()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }
        }

        public void SetupConfirmationPopup(GameObject popup, TextMeshProUGUI text, Button yes, Button no)
        {
            confirmationPopup = popup;
            confirmationText = text;
            yesButton = yes;
            noButton = no;

            if (yesButton != null)
            {
                yesButton.onClick.RemoveAllListeners();
                yesButton.onClick.AddListener(OnConfirmDestroy);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveAllListeners();
                noButton.onClick.AddListener(OnCancelDestroy);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (InventoryUIManager.Instance == null || InventoryUIManager.Instance.DragController == null)
                return;

            var dragController = InventoryUIManager.Instance.DragController;
            if (!dragController.HasHeldStack) return;

            RequestDestroy(dragController.HeldStack);
        }

        public void RequestDestroy(ItemStack stack)
        {
            if (stack.IsEmpty) return;

            pendingDestroyStack = stack;

            // Show confirmation popup
            if (confirmationPopup != null)
            {
                if (confirmationText != null)
                {
                    confirmationText.text = $"Destroy {stack.amount}x {stack.item.itemName}?";
                }
                confirmationPopup.SetActive(true);
            }
            else
            {
                // No popup - destroy immediately
                ConfirmDestroy();
            }

            // Highlight trash slot
            if (backgroundImage != null)
            {
                backgroundImage.color = activeColor;
            }
        }

        private void OnConfirmDestroy()
        {
            ConfirmDestroy();
        }

        private void ConfirmDestroy()
        {
            if (!pendingDestroyStack.IsEmpty)
            {
                Debug.Log($"[InventoryTrashSlot] Destroyed {pendingDestroyStack.amount}x {pendingDestroyStack.item.itemName}");

                // Notify inventory system
                if (InventorySystem.Instance != null)
                {
                    InventorySystem.Instance.DestroyStack(pendingDestroyStack);
                }

                // Clear the held stack in drag controller
                if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.DragController != null)
                {
                    InventoryUIManager.Instance.DragController.ClearHeldStack();
                }
            }

            CleanupConfirmation();
        }

        private void OnCancelDestroy()
        {
            CancelDestroy();
        }

        private void CancelDestroy()
        {
            if (!pendingDestroyStack.IsEmpty)
            {
                Debug.Log($"[InventoryTrashSlot] Cancelled destroy of {pendingDestroyStack.amount}x {pendingDestroyStack.item.itemName}");

                // Return item to drag controller (keep holding it)
                // The drag controller already has it, so we just don't clear it
            }

            CleanupConfirmation();
        }

        private void CleanupConfirmation()
        {
            pendingDestroyStack = ItemStack.Empty;

            if (confirmationPopup != null)
            {
                confirmationPopup.SetActive(false);
            }

            // Reset color
            if (backgroundImage != null)
            {
                backgroundImage.color = isHovered ? hoverColor : normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;

            if (backgroundImage != null && pendingDestroyStack.IsEmpty)
            {
                backgroundImage.color = hoverColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;

            if (backgroundImage != null && pendingDestroyStack.IsEmpty)
            {
                backgroundImage.color = normalColor;
            }
        }
    }
}
