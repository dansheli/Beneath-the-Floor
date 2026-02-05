using UnityEngine;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// Ceiling-mounted spotlight that illuminates downward.
    /// Place above excavation area to light the top section.
    /// </summary>
    public class CeilingSpotlight : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private float range = 15f;
        [SerializeField] private float spotAngle = 60f;
        [SerializeField] private float intensity = 2.5f;
        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.85f);
        [SerializeField] private bool enableShadows = false; // Disabled for performance

        [Header("Visual")]
        [SerializeField] private float projectorSize = 0.3f;
        [SerializeField] private Color emissionColor = new Color(1f, 0.95f, 0.85f);

        private Light spotLight;
        private MeshRenderer projectorVisual;

        private void Awake()
        {
            SetupLight();
            CreateVisual();
        }

        private void SetupLight()
        {
            spotLight = GetComponentInChildren<Light>();

            if (spotLight == null)
            {
                var lightObj = new GameObject("SpotLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.down * 0.1f;
                spotLight = lightObj.AddComponent<Light>();
            }

            spotLight.type = LightType.Spot;
            spotLight.range = range;
            spotLight.spotAngle = spotAngle;
            spotLight.innerSpotAngle = spotAngle * 0.5f;
            spotLight.intensity = intensity;
            spotLight.color = lightColor;
            spotLight.shadows = enableShadows ? LightShadows.Soft : LightShadows.None;
            spotLight.shadowStrength = 0.6f;

            // Point downward
            spotLight.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void CreateVisual()
        {
            projectorVisual = GetComponentInChildren<MeshRenderer>();
            if (projectorVisual != null) return;

            // Create a cylinder to represent the projector housing
            var visualObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualObj.name = "ProjectorVisual";
            visualObj.transform.SetParent(transform, false);
            visualObj.transform.localScale = new Vector3(projectorSize, projectorSize * 0.3f, projectorSize);

            // Remove collider
            var collider = visualObj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            projectorVisual = visualObj.GetComponent<MeshRenderer>();

            // Create material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat != null)
            {
                mat.SetColor("_BaseColor", Color.gray);
                mat.SetColor("_EmissionColor", emissionColor * 0.5f);
                mat.EnableKeyword("_EMISSION");
                projectorVisual.material = mat;
            }
        }

        // Only show gizmos when selected in editor (not always)
        private void OnDrawGizmosSelected()
        {
            // When selected, show full range cone
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

            float endRadius = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) * range;
            Vector3 endCenter = transform.position + Vector3.down * range;

            // Draw cone lines
            Gizmos.DrawLine(transform.position, endCenter + Vector3.forward * endRadius);
            Gizmos.DrawLine(transform.position, endCenter - Vector3.forward * endRadius);
            Gizmos.DrawLine(transform.position, endCenter + Vector3.right * endRadius);
            Gizmos.DrawLine(transform.position, endCenter - Vector3.right * endRadius);

            DrawWireCircle(endCenter, endRadius);
        }

        private void DrawWireCircle(Vector3 center, float radius)
        {
            int segments = 24;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 p1 = center + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * radius;
                Vector3 p2 = center + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * radius;

                Gizmos.DrawLine(p1, p2);
            }
        }

        private void OnValidate()
        {
            if (spotLight != null)
            {
                spotLight.range = range;
                spotLight.spotAngle = spotAngle;
                spotLight.innerSpotAngle = spotAngle * 0.5f;
                spotLight.intensity = intensity;
                spotLight.color = lightColor;
            }
        }
    }
}
