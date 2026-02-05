using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BeneathTheFloor.TreasureChests
{
    /// <summary>
    /// UI for displaying treasure chest rewards.
    /// Shows a popup with reward icon, name, and description.
    /// Auto-creates UI if needed.
    /// </summary>
    public class TreasureChestRewardUI : MonoBehaviour
    {
        public static TreasureChestRewardUI Instance { get; private set; }

        [Header("UI References (Auto-created if null)")]
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TextMeshProUGUI rewardTitle;
        [SerializeField] private TextMeshProUGUI rewardDescription;
        [SerializeField] private Image panelBackground;

        [Header("Settings")]
        [Tooltip("How long to show each reward")]
        [SerializeField] private float displayDuration = 3f;

        [Tooltip("Fade in/out duration")]
        [SerializeField] private float fadeDuration = 0.3f;

        [Tooltip("Position offset from center")]
        [SerializeField] private Vector2 panelOffset = new Vector2(0, 220);

        [Header("Styling")]
        [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        [SerializeField] private Color titleColor = new Color(1f, 0.9f, 0.5f);
        [SerializeField] private Color descriptionColor = Color.white;

        [Header("Audio")]
        [SerializeField] private AudioClip popupSound;
        [SerializeField] private float popupVolume = 0.5f;

        // State
        private Queue<TreasureChestReward> pendingRewards = new Queue<TreasureChestReward>();
        private bool isShowing = false;
        private Canvas parentCanvas;
        private CanvasGroup canvasGroup;
        private AudioSource audioSource;

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
            // Find parent canvas
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindObjectOfType<Canvas>();
            }

            // Setup audio
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound

            // Create UI if not assigned
            if (rewardPanel == null)
            {
                CreateRewardUI();
            }

            // Initially hide
            if (rewardPanel != null)
            {
                rewardPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Show a reward popup.
        /// </summary>
        public void ShowReward(TreasureChestReward reward)
        {
            if (reward == null)
                return;

            pendingRewards.Enqueue(reward);

            if (!isShowing)
            {
                StartCoroutine(ShowRewardSequence());
            }
        }

        /// <summary>
        /// Show multiple rewards in sequence.
        /// </summary>
        public void ShowRewards(TreasureChestReward[] rewards)
        {
            if (rewards == null)
                return;

            foreach (var reward in rewards)
            {
                if (reward != null)
                {
                    pendingRewards.Enqueue(reward);
                }
            }

            if (!isShowing)
            {
                StartCoroutine(ShowRewardSequence());
            }
        }

        private IEnumerator ShowRewardSequence()
        {
            isShowing = true;

            while (pendingRewards.Count > 0)
            {
                var reward = pendingRewards.Dequeue();
                yield return StartCoroutine(ShowSingleReward(reward));
            }

            isShowing = false;
        }

        private IEnumerator ShowSingleReward(TreasureChestReward reward)
        {
            // Update UI content
            UpdateRewardDisplay(reward);

            // Play sound
            if (popupSound != null)
            {
                audioSource.PlayOneShot(popupSound, popupVolume);
            }
            else if (reward.rewardSound != null)
            {
                audioSource.PlayOneShot(reward.rewardSound, reward.rewardVolume);
            }

            // Show panel
            rewardPanel.SetActive(true);

            // Fade in
            yield return StartCoroutine(FadePanel(0f, 1f, fadeDuration));

            // Wait
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            yield return StartCoroutine(FadePanel(1f, 0f, fadeDuration));

            // Hide panel
            rewardPanel.SetActive(false);

            // Small delay between rewards
            if (pendingRewards.Count > 0)
            {
                yield return new WaitForSeconds(0.2f);
            }
        }

        private IEnumerator FadePanel(float from, float to, float duration)
        {
            if (canvasGroup == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private void UpdateRewardDisplay(TreasureChestReward reward)
        {
            // Title
            if (rewardTitle != null)
            {
                rewardTitle.text = reward.displayName;
                rewardTitle.color = reward.rewardColor != Color.white ? reward.rewardColor : titleColor;
            }

            // Description
            if (rewardDescription != null)
            {
                string desc = reward.GetFormattedDescription();

                // Add note content if it's a note
                if (reward.rewardType == RewardType.Note && !string.IsNullOrEmpty(reward.noteContent))
                {
                    desc = reward.noteContent;
                }
                else if (!string.IsNullOrEmpty(reward.description))
                {
                    desc = reward.description;
                }

                rewardDescription.text = desc;
            }

            // Icon
            if (rewardIcon != null)
            {
                Sprite icon = reward.GetIcon();
                if (icon != null)
                {
                    rewardIcon.sprite = icon;
                    rewardIcon.enabled = true;
                }
                else
                {
                    rewardIcon.enabled = false;
                }
            }

            // Background color
            if (panelBackground != null)
            {
                panelBackground.color = panelColor;
            }
        }

        private void CreateRewardUI()
        {
            if (parentCanvas == null)
            {
                // Debug.LogError("[TreasureChestRewardUI] No Canvas found!");
                return;
            }

            // Create panel
            rewardPanel = new GameObject("TreasureRewardPanel");
            rewardPanel.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRect = rewardPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = panelOffset;
            panelRect.sizeDelta = new Vector2(400, 150);

            // Canvas group for fading
            canvasGroup = rewardPanel.AddComponent<CanvasGroup>();

            // Background image
            panelBackground = rewardPanel.AddComponent<Image>();
            panelBackground.color = panelColor;

            // Rounded corners (optional - use sprite if available)
            panelBackground.type = Image.Type.Sliced;

            // Vertical layout group
            var vlg = rewardPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 15, 15);
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Create icon container
            GameObject iconContainer = new GameObject("IconContainer");
            iconContainer.transform.SetParent(rewardPanel.transform, false);

            RectTransform iconContainerRect = iconContainer.AddComponent<RectTransform>();
            iconContainerRect.sizeDelta = new Vector2(64, 64);

            var iconLE = iconContainer.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 64;
            iconLE.preferredHeight = 64;

            // Create icon
            GameObject iconObj = new GameObject("RewardIcon");
            iconObj.transform.SetParent(iconContainer.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            rewardIcon = iconObj.AddComponent<Image>();
            rewardIcon.preserveAspect = true;

            // Create title
            GameObject titleObj = new GameObject("RewardTitle");
            titleObj.transform.SetParent(rewardPanel.transform, false);

            rewardTitle = titleObj.AddComponent<TextMeshProUGUI>();
            rewardTitle.text = "Reward!";
            rewardTitle.fontSize = 28;
            rewardTitle.color = titleColor;
            rewardTitle.alignment = TextAlignmentOptions.Center;
            rewardTitle.fontStyle = FontStyles.Bold;

            var titleLE = titleObj.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 35;

            // Create description
            GameObject descObj = new GameObject("RewardDescription");
            descObj.transform.SetParent(rewardPanel.transform, false);

            rewardDescription = descObj.AddComponent<TextMeshProUGUI>();
            rewardDescription.text = "You found something!";
            rewardDescription.fontSize = 18;
            rewardDescription.color = descriptionColor;
            rewardDescription.alignment = TextAlignmentOptions.Center;

            var descLE = descObj.AddComponent<LayoutElement>();
            descLE.preferredHeight = 50;
            descLE.flexibleHeight = 1;

            // Debug.Log("[TreasureChestRewardUI] Created reward popup UI");
        }

        /// <summary>
        /// Show a simple text message (for notes).
        /// </summary>
        public void ShowNoteMessage(string title, string content)
        {
            // Create a temporary reward for the note
            var noteReward = ScriptableObject.CreateInstance<TreasureChestReward>();
            noteReward.displayName = title;
            noteReward.noteContent = content;
            noteReward.rewardType = RewardType.Note;

            ShowReward(noteReward);

            // Cleanup temp object after showing
            Destroy(noteReward, displayDuration + fadeDuration * 2 + 1f);
        }

        /// <summary>
        /// Clear all pending rewards.
        /// </summary>
        public void ClearPending()
        {
            pendingRewards.Clear();
        }
    }
}
