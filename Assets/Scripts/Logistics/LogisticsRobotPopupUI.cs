using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.UI;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Logistics
{
    /// <summary>
    /// Small contextual popup that opens when the player presses E on the logistics robot.
    /// Shows current mode, mode switch buttons, cargo info, and send-to-unload.
    /// </summary>
    public class LogisticsRobotPopupUI : MonoBehaviour
    {
        public static LogisticsRobotPopupUI Instance { get; private set; }

        private GameObject mainPanel;
        private LogisticsRobotController currentRobot;
        private bool isOpen;

        private static readonly Color bgColor = new Color(0.07f, 0.1f, 0.12f, 0.95f);
        private static readonly Color activeBtnColor = new Color(0.15f, 0.55f, 0.4f, 1f);
        private static readonly Color inactiveBtnColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        private static readonly Color accentColor = new Color(0.25f, 0.85f, 0.6f);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UIState.ConsumeEscape();
                CloseUI();
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                CloseUI();
            }
        }

        public void ShowUI(LogisticsRobotController robot)
        {
            currentRobot = robot;
            isOpen = true;
            UIState.IsMachineUIOpen = true;
            BuildUI();
        }

        public void CloseUI()
        {
            isOpen = false;
            UIState.IsMachineUIOpen = false;
            if (mainPanel != null) Destroy(mainPanel);
            mainPanel = null;
            currentRobot = null;
        }

        private void BuildUI()
        {
            if (mainPanel != null) Destroy(mainPanel);

            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            // Main panel - compact popup
            mainPanel = new GameObject("LogisticsRobotPopup");
            mainPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(440f, 400f);

            Image panelBg = mainPanel.AddComponent<Image>();
            panelBg.color = bgColor;

            Outline outline = mainPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.75f, 0.5f, 0.6f);
            outline.effectDistance = new Vector2(2f, 2f);

            // X close button (top-right)
            GameObject closeBtnObj = new GameObject("CloseBtn");
            closeBtnObj.transform.SetParent(mainPanel.transform, false);
            RectTransform closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1f, 1f);
            closeBtnRect.anchorMax = new Vector2(1f, 1f);
            closeBtnRect.pivot = new Vector2(1f, 1f);
            closeBtnRect.sizeDelta = new Vector2(32f, 32f);
            closeBtnRect.anchoredPosition = new Vector2(-4f, -4f);
            Image closeBtnBg = closeBtnObj.AddComponent<Image>();
            closeBtnBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnBg;
            closeBtn.onClick.AddListener(() => CloseUI());
            GameObject closeTextObj = new GameObject("X");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            TextMeshProUGUI closeTxt = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeTxt.text = "X";
            closeTxt.fontSize = 16f;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;
            RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            // Title
            CreateText(mainPanel.transform, "LOGISTICS ROBOT", 20f, FontStyles.Bold, accentColor,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(0f, 36f));

            // ---- Mode Section ----
            CreateText(mainPanel.transform, "TASK MODE", 14f, FontStyles.Normal, new Color(0.6f, 0.6f, 0.6f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(15f, -48f), new Vector2(200f, 22f));

            var mode = currentRobot != null ? currentRobot.ActiveMode : LogisticsRobotController.RobotMode.Idle;

            // Mode buttons row
            float btnY = -72f;
            float btnW = 120f;
            float btnH = 36f;
            float btnSpacing = 8f;
            float startX = -btnW - btnSpacing;

            CreateModeButton(mainPanel.transform, "Help Me", LogisticsRobotController.RobotMode.FollowPlayer,
                mode, new Vector2(startX, btnY), btnW, btnH);
            CreateModeButton(mainPanel.transform, "With Digger", LogisticsRobotController.RobotMode.WorkWithDigger,
                mode, new Vector2(0f, btnY), btnW, btnH);
            CreateModeButton(mainPanel.transform, "Idle", LogisticsRobotController.RobotMode.Idle,
                mode, new Vector2(btnW + btnSpacing, btnY), btnW, btnH);

            // ---- Status Section ----
            float statusY = -125f;
            string stateStr = currentRobot != null ? currentRobot.GetStateName() : "N/A";
            float battPct = currentRobot != null ? currentRobot.BatteryRatio * 100f : 0f;
            CreateText(mainPanel.transform, $"State: {stateStr}  |  Battery: {Mathf.RoundToInt(battPct)}%",
                15f, FontStyles.Normal, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, statusY), new Vector2(0f, 24f));

            // ---- Cargo Section ----
            float cargoY = -160f;
            CreateText(mainPanel.transform, "CARGO", 14f, FontStyles.Normal, new Color(0.6f, 0.6f, 0.6f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(15f, cargoY), new Vector2(200f, 22f));

            string cargoStr = "Empty";
            if (currentRobot != null)
            {
                var cargoBuf = currentRobot.GetCargo();
                if (cargoBuf != null)
                {
                    cargoStr = cargoBuf.GetCargoSummary();
                    int maxTypes = cargoBuf.MaxDistinctTypes;
                    var allCargo = cargoBuf.GetAllCargo();
                    cargoStr += $"\nSlots: {allCargo.Count}/{maxTypes}";
                }
            }

            CreateText(mainPanel.transform, cargoStr, 14f, FontStyles.Normal, new Color(0.9f, 0.9f, 0.8f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(15f, cargoY - 24f), new Vector2(400f, 50f));

            // ---- Action Buttons ----
            float actionY = -275f;
            float actionY2 = -315f;

            CreateActionButton(mainPanel.transform, "Send to Unload", new Vector2(-70f, actionY), 150f, 34f, () =>
            {
                if (currentRobot != null) currentRobot.ForceUnload();
                CloseUI();
            });

            // Advanced module buttons
            if (FirstRoomUpgradeStationUI.LogisticsAdvancedModule)
            {
                CreateActionButton(mainPanel.transform, "Fetch E-Drink", new Vector2(88f, actionY), 130f, 34f, () =>
                {
                    var vendor = new TradeTerminalVendorAdapter();
                    vendor.TryBuyEnergyDrink();
                    CloseUI();
                });
            }

            // Pick Up button - always available for when robot gets stuck
            CreateActionButton(mainPanel.transform, "Pick Up Robot", new Vector2(0f, actionY2), 150f, 34f, () =>
            {
                if (currentRobot != null)
                {
                    // Get or add carry component on player
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player == null) player = Camera.main?.transform.root.gameObject;
                    if (player != null)
                    {
                        var carry = player.GetComponent<LogisticsRobotCarry>();
                        if (carry == null) carry = player.AddComponent<LogisticsRobotCarry>();
                        carry.PickUpRobot(currentRobot);
                    }
                }
                CloseUI();
            });

            // Close hint
            CreateText(mainPanel.transform, "Press E or ESC to close", 11f, FontStyles.Normal,
                new Color(0.45f, 0.45f, 0.45f), TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(0f, 18f));
        }

        private void CreateModeButton(Transform parent, string label, LogisticsRobotController.RobotMode btnMode,
            LogisticsRobotController.RobotMode currentMode, Vector2 pos, float w, float h)
        {
            bool isActive = btnMode == currentMode;

            GameObject btnObj = new GameObject($"ModeBtn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 1f);
            btnRect.anchorMax = new Vector2(0.5f, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.sizeDelta = new Vector2(w, h);
            btnRect.anchoredPosition = pos;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = isActive ? activeBtnColor : inactiveBtnColor;

            if (isActive)
            {
                Outline btnOutline = btnObj.AddComponent<Outline>();
                btnOutline.effectColor = accentColor;
                btnOutline.effectDistance = new Vector2(1.5f, 1.5f);
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() =>
            {
                if (currentRobot != null) currentRobot.SetMode(btnMode);
                BuildUI(); // Refresh to show new active state
            });

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 15f;
            text.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
            text.color = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void CreateActionButton(Transform parent, string label, Vector2 pos, float w, float h, System.Action onClick)
        {
            GameObject btnObj = new GameObject($"ActionBtn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 1f);
            btnRect.anchorMax = new Vector2(0.5f, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.sizeDelta = new Vector2(w, h);
            btnRect.anchoredPosition = pos;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.22f, 0.35f, 0.5f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, FontStyles style,
            Color color, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return tmp;
        }

        private Canvas FindCanvas()
        {
            string[] names = { "MachineUICanvas", "HUDCanvas", "GameCanvas", "MainCanvas" };
            foreach (var n in names)
            {
                var obj = GameObject.Find(n);
                if (obj != null)
                {
                    var c = obj.GetComponent<Canvas>();
                    if (c != null) return c;
                }
            }
            return null;
        }
    }
}
