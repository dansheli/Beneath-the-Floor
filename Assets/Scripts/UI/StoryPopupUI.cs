using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using BeneathTheFloor.Story;

namespace BeneathTheFloor.UI
{
    public class StoryPopupUI : MonoBehaviour
    {
        [Header("Popup Panel")]
        [SerializeField] private RectTransform popupPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button continueButton;

        [Header("Animation Settings")]
        [SerializeField] private float showDuration = 0.4f;
        [SerializeField] private float hideDuration = 0.3f;
        [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Vector2 slideOffset = new Vector2(0, -100f);

        [Header("Type Writing")]
        [SerializeField] private bool typewriterEffect = true;
        [SerializeField] private float charactersPerSecond = 30f;
        [SerializeField] private AudioClip typeSound;
        [SerializeField] private AudioSource audioSource;

        [Header("Colors by Rarity")]
        [SerializeField] private Color normalBorderColor = new Color(0.8f, 0.7f, 0.5f);
        [SerializeField] private Color rareBorderColor = new Color(0.4f, 0.7f, 1f);
        [SerializeField] private Color ancientBorderColor = new Color(0.7f, 0.3f, 1f);

        public static StoryPopupUI Instance { get; private set; }

        private bool isShowing = false;
        private Coroutine typewriterCoroutine;
        private StoryItem currentItem;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (canvasGroup == null && popupPanel != null)
            {
                canvasGroup = popupPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = popupPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            SetupButtons();
            HideImmediate();
        }

        private void Start()
        {
            // Subscribe to story events
            GameEvents.OnStoryItemFound += OnStoryItemFound;
        }

        private void OnDestroy()
        {
            GameEvents.OnStoryItemFound -= OnStoryItemFound;
        }

        private void SetupButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(Hide);
            }
        }

        private void Update()
        {
            if (isShowing && Input.GetKeyDown(KeyCode.Escape))
            {
                UIState.ConsumeEscape();
                Hide();
            }

            // Skip typewriter with click
            if (isShowing && typewriterCoroutine != null && Input.GetMouseButtonDown(0))
            {
                SkipTypewriter();
            }
        }

        private void OnStoryItemFound(StoryItem item)
        {
            Show(item);
        }

        public void Show(StoryItem item)
        {
            if (item == null) return;

            currentItem = item;

            // Update content
            if (titleText != null)
            {
                titleText.text = item.title;
            }

            if (iconImage != null)
            {
                if (item.icon != null)
                {
                    iconImage.sprite = item.icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            // Determine rarity for styling
            StoryFXManager.DiscoveryRarity rarity = DetermineRarity(item);
            ApplyRarityStyle(rarity);

            // Show panel
            StartCoroutine(ShowCoroutine());

            // Start typewriter for description
            if (typewriterEffect && descriptionText != null)
            {
                typewriterCoroutine = StartCoroutine(TypewriterCoroutine(item.description));
            }
            else if (descriptionText != null)
            {
                descriptionText.text = item.description;
            }

            // Pause game
            Time.timeScale = 0f;
            isShowing = true;

            // Set UIState so other systems know a popup is blocking
            UIState.IsMachineUIOpen = true;

            // Show cursor (UIState handles this but be explicit)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (!isShowing) return;

            StartCoroutine(HideCoroutine());
        }

        private IEnumerator ShowCoroutine()
        {
            if (popupPanel == null || canvasGroup == null) yield break;

            popupPanel.gameObject.SetActive(true);

            Vector2 startPos = popupPanel.anchoredPosition + slideOffset;
            Vector2 endPos = popupPanel.anchoredPosition - slideOffset;

            float elapsed = 0f;
            while (elapsed < showDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = showCurve.Evaluate(elapsed / showDuration);

                canvasGroup.alpha = t;
                popupPanel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            popupPanel.anchoredPosition = endPos;
        }

        private IEnumerator HideCoroutine()
        {
            if (popupPanel == null || canvasGroup == null) yield break;

            Vector2 startPos = popupPanel.anchoredPosition;
            Vector2 endPos = startPos + slideOffset;

            float elapsed = 0f;
            while (elapsed < hideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / hideDuration;

                canvasGroup.alpha = 1f - t;
                popupPanel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

                yield return null;
            }

            HideImmediate();

            // Resume game
            Time.timeScale = 1f;
            isShowing = false;

            // Clear UIState - this automatically handles cursor lock
            UIState.IsMachineUIOpen = false;

            // Force cursor lock if no other UI is open
            if (!UIState.IsAnyUIOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void HideImmediate()
        {
            if (popupPanel != null)
            {
                popupPanel.gameObject.SetActive(false);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private IEnumerator TypewriterCoroutine(string text)
        {
            if (descriptionText == null) yield break;

            descriptionText.text = "";
            float charDelay = 1f / charactersPerSecond;

            for (int i = 0; i < text.Length; i++)
            {
                descriptionText.text += text[i];

                // Play type sound occasionally
                if (typeSound != null && audioSource != null && i % 3 == 0)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(typeSound, 0.3f);
                }

                yield return new WaitForSecondsRealtime(charDelay);
            }

            typewriterCoroutine = null;
        }

        private void SkipTypewriter()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }

            if (descriptionText != null && currentItem != null)
            {
                descriptionText.text = currentItem.description;
            }
        }

        private StoryFXManager.DiscoveryRarity DetermineRarity(StoryItem item)
        {
            if (item.depthFound >= 15) return StoryFXManager.DiscoveryRarity.Ancient;
            if (item.depthFound >= 8) return StoryFXManager.DiscoveryRarity.Rare;
            return StoryFXManager.DiscoveryRarity.Normal;
        }

        private void ApplyRarityStyle(StoryFXManager.DiscoveryRarity rarity)
        {
            Color borderColor = rarity switch
            {
                StoryFXManager.DiscoveryRarity.Ancient => ancientBorderColor,
                StoryFXManager.DiscoveryRarity.Rare => rareBorderColor,
                _ => normalBorderColor
            };

            if (backgroundImage != null)
            {
                // Could apply border color or style here
            }

            if (titleText != null)
            {
                titleText.color = borderColor;
            }
        }

        public bool IsShowing()
        {
            return isShowing;
        }
    }
}
