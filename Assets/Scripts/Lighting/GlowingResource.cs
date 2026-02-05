using UnityEngine;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Makes a resource (ore, gem, etc.) glow with light.
    /// Valuable resources at depth emit light to attract players
    /// and create an exciting visual effect.
    /// </summary>
    public class GlowingResource : MonoBehaviour
    {
        [Header("Glow Type")]
        [SerializeField] private ResourceGlowType glowType = ResourceGlowType.Gem;

        [Header("Light Settings")]
        [SerializeField] private float glowRange = 3f;
        [SerializeField] private float glowIntensity = 1f;
        [SerializeField] private Color glowColor = new Color(0.5f, 0.8f, 1f); // Blue gem glow

        [Header("Pulse Animation")]
        [SerializeField] private bool enablePulse = true;
        [SerializeField] private float pulseSpeed = 1.5f;
        [SerializeField] private float pulseMin = 0.5f;
        [SerializeField] private float pulseMax = 1.2f;

        [Header("Emission")]
        [SerializeField] private bool applyEmissionToMaterial = true;
        [SerializeField] private float emissionIntensity = 2f;

        [Header("Depth Activation")]
        [Tooltip("Resource only glows when below this depth")]
        [SerializeField] private float minDepthToGlow = 0f;
        [SerializeField] private bool checkDepth = false;

        [Header("Performance")]
        [Tooltip("Light is disabled when player is further than this distance")]
        [SerializeField] private float lightCullDistance = 15f;

        // Runtime
        private Light glowLight;
        private MeshRenderer meshRenderer;
        private Material glowMaterial;
        private float baseIntensity;
        private Color baseEmissionColor;
        private bool isGlowing = true;
        private bool isLightCulled = false;
        private Transform playerTransform;
        private float cullCheckTimer = 0f;
        private const float CULL_CHECK_INTERVAL = 0.5f; // Check distance every 0.5s

        public enum ResourceGlowType
        {
            Gem,        // Blue/cyan glow
            Gold,       // Golden glow
            Crystal,    // Purple/pink glow
            Ancient,    // Green mysterious glow
            Rare,       // Rainbow/shifting color
            Custom      // Use serialized color
        }

        private void Awake()
        {
            SetupGlowLight();
            SetupMaterialEmission();
            ApplyGlowTypePreset();
        }

        private void Start()
        {
            baseIntensity = glowIntensity;
            if (glowMaterial != null)
            {
                baseEmissionColor = glowColor * emissionIntensity;
            }
        }

        private void Update()
        {
            // Distance-based light culling (checked every 0.5s, not every frame)
            cullCheckTimer += Time.deltaTime;
            if (cullCheckTimer >= CULL_CHECK_INTERVAL)
            {
                cullCheckTimer = 0f;
                UpdateDistanceCulling();
            }

            if (checkDepth)
            {
                UpdateDepthCheck();
            }

            if (isGlowing && enablePulse && !isLightCulled)
            {
                UpdatePulse();
            }
        }

        private void UpdateDistanceCulling()
        {
            if (playerTransform == null)
            {
                var cam = Camera.main;
                if (cam != null) playerTransform = cam.transform;
                else return;
            }

            float sqrDist = (playerTransform.position - transform.position).sqrMagnitude;
            bool shouldCull = sqrDist > lightCullDistance * lightCullDistance;

            if (shouldCull != isLightCulled)
            {
                isLightCulled = shouldCull;
                if (glowLight != null)
                {
                    glowLight.enabled = !isLightCulled && isGlowing;
                }
            }
        }

        private void SetupGlowLight()
        {
            glowLight = GetComponentInChildren<Light>();

            if (glowLight == null)
            {
                var lightObj = new GameObject("GlowLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.zero;

                glowLight = lightObj.AddComponent<Light>();
            }

            glowLight.type = LightType.Point;
            glowLight.range = glowRange;
            glowLight.intensity = glowIntensity;
            glowLight.color = glowColor;
            glowLight.shadows = LightShadows.None; // Performance
            glowLight.renderMode = LightRenderMode.Auto;
        }

        private void SetupMaterialEmission()
        {
            if (!applyEmissionToMaterial) return;

            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (meshRenderer != null)
            {
                // Create instance of material to avoid changing shared materials
                glowMaterial = new Material(meshRenderer.sharedMaterial);
                meshRenderer.material = glowMaterial;

                // Enable emission
                glowMaterial.EnableKeyword("_EMISSION");
                glowMaterial.SetColor("_EmissionColor", glowColor * emissionIntensity);
            }
        }

        private void ApplyGlowTypePreset()
        {
            switch (glowType)
            {
                case ResourceGlowType.Gem:
                    glowColor = new Color(0.3f, 0.7f, 1f); // Cyan/blue
                    break;
                case ResourceGlowType.Gold:
                    glowColor = new Color(1f, 0.85f, 0.3f); // Golden
                    break;
                case ResourceGlowType.Crystal:
                    glowColor = new Color(0.8f, 0.3f, 1f); // Purple/pink
                    break;
                case ResourceGlowType.Ancient:
                    glowColor = new Color(0.3f, 1f, 0.5f); // Mysterious green
                    break;
                case ResourceGlowType.Rare:
                    // Rainbow effect handled in Update
                    glowColor = Color.white;
                    break;
                case ResourceGlowType.Custom:
                    // Use the serialized color as-is
                    break;
            }

            if (glowLight != null)
            {
                glowLight.color = glowColor;
            }

            if (glowMaterial != null)
            {
                glowMaterial.SetColor("_EmissionColor", glowColor * emissionIntensity);
            }
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

            if (glowLight != null)
            {
                glowLight.intensity = baseIntensity * pulse;
            }

            if (glowMaterial != null)
            {
                Color emissionColor = baseEmissionColor * pulse;

                // Rainbow effect for rare resources
                if (glowType == ResourceGlowType.Rare)
                {
                    emissionColor = Color.HSVToRGB((Time.time * 0.1f) % 1f, 0.7f, 1f) * emissionIntensity * pulse;
                    if (glowLight != null)
                    {
                        glowLight.color = Color.HSVToRGB((Time.time * 0.1f) % 1f, 0.5f, 1f);
                    }
                }

                glowMaterial.SetColor("_EmissionColor", emissionColor);
            }
        }

        private void UpdateDepthCheck()
        {
            float currentDepth = 0f;

            // Try to get depth from DepthManager
            var depthManager = Digging.DepthManager.Instance;
            if (depthManager != null)
            {
                currentDepth = depthManager.DepthBelowSoil;
            }
            else
            {
                // Fallback: calculate based on position
                var terrainManager = Digging.UndergroundTerrainManager.Instance;
                float basementY = terrainManager != null ? terrainManager.basementFloorY : -3f;
                currentDepth = Mathf.Max(0, basementY - transform.position.y);
            }

            bool shouldGlow = currentDepth >= minDepthToGlow;

            if (shouldGlow != isGlowing)
            {
                isGlowing = shouldGlow;
                if (glowLight != null)
                {
                    glowLight.enabled = isGlowing;
                }

                if (glowMaterial != null && !isGlowing)
                {
                    glowMaterial.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        /// <summary>
        /// Enable or disable the glow effect.
        /// </summary>
        public void SetGlowing(bool enabled)
        {
            isGlowing = enabled;
            if (glowLight != null)
            {
                glowLight.enabled = enabled && !isLightCulled;
            }

            if (glowMaterial != null)
            {
                glowMaterial.SetColor("_EmissionColor", enabled ? baseEmissionColor : Color.black);
            }
        }

        /// <summary>
        /// Set the glow color.
        /// </summary>
        public void SetGlowColor(Color color)
        {
            glowColor = color;
            baseEmissionColor = color * emissionIntensity;

            if (glowLight != null)
            {
                glowLight.color = color;
            }

            if (glowMaterial != null)
            {
                glowMaterial.SetColor("_EmissionColor", baseEmissionColor);
            }
        }

        /// <summary>
        /// Set the glow intensity.
        /// </summary>
        public void SetIntensity(float intensity)
        {
            glowIntensity = intensity;
            baseIntensity = intensity;

            if (glowLight != null)
            {
                glowLight.intensity = intensity;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = glowColor;
            Gizmos.DrawWireSphere(transform.position, glowRange);
        }
    }
}
