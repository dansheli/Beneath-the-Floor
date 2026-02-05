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
        DrillPike  // Tier 4 - Normal hit + charged spinning super hit
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
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            idleTimer = 0f;
            isAnimating = false;

            // Re-store child transforms on enable
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

            transform.localPosition = initialLocalPosition + new Vector3(bobX, bobY, 0);
            transform.localRotation = initialLocalRotation * Quaternion.Euler(swayX, 0, swayZ);
        }

        public void PlayDigAnimation()
        {
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
        /// Start charging the Drill Pike super attack.
        /// Call this when player starts holding the dig button.
        /// </summary>
        public void StartCharging()
        {
            // Force set to DrillPike if called (HeldToolController already checks SupportsCharging)
            if (animationType != ToolAnimationType.DrillPike)
            {
                Debug.Log($"[ToolVisual] StartCharging called but animationType is {animationType}, forcing to DrillPike");
                animationType = ToolAnimationType.DrillPike;
            }
            if (isCharging) return;

            isCharging = true;
            chargeTime = 0f;
            currentSpinAngle = 0f;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
            }
            chargeCoroutine = StartCoroutine(DrillPikeChargeCoroutine());

            Debug.Log($"[ToolVisual] StartCharging: isCharging={isCharging}, toolHead={toolHead != null}");
        }

        /// <summary>
        /// Release the charged attack.
        /// Call this when player releases the dig button after charging.
        /// </summary>
        public void ReleaseChargedAttack()
        {
            if (!isCharging) return;

            isCharging = false;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            // Calculate charge power (0 to 1)
            float chargePower = Mathf.Clamp01(chargeTime / chargeTimeToMax);

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            animationCoroutine = StartCoroutine(DrillPikeSuperHitAnimation(chargePower));
        }

        /// <summary>
        /// Cancel charging without attacking.
        /// </summary>
        public void CancelCharging()
        {
            if (!isCharging) return;

            isCharging = false;
            chargeTime = 0f;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            // Reset head rotation
            if (toolHead != null)
            {
                toolHead.localRotation = headInitialLocalRot;
            }
        }

        /// <summary>
        /// Check if currently charging.
        /// </summary>
        public bool IsCharging() => isCharging;

        /// <summary>
        /// Get current charge progress (0 to 1).
        /// </summary>
        public float GetChargeProgress() => Mathf.Clamp01(chargeTime / chargeTimeToMax);

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
