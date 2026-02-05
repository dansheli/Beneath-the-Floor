using UnityEngine;
using UnityEngine.UI;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Logistics
{
    public class LogisticsBattery : MonoBehaviour
    {
        private LogisticsRobotConfig config;
        private float current;

        private Canvas worldCanvas;
        private Image barFill;

        public float Ratio => config != null && config.maxBattery > 0 ? current / config.maxBattery : 0f;
        public bool IsLow => Ratio <= (config != null ? config.lowBatteryThreshold : 0.15f);
        public bool IsDepleted => current <= 0f;
        public bool IsFull => config != null && current >= config.maxBattery;

        public void Init(LogisticsRobotConfig cfg)
        {
            config = cfg;
            current = cfg.maxBattery;
            CreateBatteryUI();
        }

        public void Drain(float amount)
        {
            float multiplier = FirstRoomUpgradeStationUI.RobotEfficiency ? 0.6f : 1f;
            current = Mathf.Max(0f, current - amount * multiplier);
            UpdateBar();
        }

        public void DrainPassive(float deltaTime, float drainRate)
        {
            Drain(drainRate * deltaTime);
        }

        public void Recharge(float deltaTime)
        {
            if (config == null) return;
            current = Mathf.Min(config.maxBattery, current + config.rechargeRate * deltaTime);
            UpdateBar();
        }

        private void CreateBatteryUI()
        {
            GameObject canvasObj = new GameObject("BatteryCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = Vector3.up * 0.8f;

            worldCanvas = canvasObj.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(0.6f, 0.08f);

            GameObject bgObj = new GameObject("BG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(canvasObj.transform, false);
            barFill = fillObj.AddComponent<Image>();
            barFill.color = Color.green;
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.pivot = new Vector2(0f, 0.5f);
        }

        private void UpdateBar()
        {
            if (barFill == null) return;
            float r = Ratio;
            barFill.rectTransform.anchorMax = new Vector2(r, 1f);

            if (r > 0.5f) barFill.color = Color.green;
            else if (r > 0.2f) barFill.color = Color.yellow;
            else barFill.color = Color.red;
        }

        private void LateUpdate()
        {
            if (worldCanvas == null) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                worldCanvas.transform.LookAt(
                    worldCanvas.transform.position + cam.transform.forward);
            }
        }
    }
}
