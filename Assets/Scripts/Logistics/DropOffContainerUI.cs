using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BeneathTheFloor.UI;
namespace BeneathTheFloor.Logistics
{
    public class DropOffContainerUI : MonoBehaviour
    {
        public static DropOffContainerUI Instance { get; private set; }

        private GameObject mainPanel;
        private Transform contentArea;
        private DropOffContainer currentContainer;
        private bool isOpen;

        private static readonly Color bgColor = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        private static readonly Color rowColor = new Color(0.12f, 0.14f, 0.17f, 1f);
        private static readonly Color headerColor = new Color(0.2f, 0.75f, 0.5f);

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

        public void ShowUI(DropOffContainer container)
        {
            currentContainer = container;
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
            currentContainer = null;
        }

        private void BuildUI()
        {
            if (mainPanel != null) Destroy(mainPanel);

            Canvas canvas = FindOrCreateCanvas();
            if (canvas == null) return;

            // Main panel
            mainPanel = new GameObject("ContainerUIPanel");
            mainPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500f, 400f);

            Image panelBg = mainPanel.AddComponent<Image>();
            panelBg.color = bgColor;

            Outline outline = mainPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.7f, 0.5f, 0.6f);
            outline.effectDistance = new Vector2(2f, 2f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(mainPanel.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "RESOURCE CONTAINER";
            titleText.fontSize = 20f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = headerColor;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = new Vector2(0f, -5f);

            // Content area with vertical layout
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(mainPanel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(15f, 60f);
            contentRect.offsetMax = new Vector2(-15f, -50f);

            VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            contentArea = contentObj.transform;

            // Populate rows
            if (currentContainer != null)
            {
                var inv = currentContainer.GetInventory();
                foreach (var kv in inv)
                {
                    CreateRow(kv.Key, kv.Value);
                }
            }

            // Grand total
            int total = currentContainer != null ? currentContainer.GetTotalValue() : 0;
            GameObject totalObj = new GameObject("TotalRow");
            totalObj.transform.SetParent(contentArea, false);
            totalObj.AddComponent<LayoutElement>().minHeight = 30f;
            TextMeshProUGUI totalText = totalObj.AddComponent<TextMeshProUGUI>();
            totalText.text = $"Total Value: ${total}";
            totalText.fontSize = 16f;
            totalText.fontStyle = FontStyles.Bold;
            totalText.color = new Color(0.9f, 0.85f, 0.3f);
            totalText.alignment = TextAlignmentOptions.Right;

            // Take All button
            CreateButton(mainPanel.transform, "Take All", new Vector2(0f, 0f), () =>
            {
                if (currentContainer != null)
                {
                    currentContainer.TakeAll();
                    BuildUI(); // Refresh
                }
            });

            // Close hint
            GameObject closeObj = new GameObject("CloseHint");
            closeObj.transform.SetParent(mainPanel.transform, false);
            TextMeshProUGUI closeText = closeObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "Press E or ESC to close";
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

        private void CreateRow(string resourceId, ContainerSlot slot)
        {
            GameObject row = new GameObject($"Row_{resourceId}");
            row.transform.SetParent(contentArea, false);
            row.AddComponent<LayoutElement>().minHeight = 32f;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = rowColor;

            // Icon (if available)
            float nameStartX = 0.08f;
            if (slot.icon != null)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(row.transform, false);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = slot.icon;
                iconImg.preserveAspect = true;
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.1f);
                iconRect.anchorMax = new Vector2(0.07f, 0.9f);
                iconRect.offsetMin = new Vector2(4f, 0f);
                iconRect.offsetMax = Vector2.zero;
            }

            // Item name — use display name
            string name = !string.IsNullOrEmpty(slot.displayName) ? slot.displayName : resourceId;
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = name;
            nameText.fontSize = 14f;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(nameStartX, 0f);
            nameRect.anchorMax = new Vector2(0.4f, 1f);
            nameRect.offsetMin = new Vector2(10f, 0f);
            nameRect.offsetMax = Vector2.zero;

            // Quantity
            GameObject qtyObj = new GameObject("Qty");
            qtyObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI qtyText = qtyObj.AddComponent<TextMeshProUGUI>();
            qtyText.text = $"x{slot.quantity}";
            qtyText.fontSize = 14f;
            qtyText.color = new Color(0.8f, 0.8f, 0.8f);
            qtyText.alignment = TextAlignmentOptions.Center;
            RectTransform qtyRect = qtyObj.GetComponent<RectTransform>();
            qtyRect.anchorMin = new Vector2(0.4f, 0f);
            qtyRect.anchorMax = new Vector2(0.55f, 1f);
            qtyRect.offsetMin = Vector2.zero;
            qtyRect.offsetMax = Vector2.zero;

            // Price
            GameObject priceObj = new GameObject("Price");
            priceObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI priceText = priceObj.AddComponent<TextMeshProUGUI>();
            priceText.text = $"${slot.creditValuePerUnit}/u = ${slot.quantity * slot.creditValuePerUnit}";
            priceText.fontSize = 13f;
            priceText.color = new Color(0.9f, 0.85f, 0.3f);
            priceText.alignment = TextAlignmentOptions.Right;
            RectTransform priceRect = priceObj.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.55f, 0f);
            priceRect.anchorMax = new Vector2(1f, 1f);
            priceRect.offsetMin = Vector2.zero;
            priceRect.offsetMax = new Vector2(-10f, 0f);
        }

        private void CreateButton(Transform parent, string label, Vector2 anchoredPos, System.Action onClick)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = new Vector2(150f, 35f);
            btnRect.anchoredPosition = new Vector2(anchoredPos.x, 25f);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.15f, 0.5f, 0.35f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 15f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private Canvas FindOrCreateCanvas()
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
