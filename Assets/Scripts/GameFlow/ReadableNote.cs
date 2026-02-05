using UnityEngine;
using BeneathTheFloor.Interaction;

namespace BeneathTheFloor.GameFlow
{
    /// <summary>
    /// A readable note that can be interacted with to display text.
    /// Creates a child collider for raycast detection by InteractionSystem.
    /// </summary>
    public class ReadableNote : MonoBehaviour, IInteractable
    {
        [Header("Note Content")]
        [SerializeField] [TextArea(5, 15)] private string noteTitle = "Grandpa's Note";
        [SerializeField] [TextArea(5, 15)] private string noteContent =
            "If you're reading this, I didn't make it.\n\n" +
            "I could hear it...\n" +
            "A sound coming from beneath the ground.\n\n" +
            "I tried to reach it, but the earth was stronger than me.\n\n" +
            "Take the shovel.\n" +
            "Start digging.\n\n" +
            "Maybe you'll succeed where I failed.";

        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Press E to read Grandpa's note";

        [Header("Events")]
        [SerializeField] private bool disableAfterReading = false;
        [SerializeField] private bool triggerToolPickupOnClose = true;

        [Header("Audio")]
        [Tooltip("Looping ambient sound that plays while reading this note")]
        [SerializeField] private AudioClip noteAmbientLoop;

        [Tooltip("Volume for the ambient loop")]
        [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.5f;

        // Events
        public event System.Action OnNoteRead;
        public event System.Action OnNoteClosed;

        private bool hasBeenRead = false;
        private bool isShowingNote = false;

        // IInteractable implementation
        public bool CanInteract => !isShowingNote;
        public bool HasBeenRead => hasBeenRead;

        private void Start()
        {
            // Create a child object with a large collider for raycast detection
            // This avoids issues with combined static meshes
            GameObject colliderChild = new GameObject("NoteCollider");
            colliderChild.transform.SetParent(transform);
            colliderChild.transform.localPosition = Vector3.zero;
            colliderChild.transform.localRotation = Quaternion.identity;

            // Add a large sphere collider for easy raycast detection
            var sphereCollider = colliderChild.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f; // 0.5m radius sphere
            sphereCollider.center = Vector3.zero;

            // Copy the ReadableNote reference to the child so InteractionSystem can find it
            // We use a helper component for this
            var helper = colliderChild.AddComponent<ReadableNoteColliderHelper>();
            helper.parentNote = this;
        }

        // IInteractable interface methods
        // InteractionSystem handles all detection via raycast and calls these methods
        public string GetInteractionText()
        {
            return interactionPrompt;
        }

        public void Interact(GameObject interactor)
        {
            ShowNote();
        }

        public void OnHoverEnter()
        {
            // Show interaction prompt via HUD
            if (UI.HUDController.Instance != null)
            {
                UI.HUDController.Instance.ShowInteractionPrompt(interactionPrompt);
            }
        }

        public void OnHoverExit()
        {
            if (UI.HUDController.Instance != null)
            {
                UI.HUDController.Instance.HideInteractionPrompt();
            }
        }

        private void ShowNote()
        {
            if (isShowingNote) return;

            isShowingNote = true;
            OnHoverExit(); // Hide interaction prompt

            // Show note UI
            if (NoteUIController.Instance == null)
            {
                CreateNoteUI();
            }

            if (NoteUIController.Instance != null)
            {
                // Set audio settings from this note before showing
                if (noteAmbientLoop != null)
                {
                    NoteUIController.Instance.SetAmbientClip(noteAmbientLoop);
                    NoteUIController.Instance.SetAmbientVolume(ambientVolume);
                }

                NoteUIController.Instance.ShowNote(noteTitle, noteContent, OnNoteUIClosed);
            }

            hasBeenRead = true;
            OnNoteRead?.Invoke();
        }

        private void OnNoteUIClosed()
        {
            isShowingNote = false;

            // InteractionSystem will handle showing the prompt again if player is still looking at the note

            OnNoteClosed?.Invoke();

            // Trigger tool pickup if configured
            if (triggerToolPickupOnClose)
            {
                // Show the player's tool
                if (Tools.HeldToolController.Instance != null)
                {
                    Tools.HeldToolController.Instance.ShowCurrentTool();
                }
            }

            if (disableAfterReading)
            {
                gameObject.SetActive(false);
            }
        }

        private void CreateNoteUI()
        {
            GameObject noteUIObj = new GameObject("NoteUIController");
            noteUIObj.AddComponent<NoteUIController>();
        }
    }

    /// <summary>
    /// Helper component that forwards IInteractable calls to the parent ReadableNote.
    /// This is placed on a child collider object for easier raycast detection.
    /// </summary>
    public class ReadableNoteColliderHelper : MonoBehaviour, IInteractable
    {
        public ReadableNote parentNote;

        public bool CanInteract => parentNote != null && parentNote.CanInteract;

        public string GetInteractionText()
        {
            return parentNote?.GetInteractionText() ?? "Read";
        }

        public void Interact(GameObject interactor)
        {
            parentNote?.Interact(interactor);
        }

        public void OnHoverEnter()
        {
            parentNote?.OnHoverEnter();
        }

        public void OnHoverExit()
        {
            parentNote?.OnHoverExit();
        }
    }
}
