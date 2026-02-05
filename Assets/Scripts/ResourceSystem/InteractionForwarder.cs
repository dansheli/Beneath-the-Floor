using UnityEngine;
using BeneathTheFloor.Interaction;

namespace BeneathTheFloor.ResourceSystem
{
    /// <summary>
    /// Forwards IInteractable calls to the parent object.
    /// Used for larger interaction zones that don't affect physics.
    /// </summary>
    public class InteractionForwarder : MonoBehaviour, IInteractable
    {
        private IInteractable parentInteractable;

        public bool CanInteract => parentInteractable?.CanInteract ?? false;

        private void Awake()
        {
            // Find IInteractable on parent
            parentInteractable = GetComponentInParent<IInteractable>();

            // Skip self
            if (parentInteractable == (IInteractable)this)
            {
                parentInteractable = transform.parent?.GetComponent<IInteractable>();
            }
        }

        public string GetInteractionText()
        {
            return parentInteractable?.GetInteractionText() ?? "";
        }

        public void Interact(GameObject interactor)
        {
            parentInteractable?.Interact(interactor);
        }

        public void OnHoverEnter()
        {
            parentInteractable?.OnHoverEnter();
        }

        public void OnHoverExit()
        {
            parentInteractable?.OnHoverExit();
        }
    }
}
