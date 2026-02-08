using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Energy
{
    /// <summary>
    /// EnergyUI displays the energy HUD.
    /// IMPORTANT: This UI should exist as a STATIC element in the scene (under HUDCanvas or similar).
    /// The script only UPDATES the existing display - it does NOT create the UI structure.
    /// Use "BeneathTheFloor/Setup/Create Static Machine UI" in the Editor to create the HUD structure.
    /// </summary>
    public class EnergyUI : MonoBehaviour
    {
        [Header("Custom Frame")]
        [Tooltip("Custom energy bar frame sprite (energy_bar_frame.png)")]
        [SerializeField] private Sprite energyBarFrameSprite;

        [Header("Static UI References")]
        [Tooltip("Reference to the main EnergyHUD panel (should exist in scene)")]
        [SerializeField] private GameObject energyHUD;
        [SerializeField] private Slider energyBar;
        [SerializeField] private Image energyFill;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI energyPercentText;

        [Header("Status Indicators")]
        [SerializeField] private GameObject lowEnergyWarning;
        [SerializeField] private GameObject noEnergyWarning;
        [SerializeField] private TextMeshProUGUI netRateText;

        // NOTE: Drink count display moved to ConsumablesHUD (bottom-right panel)

        // HARDCODED colors - do NOT serialize to prevent scene override
        // Industrial calm teal theme (not neon green)
        private readonly Color fullColor = new Color(0.35f, 0.7f, 0.75f);   // Calm teal/cyan
        private readonly Color mediumColor = new Color(0.85f, 0.7f, 0.35f); // Muted amber
        private readonly Color lowColor = new Color(0.85f, 0.4f, 0.35f);    // Muted red
        private const float lowThreshold = 0.2f;
        private const float mediumThreshold = 0.5f;

        [Header("Animation")]
        [SerializeField] private bool pulseOnLow = true;
        [SerializeField] private float pulseSpeed = 2f;

        [Header("Energy Drink Hint")]
        [SerializeField] private float hintDisplayDuration = 3f;

        public static EnergyUI Instance { get; private set; }

        private EnergyManager energyManager;
        private bool isLowEnergy = false;

        // Energy drink hint
        private GameObject energyDrinkHint;
        private TextMeshProUGUI hintText;
        private float hintHideTime;
        private bool isHintShowing;

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
            // Validate static references
            ValidateReferences();

            // Ensure EnergyManager exists
            if (EnergyManager.Instance == null)
            {
                var energyManagerObj = new GameObject("EnergyManager");
                energyManagerObj.AddComponent<EnergyManager>();
            }

            energyManager = EnergyManager.Instance;

            if (energyManager != null)
            {
                energyManager.OnEnergyChanged += UpdateEnergyDisplay;
                energyManager.OnLowEnergy += OnLowEnergy;
                energyManager.OnEnergyDepleted += OnEnergyDepleted;
                energyManager.OnEnergyRestored += OnEnergyRestored;

                // Initial update
                UpdateEnergyDisplay(energyManager.CurrentEnergy, energyManager.MaxEnergy);
            }

            // Hide warnings initially
            if (lowEnergyWarning != null) lowEnergyWarning.SetActive(false);
            if (noEnergyWarning != null) noEnergyWarning.SetActive(false);
        }

        /// <summary>
        /// ALWAYS creates fresh modern styled UI, destroying any old EnergyHUD.
        /// This ensures no old serialized colors are used.
        /// </summary>
        private void ValidateReferences()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogWarning("[EnergyUI] No Canvas found!");
                return;
            }

            // ALWAYS destroy old EnergyHUD to ensure fresh modern styling
            // Check for any existing EnergyHUD and destroy it
            var existingHUD = canvas.transform.Find("EnergyHUD");
            if (existingHUD != null && energyHUD != existingHUD.gameObject)
            {
                Destroy(existingHUD.gameObject);
            }

            // If we already have a reference that's not destroyed, clear it
            if (energyHUD != null)
            {
                Destroy(energyHUD);
                energyHUD = null;
            }

            // Clear all references
            energyBar = null;
            energyFill = null;
            energyText = null;
            energyPercentText = null;
            lowEnergyWarning = null;
            noEnergyWarning = null;
            netRateText = null;

            // Create fresh modern HUD
            CreateModernEnergyHUD(canvas);
        }

        /// <summary>
        /// Creates a modern styled Energy HUD at runtime.
        /// Uses custom frame sprite with fill bar on top (in slot area).
        /// </summary>
        private void CreateModernEnergyHUD(Canvas canvas)
        {
            // Main container - top-right position
            energyHUD = new GameObject("EnergyHUD");
            energyHUD.transform.SetParent(canvas.transform, false);

            RectTransform hudRect = energyHUD.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.5f, 0); // Bottom-center
            hudRect.anchorMax = new Vector2(0.5f, 0);
            hudRect.pivot = new Vector2(0.5f, 0);
            hudRect.anchoredPosition = new Vector2(0, 20);
            hudRect.sizeDelta = new Vector2(380, 80); // Sized for custom frame aspect ratio

            // Create a white sprite for fill elements
            Texture2D tex = new Texture2D(4, 4);
            Color[] colors = new Color[16];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

            // LAYER 1: Fill bar container (created first, renders behind)
            GameObject fillContainerObj = new GameObject("FillContainer");
            fillContainerObj.transform.SetParent(energyHUD.transform, false);
            RectTransform fillContainerRect = fillContainerObj.AddComponent<RectTransform>();
            fillContainerRect.anchorMin = new Vector2(0.115f, 0.36f);
            fillContainerRect.anchorMax = new Vector2(0.885f, 0.64f);
            fillContainerRect.offsetMin = Vector2.zero;
            fillContainerRect.offsetMax = Vector2.zero;

            // Energy bar fill - TEAL/CYAN color
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillContainerObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            energyFill = fillObj.AddComponent<Image>();
            energyFill.sprite = whiteSprite;
            energyFill.color = fullColor; // Calm teal
            energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal;
            energyFill.fillOrigin = 0; // Depletes from right to left
            energyFill.fillAmount = 1f;

            // LAYER 2: Frame overlay (created after fill, renders on top)
            GameObject frameObj = new GameObject("Frame");
            frameObj.transform.SetParent(energyHUD.transform, false);
            RectTransform frameRect = frameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frameRect.localScale = new Vector3(1f, 2f, 1f);
            Image frameImage = frameObj.AddComponent<Image>();

            // Load frame sprite from Resources at runtime
            Sprite frameSprite = energyBarFrameSprite;
            if (frameSprite == null)
            {
                frameSprite = Resources.Load<Sprite>("UI/energy_bar_frame");
            }

            if (frameSprite != null)
            {
                frameImage.sprite = frameSprite;
                frameImage.type = Image.Type.Simple;
                frameImage.preserveAspect = false;
                frameImage.color = Color.white;
            }
            else
            {
                // Fallback: dark panel
                frameImage.sprite = whiteSprite;
                frameImage.color = new Color(0.1f, 0.12f, 0.14f, 0.95f);
                Debug.LogWarning("[EnergyUI] Could not load energy_bar_frame from Resources/UI/. Using fallback.");
            }

            // LAYER 3: Energy text below the bar
            GameObject textObj = new GameObject("EnergyText");
            textObj.transform.SetParent(energyHUD.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, -0.35f);
            textRect.anchorMax = new Vector2(1, 0f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            energyText = textObj.AddComponent<TextMeshProUGUI>();
            energyText.text = "100/100";
            energyText.fontSize = 14;
            energyText.color = new Color(0.7f, 0.75f, 0.8f, 0.9f);
            energyText.alignment = TextAlignmentOptions.Center;
            energyText.enableWordWrapping = false;

            // LAYER 4: Energy drink pop-up (created on-demand via CreateEnergyDrinkHint)
            // Don't create here - will be created when first needed
        }


        private Transform FindChildRecursive(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null) return found;

            foreach (Transform child in parent)
            {
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private T FindChildComponent<T>(Transform parent, string name) where T : Component
        {
            var found = FindChildRecursive(parent, name);
            return found?.GetComponent<T>();
        }

        private void OnDestroy()
        {
            if (energyManager != null)
            {
                energyManager.OnEnergyChanged -= UpdateEnergyDisplay;
                energyManager.OnLowEnergy -= OnLowEnergy;
                energyManager.OnEnergyDepleted -= OnEnergyDepleted;
                energyManager.OnEnergyRestored -= OnEnergyRestored;
            }
        }

        private void Update()
        {
            // Subtle pulse effect when low on energy (brightness only, no color change)
            if (isLowEnergy && pulseOnLow && energyFill != null)
            {
                float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                energyFill.color = Color.Lerp(fullColor, Color.white, pulse * 0.15f);
            }

            // Update net rate display - subtle coloring
            if (netRateText != null && energyManager != null)
            {
                float netRate = energyManager.GetNetEnergyRate();
                string sign = netRate >= 0 ? "+" : "";
                netRateText.text = $"{sign}{netRate:F1}/s";
                netRateText.color = netRate >= 0
                    ? new Color(0.4f, 0.8f, 0.6f, 0.8f)  // Subtle green
                    : new Color(0.9f, 0.5f, 0.4f, 0.8f); // Subtle red
            }

            // Handle energy drink hint auto-hide
            if (isHintShowing)
            {
                // Hide hint if player presses R (uses a drink)
                if (Input.GetKeyDown(KeyCode.R))
                {
                    HideEnergyDrinkHint();
                }
                // Auto-hide after duration
                else if (Time.time >= hintHideTime)
                {
                    HideEnergyDrinkHint();
                }
            }
        }

        /// <summary>
        /// Shows the energy drink pop-up to the player.
        /// Always shows when energy depletes - text changes based on drink availability.
        /// </summary>
        public void ShowEnergyDrinkHint()
        {
            // Create hint if it doesn't exist yet
            if (energyDrinkHint == null)
            {
                CreateEnergyDrinkHint();
            }

            if (energyDrinkHint != null)
            {
                // Update text based on drink availability
                if (hintText != null)
                {
                    if (energyManager != null && energyManager.DrinkCount > 0)
                    {
                        hintText.text = $"Press  R  to use Energy Drink  ({energyManager.DrinkCount}/{energyManager.MaxDrinks})";
                    }
                    else
                    {
                        hintText.text = "No Energy Drinks! Buy more at the station.";
                    }
                }

                energyDrinkHint.SetActive(true);
                isHintShowing = true;
                hintHideTime = Time.time + hintDisplayDuration;

                // Animate: scale pop-up from small to full size for attention
                StartCoroutine(PopUpAnimation());
            }
        }

        /// <summary>
        /// Animate the pop-up scaling in for visual punch.
        /// </summary>
        private System.Collections.IEnumerator PopUpAnimation()
        {
            if (energyDrinkHint == null) yield break;
            RectTransform rect = energyDrinkHint.GetComponent<RectTransform>();
            if (rect == null) yield break;

            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // Overshoot bounce: scale goes to 1.15 then back to 1.0
                float scale = t < 0.6f
                    ? Mathf.Lerp(0.3f, 1.15f, t / 0.6f)
                    : Mathf.Lerp(1.15f, 1.0f, (t - 0.6f) / 0.4f);
                rect.localScale = Vector3.one * scale;
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        /// <summary>
        /// Creates the energy drink pop-up UI centered on screen.
        /// Prominent and eye-catching to teach the drink mechanic.
        /// </summary>
        private void CreateEnergyDrinkHint()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            if (canvas == null)
            {
                return;
            }

            // Center-screen pop-up panel
            energyDrinkHint = new GameObject("EnergyDrinkPopUp");
            energyDrinkHint.transform.SetParent(canvas.transform, false);
            RectTransform hintRect = energyDrinkHint.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = Vector2.zero; // Dead center
            hintRect.sizeDelta = new Vector2(500, 80);

            // Dark background with colored border feel
            Image hintBg = energyDrinkHint.AddComponent<Image>();
            hintBg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            // Outline for visual punch
            var outline = energyDrinkHint.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.75f, 0.2f, 0.8f); // Gold border
            outline.effectDistance = new Vector2(2, 2);

            // Text
            GameObject hintTextObj = new GameObject("HintText");
            hintTextObj.transform.SetParent(energyDrinkHint.transform, false);
            RectTransform hintTextRect = hintTextObj.AddComponent<RectTransform>();
            hintTextRect.anchorMin = Vector2.zero;
            hintTextRect.anchorMax = Vector2.one;
            hintTextRect.offsetMin = new Vector2(15, 5);
            hintTextRect.offsetMax = new Vector2(-15, -5);
            hintText = hintTextObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "Press  R  to use Energy Drink";
            hintText.fontSize = 26;
            hintText.fontStyle = TMPro.FontStyles.Bold;
            hintText.color = new Color(1f, 0.9f, 0.35f); // Bright gold
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.enableWordWrapping = false;
        }

        /// <summary>
        /// Hides the energy drink hint.
        /// </summary>
        public void HideEnergyDrinkHint()
        {
            if (energyDrinkHint != null)
            {
                energyDrinkHint.SetActive(false);
            }
            isHintShowing = false;
        }

        private void UpdateEnergyDisplay(float current, float max)
        {
            float percent = max > 0 ? current / max : 0;

            // Update bar
            if (energyBar != null)
            {
                energyBar.value = percent;
            }

            // Update fill amount and keep color constant
            if (energyFill != null)
            {
                energyFill.fillAmount = percent; // THIS updates the bar!
                energyFill.color = fullColor;    // Always teal
            }

            // Always show energy text (current/max)
            if (energyText != null)
            {
                energyText.text = $"{current:F0}/{max:F0}";
            }

            // Percent text hidden (not used in modern UI)
            if (energyPercentText != null)
            {
                energyPercentText.text = "";
                energyPercentText.gameObject.SetActive(false);
            }

            // Update low energy state
            isLowEnergy = percent <= lowThreshold;
        }

        private Color GetEnergyColor(float percent)
        {
            if (percent <= lowThreshold)
            {
                return lowColor;
            }
            else if (percent <= mediumThreshold)
            {
                return mediumColor;
            }
            else
            {
                return fullColor;
            }
        }

        private void OnLowEnergy()
        {
            isLowEnergy = true;

            if (lowEnergyWarning != null)
            {
                lowEnergyWarning.SetActive(true);
            }
        }

        private void OnEnergyDepleted()
        {
            if (noEnergyWarning != null)
            {
                noEnergyWarning.SetActive(true);
            }

            if (lowEnergyWarning != null)
            {
                lowEnergyWarning.SetActive(false);
            }

            // Show center-screen energy drink pop-up
            ShowEnergyDrinkHint();
        }

        private void OnEnergyRestored()
        {
            if (noEnergyWarning != null)
            {
                noEnergyWarning.SetActive(false);
            }

            // Hide drink hint when energy is restored
            HideEnergyDrinkHint();

            // Re-evaluate low energy state
            if (energyManager != null && energyManager.IsLowEnergy)
            {
                if (lowEnergyWarning != null)
                {
                    lowEnergyWarning.SetActive(true);
                }
            }
            else
            {
                isLowEnergy = false;
                if (lowEnergyWarning != null)
                {
                    lowEnergyWarning.SetActive(false);
                }
            }
        }

        public void ShowHUD()
        {
            if (energyHUD != null)
            {
                energyHUD.SetActive(true);
            }
        }

        public void HideHUD()
        {
            if (energyHUD != null)
            {
                energyHUD.SetActive(false);
            }
        }

        public void ToggleHUD()
        {
            if (energyHUD != null)
            {
                energyHUD.SetActive(!energyHUD.activeSelf);
            }
        }
    }
}
