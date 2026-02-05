using UnityEngine;

namespace BeneathTheFloor.Robot
{
    [CreateAssetMenu(fileName = "DiggerRobotConfig", menuName = "BeneathTheFloor/Digger Robot Config")]
    public class DiggerRobotConfig : ScriptableObject
    {
        [Header("Digging")]
        public float digRadius = 1.2f;
        public float digStrength = 0.25f;
        public float digInterval = 0.15f;
        public float digReach = 0.6f;

        [Header("Movement")]
        public float moveSpeed = 1f;
        public float breadcrumbSpacing = 1.5f;
        public float robotMass = 20f;
        public float robotFriction = 0.3f;

        [Header("Climbing (Return)")]
        [Tooltip("Vertical climb speed when returning upward (m/s)")]
        public float climbSpeed = 2f;
        [Tooltip("Height difference threshold to trigger climb mode (m)")]
        public float climbHeightThreshold = 0.5f;
        [Tooltip("How close to a wall to start climbing (m)")]
        public float wallDetectionDist = 0.8f;

        [Header("Obstacle Avoidance")]
        [Tooltip("SphereCast radius for obstacle detection")]
        public float obstacleCheckRadius = 0.5f;
        [Tooltip("Distance ahead to check for obstacles")]
        public float obstacleCheckDist = 2f;
        [Tooltip("Very short range — if terrain is this close, block movement and dig immediately")]
        public float terrainContactDist = 0.4f;

        [Header("AI Terrain Scan")]
        [Tooltip("How far the 180° fan scan looks for terrain")]
        public float scanRange = 8f;
        [Tooltip("Angle step for fan scan (degrees). Smaller = more accurate but heavier")]
        public float scanAngleStep = 10f;
        [Tooltip("Distance to maintain from terrain before digging (meters)")]
        public float digStopDistance = 0.5f;
        [Tooltip("How often the robot re-scans for terrain (seconds)")]
        public float scanInterval = 0.3f;

        [Header("Layer Sweep")]
        public float layerHeight = 1.8f;
        public float holeDetectDepth = 2.5f;
        public float layerClearTimeout = 15f;

        [Header("Dig Animation")]
        public float digCycleDuration = 0.7f;
        public float armOpenAngle = 15f;
        public float armCloseAngle = -20f;
        public float bodyTiltAngle = 10f;
        public float ballRollFactor = 180f;

        [Header("Stuck Recovery")]
        [Tooltip("Seconds without meaningful movement before recovery kicks in")]
        public float stuckTimeout = 5f;
        [Tooltip("Minimum distance (m) the robot must move within stuckTimeout")]
        public float stuckMinMove = 0.3f;
        [Tooltip("How long the robot reverses when stuck (seconds)")]
        public float reverseTime = 1f;

        [Header("Battery")]
        public float maxBattery = 100f;
        public float drainPerSecond = 0.3f;
        public float drainPerDig = 2f;
        [Range(0f, 1f)]
        public float lowBatteryThreshold = 0.15f;
        public float rechargeRate = 25f;
    }
}
