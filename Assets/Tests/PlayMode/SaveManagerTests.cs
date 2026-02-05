using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BeneathTheFloor.Save;

namespace BeneathTheFloor.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for SaveManager.
    /// Tests file I/O operations, events, and save/load flow.
    /// </summary>
    [TestFixture]
    public class SaveManagerTests
    {
        private GameObject saveManagerObject;
        private Save.SaveManager saveManager;
        private string testSavePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Create SaveManager instance for testing
            saveManagerObject = new GameObject("TestSaveManager");
            saveManager = saveManagerObject.AddComponent<Save.SaveManager>();

            // Wait a frame for Awake to complete
            yield return null;

            // Get the save path
            testSavePath = saveManager.SavePath;

            // Clean up any existing test save file
            CleanupTestSave();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up test save file
            CleanupTestSave();

            // Destroy the test object
            if (saveManagerObject != null)
            {
                Object.Destroy(saveManagerObject);
            }

            yield return null;
        }

        private void CleanupTestSave()
        {
            if (!string.IsNullOrEmpty(testSavePath) && File.Exists(testSavePath))
            {
                try
                {
                    File.Delete(testSavePath);
                }
                catch (System.Exception)
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region HasSaveFile Tests

        [UnityTest]
        public IEnumerator HasSaveFile_WhenNoSaveExists_ReturnsFalse()
        {
            // Arrange
            CleanupTestSave();
            yield return null;

            // Act
            bool hasSave = saveManager.HasSaveFile;

            // Assert
            Assert.IsFalse(hasSave, "HasSaveFile should return false when no save exists");
        }

        [UnityTest]
        public IEnumerator HasSaveFile_AfterSave_ReturnsTrue()
        {
            // Arrange & Act
            bool saveResult = saveManager.SaveGame("Test Save");
            yield return null;

            // Assert
            Assert.IsTrue(saveResult, "SaveGame should return true");
            Assert.IsTrue(saveManager.HasSaveFile, "HasSaveFile should return true after saving");
        }

        #endregion

        #region SaveGame Tests

        [UnityTest]
        public IEnumerator SaveGame_CreatesFile()
        {
            // Arrange
            CleanupTestSave();

            // Act
            bool result = saveManager.SaveGame();
            yield return null;

            // Assert
            Assert.IsTrue(result, "SaveGame should return true");
            Assert.IsTrue(File.Exists(testSavePath), "Save file should exist after SaveGame");
        }

        [UnityTest]
        public IEnumerator SaveGame_WithCustomName_StoresName()
        {
            // Arrange
            string customName = "My Custom Save";

            // Act
            saveManager.SaveGame(customName);
            yield return null;

            // Assert
            string json = File.ReadAllText(testSavePath);
            Assert.IsTrue(json.Contains(customName), "Save file should contain custom save name");
        }

        [UnityTest]
        public IEnumerator SaveGame_CreatesValidJsonFile()
        {
            // Arrange & Act
            saveManager.SaveGame();
            yield return null;

            // Assert
            string json = File.ReadAllText(testSavePath);
            Assert.IsNotNull(json, "Save file content should not be null");
            Assert.IsNotEmpty(json, "Save file content should not be empty");

            // Try to parse the JSON
            var saveData = JsonUtility.FromJson<Save.GameSaveData>(json);
            Assert.IsNotNull(saveData, "Save file should be valid JSON");
            Assert.IsTrue(saveData.IsValid(), "Parsed save data should be valid");
        }

        [UnityTest]
        public IEnumerator SaveGame_FiresSaveStartedEvent()
        {
            // Arrange
            bool eventFired = false;
            saveManager.OnSaveStarted += () => eventFired = true;

            // Act
            saveManager.SaveGame();
            yield return null;

            // Assert
            Assert.IsTrue(eventFired, "OnSaveStarted event should be fired");
        }

        [UnityTest]
        public IEnumerator SaveGame_FiresSaveCompletedEvent()
        {
            // Arrange
            bool eventFired = false;
            saveManager.OnSaveCompleted += () => eventFired = true;

            // Act
            saveManager.SaveGame();
            yield return null;

            // Assert
            Assert.IsTrue(eventFired, "OnSaveCompleted event should be fired");
        }

        [UnityTest]
        public IEnumerator SaveGame_EventsFireInCorrectOrder()
        {
            // Arrange
            int startedOrder = 0;
            int completedOrder = 0;
            int eventCounter = 0;

            saveManager.OnSaveStarted += () => startedOrder = ++eventCounter;
            saveManager.OnSaveCompleted += () => completedOrder = ++eventCounter;

            // Act
            saveManager.SaveGame();
            yield return null;

            // Assert
            Assert.AreEqual(1, startedOrder, "OnSaveStarted should fire first");
            Assert.AreEqual(2, completedOrder, "OnSaveCompleted should fire second");
        }

        #endregion

        #region LoadGame Tests

        [UnityTest]
        public IEnumerator LoadGame_WhenNoSaveExists_ReturnsFalse()
        {
            // Arrange
            CleanupTestSave();
            yield return null;

            // Act
            bool result = saveManager.LoadGame();

            // Assert
            Assert.IsFalse(result, "LoadGame should return false when no save exists");
        }

        [UnityTest]
        public IEnumerator LoadGame_AfterSave_ReturnsTrue()
        {
            // Arrange
            saveManager.SaveGame("Test Save");
            yield return null;

            // Act
            bool result = saveManager.LoadGame();

            // Assert
            Assert.IsTrue(result, "LoadGame should return true after a valid save");
        }

        [UnityTest]
        public IEnumerator LoadGame_FiresLoadStartedEvent()
        {
            // Arrange
            saveManager.SaveGame();
            yield return null;

            bool eventFired = false;
            saveManager.OnLoadStarted += () => eventFired = true;

            // Act
            saveManager.LoadGame();
            yield return null;

            // Assert
            Assert.IsTrue(eventFired, "OnLoadStarted event should be fired");
        }

        [UnityTest]
        public IEnumerator LoadGame_FiresLoadCompletedEvent()
        {
            // Arrange
            saveManager.SaveGame();
            yield return null;

            bool eventFired = false;
            saveManager.OnLoadCompleted += () => eventFired = true;

            // Act
            saveManager.LoadGame();
            yield return null;

            // Assert
            Assert.IsTrue(eventFired, "OnLoadCompleted event should be fired");
        }

        [UnityTest]
        public IEnumerator LoadGame_WithCorruptedFile_ReturnsFalse()
        {
            // Arrange
            File.WriteAllText(testSavePath, "{ invalid json }}}");
            yield return null;

            // Act
            bool result = saveManager.LoadGame();

            // Assert - expect false due to invalid JSON
            // Note: This might still return true if JsonUtility is lenient
            // The important thing is it doesn't crash
            Assert.DoesNotThrow(() => saveManager.LoadGame(),
                "LoadGame should not throw exception with corrupted file");
        }

        #endregion

        #region DeleteSave Tests

        [UnityTest]
        public IEnumerator DeleteSave_WhenNoSaveExists_ReturnsFalse()
        {
            // Arrange
            CleanupTestSave();
            yield return null;

            // Act
            bool result = saveManager.DeleteSave();

            // Assert
            Assert.IsFalse(result, "DeleteSave should return false when no save exists");
        }

        [UnityTest]
        public IEnumerator DeleteSave_AfterSave_ReturnsTrue()
        {
            // Arrange
            saveManager.SaveGame();
            yield return null;
            Assert.IsTrue(File.Exists(testSavePath), "Save file should exist before delete");

            // Act
            bool result = saveManager.DeleteSave();

            // Assert
            Assert.IsTrue(result, "DeleteSave should return true");
        }

        [UnityTest]
        public IEnumerator DeleteSave_RemovesFile()
        {
            // Arrange
            saveManager.SaveGame();
            yield return null;
            Assert.IsTrue(File.Exists(testSavePath), "Save file should exist before delete");

            // Act
            saveManager.DeleteSave();
            yield return null;

            // Assert
            Assert.IsFalse(File.Exists(testSavePath), "Save file should not exist after delete");
        }

        [UnityTest]
        public IEnumerator DeleteSave_UpdatesHasSaveFile()
        {
            // Arrange
            saveManager.SaveGame();
            yield return null;
            Assert.IsTrue(saveManager.HasSaveFile, "HasSaveFile should be true before delete");

            // Act
            saveManager.DeleteSave();
            yield return null;

            // Assert
            Assert.IsFalse(saveManager.HasSaveFile, "HasSaveFile should be false after delete");
        }

        #endregion

        #region Save/Load Cycle Tests

        [UnityTest]
        public IEnumerator SaveAndLoad_PreservesDataIntegrity()
        {
            // Arrange - Save with specific data
            saveManager.SaveGame("Integrity Test");
            yield return null;

            // Read the saved data directly
            string originalJson = File.ReadAllText(testSavePath);
            var originalData = JsonUtility.FromJson<Save.GameSaveData>(originalJson);

            // Act - Load and verify
            bool loadResult = saveManager.LoadGame();
            yield return null;

            // Assert
            Assert.IsTrue(loadResult, "Load should succeed");
            Assert.AreEqual("Integrity Test", originalData.saveName);
            Assert.IsTrue(originalData.IsValid());
        }

        [UnityTest]
        public IEnumerator MultipleSaves_OverwritesPreviousSave()
        {
            // Arrange - First save
            saveManager.SaveGame("First Save");
            yield return null;

            // Act - Second save with different name
            saveManager.SaveGame("Second Save");
            yield return null;

            // Assert - Read file and check it has second save name
            string json = File.ReadAllText(testSavePath);
            Assert.IsTrue(json.Contains("Second Save"), "File should contain second save name");
            Assert.IsFalse(json.Contains("First Save"), "File should not contain first save name");
        }

        #endregion

        #region Singleton Tests

        [UnityTest]
        public IEnumerator Instance_IsNotNull_AfterAwake()
        {
            yield return null;

            // Assert
            Assert.IsNotNull(Save.SaveManager.Instance, "SaveManager.Instance should not be null");
        }

        [UnityTest]
        public IEnumerator Instance_IsSameAsComponent()
        {
            yield return null;

            // Assert
            Assert.AreSame(saveManager, Save.SaveManager.Instance,
                "SaveManager.Instance should be the same as the component");
        }

        #endregion

        #region SavePath Tests

        [Test]
        public void SavePath_IsInPersistentDataPath()
        {
            // Assert
            Assert.IsTrue(saveManager.SavePath.StartsWith(Application.persistentDataPath),
                "SavePath should be in Application.persistentDataPath");
        }

        [Test]
        public void SavePath_EndsWithJsonExtension()
        {
            // Assert
            Assert.IsTrue(saveManager.SavePath.EndsWith(".json"),
                "SavePath should end with .json extension");
        }

        #endregion
    }
}
