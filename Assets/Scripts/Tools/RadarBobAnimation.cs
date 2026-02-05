using UnityEngine;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Adds a subtle bobbing animation to the radar tool for a more natural held-item feel.
    /// </summary>
    public class RadarBobAnimation : MonoBehaviour
    {
        [Header("Bob Settings")]
        [Tooltip("How much the radar moves up and down")]
        [SerializeField] private float bobAmount = 0.03f;

        [Tooltip("How fast the radar bobs")]
        [SerializeField] private float bobSpeed = 2.5f;

        [Header("Sway Settings")]
        [Tooltip("How much the radar sways side to side")]
        [SerializeField] private float swayAmount = 0.015f;

        [Tooltip("How fast the radar sways (usually slower than bob)")]
        [SerializeField] private float swaySpeed = 1.8f;

        [Header("Rotation Sway")]
        [Tooltip("How much the radar rotates while bobbing")]
        [SerializeField] private float rotationAmount = 2f;

        [Header("Movement Response")]
        [Tooltip("Extra bob when player is moving")]
        [SerializeField] private float movementBobMultiplier = 1.5f;

        [Tooltip("Smoothing for movement detection")]
        [SerializeField] private float movementSmoothing = 5f;

        // Base transform values (set on Start)
        private Vector3 basePosition;
        private Quaternion baseRotation;

        // Animation state
        private float bobTimer = 0f;
        private float swayTimer = 0f;
        private float currentMovementMultiplier = 1f;

        // Movement detection
        private Vector3 lastCameraPosition;
        private Camera playerCamera;

        private void Start()
        {
            // Store the initial local position and rotation as base
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;

            playerCamera = Camera.main;
            if (playerCamera != null)
            {
                lastCameraPosition = playerCamera.transform.position;
            }

            // Randomize starting phase so multiple items don't sync
            bobTimer = Random.Range(0f, Mathf.PI * 2f);
            swayTimer = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            // Detect player movement
            float movementSpeed = 0f;
            if (playerCamera != null)
            {
                Vector3 currentPos = playerCamera.transform.position;
                movementSpeed = (currentPos - lastCameraPosition).magnitude / Time.deltaTime;
                lastCameraPosition = currentPos;
            }

            // Smooth movement multiplier
            float targetMultiplier = movementSpeed > 0.5f ? movementBobMultiplier : 1f;
            currentMovementMultiplier = Mathf.Lerp(currentMovementMultiplier, targetMultiplier, Time.deltaTime * movementSmoothing);

            // Update timers
            bobTimer += Time.deltaTime * bobSpeed * currentMovementMultiplier;
            swayTimer += Time.deltaTime * swaySpeed;

            // Calculate bob offset (up/down)
            float bobOffset = Mathf.Sin(bobTimer) * bobAmount * currentMovementMultiplier;

            // Calculate sway offset (left/right)
            float swayOffset = Mathf.Sin(swayTimer) * swayAmount;

            // Calculate rotation sway
            float rotationOffset = Mathf.Sin(swayTimer * 0.8f) * rotationAmount;

            // Apply position
            Vector3 newPosition = basePosition;
            newPosition.y += bobOffset;
            newPosition.x += swayOffset;
            transform.localPosition = newPosition;

            // Apply rotation
            Vector3 baseEuler = baseRotation.eulerAngles;
            transform.localRotation = Quaternion.Euler(
                baseEuler.x + rotationOffset * 0.5f,
                baseEuler.y,
                baseEuler.z + rotationOffset
            );
        }

        /// <summary>
        /// Reset to base position (useful when showing/hiding)
        /// </summary>
        public void ResetToBase()
        {
            transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
            bobTimer = 0f;
            swayTimer = 0f;
        }

        /// <summary>
        /// Update the base position (call after repositioning the radar)
        /// </summary>
        public void SetNewBase()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
        }
    }
}
