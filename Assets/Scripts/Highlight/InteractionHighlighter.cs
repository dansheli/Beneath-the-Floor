using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;

namespace BeneathTheFloor.Highlight
{
    public class InteractionHighlighter : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float detectionRange = 5f; // Increased range for better detection
        [SerializeField] private LayerMask interactableLayers = -1; // Default to all layers
        [SerializeField] private Transform cameraTransform;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        [Header("Highlight Settings")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField] private float highlightIntensity = 1.5f;
        [SerializeField] private float outlineWidth = 0.02f;

        [Header("Pulse Effect")]
        [SerializeField] private bool enablePulse = true;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMinIntensity = 0.8f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private float hoverSoundVolume = 0.3f;

        public static InteractionHighlighter Instance { get; private set; }

        private GameObject currentHighlighted;
        private IInteractable currentInteractable;
        private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private Dictionary<Renderer, Material[]> highlightMaterials = new Dictionary<Renderer, Material[]>();
        private Material highlightMaterialTemplate;
        private Coroutine pulseCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            CreateHighlightMaterial();
        }

        private void CreateHighlightMaterial()
        {
            // Create a simple unlit highlight material
            highlightMaterialTemplate = new Material(Shader.Find("Standard"));
            highlightMaterialTemplate.EnableKeyword("_EMISSION");
            highlightMaterialTemplate.SetColor("_EmissionColor", highlightColor * highlightIntensity);
        }

        private void Update()
        {
            DetectInteractable();

            if (enablePulse && currentHighlighted != null)
            {
                UpdatePulse();
            }
        }

        private void DetectInteractable()
        {
            if (cameraTransform == null)
            {
                if (debugMode) Debug.LogWarning("[InteractionHighlighter] cameraTransform is null!");
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, detectionRange, interactableLayers))
            {
                GameObject hitObject = hit.collider.gameObject;
                IInteractable interactable = hitObject.GetComponent<IInteractable>();

                if (debugMode && interactable == null)
                {
                    // Only log occasionally to avoid spam
                    if (Time.frameCount % 60 == 0)
                    {
                        Debug.Log($"[InteractionHighlighter] Hit: {hitObject.name} (layer: {LayerMask.LayerToName(hitObject.layer)}) - No IInteractable on object");
                    }
                }

                // Check parent if not found on direct hit
                if (interactable == null)
                {
                    interactable = hitObject.GetComponentInParent<IInteractable>();
                    if (interactable != null)
                    {
                        hitObject = ((MonoBehaviour)interactable).gameObject;
                        if (debugMode)
                        {
                            Debug.Log($"[InteractionHighlighter] Found IInteractable on parent: {hitObject.name}");
                        }
                    }
                }

                if (interactable != null && interactable.CanInteract)
                {
                    if (hitObject != currentHighlighted)
                    {
                        // New object
                        if (debugMode)
                        {
                            Debug.Log($"[InteractionHighlighter] Highlighting: {hitObject.name}");
                        }
                        ClearHighlight();
                        SetHighlight(hitObject, interactable);
                    }
                }
                else if (interactable != null && !interactable.CanInteract)
                {
                    if (debugMode && Time.frameCount % 60 == 0)
                    {
                        Debug.Log($"[InteractionHighlighter] {hitObject.name} has IInteractable but CanInteract=false");
                    }
                    ClearHighlight();
                }
                else
                {
                    ClearHighlight();
                }
            }
            else
            {
                ClearHighlight();
            }
        }

        private void SetHighlight(GameObject obj, IInteractable interactable)
        {
            currentHighlighted = obj;
            currentInteractable = interactable;

            // Notify interactable
            interactable.OnHoverEnter();

            // Play hover sound
            if (hoverSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound, hoverSoundVolume);
            }

            // Store original materials and apply highlight
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                // Store originals
                Material[] originals = renderer.materials;
                originalMaterials[renderer] = originals;

                // Create highlight versions
                Material[] highlights = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    if (originals[i] != null)
                    {
                        highlights[i] = new Material(originals[i]);
                        highlights[i].EnableKeyword("_EMISSION");
                        highlights[i].SetColor("_EmissionColor", highlightColor * highlightIntensity);
                    }
                }

                highlightMaterials[renderer] = highlights;
                renderer.materials = highlights;
            }

            // Add outline effect
            AddOutline(obj);

            // Start pulse
            if (enablePulse && pulseCoroutine == null)
            {
                pulseCoroutine = StartCoroutine(PulseCoroutine());
            }
        }

        private void ClearHighlight()
        {
            if (currentHighlighted == null) return;

            // Notify interactable
            currentInteractable?.OnHoverExit();

            // Restore original materials
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }

            // Clean up highlight materials
            foreach (var kvp in highlightMaterials)
            {
                foreach (var mat in kvp.Value)
                {
                    if (mat != null)
                    {
                        Destroy(mat);
                    }
                }
            }

            // Remove outline
            RemoveOutline(currentHighlighted);

            originalMaterials.Clear();
            highlightMaterials.Clear();
            currentHighlighted = null;
            currentInteractable = null;

            // Stop pulse
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
        }

        private void AddOutline(GameObject obj)
        {
            // Check if outline component exists
            HighlightOutline outline = obj.GetComponent<HighlightOutline>();
            if (outline == null)
            {
                outline = obj.AddComponent<HighlightOutline>();
            }

            outline.SetColor(highlightColor);
            outline.SetWidth(outlineWidth);
            outline.enabled = true;
        }

        private void RemoveOutline(GameObject obj)
        {
            if (obj == null) return;

            HighlightOutline outline = obj.GetComponent<HighlightOutline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Lerp(pulseMinIntensity, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);

            foreach (var kvp in highlightMaterials)
            {
                foreach (var mat in kvp.Value)
                {
                    if (mat != null)
                    {
                        mat.SetColor("_EmissionColor", highlightColor * highlightIntensity * pulse);
                    }
                }
            }
        }

        private IEnumerator PulseCoroutine()
        {
            while (currentHighlighted != null)
            {
                UpdatePulse();
                yield return null;
            }
        }

        public IInteractable GetCurrentInteractable()
        {
            return currentInteractable;
        }

        public bool HasHighlightedObject()
        {
            return currentHighlighted != null;
        }

        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
        }

        public void SetHighlightIntensity(float intensity)
        {
            highlightIntensity = Mathf.Max(0f, intensity);
        }

        public void SetDetectionRange(float range)
        {
            detectionRange = Mathf.Max(0.1f, range);
        }
    }
}
