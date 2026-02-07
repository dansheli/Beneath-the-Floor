using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Economy;
using BeneathTheFloor.Tools;
using BeneathTheFloor.Winch;
using BeneathTheFloor.Managers;
using BeneathTheFloor.Machines;
using BeneathTheFloor.ResourceSystem;
using BeneathTheFloor.Missions;
using BeneathTheFloor.UI;
using BeneathTheFloor.TreasureChests;
using BeneathTheFloor.Robot;
using BeneathTheFloor.Logistics;
using BeneathTheFloor.Story;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// Central save manager that coordinates saving/loading all game systems.
    /// Supports multiple save slots with an active slot for autosaves.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool autoSaveOnQuit = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private bool enableAutoSave = true;

        [Header("Spawn Point")]
        [Tooltip("Fixed spawn point when loading a save. If null, uses saved position.")]
        [SerializeField] private Transform fixedSpawnPoint;
        [Tooltip("If true, always spawn at fixed point. If false, use saved position.")]
        [SerializeField] private bool useFixedSpawnPoint = true;
        [Tooltip("Fallback position if no spawn point is set (basement floor level)")]
        [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0f, -2.5f, 0f);

        [Header("Keybinds")]
        [SerializeField] private KeyCode quickSaveKey = KeyCode.F5;
        [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;
        [Tooltip("Set to false to disable quick save/load keybinds (F5/F9)")]
        [SerializeField] private bool enableDebugKeybinds = false;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private GameSaveData currentSave;
        private float lastAutoSaveTime;
        private float sessionStartTime;
        private bool pendingTerrainLoad = false;
        private GameObject loadingOverlay;
        private bool isPlayerFrozen = false;

        /// <summary>
        /// Index of all save slots and the active slot.
        /// </summary>
        private SavesIndex savesIndex;

        // Events
        public System.Action OnSaveStarted;
        public System.Action OnSaveCompleted;
        public System.Action OnLoadStarted;
        public System.Action OnLoadCompleted;
        public System.Action<int> OnActiveSlotChanged;

        /// <summary>
        /// Get the saves index (loads from disk if not cached).
        /// </summary>
        public SavesIndex SavesIndex
        {
            get
            {
                if (savesIndex == null)
                {
                    savesIndex = SavesIndex.Load();
                }
                return savesIndex;
            }
        }

        /// <summary>
        /// Get the current active slot ID.
        /// </summary>
        public int ActiveSlotId => SavesIndex.activeSlotId;

        /// <summary>
        /// Get the file path for the active slot.
        /// </summary>
        public string SavePath => Save.SavesIndex.GetSaveFilePath(SavesIndex.activeSlotId);

        /// <summary>
        /// Check if the active slot has a save file.
        /// </summary>
        public bool HasSaveFile => Save.SavesIndex.SaveFileExists(SavesIndex.activeSlotId);

        /// <summary>
        /// Static check for save file existence - works even if Instance is null.
        /// Returns true if ANY save slot has data.
        /// </summary>
        public static bool SaveFileExists()
        {
            // Check for new multi-save files
            for (int i = 0; i < SavesIndex.MAX_SLOTS; i++)
            {
                if (Save.SavesIndex.SaveFileExists(i))
                {
                    return true;
                }
            }

            // Check for legacy save file
            string legacyPath = Path.Combine(Application.persistentDataPath, "gamesave.json");
            return File.Exists(legacyPath);
        }

        /// <summary>
        /// Static method to delete ALL save files from all slots.
        /// Use this from MainMenu when starting a completely new game.
        /// </summary>
        public static void DeleteAllSaveFiles()
        {
            string basePath = Application.persistentDataPath;

            // Delete all slot save files
            for (int i = 0; i < SavesIndex.MAX_SLOTS; i++)
            {
                Save.SavesIndex.DeleteSaveFile(i);
            }

            // Delete saves index
            string indexPath = Path.Combine(basePath, "saves_index.json");
            if (File.Exists(indexPath))
            {
                File.Delete(indexPath);
            }

            // Delete legacy main save file
            string mainSave = Path.Combine(basePath, "gamesave.json");
            if (File.Exists(mainSave))
            {
                File.Delete(mainSave);
            }

            // Delete legacy backup
            string backupSave = mainSave + ".backup";
            if (File.Exists(backupSave))
            {
                File.Delete(backupSave);
            }

            // Delete terrain save
            string terrainSave = Path.Combine(basePath, "terrain.json");
            if (File.Exists(terrainSave))
            {
                File.Delete(terrainSave);
            }

            // Delete resource system save
            string resourceSave = Path.Combine(basePath, "resources_v3.json");
            if (File.Exists(resourceSave))
            {
                File.Delete(resourceSave);
            }

            // Delete world pickups save
            string pickupsSave = Path.Combine(basePath, "world_pickups.json");
            if (File.Exists(pickupsSave))
            {
                File.Delete(pickupsSave);
            }

            // Clear relevant PlayerPrefs
            PlayerPrefs.DeleteKey("PlayerToolTier");
            PlayerPrefs.DeleteKey("PlayerToolEquipped");
            PlayerPrefs.DeleteKey("LoadPosX");
            PlayerPrefs.DeleteKey("LoadPosY");
            PlayerPrefs.DeleteKey("LoadPosZ");
            PlayerPrefs.DeleteKey("LoadRotY");
            PlayerPrefs.Save();

        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                    transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                sessionStartTime = Time.realtimeSinceStartup;

                // Load saves index (handles legacy migration)
                savesIndex = SavesIndex.Load();

                // Register for scene load events to handle deferred terrain loading
                SceneManager.sceneLoaded += OnSceneLoaded;

                // Register for application quit - more reliable than OnApplicationQuit
                Application.wantsToQuit += OnWantsToQuit;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Application.wantsToQuit -= OnWantsToQuit;
            }
        }

        /// <summary>
        /// Called when application is about to quit. More reliable than OnApplicationQuit.
        /// Returns true to allow quit, false to cancel.
        /// </summary>
        private bool OnWantsToQuit()
        {
            if (autoSaveOnQuit && ShouldSaveOnQuit())
            {
                Debug.Log("[SaveManager] Application wants to quit - saving game...");
                bool saveSuccess = SaveGame();
                Debug.Log($"[SaveManager] Quit save {(saveSuccess ? "succeeded" : "failed")}");
            }
            return true; // Allow quit to proceed
        }

        /// <summary>
        /// Check if we should save on quit (only in gameplay scenes with valid data).
        /// </summary>
        private bool ShouldSaveOnQuit()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            // Don't save from main menu or press any key scenes
            if (currentScene.Contains("MainMenu") || currentScene.Contains("PressAnyKey"))
            {
                Debug.Log($"[SaveManager] Skipping quit save - in menu scene: {currentScene}");
                return false;
            }

            // Only save if there's a player in the scene (we're in gameplay)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.Log("[SaveManager] Skipping quit save - no player found");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Called when a scene finishes loading.
        /// Handles deferred terrain loading after scene change.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pendingTerrainLoad && currentSave != null && currentSave.hasTerrainData)
            {
                // Give terrain manager time to initialize
                StartCoroutine(DeferredTerrainLoad());
            }
        }

        private System.Collections.IEnumerator DeferredTerrainLoad()
        {
            if (debugLogs)
                Debug.Log("[SaveManager] Starting deferred load after scene change...");

            // Show loading screen / freeze player immediately
            ShowLoadingOverlay(true);
            FreezePlayer(true);

            // Wait for basic systems to initialize (HUDController, EconomySetup, etc.)
            yield return null; // Wait for Awake
            yield return null; // Wait for Start to begin
            yield return null; // Extra frame for HUD creation
            yield return new WaitForSeconds(0.2f); // Allow time for all HUD components to create their UI

            if (currentSave == null)
            {
                pendingTerrainLoad = false;
                ShowLoadingOverlay(false);
                FreezePlayer(false);
                yield break;
            }

            // IMMEDIATELY teleport player to prevent seeing wrong position
            TeleportPlayer(currentSave.player.GetPosition(), currentSave.player.GetRotation());

            // Wait for terrain manager to be ready
            float timeout = 5f;
            float waited = 0f;
            while (UndergroundTerrainManager.Instance == null || !UndergroundTerrainManager.Instance.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
                waited += 0.1f;
                if (waited >= timeout)
                {
                    Debug.LogWarning("[SaveManager] Timeout waiting for terrain manager - terrain may not load correctly");
                    break;
                }
            }

            // Load terrain FIRST (so it's ready before player sees it)
            if (currentSave.hasTerrainData)
            {
                LoadTerrainData();
                // Give terrain a moment to regenerate mesh
                yield return new WaitForSeconds(0.2f);
            }

            // Load hidden node data
            ApplyNodeData();

            // Now apply other data
            ApplyCurrencyData();
            ApplyInventoryData();
            ApplyToolData();
            ApplyUpgradeData();
            ApplyWinchData();
            ApplyEnergyData();
            ApplyProgressData();
            ApplyMissionData();
            ApplyStoryData();
            ApplyTreasureChestData();

            // Load robot data (digger robots, logistics robots)
            ApplyRobotData();

            // Load world pickups (loose resources on the ground)
            WorldPickupSaveManager.ClearExistingPickups();
            WorldPickupSaveManager.LoadWorldPickups();

            // Small delay to ensure everything is rendered
            yield return new WaitForSeconds(0.1f);

            // Refresh all HUD elements after data is applied
            RefreshAllHUDs();

            // Hide loading screen / unfreeze player
            ShowLoadingOverlay(false);
            FreezePlayer(false);

            pendingTerrainLoad = false;
            OnLoadCompleted?.Invoke();

            // Ensure game is unpaused after loading
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                GameManager.Instance.SetPause(false);
            }

            if (debugLogs)
                Debug.Log("[SaveManager] Deferred load completed after scene change");
        }

        private void Update()
        {
            // Quick save/load (disabled by default to avoid conflicts with debug tools)
            if (enableDebugKeybinds)
            {
                if (Input.GetKeyDown(quickSaveKey))
                {
                    SaveGame();
                }
                if (Input.GetKeyDown(quickLoadKey))
                {
                    LoadGame();
                }
            }

            // Auto-save (only in gameplay scenes — never on menu/loading screens)
            if (enableAutoSave && Time.realtimeSinceStartup - lastAutoSaveTime > autoSaveInterval)
            {
                if (IsInGameplayScene())
                {
                    AutoSave();
                }
                lastAutoSaveTime = Time.realtimeSinceStartup;
            }
        }

        private void OnApplicationQuit()
        {
            // Note: OnWantsToQuit is more reliable and should handle saving,
            // but we keep this as a fallback
            if (autoSaveOnQuit && ShouldSaveOnQuit() && IsInGameplayScene())
            {
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Auto-save when app is paused (mobile/alt-tab)
            if (pauseStatus && autoSaveOnQuit && IsInGameplayScene())
            {
                SaveGame();
            }
        }

        /// <summary>
        /// Returns true if the active scene is a gameplay scene (not menu/loading).
        /// Prevents autosave from overwriting good data with empty state.
        /// </summary>
        private bool IsInGameplayScene()
        {
            string scene = SceneManager.GetActiveScene().name;
            return scene == "HouseBuilding";
        }

        /// <summary>
        /// Save all game data to the currently active slot.
        /// </summary>
        public bool SaveGame(string customName = null)
        {
            return SaveGameToSlot(SavesIndex.activeSlotId, customName);
        }

        /// <summary>
        /// Save all game data to a specific slot.
        /// This also sets the slot as the new active slot.
        /// </summary>
        /// <param name="slotId">Slot ID (0 = Autosave, 1-5 = Manual)</param>
        /// <param name="saveName">Custom name for the save (optional)</param>
        /// <returns>True if save succeeded</returns>
        public bool SaveGameToSlot(int slotId, string saveName = null)
        {
            if (slotId < 0 || slotId >= SavesIndex.MAX_SLOTS)
            {
                Debug.LogError($"[SaveManager] Invalid slot ID: {slotId}");
                return false;
            }

            OnSaveStarted?.Invoke();

            currentSave = new GameSaveData();

            // Use provided name, or slot-specific default
            if (string.IsNullOrEmpty(saveName))
            {
                if (slotId == SavesIndex.AUTOSAVE_SLOT)
                {
                    saveName = "Autosave";
                }
                else
                {
                    saveName = System.DateTime.Now.ToString("MMM dd, yyyy - HH:mm");
                }
            }

            currentSave.saveName = saveName;
            currentSave.totalPlayTime = Time.realtimeSinceStartup - sessionStartTime;

            // Capture player data
            CapturePlayerData();

            // Capture economy
            CaptureCurrencyData();

            // Capture inventory
            CaptureInventoryData();

            // Capture tools & upgrades
            CaptureToolData();
            CaptureUpgradeData();

            // Capture winch
            CaptureWinchData();

            // Capture energy
            CaptureEnergyData();

            // Capture progress
            CaptureProgressData();

            // Capture mission progress
            CaptureMissionData();

            // Capture story progress
            CaptureStoryData();

            // Save terrain separately
            SaveTerrainData();

            // Save hidden node data (broken nodes, world seed)
            CaptureNodeData();

            // Save treasure chest collection state
            CaptureTreasureChestData();

            // Save robot data (digger robots, logistics robots)
            CaptureRobotData();

            // Save world pickups (loose resources on the ground)
            WorldPickupSaveManager.SaveWorldPickups();

            // Write to file for this slot
            bool success = WriteToFile(slotId);

            if (success)
            {
                // Update slot info in index
                SavesIndex.UpdateSlot(slotId, saveName, currentSave);

                // Set this slot as the active slot (autosaves now go here)
                int previousSlot = SavesIndex.activeSlotId;
                SavesIndex.SetActiveSlot(slotId);

                // Save the updated index
                SavesIndex.Save();

                // Notify listeners if active slot changed
                if (previousSlot != slotId)
                {
                    OnActiveSlotChanged?.Invoke(slotId);
                }

                OnSaveCompleted?.Invoke();
                if (debugLogs)
                    Debug.Log($"[SaveManager] Game saved to slot {slotId}: {Save.SavesIndex.GetSaveFilePath(slotId)}");
            }

            return success;
        }

        /// <summary>
        /// Create a new save in the first available slot.
        /// </summary>
        /// <param name="saveName">Custom name for the save (optional)</param>
        /// <returns>Slot ID used, or -1 if no slots available</returns>
        public int CreateNewSave(string saveName = null)
        {
            int slotId = SavesIndex.FindFirstEmptyManualSlot();

            if (slotId < 0)
            {
                Debug.LogWarning("[SaveManager] No empty save slots available");
                return -1;
            }

            if (SaveGameToSlot(slotId, saveName))
            {
                return slotId;
            }

            return -1;
        }

        /// <summary>
        /// Load game data from the currently active slot.
        /// </summary>
        public bool LoadGame()
        {
            return LoadGameFromSlot(SavesIndex.activeSlotId);
        }

        /// <summary>
        /// Load game data from a specific slot.
        /// This also sets the slot as the new active slot.
        /// </summary>
        /// <param name="slotId">Slot ID to load from</param>
        /// <returns>True if load succeeded</returns>
        public bool LoadGameFromSlot(int slotId)
        {
            if (slotId < 0 || slotId >= SavesIndex.MAX_SLOTS)
            {
                Debug.LogError($"[SaveManager] Invalid slot ID: {slotId}");
                return false;
            }

            if (!Save.SavesIndex.SaveFileExists(slotId))
            {
                if (debugLogs)
                    Debug.LogWarning($"[SaveManager] No save file found for slot {slotId}!");
                return false;
            }

            OnLoadStarted?.Invoke();

            if (!ReadFromFile(slotId))
            {
                return false;
            }

            // Set this slot as the active slot (autosaves now go here)
            int previousSlot = SavesIndex.activeSlotId;
            SavesIndex.SetActiveSlot(slotId);
            SavesIndex.Save();

            // Notify listeners if active slot changed
            if (previousSlot != slotId)
            {
                OnActiveSlotChanged?.Invoke(slotId);
            }

            // Check if we need a scene change before applying data
            bool sceneChangeRequired = NeedsSceneChange();

            // Apply data to systems
            ApplyPlayerData();

            // If scene change is happening, defer the rest until after scene loads
            if (sceneChangeRequired)
            {
                if (debugLogs)
                    Debug.Log("[SaveManager] Scene change required - deferring remaining load until scene is ready");
                return true;
            }

            // Same scene - apply remaining data immediately
            ApplyCurrencyData();
            ApplyInventoryData();
            ApplyToolData();
            ApplyUpgradeData();
            ApplyWinchData();
            ApplyEnergyData();
            ApplyProgressData();
            ApplyMissionData();
            ApplyStoryData();
            ApplyTreasureChestData();

            // Load terrain
            LoadTerrainData();

            // Load hidden node data (broken nodes, world seed)
            ApplyNodeData();

            // Load robot data (digger robots, logistics robots)
            ApplyRobotData();

            // Load world pickups (loose resources on the ground)
            // First clear existing pickups to prevent duplicates
            WorldPickupSaveManager.ClearExistingPickups();
            WorldPickupSaveManager.LoadWorldPickups();

            // Refresh HUD elements after data is applied
            RefreshAllHUDs();

            OnLoadCompleted?.Invoke();
            if (debugLogs)
                Debug.Log($"[SaveManager] Game loaded from slot {slotId}: {Save.SavesIndex.GetSaveFilePath(slotId)}");

            return true;
        }

        /// <summary>
        /// Auto-save to the currently active slot (silent).
        /// Preserves the existing save name.
        /// </summary>
        private void AutoSave()
        {
            if (debugLogs)
                Debug.Log($"[SaveManager] Auto-saving to active slot {SavesIndex.activeSlotId}...");

            // Get existing save name from the active slot
            var slotInfo = SavesIndex.GetActiveSlot();
            string saveName = slotInfo?.saveName;

            // If no name exists, use "Autosave" for slot 0, or timestamp for others
            if (string.IsNullOrEmpty(saveName))
            {
                saveName = SavesIndex.activeSlotId == SavesIndex.AUTOSAVE_SLOT ? "Autosave" : null;
            }

            SaveGameToSlot(SavesIndex.activeSlotId, saveName);
        }

        /// <summary>
        /// Delete the active slot's save file and reset save-related data in memory.
        /// </summary>
        public bool DeleteSave()
        {
            return DeleteSaveSlot(SavesIndex.activeSlotId);
        }

        /// <summary>
        /// Delete a specific save slot.
        /// </summary>
        /// <param name="slotId">Slot ID to delete</param>
        /// <returns>True if deletion succeeded</returns>
        public bool DeleteSaveSlot(int slotId)
        {
            if (slotId < 0 || slotId >= SavesIndex.MAX_SLOTS)
            {
                Debug.LogError($"[SaveManager] Invalid slot ID: {slotId}");
                return false;
            }

            try
            {
                // Delete the save file for this slot
                Save.SavesIndex.DeleteSaveFile(slotId);

                // Clear the slot in the index
                SavesIndex.ClearSlot(slotId);
                SavesIndex.Save();

                // If we deleted the active slot, reset to autosave slot
                if (SavesIndex.activeSlotId == slotId)
                {
                    SavesIndex.SetActiveSlot(SavesIndex.AUTOSAVE_SLOT);
                    SavesIndex.Save();
                    OnActiveSlotChanged?.Invoke(SavesIndex.AUTOSAVE_SLOT);
                }

                if (debugLogs)
                    Debug.Log($"[SaveManager] Deleted save slot {slotId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to delete slot {slotId}: {e.Message}");
                return false;
            }

            // Clear current save data in memory if it was from the deleted slot
            currentSave = null;

            // Reset pending terrain load flag to prevent deferred load with stale data
            pendingTerrainLoad = false;

            return true;
        }

        /// <summary>
        /// Delete ALL save data and reset to fresh state.
        /// Use this when starting a completely new game.
        /// </summary>
        public bool DeleteAllSaves()
        {
            try
            {
                // Delete all slot save files
                for (int i = 0; i < SavesIndex.MAX_SLOTS; i++)
                {
                    Save.SavesIndex.DeleteSaveFile(i);
                }

                // Reset the saves index
                savesIndex = new SavesIndex();
                savesIndex.Save();

                // Delete terrain saves
                DiggingSaveManager.DeleteSaveFile();

                // Delete resource system save (nodes, dust)
                if (ResourceSystem.ResourceSaveManager.Instance != null)
                {
                    ResourceSystem.ResourceSaveManager.Instance.DeleteSave();
                }

                // Delete world pickups save
                WorldPickupSaveManager.DeleteSave();

                // Clear hidden node data
                if (HiddenNodeManager.Instance != null)
                {
                    HiddenNodeManager.Instance.ForceClearAllSaveData();
                    if (debugLogs)
                        Debug.Log("[SaveManager] Hidden node data cleared");
                }

                // Clear current save data in memory
                currentSave = null;
                pendingTerrainLoad = false;

                OnActiveSlotChanged?.Invoke(SavesIndex.AUTOSAVE_SLOT);

                if (debugLogs)
                    Debug.Log("[SaveManager] All save data deleted for new game");

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to delete all saves: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set the active save slot (for autosaves).
        /// </summary>
        public void SetActiveSlot(int slotId)
        {
            if (slotId < 0 || slotId >= SavesIndex.MAX_SLOTS)
            {
                Debug.LogError($"[SaveManager] Invalid slot ID: {slotId}");
                return;
            }

            int previousSlot = SavesIndex.activeSlotId;
            SavesIndex.SetActiveSlot(slotId);
            SavesIndex.Save();

            if (previousSlot != slotId)
            {
                OnActiveSlotChanged?.Invoke(slotId);
            }
        }

        /// <summary>
        /// Get information about all save slots.
        /// </summary>
        public List<SaveSlotInfo> GetAllSlots()
        {
            return SavesIndex.slots;
        }

        /// <summary>
        /// Get information about a specific slot.
        /// </summary>
        public SaveSlotInfo GetSlotInfo(int slotId)
        {
            return SavesIndex.GetSlot(slotId);
        }

        /// <summary>
        /// Check if a slot has save data.
        /// </summary>
        public bool SlotHasData(int slotId)
        {
            var slot = SavesIndex.GetSlot(slotId);
            return slot != null && slot.hasData;
        }

        #region Capture Methods

        private void CapturePlayerData()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                currentSave.player.SetPosition(player.transform.position);
                currentSave.player.SetRotation(player.transform.eulerAngles.y);
                currentSave.player.currentScene = SceneManager.GetActiveScene().name;
            }
        }

        private void CaptureCurrencyData()
        {
            if (CurrencyManager.Instance != null)
            {
                currentSave.currency = CurrencyManager.Instance.CurrentCurrency;
                currentSave.totalEarned = CurrencyManager.Instance.TotalEarned;
            }
        }

        private void CaptureInventoryData()
        {
            if (InventorySystem.Instance != null)
            {
                currentSave.inventoryItems.Clear();
                var slots = InventorySystem.Instance.GetAllSlots();

                if (debugLogs)
                    Debug.Log($"[SaveManager] CaptureInventoryData: Found {slots.Count} slots");

                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot != null && !slot.IsEmpty)
                    {
                        string itemId = slot.ItemData?.itemId ?? "";
                        string itemName = slot.ItemData?.itemName ?? "";
                        int quantity = slot.Quantity;

                        if (debugLogs)
                            Debug.Log($"[SaveManager] Saving slot {i}: {itemName} x{quantity} (id={itemId})");

                        currentSave.inventoryItems.Add(new InventoryItemSaveData
                        {
                            itemId = itemId,
                            itemName = itemName,
                            quantity = quantity,
                            slotIndex = i
                        });
                    }
                }

                if (debugLogs)
                    Debug.Log($"[SaveManager] Total items saved: {currentSave.inventoryItems.Count}");
            }
            else
            {
                Debug.LogWarning("[SaveManager] InventorySystem.Instance is null - cannot save inventory!");
            }
        }

        private void CaptureToolData()
        {
            // Get tool tier from multiple sources to ensure we capture the correct value
            int upgradeTier = 1;
            int prefsTier = PlayerPrefs.GetInt("PlayerToolTier", 1);
            int heldTier = 1;

            if (Upgrades.UpgradeSystem.Instance != null)
            {
                upgradeTier = Upgrades.UpgradeSystem.Instance.CurrentToolTier;
            }

            if (HeldToolController.Instance != null)
            {
                heldTier = HeldToolController.Instance.GetCurrentTier();
            }

            // Use the highest tier found (in case of desync)
            currentSave.currentToolTier = Mathf.Max(upgradeTier, prefsTier, heldTier);

            if (debugLogs)
                Debug.Log($"[SaveManager] Captured tool tier: {currentSave.currentToolTier} (upgrade={upgradeTier}, prefs={prefsTier}, held={heldTier})");

            if (HeldToolController.Instance != null)
            {
                currentSave.hasToolEquipped = HeldToolController.Instance.AreToolsVisible();
                currentSave.currentToolIndex = HeldToolController.Instance.GetCurrentToolIndex();
            }

            // Save to PlayerPrefs for HeldToolController to read on Start()
            // This ensures tool state persists even if HeldToolController.Start() runs before ApplyToolData()
            PlayerPrefs.SetInt("PlayerToolTier", currentSave.currentToolTier);
            PlayerPrefs.SetInt("PlayerToolIndex", currentSave.currentToolIndex);
            PlayerPrefs.SetInt("PlayerToolEquipped", currentSave.hasToolEquipped ? 1 : 0);
            PlayerPrefs.Save();

            if (InventorySystem.Instance != null)
            {
                currentSave.ownedTools.Clear();
                foreach (var tool in InventorySystem.Instance.OwnedTools)
                {
                    currentSave.ownedTools.Add(new OwnedToolSaveData
                    {
                        toolName = tool.toolName,
                        tier = tool.tier,
                        digSpeed = tool.digSpeed,
                        durability = tool.durability,
                        maxDurability = tool.maxDurability,
                        maxDepth = tool.maxDepth,
                        isEquipped = InventorySystem.Instance.CurrentTool == tool
                    });
                }
            }
        }

        private void CaptureUpgradeData()
        {
            if (Upgrades.UpgradeSystem.Instance != null)
            {
                currentSave.unlockedUpgrades = new List<string>(
                    Upgrades.UpgradeSystem.Instance.GetUnlockedUpgradeIds()
                );
            }
        }

        private void CaptureWinchData()
        {
            if (WinchAnchor.Instance != null)
            {
                currentSave.winchTier = WinchAnchor.Instance.CurrentTierIndex;
            }
        }

        private void CaptureEnergyData()
        {
            if (Energy.EnergyManager.Instance != null)
            {
                currentSave.currentEnergy = Energy.EnergyManager.Instance.CurrentEnergy;
                currentSave.maxEnergy = Energy.EnergyManager.Instance.MaxEnergy;
                currentSave.drinkCount = Energy.EnergyManager.Instance.DrinkCount;
                currentSave.energyUpgradeLevel = Energy.EnergyManager.Instance.EnergyUpgradeLevel;
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured energy: {currentSave.currentEnergy}/{currentSave.maxEnergy}, drinks: {currentSave.drinkCount}, upgradeLevel: {currentSave.energyUpgradeLevel}");
            }

            // Capture lamps from LampPlacementController (available to place)
            var lampController = Object.FindObjectOfType<Lighting.LampPlacementController>();
            if (lampController != null)
            {
                currentSave.lampsAvailable = lampController.LampsAvailable;
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured lamps available: {currentSave.lampsAvailable}");
            }

            // Capture placed lamp positions from UndergroundLightingSystem
            if (Lighting.UndergroundLightingSystem.Instance != null)
            {
                currentSave.placedLamps.Clear();
                var lampData = Lighting.UndergroundLightingSystem.Instance.GetPlacedLampData();
                foreach (var (position, rotation) in lampData)
                {
                    var lampSaveData = new PlacedLampSaveData();
                    lampSaveData.SetPosition(position);
                    lampSaveData.SetRotation(rotation);
                    currentSave.placedLamps.Add(lampSaveData);
                }
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured {currentSave.placedLamps.Count} placed lamps");
            }

            // Capture inventory upgrade level
            if (Inventory.InventorySystem.Instance != null)
            {
                currentSave.inventoryUpgradeLevel = Inventory.InventorySystem.Instance.InventoryUpgradeLevel;
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured inventory upgrade level: {currentSave.inventoryUpgradeLevel}");
            }

            // Capture headlamp upgrade level from UpgradeStation
            if (UpgradeStation.Instance != null)
            {
                currentSave.headlampUpgradeLevel = UpgradeStation.Instance.GetRuntimeUpgradeLevel("headlamp");
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured headlamp upgrade level: {currentSave.headlampUpgradeLevel}");
            }

            // Capture radar unlock state
            if (Tools.RadarTool.Instance != null)
            {
                currentSave.hasRadarUnlocked = Tools.RadarTool.Instance.IsUnlocked;
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured radar unlocked: {currentSave.hasRadarUnlocked}");
            }

            // Capture jetpack state
            var jetpack = Object.FindObjectOfType<Player.JetpackController>();
            if (jetpack != null)
            {
                currentSave.hasJetpack = jetpack.HasJetpack;
            }
        }

        private void CaptureTreasureChestData()
        {
            if (TreasureChestManager.Instance != null)
            {
                var chestData = TreasureChestManager.Instance.GetSaveData();
                if (chestData?.openedChestIds != null)
                {
                    currentSave.openedChestIds = new List<string>(chestData.openedChestIds);
                    if (debugLogs)
                        Debug.Log($"[SaveManager] Captured {currentSave.openedChestIds.Count} opened treasure chests");
                }
            }
        }

        private void CaptureProgressData()
        {
            if (Managers.GameManager.Instance != null)
            {
                currentSave.maxDepthReached = Managers.GameManager.Instance.MaxDepthReached;
                currentSave.currentDepth = Managers.GameManager.Instance.CurrentDepth;
            }
        }

        private void CaptureMissionData()
        {
            if (MissionManager.Instance != null)
            {
                currentSave.currentMissionIndex = MissionManager.Instance.GetCurrentMissionIndex();
                currentSave.missionDigCount = MissionManager.Instance.GetCurrentDigCount();
                currentSave.missionResourceCount = MissionManager.Instance.GetCurrentResourceCount();
                if (debugLogs)
                    Debug.Log($"[SaveManager] Saved mission: index={currentSave.currentMissionIndex}, digs={currentSave.missionDigCount}, resources={currentSave.missionResourceCount}");
            }
        }

        private void CaptureStoryData()
        {
            if (StoryManager.Instance != null)
            {
                currentSave.storyChapter = StoryManager.Instance.CurrentChapter;
                currentSave.discoveredStoryItemIds = StoryManager.Instance.GetDiscoveredItemIds();
                if (debugLogs)
                    Debug.Log($"[SaveManager] Saved story: chapter={currentSave.storyChapter}, items={currentSave.discoveredStoryItemIds.Count}");
            }
        }

        private void SaveTerrainData()
        {
            // Try V3 first (ChunkManager)
            var chunkManager = Object.FindObjectOfType<ChunkManager>();
            if (chunkManager != null)
            {
                DiggingSaveManager.OnGameSave();
                currentSave.hasTerrainData = true;
                if (debugLogs)
                    Debug.Log("[SaveManager] Saved V3 terrain data");
            }
            // Fall back to V2 (UndergroundTerrainManager)
            else if (UndergroundTerrainManager.Instance != null && UndergroundTerrainManager.Instance.IsInitialized)
            {
                DiggingSaveManager.OnGameSave();
                currentSave.hasTerrainData = true;
                if (debugLogs)
                    Debug.Log("[SaveManager] Saved V2 terrain data");
            }
            else
            {
                currentSave.hasTerrainData = false;
            }
        }

        private void CaptureNodeData()
        {
            if (HiddenNodeManager.Instance != null)
            {
                currentSave.nodeWorldSeed = HiddenNodeManager.Instance.GetWorldSeed();
                currentSave.nodeData = HiddenNodeManager.Instance.GetSaveData();
                if (debugLogs)
                    Debug.Log($"[SaveManager] Saved node data: seed={currentSave.nodeWorldSeed}, chunks={currentSave.nodeData.Count}");
            }
        }

        private void CaptureRobotData()
        {
            // Capture Digger Robots
            currentSave.diggerRobots.Clear();
            var diggerRobots = Object.FindObjectsOfType<DiggerRobotStateMachine>();
            foreach (var robot in diggerRobots)
            {
                var data = new DiggerRobotSaveData();
                data.state = robot.CurrentState.ToString();
                data.batteryRatio = robot.BatteryRatio;
                data.SetPosition(robot.transform.position);
                data.rotY = robot.transform.eulerAngles.y;

                // Get resume point data via reflection (private fields)
                var hasResumeField = typeof(DiggerRobotStateMachine).GetField("hasResumePoint",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var resumePosField = typeof(DiggerRobotStateMachine).GetField("savedResumePos",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var resumeYawField = typeof(DiggerRobotStateMachine).GetField("savedResumeYaw",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (hasResumeField != null)
                    data.hasResumePoint = (bool)hasResumeField.GetValue(robot);
                if (resumePosField != null)
                    data.SetResumePosition((Vector3)resumePosField.GetValue(robot));
                if (resumeYawField != null)
                    data.resumeYaw = (float)resumeYawField.GetValue(robot);

                currentSave.diggerRobots.Add(data);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured digger robot: state={data.state}, battery={data.batteryRatio:P0}");
            }

            // Capture Logistics Robots
            currentSave.logisticsRobots.Clear();
            var logisticsRobots = Object.FindObjectsOfType<LogisticsRobotController>();
            foreach (var robot in logisticsRobots)
            {
                var data = new LogisticsRobotSaveData();
                data.state = robot.CurrentState.ToString();
                data.batteryRatio = robot.BatteryRatio;
                data.SetPosition(robot.transform.position);
                data.rotY = robot.transform.eulerAngles.y;
                data.activeMode = robot.ActiveMode.ToString();

                // Capture cargo
                var cargo = robot.GetCargo();
                if (cargo != null)
                {
                    var cargoList = cargo.GetAllCargo();
                    foreach (var entry in cargoList)
                    {
                        data.cargoContents.Add(new CargoEntrySaveData
                        {
                            resourceId = entry.resourceId,
                            tier = entry.tier,
                            quantity = entry.quantity,
                            creditValuePerUnit = entry.creditValuePerUnit
                        });
                    }
                }

                currentSave.logisticsRobots.Add(data);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Captured logistics robot: state={data.state}, mode={data.activeMode}, battery={data.batteryRatio:P0}, cargo={data.cargoContents.Count} types");
            }

            if (debugLogs)
                Debug.Log($"[SaveManager] Captured {currentSave.diggerRobots.Count} digger robots, {currentSave.logisticsRobots.Count} logistics robots");
        }

        #endregion

        #region Apply Methods

        /// <summary>
        /// Check if we need to change scenes for load.
        /// Returns true if scene change is required.
        /// </summary>
        private bool NeedsSceneChange()
        {
            if (currentSave == null || string.IsNullOrEmpty(currentSave.player.currentScene))
                return false;

            return SceneManager.GetActiveScene().name != currentSave.player.currentScene;
        }

        private void ApplyPlayerData()
        {
            if (string.IsNullOrEmpty(currentSave.player.currentScene))
                return;

            // ALWAYS store tool data to PlayerPrefs - even if same scene
            // This ensures HeldToolController can read the correct state on Start()
            PlayerPrefs.SetInt("PlayerToolTier", currentSave.currentToolTier);
            PlayerPrefs.SetInt("PlayerToolIndex", currentSave.currentToolIndex);
            PlayerPrefs.SetInt("PlayerToolEquipped", currentSave.hasToolEquipped ? 1 : 0);
            PlayerPrefs.Save();

            // If we're in a different scene, we need to load that scene first
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != currentSave.player.currentScene)
            {
                // Store position for after scene load
                PlayerPrefs.SetFloat("LoadPosX", currentSave.player.posX);
                PlayerPrefs.SetFloat("LoadPosY", currentSave.player.posY);
                PlayerPrefs.SetFloat("LoadPosZ", currentSave.player.posZ);
                PlayerPrefs.SetFloat("LoadRotY", currentSave.player.rotY);
                PlayerPrefs.SetString("LoadScene", currentSave.player.currentScene);

                // Mark terrain load as pending - will be loaded after scene change
                pendingTerrainLoad = true;

                SceneManager.LoadScene(currentSave.player.currentScene);
                return;
            }

            // Same scene - teleport player and apply tool state directly
            TeleportPlayer(currentSave.player.GetPosition(), currentSave.player.GetRotation());

            // Apply tool state directly since we're not reloading the scene
            if (HeldToolController.Instance != null)
            {
                int toolIndex = currentSave.currentToolIndex;
                if (toolIndex == 0 && UpgradeStation.Instance != null)
                {
                    int tierLevel = UpgradeStation.Instance.GetRuntimeUpgradeLevel("tool_tier");
                    if (tierLevel > 0)
                        toolIndex = Mathf.Clamp(tierLevel, 0, 4);
                }
                // Check if Sonic Pulser was purchased (use PlayerPrefs directly -
                // RuntimeUpgrades may not be loaded yet when ApplyToolData runs before ApplyUpgradeData)
                if (PlayerPrefs.GetInt("RuntimeUpgrade_sonic_pulser", 0) >= 1)
                    toolIndex = 4;
                HeldToolController.Instance.SetActiveToolAndTier(toolIndex, currentSave.currentToolTier);
                if (currentSave.hasToolEquipped)
                {
                    HeldToolController.Instance.ShowCurrentTool();
                }
                else
                {
                    HeldToolController.Instance.HideAllTools();
                }
            }
        }

        private void TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            // Determine spawn position
            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (useFixedSpawnPoint)
            {
                if (fixedSpawnPoint != null)
                {
                    spawnPosition = fixedSpawnPoint.position;
                    spawnRotation = fixedSpawnPoint.rotation;
                }
                else
                {
                    // Use default fallback position
                    spawnPosition = defaultSpawnPosition;
                    spawnRotation = Quaternion.identity;
                }
            }
            else
            {
                // Use saved position
                spawnPosition = position;
                spawnRotation = rotation;
            }

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = spawnPosition;
            player.transform.rotation = spawnRotation;

            if (cc != null) cc.enabled = true;

            if (debugLogs)
                Debug.Log($"[SaveManager] Teleported player to {spawnPosition} (useFixedSpawn={useFixedSpawnPoint})");
        }

        private void ApplyCurrencyData()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.SetCurrency(currentSave.currency);
            }
        }

        private void ApplyInventoryData()
        {
            if (InventorySystem.Instance != null)
            {
                if (debugLogs)
                    Debug.Log($"[SaveManager] ApplyInventoryData: Loading {currentSave.inventoryItems.Count} items");

                InventorySystem.Instance.ClearInventory();

                foreach (var itemData in currentSave.inventoryItems)
                {
                    // Find item definition and add to inventory
                    var itemDef = InventorySystem.Instance.FindItemById(itemData.itemId);
                    if (itemDef != null)
                    {
                        bool success = InventorySystem.Instance.AddItemToSlot(itemDef, itemData.quantity, itemData.slotIndex);
                        if (debugLogs)
                            Debug.Log($"[SaveManager] Loaded slot {itemData.slotIndex}: {itemData.itemName} x{itemData.quantity} (success={success})");
                    }
                    else
                    {
                        Debug.LogWarning($"[SaveManager] Could not find item definition for '{itemData.itemId}' ({itemData.itemName})");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[SaveManager] InventorySystem.Instance is null - cannot load inventory!");
            }
        }

        private void ApplyToolData()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.ClearTools();

                foreach (var toolData in currentSave.ownedTools)
                {
                    var tool = new ToolData
                    {
                        toolName = toolData.toolName,
                        tier = toolData.tier,
                        digSpeed = toolData.digSpeed,
                        durability = toolData.durability,
                        maxDurability = toolData.maxDurability,
                        maxDepth = toolData.maxDepth
                    };
                    InventorySystem.Instance.AddTool(tool);

                    if (toolData.isEquipped)
                    {
                        InventorySystem.Instance.EquipTool(tool);
                    }
                }
            }

            // Apply tool tier to UpgradeSystem (authoritative source)
            if (Upgrades.UpgradeSystem.Instance != null && currentSave.currentToolTier > 0)
            {
                Upgrades.UpgradeSystem.Instance.SetToolTier(currentSave.currentToolTier);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied tool tier to UpgradeSystem: {currentSave.currentToolTier}");
            }

            if (HeldToolController.Instance != null)
            {
                // Backward compat: old saves have currentToolIndex=0 even for higher tools.
                // Infer from tool_tier upgrade level if available.
                int toolIndex = currentSave.currentToolIndex;
                if (toolIndex == 0 && UpgradeStation.Instance != null)
                {
                    int tierLevel = UpgradeStation.Instance.GetRuntimeUpgradeLevel("tool_tier");
                    if (tierLevel > 0)
                        toolIndex = Mathf.Clamp(tierLevel, 0, 4);
                }
                // Check if Sonic Pulser was purchased (use PlayerPrefs directly -
                // RuntimeUpgrades may not be loaded yet when ApplyToolData runs before ApplyUpgradeData)
                if (PlayerPrefs.GetInt("RuntimeUpgrade_sonic_pulser", 0) >= 1)
                    toolIndex = 4;

                if (debugLogs)
                    Debug.Log($"[SaveManager] Setting HeldToolController tool={toolIndex}, tier={currentSave.currentToolTier}, equipped={currentSave.hasToolEquipped}");

                HeldToolController.Instance.SetActiveToolAndTier(toolIndex, currentSave.currentToolTier);

                // Restore tool visibility state
                if (currentSave.hasToolEquipped)
                {
                    HeldToolController.Instance.ShowCurrentTool();
                }
                else
                {
                    HeldToolController.Instance.HideAllTools();
                }
            }
        }

        private void ApplyUpgradeData()
        {
            // NOTE: We do NOT call SetUnlockedUpgrades here because it resets the tool tier.
            // The tool tier is already set directly in ApplyToolData() using currentSave.currentToolTier.
            // SetUnlockedUpgrades calculates tier from upgrade names, which is unreliable.

            // Just reload UpgradeStation progress (tools, energy capacity, etc.)
            if (UpgradeStation.Instance != null)
            {
                UpgradeStation.Instance.ReloadUpgradeProgress();
            }
        }

        private void ApplyWinchData()
        {
            if (WinchAnchor.Instance != null)
            {
                WinchAnchor.Instance.SetTier(currentSave.winchTier);
            }
        }

        private void ApplyEnergyData()
        {
            if (Energy.EnergyManager.Instance != null)
            {
                // Restore energy upgrade level FIRST (affects max energy)
                if (currentSave.energyUpgradeLevel > 0)
                {
                    Energy.EnergyManager.Instance.SetEnergyUpgradeLevel(currentSave.energyUpgradeLevel);
                    if (debugLogs)
                        Debug.Log($"[SaveManager] Applied energy upgrade level: {currentSave.energyUpgradeLevel}");
                }

                Energy.EnergyManager.Instance.SetEnergy(currentSave.currentEnergy);

                // Restore drink count
                Energy.EnergyManager.Instance.SetDrinkCount(currentSave.drinkCount);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied drink count: {currentSave.drinkCount}");
            }

            // Restore available lamps (in inventory)
            var lampController = Object.FindObjectOfType<Lighting.LampPlacementController>();
            if (lampController != null)
            {
                lampController.SetLampCount(currentSave.lampsAvailable);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied lamps available: {currentSave.lampsAvailable}");
            }

            // Restore placed lamps in the world
            if (Lighting.UndergroundLightingSystem.Instance != null && currentSave.placedLamps != null && currentSave.placedLamps.Count > 0)
            {
                var lampData = new List<(Vector3, Quaternion)>();
                foreach (var lamp in currentSave.placedLamps)
                {
                    lampData.Add((lamp.GetPosition(), lamp.GetRotation()));
                }
                Lighting.UndergroundLightingSystem.Instance.RestorePlacedLamps(lampData);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Restored {lampData.Count} placed lamps");
            }

            // Restore inventory upgrade level
            if (Inventory.InventorySystem.Instance != null && currentSave.inventoryUpgradeLevel > 0)
            {
                Inventory.InventorySystem.Instance.SetUpgradeLevel(currentSave.inventoryUpgradeLevel);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied inventory upgrade level: {currentSave.inventoryUpgradeLevel}");
            }

            // Restore headlamp upgrade level
            if (UpgradeStation.Instance != null && currentSave.headlampUpgradeLevel > 0)
            {
                UpgradeStation.Instance.SetRuntimeUpgradeLevel("headlamp", currentSave.headlampUpgradeLevel);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied headlamp upgrade level: {currentSave.headlampUpgradeLevel}");
            }

            // Restore radar unlock state
            if (Tools.RadarTool.Instance != null && currentSave.hasRadarUnlocked)
            {
                Tools.RadarTool.Instance.UnlockRadar();
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied radar unlocked: {currentSave.hasRadarUnlocked}");
            }

            // Restore jetpack state
            if (currentSave.hasJetpack)
            {
                var jetpack = Object.FindObjectOfType<Player.JetpackController>();
                if (jetpack != null)
                {
                    jetpack.PickupJetpack();
                }
            }
        }

        private void ApplyTreasureChestData()
        {
            if (TreasureChestManager.Instance != null && currentSave.openedChestIds != null)
            {
                var chestData = new TreasureChestSaveData
                {
                    openedChestIds = currentSave.openedChestIds.ToArray()
                };
                TreasureChestManager.Instance.LoadSaveData(chestData);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Applied {currentSave.openedChestIds.Count} opened treasure chests");
            }
        }

        private void ApplyProgressData()
        {
            // Progress is managed by GameManager and will be loaded from PlayerPrefs
        }

        private void ApplyMissionData()
        {
            if (MissionManager.Instance != null && currentSave.currentMissionIndex >= 0)
            {
                MissionManager.Instance.SetMissionProgress(
                    currentSave.currentMissionIndex,
                    currentSave.missionDigCount,
                    currentSave.missionResourceCount);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Loaded mission: index={currentSave.currentMissionIndex}, digs={currentSave.missionDigCount}, resources={currentSave.missionResourceCount}");
            }
        }

        private void ApplyStoryData()
        {
            if (StoryManager.Instance != null)
            {
                StoryManager.Instance.SetProgress(
                    currentSave.storyChapter,
                    currentSave.discoveredStoryItemIds);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Loaded story: chapter={currentSave.storyChapter}, items={currentSave.discoveredStoryItemIds?.Count ?? 0}");
            }
        }

        private void LoadTerrainData()
        {
            if (currentSave.hasTerrainData)
            {
                // Try V3 first (ChunkManager)
                var chunkManager = Object.FindObjectOfType<ChunkManager>();
                if (chunkManager != null)
                {
                    DiggingSaveManager.OnGameLoad();
                    if (debugLogs)
                        Debug.Log("[SaveManager] Loaded V3 terrain data");
                }
                // Fall back to V2 (UndergroundTerrainManager)
                else if (UndergroundTerrainManager.Instance != null)
                {
                    DiggingSaveManager.OnGameLoad();
                    if (debugLogs)
                        Debug.Log("[SaveManager] Loaded V2 terrain data");
                }
            }
        }

        private void ApplyNodeData()
        {
            if (HiddenNodeManager.Instance != null && currentSave.nodeData != null)
            {
                HiddenNodeManager.Instance.LoadSaveData(currentSave.nodeData, currentSave.nodeWorldSeed);
                if (debugLogs)
                    Debug.Log($"[SaveManager] Loaded node data: seed={currentSave.nodeWorldSeed}, chunks={currentSave.nodeData.Count}");
            }
        }

        private void ApplyRobotData()
        {
            // Apply Digger Robot data
            if (currentSave.diggerRobots != null && currentSave.diggerRobots.Count > 0)
            {
                var diggerRobots = Object.FindObjectsOfType<DiggerRobotStateMachine>();
                for (int i = 0; i < Mathf.Min(currentSave.diggerRobots.Count, diggerRobots.Length); i++)
                {
                    var data = currentSave.diggerRobots[i];
                    var robot = diggerRobots[i];

                    // Set position and rotation
                    robot.transform.position = data.GetPosition();
                    robot.transform.rotation = Quaternion.Euler(0f, data.rotY, 0f);

                    // Set battery via reflection
                    var batteryComponent = robot.GetComponent<DiggerRobotBattery>();
                    if (batteryComponent != null)
                    {
                        var ratioField = typeof(DiggerRobotBattery).GetField("current",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var maxField = typeof(DiggerRobotBattery).GetField("max",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (ratioField != null && maxField != null)
                        {
                            float max = (float)maxField.GetValue(batteryComponent);
                            ratioField.SetValue(batteryComponent, data.batteryRatio * max);
                        }
                    }

                    // Set resume point data via reflection
                    var hasResumeField = typeof(DiggerRobotStateMachine).GetField("hasResumePoint",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var resumePosField = typeof(DiggerRobotStateMachine).GetField("savedResumePos",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var resumeYawField = typeof(DiggerRobotStateMachine).GetField("savedResumeYaw",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (hasResumeField != null)
                        hasResumeField.SetValue(robot, data.hasResumePoint);
                    if (resumePosField != null)
                        resumePosField.SetValue(robot, data.GetResumePosition());
                    if (resumeYawField != null)
                        resumeYawField.SetValue(robot, data.resumeYaw);

                    // For now, always restore to Docked state for safety
                    // Complex mid-operation states are hard to restore correctly
                    // The robot will auto-redeploy if it was active before
                    if (debugLogs)
                        Debug.Log($"[SaveManager] Restored digger robot: state={data.state}, battery={data.batteryRatio:P0}");
                }
            }

            // Apply Logistics Robot data
            if (currentSave.logisticsRobots != null && currentSave.logisticsRobots.Count > 0)
            {
                var logisticsRobots = Object.FindObjectsOfType<LogisticsRobotController>();
                for (int i = 0; i < Mathf.Min(currentSave.logisticsRobots.Count, logisticsRobots.Length); i++)
                {
                    var data = currentSave.logisticsRobots[i];
                    var robot = logisticsRobots[i];

                    // Set position and rotation
                    robot.transform.position = data.GetPosition();
                    robot.transform.rotation = Quaternion.Euler(0f, data.rotY, 0f);

                    // Set battery via reflection
                    var batteryComponent = robot.GetComponent<LogisticsBattery>();
                    if (batteryComponent != null)
                    {
                        var ratioField = typeof(LogisticsBattery).GetField("current",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var maxField = typeof(LogisticsBattery).GetField("max",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (ratioField != null && maxField != null)
                        {
                            float max = (float)maxField.GetValue(batteryComponent);
                            ratioField.SetValue(batteryComponent, data.batteryRatio * max);
                        }
                    }

                    // Set active mode
                    if (!string.IsNullOrEmpty(data.activeMode))
                    {
                        if (System.Enum.TryParse<LogisticsRobotController.RobotMode>(data.activeMode, out var mode))
                        {
                            robot.SetMode(mode);
                        }
                    }

                    // Restore cargo
                    var cargo = robot.GetCargo();
                    if (cargo != null && data.cargoContents != null)
                    {
                        cargo.Clear();
                        foreach (var entry in data.cargoContents)
                        {
                            cargo.TryAddCargo(entry.resourceId, entry.tier, entry.quantity, entry.creditValuePerUnit);
                        }
                    }

                    if (debugLogs)
                        Debug.Log($"[SaveManager] Restored logistics robot: state={data.state}, mode={data.activeMode}, battery={data.batteryRatio:P0}, cargo={data.cargoContents?.Count ?? 0} types");
                }
            }

            if (debugLogs)
                Debug.Log($"[SaveManager] Applied robot data: {currentSave.diggerRobots?.Count ?? 0} digger, {currentSave.logisticsRobots?.Count ?? 0} logistics");
        }

        /// <summary>
        /// Ensure HUD-related canvases are active and HUD components exist.
        /// </summary>
        private void EnsureHUDCanvasActive()
        {
            // Find and enable HUD canvases
            string[] hudCanvasNames = { "HUDCanvas", "MachineUICanvas", "GameCanvas" };
            foreach (var canvasName in hudCanvasNames)
            {
                var canvasObj = GameObject.Find(canvasName);
                if (canvasObj != null && !canvasObj.activeSelf)
                {
                    canvasObj.SetActive(true);
                }
            }

            // Find the proper HUD canvas (NOT the LoadingOverlay)
            Canvas hudCanvas = FindHUDCanvas();
            if (hudCanvas == null)
            {
                Debug.LogWarning("[SaveManager] Could not find a proper HUD canvas");
                return;
            }

            // Create CurrencyHUD if it doesn't exist
            if (CurrencyHUD.Instance == null)
            {
                var hudObj = new GameObject("CurrencyHUD");
                hudObj.transform.SetParent(hudCanvas.transform, false);
                hudObj.AddComponent<CurrencyHUD>();
            }

            // Create ConsumablesHUD if it doesn't exist
            if (ConsumablesHUD.Instance == null)
            {
                var hudObj = new GameObject("ConsumablesHUD");
                hudObj.transform.SetParent(hudCanvas.transform, false);
                hudObj.AddComponent<ConsumablesHUD>();
            }

            // Create EstimatedValueHUD if it doesn't exist (bag value display below currency)
            if (EstimatedValueHUD.Instance == null)
            {
                var hudObj = new GameObject("EstimatedValueHUD");
                hudObj.transform.SetParent(hudCanvas.transform, false);
                hudObj.AddComponent<EstimatedValueHUD>();
            }
        }

        /// <summary>
        /// Find the proper HUD canvas, skipping LoadingOverlay and other non-HUD canvases.
        /// </summary>
        private Canvas FindHUDCanvas()
        {
            // First, try to find a canvas by preferred name
            string[] preferredCanvasNames = { "HUDCanvas", "GameCanvas", "MachineUICanvas", "MainCanvas" };
            foreach (var canvasName in preferredCanvasNames)
            {
                var canvasObj = GameObject.Find(canvasName);
                if (canvasObj != null)
                {
                    var canvas = canvasObj.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        return canvas;
                    }
                }
            }

            // Fall back to finding any canvas that's NOT the loading overlay
            Canvas[] allCanvases = Object.FindObjectsOfType<Canvas>();
            foreach (var canvas in allCanvases)
            {
                // Skip loading overlay and other non-HUD canvases
                if (canvas.name == "LoadingOverlay" ||
                    canvas.name.Contains("Loading") ||
                    canvas.sortingOrder >= 9000) // Loading overlay uses sortingOrder 9999
                {
                    continue;
                }

                // Prefer screen space overlay canvases
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            // Last resort: create a new HUD canvas
            var newCanvasObj = new GameObject("HUDCanvas");
            var newCanvas = newCanvasObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 100;
            newCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            newCanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return newCanvas;
        }

        /// <summary>
        /// Refresh all HUD elements after loading save data.
        /// This ensures UI is updated even if events fired before HUD subscribed.
        /// </summary>
        private void RefreshAllHUDs()
        {
            // Start a coroutine to do an immediate refresh plus a delayed one
            // The delayed refresh catches any HUDs that weren't fully initialized
            StartCoroutine(RefreshAllHUDsCoroutine());
        }

        private System.Collections.IEnumerator RefreshAllHUDsCoroutine()
        {
            // Wait a frame for HUDs to run their Start() methods
            yield return null;

            // First refresh after Start() has run
            DoHUDRefresh("immediate");

            // Wait for HUDs to fully initialize their UI elements
            yield return new WaitForSeconds(0.3f);

            // Second refresh to catch any late initializers
            DoHUDRefresh("delayed");

            // Final refresh after another short delay for extra safety
            yield return new WaitForSeconds(0.5f);
            DoHUDRefresh("final");
        }

        private void DoHUDRefresh(string phase)
        {
            if (currentSave == null)
            {
                Debug.LogWarning($"[SaveManager] {phase}: currentSave is null, skipping HUD refresh");
                return;
            }

            if (debugLogs) Debug.Log($"[SaveManager] {phase}: Refreshing HUDs - currency={currentSave.currency}, drinks={currentSave.drinkCount}, lamps={currentSave.lampsAvailable}");

            // Ensure HUD canvas is active
            EnsureHUDCanvasActive();

            // Re-apply data to managers (this fires events for any subscribers)
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.SetCurrency(currentSave.currency);
                if (debugLogs) Debug.Log($"[SaveManager] {phase}: CurrencyManager.SetCurrency({currentSave.currency}) called");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] {phase}: CurrencyManager.Instance is NULL");
            }

            if (Energy.EnergyManager.Instance != null)
            {
                Energy.EnergyManager.Instance.SetEnergy(currentSave.currentEnergy);
                Energy.EnergyManager.Instance.SetDrinkCount(currentSave.drinkCount);
                if (debugLogs) Debug.Log($"[SaveManager] {phase}: EnergyManager.SetEnergy({currentSave.currentEnergy}), SetDrinkCount({currentSave.drinkCount}) called");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] {phase}: EnergyManager.Instance is NULL");
            }

            // Re-apply lamp count
            var lampController = Lighting.LampPlacementController.Instance;
            if (lampController == null)
            {
                lampController = Object.FindObjectOfType<Lighting.LampPlacementController>();
            }
            if (lampController != null)
            {
                lampController.SetLampCount(currentSave.lampsAvailable);
                if (debugLogs) Debug.Log($"[SaveManager] {phase}: LampPlacementController.SetLampCount({currentSave.lampsAvailable}) called");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] {phase}: LampPlacementController not found");
            }

            // Directly refresh HUD components if they exist
            if (CurrencyHUD.Instance != null)
            {
                CurrencyHUD.Instance.Refresh();
                CurrencyHUD.Instance.SetVisible(true); // Force visibility
                if (debugLogs) Debug.Log($"[SaveManager] {phase}: CurrencyHUD.Refresh() and SetVisible(true) called");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] {phase}: CurrencyHUD.Instance is NULL");
            }

            if (ConsumablesHUD.Instance != null)
            {
                ConsumablesHUD.Instance.RefreshDisplay();
                ConsumablesHUD.Instance.SetVisible(true); // Force visibility
                if (debugLogs) Debug.Log($"[SaveManager] {phase}: ConsumablesHUD.RefreshDisplay() and SetVisible(true) called");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] {phase}: ConsumablesHUD.Instance is NULL");
            }

            // Refresh EstimatedValueHUD (bag value display)
            if (EstimatedValueHUD.Instance != null)
            {
                EstimatedValueHUD.Instance.RefreshDisplay();
                EstimatedValueHUD.Instance.SetVisible(true); // Force visibility
                if (debugLogs) Debug.Log($"[SaveManager] {phase}: EstimatedValueHUD.RefreshDisplay() and SetVisible(true) called");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] {phase}: EstimatedValueHUD.Instance is NULL");
            }
        }

        #endregion

        #region File I/O

        private bool WriteToFile()
        {
            return WriteToFile(SavesIndex.activeSlotId);
        }

        private bool WriteToFile(int slotId)
        {
            try
            {
                string path = Save.SavesIndex.GetSaveFilePath(slotId);
                string json = JsonUtility.ToJson(currentSave, true);
                File.WriteAllText(path, json);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to write save to slot {slotId}: {e.Message}");
                return false;
            }
        }

        private bool ReadFromFile()
        {
            return ReadFromFile(SavesIndex.activeSlotId);
        }

        private bool ReadFromFile(int slotId)
        {
            try
            {
                string path = Save.SavesIndex.GetSaveFilePath(slotId);
                string json = File.ReadAllText(path);
                currentSave = JsonUtility.FromJson<GameSaveData>(json);
                return currentSave != null && currentSave.IsValid();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to read save from slot {slotId}: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Loading Helpers

        private Text loadingText;
        private Image spinnerImage;

        /// <summary>
        /// Show/hide a loading screen with text and spinner.
        /// </summary>
        private void ShowLoadingOverlay(bool show)
        {
            if (show)
            {
                if (loadingOverlay == null)
                {
                    CreateLoadingScreen();
                }
                loadingOverlay.SetActive(true);
                if (spinnerImage != null)
                    StartCoroutine(AnimateSpinner());
            }
            else
            {
                if (loadingOverlay != null)
                {
                    loadingOverlay.SetActive(false);
                    StopCoroutine(AnimateSpinner());
                }
            }
        }

        private void CreateLoadingScreen()
        {
            // Create canvas
            loadingOverlay = new GameObject("LoadingOverlay");
            var canvas = loadingOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            loadingOverlay.AddComponent<CanvasScaler>();
            loadingOverlay.AddComponent<GraphicRaycaster>();

            // Black background
            var bg = new GameObject("Background").AddComponent<Image>();
            bg.transform.SetParent(loadingOverlay.transform, false);
            bg.color = new Color(0.05f, 0.05f, 0.08f, 1f); // Dark blue-black
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            // Loading text
            var textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(loadingOverlay.transform, false);
            loadingText = textObj.AddComponent<Text>();
            loadingText.text = "Loading...";
            loadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            loadingText.fontSize = 36;
            loadingText.color = Color.white;
            loadingText.alignment = TextAnchor.MiddleCenter;
            loadingText.rectTransform.anchorMin = new Vector2(0.5f, 0.4f);
            loadingText.rectTransform.anchorMax = new Vector2(0.5f, 0.4f);
            loadingText.rectTransform.sizeDelta = new Vector2(400, 60);
            loadingText.rectTransform.anchoredPosition = Vector2.zero;

            // Simple spinner (rotating dots)
            var spinnerObj = new GameObject("Spinner");
            spinnerObj.transform.SetParent(loadingOverlay.transform, false);
            spinnerImage = spinnerObj.AddComponent<Image>();
            spinnerImage.color = new Color(0.8f, 0.6f, 0.2f, 1f); // Gold color
            spinnerImage.rectTransform.anchorMin = new Vector2(0.5f, 0.55f);
            spinnerImage.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
            spinnerImage.rectTransform.sizeDelta = new Vector2(50, 50);
            spinnerImage.rectTransform.anchoredPosition = Vector2.zero;

            // Create a simple circle texture for spinner
            var texture = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                    if (dist > 24 && dist < 32)
                        texture.SetPixel(x, y, Color.white);
                    else
                        texture.SetPixel(x, y, Color.clear);
                }
            }
            texture.Apply();
            spinnerImage.sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

            // Tip text
            var tipObj = new GameObject("TipText");
            tipObj.transform.SetParent(loadingOverlay.transform, false);
            var tipText = tipObj.AddComponent<Text>();
            tipText.text = "Preparing your excavation site...";
            tipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tipText.fontSize = 18;
            tipText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            tipText.alignment = TextAnchor.MiddleCenter;
            tipText.fontStyle = FontStyle.Italic;
            tipText.rectTransform.anchorMin = new Vector2(0.5f, 0.25f);
            tipText.rectTransform.anchorMax = new Vector2(0.5f, 0.25f);
            tipText.rectTransform.sizeDelta = new Vector2(600, 40);
            tipText.rectTransform.anchoredPosition = Vector2.zero;
        }

        private System.Collections.IEnumerator AnimateSpinner()
        {
            while (loadingOverlay != null && loadingOverlay.activeSelf && spinnerImage != null)
            {
                spinnerImage.rectTransform.Rotate(0, 0, -360f * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// Freeze/unfreeze player movement and camera during loading.
        /// </summary>
        private void FreezePlayer(bool freeze)
        {
            isPlayerFrozen = freeze;

            // Find player and disable/enable movement
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // Disable CharacterController to prevent movement
                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = !freeze;

                // Try to disable FirstPersonController if it exists
                var fpc = player.GetComponent<BeneathTheFloor.Player.FirstPersonController>();
                if (fpc != null)
                    fpc.enabled = !freeze;
            }

            // Optionally freeze time (but this can cause issues with coroutines)
            // Time.timeScale = freeze ? 0f : 1f;
        }

        #endregion
    }
}
