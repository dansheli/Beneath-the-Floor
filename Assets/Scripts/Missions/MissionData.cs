using UnityEngine;

namespace BeneathTheFloor.Missions
{
    /// <summary>
    /// ScriptableObject that defines a mission.
    /// Data-driven approach for easy configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "New Mission", menuName = "Beneath The Floor/Mission Data")]
    public class MissionData : ScriptableObject
    {
        [Header("Mission Info")]
        public string missionId;
        public string missionName;
        [TextArea(2, 4)]
        public string objectiveText;

        [Header("Completion")]
        [Tooltip("Event that triggers mission completion")]
        public MissionTriggerType completionTrigger;

        [Header("Marker Settings")]
        [Tooltip("Show floating diamond marker above target")]
        public bool showMarker = true;
        [Tooltip("Tag of the object to show marker above (if showMarker is true)")]
        public string markerTargetTag;
        [Tooltip("Name of the object to show marker above (alternative to tag)")]
        public string markerTargetName;

        [Header("Rewards")]
        [Tooltip("Action to perform on completion")]
        public MissionRewardType rewardType;

        [Header("Help Text")]
        [Tooltip("Secondary help text shown below objective (leave empty for none)")]
        [TextArea(2, 4)]
        public string helpText;
        [Tooltip("What hides the help text (None = stays until mission ends)")]
        public HelpTextHideTrigger helpTextHideTrigger;

        [Header("Completion Message")]
        [Tooltip("Message to show when mission completes (leave empty for none)")]
        [TextArea(2, 4)]
        public string completionMessage;
        [Tooltip("How long to show the completion message")]
        public float completionMessageDuration = 3f;

        [Header("Centered Popup")]
        [Tooltip("Centered popup message shown above crosshair on completion (leave empty for none)")]
        [TextArea(2, 4)]
        public string centeredPopupMessage;
        [Tooltip("How long to show the centered popup")]
        public float centeredPopupDuration = 4f;

        [Header("Dynamic Objective (for branching missions)")]
        [Tooltip("Secondary objective text shown after cable limit is reached")]
        [TextArea(2, 4)]
        public string secondaryObjectiveText;
        [Tooltip("Toast message when player upgrades something other than winch")]
        [TextArea(2, 4)]
        public string wrongUpgradeToastMessage;

        [Header("Dig Counter (for FirstDig missions)")]
        [Tooltip("Number of digs required to complete mission (0 = complete on first dig)")]
        public int requiredDigCount = 0;
        [Tooltip("Hide marker after first dig")]
        public bool hideMarkerOnFirstDig = false;
        [Tooltip("Popup message shown after the first dig (leave empty for none)")]
        [TextArea(2, 4)]
        public string firstDigPopupMessage;
        [Tooltip("How long to show the first dig popup")]
        public float firstDigPopupDuration = 4f;

        [Header("Resource Collection (for ResourceCollected missions)")]
        [Tooltip("Number of resources required to complete mission (0 = complete on first resource)")]
        public int requiredResourceCount = 0;
        [Tooltip("Counter label shown in UI (e.g., 'Dust' or 'Resources')")]
        public string resourceCounterLabel = "Resources";
        [Tooltip("Crosshair hint shown when resource count reaches threshold (for ItemsSold missions that track resources). E.g., 'Go Up Press F'")]
        public string resourceThresholdCrosshairHint;
        [Tooltip("Show marker only after resource threshold is reached (for ItemsSold missions)")]
        public bool showMarkerOnResourceThreshold = false;

        [Header("Conditional Popups (for AnyUpgradePurchased missions)")]
        [Tooltip("Popup shown when player upgrades tool power")]
        [TextArea(2, 4)]
        public string toolPowerUpgradePopup;
        [Tooltip("Popup shown when player upgrades energy")]
        [TextArea(2, 4)]
        public string energyUpgradePopup;
        [Tooltip("Duration for conditional popups")]
        public float conditionalPopupDuration = 4f;

        [Header("Crosshair Hint")]
        [Tooltip("Hint text shown near crosshair when mission starts (e.g., 'Press Q to activate the Scanner')")]
        public string crosshairHintText;
        [Tooltip("What hides the crosshair hint")]
        public CrosshairHintHideTrigger crosshairHintHideTrigger;

        [Header("Upgrade Locking (for WinchUpgraded missions)")]
        [Tooltip("Lock all upgrades except winch during this mission")]
        public bool lockUpgradesExceptWinch = false;

        [Header("Depth Trigger (for DepthReached missions)")]
        [Tooltip("Depth (negative Y value) to reach for completion. E.g., -20 for 20 meters deep.")]
        public float requiredDepth = 0f;

        [Header("Marker Style")]
        [Tooltip("Use subtle ring marker instead of full diamond+beam marker")]
        public bool useSubtleMarker = false;
    }

    public enum HelpTextHideTrigger
    {
        None,               // Help text stays until mission ends
        WinchExit           // Help text hides when player exits winch area
    }

    public enum CrosshairHintHideTrigger
    {
        None,               // Hint stays until mission ends
        FirstDig,           // Hint hides after first dig
        RadarActivated,     // Hint hides when radar is activated
        WinchExit           // Hint hides when player uses winch to go up
    }

    public enum MissionTriggerType
    {
        None,
        NoteRead,           // Completed when a ReadableNote is read
        ItemPickedUp,       // Completed when specific item is picked up
        LocationReached,    // Completed when player reaches a location
        FirstDig,           // Completed when player digs for the first time
        Manual,             // Completed via code call
        NodeRevealed,       // Completed when a hidden node is exposed
        ResourceCollected,  // Completed when a resource is added to inventory
        ItemsSold,          // Completed when player sells items
        WinchUpgraded,      // Completed when player upgrades the winch cable
        PastPreviousCableLimit, // Completed when player goes deeper than the old cable limit
        RadarPickedUp,      // Completed when player picks up the radar tool
        RadarActivated,     // Completed when player activates the radar for required duration
        AnyUpgradePurchased, // Completed when player purchases any upgrade (energy or tool power)
        TreasureChestOpened, // Completed when player opens a treasure chest
        DepthReached,       // Completed when player reaches a specific depth (Y position)
        RoomEntranceFound,  // Completed when player touches a room entrance trigger
        CrystalInserted,    // Completed when crystal is inserted into the engine
        JetpackPickedUp,    // Completed when player picks up the jetpack
        DrillPikePickedUp   // Completed when player picks up the drill pike (tool 4)
    }

    public enum MissionRewardType
    {
        None,
        ShowTool,           // Show the player's tool
        UnlockArea,         // Unlock a new area
        GiveItem,           // Give player an item
        UnlockRadar         // Unlock the radar tool
    }
}
