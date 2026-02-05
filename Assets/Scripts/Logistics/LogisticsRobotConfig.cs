using UnityEngine;

namespace BeneathTheFloor.Logistics
{
    [CreateAssetMenu(fileName = "LogisticsRobotConfig", menuName = "BeneathTheFloor/Logistics Robot Config")]
    public class LogisticsRobotConfig : ScriptableObject
    {
        [Header("Battery")]
        public float maxBattery = 100f;
        public float drainPerSecondModeA = 2f;
        public float drainPerSecondModeB = 1.5f;
        public float drainPerCollect = 3f;
        [Range(0f, 1f)]
        public float lowBatteryThreshold = 0.15f;
        public float rechargeRate = 25f;

        [Header("Movement")]
        public float hoverHeight = 0.5f;
        public float moveSpeed = 2f;
        public float followPlayerDist = 3f;
        public float followDiggerDist = 1.5f;
        public float collectRadius = 5f;
        public float collectRadiusDigger = 8f;

        [Header("Cargo")]
        public int baseDistinctTypeSlots = 1;
        public int totalCapacityPerType = 5;

        [Header("Scan")]
        public float scanInterval = 0.5f;
        public float noPickupRepositionTime = 8f;

        [Header("Stuck Recovery")]
        public float stuckTimeout = 4f;
        public float stuckMinMove = 0.3f;

        [Header("Arm Animation")]
        public float armCloseAngle = 20f;
        public float armAnimDuration = 0.15f;

        [Header("Unload")]
        public float unloadTravelSpeed = 2.5f;
    }
}
