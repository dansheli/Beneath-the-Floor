using UnityEngine;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Controls the first-person arms animation and integrates with the digging system.
    /// Attach this to the FPS arms prefab instance.
    /// </summary>
    public class PlayerFPArmsController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Animator component on the arms. Auto-found if not set.")]
        [SerializeField] private Animator armsAnimator;

        [Header("Animation Parameters")]
        [Tooltip("Animator bool parameter for punch/dig animation.")]
        [SerializeField] private string digParamName = "punch";

        [Tooltip("Animator bool parameter for walking.")]
        [SerializeField] private string walkParamName = "walk";

        [Tooltip("Animator bool parameter for running.")]
        [SerializeField] private string runParamName = "run";

        [Tooltip("Animator bool parameter for idle break.")]
        [SerializeField] private string idleBreakParamName = "idle_break";

        [Header("Dig Animation Settings")]
        [Tooltip("Duration to hold the dig animation before returning to idle.")]
        [SerializeField] private float digAnimationDuration = 0.4f;

        [Tooltip("Allow triggering new dig while previous is playing.")]
        [SerializeField] private bool allowDigInterrupt = true;

        [Header("Idle Break Settings")]
        [Tooltip("Time of no input before idle break animation plays.")]
        [SerializeField] private float idleBreakDelay = 5f;

        [Header("Tool Socket")]
        [Tooltip("Transform where tools are attached. Created automatically if not set.")]
        [SerializeField] private Transform rightHandSocket;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // State
        private bool isDigging = false;
        private float digTimer = 0f;
        private float idleTimer = 0f;
        private bool idleBreakPlaying = false;

        // Cached references
        private DiggingSystem diggingSystem;
        private FirstPersonController playerController;

        // Singleton for easy access
        public static PlayerFPArmsController Instance { get; private set; }

        /// <summary>
        /// The transform where tools should be parented to.
        /// </summary>
        public Transform RightHandSocket => rightHandSocket;

        /// <summary>
        /// The animator component.
        /// </summary>
        public Animator Animator => armsAnimator;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[PlayerFPArmsController] Duplicate instance destroyed");
                Destroy(this);
                return;
            }

            // Auto-find animator
            if (armsAnimator == null)
            {
                armsAnimator = GetComponent<Animator>();
                if (armsAnimator == null)
                {
                    armsAnimator = GetComponentInChildren<Animator>();
                }
            }

            if (armsAnimator == null)
            {
                Debug.LogError("[PlayerFPArmsController] No Animator found on arms!");
            }

            // Find or create right hand socket
            SetupRightHandSocket();
        }

        private void Start()
        {
            // Find digging system
            diggingSystem = DiggingSystem.Instance;
            if (diggingSystem == null)
            {
                diggingSystem = FindObjectOfType<DiggingSystem>();
            }

            // Subscribe to dig events
            if (diggingSystem != null)
            {
                diggingSystem.OnDigCompleted += OnDigCompleted;
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerFPArmsController] Subscribed to DiggingSystem.OnDigCompleted");
                }
            }
            else
            {
                Debug.LogWarning("[PlayerFPArmsController] DiggingSystem not found - dig animations won't play automatically");
            }

            // Find player controller for movement state
            playerController = FirstPersonController.Instance;
            if (playerController == null)
            {
                playerController = FindObjectOfType<FirstPersonController>();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerFPArmsController] Started. Animator: {(armsAnimator != null ? armsAnimator.name : "NULL")}, " +
                          $"DiggingSystem: {(diggingSystem != null ? "Found" : "NULL")}, " +
                          $"RightHandSocket: {(rightHandSocket != null ? rightHandSocket.name : "NULL")}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Unsubscribe from events
            if (diggingSystem != null)
            {
                diggingSystem.OnDigCompleted -= OnDigCompleted;
            }
        }

        private void Update()
        {
            // Update dig animation timer
            UpdateDigTimer();

            // Update movement animations
            UpdateMovementAnimations();

            // Update idle break
            UpdateIdleBreak();
        }

        /// <summary>
        /// Sets up the right hand socket for tool attachment.
        /// </summary>
        private void SetupRightHandSocket()
        {
            if (rightHandSocket != null)
                return;

            // Try to find existing socket
            rightHandSocket = transform.Find("RightHandSocket");
            if (rightHandSocket != null)
                return;

            // Try to find the right hand bone
            string[] possibleBoneNames = new string[]
            {
                "RightHand", "Hand_R", "hand_R", "R_Hand", "r_hand",
                "mixamorig:RightHand", "Bip01 R Hand", "R Hand"
            };

            Transform handBone = null;
            foreach (string boneName in possibleBoneNames)
            {
                handBone = FindDeepChild(transform, boneName);
                if (handBone != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[PlayerFPArmsController] Found hand bone: {boneName}");
                    break;
                }
            }

            // Create socket
            GameObject socketObj = new GameObject("RightHandSocket");

            if (handBone != null)
            {
                socketObj.transform.SetParent(handBone);
                socketObj.transform.localPosition = Vector3.zero;
                socketObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Fallback: parent to arms root with offset
                socketObj.transform.SetParent(transform);
                socketObj.transform.localPosition = new Vector3(0.1f, -0.1f, 0.3f);
                socketObj.transform.localRotation = Quaternion.identity;
                Debug.LogWarning("[PlayerFPArmsController] Could not find hand bone, socket created at root with offset");
            }

            rightHandSocket = socketObj.transform;
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerFPArmsController] Created RightHandSocket at {rightHandSocket.position}");
            }
        }

        /// <summary>
        /// Recursively searches for a child by name.
        /// </summary>
        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform found = FindDeepChild(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Called when a dig operation completes.
        /// </summary>
        private void OnDigCompleted(DigResult result)
        {
            // Play dig animation on any dig attempt (success or fail)
            PlayDigAnimation();
        }

        /// <summary>
        /// Plays the dig/punch animation.
        /// </summary>
        public void PlayDigAnimation()
        {
            if (armsAnimator == null)
                return;

            if (isDigging && !allowDigInterrupt)
                return;

            isDigging = true;
            digTimer = digAnimationDuration;

            // Reset idle timer
            idleTimer = 0f;
            if (idleBreakPlaying)
            {
                armsAnimator.SetBool(idleBreakParamName, false);
                idleBreakPlaying = false;
            }

            // Set punch parameter
            armsAnimator.SetBool(digParamName, true);

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerFPArmsController] Playing dig animation (duration: {digAnimationDuration}s)");
            }
        }

        /// <summary>
        /// Updates the dig animation timer.
        /// </summary>
        private void UpdateDigTimer()
        {
            if (!isDigging)
                return;

            digTimer -= Time.deltaTime;

            if (digTimer <= 0f)
            {
                isDigging = false;
                if (armsAnimator != null)
                {
                    armsAnimator.SetBool(digParamName, false);
                }

                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerFPArmsController] Dig animation completed");
                }
            }
        }

        /// <summary>
        /// Updates movement-based animations (walk/run).
        /// </summary>
        private void UpdateMovementAnimations()
        {
            if (armsAnimator == null || playerController == null)
                return;

            // Get movement state from player controller
            bool isMoving = Input.GetButton("Vertical") || Input.GetButton("Horizontal");
            bool isRunning = playerController.IsSprinting;

            // Update animator
            armsAnimator.SetBool(walkParamName, isMoving && !isRunning);
            armsAnimator.SetBool(runParamName, isRunning);

            // Reset idle timer if moving or digging
            if (isMoving || isDigging)
            {
                idleTimer = 0f;
                if (idleBreakPlaying)
                {
                    armsAnimator.SetBool(idleBreakParamName, false);
                    idleBreakPlaying = false;
                }
            }
        }

        /// <summary>
        /// Updates idle break animation logic.
        /// </summary>
        private void UpdateIdleBreak()
        {
            if (armsAnimator == null)
                return;

            // Don't run idle break if digging or moving
            if (isDigging || Input.anyKey)
            {
                idleTimer = 0f;
                return;
            }

            idleTimer += Time.deltaTime;

            if (idleTimer >= idleBreakDelay && !idleBreakPlaying)
            {
                armsAnimator.SetBool(idleBreakParamName, true);
                idleBreakPlaying = true;

                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerFPArmsController] Playing idle break animation");
                }
            }
        }

        /// <summary>
        /// Manually trigger dig animation (can be called from other scripts).
        /// </summary>
        public void TriggerDig()
        {
            PlayDigAnimation();
        }

        /// <summary>
        /// Sets the animator speed for all animations.
        /// </summary>
        public void SetAnimationSpeed(float speed)
        {
            if (armsAnimator != null)
            {
                armsAnimator.speed = speed;
            }
        }

        /// <summary>
        /// Attaches a tool prefab to the right hand socket.
        /// </summary>
        /// <param name="toolPrefab">The tool prefab to instantiate.</param>
        /// <returns>The instantiated tool GameObject.</returns>
        public GameObject AttachTool(GameObject toolPrefab)
        {
            if (toolPrefab == null || rightHandSocket == null)
                return null;

            // Clear existing tools
            ClearAttachedTools();

            // Instantiate new tool
            GameObject tool = Instantiate(toolPrefab, rightHandSocket);
            tool.transform.localPosition = Vector3.zero;
            tool.transform.localRotation = Quaternion.identity;

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerFPArmsController] Attached tool: {toolPrefab.name}");
            }

            return tool;
        }

        /// <summary>
        /// Removes all tools attached to the right hand socket.
        /// </summary>
        public void ClearAttachedTools()
        {
            if (rightHandSocket == null)
                return;

            for (int i = rightHandSocket.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(rightHandSocket.GetChild(i).gameObject);
            }
        }
    }
}
