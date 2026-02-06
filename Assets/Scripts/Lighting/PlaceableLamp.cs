using UnityEngine;
using BeneathTheFloor.Interaction;

namespace BeneathTheFloor.Lighting
{
    /// <summary>
    /// A lamp that can be placed by the player in the underground area.
    /// Players purchase these lamps to illuminate their dig site.
    /// Implements IInteractable to allow picking up placed lamps.
    /// </summary>
    public class PlaceableLamp : MonoBehaviour, IInteractable
    {
        [Header("Light Settings")]
        [SerializeField] private LightType lampType = LightType.Point;
        [SerializeField] private float range = 10f;
        [SerializeField] private float intensity = 2.5f;
        [SerializeField] private Color lightColor = new Color(1f, 0.9f, 0.7f); // Warm lamp color
        [SerializeField] private bool enableShadows = true;

        [Header("Visual")]
        [SerializeField] private bool createDefaultVisual = true;
        [SerializeField] private float lampScale = 0.3f;
        [SerializeField] private Color lampEmissionColor = new Color(1f, 0.8f, 0.5f);

        [Header("Flicker Effect")]
        [SerializeField] private bool enableFlicker = false;
        [SerializeField] private float flickerSpeed = 3f;
        [SerializeField] private float flickerAmount = 0.1f;

        [Header("Interaction")]
        [SerializeField] private string interactionText = "Pick up Lamp [E]";
        [SerializeField] private bool canBePickedUp = true;

        [Header("Support Detection")]
        [Tooltip("Distance to raycast for support check.")]
        [SerializeField] private float supportCheckDistance = 0.5f;
        [Tooltip("Layer mask for support detection.")]
        [SerializeField] private LayerMask supportLayerMask = ~0;

        // Runtime
        private Light lampLight;
        private float baseIntensity;

        // Support detection
        private bool _isFalling = false;
        private Rigidbody _rigidbody;
        private Vector3 _placementNormal = Vector3.up; // The normal of the surface we were placed on

        /// <summary>
        /// The Light component of this lamp.
        /// </summary>
        public Light LampLight => lampLight;

        /// <summary>
        /// Whether the lamp is currently on.
        /// </summary>
        public bool IsOn => lampLight != null && lampLight.enabled;

        private void Awake()
        {
            SetupLight();
            if (createDefaultVisual)
            {
                CreateDefaultVisual();
            }
        }

        private void Start()
        {
            // Ensure light is properly enabled after all initialization
            if (lampLight != null)
            {
                lampLight.enabled = true;
                lampLight.intensity = intensity;
                lampLight.range = range;
            }
        }

        private void Update()
        {
            // Safety check - ensure light stays on
            if (lampLight != null && !lampLight.enabled)
            {
                lampLight.enabled = true;
            }

            if (enableFlicker && lampLight != null && lampLight.enabled)
            {
                UpdateFlicker();
            }
        }

        private void SetupLight()
        {
            // Find ALL lights in prefab and enable them
            Light[] allLights = GetComponentsInChildren<Light>(true); // include inactive
            foreach (var light in allLights)
            {
                light.enabled = true;
                light.intensity = Mathf.Max(light.intensity, intensity);
            }

            lampLight = GetComponentInChildren<Light>();

            if (lampLight == null)
            {
                var lightObj = new GameObject("LampLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.up * 0.2f;

                lampLight = lightObj.AddComponent<Light>();
            }

            lampLight.type = lampType;
            lampLight.range = range;
            lampLight.intensity = intensity;
            lampLight.color = lightColor;
            lampLight.shadows = enableShadows ? LightShadows.Soft : LightShadows.None;
            lampLight.shadowStrength = 0.6f;
            lampLight.enabled = true; // Ensure light is always on

            // Set culling mask to affect digging area
            lampLight.cullingMask = ~0; // All layers

            baseIntensity = intensity;
        }

        private void CreateDefaultVisual()
        {
            // Check if visual already exists
            if (GetComponentInChildren<MeshRenderer>() != null) return;

            // Create a simple lamp visual (sphere)
            var visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObj.name = "LampVisual";
            visualObj.transform.SetParent(transform, false);
            visualObj.transform.localScale = Vector3.one * lampScale;

            // Remove collider from visual
            var collider = visualObj.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Add collider to main object for interaction
            if (GetComponent<Collider>() == null)
            {
                var sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = lampScale * 0.6f;
            }

            var renderer = visualObj.GetComponent<MeshRenderer>();

            // Create simple material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat != null)
            {
                mat.SetColor("_BaseColor", lampEmissionColor);
                renderer.material = mat;
            }
        }

        private void UpdateFlicker()
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
            float flickerIntensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount * baseIntensity;
            lampLight.intensity = Mathf.Max(0.1f, flickerIntensity);
        }

        /// <summary>
        /// Whether this lamp is currently falling (lost support).
        /// </summary>
        public bool IsFalling => _isFalling;

        /// <summary>
        /// Check if the lamp still has support beneath it.
        /// If support is gone (terrain dug away), enable physics and let it fall.
        /// Called by UndergroundLightingSystem when digging occurs nearby.
        /// </summary>
        public void CheckSupport()
        {
            // Raycast in the opposite direction of our placement normal (into the surface)
            Vector3 checkDirection = -_placementNormal;

            // Start slightly inside the lamp position
            Vector3 rayStart = transform.position + _placementNormal * 0.1f;

            // Check for support
            bool hasSupport = Physics.Raycast(rayStart, checkDirection, supportCheckDistance, supportLayerMask);

            if (!hasSupport)
            {
                // No support - start falling
                EnableFallingPhysics();
            }
        }

        /// <summary>
        /// Enable physics on this lamp to let it fall.
        /// The lamp continues to function (light stays on, can still be picked up).
        /// </summary>
        private void EnableFallingPhysics()
        {
            if (_isFalling) return;

            _isFalling = true;

            // Add or get Rigidbody
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            // Configure rigidbody for natural falling
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.mass = 0.5f;
            _rigidbody.drag = 0.5f;
            _rigidbody.angularDrag = 0.5f;

            // Add a small random impulse for variety
            Vector3 randomImpulse = new Vector3(
                Random.Range(-0.5f, 0.5f),
                0f,
                Random.Range(-0.5f, 0.5f)
            );
            _rigidbody.AddForce(randomImpulse, ForceMode.Impulse);

            // Ensure collider exists for physics - compute from mesh bounds
            if (GetComponent<Collider>() == null)
            {
                var sphereCol = gameObject.AddComponent<SphereCollider>();
                var renderers = GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null)
                            bounds.Encapsulate(renderers[i].bounds);
                    }
                    sphereCol.center = transform.InverseTransformPoint(bounds.center);
                    sphereCol.radius = bounds.extents.magnitude / transform.lossyScale.x;
                }
                else
                {
                    sphereCol.radius = lampScale * 0.6f;
                }
            }
        }

        /// <summary>
        /// Set the normal of the surface this lamp was placed on.
        /// Called by the placement system when placing the lamp.
        /// </summary>
        public void SetPlacementNormal(Vector3 normal)
        {
            _placementNormal = normal.normalized;
        }

        #region IInteractable Implementation

        public bool CanInteract => canBePickedUp;

        public string GetInteractionText()
        {
            return interactionText;
        }

        public void Interact(GameObject interactor)
        {
            if (!canBePickedUp) return;

            // Add lamp back to player's inventory
            if (LampPlacementController.Instance != null)
            {
                LampPlacementController.Instance.AddLamps(1);
            }

            // Remove from UndergroundLightingSystem tracking
            if (UndergroundLightingSystem.Instance != null)
            {
                UndergroundLightingSystem.Instance.RemoveLamp(this);
            }

            // Destroy this lamp
            Destroy(gameObject);
        }

        public void OnHoverEnter()
        {
            // Interaction text is shown by IInteractable - no visual effect needed
        }

        public void OnHoverExit()
        {
            // No visual effect to restore
        }

        #endregion

        #region Public API

        /// <summary>
        /// Turn the lamp on.
        /// </summary>
        public void TurnOn()
        {
            if (lampLight != null)
            {
                lampLight.enabled = true;
            }
        }

        /// <summary>
        /// Turn the lamp off.
        /// </summary>
        public void TurnOff()
        {
            if (lampLight != null)
            {
                lampLight.enabled = false;
            }
        }

        /// <summary>
        /// Toggle the lamp state.
        /// </summary>
        public void Toggle()
        {
            if (lampLight != null)
            {
                lampLight.enabled = !lampLight.enabled;
            }
        }

        /// <summary>
        /// Set lamp intensity.
        /// </summary>
        public void SetIntensity(float newIntensity)
        {
            intensity = newIntensity;
            baseIntensity = newIntensity;
            if (lampLight != null)
            {
                lampLight.intensity = newIntensity;
            }
        }

        /// <summary>
        /// Set lamp range.
        /// </summary>
        public void SetRange(float newRange)
        {
            range = newRange;
            if (lampLight != null)
            {
                lampLight.range = newRange;
            }
        }

        /// <summary>
        /// Set lamp color.
        /// </summary>
        public void SetColor(Color newColor)
        {
            lightColor = newColor;
            if (lampLight != null)
            {
                lampLight.color = newColor;
            }
        }

        /// <summary>
        /// Set whether this lamp can be picked up.
        /// </summary>
        public void SetCanPickUp(bool canPickUp)
        {
            canBePickedUp = canPickUp;
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
