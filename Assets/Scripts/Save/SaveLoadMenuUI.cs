using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// UI manager for the save/load menu screen.
    /// Supports both Save mode (with name input) and Load mode.
    /// </summary>
    public class SaveLoadMenuUI : MonoBehaviour
    {
        public static SaveLoadMenuUI Instance { get; private set; }

        public enum MenuMode { Save, Load }

        // Runtime created references
        private GameObject menuPanel;
        private Transform slotsContainer;
        private TextMeshProUGUI titleText;
        private GameObject nameInputPanel;
        private TMP_InputField nameInputField;
        private TextMeshProUGUI nameHintText;
        private Button confirmButton;
        private TextMeshProUGUI confirmButtonText;
        private Button cancelButton;
        private Button newSaveButton;
        private Button deleteButton;
        private GameObject deleteConfirmPanel;
        private TextMeshProUGUI deleteConfirmText;
        private Button deleteYesButton;
        private Button deleteNoButton;

        private MenuMode currentMode;
        private int selectedSlotId = -1;
        private int pendingDeleteSlotId = -1;
        private List<GameObject> slotObjects = new List<GameObject>();
        private bool isOpen;

        /// <summary>
        /// Event fired when menu is closed.
        /// </summary>
        public System.Action OnMenuClosed;

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
        }

        private void Update()
        {
            if (!isOpen) return;

            // ESC to close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (deleteConfirmPanel != null && deleteConfirmPanel.activeSelf)
                {
                    OnDeleteCancelled();
                }
                else
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// Open the menu in Save mode.
        /// </summary>
        public void OpenSaveMenu()
        {
            Open(MenuMode.Save);
        }

        /// <summary>
        /// Open the menu in Load mode.
        /// </summary>
        public void OpenLoadMenu()
        {
            Open(MenuMode.Load);
        }

        /// <summary>
        /// Open the menu in the specified mode.
        /// </summary>
        public void Open(MenuMode mode)
        {
            // Create UI if needed
            if (menuPanel == null)
            {
                CreateUI();
            }

            currentMode = mode;
            selectedSlotId = -1;
            isOpen = true;

            // Update title
            if (titleText != null)
            {
                titleText.text = mode == MenuMode.Save ? "SAVE GAME" : "LOAD GAME";
            }

            // Update confirm button
            if (confirmButtonText != null)
            {
                confirmButtonText.text = mode == MenuMode.Save ? "SAVE" : "LOAD";
            }

            // Show/hide name input (only in save mode)
            if (nameInputPanel != null)
            {
                nameInputPanel.SetActive(mode == MenuMode.Save);
            }
            if (nameInputField != null)
            {
                nameInputField.text = "";
            }
            if (nameHintText != null)
            {
                nameHintText.gameObject.SetActive(mode == MenuMode.Save);
            }

            // Show/hide new save button (only in save mode)
            if (newSaveButton != null)
            {
                newSaveButton.gameObject.SetActive(mode == MenuMode.Save);
            }

            // Refresh slot list
            RefreshSlots();

            // Show panel
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
            }

            // Disable confirm until a slot is selected
            UpdateConfirmButton();
        }

        /// <summary>
        /// Close the menu.
        /// </summary>
        public void Close()
        {
            Debug.Log("[SaveLoadMenuUI] Close() called");
            isOpen = false;

            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }

            if (deleteConfirmPanel != null)
            {
                deleteConfirmPanel.SetActive(false);
            }

            OnMenuClosed?.Invoke();
        }

        /// <summary>
        /// Check if menu is currently open.
        /// </summary>
        public bool IsOpen => isOpen;

        /// <summary>
        /// Refresh the slot list from SaveManager.
        /// </summary>
        private void RefreshSlots()
        {
            // Clear existing slots
            foreach (var slotObj in slotObjects)
            {
                if (slotObj != null)
                    Destroy(slotObj);
            }
            slotObjects.Clear();

            if (SaveManager.Instance == null)
            {
                Debug.LogWarning("[SaveLoadMenuUI] SaveManager.Instance is null");
                return;
            }

            var slots = SaveManager.Instance.GetAllSlots();
            int activeSlotId = SaveManager.Instance.ActiveSlotId;

            foreach (var slotInfo in slots)
            {
                bool isActiveSlot = slotInfo.slotId == activeSlotId;
                GameObject slotObj = CreateSlotUI(slotInfo, isActiveSlot);
                slotObjects.Add(slotObj);
            }
        }

        /// <summary>
        /// Create UI for a single slot.
        /// </summary>
        private GameObject CreateSlotUI(SaveSlotInfo info, bool isActiveSlot)
        {
            GameObject slotObj = new GameObject($"Slot_{info.slotId}");
            slotObj.transform.SetParent(slotsContainer, false);

            // Background - height 65 for 6 slots to fit nicely
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(0, 65);

            Image slotBg = slotObj.AddComponent<Image>();
            if (info.hasData)
            {
                slotBg.color = isActiveSlot ? new Color(0.15f, 0.25f, 0.15f, 0.95f) : new Color(0.18f, 0.18f, 0.22f, 0.95f);
            }
            else
            {
                slotBg.color = new Color(0.12f, 0.12f, 0.15f, 0.8f);
            }

            // Make clickable
            Button slotButton = slotObj.AddComponent<Button>();
            slotButton.targetGraphic = slotBg;
            int capturedSlotId = info.slotId;
            slotButton.onClick.AddListener(() => OnSlotClicked(capturedSlotId));

            // Layout
            HorizontalLayoutGroup layout = slotObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 8, 8);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Active indicator
            if (isActiveSlot && info.hasData)
            {
                GameObject indicator = new GameObject("ActiveIndicator");
                indicator.transform.SetParent(slotObj.transform, false);
                RectTransform indRect = indicator.AddComponent<RectTransform>();
                indRect.sizeDelta = new Vector2(4, 50);
                Image indImg = indicator.AddComponent<Image>();
                indImg.color = new Color(0.3f, 0.8f, 0.3f, 1f);
            }

            // Text container
            GameObject textContainer = new GameObject("TextContainer");
            textContainer.transform.SetParent(slotObj.transform, false);
            RectTransform textRect = textContainer.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(300, 50);
            VerticalLayoutGroup textLayout = textContainer.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;

            // Slot name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(textContainer.transform, false);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 18;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;

            string displayName = info.hasData ? info.GetDisplayName() : (info.slotId == 0 ? "Autosave - Empty" : $"Slot {info.slotId} - Empty");
            if (isActiveSlot && info.hasData)
            {
                displayName += " (Active)";
            }
            nameText.text = displayName;

            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 24;

            // Date/info text
            GameObject dateObj = new GameObject("Date");
            dateObj.transform.SetParent(textContainer.transform, false);
            TextMeshProUGUI dateText = dateObj.AddComponent<TextMeshProUGUI>();
            dateText.fontSize = 14;
            dateText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            dateText.text = info.hasData ? info.GetFormattedDate() : "No save data";
            LayoutElement dateLayout = dateObj.AddComponent<LayoutElement>();
            dateLayout.preferredHeight = 18;

            // Info container (play time, mission)
            if (info.hasData)
            {
                GameObject infoContainer = new GameObject("Info");
                infoContainer.transform.SetParent(slotObj.transform, false);
                RectTransform infoRect = infoContainer.AddComponent<RectTransform>();
                infoRect.sizeDelta = new Vector2(120, 50);
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
                playTimeText.color = new Color(0.8f, 0.7f, 0.4f, 1f);
                playTimeText.text = info.GetFormattedPlayTime();
                playTimeText.alignment = TextAlignmentOptions.Right;
                LayoutElement playLayout = playTimeObj.AddComponent<LayoutElement>();
                playLayout.preferredHeight = 18;

                // Mission
                if (info.missionIndex >= 0)
                {
                    GameObject missionObj = new GameObject("Mission");
                    missionObj.transform.SetParent(infoContainer.transform, false);
                    TextMeshProUGUI missionText = missionObj.AddComponent<TextMeshProUGUI>();
                    missionText.fontSize = 14;
                    missionText.color = new Color(0.5f, 0.7f, 0.8f, 1f);
                    missionText.text = $"Mission {info.missionIndex + 1}";
                    missionText.alignment = TextAlignmentOptions.Right;
                    LayoutElement missionLayout = missionObj.AddComponent<LayoutElement>();
                    missionLayout.preferredHeight = 18;
                }
            }

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(slotObj.transform, false);
            spacer.AddComponent<RectTransform>();
            LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1;

            // Individual delete buttons removed - using bottom Delete button instead

            return slotObj;
        }

        /// <summary>
        /// Handle slot click.
        /// </summary>
        private void OnSlotClicked(int slotId)
        {
            Debug.Log($"[SaveLoadMenuUI] Slot {slotId} clicked");

            // In load mode, can only select slots with data
            if (currentMode == MenuMode.Load)
            {
                var slotInfo = SaveManager.Instance.GetSlotInfo(slotId);
                if (slotInfo == null || !slotInfo.hasData)
                {
                    Debug.Log($"[SaveLoadMenuUI] Slot {slotId} has no data, ignoring");
                    return;
                }
            }

            selectedSlotId = slotId;
            UpdateSlotVisuals();
            UpdateConfirmButton();
        }

        /// <summary>
        /// Update visual state of all slots based on selection.
        /// </summary>
        private void UpdateSlotVisuals()
        {
            var slots = SaveManager.Instance?.GetAllSlots();
            if (slots == null) return;

            int activeSlotId = SaveManager.Instance.ActiveSlotId;

            for (int i = 0; i < slotObjects.Count && i < slots.Count; i++)
            {
                var slotObj = slotObjects[i];
                if (slotObj == null) continue;

                var slotInfo = slots[i];
                Image bg = slotObj.GetComponent<Image>();
                if (bg != null)
                {
                    if (slotInfo.slotId == selectedSlotId)
                    {
                        bg.color = new Color(0.25f, 0.35f, 0.45f, 0.95f); // Selected
                    }
                    else if (slotInfo.hasData)
                    {
                        bg.color = (slotInfo.slotId == activeSlotId) ?
                            new Color(0.15f, 0.25f, 0.15f, 0.95f) : // Active
                            new Color(0.18f, 0.18f, 0.22f, 0.95f);   // Normal
                    }
                    else
                    {
                        bg.color = new Color(0.12f, 0.12f, 0.15f, 0.8f); // Empty
                    }
                }
            }
        }

        /// <summary>
        /// Handle delete button click.
        /// </summary>
        private void OnDeleteClicked(int slotId)
        {
            Debug.Log($"[SaveLoadMenuUI] Delete clicked for slot {slotId}");
            pendingDeleteSlotId = slotId;
            var slotInfo = SaveManager.Instance.GetSlotInfo(slotId);

            if (deleteConfirmText != null)
            {
                string saveName = slotInfo?.GetDisplayName() ?? $"Slot {slotId}";
                deleteConfirmText.text = $"Delete \"{saveName}\"?\n\nThis cannot be undone.";
            }

            if (deleteConfirmPanel != null)
            {
                deleteConfirmPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Handle delete confirmation.
        /// </summary>
        private void OnDeleteConfirmed()
        {
            Debug.Log($"[SaveLoadMenuUI] Delete confirmed for slot {pendingDeleteSlotId}");

            if (pendingDeleteSlotId >= 0)
            {
                SaveManager.Instance.DeleteSaveSlot(pendingDeleteSlotId);

                if (selectedSlotId == pendingDeleteSlotId)
                {
                    selectedSlotId = -1;
                }

                RefreshSlots();
                UpdateConfirmButton();
            }

            pendingDeleteSlotId = -1;
            if (deleteConfirmPanel != null)
            {
                deleteConfirmPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Handle delete cancellation.
        /// </summary>
        private void OnDeleteCancelled()
        {
            pendingDeleteSlotId = -1;
            if (deleteConfirmPanel != null)
            {
                deleteConfirmPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Handle new save button click.
        /// </summary>
        private void OnNewSaveClicked()
        {
            Debug.Log("[SaveLoadMenuUI] New Save clicked");

            int emptySlot = SaveManager.Instance.SavesIndex.FindFirstEmptyManualSlot();

            if (emptySlot < 0)
            {
                Debug.LogWarning("[SaveLoadMenuUI] No empty save slots available");
                if (nameHintText != null)
                {
                    nameHintText.text = "No empty slots! Delete a save first.";
                    nameHintText.color = new Color(0.9f, 0.4f, 0.4f, 1f);
                }
                return;
            }

            OnSlotClicked(emptySlot);
        }

        /// <summary>
        /// Handle confirm button click.
        /// </summary>
        private void OnConfirmClicked()
        {
            Debug.Log($"[SaveLoadMenuUI] Confirm clicked, selectedSlot={selectedSlotId}, mode={currentMode}");

            if (selectedSlotId < 0) return;

            if (currentMode == MenuMode.Save)
            {
                PerformSave();
            }
            else
            {
                PerformLoad();
            }
        }

        /// <summary>
        /// Perform save to selected slot.
        /// </summary>
        private void PerformSave()
        {
            string saveName = null;
            if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            {
                saveName = nameInputField.text.Trim();
            }

            Debug.Log($"[SaveLoadMenuUI] Saving to slot {selectedSlotId} with name: {saveName ?? "(auto)"}");

            bool success = SaveManager.Instance.SaveGameToSlot(selectedSlotId, saveName);

            if (success)
            {
                Debug.Log($"[SaveLoadMenuUI] Save successful");
                Close();
            }
            else
            {
                Debug.LogError($"[SaveLoadMenuUI] Save failed");
                if (nameHintText != null)
                {
                    nameHintText.text = "Save failed! Please try again.";
                    nameHintText.color = new Color(0.9f, 0.4f, 0.4f, 1f);
                }
            }
        }

        /// <summary>
        /// Perform load from selected slot.
        /// </summary>
        private void PerformLoad()
        {
            Debug.Log($"[SaveLoadMenuUI] Loading from slot {selectedSlotId}");

            if (SaveManager.Instance == null)
            {
                Debug.LogError("[SaveLoadMenuUI] SaveManager.Instance is null!");
                return;
            }

            var slotInfo = SaveManager.Instance.GetSlotInfo(selectedSlotId);
            if (slotInfo == null || !slotInfo.hasData)
            {
                Debug.LogError($"[SaveLoadMenuUI] Slot {selectedSlotId} has no data!");
                return;
            }

            Debug.Log($"[SaveLoadMenuUI] Loading slot {selectedSlotId}: {slotInfo.GetDisplayName()}");

            // Close the menu BEFORE loading so scene transition is clean
            isOpen = false;
            if (menuPanel != null)
                menuPanel.SetActive(false);

            // Now load the game - this will trigger scene transition
            bool success = SaveManager.Instance.LoadGameFromSlot(selectedSlotId);

            if (!success)
            {
                Debug.LogError($"[SaveLoadMenuUI] Load failed for slot {selectedSlotId}");
                // Reopen menu on failure
                isOpen = true;
                if (menuPanel != null)
                    menuPanel.SetActive(true);
            }
            else
            {
                Debug.Log($"[SaveLoadMenuUI] Load initiated successfully, scene should be loading...");
                OnMenuClosed?.Invoke();
            }
        }

        /// <summary>
        /// Update confirm button and delete button state.
        /// </summary>
        private void UpdateConfirmButton()
        {
            bool canConfirm = false;
            bool canDelete = false;

            if (selectedSlotId >= 0)
            {
                var slotInfo = SaveManager.Instance?.GetSlotInfo(selectedSlotId);
                bool slotHasData = slotInfo != null && slotInfo.hasData;

                if (currentMode == MenuMode.Save)
                {
                    canConfirm = true;
                    canDelete = slotHasData; // Can delete if slot has data
                }
                else
                {
                    canConfirm = slotHasData;
                    canDelete = slotHasData;
                }
            }

            // Update confirm button
            if (confirmButton != null)
            {
                confirmButton.interactable = canConfirm;
                Image btnBg = confirmButton.GetComponent<Image>();
                if (btnBg != null)
                {
                    btnBg.color = canConfirm ?
                        (currentMode == MenuMode.Save ? new Color(0.2f, 0.5f, 0.2f, 1f) : new Color(0.2f, 0.35f, 0.5f, 1f)) :
                        new Color(0.25f, 0.25f, 0.25f, 0.5f);
                }
            }

            // Update delete button
            if (deleteButton != null)
            {
                deleteButton.interactable = canDelete;
                Image deleteBg = deleteButton.GetComponent<Image>();
                if (deleteBg != null)
                {
                    deleteBg.color = canDelete ?
                        new Color(0.6f, 0.2f, 0.2f, 1f) :
                        new Color(0.25f, 0.25f, 0.25f, 0.5f);
                }
            }
        }

        /// <summary>
        /// Create the entire UI programmatically.
        /// </summary>
        private void CreateUI()
        {
            // Ensure EventSystem exists (required for UI interaction)
            EnsureEventSystemExists();

            // Main panel (full screen dark overlay)
            menuPanel = new GameObject("SaveLoadMenuPanel");
            menuPanel.transform.SetParent(transform, false);

            Canvas canvas = menuPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;

            CanvasScaler scaler = menuPanel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            menuPanel.AddComponent<GraphicRaycaster>();

            // Dark background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(menuPanel.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);

            // Content container (centered) - sized to fit all 6 slots without scrolling
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(menuPanel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(600, 720);
            contentRect.anchoredPosition = Vector2.zero;

            Image contentBg = contentObj.AddComponent<Image>();
            contentBg.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(25, 25, 25, 25);
            contentLayout.spacing = 12;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(contentObj.transform, false);
            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "SAVE GAME";
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 45;

            // Slots container (no scroll - fits all 6 slots)
            GameObject slotsObj = new GameObject("SlotsContainer");
            slotsObj.transform.SetParent(contentObj.transform, false);
            RectTransform slotsRect = slotsObj.AddComponent<RectTransform>();

            VerticalLayoutGroup slotsLayout = slotsObj.AddComponent<VerticalLayoutGroup>();
            slotsLayout.padding = new RectOffset(0, 0, 5, 5);
            slotsLayout.spacing = 8;
            slotsLayout.childControlWidth = true;
            slotsLayout.childControlHeight = false;
            slotsLayout.childForceExpandWidth = true;
            slotsLayout.childForceExpandHeight = false;

            // Height for 6 slots: 6 * 70 + 5 * 8 (spacing) + 10 (padding) = 470
            LayoutElement slotsLayoutElem = slotsObj.AddComponent<LayoutElement>();
            slotsLayoutElem.preferredHeight = 480;

            slotsContainer = slotsRect;

            // Name input panel (for save mode)
            nameInputPanel = new GameObject("NameInputPanel");
            nameInputPanel.transform.SetParent(contentObj.transform, false);
            RectTransform nameRect = nameInputPanel.AddComponent<RectTransform>();
            LayoutElement nameLayoutElem = nameInputPanel.AddComponent<LayoutElement>();
            nameLayoutElem.preferredHeight = 40;

            HorizontalLayoutGroup nameLayout = nameInputPanel.AddComponent<HorizontalLayoutGroup>();
            nameLayout.spacing = 10;
            nameLayout.childControlWidth = true;
            nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = false;
            nameLayout.childForceExpandHeight = false;

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(nameInputPanel.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Save Name:";
            labelText.fontSize = 16;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 100;

            // Input field
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(nameInputPanel.transform, false);
            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            nameInputField = inputObj.AddComponent<TMP_InputField>();
            LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1;
            inputLayout.preferredHeight = 35;

            // Text area
            GameObject textAreaObj = new GameObject("TextArea");
            textAreaObj.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRect = textAreaObj.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            // Input text
            GameObject inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(textAreaObj.transform, false);
            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 16;
            inputText.color = Color.white;

            nameInputField.textViewport = textAreaRect;
            nameInputField.textComponent = inputText;

            // Hint text
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(contentObj.transform, false);
            nameHintText = hintObj.AddComponent<TextMeshProUGUI>();
            nameHintText.text = "(Leave empty to use date/time)";
            nameHintText.fontSize = 12;
            nameHintText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            nameHintText.alignment = TextAlignmentOptions.Center;
            LayoutElement hintLayout = hintObj.AddComponent<LayoutElement>();
            hintLayout.preferredHeight = 20;

            // Buttons container - ABSOLUTE POSITION at bottom of content panel
            GameObject buttonsObj = new GameObject("Buttons");
            buttonsObj.transform.SetParent(contentObj.transform, false);
            RectTransform buttonsRect = buttonsObj.AddComponent<RectTransform>();
            // Anchor to bottom center
            buttonsRect.anchorMin = new Vector2(0.5f, 0f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0f);
            buttonsRect.pivot = new Vector2(0.5f, 0f);
            buttonsRect.anchoredPosition = new Vector2(0, 25); // 25px from bottom
            buttonsRect.sizeDelta = new Vector2(500, 50); // Wide enough for 4 buttons

            HorizontalLayoutGroup buttonsLayout = buttonsObj.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 15;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlWidth = false;
            buttonsLayout.childControlHeight = false;
            buttonsLayout.childForceExpandWidth = false;
            buttonsLayout.childForceExpandHeight = false;

            // Ignore layout so it doesn't participate in VerticalLayoutGroup
            LayoutElement buttonsLayoutElem = buttonsObj.AddComponent<LayoutElement>();
            buttonsLayoutElem.ignoreLayout = true;

            // New Save button
            newSaveButton = CreateButton(buttonsObj.transform, "New Save", new Color(0.2f, 0.4f, 0.3f, 1f), 110);
            newSaveButton.onClick.AddListener(OnNewSaveClicked);

            // Confirm button
            confirmButton = CreateButton(buttonsObj.transform, "SAVE", new Color(0.2f, 0.5f, 0.2f, 1f), 110);
            confirmButtonText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            confirmButton.onClick.AddListener(() => {
                Debug.Log("[SaveLoadMenuUI] Confirm/Load button CLICKED!");
                OnConfirmClicked();
            });
            Debug.Log($"[SaveLoadMenuUI] Confirm button created and listener added");

            // Cancel button
            cancelButton = CreateButton(buttonsObj.transform, "Cancel", new Color(0.3f, 0.3f, 0.35f, 1f), 100);
            cancelButton.onClick.AddListener(() => {
                Debug.Log("[SaveLoadMenuUI] Cancel button CLICKED!");
                Close();
            });
            Debug.Log($"[SaveLoadMenuUI] Cancel button created and listener added");

            // Delete button (disabled by default, enables when save is selected)
            deleteButton = CreateButton(buttonsObj.transform, "Delete", new Color(0.5f, 0.2f, 0.2f, 1f), 100);
            deleteButton.onClick.AddListener(() => {
                Debug.Log("[SaveLoadMenuUI] Delete button CLICKED!");
                if (selectedSlotId >= 0)
                {
                    var slotInfo = SaveManager.Instance?.GetSlotInfo(selectedSlotId);
                    if (slotInfo != null && slotInfo.hasData)
                    {
                        OnDeleteClicked(selectedSlotId);
                    }
                }
            });
            deleteButton.interactable = false; // Disabled by default
            Debug.Log($"[SaveLoadMenuUI] Delete button created");

            // Delete confirmation panel
            CreateDeleteConfirmPanel();

            menuPanel.SetActive(false);
        }

        private Button CreateButton(Transform parent, string text, Color bgColor, float width)
        {
            GameObject btnObj = new GameObject($"{text}Button");
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(width, 40);
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = bgColor;
            btnBg.raycastTarget = true;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 16;
            btnText.fontStyle = FontStyles.Bold;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.raycastTarget = false; // Let clicks pass through to parent button

            return btn;
        }

        private void CreateDeleteConfirmPanel()
        {
            deleteConfirmPanel = new GameObject("DeleteConfirmPanel");
            deleteConfirmPanel.transform.SetParent(menuPanel.transform, false);
            RectTransform panelRect = deleteConfirmPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelBg = deleteConfirmPanel.AddComponent<Image>();
            panelBg.color = new Color(0, 0, 0, 0.85f);

            // Dialog box
            GameObject dialogObj = new GameObject("Dialog");
            dialogObj.transform.SetParent(deleteConfirmPanel.transform, false);
            RectTransform dialogRect = dialogObj.AddComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(350, 180);
            dialogRect.anchoredPosition = Vector2.zero;
            Image dialogBg = dialogObj.AddComponent<Image>();
            dialogBg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

            VerticalLayoutGroup dialogLayout = dialogObj.AddComponent<VerticalLayoutGroup>();
            dialogLayout.padding = new RectOffset(20, 20, 20, 20);
            dialogLayout.spacing = 20;
            dialogLayout.childControlWidth = true;
            dialogLayout.childControlHeight = false;
            dialogLayout.childForceExpandWidth = true;
            dialogLayout.childForceExpandHeight = false;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(dialogObj.transform, false);
            deleteConfirmText = textObj.AddComponent<TextMeshProUGUI>();
            deleteConfirmText.text = "Delete this save?\n\nThis cannot be undone.";
            deleteConfirmText.fontSize = 16;
            deleteConfirmText.alignment = TextAlignmentOptions.Center;
            deleteConfirmText.color = Color.white;
            LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.preferredHeight = 70;

            // Buttons
            GameObject buttonsObj = new GameObject("Buttons");
            buttonsObj.transform.SetParent(dialogObj.transform, false);
            RectTransform buttonsRect = buttonsObj.AddComponent<RectTransform>();
            buttonsRect.sizeDelta = new Vector2(0, 40);
            HorizontalLayoutGroup buttonsLayout = buttonsObj.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 20;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlWidth = false;
            buttonsLayout.childControlHeight = false;
            buttonsLayout.childForceExpandWidth = false;
            buttonsLayout.childForceExpandHeight = false;
            LayoutElement buttonsLayoutElem = buttonsObj.AddComponent<LayoutElement>();
            buttonsLayoutElem.preferredHeight = 40;
            buttonsLayoutElem.minHeight = 40;

            deleteYesButton = CreateButton(buttonsObj.transform, "Delete", new Color(0.6f, 0.2f, 0.2f, 1f), 90);
            deleteYesButton.onClick.AddListener(OnDeleteConfirmed);

            deleteNoButton = CreateButton(buttonsObj.transform, "Cancel", new Color(0.3f, 0.3f, 0.35f, 1f), 90);
            deleteNoButton.onClick.AddListener(OnDeleteCancelled);

            deleteConfirmPanel.SetActive(false);
        }

        /// <summary>
        /// Ensure an EventSystem exists in the scene (required for UI interaction).
        /// </summary>
        private void EnsureEventSystemExists()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                Debug.Log("[SaveLoadMenuUI] Creating EventSystem (none found in scene)");
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>
        /// Create the SaveLoadMenuUI component.
        /// </summary>
        public static SaveLoadMenuUI Create(Transform parent = null)
        {
            GameObject menuObj = new GameObject("SaveLoadMenuUI");
            if (parent != null)
            {
                menuObj.transform.SetParent(parent, false);
            }

            SaveLoadMenuUI menu = menuObj.AddComponent<SaveLoadMenuUI>();
            return menu;
        }
    }
}
