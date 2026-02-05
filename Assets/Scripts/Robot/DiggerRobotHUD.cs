using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Robot
{
    /// <summary>
    /// Screen-space HUD panel showing the digger robot's name, state, and battery bar.
    /// Attach to the same GameObject as DiggerRobotStateMachine.
    /// Auto-creates its own UI elements on the existing HUD canvas.
    /// </summary>
    public class DiggerRobotHUD : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private string robotDisplayName = "Digger Bot";
        [SerializeField] private Vector2 panelSize = new Vector2(220f, 58f);
        [SerializeField] private Vector2 panelOffset = new Vector2(-20f, 40f); // right side, above center

        private DiggerRobotStateMachine stateMachine;
        private DiggerRobotBattery battery;
        private bool activatedOnce;

        // UI refs
        private GameObject panelRoot;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI stateText;
        private Image barFill;
        private Image barBg;

        private void Awake()
        {
            stateMachine = GetComponent<DiggerRobotStateMachine>();
            battery = GetComponent<DiggerRobotBattery>();
        }

        private void Update()
        {
            if (panelRoot == null)
            {
                // Lazily create UI — canvas may not exist yet at Start time
                CreateUI();
                if (panelRoot == null) return;
            }

            // Once the robot has been deployed at least once, HUD stays visible in all states
            if (stateMachine != null && stateMachine.CurrentState != DiggerRobotStateMachine.State.Docked)
                activatedOnce = true;

            bool show = stateMachine != null && activatedOnce;

            panelRoot.SetActive(show);
            if (!show) return;

            // Update state label
            if (stateText != null && stateMachine != null)
            {
                stateText.text = GetStateLabel(stateMachine.CurrentState);
                stateText.color = GetStateColor(stateMachine.CurrentState);

                // Flashing red for Shutdown — make it impossible to miss
                if (stateMachine.CurrentState == DiggerRobotStateMachine.State.Shutdown)
                {
                    float flash = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                    stateText.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.4f, 0.1f), flash);
                    stateText.fontSize = 15f;
                    nameText.text = robotDisplayName + " - NO BATTERY";
                    nameText.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f, 1f), new Color(0.9f, 0.2f, 0.15f, 0.4f), flash);
                }
                else
                {
                    stateText.fontSize = 13f;
                    nameText.text = robotDisplayName;
                    nameText.color = new Color(0.7f, 0.85f, 1f);
                }
            }

            // Update battery bar
            if (barFill != null && battery != null)
            {
                float r = battery.Ratio;
                barFill.rectTransform.anchorMax = new Vector2(r, 1f);

                if (r > 0.5f) barFill.color = new Color(0.2f, 0.8f, 0.3f);
                else if (r > 0.2f) barFill.color = new Color(0.9f, 0.75f, 0.1f);
                else barFill.color = new Color(0.9f, 0.2f, 0.15f);
            }
        }

        private void OnDestroy()
        {
            if (panelRoot != null) Destroy(panelRoot);
        }

        private void CreateUI()
        {
            Canvas canvas = FindHUDCanvas();
            if (canvas == null) return;

            // Panel — bottom-right, above control hints
            panelRoot = new GameObject("RobotHUDPanel");
            panelRoot.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = panelOffset;
            panelRect.sizeDelta = panelSize;

            Image panelBg = panelRoot.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

            Outline panelOutline = panelRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.25f, 0.55f, 0.9f, 0.5f);
            panelOutline.effectDistance = new Vector2(1f, 1f);

            // Row 1: Robot name + state
            // Name (left)
            GameObject nameObj = new GameObject("RobotName");
            nameObj.transform.SetParent(panelRoot.transform, false);
            nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = robotDisplayName;
            nameText.fontSize = 15f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = new Color(0.7f, 0.85f, 1f);
            nameText.alignment = TextAlignmentOptions.Left;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.55f);
            nameRect.anchorMax = new Vector2(0.55f, 1f);
            nameRect.offsetMin = new Vector2(10f, 2f);
            nameRect.offsetMax = new Vector2(0f, -4f);

            // State (right)
            GameObject stateObj = new GameObject("RobotState");
            stateObj.transform.SetParent(panelRoot.transform, false);
            stateText = stateObj.AddComponent<TextMeshProUGUI>();
            stateText.text = "Idle";
            stateText.fontSize = 13f;
            stateText.color = Color.gray;
            stateText.alignment = TextAlignmentOptions.Right;

            RectTransform stateRect = stateObj.GetComponent<RectTransform>();
            stateRect.anchorMin = new Vector2(0.55f, 0.55f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.offsetMin = new Vector2(0f, 2f);
            stateRect.offsetMax = new Vector2(-10f, -4f);

            // Row 2: Battery bar
            // Bar background
            GameObject barBgObj = new GameObject("BarBg");
            barBgObj.transform.SetParent(panelRoot.transform, false);
            barBg = barBgObj.AddComponent<Image>();
            barBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            RectTransform barBgRect = barBgObj.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0.5f);
            barBgRect.offsetMin = new Vector2(10f, 6f);
            barBgRect.offsetMax = new Vector2(-10f, -4f);

            // Bar fill
            GameObject fillObj = new GameObject("BarFill");
            fillObj.transform.SetParent(barBgObj.transform, false);
            barFill = fillObj.AddComponent<Image>();
            barFill.color = new Color(0.2f, 0.8f, 0.3f);

            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.pivot = new Vector2(0f, 0.5f);

            panelRoot.SetActive(false);
        }

        private static string GetStateLabel(DiggerRobotStateMachine.State s)
        {
            switch (s)
            {
                case DiggerRobotStateMachine.State.Digging: return "Digging";
                case DiggerRobotStateMachine.State.Returning: return "Returning";
                case DiggerRobotStateMachine.State.Shutdown: return "Shutdown";
                case DiggerRobotStateMachine.State.Recharging: return "Charging";
                default: return "Idle";
            }
        }

        private static Color GetStateColor(DiggerRobotStateMachine.State s)
        {
            switch (s)
            {
                case DiggerRobotStateMachine.State.Digging: return new Color(0.3f, 0.9f, 0.4f);
                case DiggerRobotStateMachine.State.Returning: return new Color(0.9f, 0.75f, 0.2f);
                case DiggerRobotStateMachine.State.Shutdown: return new Color(0.9f, 0.25f, 0.2f);
                case DiggerRobotStateMachine.State.Recharging: return new Color(0.4f, 0.7f, 1f);
                default: return Color.gray;
            }
        }

        private static Canvas FindHUDCanvas()
        {
            string[] names = { "HUDCanvas", "GameCanvas", "MachineUICanvas", "MainCanvas" };
            foreach (var n in names)
            {
                var obj = GameObject.Find(n);
                if (obj != null)
                {
                    var c = obj.GetComponent<Canvas>();
                    if (c != null) return c;
                }
            }

            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay
                    && !c.name.Contains("Loading")
                    && c.sortingOrder < 9000)
                    return c;
            }
            return null;
        }
    }
}
