using UnityEngine;
using System.Collections;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Robot
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(DiggerRobotBattery))]
    [RequireComponent(typeof(DiggerRobotBreadcrumbs))]
    public class DiggerRobotStateMachine : MonoBehaviour, IInteractable
    {
        public enum State { Docked, Digging, Returning, Shutdown, Carried, Recharging }

        /// <summary>Sub-states within Digging to prevent overlapping logic.</summary>
        private enum DigSubState { MovingToSite, ActiveDig, Sweeping, Reversing }

        [SerializeField] private DiggerRobotConfig config;
        [SerializeField] private DiggerRobotDock dock;
        [SerializeField] private RobotWaypointPath waypointPath;
        [SerializeField] private RobotGate gate;

        public State CurrentState { get; private set; } = State.Docked;
        public float BatteryRatio => battery != null ? battery.Ratio : 0f;
        public DiggerRobotConfig Config => config;

        private Rigidbody rb;
        private Collider col;
        private DiggerRobotBattery battery;
        private DiggerRobotBreadcrumbs breadcrumbs;
        private DiggingSystem diggingSystem;
        private DiggerRobotVisual visual;

        private float digTimer;
        private Vector3? returnTarget;

        // Terrain layer for physics queries
        private int terrainLayer;
        private LayerMask terrainLayerMask;

        // Digging AI state
        private DigSubState digSubState;
        private bool reachedDigArea;
        private float targetYaw;
        private bool yawInitialized;
        private float turnCooldown;
        private bool hasEncounteredTerrain;
        private float debugLogTimer;

        // Waypoint navigation
        private int currentWaypointIndex;

        // AI terrain scan
        private Vector3? scanTarget;       // nearest terrain found by fan scan
        private float scanTimer;

        // Dig cycle animation
        private bool isPerformingDig;
        private Coroutine activeDigCoroutine;

        // Stuck detection (universal — works in all sub-states)
        private Vector3 stuckCheckPos;
        private float stuckTimer;

        // Reverse recovery
        private float reverseTimer;

        // Deploy grace — skip terrain blocking for a brief period after deploy
        private float deployGraceTimer;

        // Obstacle bypass — single arc waypoint: diagonal past obstacle
        private Vector3? bypassWaypoint;
        private const float BypassSideOffset = 0.75f;
        private const float BypassForwardOffset = 2.5f;

        // Resume-after-recharge
        private Vector3 savedResumePos;
        private float savedResumeYaw;
        private bool hasResumePoint;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            battery = GetComponent<DiggerRobotBattery>();
            breadcrumbs = GetComponent<DiggerRobotBreadcrumbs>();
            visual = GetComponent<DiggerRobotVisual>();

            if (GetComponent<DiggerRobotHUD>() == null) gameObject.AddComponent<DiggerRobotHUD>();
            if (GetComponent<DiggerRobotMarker>() == null) gameObject.AddComponent<DiggerRobotMarker>();
        }

        private void Start()
        {
            if (config != null)
            {
                battery.Init(config);
                breadcrumbs.Init(config.breadcrumbSpacing);
            }
            diggingSystem = FindObjectOfType<DiggingSystem>();

            // Resolve terrain layer — try by name first, then fall back to known index 6
            terrainLayer = LayerMask.NameToLayer("Terrain");
            if (terrainLayer < 0)
            {
                terrainLayer = LayerMask.NameToLayer("Diggable");
            }
            if (terrainLayer < 0)
            {
                terrainLayer = 6;
                Debug.LogWarning($"[DiggerRobot] Layer lookup by name failed — using hard-coded layer index {terrainLayer} ('{LayerMask.LayerToName(terrainLayer)}')");
            }
            terrainLayerMask = 1 << terrainLayer;
            Debug.Log($"[DiggerRobot] Using terrain layer: {terrainLayer} ('{LayerMask.LayerToName(terrainLayer)}'), mask={terrainLayerMask.value}");

            Debug.Log($"[DiggerRobot] Start: diggingSystem={(diggingSystem != null ? diggingSystem.name : "NULL")}, terrainLayer={terrainLayer}");

            // Remove old colliders
            var oldCapsule = GetComponent<CapsuleCollider>();
            if (oldCapsule != null) Destroy(oldCapsule);
            var oldBox = GetComponent<BoxCollider>();
            if (oldBox != null) Destroy(oldBox);
            var oldBlocker = transform.Find("GroundBlocker");
            if (oldBlocker != null) Destroy(oldBlocker.gameObject);

            // Physics material with configurable friction
            var mat = new PhysicMaterial("RobotPhysMat");
            mat.dynamicFriction = config != null ? config.robotFriction : 0.3f;
            mat.staticFriction = config != null ? config.robotFriction : 0.3f;
            mat.frictionCombine = PhysicMaterialCombine.Average;

            // Body collider
            var bodyBox = gameObject.AddComponent<BoxCollider>();
            bodyBox.size = new Vector3(1.2f, 0.8f, 1.6f);
            bodyBox.center = new Vector3(0f, 0.4f, 0f);
            bodyBox.material = mat;
            col = bodyBox;

            // Configure Rigidbody
            rb.mass = config != null ? config.robotMass : 20f;
            rb.drag = 2f;
            rb.angularDrag = 10f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = true;

            // Start docked
            rb.isKinematic = true;
        }

        // =====================================================================
        // MAIN LOOPS
        // =====================================================================

        private void FixedUpdate()
        {
            switch (CurrentState)
            {
                case State.Digging:
                    FixedUpdateDigging();
                    break;
                case State.Returning:
                    FixedUpdateReturning();
                    break;
            }
        }

        private void Update()
        {
            switch (CurrentState)
            {
                case State.Digging:
                    UpdateDiggingLogic();
                    break;
                case State.Returning:
                    UpdateReturningLogic();
                    break;
                case State.Recharging:
                    UpdateRecharging();
                    break;
            }
        }

        // =====================================================================
        // PHYSICS MOVEMENT — uses MovePosition to respect collisions
        // =====================================================================

        /// <summary>Move robot by delta using MovePosition. Respects physics collisions.</summary>
        private void PhysicsMove(Vector3 direction, float speed)
        {
            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z).normalized;
            if (flatDir.sqrMagnitude < 0.001f) return;

            // Face movement direction — Y axis only, never tilt
            float desiredYaw = Quaternion.LookRotation(flatDir).eulerAngles.y;
            float currentYaw = transform.eulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, 360f * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

            // Move via physics — respects colliders
            Vector3 move = flatDir * speed * Time.fixedDeltaTime;
            Vector3 newPos = rb.position + move;
            newPos.y = rb.position.y + rb.velocity.y * Time.fixedDeltaTime;
            rb.MovePosition(newPos);
        }

        /// <summary>Move along targetYaw heading at given speed.</summary>
        private void PhysicsMoveYaw(float speed)
        {
            // Enforce upright rotation
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            Vector3 fwd = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward;
            Vector3 move = fwd * speed * Time.fixedDeltaTime;
            Vector3 newPos = rb.position + move;
            newPos.y = rb.position.y + rb.velocity.y * Time.fixedDeltaTime;
            rb.MovePosition(newPos);
        }

        /// <summary>Stop horizontal movement, keep vertical for gravity.</summary>
        private void PhysicsStop()
        {
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        }

        // =====================================================================
        // TERRAIN & OBSTACLE DETECTION
        // =====================================================================

        /// <summary>Returns true if this collider belongs to a diggable voxel chunk.</summary>
        private bool IsTerrainCollider(Collider c)
        {
            if (c == null) return false;
            if (c.gameObject.layer == terrainLayer) return true;
            return c.gameObject.name.StartsWith("Chunk_");
        }

        /// <summary>
        /// Raycast through ALL objects and return the closest terrain hit.
        /// </summary>
        private bool RaycastTerrain(Vector3 origin, Vector3 dir, out RaycastHit bestHit, float maxDist)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxDist);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsTerrainCollider(hits[i].collider) && hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>SphereCast through ALL objects and return the closest terrain hit.</summary>
        private bool SphereCastTerrain(Vector3 origin, float radius, Vector3 dir, out RaycastHit bestHit, float maxDist)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, dir, maxDist);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsTerrainCollider(hits[i].collider) && hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// SphereCast forward. Returns what kind of obstacle is ahead.
        /// </summary>
        private enum AheadResult { Clear, Terrain, Obstacle }

        private AheadResult CheckAhead(out RaycastHit hitInfo)
        {
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            float radius = config.obstacleCheckRadius;
            float dist = config.obstacleCheckDist;

            if (SphereCastTerrain(origin, radius, transform.forward, out hitInfo, dist))
                return AheadResult.Terrain;

            if (Physics.SphereCast(origin, radius, transform.forward, out hitInfo, dist))
                return AheadResult.Obstacle;

            hitInfo = default;
            return AheadResult.Clear;
        }

        /// <summary>Confirm voxel terrain exists at a world position before digging.</summary>
        private bool ConfirmTerrainAt(Vector3 pos, float radius)
        {
            Collider[] cols = Physics.OverlapSphere(pos, radius);
            foreach (var c in cols)
                if (IsTerrainCollider(c)) return true;
            return false;
        }

        /// <summary>
        /// Very short range terrain check — returns true if terrain is within
        /// terrainContactDist directly ahead.
        /// </summary>
        private bool IsTerrainBlocking(out RaycastHit blockHit)
        {
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            return SphereCastTerrain(origin, 0.3f, transform.forward, out blockHit, config.terrainContactDist);
        }

        /// <summary>
        /// Check if the robot's body is currently overlapping terrain geometry.
        /// </summary>
        private bool IsInsideTerrain()
        {
            Vector3 center = transform.position + Vector3.up * 0.4f;
            Collider[] cols = Physics.OverlapBox(center, new Vector3(0.5f, 0.3f, 0.7f),
                transform.rotation);
            foreach (var c in cols)
                if (IsTerrainCollider(c)) return true;
            return false;
        }

        // =====================================================================
        // STATE TRANSITIONS
        // =====================================================================

        public void Deploy()
        {
            if (CurrentState != State.Docked) return;

            // Ignore dock colliders so robot doesn't get stuck on the stand
            if (dock != null)
            {
                foreach (var dc in dock.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(col, dc, true);
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            transform.position += Vector3.up * 0.15f;

            // Face toward first waypoint so robot doesn't walk into neighbours
            Vector3 firstTarget = Vector3.zero;
            bool hasTarget = false;
            if (waypointPath != null && waypointPath.WaypointCount > 0)
            {
                firstTarget = waypointPath.GetWaypoint(0);
                hasTarget = true;
            }
            if (hasTarget)
            {
                Vector3 toTarget = firstTarget - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.1f)
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            breadcrumbs.Clear();
            breadcrumbs.DropCrumb(transform.position);
            CurrentState = State.Digging;
            digSubState = DigSubState.MovingToSite;
            digTimer = 0f;
            turnCooldown = 0f;
            isPerformingDig = false;
            stuckCheckPos = transform.position;
            stuckTimer = 0f;
            deployGraceTimer = 2f;
            bypassWaypoint = null;
            hasEncounteredTerrain = false;
            scanTarget = null;
            scanTimer = 0f;
            if (gate != null) gate.ResetGate();

            // Always follow waypoints from the start — even on resume.
            // The robot will reach the dig area via the known safe path.
            currentWaypointIndex = 0;
            reachedDigArea = false;
            yawInitialized = false;
            Debug.Log($"[DiggerRobot] Deployed from {transform.position}, battery={battery.Ratio:P0}, hasResume={hasResumePoint}");
        }

        public void Dock()
        {
            CancelDigCycle();
            CurrentState = State.Recharging;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            isClimbing = false;
            isMountingLedge = false;

            if (dock != null)
            {
                Transform dp = dock.GetDockPoint();
                // Face toward first waypoint so robot is ready to deploy
                Quaternion dockRot = dp.rotation;
                if (waypointPath != null && waypointPath.WaypointCount > 0)
                {
                    Vector3 toWP = waypointPath.GetWaypoint(0) - dp.position;
                    toWP.y = 0f;
                    if (toWP.sqrMagnitude > 0.1f)
                        dockRot = Quaternion.LookRotation(toWP.normalized, Vector3.up);
                }
                StartCoroutine(AnimateToDock(dp.position, dockRot, 0.8f));
            }
            Debug.Log("[DiggerRobot] Docked — recharging");
        }

        private IEnumerator AnimateToDock(Vector3 targetPos, Quaternion targetRot, float duration)
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

        public void PickUp(DiggerRobotCarry carry)
        {
            CancelDigCycle();

            // Try to pick up — only change state if carry succeeds
            var prevState = CurrentState;
            CurrentState = State.Carried;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            isClimbing = false;
            isMountingLedge = false;

            if (gate != null) gate.DeactivateGate();

            carry.PickUpRobot(this);

            // Verify the carry actually took hold
            if (!DiggerRobotCarry.IsCarryingRobot)
            {
                Debug.LogWarning("[DiggerRobot] Carry failed — reverting state");
                CurrentState = prevState;
                rb.isKinematic = false;
                return;
            }
            Debug.Log("[DiggerRobot] Picked up by player");
        }

        /// <summary>Cancel any running dig coroutine cleanly.</summary>
        private void CancelDigCycle()
        {
            if (activeDigCoroutine != null)
            {
                StopCoroutine(activeDigCoroutine);
                activeDigCoroutine = null;
            }
            isPerformingDig = false;
        }

        // --- IInteractable ---

        public bool CanInteract => CurrentState != State.Docked && CurrentState != State.Carried && CurrentState != State.Recharging;

        public string GetInteractionText()
        {
            if (CanInteract)
                return "[E] Pick Up Robot";
            return "";
        }

        public void Interact(GameObject interactor)
        {
            var carry = interactor.GetComponentInChildren<DiggerRobotCarry>();
            if (carry == null)
                carry = interactor.AddComponent<DiggerRobotCarry>();
            PickUp(carry);
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        /// <summary>Activate sweep/dig mode at end of waypoint path.</summary>
        private void ActivateSweepMode()
        {
            reachedDigArea = true;
            yawInitialized = true;
            targetYaw = transform.eulerAngles.y;
            turnCooldown = 1f;
            stuckCheckPos = transform.position;
            stuckTimer = 0f;
            scanTarget = null;
            scanTimer = 0f;
            digSubState = DigSubState.Sweeping;
            Debug.Log($"[DiggerRobot] SWEEP MODE activated at {transform.position}");
        }

        // =====================================================================
        // DIGGING AI — FIXED UPDATE (physics movement)
        // =====================================================================

        private void SetTargetYaw(float yaw)
        {
            targetYaw = yaw % 360f;
            turnCooldown = 0.8f;
        }

        private void FixedUpdateDigging()
        {
            // Smoothly rotate toward targetYaw — Y axis only, never tilt
            if (yawInitialized)
            {
                float currentYaw = transform.eulerAngles.y;
                float newYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, 180f * Time.fixedDeltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }

            switch (digSubState)
            {
                case DigSubState.MovingToSite:
                    FixedUpdate_MovingToSite();
                    break;

                case DigSubState.ActiveDig:
                    PhysicsStop();
                    break;

                case DigSubState.Sweeping:
                    FixedUpdate_Sweeping();
                    break;

                case DigSubState.Reversing:
                    FixedUpdate_Reversing();
                    break;
            }
        }

        private void FixedUpdate_MovingToSite()
        {
            if (isPerformingDig)
            {
                PhysicsStop();
                return;
            }

            // Determine navigation target FIRST so we can check terrain
            // in the waypoint direction, not the robot's current facing
            Vector3 navTarget;
            if (waypointPath != null && waypointPath.WaypointCount > 0)
            {
                navTarget = waypointPath.GetWaypoint(currentWaypointIndex);
            }
            else
            {
                navTarget = transform.position;
            }

            Vector3 dir = navTarget - transform.position;
            dir.y = 0f;
            float distToTarget = dir.magnitude;
            Vector3 waypointDir = distToTarget > 0.01f ? dir.normalized : transform.forward;

            // Always face the waypoint direction — even when stopped by terrain
            if (distToTarget > 0.1f)
            {
                targetYaw = Quaternion.LookRotation(waypointDir).eulerAngles.y;
                yawInitialized = true;
            }

            // HARD BLOCK: stop at digStopDistance from terrain
            // Check in the WAYPOINT direction, not transform.forward
            // Skip during deploy grace period so robot can leave dock area
            if (deployGraceTimer <= 0f)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 0.3f;
                if (RaycastTerrain(rayOrigin, waypointDir, out RaycastHit fwdHit, SweepDigReach))
                {
                    PhysicsStop();
                    // Don't return — let Update_MovingToSite trigger a dig
                }
                if (IsInsideTerrain())
                {
                    PhysicsStop();
                    return;
                }
            }

            // Check waypoint arrival
            bool arrived = false;
            if (waypointPath == null || waypointPath.WaypointCount == 0)
            {
                Debug.LogWarning("[RobotAI] No waypoint path assigned — activating sweep immediately");
                arrived = true;
            }
            else
            {
                bool isLastWaypoint = currentWaypointIndex >= waypointPath.WaypointCount - 1;
                // Last waypoint may be inside terrain — use wider arrival radius
                float arrivalDist = isLastWaypoint ? 2.5f : 0.5f;

                if (distToTarget < arrivalDist)
                {
                    if (!isLastWaypoint)
                    {
                        currentWaypointIndex++;
                        Debug.Log($"[RobotAI] Reached waypoint {currentWaypointIndex - 1}, advancing to {currentWaypointIndex}");
                        return;
                    }
                    else
                    {
                        arrived = true;
                    }
                }
                // Also count as arrived at last waypoint if terrain is blocking the path
                else if (isLastWaypoint)
                {
                    Vector3 rayOrigin = transform.position + Vector3.up * 0.3f;
                    if (RaycastTerrain(rayOrigin, waypointDir, out RaycastHit _, SweepDigReach))
                    {
                        Debug.Log($"[RobotAI] Terrain blocking path to last waypoint (dist={distToTarget:F1}) — close enough");
                        arrived = true;
                    }
                }
            }

            if (!arrived && distToTarget > 0.1f)
            {
                // --- Bypass arc navigation ---
                if (bypassWaypoint.HasValue)
                {
                    Vector3 toWP = bypassWaypoint.Value - transform.position;
                    toWP.y = 0f;

                    if (toWP.magnitude < 0.5f)
                    {
                        bypassWaypoint = null;
                        Debug.Log("[RobotAI] Bypass arc complete — resuming nav");
                    }
                    else
                    {
                        PhysicsMove(toWP.normalized, config.moveSpeed);
                        return;
                    }
                }

                // Check for obstacles in waypoint direction
                AheadResult ahead = CheckAhead(out RaycastHit aheadHit);
                if (ahead == AheadResult.Obstacle)
                {
                    Vector3 fwd = waypointDir;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    float side = Vector3.Dot(waypointDir, right) >= 0f ? 1f : -1f;

                    Vector3 arc = transform.position
                        + right * side * BypassSideOffset
                        + fwd * BypassForwardOffset;
                    arc.y = transform.position.y;
                    bypassWaypoint = arc;

                    Debug.Log($"[RobotAI] Obstacle '{aheadHit.collider.name}' at {aheadHit.distance:F1}m — arc {(side > 0 ? "right" : "left")} to {arc:F1}");

                    Vector3 toArc = arc - transform.position;
                    toArc.y = 0f;
                    PhysicsMove(toArc.normalized, config.moveSpeed);
                    return;
                }
                if (ahead == AheadResult.Terrain)
                {
                    PhysicsMove(dir.normalized, config.moveSpeed * 0.3f);
                    return;
                }

                float speed = hasEncounteredTerrain ? config.moveSpeed * 0.3f : config.moveSpeed;
                PhysicsMove(dir.normalized, speed);
                return;
            }

            // Arrived — activate sweep mode
            ActivateSweepMode();
            if (hasResumePoint)
            {
                targetYaw = savedResumeYaw;
                hasResumePoint = false;
                Debug.Log($"[DiggerRobot] Resumed at saved position, yaw={targetYaw:F0}");
            }
        }

        /// <summary>
        /// Sweep dig reach — accounts for robot body size (0.8m half-length).
        /// Robot center can't get closer than ~0.8m to terrain due to collider,
        /// so dig trigger must be wider than digStopDistance.
        /// </summary>
        private const float SweepDigReach = 1.0f;

        private void FixedUpdate_Sweeping()
        {
            if (isPerformingDig)
            {
                PhysicsStop();
                return;
            }

            // If inside terrain, stop but don't return — let Update_Sweeping dig us out
            if (IsInsideTerrain())
            {
                PhysicsStop();
                // Don't return — fall through so scan target stays active
            }

            // Bypass arc for non-terrain obstacles
            if (bypassWaypoint.HasValue)
            {
                Vector3 toWP = bypassWaypoint.Value - transform.position;
                toWP.y = 0f;
                if (toWP.magnitude < 0.5f)
                {
                    bypassWaypoint = null;
                }
                else
                {
                    PhysicsMove(toWP.normalized, config.moveSpeed);
                    return;
                }
            }

            // Navigate toward scan target
            if (scanTarget.HasValue)
            {
                Vector3 toTarget = scanTarget.Value - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                // Within sweep dig reach — freeze, let Update trigger dig
                if (dist <= SweepDigReach)
                {
                    PhysicsStop();
                    return;
                }

                // Check for non-terrain obstacles on the way
                AheadResult ahead = CheckAhead(out RaycastHit aheadHit);
                if (ahead == AheadResult.Obstacle)
                {
                    Vector3 fwd = transform.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    float side = Vector3.Dot(toTarget.normalized, right) >= 0f ? 1f : -1f;
                    Vector3 arc = transform.position + right * side * BypassSideOffset + fwd * BypassForwardOffset;
                    arc.y = transform.position.y;
                    bypassWaypoint = arc;
                    return;
                }

                PhysicsMove(toTarget.normalized, config.moveSpeed);
            }
            else
            {
                // No scan target — stay still, wait for next scan
                PhysicsStop();
            }
        }

        private void FixedUpdate_Reversing()
        {
            Vector3 back = -transform.forward;
            PhysicsMove(back, config.moveSpeed * 0.5f);
        }

        // =====================================================================
        // DIGGING AI — UPDATE (logic, timers, decisions)
        // =====================================================================

        private void UpdateDiggingLogic()
        {
            battery.DrainPassive(Time.deltaTime);

            if (battery.IsDepleted)
            {
                SaveResumePoint();
                Debug.Log("[DiggerRobot] Battery depleted — saved resume point.");
                EnterShutdown();
                return;
            }

            if (FirstRoomUpgradeStationUI.RobotSmartStop && battery.IsLow)
            {
                SaveResumePoint();
                returnTarget = null;
                CancelDigCycle();
                CurrentState = State.Returning;
                rb.useGravity = true;
                Debug.Log("[DiggerRobot] Battery low — returning to dock (resume point saved)");
                return;
            }

            breadcrumbs.DropCrumb(transform.position);

            if (turnCooldown > 0f)
                turnCooldown -= Time.deltaTime;
            if (deployGraceTimer > 0f)
                deployGraceTimer -= Time.deltaTime;
            digTimer -= Time.deltaTime;

            // --- Universal stuck detection (all sub-states) ---
            UpdateStuckDetection();

            switch (digSubState)
            {
                case DigSubState.MovingToSite:
                    Update_MovingToSite();
                    break;
                case DigSubState.ActiveDig:
                    // Nothing — coroutine handles it
                    break;
                case DigSubState.Sweeping:
                    Update_Sweeping();
                    break;
                case DigSubState.Reversing:
                    Update_Reversing();
                    break;
            }
        }

        private void Update_MovingToSite()
        {
            if (isPerformingDig) return;

            debugLogTimer -= Time.deltaTime;
            if (debugLogTimer <= 0f)
            {
                debugLogTimer = 0.5f;
                Debug.Log($"[RobotAI] MOVING_TO_SITE pos={transform.position:F1} wp={currentWaypointIndex} digTimer={digTimer:F1}");
            }

            // Skip during deploy grace so robot can leave dock
            if (deployGraceTimer > 0f) return;

            // Compute direction toward current waypoint
            Vector3 wpDir = transform.forward;
            Vector3 navTarget = (waypointPath != null && waypointPath.WaypointCount > 0 && currentWaypointIndex < waypointPath.WaypointCount)
                    ? waypointPath.GetWaypoint(currentWaypointIndex)
                    : transform.position;
            {
                Vector3 toNav = navTarget - transform.position;
                toNav.y = 0f;
                if (toNav.sqrMagnitude > 0.01f)
                    wpDir = toNav.normalized;
            }

            // Raycast in waypoint direction to detect terrain
            Vector3 rayOrigin = transform.position + Vector3.up * 0.3f;
            if (RaycastTerrain(rayOrigin, wpDir, out RaycastHit hit, config.scanRange))
            {
                if (hit.distance <= SweepDigReach && digTimer <= 0f && diggingSystem != null)
                {
                    StartDigCycle(hit.point);
                    Debug.Log($"[RobotAI] DIG en-route at {hit.point:F1} dist={hit.distance:F1}");
                }
            }
        }

        private void Update_Sweeping()
        {
            if (isPerformingDig) return;

            // --- Periodic 180° fan scan for nearest terrain ---
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = config.scanInterval;
                scanTarget = ScanForNearestTerrain();

                debugLogTimer -= config.scanInterval;
                if (debugLogTimer <= 0f)
                {
                    debugLogTimer = 1f;
                    if (scanTarget.HasValue)
                    {
                        float d = Vector3.Distance(transform.position, scanTarget.Value);
                        Debug.Log($"[RobotAI] SCAN: terrain at {scanTarget.Value:F1} dist={d:F1}");
                    }
                    else
                    {
                        Debug.Log($"[RobotAI] SCAN: no terrain found");
                    }
                }
            }

            // --- No terrain found ---
            if (!scanTarget.HasValue)
            {
                return;
            }

            // --- Have a target — check if close enough to dig ---
            Vector3 toTarget = scanTarget.Value - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist <= SweepDigReach && digTimer <= 0f && diggingSystem != null)
            {
                StartDigCycle(scanTarget.Value);
                scanTarget = null; // will re-scan after dig
                scanTimer = 0f;
            }
        }

        /// <summary>
        /// Cast rays in a 180° forward fan to find the nearest terrain.
        /// Returns the hit point of the closest terrain, or null if none found.
        /// </summary>
        private Vector3? ScanForNearestTerrain()
        {
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            float range = config.scanRange;
            float step = config.scanAngleStep;
            float baseYaw = transform.eulerAngles.y;

            float bestDist = float.MaxValue;
            Vector3? bestPoint = null;

            for (float angle = -90f; angle <= 90f; angle += step)
            {
                Vector3 dir = Quaternion.Euler(0f, baseYaw + angle, 0f) * Vector3.forward;

                if (RaycastTerrain(origin, dir, out RaycastHit hit, range))
                {
                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        bestPoint = hit.point;
                    }
                }
            }

            return bestPoint;
        }

        private void Update_Reversing()
        {
            reverseTimer -= Time.deltaTime;
            if (reverseTimer <= 0f)
            {
                SetTargetYaw(targetYaw + 90f + Random.Range(-30f, 30f));
                digSubState = DigSubState.Sweeping;
                stuckCheckPos = transform.position;
                stuckTimer = 0f;
                Debug.Log($"[RobotAI] REVERSE DONE — new yaw={targetYaw:F0}");
            }
        }

        // =====================================================================
        // STUCK DETECTION — universal, runs in all dig sub-states
        // =====================================================================

        private void UpdateStuckDetection()
        {
            if (digSubState == DigSubState.ActiveDig || digSubState == DigSubState.Reversing)
            {
                stuckCheckPos = transform.position;
                stuckTimer = 0f;
                return;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer >= config.stuckTimeout)
            {
                float moved = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(stuckCheckPos.x, 0f, stuckCheckPos.z));

                if (moved < config.stuckMinMove)
                {
                    Debug.Log($"[RobotAI] STUCK (moved {moved:F2}m in {config.stuckTimeout}s) — reversing");
                    CancelDigCycle();
                    digSubState = DigSubState.Reversing;
                    reverseTimer = config.reverseTime;
                    stuckTimer = 0f;
                    stuckCheckPos = transform.position;
                    return;
                }

                stuckCheckPos = transform.position;
                stuckTimer = 0f;
            }
        }

        // =====================================================================
        // DIG CYCLE
        // =====================================================================

        private void StartDigCycle(Vector3 hitPoint)
        {
            isPerformingDig = true;
            hasEncounteredTerrain = true;
            digSubState = DigSubState.ActiveDig;
            digTimer = config.digCycleDuration + config.digInterval;
            activeDigCoroutine = StartCoroutine(DigCycleCoroutine(hitPoint));
        }

        private IEnumerator DigCycleCoroutine(Vector3 hitPoint)
        {
            PhysicsStop();

            if (visual != null) visual.PlayDigCycle();

            Vector3 right = transform.right;
            float r = config.digRadius;
            // Semicircle dig: center at robot height, then upper-left and upper-right
            // Never dig below robot center — prevents sinking
            float baseY = transform.position.y + 0.4f;
            Vector3 center = new Vector3(hitPoint.x, baseY, hitPoint.z);

            // Center
            diggingSystem.ExecuteDigCustom(center, r, config.digStrength, fireEvent: true);
            // Left and right at same height
            diggingSystem.ExecuteDigCustom(center + right * r * 0.7f, r, config.digStrength, fireEvent: false);
            diggingSystem.ExecuteDigCustom(center - right * r * 0.7f, r, config.digStrength, fireEvent: false);
            // Upper center — clears headroom
            diggingSystem.ExecuteDigCustom(center + Vector3.up * r * 0.8f, r, config.digStrength, fireEvent: false);
            // Upper-left and upper-right — completes the semicircle above
            diggingSystem.ExecuteDigCustom(center + right * r * 0.5f + Vector3.up * r * 0.6f, r, config.digStrength, fireEvent: false);
            diggingSystem.ExecuteDigCustom(center - right * r * 0.5f + Vector3.up * r * 0.6f, r, config.digStrength, fireEvent: false);
            battery.Drain(config.drainPerDig);

            Debug.Log($"[RobotAI] DIG at {center:F1} radius={r}");

            yield return new WaitForSeconds(config.digCycleDuration);

            isPerformingDig = false;
            activeDigCoroutine = null;
            deployGraceTimer = 0f;

            // Return to appropriate sub-state
            digSubState = reachedDigArea ? DigSubState.Sweeping : DigSubState.MovingToSite;
        }

        private void SaveResumePoint()
        {
            savedResumePos = transform.position;
            savedResumeYaw = targetYaw;
            hasResumePoint = true;
        }

        // =====================================================================
        // RETURNING (with wall climbing)
        // =====================================================================

        // Climbing state
        private bool isClimbing;
        private bool isMountingLedge;
        private float ledgeMountTimer;
        private float ledgeMountCooldown;
        private float climbStuckTimer;
        private Vector3 climbStuckCheckPos;

        private void FixedUpdateReturning()
        {
            if (!returnTarget.HasValue) return;

            Vector3 toTarget = returnTarget.Value - transform.position;
            float heightDiff = toTarget.y;
            Vector3 horizontalDir = new Vector3(toTarget.x, 0f, toTarget.z);
            float horizontalDist = horizontalDir.magnitude;

            // Check if wall/ledge is blocking horizontal movement
            bool wallBlocking = false;
            bool ledgeBlocking = false;
            if (horizontalDist > 0.1f)
            {
                Vector3 originLow = transform.position + Vector3.up * 0.3f;
                Vector3 originHigh = transform.position + Vector3.up * 0.8f;

                bool blockedLow = Physics.Raycast(originLow, horizontalDir.normalized, config.wallDetectionDist);
                bool blockedHigh = Physics.Raycast(originHigh, horizontalDir.normalized, config.wallDetectionDist);

                wallBlocking = blockedLow || blockedHigh;
                // Ledge = blocked at bottom but clear at top
                ledgeBlocking = blockedLow && !blockedHigh;
            }

            // Check if we need to climb (target is above us)
            bool needToClimb = heightDiff > config.climbHeightThreshold;

            // Check if we're at a ledge we need to mount (close to target height but blocked)
            bool nearTargetHeight = heightDiff > -0.3f && heightDiff < config.climbHeightThreshold;
            bool needLedgeMount = nearTargetHeight && (wallBlocking || ledgeBlocking) && horizontalDist > 0.3f;

            // Cooldown prevents re-triggering ledge mount immediately after completing one
            if (ledgeMountCooldown > 0f)
            {
                ledgeMountCooldown -= Time.fixedDeltaTime;
                needLedgeMount = false;
            }

            // LEDGE MOUNT: lift up first, then push forward to clear the edge
            if ((needLedgeMount && !isMountingLedge && ledgeMountCooldown <= 0f) || isMountingLedge)
            {
                if (!isMountingLedge)
                {
                    isMountingLedge = true;
                    ledgeMountTimer = 2.0f; // Max time to attempt ledge mount
                    Debug.Log($"[DiggerRobot] Ledge mount started, heightDiff={heightDiff:F2}, horizDist={horizontalDist:F2}");
                }

                rb.useGravity = false;
                rb.isKinematic = true; // Bypass physics collisions during ledge mount
                isClimbing = true;

                // Phase 1 (first 0.8s): Go UP to clear the ledge lip
                // Phase 2 (remaining): Push forward onto the floor
                float phase1Duration = 0.8f;
                float elapsed = 2.0f - ledgeMountTimer;

                Vector3 move;
                if (elapsed < phase1Duration)
                {
                    // Phase 1: Strong upward movement to get above the ledge
                    move = Vector3.up * config.climbSpeed * 4f * Time.fixedDeltaTime;
                }
                else
                {
                    // Phase 2: Forward + slight down to land on the floor
                    Vector3 mountDir = (horizontalDir.normalized * 3f + Vector3.down * 0.5f).normalized;
                    move = mountDir * config.climbSpeed * 2f * Time.fixedDeltaTime;
                }

                transform.position += move;

                // Face the direction we're going
                if (horizontalDist > 0.1f)
                {
                    float desiredYaw = Quaternion.LookRotation(horizontalDir.normalized).eulerAngles.y;
                    float currentYaw = transform.eulerAngles.y;
                    float newYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, 180f * Time.fixedDeltaTime);
                    transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
                }

                ledgeMountTimer -= Time.fixedDeltaTime;

                // Exit ledge mount if: timer expired, or we're past the ledge (no longer blocked)
                if (ledgeMountTimer <= 0f || (!wallBlocking && !ledgeBlocking))
                {
                    isMountingLedge = false;
                    isClimbing = false;
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    ledgeMountCooldown = 3f; // Prevent re-triggering for 3 seconds
                    Debug.Log("[DiggerRobot] Ledge mount complete — cooldown started");
                }
            }
            else if (needToClimb && (wallBlocking || horizontalDist < 0.5f))
            {
                // CLIMB MODE: move upward along the wall
                isClimbing = true;
                rb.useGravity = false;

                // Move up + slightly toward target horizontally
                Vector3 climbDir = Vector3.up;
                if (horizontalDist > 0.1f)
                {
                    // Slight horizontal bias to stay against wall and move toward target
                    climbDir = (Vector3.up * 3f + horizontalDir.normalized).normalized;
                }

                Vector3 move = climbDir * config.climbSpeed * Time.fixedDeltaTime;
                rb.MovePosition(rb.position + move);

                // Face the direction we're trying to go
                if (horizontalDist > 0.1f)
                {
                    float desiredYaw = Quaternion.LookRotation(horizontalDir.normalized).eulerAngles.y;
                    float currentYaw = transform.eulerAngles.y;
                    float newYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, 180f * Time.fixedDeltaTime);
                    transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
                }
            }
            else
            {
                // Normal horizontal movement
                if (isClimbing)
                {
                    isClimbing = false;
                    rb.useGravity = true;
                }

                if (horizontalDist > 0.1f)
                {
                    PhysicsMove(horizontalDir.normalized, config.moveSpeed * 1.5f);
                }
            }
        }

        private void UpdateReturningLogic()
        {
            battery.DrainPassive(Time.deltaTime);

            if (battery.IsDepleted)
            {
                EnterShutdown();
                return;
            }

            // Stuck detection while climbing
            if (isClimbing)
            {
                climbStuckTimer += Time.deltaTime;
                if (climbStuckTimer >= 3f)
                {
                    float moved = Vector3.Distance(transform.position, climbStuckCheckPos);
                    if (moved < 0.2f)
                    {
                        // Stuck while climbing — skip to next breadcrumb
                        Debug.Log("[DiggerRobot] Stuck climbing — skipping breadcrumb");
                        returnTarget = null;
                        climbStuckTimer = 0f;
                    }
                    climbStuckCheckPos = transform.position;
                    climbStuckTimer = 0f;
                }
            }
            else
            {
                climbStuckTimer = 0f;
                climbStuckCheckPos = transform.position;
            }

            if (returnTarget == null)
            {
                Vector3? next = breadcrumbs.PopNextReturnPoint();
                if (next == null)
                {
                    if (dock != null)
                    {
                        Transform dp = dock.GetDockPoint();
                        transform.position = dp.position;
                        transform.rotation = dp.rotation;
                    }
                    Dock();
                    return;
                }
                returnTarget = next;
                Debug.Log($"[DiggerRobot] Return target: {returnTarget.Value}, current: {transform.position}, heightDiff: {returnTarget.Value.y - transform.position.y:F1}m");
            }

            // Check arrival — include Y difference for 3D arrival
            Vector3 toTarget = returnTarget.Value - transform.position;
            Vector3 horizontalDiff = new Vector3(toTarget.x, 0f, toTarget.z);
            float verticalDiff = Mathf.Abs(toTarget.y);

            // Arrived if close horizontally AND vertically (or above target)
            bool arrivedHorizontal = horizontalDiff.magnitude < 0.5f;
            bool arrivedVertical = verticalDiff < 0.8f || transform.position.y >= returnTarget.Value.y - 0.2f;

            if (arrivedHorizontal && arrivedVertical)
            {
                returnTarget = null;
            }
        }

        // =====================================================================
        // RECHARGING
        // =====================================================================

        private void UpdateRecharging()
        {
            battery.Recharge(Time.deltaTime);
            if (battery.IsFull)
            {
                // If Smart Stop is enabled, auto-redeploy to resume digging
                if (FirstRoomUpgradeStationUI.RobotSmartStop)
                {
                    CurrentState = State.Docked; // Deploy() requires Docked state
                    Deploy();
                    Debug.Log("[DiggerRobot] Fully recharged — auto-redeploying to resume digging");
                }
                else
                {
                    CurrentState = State.Docked;
                    Debug.Log("[DiggerRobot] Fully recharged — ready");
                }
            }
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private void EnterShutdown()
        {
            CancelDigCycle();
            CurrentState = State.Shutdown;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.velocity = Vector3.zero;
            isClimbing = false;
            isMountingLedge = false;
            Debug.Log("[DiggerRobot] Shutdown");
        }
    }
}
