using UnityEngine;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Simple permanent wall lamp for atmosphere.
    /// Place manually in scene - cannot be picked up by player.
    /// </summary>
    public class WallLamp : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private float range = 8f;
        [SerializeField] private float intensity = 1.8f;
        [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.6f);
        [SerializeField] private bool enableShadows = false; // Disabled for performance

        [Header("Visual")]
        [SerializeField] private float lampScale = 0.25f;
        [SerializeField] private Color emissionColor = new Color(1f, 0.85f, 0.6f);

        [Header("Flicker Effect")]
        [SerializeField] private bool enableFlicker = true;
        [SerializeField] private float flickerSpeed = 2f;
        [SerializeField] private float flickerAmount = 0.08f;

        private Light lampLight;
        private float baseIntensity;
        private MeshRenderer lampVisual;

        private void Awake()
        {
            SetupLight();
            CreateVisual();
        }

        private void Update()
        {
            if (enableFlicker && lampLight != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, transform.position.x);
                float flickerIntensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount * baseIntensity;
                lampLight.intensity = Mathf.Max(0.1f, flickerIntensity);
            }
        }

        private void SetupLight()
        {
            lampLight = GetComponentInChildren<Light>();

            if (lampLight == null)
            {
                var lightObj = new GameObject("Light");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.up * 0.15f;
                lampLight = lightObj.AddComponent<Light>();
            }

            lampLight.type = LightType.Point;
            lampLight.range = range;
            lampLight.intensity = intensity;
            lampLight.color = lightColor;
            lampLight.shadows = enableShadows ? LightShadows.Soft : LightShadows.None;
            lampLight.shadowStrength = 0.5f;

            baseIntensity = intensity;
        }

        private void CreateVisual()
        {
            lampVisual = GetComponentInChildren<MeshRenderer>();
            if (lampVisual != null) return;

            var visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObj.name = "LampVisual";
            visualObj.transform.SetParent(transform, false);
            visualObj.transform.localScale = Vector3.one * lampScale;

            // Remove collider
            var collider = visualObj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            lampVisual = visualObj.GetComponent<MeshRenderer>();

            // Create emissive material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat != null)
            {
                mat.SetColor("_BaseColor", emissionColor);
                mat.SetColor("_EmissionColor", emissionColor * 2f);
                mat.EnableKeyword("_EMISSION");
                lampVisual.material = mat;
            }
        }

        // Only show gizmos when selected in editor (not always)
        private void OnDrawGizmosSelected()
        {
            // Draw solid sphere to represent lamp bulb
            Gizmos.color = new Color(1f, 0.85f, 0.6f, 0.8f);
            Gizmos.DrawSphere(transform.position, lampScale * 0.5f);

            // Draw wire sphere for light icon
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, lampScale);

            // Also show range when selected
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, range);
        }

        // Editor helper to update settings at runtime
        private void OnValidate()
        {
            if (lampLight != null)
            {
                lampLight.range = range;
                lampLight.intensity = intensity;
                lampLight.color = lightColor;
                baseIntensity = intensity;
            }
        }
    }
}
