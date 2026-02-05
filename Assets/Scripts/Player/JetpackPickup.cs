using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.World;
using BeneathTheFloor.Winch;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Pickup script for the Jetpack world object.
    /// When player interacts, the jetpack is picked up and the world object is destroyed.
    /// Requires the engine to be activated (crystal inserted) to interact.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class JetpackPickup : MonoBehaviour, IInteractable
    {
        // Event fired when jetpack is picked up
        public static event System.Action OnJetpackPickedUp;
        [Header("Pickup Settings")]
        [SerializeField] private string pickupPrompt = "Press E to pick up Jetpack";

        [Header("Engine Requirement")]
        [Tooltip("Reference to the Engine that must be activated for this pickup to work")]
        [SerializeField] private EngineActivationInteract engine;

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;

        [Header("Visual")]
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.1f;
        [SerializeField] private float rotateSpeed = 30f;
        [SerializeField] private bool enableBobAndRotate = false; // Disabled by default - jetpack hangs on wall

        private Vector3 startPosition;
        private bool isPickedUp = false;

        /// <summary>
        /// Returns true if the engine is activated and the pickup can be used.
        /// </summary>
        private bool IsEngineActivated
        {
            get
            {
                if (engine == null)
                {
                    engine = FindObjectOfType<EngineActivationInteract>();
                }
                return engine != null && engine.IsActivated;
            }
        }

        // IInteractable implementation
        public bool CanInteract
        {
            get
            {
                if (isPickedUp) return false;
                if (!IsEngineActivated) return false;
                return true;
            }
        }

        public string GetInteractionText()
        {
            if (!IsEngineActivated)
            {
                return "Jetpack [OFFLINE - Insert crystal into engine]";
            }
            return pickupPrompt;
        }

        public void Interact(GameObject interactor)
        {
            if (isPickedUp) return;
            if (!IsEngineActivated) return;

            // Find JetpackController on the player
            JetpackController jetpack = interactor.GetComponent<JetpackController>();
            if (jetpack == null)
            {
                jetpack = interactor.GetComponentInChildren<JetpackController>();
            }

            if (jetpack == null)
            {
                // Try to find it in the scene (might be on a different object)
                jetpack = FindObjectOfType<JetpackController>();
            }

            if (jetpack != null)
            {
                isPickedUp = true;
                jetpack.PickupJetpack();

                // Play pickup sound
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1f);
                }

                Debug.Log("[JetpackPickup] Jetpack picked up!");

                // Permanently disable the winch motor - player now has jetpack
                WinchExitTrigger.LockWinchPermanently();

                // Fire event for mission system
                OnJetpackPickedUp?.Invoke();

                // Destroy the world object
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("[JetpackPickup] No JetpackController found on player!");
            }
        }

        public void OnHoverEnter()
        {
            // Optional: Add highlight effect
        }

        public void OnHoverExit()
        {
            // Optional: Remove highlight effect
        }

        private void Start()
        {
            startPosition = transform.position;

            // Try to find engine if not assigned
            if (engine == null)
            {
                engine = FindObjectOfType<EngineActivationInteract>();
            }

            // Ensure we have a collider
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
            }
        }

        private void Update()
        {
            if (!enableBobAndRotate || isPickedUp) return;

            // Bob up and down
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);

            // Rotate slowly
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }
    }
}
