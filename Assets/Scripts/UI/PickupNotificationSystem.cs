using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Lightweight notification system for item pickups.
    /// Shows toast notifications on the right side of the screen that fade out.
    /// </summary>
    public class PickupNotificationSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float displayDuration = 1.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float slideInDuration = 0.2f;
        [SerializeField] private int maxVisibleNotifications = 5;
        [SerializeField] private float notificationSpacing = 5f;
        [SerializeField] private float notificationHeight = 35f;

        [Header("Appearance")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        [SerializeField] private Color textColor = new Color(0.9f, 0.95f, 0.9f);
        [SerializeField] private Color amountColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private int fontSize = 16;

        private Transform notificationContainer;
        private List<NotificationEntry> activeNotifications = new List<NotificationEntry>();
        private Queue<NotificationEntry> notificationPool = new Queue<NotificationEntry>();

        public static PickupNotificationSystem Instance { get; private set; }

        private class NotificationEntry
        {
            public GameObject gameObject;
            public RectTransform rectTransform;
            public TextMeshProUGUI text;
            public CanvasGroup canvasGroup;
            public Image background;
            public Coroutine fadeCoroutine;
            public int targetIndex;
        }

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
        }

        private void Start()
        {
            CreateNotificationContainer();
        }

        private void CreateNotificationContainer()
        {
            // Find or use existing HUD Canvas
            Canvas hudCanvas = GetComponentInParent<Canvas>();
            if (hudCanvas == null)
            {
                hudCanvas = FindHUDCanvas();
            }

            if (hudCanvas == null)
            {
                Debug.LogError("[PickupNotificationSystem] No HUD Canvas found!");
                return;
            }

            // Create container on the right side of the screen
            GameObject container = new GameObject("NotificationContainer");
            container.transform.SetParent(hudCanvas.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1, 0.5f);
            containerRect.anchorMax = new Vector2(1, 0.5f);
            containerRect.pivot = new Vector2(1, 0.5f);
            containerRect.anchoredPosition = new Vector2(-20, 50); // Right side, slightly above center
            containerRect.sizeDelta = new Vector2(300, 400);

            notificationContainer = container.transform;
        }

        private Canvas FindHUDCanvas()
        {
            // Look for HUDCanvas specifically
            GameObject hudObj = GameObject.Find("HUDCanvas");
            if (hudObj != null)
            {
                return hudObj.GetComponent<Canvas>();
            }

            // Fallback to any overlay canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                    !canvas.name.Contains("Machine"))
                {
                    return canvas;
                }
            }

            return null;
        }

        /// <summary>
        /// Show a pickup notification for an item.
        /// </summary>
        public void ShowPickup(string itemName, int amount)
        {
            if (notificationContainer == null)
            {
                Debug.LogWarning("[PickupNotificationSystem] Container not ready");
                return;
            }

            string message = $"+{amount} {itemName} collected";
            ShowNotification(message);
        }

        /// <summary>
        /// Show a generic notification message.
        /// </summary>
        public void ShowNotification(string message)
        {
            if (notificationContainer == null) return;

            // Remove oldest notification if at max
            while (activeNotifications.Count >= maxVisibleNotifications)
            {
                RemoveNotification(activeNotifications[0], immediate: true);
            }

            // Get or create notification entry
            NotificationEntry entry = GetNotificationEntry();
            entry.text.text = message;
            entry.gameObject.SetActive(true);
            entry.targetIndex = activeNotifications.Count;

            // Position at the bottom of the stack
            PositionNotification(entry, entry.targetIndex, animate: true);

            activeNotifications.Add(entry);

            // Start fade out timer
            entry.fadeCoroutine = StartCoroutine(FadeOutAfterDelay(entry));
        }

        private NotificationEntry GetNotificationEntry()
        {
            NotificationEntry entry;

            if (notificationPool.Count > 0)
            {
                entry = notificationPool.Dequeue();
                entry.canvasGroup.alpha = 1f;
            }
            else
            {
                entry = CreateNotificationEntry();
            }

            return entry;
        }

        private NotificationEntry CreateNotificationEntry()
        {
            GameObject notifObj = new GameObject("Notification");
            notifObj.transform.SetParent(notificationContainer, false);

            RectTransform rect = notifObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(280, notificationHeight);

            // Background
            Image bg = notifObj.AddComponent<Image>();
            bg.color = backgroundColor;

            // Canvas group for fading
            CanvasGroup group = notifObj.AddComponent<CanvasGroup>();

            // Horizontal layout
            HorizontalLayoutGroup layout = notifObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(notifObj.transform, false);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.fontStyle = FontStyles.Normal;

            // Make the + and number colored
            text.enableWordWrapping = false;

            NotificationEntry entry = new NotificationEntry
            {
                gameObject = notifObj,
                rectTransform = rect,
                text = text,
                canvasGroup = group,
                background = bg
            };

            return entry;
        }

        private void PositionNotification(NotificationEntry entry, int index, bool animate)
        {
            float targetY = -index * (notificationHeight + notificationSpacing);
            Vector2 targetPos = new Vector2(0, targetY);

            if (animate)
            {
                // Start from off-screen right
                entry.rectTransform.anchoredPosition = new Vector2(300, targetY);
                entry.canvasGroup.alpha = 0f;
                StartCoroutine(SlideIn(entry, targetPos));
            }
            else
            {
                entry.rectTransform.anchoredPosition = targetPos;
            }
        }

        private IEnumerator SlideIn(NotificationEntry entry, Vector2 targetPos)
        {
            float elapsed = 0f;
            Vector2 startPos = entry.rectTransform.anchoredPosition;

            while (elapsed < slideInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / slideInDuration);
                entry.rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                entry.canvasGroup.alpha = t;
                yield return null;
            }

            entry.rectTransform.anchoredPosition = targetPos;
            entry.canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutAfterDelay(NotificationEntry entry)
        {
            // Wait for display duration
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / fadeOutDuration);
                entry.canvasGroup.alpha = t;
                yield return null;
            }

            RemoveNotification(entry, immediate: false);
        }

        private void RemoveNotification(NotificationEntry entry, bool immediate)
        {
            if (entry.fadeCoroutine != null && immediate)
            {
                StopCoroutine(entry.fadeCoroutine);
            }

            activeNotifications.Remove(entry);
            entry.gameObject.SetActive(false);
            notificationPool.Enqueue(entry);

            // Reposition remaining notifications
            for (int i = 0; i < activeNotifications.Count; i++)
            {
                activeNotifications[i].targetIndex = i;
                StartCoroutine(AnimateToPosition(activeNotifications[i], i));
            }
        }

        private IEnumerator AnimateToPosition(NotificationEntry entry, int index)
        {
            float targetY = -index * (notificationHeight + notificationSpacing);
            Vector2 targetPos = new Vector2(0, targetY);
            Vector2 startPos = entry.rectTransform.anchoredPosition;

            float elapsed = 0f;
            float duration = 0.15f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                entry.rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            entry.rectTransform.anchoredPosition = targetPos;
        }

        /// <summary>
        /// Clear all notifications immediately.
        /// </summary>
        public void ClearAll()
        {
            foreach (var entry in activeNotifications.ToArray())
            {
                RemoveNotification(entry, immediate: true);
            }
        }
    }
}
