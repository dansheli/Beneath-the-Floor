using UnityEngine;
using System;
using System.Collections.Generic;

namespace BeneathTheFloor.Environment
{
    /// <summary>
    /// Serializable transform data for capturing object state.
    /// </summary>
    [Serializable]
    public class TransformSnapshot
    {
        public Vector3 position;
        public Vector3 rotation; // Euler angles
        public Vector3 scale;

        public TransformSnapshot() { }

        public TransformSnapshot(Transform t)
        {
            if (t != null)
            {
                position = t.position;
                rotation = t.eulerAngles;
                scale = t.localScale;
            }
        }

        public void ApplyTo(Transform t)
        {
            if (t != null)
            {
                t.position = position;
                t.eulerAngles = rotation;
                t.localScale = scale;
            }
        }

        public override string ToString()
        {
            return $"pos=({position.x:F4}, {position.y:F4}, {position.z:F4}), rot=({rotation.x:F1}, {rotation.y:F1}, {rotation.z:F1}), scale=({scale.x:F4}, {scale.y:F4}, {scale.z:F4})";
        }
    }

    /// <summary>
    /// Snapshot entry for a single named object.
    /// </summary>
    [Serializable]
    public class ObjectSnapshot
    {
        public string objectName;
        public string hierarchyPath;
        public TransformSnapshot transform;
        public bool isActive;

        public ObjectSnapshot() { }

        public ObjectSnapshot(GameObject obj, string basePath = "")
        {
            if (obj != null)
            {
                objectName = obj.name;
                hierarchyPath = GetHierarchyPath(obj, basePath);
                transform = new TransformSnapshot(obj.transform);
                isActive = obj.activeSelf;
            }
        }

        private string GetHierarchyPath(GameObject obj, string basePath)
        {
            if (obj == null) return "";

            List<string> pathParts = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                pathParts.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", pathParts);
        }

        public override string ToString()
        {
            return $"{objectName} @ {hierarchyPath}: {transform}";
        }
    }

    /// <summary>
    /// ScriptableObject that stores a complete basement layout snapshot.
    /// </summary>
    [CreateAssetMenu(fileName = "BasementLayoutSnapshot", menuName = "Beneath The Floor/Basement Layout Snapshot")]
    public class BasementLayoutSnapshot : ScriptableObject
    {
        [Header("Metadata")]
        public string captureTime;
        public string sceneName;
        public string notes;

        [Header("Main Structure")]
        public ObjectSnapshot basementRoot;
        public ObjectSnapshot basementFloor;
        public ObjectSnapshot basementCeiling;

        [Header("Walls")]
        public ObjectSnapshot wallNorth;
        public ObjectSnapshot wallSouth;
        public ObjectSnapshot wallEast;
        public ObjectSnapshot wallWest;

        [Header("Floor Ring")]
        public ObjectSnapshot floorRingRoot;
        public List<ObjectSnapshot> floorRingSegments = new List<ObjectSnapshot>();

        [Header("Stairs")]
        public ObjectSnapshot stairsUp;
        public List<ObjectSnapshot> stairSteps = new List<ObjectSnapshot>();

        [Header("Machines")]
        public List<ObjectSnapshot> machines = new List<ObjectSnapshot>();

        [Header("Lights")]
        public List<ObjectSnapshot> lights = new List<ObjectSnapshot>();

        [Header("Other Objects")]
        public List<ObjectSnapshot> otherObjects = new List<ObjectSnapshot>();

        /// <summary>
        /// Captures the current runtime layout.
        /// Call this while in Play Mode.
        /// </summary>
        public void CaptureCurrentLayout()
        {
            captureTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            Debug.Log($"[BasementLayoutSnapshot] Starting capture at {captureTime}");

            // Clear existing data
            floorRingSegments.Clear();
            stairSteps.Clear();
            machines.Clear();
            lights.Clear();
            otherObjects.Clear();

            // Find Basement root
            GameObject basement = GameObject.Find("Basement");
            if (basement != null)
            {
                basementRoot = new ObjectSnapshot(basement);
                Debug.Log($"[BasementLayoutSnapshot] Found Basement root: {basementRoot}");
            }
            else
            {
                Debug.LogWarning("[BasementLayoutSnapshot] Basement root not found!");
            }

            // Capture main structure
            CaptureObject("BasementFloor", ref basementFloor, basement?.transform);
            CaptureObject("BasementCeiling", ref basementCeiling, basement?.transform);

            // Capture walls
            CaptureObject("Basement_Wall_North", ref wallNorth, basement?.transform);
            CaptureObject("Basement_Wall_South", ref wallSouth, basement?.transform);
            CaptureObject("Basement_Wall_East", ref wallEast, basement?.transform);
            CaptureObject("Basement_Wall_West", ref wallWest, basement?.transform);

            // Capture floor ring
            CaptureFloorRing();

            // Capture stairs
            CaptureStairs(basement?.transform);

            // Capture machines
            CaptureMachines();

            // Capture lights
            CaptureLights(basement?.transform);

            Debug.Log($"[BasementLayoutSnapshot] Capture complete!");
            Debug.Log($"[BasementLayoutSnapshot] - Floor: {(basementFloor != null ? "YES" : "NO")}");
            Debug.Log($"[BasementLayoutSnapshot] - Ceiling: {(basementCeiling != null ? "YES" : "NO")}");
            Debug.Log($"[BasementLayoutSnapshot] - Walls: N={wallNorth != null}, S={wallSouth != null}, E={wallEast != null}, W={wallWest != null}");
            Debug.Log($"[BasementLayoutSnapshot] - Floor Ring Segments: {floorRingSegments.Count}");
            Debug.Log($"[BasementLayoutSnapshot] - Stair Steps: {stairSteps.Count}");
            Debug.Log($"[BasementLayoutSnapshot] - Machines: {machines.Count}");
            Debug.Log($"[BasementLayoutSnapshot] - Lights: {lights.Count}");
        }

        private void CaptureObject(string name, ref ObjectSnapshot snapshot, Transform parent)
        {
            GameObject obj = null;

            // Try to find under parent first
            if (parent != null)
            {
                Transform found = parent.Find(name);
                if (found != null)
                    obj = found.gameObject;
            }

            // Fall back to global search
            if (obj == null)
            {
                obj = GameObject.Find(name);
            }

            if (obj != null)
            {
                snapshot = new ObjectSnapshot(obj);
                Debug.Log($"[BasementLayoutSnapshot] Captured {name}: {snapshot.transform}");
            }
            else
            {
                Debug.LogWarning($"[BasementLayoutSnapshot] {name} not found!");
                snapshot = null;
            }
        }

        private void CaptureFloorRing()
        {
            // Find floor ring root
            GameObject ringRoot = GameObject.Find("BasementFloor_Ring");
            if (ringRoot != null)
            {
                floorRingRoot = new ObjectSnapshot(ringRoot);
                Debug.Log($"[BasementLayoutSnapshot] Found BasementFloor_Ring with {ringRoot.transform.childCount} children");

                // Capture all children
                foreach (Transform child in ringRoot.transform)
                {
                    ObjectSnapshot childSnapshot = new ObjectSnapshot(child.gameObject);
                    floorRingSegments.Add(childSnapshot);
                    Debug.Log($"[BasementLayoutSnapshot] Captured ring segment: {childSnapshot}");
                }
            }
            else
            {
                // Try individual floor segments
                string[] segmentNames = { "Floor_North", "Floor_South", "Floor_East", "Floor_West" };
                foreach (string name in segmentNames)
                {
                    GameObject segment = GameObject.Find(name);
                    if (segment != null)
                    {
                        ObjectSnapshot segSnapshot = new ObjectSnapshot(segment);
                        floorRingSegments.Add(segSnapshot);
                        Debug.Log($"[BasementLayoutSnapshot] Captured standalone ring segment: {segSnapshot}");
                    }
                }
            }
        }

        private void CaptureStairs(Transform basementParent)
        {
            // Find StairsUp
            GameObject stairs = null;
            if (basementParent != null)
            {
                Transform found = basementParent.Find("StairsUp");
                if (found != null)
                    stairs = found.gameObject;
            }

            if (stairs == null)
                stairs = GameObject.Find("StairsUp");

            if (stairs != null)
            {
                stairsUp = new ObjectSnapshot(stairs);
                Debug.Log($"[BasementLayoutSnapshot] Found StairsUp: {stairsUp}");

                // Capture stair steps
                foreach (Transform child in stairs.transform)
                {
                    if (child.name.StartsWith("Step_") || child.name.Contains("Step"))
                    {
                        ObjectSnapshot stepSnapshot = new ObjectSnapshot(child.gameObject);
                        stairSteps.Add(stepSnapshot);
                    }
                }
                Debug.Log($"[BasementLayoutSnapshot] Captured {stairSteps.Count} stair steps");
            }
        }

        private void CaptureMachines()
        {
            string[] machineNames = {
                "Workbench",
                "Refinery",
                "UpgradeStation",
                "EnergyGenerator",
                "TradeTerminal",
                "StorageCrate",
                "FuelStation"
            };

            // First try to find Machines container
            GameObject machinesContainer = GameObject.Find("Machines");
            Transform machinesParent = machinesContainer?.transform;

            foreach (string name in machineNames)
            {
                GameObject machine = null;

                // Try under Machines container first
                if (machinesParent != null)
                {
                    Transform found = machinesParent.Find(name);
                    if (found != null)
                        machine = found.gameObject;
                }

                // Fall back to global search
                if (machine == null)
                    machine = GameObject.Find(name);

                if (machine != null)
                {
                    ObjectSnapshot machineSnapshot = new ObjectSnapshot(machine);
                    machines.Add(machineSnapshot);
                    Debug.Log($"[BasementLayoutSnapshot] Captured machine: {machineSnapshot}");
                }
            }
        }

        private void CaptureLights(Transform basementParent)
        {
            string[] lightNames = {
                "BasementLight_1",
                "BasementLight_2",
                "BasementLight_Stairs",
                "BasementLight1",
                "BasementLight2"
            };

            foreach (string name in lightNames)
            {
                GameObject lightObj = null;

                if (basementParent != null)
                {
                    Transform found = basementParent.Find(name);
                    if (found != null)
                        lightObj = found.gameObject;
                }

                if (lightObj == null)
                    lightObj = GameObject.Find(name);

                if (lightObj != null)
                {
                    ObjectSnapshot lightSnapshot = new ObjectSnapshot(lightObj);
                    lights.Add(lightSnapshot);
                    Debug.Log($"[BasementLayoutSnapshot] Captured light: {lightSnapshot}");
                }
            }
        }

        /// <summary>
        /// Gets a summary of this snapshot for logging.
        /// </summary>
        public string GetSummary()
        {
            return $"BasementLayoutSnapshot captured at {captureTime}\n" +
                   $"Scene: {sceneName}\n" +
                   $"Floor: {basementFloor?.transform}\n" +
                   $"Ceiling: {basementCeiling?.transform}\n" +
                   $"Walls: N={wallNorth != null}, S={wallSouth != null}, E={wallEast != null}, W={wallWest != null}\n" +
                   $"Floor Ring Segments: {floorRingSegments.Count}\n" +
                   $"Machines: {machines.Count}\n" +
                   $"Lights: {lights.Count}";
        }
    }
}
