using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BeneathTheFloor.Save;

namespace BeneathTheFloor.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for GameSaveData and related data structures.
    /// Tests serialization, validation, and data integrity.
    /// </summary>
    [TestFixture]
    public class GameSaveDataTests
    {
        #region GameSaveData Tests

        [Test]
        public void GameSaveData_NewInstance_HasValidDefaults()
        {
            // Arrange & Act
            var saveData = new Save.GameSaveData();

            // Assert
            Assert.AreEqual(1, saveData.version, "Version should default to 1");
            Assert.IsFalse(string.IsNullOrEmpty(saveData.savedAt), "savedAt should be set on creation");
            Assert.IsNotNull(saveData.player, "Player data should not be null");
            Assert.IsNotNull(saveData.inventoryItems, "Inventory items list should not be null");
            Assert.IsNotNull(saveData.unlockedUpgrades, "Unlocked upgrades list should not be null");
            Assert.IsNotNull(saveData.ownedTools, "Owned tools list should not be null");
        }

        [Test]
        public void GameSaveData_IsValid_ReturnsTrueForValidData()
        {
            // Arrange
            var saveData = new Save.GameSaveData();

            // Act
            bool isValid = saveData.IsValid();

            // Assert
            Assert.IsTrue(isValid, "Newly created GameSaveData should be valid");
        }

        [Test]
        public void GameSaveData_IsValid_ReturnsFalseForZeroVersion()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            saveData.version = 0;

            // Act
            bool isValid = saveData.IsValid();

            // Assert
            Assert.IsFalse(isValid, "GameSaveData with version 0 should be invalid");
        }

        [Test]
        public void GameSaveData_IsValid_ReturnsFalseForEmptySavedAt()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            saveData.savedAt = "";

            // Act
            bool isValid = saveData.IsValid();

            // Assert
            Assert.IsFalse(isValid, "GameSaveData with empty savedAt should be invalid");
        }

        [Test]
        public void GameSaveData_IsValid_ReturnsFalseForNullSavedAt()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            saveData.savedAt = null;

            // Act
            bool isValid = saveData.IsValid();

            // Assert
            Assert.IsFalse(isValid, "GameSaveData with null savedAt should be invalid");
        }

        [Test]
        public void GameSaveData_Serialization_PreservesAllFields()
        {
            // Arrange
            var original = new Save.GameSaveData();
            original.saveName = "Test Save";
            original.currency = 1000;
            original.totalEarned = 5000;
            original.currentToolTier = 3;
            original.winchTier = 2;
            original.currentEnergy = 75.5f;
            original.maxEnergy = 150f;
            original.maxDepthReached = 50;
            original.currentDepth = 25;
            original.totalPlayTime = 3600f;
            original.hasTerrainData = true;

            original.player.posX = 10f;
            original.player.posY = 5f;
            original.player.posZ = -3f;
            original.player.rotY = 90f;
            original.player.currentScene = "TestScene";

            original.inventoryItems.Add(new Save.InventoryItemSaveData
            {
                itemId = "item_001",
                itemName = "Test Item",
                quantity = 5,
                slotIndex = 0
            });

            original.unlockedUpgrades.Add("upgrade_speed");
            original.unlockedUpgrades.Add("upgrade_depth");

            original.ownedTools.Add(new Save.OwnedToolSaveData
            {
                toolName = "Pickaxe",
                tier = 2,
                digSpeed = 1.5f,
                durability = 80,
                maxDurability = 100,
                maxDepth = 30,
                isEquipped = true
            });

            // Act
            string json = JsonUtility.ToJson(original);
            var deserialized = JsonUtility.FromJson<Save.GameSaveData>(json);

            // Assert
            Assert.AreEqual(original.version, deserialized.version);
            Assert.AreEqual(original.saveName, deserialized.saveName);
            Assert.AreEqual(original.currency, deserialized.currency);
            Assert.AreEqual(original.totalEarned, deserialized.totalEarned);
            Assert.AreEqual(original.currentToolTier, deserialized.currentToolTier);
            Assert.AreEqual(original.winchTier, deserialized.winchTier);
            Assert.AreEqual(original.currentEnergy, deserialized.currentEnergy, 0.001f);
            Assert.AreEqual(original.maxEnergy, deserialized.maxEnergy, 0.001f);
            Assert.AreEqual(original.maxDepthReached, deserialized.maxDepthReached);
            Assert.AreEqual(original.currentDepth, deserialized.currentDepth);
            Assert.AreEqual(original.totalPlayTime, deserialized.totalPlayTime, 0.001f);
            Assert.AreEqual(original.hasTerrainData, deserialized.hasTerrainData);

            // Player data
            Assert.AreEqual(original.player.posX, deserialized.player.posX, 0.001f);
            Assert.AreEqual(original.player.posY, deserialized.player.posY, 0.001f);
            Assert.AreEqual(original.player.posZ, deserialized.player.posZ, 0.001f);
            Assert.AreEqual(original.player.rotY, deserialized.player.rotY, 0.001f);
            Assert.AreEqual(original.player.currentScene, deserialized.player.currentScene);

            // Inventory
            Assert.AreEqual(1, deserialized.inventoryItems.Count);
            Assert.AreEqual("item_001", deserialized.inventoryItems[0].itemId);
            Assert.AreEqual(5, deserialized.inventoryItems[0].quantity);

            // Upgrades
            Assert.AreEqual(2, deserialized.unlockedUpgrades.Count);
            Assert.Contains("upgrade_speed", deserialized.unlockedUpgrades);
            Assert.Contains("upgrade_depth", deserialized.unlockedUpgrades);

            // Tools
            Assert.AreEqual(1, deserialized.ownedTools.Count);
            Assert.AreEqual("Pickaxe", deserialized.ownedTools[0].toolName);
            Assert.AreEqual(2, deserialized.ownedTools[0].tier);
            Assert.IsTrue(deserialized.ownedTools[0].isEquipped);
        }

        [Test]
        public void GameSaveData_SavedAt_IsValidDateTimeFormat()
        {
            // Arrange
            var saveData = new Save.GameSaveData();

            // Act
            bool canParse = DateTime.TryParse(saveData.savedAt, out DateTime parsedDate);

            // Assert
            Assert.IsTrue(canParse, "savedAt should be a valid DateTime string");
            Assert.That(parsedDate, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(5)),
                "savedAt should be close to current time");
        }

        #endregion

        #region PlayerSaveData Tests

        [Test]
        public void PlayerSaveData_SetPosition_StoresCorrectValues()
        {
            // Arrange
            var playerData = new Save.PlayerSaveData();
            var position = new Vector3(10.5f, 20.3f, -5.7f);

            // Act
            playerData.SetPosition(position);

            // Assert
            Assert.AreEqual(10.5f, playerData.posX, 0.001f);
            Assert.AreEqual(20.3f, playerData.posY, 0.001f);
            Assert.AreEqual(-5.7f, playerData.posZ, 0.001f);
        }

        [Test]
        public void PlayerSaveData_GetPosition_ReturnsCorrectVector()
        {
            // Arrange
            var playerData = new Save.PlayerSaveData();
            playerData.posX = 10.5f;
            playerData.posY = 20.3f;
            playerData.posZ = -5.7f;

            // Act
            Vector3 position = playerData.GetPosition();

            // Assert
            Assert.AreEqual(10.5f, position.x, 0.001f);
            Assert.AreEqual(20.3f, position.y, 0.001f);
            Assert.AreEqual(-5.7f, position.z, 0.001f);
        }

        [Test]
        public void PlayerSaveData_SetRotation_StoresYRotation()
        {
            // Arrange
            var playerData = new Save.PlayerSaveData();

            // Act
            playerData.SetRotation(90f);

            // Assert
            Assert.AreEqual(90f, playerData.rotY, 0.001f);
        }

        [Test]
        public void PlayerSaveData_GetRotation_ReturnsCorrectQuaternion()
        {
            // Arrange
            var playerData = new Save.PlayerSaveData();
            playerData.rotY = 90f;

            // Act
            Quaternion rotation = playerData.GetRotation();
            Vector3 euler = rotation.eulerAngles;

            // Assert
            Assert.AreEqual(0f, euler.x, 0.001f);
            Assert.AreEqual(90f, euler.y, 0.001f);
            Assert.AreEqual(0f, euler.z, 0.001f);
        }

        [Test]
        public void PlayerSaveData_PositionRoundTrip_PreservesValues()
        {
            // Arrange
            var playerData = new Save.PlayerSaveData();
            var originalPos = new Vector3(123.456f, 789.012f, -345.678f);

            // Act
            playerData.SetPosition(originalPos);
            Vector3 retrievedPos = playerData.GetPosition();

            // Assert
            Assert.AreEqual(originalPos.x, retrievedPos.x, 0.001f);
            Assert.AreEqual(originalPos.y, retrievedPos.y, 0.001f);
            Assert.AreEqual(originalPos.z, retrievedPos.z, 0.001f);
        }

        #endregion

        #region InventoryItemSaveData Tests

        [Test]
        public void InventoryItemSaveData_Serialization_PreservesFields()
        {
            // Arrange
            var item = new Save.InventoryItemSaveData
            {
                itemId = "gold_ore",
                itemName = "Gold Ore",
                quantity = 25,
                slotIndex = 3
            };

            // Act
            string json = JsonUtility.ToJson(item);
            var deserialized = JsonUtility.FromJson<Save.InventoryItemSaveData>(json);

            // Assert
            Assert.AreEqual("gold_ore", deserialized.itemId);
            Assert.AreEqual("Gold Ore", deserialized.itemName);
            Assert.AreEqual(25, deserialized.quantity);
            Assert.AreEqual(3, deserialized.slotIndex);
        }

        #endregion

        #region OwnedToolSaveData Tests

        [Test]
        public void OwnedToolSaveData_Serialization_PreservesFields()
        {
            // Arrange
            var tool = new Save.OwnedToolSaveData
            {
                toolName = "Diamond Pickaxe",
                tier = 5,
                digSpeed = 3.5f,
                durability = 450,
                maxDurability = 500,
                maxDepth = 100,
                isEquipped = true
            };

            // Act
            string json = JsonUtility.ToJson(tool);
            var deserialized = JsonUtility.FromJson<Save.OwnedToolSaveData>(json);

            // Assert
            Assert.AreEqual("Diamond Pickaxe", deserialized.toolName);
            Assert.AreEqual(5, deserialized.tier);
            Assert.AreEqual(3.5f, deserialized.digSpeed, 0.001f);
            Assert.AreEqual(450, deserialized.durability);
            Assert.AreEqual(500, deserialized.maxDurability);
            Assert.AreEqual(100, deserialized.maxDepth);
            Assert.IsTrue(deserialized.isEquipped);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void GameSaveData_EmptyLists_SerializeCorrectly()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            // Lists are empty by default

            // Act
            string json = JsonUtility.ToJson(saveData);
            var deserialized = JsonUtility.FromJson<Save.GameSaveData>(json);

            // Assert
            Assert.IsNotNull(deserialized.inventoryItems);
            Assert.AreEqual(0, deserialized.inventoryItems.Count);
            Assert.IsNotNull(deserialized.unlockedUpgrades);
            Assert.AreEqual(0, deserialized.unlockedUpgrades.Count);
            Assert.IsNotNull(deserialized.ownedTools);
            Assert.AreEqual(0, deserialized.ownedTools.Count);
        }

        [Test]
        public void GameSaveData_NegativeValues_PreserveCorrectly()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            saveData.player.posX = -100f;
            saveData.player.posY = -50f;
            saveData.player.posZ = -200f;
            saveData.currentDepth = -10; // Edge case

            // Act
            string json = JsonUtility.ToJson(saveData);
            var deserialized = JsonUtility.FromJson<Save.GameSaveData>(json);

            // Assert
            Assert.AreEqual(-100f, deserialized.player.posX, 0.001f);
            Assert.AreEqual(-50f, deserialized.player.posY, 0.001f);
            Assert.AreEqual(-200f, deserialized.player.posZ, 0.001f);
            Assert.AreEqual(-10, deserialized.currentDepth);
        }

        [Test]
        public void GameSaveData_LargeValues_PreserveCorrectly()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            saveData.currency = int.MaxValue;
            saveData.totalPlayTime = float.MaxValue / 2; // Avoid overflow
            saveData.maxDepthReached = 999999;

            // Act
            string json = JsonUtility.ToJson(saveData);
            var deserialized = JsonUtility.FromJson<Save.GameSaveData>(json);

            // Assert
            Assert.AreEqual(int.MaxValue, deserialized.currency);
            Assert.AreEqual(saveData.totalPlayTime, deserialized.totalPlayTime, 1f);
            Assert.AreEqual(999999, deserialized.maxDepthReached);
        }

        [Test]
        public void GameSaveData_SpecialCharactersInStrings_PreserveCorrectly()
        {
            // Arrange
            var saveData = new Save.GameSaveData();
            saveData.saveName = "Test \"Save\" with 'quotes' and\nnewlines";
            saveData.player.currentScene = "Scene_With_Underscore";

            var item = new Save.InventoryItemSaveData
            {
                itemId = "item-with-dash",
                itemName = "Item: The Sequel"
            };
            saveData.inventoryItems.Add(item);

            // Act
            string json = JsonUtility.ToJson(saveData);
            var deserialized = JsonUtility.FromJson<Save.GameSaveData>(json);

            // Assert
            Assert.AreEqual(saveData.saveName, deserialized.saveName);
            Assert.AreEqual(saveData.player.currentScene, deserialized.player.currentScene);
            Assert.AreEqual("item-with-dash", deserialized.inventoryItems[0].itemId);
        }

        #endregion
    }
}
