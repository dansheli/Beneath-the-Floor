using System;
using UnityEngine;
using BeneathTheFloor.Missions;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Pickup component for the radar on grandpa's table.
    /// Only shows interaction prompt when the RadarPickedUp mission is active.
    /// </summary>
    public class RadarPickup : MonoBehaviour
    {
        public static event Action OnRadarPickedUp;

        [Header("Interaction")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private string promptText = "Press E to take";

        [Header("UI")]
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.3f, 0f);

        // State
        private bool isPlayerInRange = false;
        private bool hasBeenPickedUp = false;
        private Transform playerTransform;
        private Camera playerCamera;

        // UI Elements
        private GameObject promptUI;
        private TMPro.TextMeshProUGUI promptTextComponent;
        private Canvas worldCanvas;

        private void Start()
        {
            // Find player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                player = GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }

            playerCamera = Camera.main;

            CreatePromptUI();
        }

        private void CreatePromptUI()
        {
            // Create world-space canvas for prompt
            GameObject canvasObj = new GameObject("RadarPickupPrompt");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = promptOffset;

            worldCanvas = canvasObj.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = canvasObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200f, 50f);
            rectTransform.localScale = Vector3.one * 0.01f;

            // Create text
            promptUI = new GameObject("PromptText");
            promptUI.transform.SetParent(canvasObj.transform, false);

            var textRect = promptUI.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200f, 50f);

            promptTextComponent = promptUI.AddComponent<TMPro.TextMeshProUGUI>();
            promptTextComponent.text = promptText;
            promptTextComponent.fontSize = 24;
            promptTextComponent.color = Color.white;
            promptTextComponent.alignment = TMPro.TextAlignmentOptions.Center;
            promptTextComponent.outlineWidth = 0.2f;
            promptTextComponent.outlineColor = Color.black;

            // Start hidden
            canvasObj.SetActive(false);
        }

        private void Update()
        {
            if (hasBeenPickedUp) return;
            if (playerTransform == null) return;

            // Only allow interaction when RadarPickedUp mission is active
            bool missionActive = IsMissionActive();

            // Check distance to player
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool wasInRange = isPlayerInRange;
            isPlayerInRange = distance <= interactionRange && missionActive;

            // Show/hide prompt
            if (isPlayerInRange != wasInRange)
            {
                if (worldCanvas != null)
                {
                    worldCanvas.gameObject.SetActive(isPlayerInRange);
                }
            }

            // Make prompt face camera
            if (isPlayerInRange && worldCanvas != null && playerCamera != null)
            {
                worldCanvas.transform.rotation = Quaternion.LookRotation(
                    worldCanvas.transform.position - playerCamera.transform.position
                );
            }

            // Handle pickup input
            if (isPlayerInRange && Input.GetKeyDown(interactKey))
            {
                PickupRadar();
            }
        }

        /// <summary>
        /// Check if the RadarPickedUp mission is currently active.
        /// </summary>
        private bool IsMissionActive()
        {
            if (MissionManager.Instance == null) return false;
            if (MissionManager.Instance.CurrentMission == null) return false;

            return MissionManager.Instance.CurrentMission.completionTrigger == MissionTriggerType.RadarPickedUp;
        }

        private void PickupRadar()
        {
            hasBeenPickedUp = true;

            Debug.Log("[RadarPickup] Radar picked up!");

            // Hide prompt
            if (worldCanvas != null)
            {
                worldCanvas.gameObject.SetActive(false);
            }

            // Fire event for mission system
            OnRadarPickedUp?.Invoke();

            // Unlock the radar tool
            if (RadarTool.Instance != null)
            {
                RadarTool.Instance.UnlockRadar();
            }

            // Hide this prefab (the one on the table)
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (worldCanvas != null)
            {
                Destroy(worldCanvas.gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
