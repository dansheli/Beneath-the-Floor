using System;
using System.Collections.Generic;
using UnityEngine;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Save
{
    /// <summary>
    /// Master save data structure containing all game state.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public string savedAt;
        public string saveName;

        // Player
        public PlayerSaveData player = new PlayerSaveData();

        // Economy
        public int currency;
        public int totalEarned;

        // Inventory
        public List<InventoryItemSaveData> inventoryItems = new List<InventoryItemSaveData>();

        // Tools & Upgrades
        public int currentToolTier = 1;
        public int currentToolIndex = 0; // 0=Shovel, 1=Spade, 2=Pickaxe, 3=DrillPike
        public bool hasToolEquipped = false;
        public List<string> unlockedUpgrades = new List<string>();
        public List<OwnedToolSaveData> ownedTools = new List<OwnedToolSaveData>();

        // RuntimeUpgrade levels (tool_power, tool_tier, energy_capacity, etc.)
        public List<UpgradeLevelEntry> runtimeUpgradeLevels = new List<UpgradeLevelEntry>();

        // Winch
        public int winchTier;

        // Energy
        public float currentEnergy = 100f;
        public float maxEnergy = 100f;
        public int energyUpgradeLevel = 0;

        // Consumables
        public int drinkCount = 0;
        public int lampsAvailable = 0;

        // Inventory Upgrades
        public int inventoryUpgradeLevel = 0; // 0=base (5 slots), 1=expanded (10 slots), 2=fully expanded (15 slots), 3-7=stack size upgrades

        // Headlamp Upgrade
        public int headlampUpgradeLevel = 0; // 0=base, 1-2=upgraded range/intensity

        // Radar Tool
        public bool hasRadarUnlocked = false;

        // Jetpack
        public bool hasJetpack = false;

        // Placed Lamps (positions and rotations)
        public List<PlacedLampSaveData> placedLamps = new List<PlacedLampSaveData>();

        // Treasure Chests
        public List<string> openedChestIds = new List<string>();

        // Progress
        public int maxDepthReached;
        public int currentDepth;
        public float totalPlayTime;

        // Missions
        public int currentMissionIndex = -1;
        public int missionDigCount = 0;        // Partial dig progress for dig-count missions
        public int missionResourceCount = 0;   // Partial resource progress for resource-count missions

        // Story
        public int storyChapter = 1;
        public List<string> discoveredStoryItemIds = new List<string>();

        // Terrain (stored separately for size, but reference here)
        public bool hasTerrainData;

        // Hidden Nodes (resource nodes underground)
        public int nodeWorldSeed;
        public List<ChunkNodesSaveData> nodeData = new List<ChunkNodesSaveData>();

        // Robots
        public List<DiggerRobotSaveData> diggerRobots = new List<DiggerRobotSaveData>();
        public List<LogisticsRobotSaveData> logisticsRobots = new List<LogisticsRobotSaveData>();

        public GameSaveData()
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public bool IsValid()
        {
            return version > 0 && !string.IsNullOrEmpty(savedAt);
        }
    }

    [Serializable]
    public class PlayerSaveData
    {
        public float posX, posY, posZ;
        public float rotY; // Y rotation only (facing direction)
        public string currentScene;

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }

        public void SetRotation(float yRotation)
        {
            rotY = yRotation;
        }

        public Quaternion GetRotation()
        {
            return Quaternion.Euler(0f, rotY, 0f);
        }
    }

    [Serializable]
    public class InventoryItemSaveData
    {
        public string itemId;
        public string itemName;
        public int quantity;
        public int slotIndex;
    }

    [Serializable]
    public class UpgradeLevelEntry
    {
        public string upgradeId;
        public int level;
    }

    [Serializable]
    public class OwnedToolSaveData
    {
        public string toolName;
        public int tier;
        public float digSpeed;
        public int durability;
        public int maxDurability;
        public int maxDepth;
        public bool isEquipped;
    }

    [Serializable]
    public class PlacedLampSaveData
    {
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x;
            rotY = rot.y;
            rotZ = rot.z;
            rotW = rot.w;
        }

        public Quaternion GetRotation()
        {
            return new Quaternion(rotX, rotY, rotZ, rotW);
        }
    }

    // =====================================================================
    // ROBOT SAVE DATA
    // =====================================================================

    [Serializable]
    public class DiggerRobotSaveData
    {
        public string state; // "Docked", "Digging", "Returning", "Shutdown", "Carried", "Recharging"
        public float batteryRatio;
        public float posX, posY, posZ;
        public float rotY;
        public bool hasResumePoint;
        public float resumePosX, resumePosY, resumePosZ;
        public float resumeYaw;

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }

        public void SetResumePosition(Vector3 pos)
        {
            resumePosX = pos.x;
            resumePosY = pos.y;
            resumePosZ = pos.z;
        }

        public Vector3 GetResumePosition()
        {
            return new Vector3(resumePosX, resumePosY, resumePosZ);
        }
    }

    [Serializable]
    public class LogisticsRobotSaveData
    {
        public string state; // LogisticsState enum as string
        public float batteryRatio;
        public float posX, posY, posZ;
        public float rotY;
        public string activeMode; // "Idle", "FollowPlayer", "WorkWithDigger"
        public List<CargoEntrySaveData> cargoContents = new List<CargoEntrySaveData>();

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }
    }

    [Serializable]
    public class CargoEntrySaveData
    {
        public string resourceId;
        public int tier;
        public int quantity;
        public int creditValuePerUnit;
    }
}
