using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace BeneathTheFloor.UI
{
    public class UIFXManager : MonoBehaviour
    {
        [Header("Floating Text")]
        [SerializeField] private GameObject floatingTextPrefab;
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private float floatSpeed = 1f;
        [SerializeField] private float floatDuration = 1.5f;

        [Header("Resource Popup")]
        [SerializeField] private GameObject resourcePopupPrefab;
        [SerializeField] private Transform popupContainer;
        [SerializeField] private float popupDuration = 2f;

        [Header("Screen Flash")]
        [SerializeField] private Image screenFlashImage;
        [SerializeField] private float flashDuration = 0.2f;

        [Header("Button Effects")]
        [SerializeField] private float buttonPressScale = 0.95f;
        [SerializeField] private float buttonHoverScale = 1.05f;
        [SerializeField] private float buttonAnimDuration = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color resourceGainColor = new Color(0.2f, 1f, 0.3f);
        [SerializeField] private Color resourceLossColor = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private Color healFlashColor = new Color(0f, 1f, 0f, 0.2f);
        [SerializeField] private Color itemPickupColor = new Color(1f, 1f, 0.3f);

        [Header("Audio")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip buttonHoverSound;
        [SerializeField] private AudioClip resourceGainSound;
        [SerializeField] private AudioClip errorSound;
        [SerializeField] private AudioClip successSound;

        public static UIFXManager Instance { get; private set; }

        private Queue<GameObject> floatingTextPool = new Queue<GameObject>();
        private Queue<GameObject> popupPool = new Queue<GameObject>();
        private const int POOL_SIZE = 10;

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

            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
                uiAudioSource.playOnAwake = false;
            }

            InitializePools();
            CreateScreenFlashImage();
        }

        private void Start()
        {
            // Subscribe to events
            GameEvents.OnResourceCollected += OnResourceCollected;
            GameEvents.OnFloatingText += ShowFloatingText;
        }

        private void OnDestroy()
        {
            GameEvents.OnResourceCollected -= OnResourceCollected;
            GameEvents.OnFloatingText -= ShowFloatingText;
        }

        private void InitializePools()
        {
            if (worldCanvas == null)
            {
                CreateWorldCanvas();
            }

            // Create floating text pool
            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject floatText = CreateFloatingTextObject();
                floatText.SetActive(false);
                floatingTextPool.Enqueue(floatText);
            }
        }

        private void CreateWorldCanvas()
        {
            GameObject canvasObj = new GameObject("WorldCanvas");
            canvasObj.transform.SetParent(transform);

            worldCanvas = canvasObj.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
        }

        private void CreateScreenFlashImage()
        {
            if (screenFlashImage != null) return;

            // Find or create screen space canvas
            Canvas screenCanvas = FindObjectOfType<Canvas>();
            if (screenCanvas == null || screenCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                GameObject canvasObj = new GameObject("ScreenFlashCanvas");
                canvasObj.transform.SetParent(transform);
                screenCanvas = canvasObj.AddComponent<Canvas>();
                screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                screenCanvas.sortingOrder = 999;
            }

            GameObject flashObj = new GameObject("ScreenFlash");
            flashObj.transform.SetParent(screenCanvas.transform);

            screenFlashImage = flashObj.AddComponent<Image>();
            screenFlashImage.color = Color.clear;
            screenFlashImage.raycastTarget = false;

            RectTransform rect = screenFlashImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private GameObject CreateFloatingTextObject()
        {
            GameObject obj = new GameObject("FloatingText");
            obj.transform.SetParent(worldCanvas.transform);

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);

            return obj;
        }

        private void OnResourceCollected(ResourceType type, int amount)
        {
            string text = $"+{amount} {type}";
            ShowResourcePopup(text, resourceGainColor);
            PlaySound(resourceGainSound);
        }

        public void ShowFloatingText(string text, Vector3 worldPosition)
        {
            ShowFloatingText(text, worldPosition, Color.white);
        }

        public void ShowFloatingText(string text, Vector3 worldPosition, Color color)
        {
            GameObject floatText;

            if (floatingTextPool.Count > 0)
            {
                floatText = floatingTextPool.Dequeue();
                floatText.SetActive(true);
            }
            else
            {
                floatText = CreateFloatingTextObject();
            }

            floatText.transform.position = worldPosition;

            TextMeshProUGUI tmp = floatText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.color = color;
            }

            StartCoroutine(FloatTextCoroutine(floatText));
        }

        private IEnumerator FloatTextCoroutine(GameObject floatText)
        {
            Vector3 startPos = floatText.transform.position;
            float elapsed = 0f;

            TextMeshProUGUI tmp = floatText.GetComponent<TextMeshProUGUI>();
            Color startColor = tmp != null ? tmp.color : Color.white;

            // Make text face camera
            Camera mainCam = Camera.main;

            while (elapsed < floatDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / floatDuration;

                // Float upward
                floatText.transform.position = startPos + Vector3.up * floatSpeed * t;

                // Face camera
                if (mainCam != null)
                {
                    floatText.transform.rotation = Quaternion.LookRotation(
                        floatText.transform.position - mainCam.transform.position
                    );
                }

                // Fade out
                if (tmp != null)
                {
                    tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                }

                // Scale down slightly
                float scale = Mathf.Lerp(1f, 0.5f, t);
                floatText.transform.localScale = Vector3.one * scale * 0.01f;

                yield return null;
            }

            floatText.SetActive(false);
            floatingTextPool.Enqueue(floatText);
        }

        public void ShowResourcePopup(string text, Color color)
        {
            if (popupContainer == null) return;

            // Create simple popup (would normally use prefab)
            GameObject popup = new GameObject("ResourcePopup");
            popup.transform.SetParent(popupContainer);

            TextMeshProUGUI tmp = popup.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform rect = popup.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(200, 40);

            StartCoroutine(PopupCoroutine(popup));
        }

        private IEnumerator PopupCoroutine(GameObject popup)
        {
            float elapsed = 0f;
            RectTransform rect = popup.GetComponent<RectTransform>();
            TextMeshProUGUI tmp = popup.GetComponent<TextMeshProUGUI>();

            Vector2 startPos = rect.anchoredPosition;
            Color startColor = tmp != null ? tmp.color : Color.white;

            // Pop in animation
            rect.localScale = Vector3.zero;
            float popInDuration = 0.2f;
            float popInElapsed = 0f;

            while (popInElapsed < popInDuration)
            {
                popInElapsed += Time.deltaTime;
                float t = popInElapsed / popInDuration;
                rect.localScale = Vector3.one * EaseOutBack(t);
                yield return null;
            }

            rect.localScale = Vector3.one;

            // Hold and slide up
            while (elapsed < popupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popupDuration;

                rect.anchoredPosition = startPos + Vector2.up * 30f * t;

                if (tmp != null && t > 0.7f)
                {
                    float fadeT = (t - 0.7f) / 0.3f;
                    tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - fadeT);
                }

                yield return null;
            }

            Destroy(popup);
        }

        public void FlashScreen(Color color)
        {
            StartCoroutine(ScreenFlashCoroutine(color));
        }

        public void FlashDamage()
        {
            FlashScreen(damageFlashColor);
        }

        public void FlashHeal()
        {
            FlashScreen(healFlashColor);
        }

        private IEnumerator ScreenFlashCoroutine(Color color)
        {
            if (screenFlashImage == null) yield break;

            screenFlashImage.color = color;

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                screenFlashImage.color = Color.Lerp(color, Color.clear, t);
                yield return null;
            }

            screenFlashImage.color = Color.clear;
        }

        public void AnimateButton(RectTransform button, bool pressed)
        {
            StartCoroutine(ButtonAnimCoroutine(button, pressed ? buttonPressScale : buttonHoverScale));
        }

        private IEnumerator ButtonAnimCoroutine(RectTransform button, float targetScale)
        {
            if (button == null) yield break;

            Vector3 startScale = button.localScale;
            Vector3 endScale = Vector3.one * targetScale;

            float elapsed = 0f;
            while (elapsed < buttonAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / buttonAnimDuration;
                button.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            button.localScale = endScale;
        }

        public void ResetButtonScale(RectTransform button)
        {
            if (button != null)
            {
                StartCoroutine(ButtonAnimCoroutine(button, 1f));
            }
        }

        public void PlayButtonClick()
        {
            PlaySound(buttonClickSound);
        }

        public void PlayButtonHover()
        {
            PlaySound(buttonHoverSound, 0.5f);
        }

        public void PlayError()
        {
            PlaySound(errorSound);
            FlashScreen(new Color(1f, 0f, 0f, 0.2f));
        }

        public void PlaySuccess()
        {
            PlaySound(successSound);
            FlashScreen(new Color(0f, 1f, 0f, 0.1f));
        }

        private void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (clip != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(clip, volume);
            }
        }

        public void ShakeUI(RectTransform target, float intensity = 5f, float duration = 0.2f)
        {
            StartCoroutine(UIShakeCoroutine(target, intensity, duration));
        }

        private IEnumerator UIShakeCoroutine(RectTransform target, float intensity, float duration)
        {
            if (target == null) yield break;

            Vector2 originalPos = target.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float strength = intensity * (1f - (elapsed / duration));

                float x = Random.Range(-strength, strength);
                float y = Random.Range(-strength, strength);

                target.anchoredPosition = originalPos + new Vector2(x, y);
                yield return null;
            }

            target.anchoredPosition = originalPos;
        }

        public void PulseElement(RectTransform target, float scale = 1.2f, float duration = 0.3f)
        {
            StartCoroutine(PulseCoroutine(target, scale, duration));
        }

        private IEnumerator PulseCoroutine(RectTransform target, float scale, float duration)
        {
            if (target == null) yield break;

            float halfDuration = duration / 2f;
            Vector3 originalScale = target.localScale;
            Vector3 targetScale = originalScale * scale;

            // Scale up
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                target.localScale = Vector3.Lerp(originalScale, targetScale, EaseOutQuad(t));
                yield return null;
            }

            // Scale down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                target.localScale = Vector3.Lerp(targetScale, originalScale, EaseInQuad(t));
                yield return null;
            }

            target.localScale = originalScale;
        }

        // Easing functions
        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private float EaseInQuad(float t)
        {
            return t * t;
        }
    }
}
