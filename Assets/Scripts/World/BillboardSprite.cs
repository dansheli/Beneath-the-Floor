using UnityEngine;

namespace BeneathTheFloor.World
{
    /// <summary>
    /// Makes a sprite always face the camera (billboard effect).
    /// Useful for dropped item icons that need to be visible from any angle.
    /// </summary>
    public class BillboardSprite : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, only rotates around Y axis (stays upright)")]
        [SerializeField] private bool lockYAxis = false;

        [Tooltip("If true, flips the sprite to face away from camera (for sprites that are backwards)")]
        [SerializeField] private bool flipFacing = false;

        private Camera mainCamera;
        private Transform cachedTransform;

        private void Start()
        {
            mainCamera = Camera.main;
            cachedTransform = transform;
        }

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            if (lockYAxis)
            {
                // Only rotate around Y axis - sprite stays upright
                Vector3 lookDir = mainCamera.transform.position - cachedTransform.position;
                lookDir.y = 0; // Ignore vertical difference

                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion rotation = Quaternion.LookRotation(flipFacing ? -lookDir : lookDir);
                    cachedTransform.rotation = rotation;
                }
            }
            else
            {
                // Full billboard - always face camera directly
                if (flipFacing)
                {
                    cachedTransform.LookAt(cachedTransform.position - mainCamera.transform.forward, mainCamera.transform.up);
                }
                else
                {
                    cachedTransform.LookAt(mainCamera.transform.position, mainCamera.transform.up);
                }
            }
        }

        /// <summary>
        /// Set whether to lock rotation to Y axis only.
        /// </summary>
        public void SetLockYAxis(bool locked)
        {
            lockYAxis = locked;
        }

        /// <summary>
        /// Set whether to flip the facing direction.
        /// </summary>
        public void SetFlipFacing(bool flip)
        {
            flipFacing = flip;
        }
    }
}
