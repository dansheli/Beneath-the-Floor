using UnityEngine;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// Main winch anchor component. Holds the cable origin point and system configuration.
    /// Place this on the winch anchor object above the dig pit.
    /// </summary>
    public class WinchAnchor : MonoBehaviour
    {
        public static WinchAnchor Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Upgrade configuration ScriptableObject.")]
        [SerializeField] private WinchUpgradeConfig upgradeConfig;

        [Tooltip("Current winch tier index (0-indexed). Includes length, speed, and power.")]
        [SerializeField] private int currentTierIndex = 0;

        [Header("Attachment Point")]
        [Tooltip("Where the cable attaches to the player (offset from player center).")]
        public Vector3 playerAttachOffset = new Vector3(0f, 0.5f, 0f);

        [Tooltip("How far behind the player's look direction the visual cable endpoint sits.")]
        public float playerAttachBackOffset = 0.45f;

        [Header("Visual Cable Origin (Optional)")]
        [Tooltip("Override transform for where the cable originates. If not set, uses this transform's position.")]
        [SerializeField] private Transform cableOriginTransform;

        [Header("Cable Visual Settings")]
        [Tooltip("Number of segments in the cable LineRenderer.")]
        [Range(2, 16)]
        public int cableSegments = 6;

        [Tooltip("Amount of sag in the cable (0 = straight line).")]
        [Range(0f, 2f)]
        public float cableSagAmount = 0.3f;

        [Tooltip("Cable width.")]
        public float cableWidth = 0.02f;

        [Tooltip("Cable color.")]
        public Color cableColor = new Color(0.4f, 0.35f, 0.2f, 1f);

        [Header("Audio (Optional)")]
        [Tooltip("Sound when cable reaches tension.")]
        public AudioClip tensionSound;

        [Tooltip("Sound while winch is pulling (motor/mechanical sound).")]
        public AudioClip pullMotorSound;

        [Tooltip("Sounds of the cable/rope while winch is pulling (random selection from array).")]
        public AudioClip[] pullCableSounds;

        [Tooltip("Sounds when cable attaches to player (random selection from array).")]
        public AudioClip[] attachSounds;

        [Tooltip("Sounds when cable detaches from player (random selection from array).")]
        public AudioClip[] detachSounds;

        [Header("Audio Volume")]
        [Range(0f, 1f)]
        public float attachDetachVolume = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool enableDebugLogs = false;

        // Runtime state
        private bool isAttached = false;
        private Transform attachedPlayer = null;
        private float reeledCableLength = -1f; // Current reeled length, -1 means not set

        // Events
        public System.Action<Transform> OnPlayerAttached;
        public System.Action OnPlayerDetached;
        public System.Action<int> OnTierChanged;

        // Properties
        public bool IsAttached => isAttached;
        public Transform AttachedPlayer => attachedPlayer;
        public int CurrentTierIndex => currentTierIndex;
        // Legacy properties - now same as cable tier
        public int CurrentMotorSpeedTier => currentTierIndex;
        public int CurrentMotorPowerTier => currentTierIndex;
        public WinchUpgradeConfig Config => upgradeConfig;

        /// <summary>
        /// Get the current max cable length based on tier.
        /// </summary>
        public float MaxCableLength
        {
            get
            {
                if (upgradeConfig == null) return 10f;
                return upgradeConfig.GetMaxLength(currentTierIndex);
            }
        }

        /// <summary>
        /// Get the world position of the anchor point (uses cable origin if set).
        /// </summary>
        public Vector3 AnchorPosition => cableOriginTransform != null ? cableOriginTransform.position : transform.position;

        /// <summary>
        /// Get or set the cable origin transform.
        /// </summary>
        public Transform CableOriginTransform
        {
            get => cableOriginTransform;
            set => cableOriginTransform = value;
        }

        /// <summary>
        /// Get soft zone start distance (based on effective/reeled cable length).
        /// Uses EffectiveCableLength so the soft zone scales correctly when reeling in.
        /// </summary>
        public float SoftZoneStart
        {
            get
            {
                if (upgradeConfig == null) return EffectiveCableLength * 0.85f;
                return EffectiveCableLength * upgradeConfig.softZoneStartFraction;
            }
        }

        /// <summary>
        /// Get pull speed from config (included in winch tier).
        /// </summary>
        public float PullSpeed
        {
            get
            {
                if (upgradeConfig == null) return 3f;
                return upgradeConfig.GetEffectivePullSpeed(currentTierIndex);
            }
        }

        /// <summary>
        /// Get base pull speed (without motor tier modifier).
        /// </summary>
        public float BasePullSpeed
        {
            get
            {
                if (upgradeConfig == null) return 3f;
                return upgradeConfig.pullSpeed;
            }
        }

        /// <summary>
        /// Get motor speed multiplier (included in winch tier).
        /// </summary>
        public float MotorSpeedMultiplier
        {
            get
            {
                if (upgradeConfig == null) return 1f;
                return upgradeConfig.GetMotorSpeedMultiplier(currentTierIndex);
            }
        }

        /// <summary>
        /// Get max damping strength from config (included in winch tier).
        /// </summary>
        public float MaxDampingStrength
        {
            get
            {
                if (upgradeConfig == null) return 0.8f;
                return upgradeConfig.GetEffectiveDamping(currentTierIndex);
            }
        }

        /// <summary>
        /// Get tension resistance (included in winch tier).
        /// </summary>
        public float TensionResistance
        {
            get
            {
                if (upgradeConfig == null) return 0f;
                return upgradeConfig.GetTensionResistance(currentTierIndex);
            }
        }

        /// <summary>
        /// Get energy efficiency (included in winch tier).
        /// </summary>
        public float EnergyEfficiency
        {
            get
            {
                if (upgradeConfig == null) return 1f;
                return upgradeConfig.GetEnergyEfficiency(currentTierIndex);
            }
        }

        /// <summary>
        /// Get the effective cable length (reeled length, clamped to max).
        /// This is what the constraint system should use.
        /// </summary>
        public float EffectiveCableLength
        {
            get
            {
                if (reeledCableLength < 0) return MaxCableLength;
                return Mathf.Min(reeledCableLength, MaxCableLength);
            }
        }

        /// <summary>
        /// Get the current reeled cable length.
        /// </summary>
        public float ReeledCableLength => reeledCableLength < 0 ? MaxCableLength : reeledCableLength;

        private void Awake()
        {
            // FORCE disable debug logs (scene-serialized value may be true)
            enableDebugLogs = false;

            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[WinchAnchor] Multiple instances detected. Using first one.");
            }

            // Create default config if none assigned
            if (upgradeConfig == null)
            {
                upgradeConfig = ScriptableObject.CreateInstance<WinchUpgradeConfig>();
                upgradeConfig.ResetToDefaults();
            }

            // Auto-find cable origin (drum/pulley) if not set
            if (cableOriginTransform == null)
            {
                cableOriginTransform = FindCableOriginTransform();
            }
        }

        /// <summary>
        /// Auto-find the drum/pulley transform for cable origin.
        /// </summary>
        private Transform FindCableOriginTransform()
        {
            // Look for drum/pulley objects by common names
            string[] drumNames = { "Winch.001_LOD0", "Drum", "Pulley", "CableOrigin", "RopeOrigin", "CableExit" };

            foreach (string drumName in drumNames)
            {
                Transform found = FindChildByName(transform, drumName);
                if (found != null) return found;
            }

            // Try partial match for winch/drum mesh
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string childName = child.name.ToLower();
                if ((childName.Contains("winch") || childName.Contains("drum") || childName.Contains("pulley"))
                    && (childName.Contains("lod0") || childName.Contains("mesh")))
                {
                    return child;
                }
            }

            return null;
        }

        private Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            // Ensure WinchLimitPopup exists (must be called after Awake sets Instance)
            UI.WinchLimitPopup.EnsureExists();
        }

        private void Update()
        {
            // Debug keys disabled for release build
            // F9 was: cycle winch tier
        }

        /// <summary>
        /// Attach cable to player.
        /// </summary>
        public void AttachToPlayer(Transform player)
        {
            if (player == null) return;

            isAttached = true;
            attachedPlayer = player;

            // Initialize reeled length to MAX - player has full freedom until they press F to reel in
            reeledCableLength = MaxCableLength;

            if (enableDebugLogs)
                Debug.Log($"[WinchAnchor] Cable attached to {player.name}, full length: {reeledCableLength:F1}m");

            OnPlayerAttached?.Invoke(player);
        }

        /// <summary>
        /// Detach cable from player.
        /// </summary>
        public void DetachFromPlayer()
        {
            if (!isAttached) return;

            isAttached = false;
            attachedPlayer = null;
            reeledCableLength = -1f; // Reset reeled length

            if (enableDebugLogs)
                Debug.Log("[WinchAnchor] Cable detached");

            OnPlayerDetached?.Invoke();
        }

        /// <summary>
        /// Play a random attach sound at the anchor position.
        /// </summary>
        public void PlayAttachSound()
        {
            PlayRandomSound(attachSounds, AnchorPosition, attachDetachVolume);
        }

        /// <summary>
        /// Play a random detach sound at the anchor position.
        /// </summary>
        public void PlayDetachSound()
        {
            PlayRandomSound(detachSounds, AnchorPosition, attachDetachVolume);
        }

        /// <summary>
        /// Get a random cable sound for pulling (returns null if no sounds assigned).
        /// </summary>
        public AudioClip GetRandomPullCableSound()
        {
            if (pullCableSounds == null || pullCableSounds.Length == 0)
                return null;
            return pullCableSounds[Random.Range(0, pullCableSounds.Length)];
        }

        /// <summary>
        /// Play a random sound from an array at the specified position.
        /// Uses boosted audio settings for better audibility.
        /// </summary>
        private void PlayRandomSound(AudioClip[] sounds, Vector3 position, float volume)
        {
            if (sounds == null || sounds.Length == 0)
                return;

            AudioClip clip = sounds[Random.Range(0, sounds.Length)];
            if (clip == null)
                return;

            // Create temporary AudioSource with boosted settings
            GameObject tempGO = new GameObject("TempAudio_Winch");
            tempGO.transform.position = position;

            AudioSource audioSrc = tempGO.AddComponent<AudioSource>();
            audioSrc.clip = clip;
            audioSrc.volume = volume;
            audioSrc.spatialBlend = 0.3f; // Mostly 2D for better audibility
            audioSrc.rolloffMode = AudioRolloffMode.Linear;
            audioSrc.minDistance = 5f;
            audioSrc.maxDistance = 25f;
            audioSrc.Play();

            // Destroy after clip finishes
            Destroy(tempGO, clip.length + 0.1f);
        }

        /// <summary>
        /// Reel in the cable by the specified amount.
        /// Called when player holds the pull button.
        /// </summary>
        /// <param name="amount">Amount to reel in (meters).</param>
        /// <param name="minLength">Minimum cable length (how close player can get).</param>
        public void ReelIn(float amount, float minLength = 1f)
        {
            if (!isAttached) return;

            // Get current distance to player
            float currentDistance = GetCurrentCableLength();

            // Can't reel shorter than current distance (cable is taut)
            // But we can reel to match current distance, which will start pulling
            float newLength = reeledCableLength - amount;
            newLength = Mathf.Max(newLength, minLength); // Don't go below minimum

            reeledCableLength = newLength;

            if (enableDebugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"[WinchAnchor] Reeling: {reeledCableLength:F1}m (player at {currentDistance:F1}m)");
        }

        /// <summary>
        /// Let out cable (opposite of reeling in).
        /// </summary>
        public void LetOut(float amount)
        {
            if (!isAttached) return;

            reeledCableLength = Mathf.Min(reeledCableLength + amount, MaxCableLength);
        }

        /// <summary>
        /// Set the reeled cable length directly (for instant taut).
        /// </summary>
        public void SetReeledLength(float length)
        {
            if (!isAttached) return;
            reeledCableLength = Mathf.Clamp(length, 0f, MaxCableLength);
        }

        /// <summary>
        /// Check if the cable is currently taut (player at or beyond reeled length).
        /// </summary>
        public bool IsCableTaut()
        {
            if (!isAttached) return false;
            return GetCurrentCableLength() >= reeledCableLength - 0.1f;
        }

        /// <summary>
        /// Get current cable length (distance to player).
        /// </summary>
        public float GetCurrentCableLength()
        {
            if (!isAttached || attachedPlayer == null) return 0f;

            Vector3 playerAttachPoint = attachedPlayer.position + playerAttachOffset;
            return Vector3.Distance(AnchorPosition, playerAttachPoint);
        }

        /// <summary>
        /// Get normalized tension (0 = slack, 1 = max).
        /// Uses effective cable length (reeled length).
        /// </summary>
        public float GetTension()
        {
            float length = GetCurrentCableLength();
            float softStart = SoftZoneStart;
            float max = EffectiveCableLength;

            if (length <= softStart) return 0f;
            if (length >= max) return 1f;

            return (length - softStart) / (max - softStart);
        }

        /// <summary>
        /// Set the current cable tier index.
        /// </summary>
        public void SetTier(int tierIndex)
        {
            if (upgradeConfig == null) return;

            int newTier = Mathf.Clamp(tierIndex, 0, upgradeConfig.TierCount - 1);
            if (newTier != currentTierIndex)
            {
                currentTierIndex = newTier;
                OnTierChanged?.Invoke(currentTierIndex);

                if (enableDebugLogs)
                {
                    var tier = upgradeConfig.GetTier(currentTierIndex);
                    Debug.Log($"[WinchAnchor] Cable tier changed to {currentTierIndex}: {tier?.tierName} ({MaxCableLength}m)");
                }
            }
        }

        /// <summary>
        /// Set the motor speed tier index. Now sets the winch tier (combined upgrade).
        /// </summary>
        public void SetMotorSpeedTier(int tierIndex)
        {
            SetTier(tierIndex); // Speed is now part of winch tier
        }

        /// <summary>
        /// Set the motor power tier index. Now sets the winch tier (combined upgrade).
        /// </summary>
        public void SetMotorPowerTier(int tierIndex)
        {
            SetTier(tierIndex); // Power is now part of winch tier
        }

        /// <summary>
        /// Cycle to next winch tier (for testing).
        /// </summary>
        [ContextMenu("Cycle Winch Tier (Debug)")]
        public void CycleTier()
        {
            if (upgradeConfig == null) return;

            int nextTier = (currentTierIndex + 1) % upgradeConfig.TierCount;
            SetTier(nextTier);

            var tier = upgradeConfig.GetTier(currentTierIndex);
            Debug.Log($"[WinchAnchor] DEBUG: Cycled winch to tier {currentTierIndex}: {tier?.tierName} " +
                      $"(Length: {MaxCableLength}m, Speed: x{tier?.speedMultiplier:F1}, Power: {tier?.tensionResistance:P0})");
        }

        // Motor speed and power cycling removed - now combined in winch tier
        // Use CycleTier() to cycle through all upgrades at once

        /// <summary>
        /// Get the player's attachment world position.
        /// </summary>
        public Vector3 GetPlayerAttachPoint()
        {
            if (attachedPlayer == null) return Vector3.zero;
            return attachedPlayer.position + playerAttachOffset;
        }

        /// <summary>
        /// Get the visual cable endpoint — offset behind the player's look direction
        /// so the cable doesn't appear in front of the first-person camera.
        /// </summary>
        public Vector3 GetPlayerVisualAttachPoint()
        {
            if (attachedPlayer == null) return Vector3.zero;

            Vector3 basePoint = attachedPlayer.position + playerAttachOffset;

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 flatForward = cam.transform.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude > 0.001f)
                {
                    flatForward.Normalize();
                    basePoint -= flatForward * playerAttachBackOffset;
                }
            }

            return basePoint;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw anchor point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position - Vector3.up * 0.5f);

            // Draw max length sphere
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, MaxCableLength);

            // Draw soft zone sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, SoftZoneStart);

            // Draw cable to player if attached in editor
            if (isAttached && attachedPlayer != null)
            {
                Gizmos.color = GetTension() > 0.5f ? Color.red : Color.green;
                Gizmos.DrawLine(transform.position, GetPlayerAttachPoint());
            }
        }

        private void OnDrawGizmosSelected()
        {
            // More detailed gizmos when selected
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);

            // Labels would require Handles (Editor only)
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"Winch Anchor\nTier: {currentTierIndex}\nMax: {MaxCableLength}m");
#endif
        }
    }
}
