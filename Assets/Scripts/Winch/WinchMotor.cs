using UnityEngine;

namespace BeneathTheFloor.Winch
{
    /// <summary>
    /// All cable-player physics: constraint AND pull.
    /// Replaces WinchPullController + WinchConstraintMotor.
    /// </summary>
    public class WinchMotor : MonoBehaviour
    {
        public static WinchMotor Instance { get; private set; }
        public static System.Action OnCableLimitReached;

        [Header("Reel Settings")]
        [Tooltip("Key to hold for reeling in.")]
        public KeyCode pullKey = KeyCode.F;

        [Tooltip("Speed multiplier applied to config pull speed.")]
        [Range(0.5f, 3f)]
        public float reelSpeedMultiplier = 1f;

        [Tooltip("Minimum cable length (how close to anchor player can get).")]
        public float minCableLength = 1.5f;

        [Tooltip("How fast cable lets out when F is released.")]
        public float letOutSpeed = 10f;

        [Header("Deceleration")]
        [SerializeField] private float decelerationDistance = 3f;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float minDecelerationFactor = 0.15f;

        [Header("Constraint")]
        [Tooltip("Speed of constraint correction when over cable limit.")]
        [Range(1f, 30f)]
        [SerializeField] private float correctionSpeed = 15f;

        [Header("Anchor Push-Down")]
        [Tooltip("When player is closer than this to the anchor, push them down toward the basement floor.")]
        [SerializeField] private float pushDownRadius = 3f;
        [Tooltip("Push-down force strength.")]
        [SerializeField] private float pushDownSpeed = 4f;

        [Header("Docked State")]
        [Tooltip("Distance player must move from docked position to undock.")]
        public float undockDistance = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioSource cableAudioSource;
        [Range(0f, 1f)]
        [SerializeField] private float cableSoundVolume = 0.7f;

        // State
        private bool isReeling;
        private bool isDocked;
        private bool isConstrained;
        private bool hasHitCableLimitThisSession;
        private Vector3 lastDockedPosition;
        private float cableLimitEventCooldown;
        private const float CABLE_LIMIT_EVENT_INTERVAL = 3f; // seconds between popup triggers

        // References
        private WinchAnchor winchAnchor;
        private PlayerWinchAttachment winchAttachment;
        private WinchCable cable;
        private CharacterController cc;

        // Public properties
        public bool IsReeling => isReeling;
        public bool IsDocked => isDocked;
        public bool IsConstrained => isConstrained;
        public bool HasHitCableLimit => hasHitCableLimitThisSession;

        // Events
        public System.Action OnReelStarted;
        public System.Action OnReelStopped;
        public System.Action OnDocked;
        public System.Action OnUndocked;

        private void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool initialized;

        private void EnsureInitialized()
        {
            if (initialized) return;

            if (winchAnchor == null)
                winchAnchor = WinchAnchor.Instance ?? FindObjectOfType<WinchAnchor>();
            if (winchAttachment == null)
                winchAttachment = GetComponent<PlayerWinchAttachment>();
            if (cable == null)
                cable = FindObjectOfType<WinchCable>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (cableAudioSource == null)
            {
                var sources = GetComponents<AudioSource>();
                if (sources.Length > 1)
                    cableAudioSource = sources[1];
                else
                {
                    cableAudioSource = gameObject.AddComponent<AudioSource>();
                    cableAudioSource.playOnAwake = false;
                    cableAudioSource.spatialBlend = 0.3f;
                }
            }

            // Ensure cable exists on winch anchor
            if (cable == null && winchAnchor != null)
                cable = CreateCableOnAnchor(winchAnchor.transform);

            initialized = winchAnchor != null && winchAttachment != null;
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();

            if (winchAttachment == null || !winchAttachment.IsCableAttached || winchAnchor == null)
            {
                if (isReeling) StopReeling();
                if (isDocked) Undock();
                return;
            }

            // Push player down when close to the winch anchor (prevents hovering near the top)
            ApplyAnchorPushDown();

            if (isDocked)
            {
                HandleDocked();
                return;
            }

            bool keyPressed = Input.GetKey(pullKey);

            if (keyPressed)
            {
                // Check docking
                float dist = GetPathLength();
                if (winchAnchor.ReeledCableLength <= minCableLength || dist <= minCableLength + 0.5f)
                {
                    if (isReeling) StopReeling();
                    EnterDocked();
                    return;
                }

                if (!isReeling) StartReeling();
                ApplyPull();
            }
            else
            {
                if (isReeling) StopReeling();
                ApplyLetOut();
                ApplyConstraint();
            }
        }

        // ── PULL ──────────────────────────────────────────────

        private void ApplyPull()
        {
            float baseSpeed = winchAnchor.PullSpeed * reelSpeedMultiplier;
            float dt = Time.deltaTime;
            float reelAmount = baseSpeed * dt;

            // Deceleration near anchor
            float currentReeled = winchAnchor.ReeledCableLength;
            if (currentReeled < decelerationDistance)
            {
                float t = Mathf.Clamp01(currentReeled / decelerationDistance);
                reelAmount *= Mathf.Lerp(minDecelerationFactor, 1f, t);
            }

            // Clamp
            float maxReel = Mathf.Max(0f, currentReeled - minCableLength);
            reelAmount = Mathf.Min(reelAmount, maxReel);
            if (reelAmount <= 0.001f) return;

            // Shorten cable
            winchAnchor.ReelIn(reelAmount, minCableLength);
            float newCableLength = winchAnchor.ReeledCableLength;

            // Pull player toward anchor by reelAmount along the direction to anchor
            Vector3 playerAttach = transform.position + winchAnchor.playerAttachOffset;
            Vector3 toAnchor = (winchAnchor.AnchorPosition - playerAttach);
            float distToAnchor = toAnchor.magnitude;

            if (distToAnchor > 0.1f && cc != null && cc.enabled)
            {
                Vector3 pullDir = toAnchor / distToAnchor;
                // Move by reelAmount but don't overshoot the anchor
                float moveAmount = Mathf.Min(reelAmount, distToAnchor - minCableLength);
                if (moveAmount > 0.001f)
                    cc.Move(pullDir * moveAmount);
            }
        }

        // ── CONSTRAINT ────────────────────────────────────────

        private void ApplyConstraint()
        {
            float currentPathLength = GetPathLength();
            float maxLength = winchAnchor.EffectiveCableLength;

            // Decrement cooldown timer
            if (cableLimitEventCooldown > 0f)
                cableLimitEventCooldown -= Time.deltaTime;

            if (currentPathLength <= maxLength)
            {
                // Auto-reset cable limit flag when back in safe zone
                if (hasHitCableLimitThisSession && currentPathLength < maxLength * 0.85f)
                    hasHitCableLimitThisSession = false;

                isConstrained = false;
                return;
            }

            // Over limit — pull back along cable direction
            isConstrained = true;
            float overshoot = currentPathLength - maxLength;

            Vector3 pullDir = Vector3.up;
            if (cable != null && cable.PathCount >= 2)
                pullDir = -cable.GetDirectionAtPlayer(); // toward anchor/wrap
            else if (winchAnchor != null)
                pullDir = (winchAnchor.AnchorPosition - (transform.position + winchAnchor.playerAttachOffset)).normalized;

            Vector3 correction = pullDir * overshoot * correctionSpeed * Time.deltaTime;
            if (cc != null && cc.enabled)
                cc.Move(correction);

            // Fire cable limit event when player is actively trying to move beyond the limit
            // Check if player is providing movement input (trying to push forward)
            bool isPlayerPushing = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f ||
                                   Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f;

            if (isPlayerPushing && currentPathLength >= winchAnchor.MaxCableLength - 0.5f && cableLimitEventCooldown <= 0f)
            {
                hasHitCableLimitThisSession = true;
                cableLimitEventCooldown = CABLE_LIMIT_EVENT_INTERVAL;
                OnCableLimitReached?.Invoke();
            }
        }

        // ── DOCKED ────────────────────────────────────────────

        private void HandleDocked()
        {
            // Keep cable taut
            winchAnchor.SetReeledLength(minCableLength);

            float distFromDock = Vector3.Distance(transform.position, lastDockedPosition);
            if (distFromDock > undockDistance)
            {
                Undock();
                return;
            }

            float input = Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical"));
            if (input > 0.3f)
                Undock();
        }

        private void EnterDocked()
        {
            if (isDocked) return;
            isDocked = true;
            lastDockedPosition = transform.position;
            winchAnchor.SetReeledLength(minCableLength);
            OnDocked?.Invoke();
        }

        private void Undock()
        {
            if (!isDocked) return;
            isDocked = false;
            OnUndocked?.Invoke();
        }

        // ── ANCHOR PUSH-DOWN ─────────────────────────────────

        private void ApplyAnchorPushDown()
        {
            if (cc == null || !cc.enabled || winchAnchor == null) return;

            Vector3 anchorPos = winchAnchor.AnchorPosition;
            Vector3 playerPos = transform.position;

            // Only push down if player is above the anchor (coming up toward it)
            // and within the push radius horizontally
            float verticalDiff = playerPos.y - anchorPos.y;
            if (verticalDiff > -1f) // player is near or above anchor height
            {
                float horizontalDist = Vector3.Distance(
                    new Vector3(playerPos.x, 0f, playerPos.z),
                    new Vector3(anchorPos.x, 0f, anchorPos.z));

                if (horizontalDist < pushDownRadius)
                {
                    // Stronger push the closer the player is
                    float strength = 1f - Mathf.Clamp01(horizontalDist / pushDownRadius);
                    cc.Move(Vector3.down * (pushDownSpeed * strength * Time.deltaTime));
                }
            }
        }

        // ── LET OUT ───────────────────────────────────────────

        private void ApplyLetOut()
        {
            float currentReeled = winchAnchor.ReeledCableLength;
            float max = winchAnchor.MaxCableLength;
            if (currentReeled < max)
            {
                if (letOutSpeed <= 0)
                    winchAnchor.LetOut(max);
                else
                    winchAnchor.LetOut(letOutSpeed * Time.deltaTime);
            }
        }

        // ── AUDIO ─────────────────────────────────────────────

        private void StartReeling()
        {
            isReeling = true;

            // Snap cable to actual distance so pull starts immediately
            float currentDist = GetPathLength();
            if (currentDist <= 0.01f)
                currentDist = winchAnchor.GetCurrentCableLength();
            winchAnchor.SetReeledLength(currentDist);

            // Motor sound
            if (audioSource != null && winchAnchor.pullMotorSound != null)
            {
                audioSource.clip = winchAnchor.pullMotorSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // Cable sound
            if (cableAudioSource != null)
            {
                AudioClip clip = winchAnchor.GetRandomPullCableSound();
                if (clip != null)
                {
                    cableAudioSource.clip = clip;
                    cableAudioSource.loop = true;
                    cableAudioSource.volume = cableSoundVolume;
                    cableAudioSource.Play();
                }
            }

            OnReelStarted?.Invoke();
        }

        private void StopReeling()
        {
            isReeling = false;
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            if (cableAudioSource != null && cableAudioSource.isPlaying) cableAudioSource.Stop();
            OnReelStopped?.Invoke();
        }

        // ── HELPERS ───────────────────────────────────────────

        private float GetPathLength()
        {
            // Always use straight-line distance for gameplay logic.
            // The visual rope path is longer due to sag/drape and shouldn't affect gameplay.
            return winchAnchor.GetCurrentCableLength();
        }

        /// <summary>
        /// Reset the cable limit flag so the event can fire again.
        /// </summary>
        public void ResetCableLimitFlag()
        {
            hasHitCableLimitThisSession = false;
        }

        /// <summary>
        /// Check if player is currently at or beyond the cable limit.
        /// </summary>
        public bool IsAtCableLimit()
        {
            if (winchAnchor == null) return false;
            float currentLength = GetPathLength();
            return currentLength >= winchAnchor.MaxCableLength - 0.5f;
        }

        private WinchCable CreateCableOnAnchor(Transform anchorTransform)
        {
            GameObject cableObj = new GameObject("Cable");
            cableObj.transform.SetParent(anchorTransform);
            cableObj.transform.localPosition = Vector3.zero;
            cableObj.AddComponent<LineRenderer>();
            return cableObj.AddComponent<WinchCable>();
        }
    }
}
