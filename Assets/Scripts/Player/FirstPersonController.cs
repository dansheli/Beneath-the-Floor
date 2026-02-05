using UnityEngine;
using BeneathTheFloor.UI;
using BeneathTheFloor.Winch;

namespace BeneathTheFloor.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.5f;

        [Header("Mouse Look Settings")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 85f;
        [SerializeField] private Transform cameraTransform;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask;

        [Header("Crouch Settings")]
        [SerializeField] private float standingHeight = 1.5f;
        [SerializeField] private float crouchHeight = 0.75f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Jetpack reference (auto-found if not assigned)
        private JetpackController jetpackController;
        private WinchMotor winchMotor;

        private CharacterController controller;
        private Vector3 velocity;
        private bool isGrounded;
        private bool isSprinting;
        private bool isCrouching;
        private float xRotation = 0f;
        private float targetHeight;
        private bool isBeingDestroyedAsDuplicate = false;



        public static FirstPersonController Instance { get; private set; }

        public bool CanMove { get; set; } = true;
        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public bool IsCrouching => isCrouching;

        private void Awake()
        {
            // Fix singleton pattern for scene transitions:
            // If Instance exists but the GameObject was destroyed (scene change), clear it
            if (Instance != null && Instance.gameObject == null)
            {
                Instance = null;
            }

            if (Instance == null)
            {
                Instance = this;
                if (enableDebugLogs) Debug.Log($"[FirstPersonController] Player instance created in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            }
            else if (Instance != this)
            {
                if (enableDebugLogs) Debug.LogWarning($"[FirstPersonController] Duplicate player found, destroying this one. Existing: {Instance.gameObject.name}");
                isBeingDestroyedAsDuplicate = true;
                Destroy(gameObject);
                return;
            }

            controller = GetComponent<CharacterController>();
            targetHeight = standingHeight;
        }

        private void OnDestroy()
        {
            // ONLY clear singleton if this was a duplicate being destroyed
            // Do NOT clear if the main player is being destroyed (e.g., during scene unload)
            // This prevents the "no camera" issue during scene transitions
            if (Instance == this && isBeingDestroyedAsDuplicate)
            {
                if (enableDebugLogs) Debug.Log("[FirstPersonController] Duplicate player destroyed, clearing singleton reference");
                Instance = null;
            }
            else if (Instance == this)
            {
                // Main player destroyed (scene unload) - do NOT clear Instance
                // The new scene's player will take over in its Awake()
                if (enableDebugLogs) Debug.Log("[FirstPersonController] Main player destroyed (scene unload) - keeping singleton for transition");
            }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // Initialize CharacterController with correct height and center
            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight / 2f, 0f);
            targetHeight = standingHeight;

            // Robust camera finding - check multiple sources
            if (cameraTransform == null)
            {
                // First try Camera.main
                cameraTransform = Camera.main?.transform;

                // If still null, look for camera in our children (CameraHolder/Main Camera)
                if (cameraTransform == null)
                {
                    var cameraHolder = transform.Find("CameraHolder");
                    if (cameraHolder != null)
                    {
                        var mainCam = cameraHolder.Find("Main Camera");
                        if (mainCam != null)
                        {
                            cameraTransform = mainCam;
                            if (enableDebugLogs) Debug.Log("[FirstPersonController] Found camera in CameraHolder child");
                        }
                    }
                }

                // If still null, find any camera tagged MainCamera
                if (cameraTransform == null)
                {
                    var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
                    if (mainCamGO != null)
                    {
                        cameraTransform = mainCamGO.transform;
                        if (enableDebugLogs) Debug.Log("[FirstPersonController] Found camera by MainCamera tag");
                    }
                }

                // Last resort: find any Camera in scene
                if (cameraTransform == null)
                {
                    var anyCamera = Object.FindObjectOfType<Camera>();
                    if (anyCamera != null)
                    {
                        cameraTransform = anyCamera.transform;
                        if (enableDebugLogs) Debug.LogWarning("[FirstPersonController] Using fallback camera (not MainCamera tagged)");
                    }
                }
            }

            if (cameraTransform == null)
            {
                Debug.LogError("[FirstPersonController] NO CAMERA FOUND! Player will not have mouse look.");
            }
            else
            {
                if (enableDebugLogs) Debug.Log($"[FirstPersonController] Using camera: {cameraTransform.name}");
            }

            // Find JetpackController
            jetpackController = GetComponent<JetpackController>();
            if (jetpackController == null)
            {
                jetpackController = GetComponentInChildren<JetpackController>();
            }

            winchMotor = GetComponent<WinchMotor>();

            // Log spawn position and state
            if (enableDebugLogs) Debug.Log($"[FirstPersonController] Started at position: {transform.position}, CanMove: {CanMove}, Controller enabled: {controller.enabled}");
        }

        private void Update()
        {
            HandleGroundCheck();

            // Block movement and look when UI is open
            bool canAct = CanMove && !UIState.ShouldBlockGameplayInput();

            if (canAct)
            {
                HandleMouseLook();
                HandleMovement();
                HandleCrouch();
            }

            ApplyGravity();
        }

        private void HandleGroundCheck()
        {
            if (groundCheck != null)
            {
                isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            }
            else
            {
                isGrounded = controller.isGrounded;
            }

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }

            transform.Rotate(Vector3.up * mouseX);
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            moveDirection = moveDirection.normalized;

            // Sprint handling
            isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && vertical > 0;

            // Determine current speed
            float currentSpeed = walkSpeed;
            if (isSprinting)
            {
                currentSpeed = sprintSpeed;
            }
            else if (isCrouching)
            {
                currentSpeed = crouchSpeed;
            }

            bool winchReeling = winchMotor != null && winchMotor.IsReeling;
            if (!winchReeling)
                controller.Move(moveDirection * currentSpeed * Time.deltaTime);

            // Jump - allowed even with jetpack (short press jumps, long hold activates jetpack)
            bool jetpackHolding = jetpackController != null && jetpackController.IsHoldingForJetpack();
            if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching && !jetpackHolding)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        private void HandleCrouch()
        {
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                if (isCrouching)
                {
                    // Only allow standing up if there's enough headroom
                    if (HasHeadroom())
                    {
                        isCrouching = false;
                        targetHeight = standingHeight;
                    }
                }
                else
                {
                    isCrouching = true;
                    targetHeight = crouchHeight;
                }
            }

            // Smoothly transition height
            float currentHeight = controller.height;
            if (Mathf.Abs(currentHeight - targetHeight) > 0.01f)
            {
                float newHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
                float heightDelta = newHeight - currentHeight;

                controller.height = newHeight;

                // Keep feet on ground: center at half-height
                controller.center = new Vector3(0f, newHeight / 2f, 0f);

                // Push transform down by half the height reduction so the feet stay planted
                // and the head actually lowers (prevents CC from pushing us up)
                if (heightDelta < 0)
                {
                    transform.position += new Vector3(0f, heightDelta / 2f, 0f);
                }
                else
                {
                    // Standing up: only move up if there's clearance (avoid head-in-ceiling)
                    transform.position += new Vector3(0f, heightDelta / 2f, 0f);
                }
            }

            // Always keep camera at the right height for current controller state
            if (cameraTransform != null)
            {
                // Camera eye position relative to CameraHolder parent
                Transform camParent = cameraTransform.parent;
                if (camParent != null && camParent != transform)
                {
                    // Camera is inside CameraHolder - adjust CameraHolder Y
                    Vector3 holderPos = camParent.localPosition;
                    holderPos.y = controller.height - 0.2f;
                    camParent.localPosition = holderPos;
                }
                else
                {
                    // Camera is direct child of player
                    Vector3 camPos = cameraTransform.localPosition;
                    camPos.y = controller.height - 0.2f;
                    cameraTransform.localPosition = camPos;
                }
            }
        }

        /// <summary>
        /// Check if there's enough vertical space above the player to stand up.
        /// </summary>
        private bool HasHeadroom()
        {
            float extraHeight = standingHeight - controller.height;
            if (extraHeight <= 0.01f) return true;

            // Cast a sphere upward from the top of the current capsule
            float radius = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * controller.height;

            return !Physics.SphereCast(origin, radius, Vector3.up, out _, extraHeight, ~0, QueryTriggerInteraction.Ignore);
        }

        private void ApplyGravity()
        {
            bool winchActive = winchMotor != null && winchMotor.IsReeling;
            if (winchActive)
            {
                // Winch pull controller handles all movement — zero out gravity
                velocity.y = 0f;
            }
            else if (jetpackController != null && jetpackController.IsFlying)
            {
                // Jetpack overrides gravity - apply lift velocity
                float liftVelocity = jetpackController.GetLiftVelocity();
                velocity.y = liftVelocity;
            }
            else
            {
                // Normal gravity
                velocity.y += gravity * Time.deltaTime;
            }

            if (!winchActive)
                controller.Move(velocity * Time.deltaTime);
        }

        public void SetCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// Sets the player's walk speed (used by upgrade system)
        /// </summary>
        public void SetMoveSpeed(float speed)
        {
            walkSpeed = speed;
            if (enableDebugLogs) Debug.Log($"[FirstPersonController] Walk speed set to {speed}");
        }

        /// <summary>
        /// Gets the current walk speed
        /// </summary>
        public float GetMoveSpeed()
        {
            return walkSpeed;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }
        }
    }
}
