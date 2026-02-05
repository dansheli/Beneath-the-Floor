using System;
using System.Collections.Generic;
using UnityEngine;
using BeneathTheFloor.GameFlow;
using BeneathTheFloor.Tools;
using BeneathTheFloor.Digging;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.Winch;
using BeneathTheFloor.Machines;
using BeneathTheFloor.UI;
using BeneathTheFloor.World;
using BeneathTheFloor.Player;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// Central manager for all missions in the game.
    /// Clean, modular design for easy expansion.
    /// </summary>
    /// <summary>
    /// Links a mission to its marker object in the scene.
    /// </summary>
    [System.Serializable]
    public class MissionMarkerLink
    {
        [Tooltip("The mission this marker is for")]
        public MissionData mission;
        [Tooltip("The marker GameObject to show/hide (place in scene and drag here)")]
        public GameObject markerObject;
    }

    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Mission Configuration")]
        [Tooltip("List of missions in order")]
        [SerializeField] private List<MissionData> missions = new List<MissionData>();

        [Header("Mission Markers")]
        [Tooltip("Link each mission to its marker object in the scene")]
        [SerializeField] private List<MissionMarkerLink> missionMarkers = new List<MissionMarkerLink>();

        [Header("References")]
        [SerializeField] private MissionUI missionUI;

        [Header("Location Detection")]
        [SerializeField] private float locationReachDistance = 2f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // State
        private int currentMissionIndex = -1;
        private MissionData currentMission;
        private GameObject currentActiveMarker;  // Currently active marker object
        private bool isInitialized = false;
        private Transform playerTransform;
        private Transform currentLocationTarget;
        private float previousCableLimit = 0f;  // Stored when winch is upgraded
        private int currentDigCount = 0;  // Track digs for FirstDig missions with requiredDigCount > 0
        private int currentResourceCount = 0;  // Track resources for ResourceCollected missions with requiredResourceCount > 0
        private InventorySystem inventorySystem;  // Cached reference for reading inventory counts
        private bool markerHiddenByDig = false;  // Track if marker was hidden by first dig

        // Marker lookup by mission ID (string key for reliable matching)
        private Dictionary<string, GameObject> markersByMissionId = new Dictionary<string, GameObject>();

        // Floating marker for fallback (when no placed marker exists)
        private FloatingObjectiveMarker floatingMarker;

        // Subtle ring marker (for missions with useSubtleMarker = true)
        private SubtleRingMarker subtleMarker;

        // Events
        public event Action<MissionData> OnMissionStarted;
        public event Action<MissionData> OnMissionCompleted;
        public event Action OnAllMissionsCompleted;

        public MissionData CurrentMission => currentMission;
        public bool HasActiveMission => currentMission != null;
        public bool AllMissionsComplete => currentMissionIndex >= missions.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            // Check for location-based mission completion
            if (currentMission != null &&
                currentMission.completionTrigger == MissionTriggerType.LocationReached &&
                currentLocationTarget != null &&
                playerTransform != null)
            {
                float distance = Vector3.Distance(playerTransform.position, currentLocationTarget.position);
                if (distance <= locationReachDistance)
                {
                    CompleteMission();
                }
            }

            // Check for past previous cable limit mission completion
            if (currentMission != null &&
                currentMission.completionTrigger == MissionTriggerType.PastPreviousCableLimit &&
                previousCableLimit > 0f &&
                WinchAnchor.Instance != null &&
                playerTransform != null)
            {
                // Get current cable length (distance from anchor to player)
                float currentCableLength = WinchAnchor.Instance.GetCurrentCableLength();

                // Check if player has gone past the previous limit
                if (currentCableLength > previousCableLimit + 0.5f)  // Small buffer to ensure they're past it
                {
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Player passed previous cable limit! Current: {currentCableLength}m, Previous limit: {previousCableLimit}m");
                    CompleteMission();
                }
            }

            // Check for depth reached mission completion
            if (currentMission != null &&
                currentMission.completionTrigger == MissionTriggerType.DepthReached &&
                playerTransform != null)
            {
                float playerDepth = playerTransform.position.y;

                // Check if player has reached the required depth (Y is negative when underground)
                if (playerDepth <= currentMission.requiredDepth)
                {
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Player reached required depth! Current: {playerDepth}m, Required: {currentMission.requiredDepth}m");
                    CompleteMission();
                }
            }
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // Force log for debugging
            if (enableDebugLogs) Debug.Log($"[MissionManager] Initialize called. Missions count: {missions.Count}");

            // Find or create UI
            if (missionUI == null)
            {
                missionUI = FindFirstObjectByType<MissionUI>();
                if (missionUI == null)
                {
                    CreateMissionUI();
                }
            }

            // Get or create floating marker (fallback for missions without placed markers)
            floatingMarker = FloatingObjectiveMarker.GetOrCreate();

            // Cache all mission markers from the serialized list
            CacheMissionMarkers();

            // Find player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                // Fallback: find by name
                player = GameObject.Find("Player");
                if (player != null) playerTransform = player.transform;
            }

            // Subscribe to events
            SubscribeToEvents();

            isInitialized = true;

            // Start first mission
            if (missions.Count > 0)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Starting first mission: {missions[0]?.missionName}");
                StartNextMission();
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning("[MissionManager] No missions configured! Add missions to the list in Inspector.");
            }
        }

        private void CreateMissionUI()
        {
            GameObject uiObj = new GameObject("MissionUI");
            missionUI = uiObj.AddComponent<MissionUI>();
        }

        private void CacheMissionMarkers()
        {
            markersByMissionId.Clear();

            // Build dictionary from the serialized missionMarkers list using missionId as key
            foreach (var link in missionMarkers)
            {
                if (link.mission == null) continue;
                if (link.markerObject == null) continue;

                string missionId = link.mission.missionId;
                if (string.IsNullOrEmpty(missionId))
                {
                    missionId = link.mission.missionName;
                }

                if (markersByMissionId.ContainsKey(missionId)) continue;

                markersByMissionId[missionId] = link.markerObject;

                // Ensure marker starts hidden
                link.markerObject.SetActive(false);

                if (enableDebugLogs) Debug.Log($"[MissionManager] Cached marker for mission: {link.mission.missionName} (id: {missionId})");
            }

            if (enableDebugLogs) Debug.Log($"[MissionManager] Cached {markersByMissionId.Count} mission markers");
        }

        private void SubscribeToEvents()
        {
            // Subscribe to note closed events (fires when player finishes reading and closes the note)
            var notes = FindObjectsByType<ReadableNote>(FindObjectsSortMode.None);
            foreach (var note in notes)
            {
                note.OnNoteClosed += OnNoteClosed;
            }

            if (enableDebugLogs) Debug.Log($"[MissionManager] Found and subscribed to {notes.Length} ReadableNote(s)");

            // Subscribe to dig events (single DiggingSystem - handles both V2 and V3)
            var diggingSystem = FindFirstObjectByType<DiggingSystem>();
            if (diggingSystem != null)
            {
                diggingSystem.OnDigCompleted += OnDigCompleted;
            }

            // Subscribe to node revealed events
            var nodeManager = FindFirstObjectByType<HiddenNodeManager>();
            if (nodeManager != null)
            {
                nodeManager.OnNodeRevealed += OnNodeRevealed;
            }

            // Subscribe to inventory events
            inventorySystem = FindFirstObjectByType<InventorySystem>();
            if (inventorySystem != null)
            {
                inventorySystem.OnItemAdded += OnResourceCollected;
                inventorySystem.OnInventoryChanged += OnInventoryChanged;
                if (enableDebugLogs) Debug.Log("[MissionManager] Subscribed to InventorySystem events");
            }
            else
            {
                // Try to subscribe later if inventory isn't ready yet
                if (enableDebugLogs) Debug.LogWarning("[MissionManager] InventorySystem not found, will retry subscription");
                StartCoroutine(SubscribeToInventoryDelayed());
            }

            // Subscribe to resource pickup events (fires when picking up world drops)
            GameEvents.OnResourcePickedUp += OnResourcePickedUpFromWorld;

            // Subscribe to dust manager events (dust is separate from inventory)
            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged += OnDustChanged;
                if (enableDebugLogs) Debug.Log("[MissionManager] Subscribed to DustManager.OnDustChanged");
            }
            else
            {
                StartCoroutine(SubscribeToDustManagerDelayed());
            }

            // Subscribe to sell events (both items and dust)
            GameEvents.OnItemsSold += OnItemsSold;
            GameEvents.OnDustSold += OnDustSold;

            // Subscribe to winch exit events (for help text hiding)
            WinchExitTrigger.OnPlayerExitedWinch += OnPlayerExitedWinch;

            // Subscribe to cable limit event
            WinchMotor.OnCableLimitReached += OnCableLimitReached;

            // Subscribe to upgrade events
            var upgradeStation = UpgradeStation.Instance;
            if (upgradeStation != null)
            {
                upgradeStation.OnRuntimeUpgradeApplied += OnRuntimeUpgradeApplied;
            }
            else
            {
                // Try to find it and subscribe later
                StartCoroutine(SubscribeToUpgradeStationDelayed());
            }

            // Subscribe to radar events
            RadarPickup.OnRadarPickedUp += OnRadarPickedUp;
            RadarTool.OnRadarActivated += OnRadarActivated;

            // Subscribe to treasure chest events
            if (TreasureChests.TreasureChestManager.Instance != null)
            {
                TreasureChests.TreasureChestManager.Instance.OnChestOpened += OnTreasureChestOpened;
            }
            else
            {
                StartCoroutine(SubscribeToTreasureChestManagerDelayed());
            }

            // Subscribe to room entrance trigger events
            RoomEntranceTrigger.OnRoomEntranceTriggered += OnRoomEntranceTriggered;

            // Subscribe to crystal insertion events
            EngineActivationInteract.OnCrystalInserted += OnCrystalInserted;

            // Subscribe to jetpack and drill pike pickup events
            JetpackPickup.OnJetpackPickedUp += OnJetpackPickedUp;
            DrillPikePickup.OnDrillPikePickedUp += OnDrillPikePickedUp;

            if (enableDebugLogs) Debug.Log("[MissionManager] Event subscriptions complete");
        }

        private System.Collections.IEnumerator SubscribeToUpgradeStationDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            var upgradeStation = UpgradeStation.Instance;
            if (upgradeStation != null)
            {
                upgradeStation.OnRuntimeUpgradeApplied += OnRuntimeUpgradeApplied;
            }
        }

        private System.Collections.IEnumerator SubscribeToInventoryDelayed()
        {
            // Try multiple times to find inventory
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.5f);
                inventorySystem = FindFirstObjectByType<InventorySystem>();
                if (inventorySystem != null)
                {
                    inventorySystem.OnItemAdded += OnResourceCollected;
                    inventorySystem.OnInventoryChanged += OnInventoryChanged;
                    if (enableDebugLogs) Debug.Log("[MissionManager] Subscribed to InventorySystem events (delayed)");
                    yield break;
                }
            }
        }

        private System.Collections.IEnumerator SubscribeToDustManagerDelayed()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.5f);
                if (DustManager.Instance != null)
                {
                    DustManager.Instance.OnDustChanged += OnDustChanged;
                    if (enableDebugLogs) Debug.Log("[MissionManager] Subscribed to DustManager.OnDustChanged (delayed)");
                    yield break;
                }
            }
        }

        private System.Collections.IEnumerator SubscribeToTreasureChestManagerDelayed()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.5f);
                if (TreasureChests.TreasureChestManager.Instance != null)
                {
                    TreasureChests.TreasureChestManager.Instance.OnChestOpened += OnTreasureChestOpened;
                    if (enableDebugLogs) Debug.Log("[MissionManager] Subscribed to TreasureChestManager.OnChestOpened (delayed)");
                    yield break;
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from note events
            var notes = FindObjectsByType<ReadableNote>(FindObjectsSortMode.None);
            foreach (var note in notes)
            {
                if (note != null)
                    note.OnNoteClosed -= OnNoteClosed;
            }

            // Unsubscribe from dig events
            var diggingSystem = FindFirstObjectByType<DiggingSystem>();
            if (diggingSystem != null)
            {
                diggingSystem.OnDigCompleted -= OnDigCompleted;
            }

            // Unsubscribe from node events
            var nodeManager = FindFirstObjectByType<HiddenNodeManager>();
            if (nodeManager != null)
            {
                nodeManager.OnNodeRevealed -= OnNodeRevealed;
            }

            // Unsubscribe from inventory events
            if (inventorySystem != null)
            {
                inventorySystem.OnItemAdded -= OnResourceCollected;
                inventorySystem.OnInventoryChanged -= OnInventoryChanged;
            }

            // Unsubscribe from resource pickup events
            GameEvents.OnResourcePickedUp -= OnResourcePickedUpFromWorld;

            // Unsubscribe from dust manager events
            if (DustManager.Instance != null)
            {
                DustManager.Instance.OnDustChanged -= OnDustChanged;
            }

            // Unsubscribe from sell events
            GameEvents.OnItemsSold -= OnItemsSold;
            GameEvents.OnDustSold -= OnDustSold;

            // Unsubscribe from winch exit events
            WinchExitTrigger.OnPlayerExitedWinch -= OnPlayerExitedWinch;

            // Unsubscribe from cable limit event
            WinchMotor.OnCableLimitReached -= OnCableLimitReached;

            // Unsubscribe from upgrade events
            var upgradeStation = UpgradeStation.Instance;
            if (upgradeStation != null)
            {
                upgradeStation.OnRuntimeUpgradeApplied -= OnRuntimeUpgradeApplied;
            }

            // Unsubscribe from radar events
            RadarPickup.OnRadarPickedUp -= OnRadarPickedUp;
            RadarTool.OnRadarActivated -= OnRadarActivated;

            // Unsubscribe from treasure chest events
            if (TreasureChests.TreasureChestManager.Instance != null)
            {
                TreasureChests.TreasureChestManager.Instance.OnChestOpened -= OnTreasureChestOpened;
            }

            // Unsubscribe from room entrance trigger events
            RoomEntranceTrigger.OnRoomEntranceTriggered -= OnRoomEntranceTriggered;

            // Unsubscribe from crystal insertion events
            EngineActivationInteract.OnCrystalInserted -= OnCrystalInserted;

            // Unsubscribe from jetpack and drill pike pickup events
            JetpackPickup.OnJetpackPickedUp -= OnJetpackPickedUp;
            DrillPikePickup.OnDrillPikePickedUp -= OnDrillPikePickedUp;

            if (Instance == this)
                Instance = null;
        }

        #region Mission Flow

        /// <summary>
        /// Start the next mission in the sequence.
        /// </summary>
        public void StartNextMission()
        {
            currentMissionIndex++;

            if (currentMissionIndex >= missions.Count)
            {
                // All missions completed
                CompletedAllMissions();
                return;
            }

            StartMission(missions[currentMissionIndex]);
        }

        /// <summary>
        /// Start a specific mission.
        /// </summary>
        public void StartMission(MissionData mission)
        {
            if (mission == null)
            {
                LogDebug("Cannot start null mission");
                return;
            }

            currentMission = mission;
            markerHiddenByDig = false;  // Reset marker hidden state for new mission
            LogDebug($"Starting mission: {mission.missionName}");

            // Update UI
            if (missionUI != null)
            {
                missionUI.ShowObjective(mission.objectiveText);
            }

            // Show marker if configured (unless showMarkerOnResourceThreshold is true - then wait for threshold)
            if (mission.showMarker && !mission.showMarkerOnResourceThreshold)
            {
                ShowMarkerForMission(mission);
            }
            else
            {
                HideMarker();

                // Still set location target for LocationReached missions even without marker
                if (mission.completionTrigger == MissionTriggerType.LocationReached)
                {
                    SetLocationTargetForMission(mission);
                }
            }

            // Show help text if configured
            if (!string.IsNullOrEmpty(mission.helpText) && missionUI != null)
            {
                missionUI.ShowHelpText(mission.helpText);
                LogDebug($"Showing help text: {mission.helpText}");
            }
            else if (missionUI != null)
            {
                missionUI.HideHelpText();
            }

            // Show crosshair hint if configured
            if (!string.IsNullOrEmpty(mission.crosshairHintText) && missionUI != null)
            {
                missionUI.ShowCrosshairHint(mission.crosshairHintText);
                LogDebug($"Showing crosshair hint: {mission.crosshairHintText}");
            }
            else if (missionUI != null)
            {
                missionUI.HideCrosshairHint();
            }

            // Reset dig counter for FirstDig missions with requiredDigCount
            if (mission.completionTrigger == MissionTriggerType.FirstDig && mission.requiredDigCount > 0)
            {
                currentDigCount = 0;
                if (missionUI != null)
                {
                    missionUI.ShowCounter(currentDigCount, mission.requiredDigCount, "Digs");
                }
                LogDebug($"Dig counter started: 0 / {mission.requiredDigCount}");
            }

            // Reset resource counter for ResourceCollected missions with requiredResourceCount
            if (mission.completionTrigger == MissionTriggerType.ResourceCollected && mission.requiredResourceCount > 0)
            {
                // Get current count - use DustManager for dust, inventory for other resources
                if (mission.resourceCounterLabel == "Dust")
                {
                    // Dust is tracked separately by DustManager
                    currentResourceCount = DustManager.Instance != null ? Mathf.FloorToInt(DustManager.Instance.GetDust()) : 0;
                }
                else
                {
                    // Other resources use inventory count
                    currentResourceCount = GetTotalInventoryItemCount();
                }

                if (missionUI != null)
                {
                    missionUI.ShowCounter(currentResourceCount, mission.requiredResourceCount, mission.resourceCounterLabel);
                }
                LogDebug($"Resource counter started: {currentResourceCount} / {mission.requiredResourceCount} ({mission.resourceCounterLabel})");

                // Check if already complete (player might already have enough items)
                if (currentResourceCount >= mission.requiredResourceCount)
                {
                    LogDebug("Player already has enough resources - will complete after brief delay");
                    StartCoroutine(CompleteAfterDelay(0.5f));
                }
            }

            // Reset resource counter for ItemsSold missions that track resources (e.g., "Collect 15 Dust and Sell")
            if (mission.completionTrigger == MissionTriggerType.ItemsSold && mission.requiredResourceCount > 0)
            {
                // Get current count - use DustManager for dust, inventory for other resources
                if (mission.resourceCounterLabel == "Dust")
                {
                    currentResourceCount = DustManager.Instance != null ? Mathf.FloorToInt(DustManager.Instance.GetDust()) : 0;
                }
                else
                {
                    currentResourceCount = GetTotalInventoryItemCount();
                }

                if (missionUI != null)
                {
                    missionUI.ShowCounter(currentResourceCount, mission.requiredResourceCount, mission.resourceCounterLabel);
                }
                LogDebug($"Resource counter for sell mission started: {currentResourceCount} / {mission.requiredResourceCount} ({mission.resourceCounterLabel})");

                // If already at threshold, show crosshair hint and marker immediately
                if (currentResourceCount >= mission.requiredResourceCount)
                {
                    if (missionUI != null && !string.IsNullOrEmpty(mission.resourceThresholdCrosshairHint))
                    {
                        missionUI.ShowCrosshairHint(mission.resourceThresholdCrosshairHint);
                        LogDebug($"Already at threshold - showing crosshair hint: {mission.resourceThresholdCrosshairHint}");
                    }

                    // Show marker if configured to show on threshold
                    if (mission.showMarkerOnResourceThreshold && mission.showMarker)
                    {
                        ShowMarkerForMission(mission);
                        LogDebug("Already at threshold - showing marker");
                    }
                }
            }

            // Show winch upgrade marker for WinchUpgraded missions
            if (mission.completionTrigger == MissionTriggerType.WinchUpgraded)
            {
                // FIRST: Check if winch is already upgraded - skip this mission entirely if so
                var upgradeStation = UpgradeStation.Instance;
                if (upgradeStation != null)
                {
                    int winchLevel = upgradeStation.GetRuntimeUpgradeLevel("winch_cable");
                    if (winchLevel > 0)
                    {
                        LogDebug($"Winch already upgraded to level {winchLevel} - skipping mission and proceeding to next");
                        // Don't lock upgrades, don't show markers - just complete immediately
                        CompleteMission();
                        return; // Exit StartMission early
                    }
                }

                // Lock upgrades except winch if configured (static method - works before UI is opened)
                if (mission.lockUpgradesExceptWinch)
                {
                    UpgradeStationUI.LockUpgradesExceptWinch();
                    LogDebug("Upgrades locked except winch for this mission");
                }

                if (UpgradeStationUI.Instance != null)
                {
                    UpgradeStationUI.Instance.ShowWinchMissionMarker();
                    LogDebug("Showing winch mission marker in upgrade UI");
                }

                // Reset cable limit flag so event can fire during this mission
                if (WinchMotor.Instance != null)
                {
                    WinchMotor.Instance.ResetCableLimitFlag();

                    // Check if player is already at cable limit - if so, show secondary objective immediately
                    if (WinchMotor.Instance.IsAtCableLimit() &&
                        !string.IsNullOrEmpty(mission.secondaryObjectiveText))
                    {
                        if (missionUI != null)
                        {
                            missionUI.UpdateObjective(mission.secondaryObjectiveText);
                            LogDebug("Player already at cable limit - showing secondary objective immediately");
                        }
                    }
                }
            }

            OnMissionStarted?.Invoke(mission);

            // Check if mission condition is already met
            CheckIfMissionAlreadyComplete(mission);
        }

        /// <summary>
        /// Check if a mission's completion condition is already satisfied.
        /// This handles cases where the event fired before the mission started.
        /// </summary>
        private void CheckIfMissionAlreadyComplete(MissionData mission)
        {
            if (mission.completionTrigger == MissionTriggerType.NodeRevealed)
            {
                // Check if any nodes are already physically visible (have exposure > 0)
                var nodeManager = FindFirstObjectByType<HiddenNodeManager>();
                if (nodeManager != null && nodeManager.HasAnyVisibleNodes())
                {
                    if (enableDebugLogs) Debug.Log("[MissionManager] Node already visible - completing mission immediately!");
                    CompleteMission();
                }
            }
            else if (mission.completionTrigger == MissionTriggerType.WinchUpgraded)
            {
                // Check if winch cable is already upgraded (player upgraded before mission started)
                var upgradeStation = UpgradeStation.Instance;
                if (upgradeStation != null)
                {
                    int winchLevel = upgradeStation.GetRuntimeUpgradeLevel("winch_cable");
                    if (winchLevel > 0)
                    {
                        if (enableDebugLogs) Debug.Log($"[MissionManager] Winch already upgraded to level {winchLevel} - completing mission immediately!");
                        CompleteMission();
                    }
                }
            }
        }

        /// <summary>
        /// Complete the current mission.
        /// </summary>
        public void CompleteMission()
        {
            if (currentMission == null)
            {
                LogDebug("No active mission to complete");
                return;
            }

            LogDebug($"Mission completed: {currentMission.missionName}");

            // Handle reward
            HandleMissionReward(currentMission);

            // Show completion toast if configured
            if (!string.IsNullOrEmpty(currentMission.completionMessage) && missionUI != null)
            {
                missionUI.ShowCompletionToast(currentMission.completionMessage, currentMission.completionMessageDuration);
            }

            // Show centered popup if configured
            if (!string.IsNullOrEmpty(currentMission.centeredPopupMessage) && missionUI != null)
            {
                missionUI.ShowCenteredPopup(currentMission.centeredPopupMessage, currentMission.centeredPopupDuration);
            }

            // Hide marker and clear location target
            HideMarker();
            currentLocationTarget = null;

            // Hide counter if it was showing (used for both dig and resource counters)
            if (missionUI != null)
            {
                missionUI.HideCounter();
            }

            // Hide crosshair hint if it was showing
            if (missionUI != null)
            {
                missionUI.HideCrosshairHint();
            }

            // Hide winch upgrade marker if this was a WinchUpgraded mission
            if (currentMission.completionTrigger == MissionTriggerType.WinchUpgraded)
            {
                // Unlock all upgrades if they were locked for this mission (static method)
                if (currentMission.lockUpgradesExceptWinch)
                {
                    UpgradeStationUI.UnlockAllUpgrades();
                    LogDebug("All upgrades unlocked after mission completion");
                }

                if (UpgradeStationUI.Instance != null)
                {
                    UpgradeStationUI.Instance.HideWinchMissionMarker();
                    LogDebug("Hiding winch mission marker");
                }
            }

            // Fire event
            OnMissionCompleted?.Invoke(currentMission);

            // Clear current mission
            MissionData completedMission = currentMission;
            currentMission = null;

            // Start next mission immediately (no delay to avoid missing events)
            StartNextMission();
        }

        private System.Collections.IEnumerator StartNextMissionDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            StartNextMission();
        }

        private System.Collections.IEnumerator CompleteAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (currentMission != null)
            {
                CompleteMission();
            }
        }

        private void CompletedAllMissions()
        {
            LogDebug("All missions completed!");

            // Hide UI
            if (missionUI != null)
            {
                missionUI.HideObjective();
            }

            HideMarker();

            OnAllMissionsCompleted?.Invoke();
        }

        #endregion

        #region Markers

        private void ShowMarkerForMission(MissionData mission)
        {
            // Hide any previously active marker
            HideMarker();

            // Get mission ID for lookup
            string missionId = mission.missionId;
            if (string.IsNullOrEmpty(missionId))
            {
                missionId = mission.missionName;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[MissionManager] ShowMarkerForMission: {mission.missionName} (id: '{missionId}'), markersByMissionId count: {markersByMissionId.Count}");
                Debug.Log($"[MissionManager] Cached marker IDs: [{string.Join(", ", markersByMissionId.Keys)}]");
            }

            // FIRST: Look for a placed marker by mission ID
            if (markersByMissionId.TryGetValue(missionId, out GameObject markerObj))
            {
                if (markerObj != null)
                {
                    markerObj.SetActive(true);
                    currentActiveMarker = markerObj;
                    if (enableDebugLogs) Debug.Log($"[MissionManager] ACTIVATED marker '{markerObj.name}' for mission: {mission.missionName} (id: '{missionId}')");
                }

                // Store for location-based missions
                if (mission.completionTrigger == MissionTriggerType.LocationReached)
                {
                    currentLocationTarget = markerObj.transform;
                }
                return;
            }

            if (enableDebugLogs) Debug.Log($"[MissionManager] NO placed marker found for id '{missionId}', using fallback for: {mission.missionName}");

            // FALLBACK: Use FloatingObjectiveMarker with tag/name lookup
            Transform target = null;

            if (!string.IsNullOrEmpty(mission.markerTargetTag))
            {
                var obj = GameObject.FindGameObjectWithTag(mission.markerTargetTag);
                if (obj != null) target = obj.transform;
            }

            if (target == null && !string.IsNullOrEmpty(mission.markerTargetName))
            {
                var obj = GameObject.Find(mission.markerTargetName);
                if (obj != null)
                {
                    target = obj.transform;
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Found target by name: {mission.markerTargetName}");
                }
                else
                {
                    if (enableDebugLogs) Debug.LogWarning($"[MissionManager] Could not find object named: {mission.markerTargetName}");
                }
            }

            // For subtle markers, also check for RingMarkerTarget component
            if (mission.useSubtleMarker)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Mission uses subtle marker. RingMarkerTarget.Instance: {(RingMarkerTarget.Instance != null ? "Found" : "NULL")}");
            }

            if (mission.useSubtleMarker && RingMarkerTarget.Instance != null)
            {
                // Use RingMarkerTarget directly - it handles showing its own marker
                RingMarkerTarget.Instance.ShowMarker();
                if (enableDebugLogs) Debug.Log($"[MissionManager] Using RingMarkerTarget for mission: {mission.missionName}");
                return;
            }

            if (target != null)
            {
                // Use subtle marker or full marker based on mission config
                if (mission.useSubtleMarker)
                {
                    // Get or create subtle marker
                    if (subtleMarker == null)
                    {
                        subtleMarker = SubtleRingMarker.GetOrCreate();
                    }
                    subtleMarker.ShowAbove(target);
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Showing subtle ring marker above: {target.name} for mission: {mission.missionName}");
                }
                else if (floatingMarker != null)
                {
                    floatingMarker.ShowAbove(target);
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Showing floating marker above: {target.name} for mission: {mission.missionName}");
                }

                if (mission.completionTrigger == MissionTriggerType.LocationReached)
                {
                    currentLocationTarget = target;
                }
            }
        }

        private void HideMarker()
        {
            // Hide placed marker if active
            if (currentActiveMarker != null)
            {
                currentActiveMarker.SetActive(false);
                currentActiveMarker = null;
            }

            // Also hide floating marker
            if (floatingMarker != null)
            {
                floatingMarker.Hide();
            }

            // Also hide subtle marker
            if (subtleMarker != null)
            {
                subtleMarker.Hide();
            }

            // Also hide RingMarkerTarget if it's active
            if (RingMarkerTarget.Instance != null)
            {
                RingMarkerTarget.Instance.HideMarker();
            }
            // Don't clear currentLocationTarget here - it may still be needed for location checking
        }

        /// <summary>
        /// Set the location target for a mission without showing a marker.
        /// Used for LocationReached missions that use other guidance (like radar).
        /// </summary>
        private void SetLocationTargetForMission(MissionData mission)
        {
            Transform target = null;

            // Find by tag first
            if (!string.IsNullOrEmpty(mission.markerTargetTag))
            {
                var obj = GameObject.FindGameObjectWithTag(mission.markerTargetTag);
                if (obj != null)
                {
                    target = obj.transform;
                }
            }

            // Find by name if tag didn't work
            if (target == null && !string.IsNullOrEmpty(mission.markerTargetName))
            {
                var obj = GameObject.Find(mission.markerTargetName);
                if (obj != null)
                {
                    target = obj.transform;
                }
            }

            if (target != null)
            {
                currentLocationTarget = target;
                LogDebug($"Location target set to: {target.name} (no marker)");
            }
            else
            {
                LogDebug($"Could not find location target for mission: {mission.missionName}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnNoteClosed()
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.NoteRead)
            {
                if (enableDebugLogs) Debug.Log("[MissionManager] Completing mission due to NoteRead trigger (note closed)!");
                CompleteMission();
            }
        }

        private void OnDigCompleted(Digging.DigResult result)
        {
            if (currentMission == null) return;

            // Only complete on successful dig
            if (!result.Success) return;

            if (currentMission.completionTrigger == MissionTriggerType.FirstDig)
            {
                HandleDigForMission();
            }
            else if (currentMission.completionTrigger == MissionTriggerType.NodeRevealed)
            {
                // Check if any nodes are now visible after this dig
                var nodeManager = FindFirstObjectByType<HiddenNodeManager>();
                if (nodeManager != null && nodeManager.HasAnyVisibleNodes())
                {
                    if (enableDebugLogs) Debug.Log("[MissionManager] Node now visible after dig - completing mission!");
                    CompleteMission();
                }
            }

            // Hide marker on first dig for TreasureChestOpened missions
            if (currentMission != null &&
                currentMission.completionTrigger == MissionTriggerType.TreasureChestOpened &&
                currentMission.hideMarkerOnFirstDig &&
                !markerHiddenByDig)
            {
                HideMarker();
                markerHiddenByDig = true;
                if (enableDebugLogs) Debug.Log("[MissionManager] Marker hidden after first dig (TreasureChestOpened mission)");
            }

            // Hide crosshair hint on first dig if configured
            if (currentMission != null &&
                currentMission.crosshairHintHideTrigger == CrosshairHintHideTrigger.FirstDig && missionUI != null)
            {
                missionUI.HideCrosshairHint();
                if (enableDebugLogs) Debug.Log("[MissionManager] Crosshair hint hidden after first dig");
            }
        }

        private void HandleDigForMission()
        {
            if (currentMission == null) return;

            // Check if this mission uses a dig counter
            if (currentMission.requiredDigCount > 0)
            {
                currentDigCount++;

                // Hide marker after first dig if configured
                if (currentDigCount == 1 && currentMission.hideMarkerOnFirstDig)
                {
                    HideMarker();
                    LogDebug("Marker hidden after first dig");
                }

                // Show first dig popup if configured
                if (currentDigCount == 1 && !string.IsNullOrEmpty(currentMission.firstDigPopupMessage) && missionUI != null)
                {
                    missionUI.ShowCenteredPopup(currentMission.firstDigPopupMessage, currentMission.firstDigPopupDuration);
                    LogDebug($"First dig popup: {currentMission.firstDigPopupMessage}");
                }

                // Update counter UI
                if (missionUI != null)
                {
                    missionUI.UpdateDigCounter(currentDigCount, currentMission.requiredDigCount);
                }

                LogDebug($"Dig count: {currentDigCount} / {currentMission.requiredDigCount}");

                // Check if completed
                if (currentDigCount >= currentMission.requiredDigCount)
                {
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Dig mission completed! ({currentDigCount}/{currentMission.requiredDigCount})");
                    CompleteMission();
                }
            }
            else
            {
                // No counter - complete immediately (original FirstDig behavior)
                if (enableDebugLogs) Debug.Log("[MissionManager] First dig completed!");
                CompleteMission();
            }
        }

        private void OnNodeRevealed(HiddenNode node)
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.NodeRevealed)
            {
                // Only complete if node is physically visible (has some exposure)
                if (node != null && node.CurrentExposure > 0.1f)
                {
                    if (enableDebugLogs) Debug.Log("[MissionManager] Node visible - completing mission!");
                    CompleteMission();
                }
            }
        }

        private void OnResourceCollected(ItemSO item, int amount)
        {
            if (enableDebugLogs) Debug.Log($"[MissionManager] OnResourceCollected called: {item?.itemName ?? "NULL"} x{amount}");

            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.ResourceCollected)
            {
                if (currentMission.requiredResourceCount > 0)
                {
                    // Use inventory-based counting
                    UpdateResourceCountFromInventory();
                }
                else
                {
                    // No counter - complete immediately (original behavior)
                    if (enableDebugLogs) Debug.Log("[MissionManager] Resource collected - completing mission!");
                    CompleteMission();
                }
            }
        }

        private void OnResourcePickedUpFromWorld(Digging.UndergroundResourceType resourceType, int amount)
        {
            if (enableDebugLogs) Debug.Log($"[MissionManager] OnResourcePickedUpFromWorld: {resourceType} x{amount}");

            // Trigger inventory check for resource collection missions
            UpdateResourceCountFromInventory();
        }

        private void OnInventoryChanged()
        {
            // Update resource count from inventory when it changes
            UpdateResourceCountFromInventory();
        }

        private void OnDustChanged(float newDustAmount)
        {
            if (currentMission == null) return;

            // Handle dust-based ResourceCollected missions
            if (currentMission.completionTrigger == MissionTriggerType.ResourceCollected &&
                currentMission.requiredResourceCount > 0 &&
                currentMission.resourceCounterLabel == "Dust")
            {
                int dustCount = Mathf.FloorToInt(newDustAmount);
                currentResourceCount = dustCount;

                if (enableDebugLogs) Debug.Log($"[MissionManager] Dust changed: {dustCount} / {currentMission.requiredResourceCount}");

                // Update counter UI
                if (missionUI != null)
                {
                    missionUI.UpdateCounter(currentResourceCount, currentMission.requiredResourceCount, currentMission.resourceCounterLabel);
                }

                // Check if completed
                if (currentResourceCount >= currentMission.requiredResourceCount)
                {
                    if (enableDebugLogs) Debug.Log($"[MissionManager] Dust collection complete! ({currentResourceCount}/{currentMission.requiredResourceCount})");
                    CompleteMission();
                }
            }

            // Handle ItemsSold missions that track dust and show crosshair hint when threshold reached
            if (currentMission.completionTrigger == MissionTriggerType.ItemsSold &&
                currentMission.requiredResourceCount > 0 &&
                currentMission.resourceCounterLabel == "Dust" &&
                !string.IsNullOrEmpty(currentMission.resourceThresholdCrosshairHint))
            {
                int dustCount = Mathf.FloorToInt(newDustAmount);
                currentResourceCount = dustCount;

                if (enableDebugLogs) Debug.Log($"[MissionManager] Dust for sell mission: {dustCount} / {currentMission.requiredResourceCount}");

                // Update counter UI
                if (missionUI != null)
                {
                    missionUI.UpdateCounter(currentResourceCount, currentMission.requiredResourceCount, currentMission.resourceCounterLabel);
                }

                // Show crosshair hint and marker when threshold reached
                if (currentResourceCount >= currentMission.requiredResourceCount)
                {
                    if (missionUI != null && !string.IsNullOrEmpty(currentMission.resourceThresholdCrosshairHint))
                    {
                        missionUI.ShowCrosshairHint(currentMission.resourceThresholdCrosshairHint);
                        if (enableDebugLogs) Debug.Log($"[MissionManager] Showing crosshair hint: {currentMission.resourceThresholdCrosshairHint}");
                    }

                    // Show marker when threshold reached (if showMarker is enabled)
                    // Note: showMarkerOnResourceThreshold delays the marker until threshold; if false, marker shows at mission start
                    if (currentMission.showMarker)
                    {
                        if (currentMission.showMarkerOnResourceThreshold)
                        {
                            ShowMarkerForMission(currentMission);
                        }
                    }
                }
            }
        }

        private void UpdateResourceCountFromInventory()
        {
            if (currentMission == null) return;
            if (currentMission.completionTrigger != MissionTriggerType.ResourceCollected) return;
            if (currentMission.requiredResourceCount <= 0) return;

            // Get total items in inventory
            int totalItems = GetTotalInventoryItemCount();
            currentResourceCount = totalItems;

            if (enableDebugLogs) Debug.Log($"[MissionManager] Inventory resource count: {currentResourceCount} / {currentMission.requiredResourceCount}");

            // Update counter UI
            if (missionUI != null)
            {
                missionUI.UpdateCounter(currentResourceCount, currentMission.requiredResourceCount, currentMission.resourceCounterLabel);
            }

            // Check if completed
            if (currentResourceCount >= currentMission.requiredResourceCount)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Resource collection complete! ({currentResourceCount}/{currentMission.requiredResourceCount})");
                CompleteMission();
            }
        }

        private int GetTotalInventoryItemCount()
        {
            if (inventorySystem == null)
            {
                inventorySystem = FindFirstObjectByType<InventorySystem>();
            }

            if (inventorySystem == null) return 0;

            var slots = inventorySystem.GetAllSlots();
            int total = 0;
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    total += slot.Quantity;
                }
            }
            return total;
        }

        private void OnItemsSold(int totalCredits, int totalItemCount)
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.ItemsSold)
            {
                // Hide marker immediately when sell button is clicked
                HideMarker();
                if (enableDebugLogs) Debug.Log("[MissionManager] Items sold - completing mission!");
                CompleteMission();
            }
        }

        private void OnDustSold(float dustAmount, int creditsGained)
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.ItemsSold)
            {
                // Hide marker immediately when sell button is clicked
                HideMarker();
                if (enableDebugLogs) Debug.Log("[MissionManager] Dust sold - completing mission!");
                CompleteMission();
            }
        }

        private void OnPlayerExitedWinch()
        {
            if (currentMission == null) return;

            // Hide help text if trigger is WinchExit
            if (currentMission.helpTextHideTrigger == HelpTextHideTrigger.WinchExit)
            {
                if (missionUI != null)
                {
                    missionUI.HideHelpText();
                    LogDebug("Help text hidden due to winch exit");
                }
            }

            // Hide crosshair hint if trigger is WinchExit
            if (currentMission.crosshairHintHideTrigger == CrosshairHintHideTrigger.WinchExit)
            {
                if (missionUI != null)
                {
                    missionUI.HideCrosshairHint();
                    LogDebug("Crosshair hint hidden due to winch exit");
                }
            }
        }

        private void OnCableLimitReached()
        {
            if (currentMission == null) return;

            // If mission has a secondary objective, show it when cable limit is reached
            if (currentMission.completionTrigger == MissionTriggerType.WinchUpgraded &&
                !string.IsNullOrEmpty(currentMission.secondaryObjectiveText))
            {
                if (missionUI != null)
                {
                    missionUI.UpdateObjective(currentMission.secondaryObjectiveText);
                    LogDebug($"Objective updated to secondary: {currentMission.secondaryObjectiveText}");
                }
            }
        }

        private void OnRuntimeUpgradeApplied(RuntimeUpgrade upgrade)
        {
            if (currentMission == null) return;

            // Handle WinchUpgraded mission trigger
            if (currentMission.completionTrigger == MissionTriggerType.WinchUpgraded)
            {
                // Check if this is the winch cable upgrade
                if (upgrade != null && upgrade.upgradeId == "winch_cable")
                {
                    // Store the PREVIOUS cable limit before the upgrade takes effect
                    // (The upgrade effect is applied BEFORE this event fires, so we need to calculate what it was)
                    if (WinchAnchor.Instance != null)
                    {
                        // The current tier is now the NEW tier, so previous limit was one tier lower
                        int currentTier = WinchAnchor.Instance.CurrentTierIndex;
                        if (currentTier > 0 && WinchAnchor.Instance.Config != null)
                        {
                            previousCableLimit = WinchAnchor.Instance.Config.GetMaxLength(currentTier - 1);
                        }
                        else
                        {
                            // Fallback: use a reasonable default
                            previousCableLimit = 10f;
                        }
                    }

                    if (enableDebugLogs) Debug.Log("[MissionManager] Winch cable upgraded - completing mission!");
                    CompleteMission();
                }
                else
                {
                    // Wrong upgrade - show toast if configured
                    if (!string.IsNullOrEmpty(currentMission.wrongUpgradeToastMessage) && missionUI != null)
                    {
                        missionUI.ShowCompletionToast(currentMission.wrongUpgradeToastMessage, 3f);
                        LogDebug($"Wrong upgrade toast: {currentMission.wrongUpgradeToastMessage}");
                    }
                }
            }

            // Handle AnyUpgradePurchased mission trigger (energy or tool power)
            if (currentMission.completionTrigger == MissionTriggerType.AnyUpgradePurchased)
            {
                if (upgrade != null)
                {
                    // Check if this is tool power or energy upgrade
                    if (upgrade.upgradeId == "tool_power")
                    {
                        // Hide marker immediately
                        HideMarker();

                        // Show tool power popup
                        if (!string.IsNullOrEmpty(currentMission.toolPowerUpgradePopup) && missionUI != null)
                        {
                            missionUI.ShowCenteredPopup(currentMission.toolPowerUpgradePopup, currentMission.conditionalPopupDuration);
                        }
                        if (enableDebugLogs) Debug.Log("[MissionManager] Tool power upgraded - completing mission!");
                        CompleteMission();
                    }
                    else if (upgrade.upgradeId == "energy_capacity")
                    {
                        // Hide marker immediately
                        HideMarker();

                        // Show energy popup
                        if (!string.IsNullOrEmpty(currentMission.energyUpgradePopup) && missionUI != null)
                        {
                            missionUI.ShowCenteredPopup(currentMission.energyUpgradePopup, currentMission.conditionalPopupDuration);
                        }
                        if (enableDebugLogs) Debug.Log("[MissionManager] Energy upgraded - completing mission!");
                        CompleteMission();
                    }
                }
            }
        }

        private void OnRadarPickedUp()
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.RadarPickedUp)
            {
                if (enableDebugLogs) Debug.Log("[MissionManager] Radar picked up - completing mission!");
                CompleteMission();
            }
        }

        private void OnRadarActivated()
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.RadarActivated)
            {
                if (enableDebugLogs) Debug.Log("[MissionManager] Radar activated - completing mission!");
                CompleteMission();
            }

            // Hide crosshair hint when radar is activated if configured
            if (currentMission != null &&
                currentMission.crosshairHintHideTrigger == CrosshairHintHideTrigger.RadarActivated &&
                missionUI != null)
            {
                missionUI.HideCrosshairHint();
                if (enableDebugLogs) Debug.Log("[MissionManager] Crosshair hint hidden after radar activated");
            }
        }

        private void OnTreasureChestOpened(TreasureChests.BuriedTreasureChest chest, TreasureChests.TreasureChestReward[] rewards)
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.TreasureChestOpened)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Treasure chest opened: {chest?.ChestId ?? "unknown"} - completing mission!");
                CompleteMission();
            }
        }

        private void OnRoomEntranceTriggered(RoomEntranceTrigger trigger)
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.RoomEntranceFound)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Room entrance triggered: {trigger?.EntranceId ?? "unknown"} - completing mission!");
                CompleteMission();
            }
        }

        private void OnCrystalInserted()
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.CrystalInserted)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Crystal inserted into engine - completing mission!");
                CompleteMission();
            }
        }

        private void OnJetpackPickedUp()
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.JetpackPickedUp)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Jetpack picked up - completing mission!");
                CompleteMission();
            }
        }

        private void OnDrillPikePickedUp()
        {
            if (currentMission == null) return;

            if (currentMission.completionTrigger == MissionTriggerType.DrillPikePickedUp)
            {
                if (enableDebugLogs) Debug.Log($"[MissionManager] Drill Pike picked up - completing mission!");
                CompleteMission();
            }
        }

        #endregion

        #region Rewards

        private void HandleMissionReward(MissionData mission)
        {
            switch (mission.rewardType)
            {
                case MissionRewardType.ShowTool:
                    if (HeldToolController.Instance != null)
                    {
                        HeldToolController.Instance.ShowCurrentTool();
                        LogDebug("Reward: Showing player tool");
                    }
                    break;

                case MissionRewardType.UnlockArea:
                    // Implement area unlocking if needed
                    LogDebug("Reward: Unlock area (not implemented)");
                    break;

                case MissionRewardType.GiveItem:
                    // Implement item giving if needed
                    LogDebug("Reward: Give item (not implemented)");
                    break;

                case MissionRewardType.UnlockRadar:
                    if (RadarTool.Instance != null)
                    {
                        RadarTool.Instance.UnlockRadar();
                        LogDebug("Reward: Radar unlocked");
                    }
                    break;

                case MissionRewardType.None:
                default:
                    break;
            }
        }

        #endregion

        #region Debug

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[MissionManager] {message}");
            }
        }

        [ContextMenu("Complete Current Mission")]
        public void DebugCompleteMission()
        {
            CompleteMission();
        }

        [ContextMenu("Skip All Missions")]
        public void DebugSkipAll()
        {
            currentMissionIndex = missions.Count;
            currentMission = null;
            CompletedAllMissions();
        }

        #endregion

        #region Save/Load Support

        /// <summary>
        /// Get current mission index for saving.
        /// </summary>
        public int GetCurrentMissionIndex() => currentMissionIndex;

        /// <summary>
        /// Get current dig count for saving (partial progress).
        /// </summary>
        public int GetCurrentDigCount() => currentDigCount;

        /// <summary>
        /// Get current resource count for saving (partial progress).
        /// </summary>
        public int GetCurrentResourceCount() => currentResourceCount;

        /// <summary>
        /// Set mission progress from save data.
        /// </summary>
        public void SetMissionProgress(int missionIndex, int digCount, int resourceCount)
        {
            // Restore partial progress
            currentDigCount = digCount;
            currentResourceCount = resourceCount;

            // Set mission index (subtract 1 because StartNextMission increments it)
            currentMissionIndex = missionIndex - 1;

            // Clear current mission state
            currentMission = null;
            currentActiveMarker = null;
            markerHiddenByDig = false;

            // Start the saved mission
            if (isInitialized)
            {
                StartNextMission();

                // Update UI counter if needed
                if (currentMission != null && missionUI != null)
                {
                    if (currentMission.requiredDigCount > 0)
                    {
                        missionUI.ShowCounter(currentDigCount, currentMission.requiredDigCount, "Digs");
                    }
                    else if (currentMission.requiredResourceCount > 0)
                    {
                        missionUI.ShowCounter(currentResourceCount, currentMission.requiredResourceCount, currentMission.resourceCounterLabel);
                    }
                }
            }

            if (enableDebugLogs) Debug.Log($"[MissionManager] Restored mission progress: index={missionIndex}, digs={digCount}, resources={resourceCount}");
        }

        #endregion
    }
}
