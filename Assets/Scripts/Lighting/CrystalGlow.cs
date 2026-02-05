using UnityEngine;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Makes a crystal pulse using its material's own emission color.
    /// Reads the emission directly from the material and pulses its brightness.
    /// Also creates a matching point light.
    /// </summary>
    public class CrystalGlow : MonoBehaviour
    {
        [Header("Pulse Settings")]
        [SerializeField] private float pulseSpeed = 0.5f;
        [SerializeField] private float minBrightness = 0.3f;
        [SerializeField] private float maxBrightness = 1.2f;

        [Header("Flicker")]
        [SerializeField] private bool enableFlicker = true;
        [SerializeField] private float flickerAmount = 0.1f;

        [Header("Light Settings")]
        [SerializeField] private bool createLight = true;
        [SerializeField] private float lightIntensity = 1.5f;
        [SerializeField] private float lightRange = 3f;

        // Runtime
        private MeshRenderer meshRenderer;
        private Material crystalMaterial;
        private Light crystalLight;
        private Color originalBaseColor;
        private Color originalEmissionColor;
        private float flickerOffset = 0f;
        private float flickerTarget = 0f;
        private float nextFlickerTime = 0f;

        private void Awake()
        {
            SetupMaterial();
            SetupLight();
        }

        private void SetupMaterial()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                // Create instance so we don't modify the shared material
                crystalMaterial = new Material(meshRenderer.sharedMaterial);
                meshRenderer.material = crystalMaterial;

                // Read the material's base color
                if (crystalMaterial.HasProperty("_BaseColor"))
                {
                    originalBaseColor = crystalMaterial.GetColor("_BaseColor");
                }
                else if (crystalMaterial.HasProperty("_Color"))
                {
                    originalBaseColor = crystalMaterial.GetColor("_Color");
                }

                // Also read emission if it exists
                if (crystalMaterial.HasProperty("_EmissionColor"))
                {
                    originalEmissionColor = crystalMaterial.GetColor("_EmissionColor");
                    crystalMaterial.EnableKeyword("_EMISSION");
                }
            }
        }

        private void SetupLight()
        {
            if (!createLight) return;

            crystalLight = GetComponentInChildren<Light>();

            if (crystalLight == null)
            {
                var lightObj = new GameObject("CrystalLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.zero;
                crystalLight = lightObj.AddComponent<Light>();
            }

            crystalLight.type = LightType.Point;
            crystalLight.range = lightRange;
            crystalLight.shadows = LightShadows.None;

            // Use the base color for the light (normalized to get the hue)
            Color sourceColor = originalBaseColor.maxColorComponent > 0 ? originalBaseColor : originalEmissionColor;
            if (sourceColor.maxColorComponent > 0)
            {
                Color lightColor = sourceColor / sourceColor.maxColorComponent;
                crystalLight.color = lightColor;
            }

            crystalLight.intensity = lightIntensity;
        }

        private void Update()
        {
            // Calculate pulse (0 to 1)
            float pulse = Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;

            // Add flicker
            if (enableFlicker)
            {
                UpdateFlicker();
            }

            // Map pulse to brightness range
            float brightness = Mathf.Lerp(minBrightness, maxBrightness, pulse) + flickerOffset;

            // Apply to material - pulse base color and emission
            if (crystalMaterial != null)
            {
                // Pulse the base color
                if (crystalMaterial.HasProperty("_BaseColor"))
                {
                    crystalMaterial.SetColor("_BaseColor", originalBaseColor * brightness);
                }
                else if (crystalMaterial.HasProperty("_Color"))
                {
                    crystalMaterial.SetColor("_Color", originalBaseColor * brightness);
                }

                // Also pulse emission if it exists
                if (originalEmissionColor.maxColorComponent > 0)
                {
                    crystalMaterial.SetColor("_EmissionColor", originalEmissionColor * brightness);
                }
            }

            // Apply to light
            if (crystalLight != null)
            {
                crystalLight.intensity = lightIntensity * brightness;
            }
        }

        private void UpdateFlicker()
        {
            // Set new target occasionally
            if (Time.time >= nextFlickerTime)
            {
                flickerTarget = Random.Range(-flickerAmount, flickerAmount);
                nextFlickerTime = Time.time + Random.Range(0.1f, 0.3f);
            }

            // Smoothly interpolate to target
            flickerOffset = Mathf.Lerp(flickerOffset, flickerTarget, Time.deltaTime * 8f);
        }

        /// <summary>
        /// Set subtle pulsing mode (for when crystal is inserted into engine).
        /// </summary>
        public void SetSubtleMode(float pulseRange, float newLightIntensity)
        {
            // Reduce the pulse range for a subtle effect
            float midPoint = (minBrightness + maxBrightness) / 2f;
            minBrightness = midPoint - pulseRange / 2f;
            maxBrightness = midPoint + pulseRange / 2f;

            // Reduce flicker
            flickerAmount = 0.03f;

            // Reduce light intensity
            lightIntensity = newLightIntensity;

            // Slower pulse
            pulseSpeed = 0.3f;
        }
    }
}
