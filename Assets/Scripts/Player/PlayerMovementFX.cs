using UnityEngine;
using System.Collections;

namespace BeneathTheFloor.Player
{
    public class PlayerMovementFX : MonoBehaviour
    {
        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobFrequency = 8f; // Reduced from 10f for smoother feel
        [SerializeField] private float bobAmplitudeX = 0.01f; // Reduced from 0.02f
        [SerializeField] private float bobAmplitudeY = 0.02f; // Reduced from 0.04f
        [SerializeField] private float sprintBobMultiplier = 1.2f; // Reduced from 1.3f

        [Header("Camera Tilt")]
        [SerializeField] private bool enableCameraTilt = false; // Disabled by default for stability
        [SerializeField] private float tiltAmount = 0.5f; // Reduced from 2f for subtlety
        [SerializeField] private float tiltSmoothing = 12f; // Increased for smoother transitions

        [Header("Landing Impact")]
        [SerializeField] private bool enableLandingImpact = true;
        [SerializeField] private float landingBobAmount = 0.15f;
        [SerializeField] private float landingBobDuration = 0.3f;
        [SerializeField] private float minFallDistance = 1f;

        [Header("FOV Effects")]
        [SerializeField] private bool enableFOVEffects = true;
        [SerializeField] private float baseFOV = 70f;
        [SerializeField] private float sprintFOV = 80f;
        [SerializeField] private float fovChangeSpeed = 5f;

        [Header("Breathing")]
        [SerializeField] private bool enableBreathing = true;
        [SerializeField] private float breathSpeed = 0.5f;
        [SerializeField] private float breathAmount = 0.005f;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private FirstPersonController controller;

        private Vector3 initialCameraPosition;
        private float bobTimer;
        private float currentTilt;
        private float breathTimer;
        private float lastGroundedY;
        private bool wasGrounded = true;
        private Coroutine landingCoroutine;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }

            if (playerCamera == null && cameraTransform != null)
            {
                playerCamera = cameraTransform.GetComponent<Camera>();
            }

            if (controller == null)
            {
                controller = GetComponent<FirstPersonController>();
            }
        }

        private void Start()
        {
            if (cameraTransform != null)
            {
                initialCameraPosition = cameraTransform.localPosition;
            }

            lastGroundedY = transform.position.y;

            if (playerCamera != null)
            {
                baseFOV = playerCamera.fieldOfView;
            }
        }

        private void Update()
        {
            if (controller == null || cameraTransform == null) return;

            bool isGrounded = controller.IsGrounded;
            bool isMoving = IsPlayerMoving();
            bool isSprinting = Input.GetKey(KeyCode.LeftShift) && isMoving;

            // Handle head bob
            if (enableHeadBob && isGrounded && isMoving)
            {
                ApplyHeadBob(isSprinting);
            }
            else if (enableHeadBob)
            {
                // Smoothly return to initial position
                ResetHeadBob();
            }

            // Handle camera tilt
            if (enableCameraTilt)
            {
                ApplyCameraTilt();
            }

            // Handle landing impact
            if (enableLandingImpact)
            {
                HandleLanding(isGrounded);
            }

            // Handle FOV
            if (enableFOVEffects)
            {
                ApplyFOVEffects(isSprinting);
            }

            // Handle breathing
            if (enableBreathing && !isMoving && isGrounded)
            {
                ApplyBreathing();
            }

            wasGrounded = isGrounded;
        }

        private bool IsPlayerMoving()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            return Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        }

        private void ApplyHeadBob(bool sprinting)
        {
            float multiplier = sprinting ? sprintBobMultiplier : 1f;
            bobTimer += Time.deltaTime * bobFrequency * multiplier;

            float bobX = Mathf.Cos(bobTimer) * bobAmplitudeX * multiplier;
            float bobY = Mathf.Sin(bobTimer * 2f) * bobAmplitudeY * multiplier;

            Vector3 targetPos = initialCameraPosition + new Vector3(bobX, bobY, 0f);
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                targetPos,
                Time.deltaTime * 10f
            );
        }

        private void ResetHeadBob()
        {
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                initialCameraPosition,
                Time.deltaTime * 5f
            );
        }

        private void ApplyCameraTilt()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float targetTilt = -horizontal * tiltAmount;

            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothing);

            // Apply tilt to local euler angles
            Vector3 currentEuler = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(currentEuler.x, currentEuler.y, currentTilt);
        }

        private void HandleLanding(bool isGrounded)
        {
            if (!wasGrounded && isGrounded)
            {
                // Just landed
                float fallDistance = lastGroundedY - transform.position.y;

                if (fallDistance > minFallDistance)
                {
                    float impactStrength = Mathf.Clamp01(fallDistance / 5f);

                    if (landingCoroutine != null)
                    {
                        StopCoroutine(landingCoroutine);
                    }
                    landingCoroutine = StartCoroutine(LandingImpactCoroutine(impactStrength));
                }
            }

            if (isGrounded)
            {
                lastGroundedY = transform.position.y;
            }
        }

        private IEnumerator LandingImpactCoroutine(float strength)
        {
            float bobAmount = landingBobAmount * strength;
            float elapsed = 0f;

            while (elapsed < landingBobDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / landingBobDuration;

                // Quick down then up motion
                float curve = Mathf.Sin(t * Mathf.PI);
                float bobOffset = -bobAmount * curve * (1f - t);

                Vector3 targetPos = initialCameraPosition + new Vector3(0f, bobOffset, 0f);
                cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, 0.3f);

                yield return null;
            }

            landingCoroutine = null;
        }

        private void ApplyFOVEffects(bool sprinting)
        {
            if (playerCamera == null) return;

            float targetFOV = sprinting ? sprintFOV : baseFOV;
            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFOV,
                Time.deltaTime * fovChangeSpeed
            );
        }

        private void ApplyBreathing()
        {
            breathTimer += Time.deltaTime * breathSpeed;

            float breathY = Mathf.Sin(breathTimer) * breathAmount;
            float breathX = Mathf.Sin(breathTimer * 0.7f) * breathAmount * 0.3f;

            Vector3 targetPos = initialCameraPosition + new Vector3(breathX, breathY, 0f);
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                targetPos,
                Time.deltaTime * 2f
            );
        }

        public void TriggerCameraShake(float intensity, float duration)
        {
            StartCoroutine(CameraShakeCoroutine(intensity, duration));
        }

        private IEnumerator CameraShakeCoroutine(float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float strength = intensity * (1f - (elapsed / duration));

                float x = Random.Range(-strength, strength);
                float y = Random.Range(-strength, strength);

                cameraTransform.localPosition = initialCameraPosition + new Vector3(x, y, 0f);
                yield return null;
            }

            cameraTransform.localPosition = initialCameraPosition;
        }

        public void SetHeadBobEnabled(bool enabled)
        {
            enableHeadBob = enabled;
        }

        public void SetCameraTiltEnabled(bool enabled)
        {
            enableCameraTilt = enabled;
        }

        public void SetLandingImpactEnabled(bool enabled)
        {
            enableLandingImpact = enabled;
        }
    }
}
