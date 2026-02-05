using UnityEngine;

namespace BeneathTheFloor.Robot
{
    /// <summary>
    /// DEPRECATED — replaced by RobotWaypointPath + RobotGate system.
    /// Kept as stub to prevent missing-script errors on existing scene objects.
    /// Safe to remove once the scene GameObject is deleted.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RobotContainmentZone : MonoBehaviour
    {
        public static RobotContainmentZone Instance { get; private set; }

        public Bounds ZoneBounds => GetComponent<BoxCollider>().bounds;

        public bool ContainsXZ(Vector3 pos, float margin = 1f)
        {
            Bounds b = ZoneBounds;
            return pos.x >= b.min.x - margin && pos.x <= b.max.x + margin
                && pos.z >= b.min.z - margin && pos.z <= b.max.z + margin;
        }

        private void Awake()
        {
            Instance = this;
            Debug.LogWarning("[RobotContainmentZone] DEPRECATED — replace with RobotWaypointPath + RobotGate. This stub will be removed in a future update.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
