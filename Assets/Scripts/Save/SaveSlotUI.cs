using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// UI component for displaying a single save slot in the save/load menu.
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI playTimeText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private Image activeIndicator;
        [SerializeField] private Image emptySlotOverlay;
        [SerializeField] private Button slotButton;
        [SerializeField] private Button deleteButton;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.25f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.3f, 0.4f, 1f);
        [SerializeField] private Color activeSlotColor = new Color(0.2f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color emptySlotColor = new Color(0.15f, 0.15f, 0.18f, 0.7f);

        private SaveSlotInfo slotInfo;
        private bool isSelected;
        private Image backgroundImage;

        /// <summary>
        /// Event fired when this slot is clicked.
        /// </summary>
        public System.Action<int> OnSlotClicked;

        /// <summary>
        /// Event fired when the delete button is clicked.
        /// </summary>
        public System.Action<int> OnDeleteClicked;

        /// <summary>
        /// Get the slot ID this UI represents.
        /// </summary>
        public int SlotId => slotInfo?.slotId ?? -1;

        /// <summary>
        /// Check if this slot has data.
        /// </summary>
        public bool HasData => slotInfo?.hasData ?? false;

        private void Awake()
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
            }

            if (slotButton != null)
            {
                slotButton.onClick.AddListener(OnSlotButtonClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(OnDeleteButtonClicked);
            }
        }

        /// <summary>
        /// Initialize this slot UI with slot data.
        /// </summary>
        public void Initialize(SaveSlotInfo info, bool isActiveSlot = false)
        {
            slotInfo = info;

            if (info == null)
            {
                SetEmpty();
                return;
            }

            if (info.hasData)
            {
                SetPopulated(info, isActiveSlot);
            }
            else
            {
                SetEmpty();
            }
        }

        /// <summary>
        /// Set this slot as empty.
        /// </summary>
        private void SetEmpty()
        {
            if (slotNameText != null)
            {
                slotNameText.text = slotInfo?.slotId == 0 ? "Autosave - Empty" : $"Empty Slot";
            }

            if (dateText != null)
            {
                dateText.text = "No save data";
            }

            if (playTimeText != null)
            {
                playTimeText.text = "";
            }

            if (missionText != null)
            {
                missionText.text = "";
            }

            if (emptySlotOverlay != null)
            {
                emptySlotOverlay.gameObject.SetActive(true);
            }

            if (activeIndicator != null)
            {
                activeIndicator.gameObject.SetActive(false);
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(false);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = emptySlotColor;
            }
        }

        /// <summary>
        /// Set this slot with save data.
        /// </summary>
        private void SetPopulated(SaveSlotInfo info, bool isActiveSlot)
        {
            if (slotNameText != null)
            {
                string displayName = info.GetDisplayName();
                if (isActiveSlot)
                {
                    displayName += " *";
                }
                slotNameText.text = displayName;
            }

            if (dateText != null)
            {
                dateText.text = info.GetFormattedDate();
            }

            if (playTimeText != null)
            {
                playTimeText.text = info.GetFormattedPlayTime();
            }

            if (missionText != null)
            {
                if (info.missionIndex >= 0)
                {
                    missionText.text = $"Mission {info.missionIndex + 1}";
                }
                else
                {
                    missionText.text = "";
                }
            }

            if (emptySlotOverlay != null)
            {
                emptySlotOverlay.gameObject.SetActive(false);
            }

            if (activeIndicator != null)
            {
                activeIndicator.gameObject.SetActive(isActiveSlot);
            }

            if (deleteButton != null)
            {
                // Allow deleting any slot except autosave while it's the active slot
                deleteButton.gameObject.SetActive(!(info.slotId == 0 && isActiveSlot));
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = isActiveSlot ? activeSlotColor : normalColor;
            }
        }

        /// <summary>
        /// Set selection state.
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;

            if (backgroundImage != null && slotInfo != null)
            {
                if (selected)
                {
                    backgroundImage.color = selectedColor;
                }
                else
                {
                    backgroundImage.color = slotInfo.hasData ?
                        (SaveManager.Instance?.ActiveSlotId == slotInfo.slotId ? activeSlotColor : normalColor) :
                        emptySlotColor;
                }
            }
        }

        private void OnSlotButtonClicked()
        {
            OnSlotClicked?.Invoke(slotInfo?.slotId ?? -1);
        }

        private void OnDeleteButtonClicked()
        {
            OnDeleteClicked?.Invoke(slotInfo?.slotId ?? -1);
        }

        /// <summary>
        /// Create a SaveSlotUI component programmatically.
        /// </summary>
        public static SaveSlotUI Create(Transform parent, SaveSlotInfo info, bool isActiveSlot = false)
        {
            // Create the slot container
            GameObject slotObj = new GameObject($"SaveSlot_{info?.slotId ?? 0}");
            slotObj.transform.SetParent(parent, false);

            // Add RectTransform
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(0, 80);

            // Add background image
            Image bg = slotObj.AddComponent<Image>();
            bg.color = info?.hasData == true ? new Color(0.2f, 0.2f, 0.25f, 0.9f) : new Color(0.15f, 0.15f, 0.18f, 0.7f);

            // Make the entire slot a button
            Button slotButton = slotObj.AddComponent<Button>();
            slotButton.targetGraphic = bg;

            // Create layout
            HorizontalLayoutGroup layout = slotObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 10, 10);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Active indicator (small colored bar on the left)
            GameObject activeIndicatorObj = new GameObject("ActiveIndicator");
            activeIndicatorObj.transform.SetParent(slotObj.transform, false);
            RectTransform activeRect = activeIndicatorObj.AddComponent<RectTransform>();
            activeRect.sizeDelta = new Vector2(4, 60);
            Image activeImg = activeIndicatorObj.AddComponent<Image>();
            activeImg.color = new Color(0.3f, 0.8f, 0.3f, 1f);
            activeIndicatorObj.SetActive(isActiveSlot && info?.hasData == true);

            // Text container
            GameObject textContainer = new GameObject("TextContainer");
            textContainer.transform.SetParent(slotObj.transform, false);
            RectTransform textRect = textContainer.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(350, 60);

            VerticalLayoutGroup textLayout = textContainer.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;

            // Slot name
            GameObject nameObj = new GameObject("SlotName");
            nameObj.transform.SetParent(textContainer.transform, false);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 18;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.text = info?.hasData == true ? info.GetDisplayName() : (info?.slotId == 0 ? "Autosave - Empty" : "Empty Slot");
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 24;

            // Date text
            GameObject dateObj = new GameObject("Date");
            dateObj.transform.SetParent(textContainer.transform, false);
            TextMeshProUGUI dateText = dateObj.AddComponent<TextMeshProUGUI>();
            dateText.fontSize = 14;
            dateText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            dateText.text = info?.hasData == true ? info.GetFormattedDate() : "No save data";
            LayoutElement dateLayout = dateObj.AddComponent<LayoutElement>();
            dateLayout.preferredHeight = 20;

            // Info container (play time, mission)
            GameObject infoContainer = new GameObject("InfoContainer");
            infoContainer.transform.SetParent(slotObj.transform, false);
            RectTransform infoRect = infoContainer.AddComponent<RectTransform>();
            infoRect.sizeDelta = new Vector2(150, 60);

            VerticalLayoutGroup infoLayout = infoContainer.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 2;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            // Play time
            GameObject playTimeObj = new GameObject("PlayTime");
            playTimeObj.transform.SetParent(infoContainer.transform, false);
            TextMeshProUGUI playTimeText = playTimeObj.AddComponent<TextMeshProUGUI>();
            playTimeText.fontSize = 14;
            playTimeText.color = new Color(0.8f, 0.8f, 0.6f, 1f);
            playTimeText.text = info?.hasData == true ? info.GetFormattedPlayTime() : "";
            LayoutElement playTimeLayout = playTimeObj.AddComponent<LayoutElement>();
            playTimeLayout.preferredHeight = 20;

            // Mission
            GameObject missionObj = new GameObject("Mission");
            missionObj.transform.SetParent(infoContainer.transform, false);
            TextMeshProUGUI missionText = missionObj.AddComponent<TextMeshProUGUI>();
            missionText.fontSize = 14;
            missionText.color = new Color(0.6f, 0.8f, 0.8f, 1f);
            missionText.text = info?.hasData == true && info.missionIndex >= 0 ? $"Mission {info.missionIndex + 1}" : "";
            LayoutElement missionLayout = missionObj.AddComponent<LayoutElement>();
            missionLayout.preferredHeight = 20;

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(slotObj.transform, false);
            RectTransform spacerRect = spacer.AddComponent<RectTransform>();
            LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1;

            // Delete button
            GameObject deleteObj = new GameObject("DeleteButton");
            deleteObj.transform.SetParent(slotObj.transform, false);
            RectTransform deleteRect = deleteObj.AddComponent<RectTransform>();
            deleteRect.sizeDelta = new Vector2(40, 40);
            Image deleteBg = deleteObj.AddComponent<Image>();
            deleteBg.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            Button deleteBtn = deleteObj.AddComponent<Button>();
            deleteBtn.targetGraphic = deleteBg;

            // Delete button text
            GameObject deleteTextObj = new GameObject("Text");
            deleteTextObj.transform.SetParent(deleteObj.transform, false);
            RectTransform deleteTextRect = deleteTextObj.AddComponent<RectTransform>();
            deleteTextRect.anchorMin = Vector2.zero;
            deleteTextRect.anchorMax = Vector2.one;
            deleteTextRect.offsetMin = Vector2.zero;
            deleteTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI deleteText = deleteTextObj.AddComponent<TextMeshProUGUI>();
            deleteText.text = "X";
            deleteText.fontSize = 20;
            deleteText.alignment = TextAlignmentOptions.Center;
            deleteText.color = Color.white;

            // Only show delete button for populated slots (except autosave if active)
            deleteObj.SetActive(info?.hasData == true && !(info.slotId == 0 && isActiveSlot));

            // Add SaveSlotUI component and wire up references
            SaveSlotUI slotUI = slotObj.AddComponent<SaveSlotUI>();
            slotUI.slotNameText = nameText;
            slotUI.dateText = dateText;
            slotUI.playTimeText = playTimeText;
            slotUI.missionText = missionText;
            slotUI.activeIndicator = activeImg;
            slotUI.slotButton = slotButton;
            slotUI.deleteButton = deleteBtn;
            slotUI.backgroundImage = bg;
            slotUI.slotInfo = info;

            return slotUI;
        }
    }
}
