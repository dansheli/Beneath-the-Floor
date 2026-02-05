using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.Player;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// UI component that shows a pickup prompt when the player is looking at a ResourcePickup.
    /// Displays "[E] Pick up {ResourceName} x{Amount}"
    /// </summary>
    public class PickupPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The parent GameObject to show/hide.")]
        public GameObject promptContainer;

        [Tooltip("Text component for the prompt message.")]
        public TextMeshProUGUI promptText;

        [Tooltip("Optional: Legacy Text component if not using TextMeshPro.")]
        public Text legacyPromptText;

        [Header("Settings")]
        [Tooltip("Format string for the prompt. {0}=key, {1}=resource name, {2}=amount")]
        public string promptFormat = "[{0}] Pick up {1}";

        [Tooltip("Show amount in prompt if > 1.")]
        public bool showAmountIfMultiple = true;

        [Header("Animation")]
        [Tooltip("Animate the prompt appearing/disappearing.")]
        public bool animatePrompt = true;

        [Tooltip("Animation speed for fade.")]
        public float fadeSpeed = 8f;

        // Runtime state
        private ResourcePickupInteractor _interactor;
        private CanvasGroup _canvasGroup;
        private float _targetAlpha;
        private bool _isShowing;

        public static PickupPromptUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Try to get or add CanvasGroup for fading
            if (promptContainer != null && animatePrompt)
            {
                _canvasGroup = promptContainer.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = promptContainer.AddComponent<CanvasGroup>();
                }
            }
        }

        private void Start()
        {
            // Find the interactor
            _interactor = ResourcePickupInteractor.Instance ?? FindObjectOfType<ResourcePickupInteractor>();

            if (_interactor != null)
            {
                // Subscribe to events
                _interactor.OnPickupTargeted.AddListener(OnPickupTargeted);
                _interactor.OnPickupUntargeted.AddListener(OnPickupUntargeted);
                _interactor.OnPickupCollected.AddListener(OnPickupCollected);
            }
            else
            {
                // Retry subscription after a delay
                Invoke(nameof(RetrySubscription), 0.5f);
            }

            // Initially hidden
            HidePrompt();
        }

        private void RetrySubscription()
        {
            if (_interactor == null)
            {
                _interactor = ResourcePickupInteractor.Instance ?? FindObjectOfType<ResourcePickupInteractor>();

                if (_interactor != null)
                {
                    _interactor.OnPickupTargeted.AddListener(OnPickupTargeted);
                    _interactor.OnPickupUntargeted.AddListener(OnPickupUntargeted);
                    _interactor.OnPickupCollected.AddListener(OnPickupCollected);
                }
                else
                {
                    Debug.LogWarning("[PickupPromptUI] Could not find ResourcePickupInteractor!");
                }
            }
        }

        private void Update()
        {
            // Check for magnet pull hint when no E-range target
            UpdateMagnetPullHint();

            // Animate alpha
            if (animatePrompt && _canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, _targetAlpha, Time.deltaTime * fadeSpeed);

                // Hide container when fully faded out
                if (_targetAlpha == 0f && _canvasGroup.alpha < 0.01f)
                {
                    if (promptContainer != null && promptContainer.activeSelf)
                        promptContainer.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Check if MagnetPullAbility has a target beyond E range and show pull hint.
        /// </summary>
        private void UpdateMagnetPullHint()
        {
            // Only show pull hint if we're NOT already showing E pickup prompt
            if (_isShowing)
            {
                _showingPullHint = false;
                return;
            }

            // Check if MagnetPullAbility has an aimed pickup
            var magnetPull = MagnetPullAbility.Instance;
            if (magnetPull == null || !magnetPull.CanPull())
            {
                // Hide pull hint if it was showing
                if (_showingPullHint)
                {
                    _showingPullHint = false;
                    _targetAlpha = 0f;
                }
                return;
            }

            // Show the pull hint
            string hintText = magnetPull.GetPullHintText();
            if (!string.IsNullOrEmpty(hintText))
            {
                ShowPullHint(hintText);
            }
        }

        private bool _showingPullHint;

        private void ShowPullHint(string text)
        {
            if (promptContainer != null && !promptContainer.activeSelf)
            {
                promptContainer.SetActive(true);
            }

            _showingPullHint = true;
            _targetAlpha = 1f;
            SetPromptText(text);

            if (!animatePrompt && _canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        private void OnPickupTargeted(ResourcePickup pickup)
        {
            ShowPrompt(pickup);
        }

        private void OnPickupUntargeted()
        {
            HidePrompt();
        }

        private void OnPickupCollected(ResourcePickup pickup)
        {
            HidePrompt();
        }

        private void ShowPrompt(ResourcePickup pickup)
        {
            if (promptContainer != null)
            {
                promptContainer.SetActive(true);
            }

            _isShowing = true;
            _targetAlpha = 1f;

            // Format the prompt text
            string keyName = _interactor != null ? _interactor.pickupKey.ToString() : "E";
            string resourceName = pickup.GetDisplayName();
            string text;

            if (showAmountIfMultiple && pickup.amount > 1)
            {
                text = string.Format(promptFormat + " x{2}", keyName, resourceName, pickup.amount);
            }
            else
            {
                text = string.Format(promptFormat, keyName, resourceName, pickup.amount);
            }

            SetPromptText(text);

            // If not animating, set alpha immediately
            if (!animatePrompt && _canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        private void HidePrompt()
        {
            _isShowing = false;
            _targetAlpha = 0f;

            // If not animating, hide immediately
            if (!animatePrompt)
            {
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 0f;

                if (promptContainer != null)
                    promptContainer.SetActive(false);
            }
        }

        private void SetPromptText(string text)
        {
            if (promptText != null)
            {
                promptText.text = text;
            }
            else if (legacyPromptText != null)
            {
                legacyPromptText.text = text;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Unsubscribe from events
            if (_interactor != null)
            {
                _interactor.OnPickupTargeted.RemoveListener(OnPickupTargeted);
                _interactor.OnPickupUntargeted.RemoveListener(OnPickupUntargeted);
                _interactor.OnPickupCollected.RemoveListener(OnPickupCollected);
            }
        }
    }
}
