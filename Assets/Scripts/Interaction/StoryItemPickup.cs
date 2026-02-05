using UnityEngine;

namespace BeneathTheFloor.Interaction
{
    public class StoryItemPickup : MonoBehaviour, IInteractable
    {
        [Header("Story Item Data")]
        [SerializeField] private StoryItem storyItem;

        [Header("Visual")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private float bobSpeed = 1f;
        [SerializeField] private float bobHeight = 0.1f;

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;

        private Vector3 startPosition;
        private bool isCollected = false;

        public bool CanInteract => !isCollected;

        private void Start()
        {
            startPosition = transform.position;

            // Check if already collected
            if (storyItem != null && Inventory.InventorySystem.Instance != null)
            {
                if (Inventory.InventorySystem.Instance.HasStoryItem(storyItem.itemId))
                {
                    isCollected = true;
                    gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (!isCollected)
            {
                // Rotate
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

                // Bob up and down
                float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
        }

        public string GetInteractionText()
        {
            return storyItem != null ? $"Press E to pick up {storyItem.title}" : "Press E to pick up";
        }

        public void Interact(GameObject interactor)
        {
            if (isCollected) return;

            Collect();
        }

        public void OnHoverEnter()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }

        public void OnHoverExit()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }

        private void Collect()
        {
            isCollected = true;

            // Add to inventory
            if (storyItem != null)
            {
                Inventory.InventorySystem.Instance?.AddStoryItem(storyItem);
            }

            // Play sound
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            // Could show UI popup with story item info
            Debug.Log($"Collected story item: {storyItem?.title}");

            // Disable or destroy
            gameObject.SetActive(false);
        }

        public void SetStoryItem(StoryItem item)
        {
            storyItem = item;
        }
    }
}
