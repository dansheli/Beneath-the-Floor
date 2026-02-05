using UnityEngine;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Animates the radar pointer to point toward the current RadarTarget.
    /// Attach this to the radar prefab and assign the pointer transform.
    /// </summary>
    public class RadarPointer : MonoBehaviour
    {
        [Header("Pointer Reference")]
        [Tooltip("The pointer transform to rotate (EMF_Modern_T2_Pointer)")]
        [SerializeField] private Transform pointer;

        public enum RotationAxis { X, Y, Z }
        [Tooltip("Which local axis the pointer rotates on")]
        [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;

        [Header("Calibration")]
        [Tooltip("Offset to align pointer's visual forward with actual forward (adjust if pointer is off)")]
        [SerializeField] private float calibrationOffset = 0f;

        [Header("Angle Limits (Gauge Arc)")]
        [Tooltip("Angle for position 1 (left side of gauge)")]
        [SerializeField] private float angleAtPosition1 = -50f;

        [Tooltip("Angle for position 5 (right side of gauge)")]
        [SerializeField] private float angleAtPosition5 = 50f;

        [Tooltip("Enable angle clamping to stay within gauge arc")]
        [SerializeField] private bool clampToGaugeArc = true;

        [Header("Animation Settings")]
        [Tooltip("How fast the pointer rotates toward target")]
        [SerializeField] private float rotationSpeed = 5f;

        [Tooltip("Add subtle wobble for realism")]
        [SerializeField] private bool enableWobble = true;

        [Tooltip("Wobble amount in degrees")]
        [SerializeField] private float wobbleAmount = 3f;

        [Tooltip("Wobble speed")]
        [SerializeField] private float wobbleSpeed = 4f;

        [Header("Detection Mode")]
        [Tooltip("Use aim accuracy mode (1-5 based on how well you aim at RadarTarget) instead of compass direction")]
        [SerializeField] private bool useAimAccuracyMode = true;
        [Tooltip("Angle in degrees for perfect aim (level 5) - must aim within this angle")]
        [SerializeField] private float perfectAimAngle = 10f;
        [Tooltip("Angle in degrees for worst detection (level 1) - beyond this shows minimum")]
        [SerializeField] private float maxDetectionAngle = 90f;

        [Header("Distance Detection")]
        [Tooltip("Enable distance-based detection (closer = stronger signal)")]
        [SerializeField] private bool useDistanceDetection = true;
        [Tooltip("Distance at which signal is strongest")]
        [SerializeField] private float minDetectionDistance = 0.3f;
        [Tooltip("Distance at which signal is weakest (beyond this = minimum signal)")]
        [SerializeField] private float maxDetectionDistance = 15f;

        [Header("No Target Behavior")]
        [Tooltip("Oscillate within gauge arc when no target is set")]
        [SerializeField] private bool oscillateWhenNoTarget = true;

        [Header("Audio")]
        [Tooltip("Looping sound that plays while radar is active")]
        [SerializeField] private AudioClip radarActiveSound;
        [SerializeField] [Range(0f, 1f)] private float radarSoundVolume = 0.5f;

        [Header("Beep Sound (Speed varies with detection)")]
        [Tooltip("Beep/click sound that plays faster when detection is stronger")]
        [SerializeField] private AudioClip beepSound;
        [SerializeField] [Range(0f, 1f)] private float beepVolume = 0.7f;
        [Tooltip("Time between beeps at level 1 (slowest)")]
        [SerializeField] private float beepIntervalAtLevel1 = 1.0f;
        [Tooltip("Time between beeps at level 5 (fastest)")]
        [SerializeField] private float beepIntervalAtLevel5 = 0.15f;
        [Tooltip("Pitch at level 1")]
        [SerializeField] private float pitchAtLevel1 = 1.0f;
        [Tooltip("Pitch at level 5")]
        [SerializeField] private float pitchAtLevel5 = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [Tooltip("Test mode - needle follows simple oscillation instead of target")]
        [SerializeField] private bool testMode = false;

        [Tooltip("Speed of oscillation when no target")]
        [SerializeField] private float idleOscillateSpeed = 2f;

        // Internal state
        private float currentAngle = 0f;
        private float targetAngle = 0f;
        private float wobbleTimer = 0f;
        private Camera playerCamera;
        private float pointerBaseX = 0f;
        private float pointerBaseY = 0f;
        private float pointerBaseZ = 0f;

        // Audio
        private AudioSource audioSource;
        private AudioSource beepAudioSource;
        private bool wasActive = false;
        private float beepTimer = 0f;
        private float currentDetectionLevel = 1f; // 1-5 scale

        private void Start()
        {
            playerCamera = Camera.main;

            // Auto-find pointer if not assigned
            if (pointer == null)
            {
                pointer = FindPointerInChildren(transform);
                if (pointer != null)
                {
                    Debug.Log($"[RadarPointer] Auto-found pointer: {pointer.name}");
                }
            }

            if (pointer == null)
            {
                Debug.LogError("[RadarPointer] No pointer transform assigned or found!");
            }
            else
            {
                // Store base rotations
                pointerBaseX = pointer.localEulerAngles.x;
                pointerBaseY = pointer.localEulerAngles.y;
                pointerBaseZ = pointer.localEulerAngles.z;
                currentAngle = 0f; // Start at center
                Debug.Log($"[RadarPointer] Initialized: base=({pointerBaseX}, {pointerBaseY}, {pointerBaseZ}), axis={rotationAxis}");
            }

            // Setup audio source for looping radar sound
            if (radarActiveSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = radarActiveSound;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D sound from radar position
                audioSource.volume = radarSoundVolume;
            }

            // Setup audio source for beep sound (plays at variable speed/pitch)
            if (beepSound != null)
            {
                beepAudioSource = gameObject.AddComponent<AudioSource>();
                beepAudioSource.clip = beepSound;
                beepAudioSource.loop = false;
                beepAudioSource.playOnAwake = false;
                beepAudioSource.spatialBlend = 0.5f; // Semi-3D sound
                beepAudioSource.volume = beepVolume;
            }
        }

        private Transform FindPointerInChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("pointer") || nameLower.Contains("needle") || nameLower.Contains("arrow"))
                {
                    return child;
                }

                // Recursive search
                Transform found = FindPointerInChildren(child);
                if (found != null) return found;
            }
            return null;
        }

        private void Update()
        {
            if (pointer == null) return;

            // Check if radar is active (only tracks RadarTarget now, not resources)
            bool isActive = testMode || RadarTarget.Current != null;

            // Test mode - simple oscillation to verify rotation works
            if (testMode)
            {
                float osc = Mathf.Sin(Time.time * 2f);
                targetAngle = Mathf.Lerp(angleAtPosition1, angleAtPosition5, (osc + 1f) * 0.5f);
            }
            // Aim accuracy mode - detect resources based on aim direction
            else if (useAimAccuracyMode && playerCamera != null)
            {
                UpdateAimAccuracyDetection();
            }
            // Legacy compass mode - track RadarTarget
            else if (RadarTarget.Current != null && playerCamera != null)
            {
                UpdateTargetDirection();
            }
            else if (oscillateWhenNoTarget)
            {
                // Oscillate within gauge arc when no target
                float osc = Mathf.Sin(Time.time * idleOscillateSpeed);
                targetAngle = Mathf.Lerp(angleAtPosition1, angleAtPosition5, (osc + 1f) * 0.5f);
            }

            // Handle radar active sound
            UpdateRadarSound(isActive);

            // Smoothly rotate toward target
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);

            // Apply wobble for realism
            float wobble = 0f;
            if (enableWobble)
            {
                wobbleTimer += Time.deltaTime * wobbleSpeed;
                wobble = Mathf.Sin(wobbleTimer) * wobbleAmount;
            }

            // Apply rotation to pointer on selected axis
            float finalAngle = -(currentAngle + wobble);

            Vector3 rotation = new Vector3(pointerBaseX, pointerBaseY, pointerBaseZ);
            switch (rotationAxis)
            {
                case RotationAxis.X:
                    rotation.x = finalAngle;
                    break;
                case RotationAxis.Y:
                    rotation.y = finalAngle;
                    break;
                case RotationAxis.Z:
                    rotation.z = finalAngle;
                    break;
            }
            pointer.localRotation = Quaternion.Euler(rotation);

            // Debug: show actual rotation being applied
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[RadarPointer:{gameObject.name}] axis={rotationAxis}, currentAngle={currentAngle:F1}, targetAngle={targetAngle:F1}, finalAngle={finalAngle:F1}");
            }
        }

        private void UpdateRadarSound(bool isActive)
        {
            // Handle looping radar sound
            if (audioSource != null)
            {
                // Start sound when radar becomes active
                if (isActive && !wasActive)
                {
                    audioSource.Play();
                }
                // Stop sound when radar becomes inactive
                else if (!isActive && wasActive)
                {
                    audioSource.Stop();
                }
            }

            // Handle beep sound (speed/pitch varies with detection level)
            if (beepAudioSource != null && beepSound != null && isActive)
            {
                UpdateBeepSound();
            }

            wasActive = isActive;
        }

        /// <summary>
        /// Update the beep sound - plays faster and at higher pitch when detection is stronger.
        /// </summary>
        private void UpdateBeepSound()
        {
            // Calculate normalized level (0-1 where 0=level1, 1=level5)
            float normalizedLevel = (currentDetectionLevel - 1f) / 4f;

            // Calculate beep interval (shorter interval = faster beeps)
            float beepInterval = Mathf.Lerp(beepIntervalAtLevel1, beepIntervalAtLevel5, normalizedLevel);

            // Calculate pitch (higher pitch at higher levels)
            float pitch = Mathf.Lerp(pitchAtLevel1, pitchAtLevel5, normalizedLevel);

            // Update timer and play beep
            beepTimer += Time.deltaTime;
            if (beepTimer >= beepInterval)
            {
                beepTimer = 0f;
                beepAudioSource.pitch = pitch;
                beepAudioSource.PlayOneShot(beepSound, beepVolume);
            }
        }

        private void OnDisable()
        {
            // Stop radar sound when disabled
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            wasActive = false;
            beepTimer = 0f; // Reset beep timer
            currentDetectionLevel = 1f;
        }

        /// <summary>
        /// Update needle based on aim direction and distance to RadarTarget.
        ///
        /// Signal = DirectionFactor × DistanceFactor (multiplicative)
        ///
        /// - Direction is dominant: wrong direction = near-zero signal regardless of distance
        /// - Distance amplifies: correct direction from far = moderate signal, correct + close = maximum
        /// - Smooth curve (power of 1.5) on direction so small aim corrections have clear feedback
        /// </summary>
        private void UpdateAimAccuracyDetection()
        {
            // Must have a RadarTarget to track
            if (RadarTarget.Current == null)
            {
                currentDetectionLevel = 1f;
                if (oscillateWhenNoTarget)
                {
                    float osc = Mathf.Sin(Time.time * idleOscillateSpeed);
                    targetAngle = Mathf.Lerp(angleAtPosition1, angleAtPosition5, (osc + 1f) * 0.5f);
                }
                else
                {
                    targetAngle = angleAtPosition1;
                }

                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log("[RadarPointer] No RadarTarget set, level 1");
                }
                return;
            }

            Vector3 playerPos = playerCamera.transform.position;
            Vector3 lookDirection = playerCamera.transform.forward;
            Vector3 targetPos = RadarTarget.Current.Position;

            Vector3 directionToTarget = (targetPos - playerPos).normalized;
            float angle = Vector3.Angle(lookDirection, directionToTarget);
            float distance = Vector3.Distance(playerPos, targetPos);

            // === DIRECTION FACTOR (0–1) ===
            // Smooth curve: small aim adjustments produce clear signal changes
            // Power of 1.5 gives a steeper drop-off when aiming away
            float directionFactor;
            if (angle <= perfectAimAngle)
            {
                directionFactor = 1f;
            }
            else if (angle >= maxDetectionAngle)
            {
                directionFactor = 0f;
            }
            else
            {
                float t = 1f - ((angle - perfectAimAngle) / (maxDetectionAngle - perfectAimAngle));
                directionFactor = Mathf.Pow(t, 1.5f);
            }

            // === DISTANCE FACTOR (0–1) ===
            // Exponential falloff — signal only gets strong when truly close.
            // This prevents the radar from reading "close" when still far away.
            float distanceFactor = 1f;
            if (useDistanceDetection)
            {
                if (distance <= minDetectionDistance)
                {
                    distanceFactor = 1f;
                }
                else if (distance >= maxDetectionDistance)
                {
                    distanceFactor = 0f;
                }
                else
                {
                    // Exponential: weak signal at mid-range, strong only when very close
                    float t = 1f - ((distance - minDetectionDistance) / (maxDetectionDistance - minDetectionDistance));
                    distanceFactor = t * t * t; // Cubic falloff
                }
            }

            // === COMBINED SIGNAL ===
            // Both factors contribute equally via multiplication.
            // Direction gates: wrong aim = no signal.
            // Distance scales: far away = weak signal even with perfect aim.
            //
            // Perfect aim examples (dir=1.0) with minDist=0.3, maxDist=15, cubic falloff:
            //   15m+: 1.0 × 0.0  = 0.0  (level 1)
            //   10m:  1.0 × 0.04 = 0.04 (level ~1)
            //   7m:   1.0 × 0.19 = 0.19 (level ~2)
            //   4m:   1.0 × 0.53 = 0.53 (level ~3)
            //   2m:   1.0 × 0.82 = 0.82 (level ~4)
            //   0.3m: 1.0 × 1.0  = 1.0  (level 5)
            float signal = directionFactor * distanceFactor;

            // Map signal to gauge angle
            targetAngle = Mathf.Lerp(angleAtPosition5, angleAtPosition1, signal);

            // Store detection level for beep sound (1-5 scale)
            currentDetectionLevel = 1f + signal * 4f;

            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                int level = Mathf.RoundToInt(currentDetectionLevel);
                Debug.Log($"[RadarPointer] Target: {RadarTarget.Current.TargetName}, " +
                    $"Angle: {angle:F1}° (dir={directionFactor:F2}), " +
                    $"Distance: {distance:F1}m (dist={distanceFactor:F2}), " +
                    $"Signal: {signal:F2}, Level: {level}");
            }
        }

        private void UpdateTargetDirection()
        {
            Vector3 targetPos = RadarTarget.Current.Position;
            Vector3 playerPos = playerCamera.transform.position;

            // Get direction to target on XZ plane (ignore Y for compass behavior)
            Vector3 directionToTarget = targetPos - playerPos;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                // Too close to target, keep current direction
                return;
            }

            // Calculate world angle to target
            float worldAngleToTarget = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;

            // Get player's forward direction (camera Y rotation)
            float playerYRotation = playerCamera.transform.eulerAngles.y;

            // Calculate relative angle (target angle relative to player's facing direction)
            // This makes the pointer show direction relative to where the player is looking
            // Negated because pointer model rotates in opposite direction
            // Add calibration offset to correct for pointer's visual forward direction
            float relativeAngle = -(worldAngleToTarget - playerYRotation) + calibrationOffset;

            // Normalize to -180 to 180 range
            relativeAngle = Mathf.DeltaAngle(0f, relativeAngle);

            // Debug output
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[RadarPointer] Target: {RadarTarget.Current.TargetName} at {targetPos}");
                Debug.Log($"[RadarPointer] worldAngle={worldAngleToTarget:F1}, playerY={playerYRotation:F1}, " +
                          $"relative={relativeAngle:F1}, calibration={calibrationOffset:F1}");
            }

            // Clamp to gauge arc if enabled
            if (clampToGaugeArc)
            {
                // Handle clamping regardless of which angle is larger
                float lowLimit = Mathf.Min(angleAtPosition1, angleAtPosition5);
                float highLimit = Mathf.Max(angleAtPosition1, angleAtPosition5);
                float beforeClamp = relativeAngle;
                relativeAngle = Mathf.Clamp(relativeAngle, lowLimit, highLimit);

                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[RadarPointer] Clamp: before={beforeClamp:F1}, after={relativeAngle:F1}, limits=[{lowLimit},{highLimit}]");
                }
            }

            targetAngle = relativeAngle;

            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[RadarPointer] ASSIGNED targetAngle={targetAngle:F1}");
            }
        }

        /// <summary>
        /// Get the current angle the pointer is showing.
        /// </summary>
        public float GetCurrentAngle() => currentAngle;

        /// <summary>
        /// Check if the pointer is pointing toward the target (within threshold).
        /// </summary>
        public bool IsPointingAtTarget(float threshold = 15f)
        {
            if (RadarTarget.Current == null) return false;
            return Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) < threshold;
        }

        /// <summary>
        /// Get distance to current target.
        /// </summary>
        public float GetDistanceToTarget()
        {
            if (RadarTarget.Current == null || playerCamera == null) return -1f;
            return Vector3.Distance(playerCamera.transform.position, RadarTarget.Current.Position);
        }

        /// <summary>
        /// Manually set the pointer transform.
        /// </summary>
        public void SetPointer(Transform newPointer)
        {
            pointer = newPointer;
            if (pointer != null)
            {
                pointerBaseX = pointer.localEulerAngles.x;
                pointerBaseY = pointer.localEulerAngles.y;
                pointerBaseZ = pointer.localEulerAngles.z;
                currentAngle = 0f;
            }
        }

        /// <summary>
        /// Get the current gauge position (1-5) based on needle angle.
        /// 1 = far left, 3 = center, 5 = far right
        /// </summary>
        public float GetGaugePosition()
        {
            // Normalize current rotation to 0-1 range within the arc
            float normalized = Mathf.InverseLerp(angleAtPosition1, angleAtPosition5, currentAngle);
            // Convert to 1-5 scale
            return 1f + normalized * 4f;
        }

        /// <summary>
        /// Check if target is within the gauge arc (not pegged at limits).
        /// </summary>
        public bool IsTargetInArc()
        {
            if (RadarTarget.Current == null) return false;
            float gauge = GetGaugePosition();
            return gauge > 1.1f && gauge < 4.9f;
        }
    }
}
