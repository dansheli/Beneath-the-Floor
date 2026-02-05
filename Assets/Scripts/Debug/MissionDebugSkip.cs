using UnityEngine;
using BeneathTheFloor.Missions;

namespace BeneathTheFloor.DebugTools
{
    /// <summary>
    /// Debug tool to skip missions for testing.
    /// Properly completes each mission (including rewards) when skipping.
    /// </summary>
    public class MissionDebugSkip : MonoBehaviour
    {
        // Set to true to enable debug mission skipping (F9/F10)
        private const bool ENABLE_DEBUG = false;

        [Header("Settings")]
        [SerializeField] private KeyCode skipCurrentKey = KeyCode.F9;
        [SerializeField] private KeyCode openMenuKey = KeyCode.F10;
#pragma warning disable CS0414 // Field is assigned but never used
        [SerializeField] private bool showDebugUI = false;

        [Header("Skip To Mission")]
        [SerializeField] private int skipToMissionIndex = 0;
#pragma warning restore CS0414

        private bool showMenu = false;
        private Vector2 scrollPosition;

        private void Update()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            // F9 - Skip current mission
            if (Input.GetKeyDown(skipCurrentKey))
            {
                SkipCurrentMission();
            }

            // F10 - Toggle menu
            if (Input.GetKeyDown(openMenuKey))
            {
                showMenu = !showMenu;
            }
#pragma warning restore CS0162
        }

        private void OnGUI()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG || !showMenu) return;

            var manager = MissionManager.Instance;
            if (manager == null) return;

            // Create window
            GUILayout.BeginArea(new Rect(10, 10, 350, 500));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Mission Debug Skip</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
            GUILayout.Space(10);

            // Current mission info
            if (manager.CurrentMission != null)
            {
                GUILayout.Label($"<b>Current Mission:</b> {manager.CurrentMission.missionName}", new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"Objective: {manager.CurrentMission.objectiveText}");
            }
            else
            {
                GUILayout.Label("No active mission");
            }

            GUILayout.Space(10);

            // Skip current button
            if (GUILayout.Button("Skip Current Mission (F9)", GUILayout.Height(30)))
            {
                SkipCurrentMission();
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Skip To Mission:</b>", new GUIStyle(GUI.skin.label) { richText = true });

            // Get missions list via reflection
            var missionsField = typeof(MissionManager).GetField("missions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (missionsField != null)
            {
                var missions = missionsField.GetValue(manager) as System.Collections.Generic.List<MissionData>;
                if (missions != null)
                {
                    scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

                    for (int i = 0; i < missions.Count; i++)
                    {
                        var mission = missions[i];
                        string buttonText = $"{i + 1}. {mission.missionName}";

                        // Highlight current mission
                        GUI.enabled = true;
                        if (mission == manager.CurrentMission)
                        {
                            buttonText = $">> {buttonText} <<";
                        }

                        if (GUILayout.Button(buttonText))
                        {
                            SkipToMission(i);
                        }
                    }

                    GUILayout.EndScrollView();
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Close (F10)"))
            {
                showMenu = false;
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
#pragma warning restore CS0162
        }

        /// <summary>
        /// Skip the current mission by completing it properly.
        /// </summary>
        public void SkipCurrentMission()
        {
            var manager = MissionManager.Instance;
            if (manager == null || manager.CurrentMission == null)
            {
                Debug.Log("[MissionDebugSkip] No active mission to skip.");
                return;
            }

            string missionName = manager.CurrentMission.missionName;
            manager.CompleteMission();
            Debug.Log($"[MissionDebugSkip] Skipped mission: {missionName}");
        }

        /// <summary>
        /// Skip to a specific mission by completing all previous missions.
        /// </summary>
        public void SkipToMission(int targetIndex)
        {
            var manager = MissionManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[MissionDebugSkip] MissionManager not found!");
                return;
            }

            // Get current index via reflection
            var indexField = typeof(MissionManager).GetField("currentMissionIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (indexField == null)
            {
                Debug.LogError("[MissionDebugSkip] Could not access currentMissionIndex!");
                return;
            }

            int currentIndex = (int)indexField.GetValue(manager);

            // Complete missions one by one until we reach target
            int safeguard = 50; // Prevent infinite loop
            while (currentIndex < targetIndex && safeguard > 0)
            {
                if (manager.CurrentMission == null)
                {
                    // Force start next mission if none active
                    manager.StartNextMission();
                }

                if (manager.CurrentMission != null)
                {
                    Debug.Log($"[MissionDebugSkip] Completing: {manager.CurrentMission.missionName}");
                    manager.CompleteMission();
                }

                currentIndex = (int)indexField.GetValue(manager);
                safeguard--;
            }

            Debug.Log($"[MissionDebugSkip] Skipped to mission index {targetIndex}");
        }
    }
}
