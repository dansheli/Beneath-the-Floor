using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Lighting;
using BeneathTheFloor.Tools;

namespace BeneathTheFloor.World
{
    /// <summary>
    /// One-time "Activate Engine" interaction that turns on all lights under a specified root.
    /// Requires the player to be holding a Core Shard to activate.
    /// The shard animates into the crystal holder before powering on.
    /// </summary>
    public class EngineActivationInteract : MonoBehaviour, IInteractable
    {
        // Event fired when crystal is inserted into the engine
        public static event System.Action OnCrystalInserted;

        [Header("Core Shard Settings")]
        [Tooltip("The transform where the Core Shard will fly to (crystal holder position)")]
        [SerializeField] private Transform crystalHolder;

        [Tooltip("Duration of the shard flying animation")]
        [SerializeField] private float shardFlightDuration = 1.2f;

        [Tooltip("Scale of the shard when inserted")]
        [SerializeField] private float insertedShardScale = 0.3f;

        [Header("Light References")]
        [Tooltip("Parent transform containing all lights to activate (e.g., Light_First_Room)")]
        [SerializeField] private Transform lightsRoot;

        [Tooltip("Specific lamps that should be dimmed (lower intensity)")]
        [SerializeField] private Light[] dimmedLamps;

        [Tooltip("Intensity for dimmed lamps")]
        [SerializeField] private float dimmedLampIntensity = 0.2f;

        [Header("Platform Lights (Robot Charging Stations)")]
        [Tooltip("Lights on robot platforms that animate with power-on but have their own intensity")]
        [SerializeField] private Light[] platformLights;

        [Tooltip("Final intensity for platform lights")]
        [SerializeField] private float platformLightIntensity = 1f;

        [Header("Interaction Settings")]
        [Tooltip("Interaction range in meters")]
        [SerializeField] private float interactRange = 2f;

        [Tooltip("Light intensity to set when activated (TEMP for testing)")]
        [SerializeField] private float testIntensity = 10f;

        [Tooltip("If true, interaction is disabled after first activation")]
        [SerializeField] private bool lockAfterActivation = true;

        [Header("Power-On Effect Settings")]
        [Tooltip("Total duration of the power-on sequence")]
        [SerializeField] private float powerOnDuration = 3f;

        [Tooltip("Duration of initial rapid flickering phase")]
        [SerializeField] private float flickerPhaseDuration = 1f;

        [Tooltip("How fast lights flicker during initial phase (flickers per second)")]
        [SerializeField] private float flickerSpeed = 15f;

        [Tooltip("Chance of a glitch/dip during ramp-up (0-1)")]
        [SerializeField] private float glitchChance = 0.3f;

        [Tooltip("How much intensity drops during a glitch (0-1)")]
        [SerializeField] private float glitchIntensityDrop = 0.7f;

        [Header("Engine Emission Settings")]
        [Tooltip("Target emission color when engine is fully active (HDR)")]
        [ColorUsage(true, true)]
        [SerializeField] private Color emissionTargetColor = new Color(3f, 3f, 3f, 1f); // Bright white HDR

        [Tooltip("How much the emission flickers during power-on (0-1)")]
        [SerializeField] private float emissionFlickerAmount = 0.3f;

        [Header("Audio")]
        [Tooltip("Sound that plays when the crystal is sucked into the engine")]
        [SerializeField] private AudioClip crystalInsertSound;
        [SerializeField] private float crystalInsertVolume = 1f;

        [Tooltip("Voice clip that plays when engine activates (e.g., 'SYSTEM ONLINE')")]
        [SerializeField] private AudioClip systemOnlineVoice;

        [Header("State (Read-Only)")]
        [Tooltip("Has the engine been activated?")]
        [SerializeField] private bool activated = false;

        [Tooltip("Is the power-on sequence currently playing?")]
        [SerializeField] private bool isPoweringOn = false;

        // Cached light references
        private Light[] cachedLights;

        // Cached emission materials
        private List<Material> emissionMaterials = new List<Material>();
        private List<Color> originalEmissionColors = new List<Color>();

        // Shader property IDs
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Track if we've logged the null error to prevent spam
        private bool hasLoggedNullError = false;

        // Reference to inserted shard (to exclude from emission changes)
        private GameObject insertedShard = null;

        /// <summary>
        /// IInteractable: Can this object be interacted with?
        /// Returns false if already activated and locked, or if powering on.
        /// </summary>
        public bool CanInteract
        {
            get
            {
                if (isPoweringOn) return false;
                if (lockAfterActivation && activated) return false;
                return true;
            }
        }

        /// <summary>
        /// IInteractable: Text shown in the interaction prompt.
        /// </summary>
        public string GetInteractionText()
        {
            if (isPoweringOn)
            {
                return "Powering On...";
            }
            if (activated)
            {
                return "Engine Active";
            }

            // Check if player is holding the Core Shard
            if (CoreShardPickup.IsHoldingShard)
            {
                return "Press E to Insert Core Shard";
            }

            return "Requires Core Shard";
        }

        /// <summary>
        /// IInteractable: Called when player presses E while looking at this object.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            // Guard against double activation or interaction during power-on
            if (activated || isPoweringOn)
            {
                return;
            }

            // Check if player is holding the Core Shard
            if (!CoreShardPickup.IsHoldingShard)
            {
                Debug.Log("[EngineActivationInteract] Player needs to hold a Core Shard to activate the engine.");
                return;
            }

            // Validate lightsRoot reference
            if (lightsRoot == null)
            {
                if (!hasLoggedNullError)
                {
                    Debug.LogError($"[EngineActivationInteract] lightsRoot is not assigned on {gameObject.name}. Cannot activate lights.");
                    hasLoggedNullError = true;
                }
                return;
            }

            // Start the shard insertion and power-on sequence
            StartCoroutine(InsertShardAndPowerOn());
        }

        /// <summary>
        /// Animates the Core Shard flying into the crystal holder, then powers on.
        /// </summary>
        private IEnumerator InsertShardAndPowerOn()
        {
            isPoweringOn = true;

            CoreShardPickup shard = CoreShardPickup.HeldShard;
            if (shard == null)
            {
                isPoweringOn = false;
                yield break;
            }

            // Disable the CrystalGlow script so it doesn't change the color during/after animation
            CrystalGlow crystalGlow = shard.GetComponent<CrystalGlow>();
            if (crystalGlow != null)
            {
                crystalGlow.enabled = false;
            }

            // Unparent the shard from camera
            Transform shardTransform = shard.transform;
            shardTransform.SetParent(null);

            // Re-enable collider visual but keep it non-interactive
            // (we'll destroy it after animation)

            // Get start and end positions
            Vector3 startPos = shardTransform.position;
            Quaternion startRot = shardTransform.rotation;
            Vector3 startScale = shardTransform.localScale;

            // Target position - use crystalHolder if assigned, otherwise use this transform
            Transform target = crystalHolder != null ? crystalHolder : transform;
            Vector3 endPos = target.position;
            Quaternion endRot = target.rotation;
            Vector3 endScale = Vector3.one * insertedShardScale;

            // Release the shard from player's hand (this also shows the tool again)
            CoreShardPickup.ReleaseHeldShard();

            // Play crystal insertion sound
            if (crystalInsertSound != null)
            {
                AudioSource.PlayClipAtPoint(crystalInsertSound, target.position, crystalInsertVolume);
            }

            Debug.Log("[EngineActivationInteract] Animating Core Shard into crystal holder...");

            // Animate the shard flying to the holder
            float elapsed = 0f;
            while (elapsed < shardFlightDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shardFlightDuration;

                // Smooth easing - start slow, accelerate toward target (like magnetic pull)
                float easedT = t * t * (3f - 2f * t); // Smoothstep
                float magneticT = Mathf.Pow(t, 0.5f); // Faster at the end

                // Blend between smooth and magnetic feel
                float finalT = Mathf.Lerp(easedT, magneticT, t);

                // Interpolate position with a slight arc
                Vector3 midPoint = (startPos + endPos) / 2f + Vector3.up * 0.3f;
                Vector3 pos1 = Vector3.Lerp(startPos, midPoint, finalT);
                Vector3 pos2 = Vector3.Lerp(midPoint, endPos, finalT);
                shardTransform.position = Vector3.Lerp(pos1, pos2, finalT);

                // Interpolate rotation
                shardTransform.rotation = Quaternion.Slerp(startRot, endRot, finalT);

                // Interpolate scale
                shardTransform.localScale = Vector3.Lerp(startScale, endScale, finalT);

                yield return null;
            }

            // Snap to final position
            shardTransform.position = endPos;
            shardTransform.rotation = endRot;
            shardTransform.localScale = endScale;

            // Store reference to the shard so we can exclude it from emission changes
            insertedShard = shard.gameObject;

            // Cache emission materials BEFORE parenting the shard (so shard is not included)
            CacheEmissionMaterials();

            // Parent to the holder so it stays there
            if (crystalHolder != null)
            {
                shardTransform.SetParent(crystalHolder);
            }
            else
            {
                shardTransform.SetParent(transform);
            }

            // Re-enable the CrystalGlow with subtle settings to show the engine is powered
            if (crystalGlow != null)
            {
                crystalGlow.enabled = true;
                // Make the glow more subtle now that it's in the engine
                crystalGlow.SetSubtleMode(0.15f, 0.3f); // Very faint pulse
            }

            Debug.Log("[EngineActivationInteract] Core Shard inserted! Starting power-on sequence...");

            // Fire event for mission system
            OnCrystalInserted?.Invoke();

            // Small delay before power-on for dramatic effect
            yield return new WaitForSeconds(0.3f);

            // Now run the power-on sequence (but isPoweringOn is already true)
            yield return StartCoroutine(PowerOnSequenceInternal());
        }

        /// <summary>
        /// IInteractable: Called when player starts looking at this object.
        /// </summary>
        public void OnHoverEnter()
        {
            // No visual feedback required per constraints
        }

        /// <summary>
        /// IInteractable: Called when player stops looking at this object.
        /// </summary>
        public void OnHoverExit()
        {
            // No visual feedback required per constraints
        }

        /// <summary>
        /// Finds all materials with emission on this GameObject and its children.
        /// </summary>
        private void CacheEmissionMaterials()
        {
            emissionMaterials.Clear();
            originalEmissionColors.Clear();

            // Get all renderers on this object and children
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.materials)
                {
                    // Check if material has emission property
                    if (mat.HasProperty(EmissionColorID))
                    {
                        emissionMaterials.Add(mat);
                        originalEmissionColors.Add(mat.GetColor(EmissionColorID));
                    }
                }
            }

            if (emissionMaterials.Count > 0)
            {
                Debug.Log($"[EngineActivationInteract] Found {emissionMaterials.Count} emission material(s) on {gameObject.name}");
            }
        }

        /// <summary>
        /// Sets emission color on all cached emission materials.
        /// </summary>
        private void SetEmissionColor(Color color)
        {
            foreach (Material mat in emissionMaterials)
            {
                if (mat != null)
                {
                    mat.SetColor(EmissionColorID, color);
                }
            }
        }

        /// <summary>
        /// Cinematic power-on sequence with flickering and gradual stabilization.
        /// </summary>
        private IEnumerator PowerOnSequenceInternal()
        {
            // isPoweringOn is already set by InsertShardAndPowerOn
            // Emission materials were already cached before shard was parented

            // Ensure the root and all children are active
            SetActiveRecursive(lightsRoot.gameObject, true);

            // Cache all Light components
            cachedLights = lightsRoot.GetComponentsInChildren<Light>(true);

            if (cachedLights.Length == 0)
            {
                Debug.LogWarning($"[EngineActivationInteract] No Light components found under '{lightsRoot.name}'.");
                isPoweringOn = false;
                yield break;
            }

            // Ensure all light GameObjects are active and lights are enabled
            foreach (Light light in cachedLights)
            {
                if (!light.gameObject.activeInHierarchy)
                {
                    light.gameObject.SetActive(true);
                }
                light.enabled = true;
                light.intensity = 0f; // Start dark
            }

            // Initialize platform lights (robot charging stations)
            if (platformLights != null)
            {
                foreach (Light light in platformLights)
                {
                    if (light != null)
                    {
                        if (!light.gameObject.activeInHierarchy)
                        {
                            light.gameObject.SetActive(true);
                        }
                        light.enabled = true;
                        light.intensity = 0f; // Start dark
                    }
                }
            }

            int platformCount = platformLights != null ? platformLights.Length : 0;
            Debug.Log($"[EngineActivationInteract] Starting power-on sequence for {cachedLights.Length} light(s) + {platformCount} platform light(s)...");

            // Store the starting emission colors (should be red-ish)
            Color startEmissionColor = originalEmissionColors.Count > 0 ? originalEmissionColors[0] : Color.red;

            float elapsed = 0f;
            float flickerTimer = 0f;
            bool flickerState = false;

            // Phase 1: Flickering that builds up from gentle to intense
            while (elapsed < flickerPhaseDuration)
            {
                elapsed += Time.deltaTime;
                flickerTimer += Time.deltaTime;

                // Progress through flicker phase (0 to 1)
                float flickerProgress = elapsed / flickerPhaseDuration;

                // Flicker speed starts slow (3/sec) and builds to full speed
                float currentFlickerSpeed = Mathf.Lerp(3f, flickerSpeed, flickerProgress * flickerProgress);

                // Toggle flicker state at current speed
                if (flickerTimer >= 1f / currentFlickerSpeed)
                {
                    flickerTimer = 0f;
                    flickerState = !flickerState;

                    // Intensity range grows as we progress
                    float minIntensity = Mathf.Lerp(0.05f, 0.1f, flickerProgress);
                    float maxIntensity = Mathf.Lerp(0.15f, 0.5f, flickerProgress);

                    float flickerIntensity = flickerState ? Random.Range(minIntensity, maxIntensity) * testIntensity : 0f;

                    // Bright bursts become more likely as we progress
                    float burstChance = Mathf.Lerp(0.05f, 0.25f, flickerProgress);
                    if (flickerState && Random.value < burstChance)
                    {
                        float burstMin = Mathf.Lerp(0.2f, 0.6f, flickerProgress);
                        float burstMax = Mathf.Lerp(0.4f, 0.9f, flickerProgress);
                        flickerIntensity = Random.Range(burstMin, burstMax) * testIntensity;
                    }

                    SetAllLightsIntensity(flickerIntensity);
                }

                // Emission: slowly start transitioning, with flicker
                float emissionProgress = flickerProgress * 0.3f; // Only 30% progress during flicker phase
                Color currentEmission = Color.Lerp(startEmissionColor, emissionTargetColor, emissionProgress);

                // Add emission flicker
                if (flickerState)
                {
                    float emissionFlicker = 1f + Random.Range(-emissionFlickerAmount, emissionFlickerAmount * 0.5f);
                    currentEmission *= emissionFlicker;
                }
                else
                {
                    currentEmission *= 0.7f; // Dim when lights are off
                }

                SetEmissionColor(currentEmission);

                yield return null;
            }

            // Phase 2: Gradual ramp-up with glitches
            float rampDuration = powerOnDuration - flickerPhaseDuration;
            float rampElapsed = 0f;
            float currentIntensity = 0.3f * testIntensity;
            float targetProgress = 0f;

            while (rampElapsed < rampDuration)
            {
                rampElapsed += Time.deltaTime;
                targetProgress = rampElapsed / rampDuration;

                // Smooth target intensity based on progress (ease-out curve)
                float smoothProgress = 1f - Mathf.Pow(1f - targetProgress, 2f);
                float targetIntensity = smoothProgress * testIntensity;

                // Emission progress: from 30% to 100%
                float emissionProgress = 0.3f + (0.7f * smoothProgress);
                Color targetEmission = Color.Lerp(startEmissionColor, emissionTargetColor, emissionProgress);

                // Random glitches - sudden dips
                if (Random.value < glitchChance * Time.deltaTime * 3f)
                {
                    // Glitch! Drop intensity briefly
                    float glitchIntensity = currentIntensity * (1f - glitchIntensityDrop);
                    SetAllLightsIntensity(glitchIntensity);

                    // Also glitch the emission
                    SetEmissionColor(targetEmission * (1f - glitchIntensityDrop));

                    // Hold the glitch for a short moment
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));

                    // Maybe double-flicker
                    if (Random.value < 0.5f)
                    {
                        SetAllLightsIntensity(currentIntensity * 0.9f);
                        SetEmissionColor(targetEmission * 0.9f);
                        yield return new WaitForSeconds(Random.Range(0.02f, 0.05f));
                        SetAllLightsIntensity(glitchIntensity * 0.5f);
                        SetEmissionColor(targetEmission * 0.5f);
                        yield return new WaitForSeconds(Random.Range(0.02f, 0.04f));
                    }
                }

                // Smoothly approach target intensity
                currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * 5f);

                // Add subtle flickering during ramp
                float flicker = 1f + Random.Range(-0.05f, 0.05f) * (1f - targetProgress);
                SetAllLightsIntensity(currentIntensity * flicker);

                // Update emission with subtle flicker
                float emissionFlicker = 1f + Random.Range(-0.03f, 0.03f) * (1f - targetProgress);
                SetEmissionColor(targetEmission * emissionFlicker);

                yield return null;
            }

            // Phase 3: Final stabilization
            float stabilizeDuration = 0.5f;
            float stabilizeElapsed = 0f;
            currentIntensity = cachedLights[0].intensity;
            Color currentEmissionColor = emissionMaterials.Count > 0 ? emissionMaterials[0].GetColor(EmissionColorID) : emissionTargetColor;

            while (stabilizeElapsed < stabilizeDuration)
            {
                stabilizeElapsed += Time.deltaTime;
                float t = stabilizeElapsed / stabilizeDuration;

                // Smooth ease-out to final intensity
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);
                float finalIntensity = Mathf.Lerp(currentIntensity, testIntensity, smoothT);
                SetAllLightsIntensity(finalIntensity);

                // Smooth ease-out to final emission color
                Color finalEmission = Color.Lerp(currentEmissionColor, emissionTargetColor, smoothT);
                SetEmissionColor(finalEmission);

                yield return null;
            }

            // Ensure final values are exact
            SetAllLightsIntensity(testIntensity);
            SetEmissionColor(emissionTargetColor);

            isPoweringOn = false;
            activated = true;

            // Play "SYSTEM ONLINE" voice - 2D audio so it's heard at full volume everywhere
            if (systemOnlineVoice != null)
            {
                GameObject audioObj = new GameObject("SystemOnlineAudio");
                AudioSource audioSource = audioObj.AddComponent<AudioSource>();
                audioSource.clip = systemOnlineVoice;
                audioSource.spatialBlend = 0f; // 2D sound - no distance falloff
                audioSource.volume = 1f;
                audioSource.Play();
                Destroy(audioObj, systemOnlineVoice.length + 0.5f);
            }

            // Set dimmed lamps to their lower intensity
            if (dimmedLamps != null)
            {
                foreach (Light lamp in dimmedLamps)
                {
                    if (lamp != null)
                    {
                        lamp.intensity = dimmedLampIntensity;
                    }
                }
            }

            // Set platform lights to their final intensity
            if (platformLights != null)
            {
                foreach (Light light in platformLights)
                {
                    if (light != null)
                    {
                        light.intensity = platformLightIntensity;
                    }
                }
            }

            Debug.Log($"[EngineActivationInteract] Power-on complete! {cachedLights.Length} light(s) at intensity {testIntensity}, {(platformLights != null ? platformLights.Length : 0)} platform light(s) at intensity {platformLightIntensity}. Emission set to white.");
        }

        /// <summary>
        /// Sets intensity for all cached lights and platform lights.
        /// Platform lights use a scaled intensity based on their target vs main target.
        /// </summary>
        private void SetAllLightsIntensity(float intensity)
        {
            if (cachedLights == null) return;

            foreach (Light light in cachedLights)
            {
                if (light != null)
                {
                    light.intensity = intensity;
                }
            }

            // Platform lights animate proportionally to their target intensity
            if (platformLights != null && testIntensity > 0)
            {
                float platformScale = platformLightIntensity / testIntensity;
                float platformIntensity = intensity * platformScale;

                foreach (Light light in platformLights)
                {
                    if (light != null)
                    {
                        light.intensity = platformIntensity;
                    }
                }
            }
        }

        /// <summary>
        /// Recursively sets a GameObject and all its children to active.
        /// </summary>
        private void SetActiveRecursive(GameObject obj, bool active)
        {
            if (obj == null) return;

            obj.SetActive(active);

            foreach (Transform child in obj.transform)
            {
                SetActiveRecursive(child.gameObject, active);
            }
        }

        /// <summary>
        /// Public getter for the activated state (for external queries).
        /// </summary>
        public bool IsActivated => activated;

        /// <summary>
        /// Public getter for interaction range (can be used by custom systems if needed).
        /// </summary>
        public float InteractRange => interactRange;

#if UNITY_EDITOR
        /// <summary>
        /// Draw interaction range gizmo in editor.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = activated ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
#endif
    }
}
