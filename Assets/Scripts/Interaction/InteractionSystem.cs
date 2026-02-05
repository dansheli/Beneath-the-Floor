using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using BeneathTheFloor.UI;
using BeneathTheFloor.Robot;
using BeneathTheFloor.Logistics;

namespace BeneathTheFloor.Interaction
{
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private LayerMask interactableLayer = -1; // Default to all layers
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private bool useLayerMask = false; // Option to use layer filtering

        [Header("UI References")]
        [SerializeField] private GameObject interactionPrompt;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private IInteractable currentInteractable;
        private bool canInteract = true;

        // Prevent multiple interactions in the same frame
        private bool interactedThisFrame = false;
        private bool uiIsOpen = false;

        public bool CanInteract
        {
            get => canInteract;
            set => canInteract = value;
        }

        /// <summary>
        /// Call this when a machine UI opens to pause interaction detection
        /// </summary>
        public void SetUIOpen(bool open)
        {
            uiIsOpen = open;
            if (debugMode)
            {
                Debug.Log($"[InteractionSystem] UI open state: {uiIsOpen}");
            }
        }

        public static InteractionSystem Instance { get; private set; }

        private void Awake()
        {
            // Check if existing Instance is still valid (not destroyed)
            if (Instance != null && Instance.gameObject == null)
            {
                Instance = null;
            }

            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // IMPORTANT: Only destroy THIS COMPONENT, not the entire Player GameObject!
                Debug.LogWarning($"[InteractionSystem] Duplicate found, destroying only this component. Existing instance on: {Instance.gameObject.name}");
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }

            if (debugMode)
            {
                Debug.Log($"[InteractionSystem] Initialized. Range: {interactionRange}, UseLayerMask: {useLayerMask}");
            }
        }

        private void Update()
        {
            // Reset per-frame flag at the start of each frame
            interactedThisFrame = false;

            // Skip all interaction logic if UI is open (local flag)
            if (uiIsOpen)
            {
                ClearCurrentInteractable(); // Hide prompt when UI is open
                return;
            }

            // CRITICAL: Skip if any blocking UI is open (machine UI, inventory, pause menu)
            if (UIState.IsAnyUIOpen)
            {
                ClearCurrentInteractable(); // Hide prompt when any UI is open
                return;
            }

            // Also skip if pointer is over UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearCurrentInteractable();
                return;
            }

            if (!canInteract) return;

            CheckForInteractable();
            HandleInteractionInput();
        }

        private void CheckForInteractable()
        {
            if (cameraTransform == null) return;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            // Use RaycastAll to find interactables even if terrain is in front
            RaycastHit[] hits;
            if (useLayerMask && interactableLayer.value != 0 && interactableLayer.value != -1)
            {
                // Always include Robot layer (11) so digger robot can be picked up
                int mask = interactableLayer.value | (1 << 11);
                hits = Physics.RaycastAll(ray, interactionRange, mask, QueryTriggerInteraction.Collide);
            }
            else
            {
                hits = Physics.RaycastAll(ray, interactionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            }

            // Find the closest IInteractable among all hits
            // Priority: DiggerRobotStateMachine > other interactables (so player can pick up robot near pickups)
            IInteractable closestInteractable = null;
            float closestDist = float.MaxValue;
            IInteractable priorityInteractable = null;
            float priorityDist = float.MaxValue;

            foreach (var hit in hits)
            {
                // Try to get IInteractable from hit object or its parent
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable == null)
                {
                    interactable = hit.collider.GetComponentInParent<IInteractable>();
                }

                if (interactable != null && interactable.CanInteract)
                {
                    if (hit.distance < closestDist)
                    {
                        closestInteractable = interactable;
                        closestDist = hit.distance;
                    }

                    // Check if this is a high-priority interactable (robot, dock, machine)
                    if (interactable is Robot.DiggerRobotStateMachine || interactable is Robot.DiggerRobotDock
                        || interactable is LogisticsRobotController || interactable is LogisticsRobotDock
                        || interactable is DropOffContainer)
                    {
                        if (hit.distance < priorityDist)
                        {
                            priorityInteractable = interactable;
                            priorityDist = hit.distance;
                        }
                    }
                }
            }

            // Prefer priority interactable if within reasonable range
            if (priorityInteractable != null && priorityDist < closestDist + 1.5f)
                closestInteractable = priorityInteractable;

            if (closestInteractable != null)
            {
                if (currentInteractable != closestInteractable)
                {
                    if (debugMode)
                    {
                        Debug.Log($"[InteractionSystem] Found interactable: {((MonoBehaviour)closestInteractable).gameObject.name}");
                    }

                    currentInteractable?.OnHoverExit();
                    currentInteractable = closestInteractable;
                    currentInteractable.OnHoverEnter();
                    ShowInteractionPrompt(true, currentInteractable.GetInteractionText());
                }
            }
            else
            {
                ClearCurrentInteractable();
            }
        }

        private void ClearCurrentInteractable()
        {
            if (currentInteractable != null)
            {
                currentInteractable.OnHoverExit();
                currentInteractable = null;
                ShowInteractionPrompt(false, "");
            }
        }

        private void HandleInteractionInput()
        {
            // Only process E key once per frame
            if (interactedThisFrame) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (debugMode)
                {
                    Debug.Log($"[InteractionSystem] E pressed. Current target: {(currentInteractable != null ? ((MonoBehaviour)currentInteractable).gameObject.name : "None")}");
                }

                if (currentInteractable != null && currentInteractable.CanInteract)
                {
                    // Mark as interacted to prevent double-calls
                    interactedThisFrame = true;

                    if (debugMode)
                    {
                        Debug.Log($"[InteractionSystem] Calling Interact() on {((MonoBehaviour)currentInteractable).gameObject.name}");
                    }
                    currentInteractable.Interact(gameObject);
                }
            }
        }

        private void ShowInteractionPrompt(bool show, string text)
        {
            // Try HUDController
            if (UI.HUDController.Instance != null)
            {
                if (show)
                {
                    UI.HUDController.Instance.ShowInteractionPrompt(text);
                }
                else
                {
                    UI.HUDController.Instance.HideInteractionPrompt();
                }
                return;
            }

            // Legacy fallback
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(show);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * interactionRange);
            }
        }
    }

    public interface IInteractable
    {
        bool CanInteract { get; }
        string GetInteractionText();
        void Interact(GameObject interactor);
        void OnHoverEnter();
        void OnHoverExit();
    }
}
