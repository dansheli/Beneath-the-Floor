using UnityEngine;
using System.Collections;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Machines;
using BeneathTheFloor.Robot;

namespace BeneathTheFloor.Logistics
{
    public enum LogisticsState
    {
        Docked, Idle, FollowPlayer, WorkWithDigger, CollectingPickup,
        TravelToUnload, Unloading, TravelToRecharge, Recharging,
        FetchingItem, DeliveringItem, Carried, Shutdown
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(LogisticsBattery))]
    [RequireComponent(typeof(RobotCargoBuffer))]
    [RequireComponent(typeof(LootScanner))]
    [RequireComponent(typeof(LogisticsArmAnimator))]
    public class LogisticsRobotController : MonoBehaviour, IInteractable
    {
        [SerializeField] private LogisticsRobotConfig config;
        [SerializeField] private LogisticsRobotDock dock;
        [SerializeField] private DropOffContainer container;

        public LogisticsState CurrentState { get; private set; } = LogisticsState.Docked;
        public LogisticsRobotConfig Config => config;
        public float BatteryRatio => battery != null ? battery.Ratio : 0f;

        private Rigidbody rb;
        private LogisticsBattery battery;
        private RobotCargoBuffer cargo;
        private LootScanner scanner;
        private LogisticsArmAnimator armAnimator;

        // Mode: what the robot does when not collecting/unloading
        public enum RobotMode { Idle, FollowPlayer, WorkWithDigger }
        private RobotMode activeMode = RobotMode.Idle;
        public RobotMode ActiveMode => activeMode;

        // Stuck detection
        private Vector3 stuckCheckPos;
        private float stuckTimer;

        // Follow movement tracking (deadzone to prevent jitter)
        private bool isFollowMoving;

        // Collect coroutine
        private Coroutine collectCoroutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            battery = GetComponent<LogisticsBattery>();
            cargo = GetComponent<RobotCargoBuffer>();
            scanner = GetComponent<LootScanner>();
            armAnimator = GetComponent<LogisticsArmAnimator>();

            if (GetComponent<LogisticsRobotHUD>() == null) gameObject.AddComponent<LogisticsRobotHUD>();
            if (GetComponent<LogisticsRobotMarker>() == null) gameObject.AddComponent<LogisticsRobotMarker>();
        }

        private void Start()
        {
            if (config != null)
            {
                battery.Init(config);
                cargo.Init(config);
                scanner.Init(config);
                armAnimator.Init(config);
            }

            rb.isKinematic = true;
            rb.useGravity = false;

            // Initialize layer masks early
            InitializeLayerMasks();
        }

        private void InitializeLayerMasks()
        {
            // Movement collision mask - exclude robot and player
            int robotLayer = LayerMask.NameToLayer("Robot");
            int playerLayer = LayerMask.NameToLayer("Player");
            _moveLayerMask = ~0;
            if (robotLayer >= 0) _moveLayerMask &= ~(1 << robotLayer);
            if (playerLayer >= 0) _moveLayerMask &= ~(1 << playerLayer);
            _moveLayerMask &= ~(1 << 11);

            // Ground detection mask
            int terrain = LayerMask.NameToLayer("Terrain");
            int diggable = LayerMask.NameToLayer("Diggable");
            int floor = LayerMask.NameToLayer("Floor");
            int defaultLayer = 0;
            _groundLayerMask = (1 << defaultLayer);
            if (terrain >= 0) _groundLayerMask |= (1 << terrain);
            if (diggable >= 0) _groundLayerMask |= (1 << diggable);
            if (floor >= 0) _groundLayerMask |= (1 << floor);
        }

        // ==================================================================
        // PUBLIC API
        // ==================================================================

        // Deploy animation
        private Coroutine deployCoroutine;

        public void Deploy()
        {
            if (CurrentState != LogisticsState.Docked) return;
            rb.isKinematic = true;
            rb.useGravity = false;

            CurrentState = LogisticsState.Idle;
            stuckCheckPos = transform.position;
            stuckTimer = 0f;

            // Smoothly roll off the dock
            if (deployCoroutine != null) StopCoroutine(deployCoroutine);
            deployCoroutine = StartCoroutine(DeployMoveRoutine());
        }

        private IEnumerator DeployMoveRoutine()
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + transform.forward * 1.5f;
            float duration = 1.2f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float smooth = t * t * (3f - 2f * t); // smoothstep
                transform.position = Vector3.Lerp(startPos, endPos, smooth);
                yield return null;
            }
            transform.position = endPos;
            deployCoroutine = null;
            ApplyMode(activeMode);
        }

        public void SetMode(RobotMode mode)
        {
            activeMode = mode;
            if (CurrentState == LogisticsState.FollowPlayer || CurrentState == LogisticsState.WorkWithDigger || CurrentState == LogisticsState.Idle)
            {
                ApplyMode(mode);
            }
        }

        public void ForceUnload()
        {
            if (CurrentState == LogisticsState.Docked || CurrentState == LogisticsState.Carried || CurrentState == LogisticsState.Shutdown) return;
            if (cargo.IsEmpty) return;
            CurrentState = LogisticsState.TravelToUnload;
        }

        public void Dock()
        {
            CancelCollect();
            CurrentState = LogisticsState.Recharging;
            rb.isKinematic = true;
            if (dock != null)
            {
                Transform dp = dock.GetDockPoint();
                float hoverY = config != null ? config.hoverHeight : 0.5f;
                Vector3 dockPos = dp.position + Vector3.up * hoverY;

                // Face the direction from dock toward where the robot currently is
                Vector3 lookDir = transform.position - dp.position;
                lookDir.y = 0f;
                Quaternion dockRot = lookDir.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                    : dp.rotation;

                StartCoroutine(AnimateTo(dockPos, dockRot, 0.8f));
            }
            Debug.Log("[LogisticsRobot] Docked -- recharging");
        }

        public void PickUp()
        {
            CancelCollect();
            CurrentState = LogisticsState.Carried;
            rb.isKinematic = true;
        }

        // ==================================================================
        // UPDATE
        // ==================================================================

        private void Update()
        {
            switch (CurrentState)
            {
                case LogisticsState.FollowPlayer:
                    UpdateFollowPlayer();
                    break;
                case LogisticsState.WorkWithDigger:
                    UpdateFollowDigger();
                    break;
                case LogisticsState.Idle:
                    UpdateIdle();
                    break;
                case LogisticsState.CollectingPickup:
                    // Handled by coroutine
                    break;
                case LogisticsState.TravelToUnload:
                    UpdateTravelToUnload();
                    break;
                case LogisticsState.Unloading:
                    DoUnload();
                    break;
                case LogisticsState.TravelToRecharge:
                    UpdateTravelToRecharge();
                    break;
                case LogisticsState.Recharging:
                    UpdateRecharging();
                    break;
            }

            // Battery drain for active states
            if (IsActiveState())
            {
                float drainRate = activeMode == RobotMode.WorkWithDigger
                    ? config.drainPerSecondModeB
                    : config.drainPerSecondModeA;
                battery.DrainPassive(Time.deltaTime, drainRate);

                if (battery.IsDepleted)
                {
                    if (FirstRoomUpgradeStationUI.LogisticsAutonomous)
                    {
                        // Autonomous: emergency return to dock
                        CancelCollect();
                        CurrentState = LogisticsState.TravelToRecharge;
                    }
                    else
                    {
                        // No autonomous upgrade: shut down in place
                        EnterShutdown();
                    }
                    return;
                }
                if (FirstRoomUpgradeStationUI.LogisticsAutonomous
                    && battery.IsLow && CurrentState != LogisticsState.TravelToRecharge)
                {
                    CancelCollect();
                    CurrentState = LogisticsState.TravelToRecharge;
                    Debug.Log("[LogisticsRobot] Battery low -- returning to dock");
                    return;
                }
            }

            // Stuck detection only when actively moving (not idle within deadzone)
            if (isFollowMoving || CurrentState == LogisticsState.TravelToUnload
                || CurrentState == LogisticsState.TravelToRecharge || CurrentState == LogisticsState.CollectingPickup)
            {
                UpdateStuckDetection();
            }
            else
            {
                stuckTimer = 0f;
                stuckCheckPos = transform.position;
            }

            // Check for pickup opportunities during follow states and idle
            if (CurrentState == LogisticsState.FollowPlayer || CurrentState == LogisticsState.WorkWithDigger || CurrentState == LogisticsState.Idle)
            {
                TryStartCollect();
            }

        }

        // ==================================================================
        // FOLLOW PLAYER
        // ==================================================================

        private void UpdateFollowPlayer()
        {
            Transform player = GetPlayerTransform();
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > config.followPlayerDist)
                isFollowMoving = true;
            else if (dist < config.followPlayerDist * 0.5f)
                isFollowMoving = false;

            if (isFollowMoving)
                MoveToward(player.position, config.moveSpeed);

            scanner.SetRadius(config.collectRadius);
        }

        // ==================================================================
        // FOLLOW DIGGER
        // ==================================================================

        private void UpdateFollowDigger()
        {
            var digger = FindObjectOfType<DiggerRobotStateMachine>();
            if (digger == null)
            {
                UpdateFollowPlayer();
                return;
            }

            float dist = Vector3.Distance(transform.position, digger.transform.position);
            // Deadzone: start moving at full distance, stop at half distance
            if (dist > config.followDiggerDist)
                isFollowMoving = true;
            else if (dist < config.followDiggerDist * 0.5f)
                isFollowMoving = false;

            if (isFollowMoving)
                MoveToward(digger.transform.position, config.moveSpeed);

            scanner.SetRadius(config.collectRadiusDigger);
        }

        // ==================================================================
        // IDLE
        // ==================================================================

        private void UpdateIdle()
        {
            scanner.SetRadius(config.collectRadius);
        }

        // ==================================================================
        // COLLECT
        // ==================================================================

        private void TryStartCollect()
        {
            if (collectCoroutine != null) return;

            var target = scanner.CurrentTarget;
            if (target == null || !target.IsValid) return;

            if (!cargo.CanAccept(target.ResourceId))
            {
                if (cargo.IsFull)
                {
                    CurrentState = LogisticsState.TravelToUnload;
                    Debug.Log("[LogisticsRobot] Cargo full -- heading to container");
                }
                return;
            }

            Debug.Log($"[LogisticsRobot] Starting collection of {target.ResourceId} at {target.Position:F1}");
            collectCoroutine = StartCoroutine(CollectRoutine(target));
        }

        private IEnumerator CollectRoutine(IPickupAdapter target)
        {
            var prevState = CurrentState;
            CurrentState = LogisticsState.CollectingPickup;

            // Move toward pickup
            while (target.IsValid)
            {
                float dist = Vector3.Distance(transform.position, target.Position);
                if (dist < 1.0f) break;
                MoveToward(target.Position, config.moveSpeed);
                yield return null;
            }

            if (target.IsValid)
            {
                // Play arm animation
                armAnimator.PlayCollectAnimation();
                yield return new WaitForSeconds(config.armAnimDuration * 2f + 0.1f);

                if (target.IsValid)
                {
                    int amount = target.Amount;
                    string resId = target.ResourceId;
                    int tier = target.Tier;
                    int creditVal = target.CreditValue;

                    if (cargo.TryAddCargo(resId, tier, amount, creditVal))
                    {
                        battery.Drain(config.drainPerCollect);
                        target.Collect();
                        Debug.Log($"[LogisticsRobot] Collected {resId} x{amount}");
                    }
                }
            }

            scanner.ReleaseClaim();
            collectCoroutine = null;

            // Decide next state
            if (cargo.IsFull)
            {
                CurrentState = LogisticsState.TravelToUnload;
            }
            else
            {
                ApplyMode(activeMode);
            }
        }

        private void CancelCollect()
        {
            if (collectCoroutine != null)
            {
                StopCoroutine(collectCoroutine);
                collectCoroutine = null;
            }
            scanner.ReleaseClaim();
        }

        // ==================================================================
        // TRAVEL TO UNLOAD
        // ==================================================================

        private Vector3 _containerApproachPos;
        private bool _containerApproachCalculated;

        private void UpdateTravelToUnload()
        {
            if (container == null)
            {
                // No container -- just dump cargo
                cargo.Clear();
                ApplyMode(activeMode);
                return;
            }

            // Calculate approach position once when entering this state
            if (!_containerApproachCalculated)
            {
                _containerApproachPos = CalculateContainerApproachPosition();
                _containerApproachCalculated = true;
            }

            float dist = Vector3.Distance(transform.position, _containerApproachPos);
            if (dist < 0.8f)
            {
                _containerApproachCalculated = false; // Reset for next time
                CurrentState = LogisticsState.Unloading;
                return;
            }

            MoveToward(_containerApproachPos, config.unloadTravelSpeed);
        }

        private Vector3 CalculateContainerApproachPosition()
        {
            // Ensure layer masks are initialized
            if (_groundLayerMask == -1)
                InitializeLayerMasks();

            Vector3 containerCenter = container.transform.position;
            Vector3 robotPos = transform.position;

            // Direction from container to robot (we approach from this side)
            Vector3 dirToRobot = robotPos - containerCenter;
            dirToRobot.y = 0f;

            // Default approach distance
            float approachDist = 1.5f;

            // Try to get bounds from collider
            Collider col = container.GetComponent<Collider>();
            if (col != null)
            {
                Bounds bounds = col.bounds;
                // Use half the largest horizontal extent plus some margin
                float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);
                approachDist = extent + 0.8f;
            }

            // If robot is very close or directly above/below, pick a default direction
            if (dirToRobot.sqrMagnitude < 0.1f)
            {
                dirToRobot = container.transform.forward;
            }

            Vector3 approachPos = containerCenter + dirToRobot.normalized * approachDist;

            // Keep same Y as robot's current hover height
            float hoverHeight = config != null ? config.hoverHeight : 0.5f;

            // Find ground below approach position
            if (Physics.Raycast(approachPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f, _groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                approachPos.y = hit.point.y + hoverHeight;
            }
            else
            {
                approachPos.y = transform.position.y;
            }

            return approachPos;
        }

        private void DoUnload()
        {
            if (container != null)
            {
                container.ReceiveCargo(cargo.GetAllCargo());
            }
            cargo.Clear();
            Debug.Log("[LogisticsRobot] Unloaded cargo");

            if (battery.IsLow)
            {
                CurrentState = LogisticsState.TravelToRecharge;
            }
            else
            {
                ApplyMode(activeMode);
            }
        }

        // ==================================================================
        // RECHARGE
        // ==================================================================

        private void UpdateTravelToRecharge()
        {
            if (dock == null)
            {
                EnterShutdown();
                return;
            }

            Transform dp = dock.GetDockPoint();

            // Use horizontal distance only (ignore Y) since robot hovers above ground
            // This prevents the robot from getting stuck when dock point is at ground level
            Vector3 robotPosFlat = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 dockPosFlat = new Vector3(dp.position.x, 0f, dp.position.z);
            float horizontalDist = Vector3.Distance(robotPosFlat, dockPosFlat);

            // Increased threshold to account for hover height variance
            if (horizontalDist < 1.5f)
            {
                Dock();
                return;
            }

            MoveToward(dp.position, config.unloadTravelSpeed);
        }

        private void UpdateRecharging()
        {
            battery.Recharge(Time.deltaTime);
            if (battery.IsFull)
            {
                // Auto-redeploy if autonomous upgrade is active
                if (FirstRoomUpgradeStationUI.LogisticsAutonomous && activeMode != RobotMode.Idle)
                {
                    CurrentState = LogisticsState.Docked;
                    Deploy();
                    ApplyMode(activeMode);
                    Debug.Log("[LogisticsRobot] Fully recharged -- auto-redeploying");
                }
                else
                {
                    CurrentState = LogisticsState.Docked;
                    Debug.Log("[LogisticsRobot] Fully recharged -- ready");
                }
            }
        }

        // ==================================================================
        // MOVEMENT
        // ==================================================================

        private int _moveLayerMask = -1;
        private int _groundLayerMask = -1;

        // Fan-ray angles for obstacle avoidance (degrees offset from target direction)
        private static readonly float[] _avoidAngles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 110f, -110f, 140f, -140f, 180f };

        private void MoveToward(Vector3 target, float speed)
        {
            float hoverHeight = config != null ? config.hoverHeight : 0.5f;

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            Vector3 desiredDir = dir.normalized;
            float moveStep = speed * Time.deltaTime;

            // Ensure layer masks are initialized (normally done in Start)
            if (_moveLayerMask == -1 || _groundLayerMask == -1)
                InitializeLayerMasks();

            // OBSTACLE AVOIDANCE: Cast rays in a fan pattern to find the best open direction
            float probeDistance = Mathf.Max(moveStep + 0.5f, 1.2f); // Look ahead at least 1.2m
            Vector3 chosenDir = Vector3.zero;

            for (int i = 0; i < _avoidAngles.Length; i++)
            {
                Vector3 testDir = Quaternion.Euler(0f, _avoidAngles[i], 0f) * desiredDir;

                // Test at two heights (low and mid) to catch obstacles of varying height
                bool blocked = false;
                Vector3 rayLow = transform.position + Vector3.up * 0.1f;
                Vector3 rayMid = transform.position + Vector3.up * 0.4f;

                if (Physics.Raycast(rayLow, testDir, out RaycastHit hitLow, probeDistance, _moveLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (Mathf.Abs(hitLow.normal.y) < 0.7f) // Wall, not floor
                        blocked = true;
                }
                if (!blocked && Physics.Raycast(rayMid, testDir, out RaycastHit hitMid, probeDistance, _moveLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (Mathf.Abs(hitMid.normal.y) < 0.7f)
                        blocked = true;
                }

                if (!blocked)
                {
                    chosenDir = testDir;
                    break; // First open direction wins (closest to desired)
                }
            }

            Vector3 newPos;
            if (chosenDir.sqrMagnitude < 0.01f)
            {
                // All directions blocked - don't move
                newPos = transform.position;
            }
            else
            {
                // Face chosen movement direction
                Quaternion targetRot = Quaternion.LookRotation(chosenDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);

                // Slow down when steering away from desired direction
                float dot = Vector3.Dot(desiredDir, chosenDir);
                float speedMult = Mathf.Lerp(0.4f, 1.0f, Mathf.Clamp01((dot + 1f) * 0.5f));

                newPos = transform.position + chosenDir * (moveStep * speedMult);
            }

            // GROUND DETECTION: Simple raycast DOWN from robot position
            float groundY = float.MinValue;

            Vector3 groundRayOrigin = newPos + Vector3.up * 0.5f;
            if (Physics.Raycast(groundRayOrigin, Vector3.down, out RaycastHit groundHit, 15f, _groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (groundHit.point.y < newPos.y + 0.5f)
                {
                    groundY = groundHit.point.y;
                }
            }

            // Calculate desired Y
            float desiredY;
            if (groundY > float.MinValue)
            {
                float targetY = groundY + hoverHeight;
                float heightDiff = targetY - transform.position.y;
                float maxChange = 2f * Time.deltaTime;

                if (Mathf.Abs(heightDiff) < maxChange)
                    desiredY = targetY;
                else if (heightDiff > 0)
                    desiredY = transform.position.y + maxChange;
                else
                    desiredY = transform.position.y - maxChange;
            }
            else
            {
                desiredY = transform.position.y;
            }

            newPos.y = desiredY;
            transform.position = newPos;
        }

        // ==================================================================
        // STUCK DETECTION
        // ==================================================================

        private void UpdateStuckDetection()
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= config.stuckTimeout)
            {
                float moved = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(stuckCheckPos.x, 0f, stuckCheckPos.z));

                if (moved < config.stuckMinMove)
                {
                    Debug.Log("[LogisticsRobot] Stuck -- finding clear direction");

                    // Try to find an open direction to nudge toward (not random)
                    if (_moveLayerMask == -1) InitializeLayerMasks();
                    Vector3 nudgeDir = Vector3.zero;
                    float probeLen = 1.5f;

                    // Try 8 compass directions and pick the first clear one
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * 45f;
                        Vector3 testDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;

                        if (!Physics.Raycast(rayOrigin, testDir, probeLen, _moveLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            nudgeDir = testDir;
                            break;
                        }
                    }

                    if (nudgeDir.sqrMagnitude > 0.01f)
                    {
                        transform.position += nudgeDir * 0.8f;
                    }
                }

                stuckCheckPos = transform.position;
                stuckTimer = 0f;
            }
        }

        // ==================================================================
        // IInteractable
        // ==================================================================

        public bool CanInteract => CurrentState != LogisticsState.Docked && CurrentState != LogisticsState.Carried && CurrentState != LogisticsState.Recharging;

        public string GetInteractionText()
        {
            if (!CanInteract) return "";
            if (CurrentState == LogisticsState.Shutdown)
                return "[E] Pick Up Robot";
            string modeStr = activeMode == RobotMode.FollowPlayer ? "Following"
                : activeMode == RobotMode.WorkWithDigger ? "With Digger"
                : "Idle";
            return $"[E] Robot Control ({modeStr})";
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            // Shutdown: pick up robot instead of opening UI
            if (CurrentState == LogisticsState.Shutdown)
            {
                var carry = interactor.GetComponent<LogisticsRobotCarry>();
                if (carry == null)
                    carry = interactor.AddComponent<LogisticsRobotCarry>();
                carry.PickUpRobot(this);
                return;
            }

            // Open popup UI for mode selection, cargo info, commands
            var popup = LogisticsRobotPopupUI.Instance;
            if (popup != null)
            {
                popup.ShowUI(this);
            }
            else
            {
                // Fallback: find or create popup component
                var existing = FindObjectOfType<LogisticsRobotPopupUI>();
                if (existing == null)
                {
                    var obj = new GameObject("LogisticsRobotPopupUI");
                    existing = obj.AddComponent<LogisticsRobotPopupUI>();
                }
                existing.ShowUI(this);
            }
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        // ==================================================================
        // HELPERS
        // ==================================================================

        private void ApplyMode(RobotMode mode)
        {
            // Reset container approach when changing modes
            _containerApproachCalculated = false;

            switch (mode)
            {
                case RobotMode.Idle:
                    CurrentState = LogisticsState.Idle;
                    break;
                case RobotMode.FollowPlayer:
                    CurrentState = LogisticsState.FollowPlayer;
                    break;
                case RobotMode.WorkWithDigger:
                    CurrentState = LogisticsState.WorkWithDigger;
                    break;
            }
        }

        private bool IsActiveState()
        {
            return CurrentState != LogisticsState.Docked
                && CurrentState != LogisticsState.Carried
                && CurrentState != LogisticsState.Shutdown
                && CurrentState != LogisticsState.Recharging;
        }

        private bool IsMovementState()
        {
            return CurrentState == LogisticsState.FollowPlayer
                || CurrentState == LogisticsState.WorkWithDigger
                || CurrentState == LogisticsState.TravelToUnload
                || CurrentState == LogisticsState.TravelToRecharge
                || CurrentState == LogisticsState.CollectingPickup;
        }

        private void EnterShutdown()
        {
            CancelCollect();
            CurrentState = LogisticsState.Shutdown;
            Debug.Log("[LogisticsRobot] Shutdown -- battery depleted");
        }

        private Transform GetPlayerTransform()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Walk up to find the root player object
                Transform t = cam.transform;
                while (t.parent != null) t = t.parent;
                return t;
            }
            return null;
        }

        private IEnumerator AnimateTo(Vector3 targetPos, Quaternion targetRot, float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float smooth = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPos, targetPos, smooth);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
                yield return null;
            }
            transform.position = targetPos;
            transform.rotation = targetRot;
        }

        public string GetStateName()
        {
            return CurrentState.ToString();
        }

        public RobotCargoBuffer GetCargo() => cargo;
    }
}
