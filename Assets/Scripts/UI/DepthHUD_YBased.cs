using TMPro;
using UnityEngine;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Simple, direct depth HUD that reads player Y position every frame.
    /// Does NOT rely on events or DepthManager - guaranteed to work.
    ///
    /// Shows height relative to basement floor:
    /// - 0.0m when standing on basement floor
    /// - Positive values when above the floor (jumping)
    /// - Negative values when below (digging down)
    /// </summary>
    public class DepthHUD_YBased : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("TextMeshProUGUI component for displaying depth. Auto-found if null.")]
        [SerializeField] private TextMeshProUGUI depthText;

        [Tooltip("Reference to the basement floor transform (top surface). If null, uses basementFloorY.")]
        [SerializeField] private Transform basementFloorRef;

        [Tooltip("Player root transform (the one that moves with the player). Auto-found if null.")]
        [SerializeField] private Transform playerRoot;

        [Header("Fallback Values")]
        [Tooltip("Y position of basement floor surface (used if basementFloorRef is null).")]
        [SerializeField] private float basementFloorY = -3.0f;

        [Header("Display Settings")]
        [Tooltip("Number of decimal places to show.")]
        [SerializeField] private int decimalPlaces = 1;

        [Tooltip("Suffix text after the number.")]
        [SerializeField] private string suffix = "m";

        [Header("Visual Settings")]
        [Tooltip("Text color when above or at floor level.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Text color when below floor level (digging).")]
        [SerializeField] private Color belowFloorColor = new Color(1f, 0.9f, 0.7f); // Slightly warm

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private string _formatString;
        private bool _isInitialized = false;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Build format string based on decimal places
            _formatString = decimalPlaces <= 0 ? "0" : $"0.{new string('0', decimalPlaces)}";

            // Auto-find depthText if not assigned
            if (depthText == null)
            {
                depthText = GetComponent<TextMeshProUGUI>();
                if (depthText == null)
                {
                    depthText = GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            // Auto-find player if not assigned
            if (playerRoot == null)
            {
                // Try Camera.main first (common for first-person games)
                if (Camera.main != null)
                {
                    // Usually we want the CharacterController root, not the camera itself
                    var cc = Camera.main.GetComponentInParent<CharacterController>();
                    if (cc != null)
                    {
                        playerRoot = cc.transform;
                    }
                    else
                    {
                        playerRoot = Camera.main.transform;
                    }
                }

                // Try "Player" tag
                if (playerRoot == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        playerRoot = player.transform;
                    }
                }
            }

            // Try to sync basementFloorY from existing managers
            if (basementFloorRef == null)
            {
                // Try Digging.DepthManager
                var depthMgr = FindObjectOfType<Digging.DepthManager>();
                if (depthMgr != null)
                {
                    basementFloorY = depthMgr.BasementFloorY;
                    if (enableDebugLogs)
                        Debug.Log($"[DepthHUD_YBased] Synced basementFloorY from DepthManager: {basementFloorY}");
                }
                else
                {
                    // Try UndergroundTerrainManager
                    var terrainMgr = FindObjectOfType<Digging.UndergroundTerrainManager>();
                    if (terrainMgr != null)
                    {
                        basementFloorY = terrainMgr.basementFloorY;
                        if (enableDebugLogs)
                            Debug.Log($"[DepthHUD_YBased] Synced basementFloorY from TerrainManager: {basementFloorY}");
                    }
                }
            }

            // Validation
            if (depthText == null)
            {
                Debug.LogError("[DepthHUD_YBased] No TextMeshProUGUI found! HUD will not display.");
                enabled = false;
                return;
            }

            if (playerRoot == null)
            {
                Debug.LogError("[DepthHUD_YBased] No player transform found! HUD will not update.");
                enabled = false;
                return;
            }

            _isInitialized = true;

            if (enableDebugLogs)
            {
                Debug.Log($"[DepthHUD_YBased] Initialized successfully:");
                Debug.Log($"[DepthHUD_YBased]   depthText = {depthText.name}");
                Debug.Log($"[DepthHUD_YBased]   playerRoot = {playerRoot.name}");
                Debug.Log($"[DepthHUD_YBased]   basementFloorY = {GetBasementY():F2}");
            }

            // Set initial value
            UpdateDisplay();
        }

        private void Update()
        {
            if (!_isInitialized) return;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (depthText == null || playerRoot == null) return;

            float basementY = GetBasementY();
            float playerY = playerRoot.position.y;

            // Depth below basement (positive when underground)
            float depthBelow = basementY - playerY;

            // Snap very small values to zero
            if (Mathf.Abs(depthBelow) < 0.05f)
                depthBelow = 0f;

            // Simple display - just depth in green
            depthText.text = $"Depth: {depthBelow.ToString(_formatString)}{suffix}";
            depthText.color = Color.green;

            if (enableDebugLogs)
                Debug.Log($"[DepthHUD_YBased] playerY={playerY:F2}, basementY={basementY:F2}, depth={depthBelow:F2}");
        }

        private float GetBasementY()
        {
            if (basementFloorRef != null)
            {
                // Get top surface of floor (position + half scale)
                return basementFloorRef.position.y + (basementFloorRef.localScale.y / 2f);
            }
            return basementFloorY;
        }

        /// <summary>
        /// Manually set the basement floor Y value.
        /// </summary>
        public void SetBasementFloorY(float y)
        {
            basementFloorY = y;
        }

        /// <summary>
        /// Manually set the player transform to track.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            playerRoot = player;
            if (!_isInitialized && depthText != null && playerRoot != null)
            {
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Force a re-initialization (useful if references change at runtime).
        /// </summary>
        [ContextMenu("Re-Initialize")]
        public void ReInitialize()
        {
            _isInitialized = false;
            Initialize();
        }
    }
}
