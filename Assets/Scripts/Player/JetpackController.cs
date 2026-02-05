using UnityEngine;
using BeneathTheFloor.Energy;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Controls the jetpack flight mechanic.
    /// Holding Space while having the jetpack makes the player fly up.
    /// Flying consumes energy.
    /// </summary>
    public class JetpackController : MonoBehaviour
    {
        [Header("Jetpack State")]
        [SerializeField] private bool hasJetpack = false;

        [Header("Flight Settings")]
        [Tooltip("Maximum upward velocity")]
        [SerializeField] private float maxLiftSpeed = 6f;

        [Tooltip("How quickly the jetpack responds to input")]
        [SerializeField] private float liftAcceleration = 15f;

        [Header("Energy Settings")]
        [Tooltip("Delay after stopping jetpack before energy can replenish again")]
        [SerializeField] private float energyReplenishCooldown = 1f;

        [Header("Activation Settings")]
        [Tooltip("How long Space must be held before jetpack activates (allows normal jump first)")]
        [SerializeField] private float holdTimeToActivate = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip jetpackLoopSound;
        [SerializeField] private AudioClip jetpackStartSound;
        [SerializeField] private AudioClip jetpackStopSound;
        [SerializeField] [Range(0f, 1f)] private float loopVolume = 0.7f;

        private AudioSource audioSource;
        private bool isFlying = false;
        private bool wasFlying = false;
        private float currentLiftVelocity = 0f;
        private float spaceHoldTime = 0f;
        private float timeSinceStoppedFlying = 999f;
        private int efficiencyUpgradeLevel = 0;

        // Energy drain per level: base=15, L1=12, L2=9, L3=5
        private static readonly float[] energyDrainByLevel = { 15f, 12f, 9f, 5f };

        // Public properties
        public bool HasJetpack => hasJetpack;
        public bool IsFlying => isFlying;
        public float CurrentLiftVelocity => currentLiftVelocity;
        public int EfficiencyUpgradeLevel => efficiencyUpgradeLevel;
        public float CurrentEnergyPerSecond => GetEffectiveEnergyDrain();

        // Events
        public static event System.Action OnJetpackPickedUp;
        public static event System.Action<bool> OnJetpackActiveChanged;

        private void Awake()
        {
            SetupAudioSource();
        }

        private void SetupAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f; // 2D sound
            audioSource.volume = loopVolume;
        }

        private void Update()
        {
            if (!hasJetpack) return;

            // Check if UI is blocking input
            if (UIState.ShouldBlockGameplayInput())
            {
                StopFlying();
                spaceHoldTime = 0f;
                return;
            }

            // Track how long Space is held
            if (Input.GetButton("Jump"))
            {
                spaceHoldTime += Time.deltaTime;

                // Only activate jetpack after holding for the threshold time
                if (spaceHoldTime >= holdTimeToActivate)
                {
                    TryFly();
                }
            }
            else
            {
                // Space released
                spaceHoldTime = 0f;
                StopFlying();
            }

            // Manage energy replenish block
            UpdateEnergyBlock();

            // Handle sound transitions
            HandleAudio();

            wasFlying = isFlying;
        }

        private void UpdateEnergyBlock()
        {
            if (isFlying)
            {
                // Block energy replenishment while flying
                timeSinceStoppedFlying = 0f;
                if (EnergyManager.Instance != null)
                {
                    EnergyManager.Instance.SetEnergyReplenishBlocked(true);
                }
            }
            else
            {
                timeSinceStoppedFlying += Time.deltaTime;

                // Unblock after cooldown
                if (timeSinceStoppedFlying >= energyReplenishCooldown)
                {
                    if (EnergyManager.Instance != null && EnergyManager.Instance.IsEnergyReplenishBlocked)
                    {
                        EnergyManager.Instance.SetEnergyReplenishBlocked(false);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if jetpack is currently trying to activate (held long enough).
        /// Used by FirstPersonController to allow normal jump on short press.
        /// </summary>
        public bool IsHoldingForJetpack()
        {
            return hasJetpack && spaceHoldTime >= holdTimeToActivate;
        }

        private void TryFly()
        {
            // Check if we have energy
            if (EnergyManager.Instance == null)
            {
                Debug.LogWarning("[JetpackController] No EnergyManager found!");
                isFlying = false;
                currentLiftVelocity = 0f;
                return;
            }

            // Consume energy (rate depends on upgrade level)
            float energyNeeded = GetEffectiveEnergyDrain() * Time.deltaTime;
            bool hasEnergy = EnergyManager.Instance.ConsumeEnergy(energyNeeded);

            if (hasEnergy)
            {
                isFlying = true;

                // Accelerate upward
                currentLiftVelocity = Mathf.MoveTowards(
                    currentLiftVelocity,
                    maxLiftSpeed,
                    liftAcceleration * Time.deltaTime
                );
            }
            else
            {
                // Out of energy
                isFlying = false;
                currentLiftVelocity = Mathf.MoveTowards(currentLiftVelocity, 0f, liftAcceleration * Time.deltaTime);
            }

            // Notify listeners of state change
            if (isFlying != wasFlying)
            {
                OnJetpackActiveChanged?.Invoke(isFlying);
            }
        }

        private void StopFlying()
        {
            if (isFlying)
            {
                OnJetpackActiveChanged?.Invoke(false);
            }
            isFlying = false;
            currentLiftVelocity = Mathf.MoveTowards(currentLiftVelocity, 0f, liftAcceleration * 2f * Time.deltaTime);
        }

        private void HandleAudio()
        {
            // Always update volume so Inspector changes take effect immediately
            if (audioSource != null)
            {
                audioSource.volume = loopVolume;
            }

            if (isFlying && !wasFlying)
            {
                // Started flying
                if (jetpackStartSound != null)
                {
                    audioSource.PlayOneShot(jetpackStartSound, loopVolume);
                }
                if (jetpackLoopSound != null)
                {
                    audioSource.clip = jetpackLoopSound;
                    audioSource.volume = loopVolume;
                    audioSource.Play();
                }
            }
            else if (!isFlying && wasFlying)
            {
                // Stopped flying
                audioSource.Stop();
                if (jetpackStopSound != null)
                {
                    audioSource.PlayOneShot(jetpackStopSound, loopVolume);
                }
            }
        }

        /// <summary>
        /// Called when the player picks up the jetpack from the world.
        /// </summary>
        public void PickupJetpack()
        {
            hasJetpack = true;
            Debug.Log("[JetpackController] Jetpack acquired!");
            OnJetpackPickedUp?.Invoke();
        }

        /// <summary>
        /// Gets the vertical velocity contribution from the jetpack.
        /// Called by FirstPersonController to apply lift.
        /// </summary>
        public float GetLiftVelocity()
        {
            return currentLiftVelocity;
        }

        /// <summary>
        /// Check if jetpack should override normal jump/gravity behavior.
        /// </summary>
        public bool ShouldOverrideVerticalMovement()
        {
            return hasJetpack && isFlying;
        }

        /// <summary>
        /// For debugging/testing - give jetpack without pickup.
        /// </summary>
        [ContextMenu("Give Jetpack")]
        public void GiveJetpack()
        {
            PickupJetpack();
        }

        /// <summary>
        /// For debugging - remove jetpack.
        /// </summary>
        [ContextMenu("Remove Jetpack")]
        public void RemoveJetpack()
        {
            hasJetpack = false;
            isFlying = false;
            audioSource.Stop();
        }

        /// <summary>
        /// Get the effective energy drain per second based on upgrade level.
        /// </summary>
        public float GetEffectiveEnergyDrain()
        {
            int level = Mathf.Clamp(efficiencyUpgradeLevel, 0, energyDrainByLevel.Length - 1);
            return energyDrainByLevel[level];
        }

        /// <summary>
        /// Set the jetpack efficiency upgrade level (0=base, 1-3=upgraded).
        /// Called by UpgradeStation when player purchases jetpack_efficiency upgrade.
        /// </summary>
        public void SetEfficiencyLevel(int level)
        {
            efficiencyUpgradeLevel = Mathf.Clamp(level, 0, energyDrainByLevel.Length - 1);
            Debug.Log($"[JetpackController] Efficiency level set to {efficiencyUpgradeLevel}, drain: {GetEffectiveEnergyDrain()}/sec");
        }

        private void OnDisable()
        {
            // Ensure energy block is removed if jetpack is disabled
            if (EnergyManager.Instance != null && EnergyManager.Instance.IsEnergyReplenishBlocked)
            {
                EnergyManager.Instance.SetEnergyReplenishBlocked(false);
            }
        }
    }
}
