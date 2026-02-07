using UnityEngine;
using System.Collections;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Tools
{
    public enum ToolAnimationType
    {
        Shovel,    // Tier 1 - Heavy digging, scooping motion
        Hoe,       // Tier 2 - Horizontal chopping sweep
        Pickaxe,   // Tier 3 - Overhead mining strike
        DrillPike,     // Tier 4 - Normal hit + charged spinning super hit
        SonicPulser    // Tier 5 - Gun-style pulse weapon with charge mechanic
    }

    /// <summary>
    /// Tool visual component with unique animations per tool type.
    /// </summary>
    public class ToolVisual : MonoBehaviour
    {
        [Header("Tool Type")]
        [SerializeField] private ToolAnimationType animationType = ToolAnimationType.Shovel;

        [Header("Idle Animation")]
        [SerializeField] private bool enableIdleAnimation = true;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobAmount = 0.01f;
        [SerializeField] private float swaySpeed = 1.5f;
        [SerializeField] private float swayAmount = 0.5f;

        [Header("Tool Parts (auto-found if not assigned)")]
        [SerializeField] private Transform toolHead;      // e.g., Shovel.002
        [SerializeField] private Transform toolHandle;    // e.g., Shovel_Handle.003
        [SerializeField] private Light toolLight;
        [SerializeField] private ParticleSystem trailParticles;

        [Header("Drill Pike Charged Attack")]
        [SerializeField] private float chargeSpinStartSpeed = 90f;    // degrees per second at start
        [SerializeField] private float chargeSpinMaxSpeed = 1800f;    // degrees per second at max charge
        [SerializeField] private float chargeTimeToMax = 1.5f;        // seconds to reach max spin
        [SerializeField] private AudioClip chargeSpinSound;
        [SerializeField] private AudioClip chargeReleaseSound;

        [Header("Sonic Pulser Settings")]
        [SerializeField] private float sonicIdleVibrationIntensity = 0.001f;
        [SerializeField] private float sonicIdleVibrationSpeed = 30f;
        [SerializeField] private float sonicRecoilDistance = 0.06f;
        [SerializeField] private Color sonicPulseColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color sonicChargedPulseColor = new Color(0.6f, 0.4f, 1f, 1f);

        // Initial transforms for child parts
        private Vector3 headInitialLocalPos;
        private Quaternion headInitialLocalRot;
        private Vector3 handleInitialLocalPos;
        private Quaternion handleInitialLocalRot;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] digSounds;
        [SerializeField] private AudioClip[] hitSounds;
        [SerializeField] private AudioClip swingSound;

        [Header("Animation Speed")]
        [SerializeField] private float animationSpeedMultiplier = 1f;

        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private float idleTimer;
        private bool isAnimating;
        private Coroutine animationCoroutine;

        // Drill Pike charged attack state
        private bool isCharging = false;
        private float chargeTime = 0f;
        private Coroutine chargeCoroutine;
        private float currentSpinAngle = 0f;

        // Sonic Pulser state
        private Light sonicTipGlow;
        private Transform sonicTipPoint;
        private ProjectileExitGizmo projectileExitGizmo;

        // Sonic Pulser charge state
        private bool isReloading = false;
        private float reloadTimer = 0f;
        private const float CHARGE_RATE = 2f; // Seconds per unit of charge
        private const float CHARGE_MAX_SECONDS = 5f; // Hard cap - charge stops after this
        private Coroutine reloadCoroutine;
        private GameObject chargeBallObj; // Visible ball growing at barrel during charge
        private bool chargePaused = false; // True when energy is depleted (ball stops growing)

        private void Awake()
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;

            // Auto-find tool parts if not assigned
            AutoFindToolParts();

            // Store initial transforms for parts
            if (toolHead != null)
            {
                headInitialLocalPos = toolHead.localPosition;
                headInitialLocalRot = toolHead.localRotation;
            }
            if (toolHandle != null)
            {
                handleInitialLocalPos = toolHandle.localPosition;
                handleInitialLocalRot = toolHandle.localRotation;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }
            }
        }

        private void AutoFindToolParts()
        {
            if (toolHead == null || toolHandle == null)
            {
                FindToolPartsRecursive(transform);
            }
        }

        private void FindToolPartsRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                string nameLower = child.name.ToLower();

                // Find drill head specifically for Drill Pike
                if (toolHead == null && (nameLower.Contains("drill_head") || nameLower.Contains("drillhead")))
                {
                    toolHead = child;
                    Debug.Log($"[ToolVisual] Found drill head: {child.name}");
                }
                // Find shovel head (blade) for other tools
                else if (toolHead == null && (nameLower.Contains("blade") ||
                    (nameLower.Contains("head") && !nameLower.Contains("handle"))))
                {
                    toolHead = child;
                }
                // Find handle
                else if (toolHandle == null && nameLower.Contains("handle"))
                {
                    toolHandle = child;
                }

                // Search children recursively
                if (toolHead == null || toolHandle == null)
                {
                    FindToolPartsRecursive(child);
                }
            }
        }

        private void OnEnable()
        {
            // Restore to the pose captured in Awake() - don't re-capture
            // (if deactivated mid-animation, transform may be in a tilted state)
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            idleTimer = 0f;
            isAnimating = false;
            isCharging = false;
            isReloading = false;
            chargePaused = false;

            // Restore child transforms captured in Awake()
            if (toolHead != null)
            {
                toolHead.localPosition = headInitialLocalPos;
                toolHead.localRotation = headInitialLocalRot;
            }
            if (toolHandle != null)
            {
                toolHandle.localPosition = handleInitialLocalPos;
                toolHandle.localRotation = handleInitialLocalRot;
            }
        }

        private void Update()
        {
            if (enableIdleAnimation && !isAnimating)
            {
                ApplyIdleAnimation();
            }
        }

        private void ApplyIdleAnimation()
        {
            idleTimer += Time.deltaTime;

            float bobY = Mathf.Sin(idleTimer * bobSpeed) * bobAmount;
            float bobX = Mathf.Sin(idleTimer * bobSpeed * 0.5f) * bobAmount * 0.5f;

            float swayZ = Mathf.Sin(idleTimer * swaySpeed) * swayAmount;
            float swayX = Mathf.Sin(idleTimer * swaySpeed * 0.7f) * swayAmount * 0.3f;

            // Sonic Pulser: add constant high-frequency vibration (the tool hums with energy)
            if (animationType == ToolAnimationType.SonicPulser)
            {
                float vibX = Mathf.PerlinNoise(idleTimer * sonicIdleVibrationSpeed, 0f) - 0.5f;
                float vibY = Mathf.PerlinNoise(0f, idleTimer * sonicIdleVibrationSpeed) - 0.5f;
                bobX += vibX * sonicIdleVibrationIntensity * 2f;
                bobY += vibY * sonicIdleVibrationIntensity * 2f;
            }

            transform.localPosition = initialLocalPosition + new Vector3(bobX, bobY, 0);
            transform.localRotation = initialLocalRotation * Quaternion.Euler(swayX, 0, swayZ);
        }

        public void PlayDigAnimation()
        {
            // Sonic Pulser: allow rapid fire by cancelling previous recoil animation
            if (animationType == ToolAnimationType.SonicPulser && isAnimating)
            {
                if (animationCoroutine != null)
                {
                    StopCoroutine(animationCoroutine);
                    animationCoroutine = null;
                }
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
                isAnimating = false;
            }

            // Prevent double-triggering if already animating
            if (isAnimating) return;

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            // Update animation speed from current dig speed multiplier (speed * tier)
            float currentSpeed = UpgradeStation.ToolSpeedMultiplier * UpgradeStation.ToolTierMultiplier;
            if (currentSpeed >= 0.5f)
            {
                animationSpeedMultiplier = currentSpeed;
            }

            switch (animationType)
            {
                case ToolAnimationType.Shovel:
                    animationCoroutine = StartCoroutine(ShovelDigAnimation());
                    break;
                case ToolAnimationType.Hoe:
                    animationCoroutine = StartCoroutine(HoeDigAnimation());
                    break;
                case ToolAnimationType.Pickaxe:
                    animationCoroutine = StartCoroutine(PickaxeDigAnimation());
                    break;
                case ToolAnimationType.DrillPike:
                    animationCoroutine = StartCoroutine(DrillPikeDigAnimation());
                    break;
                case ToolAnimationType.SonicPulser:
                    animationCoroutine = StartCoroutine(SonicPulserShotAnimation());
                    break;
            }
        }

        // ============================================================
        // TIER 1: SHOVEL - Coordinated head & handle animation
        // ============================================================
        private IEnumerator ShovelDigAnimation()
        {
            isAnimating = true;

            // Check if we have the parts to animate separately
            bool hasPartsToAnimate = toolHead != null && toolHandle != null;

            if (hasPartsToAnimate)
            {
                yield return ShovelPartsAnimation();
            }
            else
            {
                // Fallback to parent animation if parts not found
                yield return ShovelFallbackAnimation();
            }

            isAnimating = false;
            animationCoroutine = null;
        }

        private IEnumerator ShovelPartsAnimation()
        {
            // Animate the PARENT transform so all parts move together naturally
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // === PHASE 1: PUSH FORWARD + SMALL ANGLE ===
            Vector3 digPos = startPos + new Vector3(0f, 0.02f, 0.08f);  // forward push
            Quaternion digRot = startRot * Quaternion.Euler(-12f, 0f, 0f);  // slight tilt

            float elapsed = 0f;
            float duration = GetAdjustedDuration(0.12f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);

                transform.localPosition = Vector3.Lerp(startPos, digPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, digRot, t);

                yield return null;
            }

            PlaySwingSound();
            PlayDigSoundAndEffects();
            TriggerDigHit();

            // === PHASE 2: SMALL IMPACT SHAKE ===
            Vector3 shakeBase = transform.localPosition;
            for (int i = 0; i < 2; i++)
            {
                float shakeX = Random.Range(-0.008f, 0.008f);
                float shakeY = Random.Range(-0.008f, 0.008f);
                transform.localPosition = shakeBase + new Vector3(shakeX, shakeY, 0);
                yield return new WaitForSeconds(0.02f);
            }

            // === PHASE 3: RETURN TO IDLE ===
            Vector3 endPos = transform.localPosition;
            Quaternion endRot = transform.localRotation;

            elapsed = 0f;
            duration = GetAdjustedDuration(0.15f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);

                transform.localPosition = Vector3.Lerp(endPos, startPos, t);
                transform.localRotation = Quaternion.Slerp(endRot, startRot, t);

                yield return null;
            }

            // Ensure exact reset
            transform.localPosition = startPos;
            transform.localRotation = startRot;
        }

        private IEnumerator ShovelFallbackAnimation()
        {
            // Fallback: animate parent if parts not found
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // Wind up
            Quaternion windUpRot = startRot * Quaternion.Euler(-20f, 5f, -8f);
            yield return AnimateToPosition(startPos, startPos, startRot, windUpRot, GetAdjustedDuration(0.15f), EaseOutCubic);

            PlaySwingSound();

            // Dig down
            Quaternion digRot = startRot * Quaternion.Euler(25f, -3f, 5f);
            yield return AnimateToPosition(startPos, startPos, windUpRot, digRot, GetAdjustedDuration(0.1f), EaseInQuad);

            PlayDigSoundAndEffects();
            TriggerDigHit();

            // Return to idle
            yield return AnimateToPosition(startPos, startPos, digRot, startRot, GetAdjustedDuration(0.18f), EaseOutBack);

            transform.localPosition = startPos;
            transform.localRotation = startRot;
        }

        // ============================================================
        // TIER 2: HOE - Pivot swing (rotation only, minimal position)
        // ============================================================
        private IEnumerator HoeDigAnimation()
        {
            isAnimating = true;
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // === PHASE 1: TILT HEAD UP (cock back) ===
            Quaternion windUpRot = startRot * Quaternion.Euler(0f, 0f, -15f);  // head rises (Z rotation)

            float elapsed = 0f;
            float duration = GetAdjustedDuration(0.1f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);
                transform.localRotation = Quaternion.Slerp(startRot, windUpRot, t);
                yield return null;
            }

            PlaySwingSound();

            // === PHASE 2: SWING DOWN (strike) ===
            Quaternion strikeRot = startRot * Quaternion.Euler(0f, 0f, 20f);  // head swings down (Z rotation)

            elapsed = 0f;
            duration = GetAdjustedDuration(0.08f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInQuad(elapsed / duration);
                transform.localRotation = Quaternion.Slerp(windUpRot, strikeRot, t);
                yield return null;
            }

            PlayDigSoundAndEffects();
            TriggerDigHit();

            // === PHASE 3: SMALL IMPACT SHAKE ===
            Quaternion shakeBase = transform.localRotation;
            for (int i = 0; i < 2; i++)
            {
                float shakeZ = Random.Range(-2f, 2f);
                float shakeY = Random.Range(-1f, 1f);
                transform.localRotation = shakeBase * Quaternion.Euler(0f, shakeY, shakeZ);
                yield return new WaitForSeconds(0.02f);
            }

            // === PHASE 4: RETURN TO IDLE ===
            Quaternion endRot = transform.localRotation;

            elapsed = 0f;
            duration = GetAdjustedDuration(0.15f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                transform.localRotation = Quaternion.Slerp(endRot, startRot, t);
                yield return null;
            }

            transform.localPosition = startPos;
            transform.localRotation = startRot;
            isAnimating = false;
            animationCoroutine = null;
        }

        // ============================================================
        // TIER 3: PICKAXE - X-axis rotation only
        // Keeps Y and Z exactly the same, only animates X
        // ============================================================
        private IEnumerator PickaxeDigAnimation()
        {
            isAnimating = true;
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // Extract euler angles and normalize X to -180..180 range
            Vector3 startEuler = startRot.eulerAngles;
            float startX = startEuler.x;
            if (startX > 180f) startX -= 360f;

            // Keep Y and Z FIXED throughout the entire animation
            float fixedY = startEuler.y;
            float fixedZ = startEuler.z;

            // === PHASE 1: WIND UP (raise pickaxe back) ===
            float windUpX = startX - 20f;

            float elapsed = 0f;
            float duration = GetAdjustedDuration(0.25f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);
                float currentX = Mathf.Lerp(startX, windUpX, t);
                transform.localRotation = Quaternion.Euler(currentX, fixedY, fixedZ);
                yield return null;
            }

            PlaySwingSound();

            // === PHASE 2: STRIKE DOWN (swing forward) ===
            float strikeX = startX + 60f;

            elapsed = 0f;
            duration = GetAdjustedDuration(0.05f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInCubic(elapsed / duration);
                float currentX = Mathf.Lerp(windUpX, strikeX, t);
                transform.localRotation = Quaternion.Euler(currentX, fixedY, fixedZ);
                yield return null;
            }

            PlayDigSoundAndEffects();
            TriggerDigHit();

            // === PHASE 3: IMPACT SHAKE (X-axis only) ===
            for (int i = 0; i < 2; i++)
            {
                float shakeX = strikeX + Random.Range(-3f, 3f);
                transform.localRotation = Quaternion.Euler(shakeX, fixedY, fixedZ);
                yield return new WaitForSeconds(0.02f);
            }

            // === PHASE 4: RETURN TO IDLE ===
            float endX = strikeX;

            elapsed = 0f;
            duration = GetAdjustedDuration(0.2f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                float currentX = Mathf.Lerp(endX, startX, t);
                transform.localRotation = Quaternion.Euler(currentX, fixedY, fixedZ);
                yield return null;
            }

            transform.localPosition = startPos;
            transform.localRotation = startRot;
            isAnimating = false;
            animationCoroutine = null;
        }

        // ============================================================
        // TIER 4: DRILL PIKE - Normal hit + Charged spinning super hit
        // ============================================================

        /// <summary>
        /// Normal dig animation for Drill Pike - quick thrust forward.
        /// </summary>
        private IEnumerator DrillPikeDigAnimation()
        {
            isAnimating = true;
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // === PHASE 1: PULL BACK SLIGHTLY ===
            Vector3 pullBackPos = startPos + new Vector3(0f, 0.02f, -0.05f);
            Quaternion pullBackRot = startRot * Quaternion.Euler(-10f, 0f, 0f);

            float elapsed = 0f;
            float duration = GetAdjustedDuration(0.1f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, pullBackPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, pullBackRot, t);
                yield return null;
            }

            PlaySwingSound();

            // === PHASE 2: THRUST FORWARD ===
            Vector3 thrustPos = startPos + new Vector3(0f, -0.03f, 0.12f);
            Quaternion thrustRot = startRot * Quaternion.Euler(15f, 0f, 0f);

            elapsed = 0f;
            duration = GetAdjustedDuration(0.08f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInCubic(elapsed / duration);
                transform.localPosition = Vector3.Lerp(pullBackPos, thrustPos, t);
                transform.localRotation = Quaternion.Slerp(pullBackRot, thrustRot, t);
                yield return null;
            }

            PlayDigSoundAndEffects();
            TriggerDigHit();

            // === PHASE 3: IMPACT SHAKE ===
            Vector3 shakeBase = transform.localPosition;
            for (int i = 0; i < 3; i++)
            {
                float shakeX = Random.Range(-0.01f, 0.01f);
                float shakeY = Random.Range(-0.01f, 0.01f);
                transform.localPosition = shakeBase + new Vector3(shakeX, shakeY, 0);
                yield return new WaitForSeconds(0.02f);
            }

            // === PHASE 4: RETURN TO IDLE ===
            Vector3 endPos = transform.localPosition;
            Quaternion endRot = transform.localRotation;

            elapsed = 0f;
            duration = GetAdjustedDuration(0.15f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                transform.localPosition = Vector3.Lerp(endPos, startPos, t);
                transform.localRotation = Quaternion.Slerp(endRot, startRot, t);
                yield return null;
            }

            transform.localPosition = startPos;
            transform.localRotation = startRot;
            isAnimating = false;
            animationCoroutine = null;
        }

        /// <summary>
        /// Start charging the Drill Pike super attack, or start reload for Sonic Pulser.
        /// Call this when player starts holding the dig button.
        /// </summary>
        public void StartCharging()
        {
            if (animationType == ToolAnimationType.SonicPulser)
            {
                // Sonic Pulser uses reload mechanic instead of charge
                StartReload();
                return;
            }

            if (isCharging) return;

            isCharging = true;
            chargeTime = 0f;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
            }

            currentSpinAngle = 0f;
            chargeCoroutine = StartCoroutine(DrillPikeChargeCoroutine());
        }

        /// <summary>
        /// Start the Sonic Pulser reload sequence.
        /// Particles swirl into barrel, after RELOAD_DURATION auto-fires.
        /// </summary>
        public void StartReload()
        {
            if (isReloading) return;

            // Also set isCharging so GetChargeProgress() works for DiggingSystem
            isCharging = true;
            chargeTime = 0f;
            chargePaused = false;

            if (reloadCoroutine != null)
                StopCoroutine(reloadCoroutine);
            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            reloadCoroutine = StartCoroutine(SonicPulserReloadCoroutine());
        }

        /// <summary>
        /// Release the charged attack.
        /// For Drill Pike: fires based on charge power.
        /// For Sonic Pulser: fires charged shot based on current charge level.
        /// </summary>
        public void ReleaseChargedAttack()
        {
            if (animationType == ToolAnimationType.SonicPulser)
            {
                // Sonic Pulser: fire charged shot at current charge level
                if (isReloading)
                {
                    float sonicChargePower = GetChargeProgress();

                    // Stop reload coroutine and clean up
                    isReloading = false;
                    isCharging = false;
                    chargePaused = false;

                    if (reloadCoroutine != null)
                    {
                        StopCoroutine(reloadCoroutine);
                        reloadCoroutine = null;
                    }

                    // Clean up charge ball and audio
                    DestroyChargeBall();
                    if (audioSource != null)
                    {
                        audioSource.Stop();
                        audioSource.loop = false;
                    }

                    // Return to idle pose then fire
                    if (animationCoroutine != null)
                        StopCoroutine(animationCoroutine);

                    transform.localPosition = initialLocalPosition;
                    transform.localRotation = initialLocalRotation;

                    // Fire the charged shot animation (which fires the projectile)
                    animationCoroutine = StartCoroutine(SonicPulserChargedShotAnimation(sonicChargePower));
                }
                return;
            }

            if (!isCharging) return;

            isCharging = false;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            float chargePower = Mathf.Clamp01(chargeTime / chargeTimeToMax);

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            animationCoroutine = StartCoroutine(DrillPikeSuperHitAnimation(chargePower));
        }

        /// <summary>
        /// Cancel charging or reload without attacking.
        /// </summary>
        public void CancelCharging()
        {
            if (!isCharging && !isReloading) return;

            isCharging = false;
            chargeTime = 0f;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            // Reset head rotation (Drill Pike)
            if (toolHead != null)
            {
                toolHead.localRotation = headInitialLocalRot;
            }

            // Clean up Sonic Pulser reload/charge effects
            if (animationType == ToolAnimationType.SonicPulser)
            {
                // Cancel reload
                isReloading = false;
                reloadTimer = 0f;
                chargePaused = false;
                if (reloadCoroutine != null)
                {
                    StopCoroutine(reloadCoroutine);
                    reloadCoroutine = null;
                }

                DestroyChargeBall();
                if (sonicTipGlow != null)
                    sonicTipGlow.intensity = 0f;
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
            }

            // Stop charge audio
            if (audioSource != null && audioSource.isPlaying && audioSource.loop)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
        }

        /// <summary>
        /// Check if currently charging.
        /// </summary>
        public bool IsCharging() => isCharging;

        /// <summary>
        /// Get current charge/reload progress (0 to 1).
        /// </summary>
        public float GetChargeProgress()
        {
            if (animationType == ToolAnimationType.SonicPulser)
            {
                return reloadTimer / CHARGE_RATE; // Uncapped - grows as long as energy lasts
            }
            return Mathf.Clamp01(chargeTime / chargeTimeToMax);
        }

        /// <summary>
        /// Check if currently in reload sequence (Sonic Pulser only).
        /// </summary>
        public bool IsReloading() => isReloading;

        /// <summary>
        /// Pause the Sonic Pulser charge (ball stops growing but doesn't fire).
        /// Called when energy is depleted during charge.
        /// </summary>
        public void PauseCharge()
        {
            chargePaused = true;
        }

        /// <summary>
        /// Check if charge has reached the maximum time (9 seconds).
        /// </summary>
        public bool IsChargeMaxed() => reloadTimer >= CHARGE_MAX_SECONDS;

        /// <summary>
        /// Coroutine that spins the drill head while charging.
        /// </summary>
        private IEnumerator DrillPikeChargeCoroutine()
        {
            // Play charge sound (looping)
            if (chargeSpinSound != null && audioSource != null)
            {
                audioSource.clip = chargeSpinSound;
                audioSource.loop = true;
                audioSource.pitch = 0.5f;
                audioSource.Play();
            }

            while (isCharging)
            {
                chargeTime += Time.deltaTime;

                // Calculate spin speed (accelerates over time)
                float chargeProgress = Mathf.Clamp01(chargeTime / chargeTimeToMax);
                float spinSpeed = Mathf.Lerp(chargeSpinStartSpeed, chargeSpinMaxSpeed, chargeProgress * chargeProgress);

                // Update audio pitch based on charge
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.pitch = Mathf.Lerp(0.5f, 1.5f, chargeProgress);
                }

                // Spin the tool head around its local X axis (drill rotation)
                if (toolHead != null)
                {
                    currentSpinAngle += spinSpeed * Time.deltaTime;
                    toolHead.localRotation = headInitialLocalRot * Quaternion.Euler(currentSpinAngle, 0f, 0f);
                }

                // Add slight vibration to the whole tool based on charge
                float vibration = chargeProgress * 0.008f;
                float vibX = Random.Range(-vibration, vibration);
                float vibY = Random.Range(-vibration, vibration);
                transform.localPosition = initialLocalPosition + new Vector3(vibX, vibY, 0f);

                yield return null;
            }

            // Stop charge sound
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
        }

        /// <summary>
        /// Super hit animation after releasing charged attack.
        /// </summary>
        private IEnumerator DrillPikeSuperHitAnimation(float chargePower)
        {
            isAnimating = true;
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // Stop the charge sound
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            // Play release sound
            if (chargeReleaseSound != null && audioSource != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(chargeReleaseSound, 0.8f + chargePower * 0.4f);
            }

            // The head is still spinning from charge - keep it spinning during thrust
            float remainingSpinSpeed = Mathf.Lerp(chargeSpinStartSpeed, chargeSpinMaxSpeed, chargePower);

            // === PHASE 1: POWERFUL THRUST FORWARD ===
            float thrustDistance = 0.1f + chargePower * 0.15f; // More charge = more distance
            Vector3 thrustPos = startPos + new Vector3(0f, -0.05f, thrustDistance);
            Quaternion thrustRot = startRot * Quaternion.Euler(20f + chargePower * 10f, 0f, 0f);

            float elapsed = 0f;
            float duration = GetAdjustedDuration(0.06f); // Very fast thrust
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInCubic(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, thrustPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, thrustRot, t);

                // Keep head spinning during thrust (slowing down)
                if (toolHead != null)
                {
                    currentSpinAngle += remainingSpinSpeed * (1f - t * 0.5f) * Time.deltaTime;
                    toolHead.localRotation = headInitialLocalRot * Quaternion.Euler(currentSpinAngle, 0f, 0f);
                }

                yield return null;
            }

            PlaySwingSound();
            PlayDigSoundAndEffects();
            TriggerDigHit();

            // === PHASE 2: IMPACT - Strong shake based on charge ===
            Vector3 shakeBase = transform.localPosition;
            int shakeCount = 3 + Mathf.RoundToInt(chargePower * 4);
            float shakeIntensity = 0.01f + chargePower * 0.02f;

            for (int i = 0; i < shakeCount; i++)
            {
                float shakeX = Random.Range(-shakeIntensity, shakeIntensity);
                float shakeY = Random.Range(-shakeIntensity, shakeIntensity);
                transform.localPosition = shakeBase + new Vector3(shakeX, shakeY, 0);

                // Slow down head spin during impact
                if (toolHead != null)
                {
                    remainingSpinSpeed *= 0.7f;
                    currentSpinAngle += remainingSpinSpeed * 0.02f;
                    toolHead.localRotation = headInitialLocalRot * Quaternion.Euler(currentSpinAngle, 0f, 0f);
                }

                yield return new WaitForSeconds(0.025f);
            }

            // === PHASE 3: RETURN TO IDLE ===
            Vector3 endPos = transform.localPosition;
            Quaternion endRot = transform.localRotation;
            float endSpinAngle = currentSpinAngle;

            elapsed = 0f;
            duration = GetAdjustedDuration(0.25f + chargePower * 0.1f); // Slower return for powerful hits
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                transform.localPosition = Vector3.Lerp(endPos, startPos, t);
                transform.localRotation = Quaternion.Slerp(endRot, startRot, t);

                // Gradually stop head spin and return to original rotation
                if (toolHead != null)
                {
                    float spinT = EaseOutCubic(t);
                    // Interpolate the spin angle back to 0 (or nearest full rotation)
                    float targetAngle = Mathf.Round(endSpinAngle / 360f) * 360f;
                    currentSpinAngle = Mathf.Lerp(endSpinAngle, targetAngle, spinT);
                    toolHead.localRotation = headInitialLocalRot * Quaternion.Euler(currentSpinAngle, 0f, 0f);
                }

                yield return null;
            }

            // Reset everything
            transform.localPosition = startPos;
            transform.localRotation = startRot;
            if (toolHead != null)
            {
                toolHead.localRotation = headInitialLocalRot;
            }
            currentSpinAngle = 0f;

            isAnimating = false;
            animationCoroutine = null;
        }

        // ============================================================
        // TIER 5: SONIC PULSER - Gun-style pulse weapon
        // ============================================================

        /// <summary>
        /// Set a dynamically created child GameObject to match the tool's layer.
        /// This is critical for the overlay camera system (HeldTool layer).
        /// </summary>
        private void SetChildLayer(GameObject child)
        {
            if (child == null) return;
            child.layer = gameObject.layer;
        }

        /// <summary>
        /// Find the ProjectileExitGizmo on this tool (cached).
        /// </summary>
        private ProjectileExitGizmo GetProjectileExitGizmo()
        {
            if (projectileExitGizmo == null)
                projectileExitGizmo = GetComponentInChildren<ProjectileExitGizmo>(true);
            return projectileExitGizmo;
        }

        /// <summary>
        /// Find or create the tip point for particle effects.
        /// Prefers ProjectileExitGizmo if placed on the tool.
        /// </summary>
        private Transform GetSonicTipPoint()
        {
            // Prefer the gizmo if it exists
            var gizmo = GetProjectileExitGizmo();
            if (gizmo != null)
            {
                sonicTipPoint = gizmo.transform;
                return sonicTipPoint;
            }

            if (sonicTipPoint != null) return sonicTipPoint;

            // Try to find a child named "tip" or "muzzle" or "barrel"
            foreach (Transform child in transform)
            {
                string n = child.name.ToLower();
                if (n.Contains("tip") || n.Contains("muzzle") || n.Contains("barrel") || n.Contains("head"))
                {
                    sonicTipPoint = child;
                    return sonicTipPoint;
                }
            }

            // Fallback: create a tip point at the front of the tool
            GameObject tipObj = new GameObject("SonicTipPoint");
            tipObj.transform.SetParent(transform, false);
            tipObj.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            SetChildLayer(tipObj);
            sonicTipPoint = tipObj.transform;
            return sonicTipPoint;
        }



        /// <summary>
        /// Create or get the tip glow light.
        /// </summary>
        private Light GetSonicTipGlow()
        {
            if (sonicTipGlow != null) return sonicTipGlow;

            Transform tip = GetSonicTipPoint();
            GameObject lightObj = new GameObject("SonicTipGlow");
            lightObj.transform.SetParent(tip, false);
            lightObj.transform.localPosition = Vector3.zero;
            SetChildLayer(lightObj);

            sonicTipGlow = lightObj.AddComponent<Light>();
            sonicTipGlow.type = LightType.Point;
            sonicTipGlow.color = sonicPulseColor;
            sonicTipGlow.range = 2f;
            sonicTipGlow.intensity = 0f;
            sonicTipGlow.shadows = LightShadows.None;

            return sonicTipGlow;
        }

        /// <summary>
        /// Normal shot animation - fires round orb projectile from barrel with recoil.
        /// Uses a mesh-based projectile (sphere) instead of ParticleSystem for overlay camera compatibility.
        /// </summary>
        private IEnumerator SonicPulserShotAnimation()
        {
            isAnimating = true;
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // Fire projectile - small visual ball, dig radius = 75% of normal tool dig
            float quickDigRadius = Digging.DiggingSystem.Instance != null ? Digging.DiggingSystem.Instance.DigRadius : 0.5f;
            quickDigRadius *= Machines.UpgradeStation.ToolRadiusMultiplier * Machines.UpgradeStation.ToolTierMultiplier * 0.5f;
            FireSonicProjectile(0.15f, quickDigRadius, 20f, 3f, sonicPulseColor);

            // Flash tip glow
            var glow = GetSonicTipGlow();
            glow.intensity = 4f;
            glow.color = sonicPulseColor;

            // === PHASE 1: RECOIL BACK ===
            Vector3 recoilPos = startPos + new Vector3(0f, 0.02f, -sonicRecoilDistance);
            Quaternion recoilRot = startRot * Quaternion.Euler(-5f, 0f, 0f);

            float elapsed = 0f;
            float duration = 0.05f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, recoilPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, recoilRot, t);
                yield return null;
            }

            // Projectile handles digging on terrain impact
            PlayDigSoundAndEffects();

            // === PHASE 2: SMALL VIBRATION (recoil shake) ===
            Vector3 shakeBase = recoilPos;
            for (int i = 0; i < 3; i++)
            {
                float shakeX = Random.Range(-0.006f, 0.006f);
                float shakeY = Random.Range(-0.006f, 0.006f);
                transform.localPosition = shakeBase + new Vector3(shakeX, shakeY, 0);
                yield return new WaitForSeconds(0.02f);
            }

            // === PHASE 3: RETURN TO IDLE ===
            Vector3 endPos = transform.localPosition;
            Quaternion endRot = transform.localRotation;

            elapsed = 0f;
            duration = 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                transform.localPosition = Vector3.Lerp(endPos, startPos, t);
                transform.localRotation = Quaternion.Slerp(endRot, startRot, t);

                // Fade glow
                glow.intensity = Mathf.Lerp(4f, 0f, t);

                yield return null;
            }

            transform.localPosition = startPos;
            transform.localRotation = startRot;
            glow.intensity = 0f;
            isAnimating = false;
            animationCoroutine = null;
        }

        /// <summary>
        /// Fire a Sonic Pulser projectile (sphere) in world space.
        /// Uses ProjectileExitGizmo for spawn position and direction.
        /// Projectile is on layer 0 (Default) so it's rendered by the main camera
        /// and properly occluded by terrain. SonicProjectile handles movement,
        /// terrain collision, and digging on impact.
        /// </summary>
        /// <param name="visualScale">Visual diameter of the sphere in world units</param>
        /// <param name="digRadius">Actual dig radius on terrain impact</param>
        /// <param name="speed">Travel speed in units/sec</param>
        /// <param name="lifetime">Max lifetime before auto-destroy</param>
        /// <param name="color">Projectile color (emissive)</param>
        private void FireSonicProjectile(float visualScale, float digRadius, float speed, float lifetime, Color color)
        {
            var gizmo = GetProjectileExitGizmo();
            Camera cam = Camera.main;
            if (gizmo == null && cam == null) return;

            // Spawn position and direction from gizmo (or fallback to camera)
            Vector3 spawnPos = gizmo != null ? gizmo.ExitPoint : cam.transform.position;
            Vector3 fireDir = gizmo != null ? gizmo.FireDirection : cam.transform.forward;

            // Offset spawn slightly forward so it appears in front of the tool overlay
            spawnPos += fireDir * 0.5f;

            // Create sphere in world space
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "SonicProjectile";
            orb.layer = 0; // Default layer - rendered by main camera, occluded by terrain
            orb.transform.position = spawnPos;
            orb.transform.localScale = Vector3.one * visualScale;

            // Remove default collider (SonicProjectile uses raycast)
            var col = orb.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Set up emissive material
            var orbRenderer = orb.GetComponent<MeshRenderer>();
            orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 3f);
                }
                orbRenderer.material = mat;
            }

            // Dig strength always max (1.0) since DigOperation clamps to 0-1.
            // The dig radius controls the hole size.
            float digStrength = 1.0f;

            // Attach projectile behavior
            var projectile = orb.AddComponent<SonicProjectile>();
            projectile.Init(fireDir, speed, lifetime, digRadius, digStrength);
        }

        /// <summary>
        /// Create the charge ball visual at the barrel tip during reload.
        /// Parented to the tip (tool hierarchy, layer 8) so it renders on the overlay camera
        /// and stays at the barrel. Local scale accounts for the tool's 30x lossy scale.
        /// </summary>
        private void CreateChargeBall(Transform tip)
        {
            DestroyChargeBall(); // Clean up any existing one

            chargeBallObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chargeBallObj.name = "SonicChargeBall";
            chargeBallObj.layer = gameObject.layer; // Layer 8 for overlay camera
            chargeBallObj.transform.SetParent(tip, false);
            chargeBallObj.transform.localPosition = Vector3.zero;
            chargeBallObj.transform.localScale = Vector3.one * 0.0001f; // Start invisible

            // Remove collider
            var col = chargeBallObj.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Set up glowing material
            var renderer = chargeBallObj.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Use tool's own material (proven to work on overlay camera)
            Material toolMat = null;
            var toolRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in toolRenderers)
            {
                if (r.material != null && r.material.shader != null)
                {
                    toolMat = r.material;
                    break;
                }
            }

            Color chargeColor = Color.Lerp(sonicPulseColor, sonicChargedPulseColor, 0.5f);
            if (toolMat != null)
            {
                var mat = new Material(toolMat);
                mat.color = chargeColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", chargeColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", chargeColor * 2f);
                }
                renderer.material = mat;
            }
        }

        /// <summary>
        /// Update the charge ball size during reload.
        /// Ball grows from tiny to a proportional size at the barrel tip.
        /// Uses tip's lossy scale to convert desired world size to local scale.
        /// </summary>
        private void UpdateChargeBall(Transform tip, float progress)
        {
            if (chargeBallObj == null) return;

            // Size grows without cap - sqrt curve for fast initial growth, keeps growing
            float minWorldSize = 0.01f;
            float growthScale = 0.12f; // Size per sqrt(progress)
            float currentWorldSize = minWorldSize + growthScale * Mathf.Sqrt(progress);

            // Convert world size to local scale (accounting for tool's ~30x scale)
            float parentScale = Mathf.Max(tip.lossyScale.x, 0.01f);
            float localScale = currentWorldSize / parentScale;
            chargeBallObj.transform.localScale = Vector3.one * localScale;

            // Pulse the emission for visual feedback
            var renderer = chargeBallObj.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.3f;
                Color chargeColor = Color.Lerp(sonicPulseColor, sonicChargedPulseColor, progress);
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", chargeColor * (2f + progress * 3f) * pulse);
                }
                if (renderer.material.HasProperty("_BaseColor"))
                {
                    renderer.material.SetColor("_BaseColor", chargeColor);
                }
            }
        }

        /// <summary>
        /// Destroy the charge ball visual.
        /// </summary>
        private void DestroyChargeBall()
        {
            if (chargeBallObj != null)
            {
                Object.Destroy(chargeBallObj);
                chargeBallObj = null;
            }
        }

        /// <summary>
        /// Reload coroutine - charge ball grows at barrel while player holds trigger.
        /// Charges indefinitely while isReloading is true.
        /// When chargePaused is true (energy depleted), ball stops growing but stays visible.
        /// Does NOT auto-fire - player must release trigger (ReleaseChargedAttack handles firing).
        /// </summary>
        private IEnumerator SonicPulserReloadCoroutine()
        {
            isReloading = true;
            reloadTimer = 0f;

            var glow = GetSonicTipGlow();
            glow.color = new Color(0.4f, 0.85f, 1f, 1f); // Cyan during reload

            // Create the charging ball at the gizmo's charge point
            var gizmo = GetProjectileExitGizmo();
            Transform chargePoint = gizmo != null ? gizmo.ChargeBallTransform : GetSonicTipPoint();
            CreateChargeBall(chargePoint);

            // Play charge sound (looping)
            if (chargeSpinSound != null && audioSource != null)
            {
                audioSource.clip = chargeSpinSound;
                audioSource.loop = true;
                audioSource.pitch = 0.6f;
                audioSource.Play();
            }

            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // Reload tilt target: tool tilts up slightly (loading pose)
            Quaternion reloadTiltRot = startRot * Quaternion.Euler(-12f, 3f, 0f);
            Vector3 reloadTiltPos = startPos + new Vector3(0.01f, 0.03f, -0.04f);

            while (isReloading)
            {
                // Only grow the charge when not paused and under max time
                if (!chargePaused && reloadTimer < CHARGE_MAX_SECONDS)
                {
                    reloadTimer += Time.deltaTime;
                    if (reloadTimer >= CHARGE_MAX_SECONDS)
                    {
                        reloadTimer = CHARGE_MAX_SECONDS;
                        chargePaused = true; // Stop growing permanently
                    }
                }

                float progress = reloadTimer / CHARGE_RATE; // Uncapped

                // Smooth tilt into reload pose (first half), hold (second half)
                float tiltT = Mathf.Clamp01(progress * 2.5f); // Reaches full tilt at 40%
                tiltT = EaseOutCubic(tiltT);
                Quaternion currentRot = Quaternion.Slerp(startRot, reloadTiltRot, tiltT);
                Vector3 currentPos = Vector3.Lerp(startPos, reloadTiltPos, tiltT);

                // Add escalating vibration (reduced when paused)
                float vibScale = chargePaused ? 0.3f : 1f;
                float vibIntensity = Mathf.Lerp(0.001f, 0.01f, progress) * vibScale;
                float vibX = (Mathf.PerlinNoise(Time.time * 40f, 0f) - 0.5f) * vibIntensity * 2f;
                float vibY = (Mathf.PerlinNoise(0f, Time.time * 40f) - 0.5f) * vibIntensity * 2f;

                transform.localPosition = currentPos + new Vector3(vibX, vibY, 0f);
                transform.localRotation = currentRot;

                // Growing glow (asymptotic so it doesn't go infinite)
                float glowT = 1f - 1f / (1f + progress);  // 0→0, 1→0.5, 3→0.75, ∞→1
                glow.intensity = 0.5f + glowT * 8f;
                glow.range = 1f + glowT * 5f;

                // Grow the charge ball - from tiny to the charged shot size
                UpdateChargeBall(chargePoint, progress);

                // Audio pitch rises with charge progress (hold pitch when paused)
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.pitch = Mathf.Lerp(0.6f, 1.4f, progress);
                }

                yield return null;
            }

            // Coroutine exits when isReloading is set to false externally
            // (by ReleaseChargedAttack or CancelCharging)
            reloadCoroutine = null;
        }

        /// <summary>
        /// Charged shot animation - fires bigger, faster orb with heavy recoil.
        /// Called after reload completes.
        /// </summary>
        private IEnumerator SonicPulserChargedShotAnimation(float chargePower)
        {
            isAnimating = true;
            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;

            // Stop any remaining charge/reload effects
            DestroyChargeBall();

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            // Play release sound
            if (chargeReleaseSound != null && audioSource != null)
            {
                audioSource.pitch = 0.8f + chargePower * 0.4f;
                audioSource.PlayOneShot(chargeReleaseSound, 0.8f + chargePower * 0.4f);
            }

            // Fire charged projectile - visual matches the charge ball size (same formula as UpdateChargeBall)
            float chargedVisual = 0.01f + 0.12f * Mathf.Sqrt(chargePower);
            float baseDigRadius = Digging.DiggingSystem.Instance != null ? Digging.DiggingSystem.Instance.DigRadius : 0.5f;
            float chargedDigRadius = baseDigRadius * Machines.UpgradeStation.ToolRadiusMultiplier
                * Machines.UpgradeStation.ToolTierMultiplier * (1f + chargePower * 0.6f);
            float chargedSpeed = 18f + Mathf.Min(chargePower, 3f) * 8f; // Speed caps at reasonable value
            Color chargedColor = Color.Lerp(sonicPulseColor, sonicChargedPulseColor, Mathf.Clamp01(chargePower));
            FireSonicProjectile(chargedVisual, chargedDigRadius, chargedSpeed, 3f, chargedColor);

            // Flash tip glow brighter
            var glow = GetSonicTipGlow();
            float glowCharge = Mathf.Min(chargePower, 5f);
            glow.intensity = 6f + glowCharge * 8f;
            glow.color = Color.Lerp(sonicPulseColor, Color.white, Mathf.Clamp01(chargePower * 0.5f));
            glow.range = 3f + glowCharge * 4f;

            // === PHASE 1: STRONG RECOIL ===
            float recoilDist = sonicRecoilDistance * (1f + Mathf.Min(chargePower, 3f) * 2.5f);
            Vector3 recoilPos = startPos + new Vector3(0f, 0.04f, -recoilDist);
            Quaternion recoilRot = startRot * Quaternion.Euler(-10f - chargePower * 8f, 0f, 0f);

            float elapsed = 0f;
            float duration = 0.04f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, recoilPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, recoilRot, t);
                yield return null;
            }

            // Projectile handles digging on terrain impact
            PlayDigSoundAndEffects();

            // === PHASE 2: HEAVY VIBRATION/SHAKE ===
            int shakeCount = 4 + Mathf.RoundToInt(chargePower * 5);
            float shakeIntensity = 0.008f + chargePower * 0.015f;
            Vector3 shakeBase = recoilPos;

            for (int i = 0; i < shakeCount; i++)
            {
                float sx = Random.Range(-shakeIntensity, shakeIntensity);
                float sy = Random.Range(-shakeIntensity, shakeIntensity);
                transform.localPosition = shakeBase + new Vector3(sx, sy, 0);
                yield return new WaitForSeconds(0.025f);
            }

            // === PHASE 3: RETURN TO IDLE ===
            Vector3 endPos = transform.localPosition;
            Quaternion endRot = transform.localRotation;

            elapsed = 0f;
            duration = 0.25f + chargePower * 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                transform.localPosition = Vector3.Lerp(endPos, startPos, t);
                transform.localRotation = Quaternion.Slerp(endRot, startRot, t);

                // Fade glow
                glow.intensity = Mathf.Lerp(6f + chargePower * 8f, 0f, t);

                yield return null;
            }

            // Reset
            transform.localPosition = startPos;
            transform.localRotation = startRot;
            glow.intensity = 0f;

            isAnimating = false;
            animationCoroutine = null;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private IEnumerator AnimateToPosition(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float duration, System.Func<float, float> easeFunc)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = easeFunc(Mathf.Clamp01(elapsed / duration));

                transform.localPosition = Vector3.Lerp(fromPos, toPos, t);
                transform.localRotation = Quaternion.Slerp(fromRot, toRot, t);

                yield return null;
            }

            transform.localPosition = toPos;
            transform.localRotation = toRot;
        }

        private IEnumerator ShakeEffect(Vector3 basePos, float amount, int count, float duration)
        {
            float perShake = duration / count;
            for (int i = 0; i < count; i++)
            {
                float shakeX = Random.Range(-amount, amount);
                float shakeY = Random.Range(-amount, amount);
                transform.localPosition = basePos + new Vector3(shakeX, shakeY, 0);
                yield return new WaitForSeconds(perShake);
            }
        }

        private void ResetToIdle(Vector3 pos, Quaternion rot)
        {
            transform.localPosition = pos;
            transform.localRotation = rot;
            isAnimating = false;
            animationCoroutine = null;
        }

        private void PlaySwingSound()
        {
            if (swingSound != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(swingSound, 0.6f);
            }
        }

        private void PlayDigSoundAndEffects()
        {
            if (digSounds != null && digSounds.Length > 0 && audioSource != null)
            {
                AudioClip clip = digSounds[Random.Range(0, digSounds.Length)];
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(clip);
            }

            if (trailParticles != null)
            {
                trailParticles.Emit(8);
            }
        }

        private void TriggerDigHit()
        {
            HeldToolController.Instance?.TriggerDigHit();
        }

        // ============================================================
        // EASING FUNCTIONS
        // ============================================================

        private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private float EaseInQuad(float t) => t * t;
        private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private float EaseInCubic(float t) => t * t * t;
        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ============================================================
        // PUBLIC METHODS
        // ============================================================

        public void SetBaseTransform()
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
        }

        public void SetAnimationType(ToolAnimationType type)
        {
            animationType = type;
        }

        /// <summary>
        /// Set the animation speed multiplier based on dig speed.
        /// Higher dig speed = faster animations.
        /// </summary>
        public void SetAnimationSpeed(float digSpeed)
        {
            animationSpeedMultiplier = Mathf.Max(0.5f, digSpeed);
        }

        /// <summary>
        /// Get duration adjusted by animation speed multiplier.
        /// </summary>
        private float GetAdjustedDuration(float baseDuration)
        {
            return baseDuration / animationSpeedMultiplier;
        }

        public void PlayEquipAnimation() { }
        public void PlayUnequipAnimation() { }

        public void PlayIdleAnimation()
        {
            isAnimating = false;
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
        }

        public void PlayHitEffect()
        {
            if (hitSounds != null && hitSounds.Length > 0 && audioSource != null)
            {
                AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(clip);
            }
        }

        public void PlayDigSound()
        {
            PlayDigSoundAndEffects();
        }

        public void SetLightEnabled(bool enabled)
        {
            if (toolLight != null) toolLight.enabled = enabled;
        }

        public void SetLightColor(Color color)
        {
            if (toolLight != null) toolLight.color = color;
        }

        public void SetLightRange(float range)
        {
            if (toolLight != null) toolLight.range = range;
        }
    }
}
