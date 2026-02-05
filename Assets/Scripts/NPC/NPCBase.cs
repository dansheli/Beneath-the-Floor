using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.NPC
{
    public class NPCBase : MonoBehaviour, Interaction.IInteractable
    {
        [Header("NPC Settings")]
        [SerializeField] private string npcName = "NPC";
        [SerializeField] private NPCType npcType = NPCType.Generic;
        [SerializeField] private Sprite npcPortrait;

        [Header("Dialogue")]
        [SerializeField] private List<DialogueLine> dialogueLines = new List<DialogueLine>();
        [SerializeField] private string interactionText = "Talk";

        private int currentDialogueIndex = 0;
        private bool isInConversation = false;

        public bool CanInteract => !isInConversation;
        public string NPCName => npcName;
        public NPCType Type => npcType;

        public string GetInteractionText()
        {
            return $"Press E to {interactionText} to {npcName}";
        }

        public void Interact(GameObject interactor)
        {
            if (isInConversation) return;

            StartConversation();
        }

        public void OnHoverEnter()
        {
            // Could highlight NPC
        }

        public void OnHoverExit()
        {
            // Remove highlight
        }

        private void StartConversation()
        {
            isInConversation = true;
            currentDialogueIndex = 0;

            // Lock player movement
            var player = FindFirstObjectByType<Player.FirstPersonController>();
            if (player != null)
            {
                player.CanMove = false;
                player.SetCursorLock(false);
            }

            ShowCurrentDialogue();
        }

        private void ShowCurrentDialogue()
        {
            if (currentDialogueIndex >= dialogueLines.Count)
            {
                EndConversation();
                return;
            }

            DialogueLine line = dialogueLines[currentDialogueIndex];

            // Show dialogue UI
            DialogueUI.Instance?.ShowDialogue(npcName, line.text, npcPortrait, () =>
            {
                // Process any actions for this dialogue
                ProcessDialogueActions(line);

                currentDialogueIndex++;
                ShowCurrentDialogue();
            });
        }

        private void ProcessDialogueActions(DialogueLine line)
        {
            // Give items
            if (line.giveItem)
            {
                var inventory = Inventory.InventorySystem.Instance;
                if (inventory != null)
                {
                    inventory.AddResource(line.resourceToGive, line.amountToGive);
                }
            }

            // Trigger events
            if (!string.IsNullOrEmpty(line.triggerEvent))
            {
                // Could implement custom event system
                Debug.Log($"Triggered event: {line.triggerEvent}");
            }
        }

        private void EndConversation()
        {
            isInConversation = false;

            // Unlock player movement
            var player = FindFirstObjectByType<Player.FirstPersonController>();
            if (player != null)
            {
                player.CanMove = true;
                player.SetCursorLock(true);
            }

            DialogueUI.Instance?.HideDialogue();
        }

        public void AddDialogue(string text)
        {
            dialogueLines.Add(new DialogueLine { text = text });
        }

        public void SetDialogue(List<DialogueLine> lines)
        {
            dialogueLines = lines;
        }
    }

    public enum NPCType
    {
        Generic,
        Shopkeeper,
        Historian,
        MysteriousStranger,
        QuestGiver
    }

    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(2, 5)]
        public string text;
        public bool giveItem = false;
        public ResourceType resourceToGive;
        public int amountToGive = 1;
        public string triggerEvent = "";
    }
}
