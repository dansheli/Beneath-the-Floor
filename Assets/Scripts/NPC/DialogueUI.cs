using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace BeneathTheFloor.NPC
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TextMeshProUGUI npcNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Image npcPortraitImage;
        [SerializeField] private Button continueButton;
        [SerializeField] private TextMeshProUGUI continueButtonText;

        [Header("Typewriter Effect")]
        [SerializeField] private bool useTypewriter = true;
        [SerializeField] private float typewriterSpeed = 0.05f;

        public static DialogueUI Instance { get; private set; }

        private Action onContinueCallback;
        private bool isTyping = false;
        private string fullText = "";
        private Coroutine typewriterCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        private void Update()
        {
            // Allow skipping typewriter with click or key press
            if (isTyping && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
            {
                SkipTypewriter();
            }
        }

        public void ShowDialogue(string npcName, string text, Sprite portrait, Action onContinue)
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(true);
            }

            if (npcNameText != null)
            {
                npcNameText.text = npcName;
            }

            if (npcPortraitImage != null)
            {
                if (portrait != null)
                {
                    npcPortraitImage.sprite = portrait;
                    npcPortraitImage.gameObject.SetActive(true);
                }
                else
                {
                    npcPortraitImage.gameObject.SetActive(false);
                }
            }

            onContinueCallback = onContinue;

            if (useTypewriter)
            {
                StartTypewriter(text);
            }
            else
            {
                if (dialogueText != null)
                {
                    dialogueText.text = text;
                }
            }

            UpdateContinueButton();
        }

        public void HideDialogue()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }

            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            isTyping = false;
            onContinueCallback = null;
        }

        private void StartTypewriter(string text)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }

            fullText = text;
            isTyping = true;
            typewriterCoroutine = StartCoroutine(TypewriterEffect());
        }

        private System.Collections.IEnumerator TypewriterEffect()
        {
            if (dialogueText == null) yield break;

            dialogueText.text = "";

            foreach (char c in fullText)
            {
                dialogueText.text += c;
                yield return new WaitForSecondsRealtime(typewriterSpeed);
            }

            isTyping = false;
            UpdateContinueButton();
        }

        private void SkipTypewriter()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }

            if (dialogueText != null)
            {
                dialogueText.text = fullText;
            }

            isTyping = false;
            UpdateContinueButton();
        }

        private void UpdateContinueButton()
        {
            if (continueButtonText != null)
            {
                continueButtonText.text = isTyping ? "Skip" : "Continue";
            }
        }

        private void OnContinueClicked()
        {
            if (isTyping)
            {
                SkipTypewriter();
            }
            else
            {
                onContinueCallback?.Invoke();
            }
        }
    }
}
