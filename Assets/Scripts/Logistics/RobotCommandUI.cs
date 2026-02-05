using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.UI;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Logistics
{
    public class RobotCommandUI : MonoBehaviour
    {
        private GameObject mainPanel;
        private bool isOpen;

        private static readonly Color bgColor = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        private static readonly Color rowColor = new Color(0.1f, 0.13f, 0.18f, 1f);
        private static readonly Color accentColor = new Color(0.2f, 0.7f, 0.5f);

        private void Update()
        {
            if (!FirstRoomUpgradeStationUI.LogisticsAdvancedModule) return;
            if (UIState.IsAnyUIOpen && !isOpen) return;

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (isOpen) CloseUI();
                else OpenUI();
            }

            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UIState.ConsumeEscape();
                CloseUI();
            }
        }

        private void OpenUI()
        {
            isOpen = true;
            UIState.IsMachineUIOpen = true;
            BuildUI();
        }

        private void CloseUI()
        {
            isOpen = false;
            UIState.IsMachineUIOpen = false;
            if (mainPanel != null) Destroy(mainPanel);
            mainPanel = null;
        }

        private void BuildUI()
        {
            if (mainPanel != null) Destroy(mainPanel);

            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            mainPanel = new GameObject("RobotCommandPanel");
            mainPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600f, 350f);

            Image panelBg = mainPanel.AddComponent<Image>();
            panelBg.color = bgColor;

            Outline outline = mainPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.6f, 0.8f, 0.5f);
            outline.effectDistance = new Vector2(2f, 2f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(mainPanel.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "ROBOT COMMAND CENTER";
            titleText.fontSize = 20f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = accentColor;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = new Vector2(0f, -5f);

            // Content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(mainPanel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(15f, 30f);
            contentRect.offsetMax = new Vector2(-15f, -50f);

            VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            // Find all logistics robots
            var robots = FindObjectsOfType<LogisticsRobotController>();
            if (robots.Length == 0)
            {
                GameObject emptyObj = new GameObject("Empty");
                emptyObj.transform.SetParent(contentObj.transform, false);
                emptyObj.AddComponent<LayoutElement>().minHeight = 30f;
                TextMeshProUGUI emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
                emptyText.text = "No logistics robots found";
                emptyText.fontSize = 14f;
                emptyText.color = Color.gray;
                emptyText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                foreach (var robot in robots)
                {
                    CreateRobotRow(contentObj.transform, robot);
                }
            }

            // Close hint
            GameObject closeObj = new GameObject("CloseHint");
            closeObj.transform.SetParent(mainPanel.transform, false);
            TextMeshProUGUI closeText = closeObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "Press B or ESC to close";
            closeText.fontSize = 12f;
            closeText.color = new Color(0.5f, 0.5f, 0.5f);
            closeText.alignment = TextAlignmentOptions.Center;
            RectTransform closeRect = closeObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0f, 0f);
            closeRect.anchorMax = new Vector2(1f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(0f, 20f);
            closeRect.anchoredPosition = new Vector2(0f, 5f);
        }

        private void CreateRobotRow(Transform parent, LogisticsRobotController robot)
        {
            GameObject row = new GameObject($"Robot_{robot.name}");
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().minHeight = 55f;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = rowColor;

            // Info section (left)
            GameObject infoObj = new GameObject("Info");
            infoObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI infoText = infoObj.AddComponent<TextMeshProUGUI>();
            var cargoBuf = robot.GetCargo();
            string cargoStr = cargoBuf != null ? cargoBuf.GetCargoSummary() : "N/A";
            infoText.text = $"<b>{robot.name}</b>\n" +
                            $"State: {robot.GetStateName()} | Battery: {Mathf.RoundToInt(robot.BatteryRatio * 100)}%\n" +
                            $"Cargo: {cargoStr}";
            infoText.fontSize = 11f;
            infoText.color = Color.white;
            infoText.alignment = TextAlignmentOptions.Left;
            RectTransform infoRect = infoObj.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 0f);
            infoRect.anchorMax = new Vector2(0.55f, 1f);
            infoRect.offsetMin = new Vector2(8f, 2f);
            infoRect.offsetMax = new Vector2(0f, -2f);

            // Buttons section (right)
            float btnW = 70f, btnH = 22f, spacing = 4f;
            float startX = 0f;

            CreateSmallButton(row.transform, "Help Me", new Vector2(startX, 28f), btnW, btnH, () =>
            {
                robot.SetMode(LogisticsRobotController.RobotMode.FollowPlayer);
                BuildUI();
            });
            CreateSmallButton(row.transform, "Digger", new Vector2(startX + btnW + spacing, 28f), btnW, btnH, () =>
            {
                robot.SetMode(LogisticsRobotController.RobotMode.WorkWithDigger);
                BuildUI();
            });
            CreateSmallButton(row.transform, "Idle", new Vector2(startX + (btnW + spacing) * 2, 28f), btnW - 10f, btnH, () =>
            {
                robot.SetMode(LogisticsRobotController.RobotMode.Idle);
                BuildUI();
            });
            CreateSmallButton(row.transform, "Unload", new Vector2(startX, 2f), btnW, btnH, () =>
            {
                robot.ForceUnload();
                BuildUI();
            });
        }

        private void CreateSmallButton(Transform parent, string label, Vector2 pos, float w, float h, System.Action onClick)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.55f, 0f);
            btnRect.anchorMax = new Vector2(0.55f, 0f);
            btnRect.pivot = new Vector2(0f, 0f);
            btnRect.sizeDelta = new Vector2(w, h);
            btnRect.anchoredPosition = pos;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.18f, 0.35f, 0.5f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 11f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private Canvas FindCanvas()
        {
            var existing = GameObject.Find("MachineUICanvas");
            if (existing != null)
            {
                var c = existing.GetComponent<Canvas>();
                if (c != null) return c;
            }
            string[] names = { "HUDCanvas", "GameCanvas", "MainCanvas" };
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
