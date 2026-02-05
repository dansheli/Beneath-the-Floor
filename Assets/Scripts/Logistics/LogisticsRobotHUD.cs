using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeneathTheFloor.Logistics
{
    public class LogisticsRobotHUD : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private string robotDisplayName = "Logistics Bot";
        [SerializeField] private Vector2 panelSize = new Vector2(220f, 58f);
        [SerializeField] private Vector2 panelOffset = new Vector2(-20f, -30f);

        private LogisticsRobotController controller;
        private LogisticsBattery battery;
        private bool activatedOnce;

        private GameObject panelRoot;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI stateText;
        private Image barFill;

        private void Awake()
        {
            controller = GetComponent<LogisticsRobotController>();
            battery = GetComponent<LogisticsBattery>();
        }

        private void Update()
        {
            if (panelRoot == null)
            {
                CreateUI();
                if (panelRoot == null) return;
            }

            if (controller != null && controller.CurrentState != LogisticsState.Docked)
                activatedOnce = true;

            bool show = controller != null && activatedOnce;
            panelRoot.SetActive(show);
            if (!show) return;

            if (stateText != null && controller != null)
            {
                stateText.text = GetStateLabel(controller.CurrentState);
                stateText.color = GetStateColor(controller.CurrentState);

                if (controller.CurrentState == LogisticsState.Shutdown)
                {
                    float flash = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                    stateText.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.4f, 0.1f), flash);
                    nameText.text = robotDisplayName + " - NO BATTERY";
                    nameText.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f, 1f), new Color(0.9f, 0.2f, 0.15f, 0.4f), flash);
                }
                else
                {
                    nameText.text = robotDisplayName;
                    nameText.color = new Color(0.4f, 0.9f, 0.7f);
                }
            }

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

            panelRoot = new GameObject("LogisticsHUDPanel");
            panelRoot.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = panelOffset;
            panelRect.sizeDelta = panelSize;

            Image panelBg = panelRoot.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.12f, 0.1f, 0.88f);

            Outline panelOutline = panelRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.2f, 0.8f, 0.6f, 0.5f);
            panelOutline.effectDistance = new Vector2(1f, 1f);

            // Name
            GameObject nameObj = new GameObject("RobotName");
            nameObj.transform.SetParent(panelRoot.transform, false);
            nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = robotDisplayName;
            nameText.fontSize = 15f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = new Color(0.4f, 0.9f, 0.7f);
            nameText.alignment = TextAlignmentOptions.Left;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.55f);
            nameRect.anchorMax = new Vector2(0.55f, 1f);
            nameRect.offsetMin = new Vector2(10f, 2f);
            nameRect.offsetMax = new Vector2(0f, -4f);

            // State
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

            // Battery bar
            GameObject barBgObj = new GameObject("BarBg");
            barBgObj.transform.SetParent(panelRoot.transform, false);
            Image barBg = barBgObj.AddComponent<Image>();
            barBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            RectTransform barBgRect = barBgObj.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0.5f);
            barBgRect.offsetMin = new Vector2(10f, 6f);
            barBgRect.offsetMax = new Vector2(-10f, -4f);

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

        private static string GetStateLabel(LogisticsState s)
        {
            switch (s)
            {
                case LogisticsState.FollowPlayer: return "Following";
                case LogisticsState.WorkWithDigger: return "With Digger";
                case LogisticsState.CollectingPickup: return "Collecting";
                case LogisticsState.TravelToUnload: return "To Container";
                case LogisticsState.Unloading: return "Unloading";
                case LogisticsState.TravelToRecharge: return "Returning";
                case LogisticsState.Recharging: return "Charging";
                case LogisticsState.Shutdown: return "Shutdown";
                case LogisticsState.FetchingItem: return "Fetching";
                case LogisticsState.DeliveringItem: return "Delivering";
                default: return "Idle";
            }
        }

        private static Color GetStateColor(LogisticsState s)
        {
            switch (s)
            {
                case LogisticsState.FollowPlayer: return new Color(0.3f, 0.9f, 0.6f);
                case LogisticsState.WorkWithDigger: return new Color(0.3f, 0.7f, 0.9f);
                case LogisticsState.CollectingPickup: return new Color(0.9f, 0.9f, 0.3f);
                case LogisticsState.TravelToUnload: return new Color(0.9f, 0.6f, 0.2f);
                case LogisticsState.Unloading: return new Color(0.9f, 0.6f, 0.2f);
                case LogisticsState.TravelToRecharge: return new Color(0.7f, 0.7f, 0.2f);
                case LogisticsState.Recharging: return new Color(0.4f, 0.7f, 1f);
                case LogisticsState.Shutdown: return new Color(0.9f, 0.25f, 0.2f);
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
