using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace BeneathTheFloor.Story
{
    public class StoryFXManager : MonoBehaviour
    {
        [Header("Discovery Effects")]
        [SerializeField] private ParticleSystem discoveryParticles;
        [SerializeField] private Light discoveryLight;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip discoverySound;
        [SerializeField] private AudioClip mysterySound;
        [SerializeField] private AudioClip ancientSound;

        [Header("Screen Effects")]
        [SerializeField] private Image vignetteOverlay;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private float vignetteIntensity = 0.3f;
        [SerializeField] private float flashDuration = 0.5f;

        [Header("Camera Effects")]
        [SerializeField] private float discoveryZoomAmount = 5f;
        [SerializeField] private float discoveryZoomDuration = 0.5f;
        [SerializeField] private float slowMotionScale = 0.3f;
        [SerializeField] private float slowMotionDuration = 1f;

        [Header("Colors")]
        [SerializeField] private Color normalDiscoveryColor = new Color(1f, 0.9f, 0.5f);
        [SerializeField] private Color rareDiscoveryColor = new Color(0.5f, 0.8f, 1f);
        [SerializeField] private Color ancientDiscoveryColor = new Color(0.8f, 0.4f, 1f);

        public static StoryFXManager Instance { get; private set; }

        private Camera mainCamera;
        private float originalFOV;
        private float originalTimeScale;

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

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                originalFOV = mainCamera.fieldOfView;
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            SetupDefaultParticles();
            SetupScreenOverlays();
        }

        private void Start()
        {
            // Subscribe to story events
            GameEvents.OnStoryItemFound += OnStoryItemFound;
        }

        private void OnDestroy()
        {
            GameEvents.OnStoryItemFound -= OnStoryItemFound;

            // Ensure time scale is restored
            Time.timeScale = 1f;
        }

        private void SetupDefaultParticles()
        {
            if (discoveryParticles == null)
            {
                GameObject particleObj = new GameObject("DiscoveryParticles");
                particleObj.transform.SetParent(transform);
                discoveryParticles = particleObj.AddComponent<ParticleSystem>();

                // Stop the particle system before modifying duration
                discoveryParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = discoveryParticles.main;
                main.duration = 2f;
                main.loop = false;
                main.startLifetime = 1.5f;
                main.startSpeed = 2f;
                main.startSize = 0.15f;
                main.gravityModifier = -0.2f;
                main.maxParticles = 50;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = discoveryParticles.emission;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0f, 30)
                });

                var shape = discoveryParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;

                var colorOverLifetime = discoveryParticles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(normalDiscoveryColor, 0f),
                        new GradientColorKey(normalDiscoveryColor, 0.5f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.2f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                colorOverLifetime.color = gradient;

                var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            }

            if (discoveryLight == null)
            {
                GameObject lightObj = new GameObject("DiscoveryLight");
                lightObj.transform.SetParent(transform);
                discoveryLight = lightObj.AddComponent<Light>();
                discoveryLight.type = LightType.Point;
                discoveryLight.range = 5f;
                discoveryLight.intensity = 0f;
                discoveryLight.color = normalDiscoveryColor;
            }
        }

        private void SetupScreenOverlays()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                GameObject canvasObj = new GameObject("StoryFXCanvas");
                canvasObj.transform.SetParent(transform);
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 998;
            }

            if (vignetteOverlay == null)
            {
                vignetteOverlay = CreateFullscreenOverlay("VignetteOverlay", canvas.transform);
                vignetteOverlay.color = Color.clear;
            }

            if (flashOverlay == null)
            {
                flashOverlay = CreateFullscreenOverlay("FlashOverlay", canvas.transform);
                flashOverlay.color = Color.clear;
            }
        }

        private Image CreateFullscreenOverlay(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);

            Image img = obj.AddComponent<Image>();
            img.raycastTarget = false;

            RectTransform rect = img.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return img;
        }

        private void OnStoryItemFound(StoryItem item)
        {
            // Determine rarity based on depth or other factors
            DiscoveryRarity rarity = DetermineRarity(item);
            PlayDiscoveryEffect(item, rarity);
        }

        private DiscoveryRarity DetermineRarity(StoryItem item)
        {
            if (item.depthFound >= 15) return DiscoveryRarity.Ancient;
            if (item.depthFound >= 8) return DiscoveryRarity.Rare;
            return DiscoveryRarity.Normal;
        }

        public void PlayDiscoveryEffect(StoryItem item, DiscoveryRarity rarity)
        {
            StartCoroutine(DiscoverySequence(item, rarity));
        }

        private IEnumerator DiscoverySequence(StoryItem item, DiscoveryRarity rarity)
        {
            Color effectColor = GetRarityColor(rarity);

            // Slow motion
            if (rarity != DiscoveryRarity.Normal)
            {
                originalTimeScale = Time.timeScale;
                Time.timeScale = slowMotionScale;
            }

            // Flash
            yield return StartCoroutine(FlashScreen(effectColor, flashDuration));

            // Particles
            if (discoveryParticles != null)
            {
                var main = discoveryParticles.main;
                main.startColor = effectColor;
                discoveryParticles.Play();
            }

            // Light burst
            if (discoveryLight != null)
            {
                discoveryLight.color = effectColor;
                StartCoroutine(LightBurstCoroutine(rarity == DiscoveryRarity.Ancient ? 3f : 2f));
            }

            // Sound
            PlayDiscoverySound(rarity);

            // Camera zoom
            if (rarity != DiscoveryRarity.Normal)
            {
                StartCoroutine(CameraZoomCoroutine());
            }

            // Vignette
            StartCoroutine(VignetteCoroutine(effectColor, rarity == DiscoveryRarity.Ancient ? 2f : 1f));

            // Wait for slow motion
            if (rarity != DiscoveryRarity.Normal)
            {
                yield return new WaitForSecondsRealtime(slowMotionDuration);
                Time.timeScale = originalTimeScale;
            }
        }

        private IEnumerator FlashScreen(Color color, float duration)
        {
            if (flashOverlay == null) yield break;

            flashOverlay.color = new Color(color.r, color.g, color.b, 0.5f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                flashOverlay.color = Color.Lerp(
                    new Color(color.r, color.g, color.b, 0.5f),
                    Color.clear,
                    t
                );
                yield return null;
            }

            flashOverlay.color = Color.clear;
        }

        private IEnumerator LightBurstCoroutine(float intensity)
        {
            if (discoveryLight == null) yield break;

            discoveryLight.intensity = intensity;

            float duration = 1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                discoveryLight.intensity = Mathf.Lerp(intensity, 0f, t);
                yield return null;
            }

            discoveryLight.intensity = 0f;
        }

        private IEnumerator CameraZoomCoroutine()
        {
            if (mainCamera == null) yield break;

            float targetFOV = originalFOV - discoveryZoomAmount;
            float halfDuration = discoveryZoomDuration / 2f;

            // Zoom in
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / halfDuration;
                mainCamera.fieldOfView = Mathf.Lerp(originalFOV, targetFOV, EaseOutQuad(t));
                yield return null;
            }

            // Hold briefly
            yield return new WaitForSecondsRealtime(0.1f);

            // Zoom out
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / halfDuration;
                mainCamera.fieldOfView = Mathf.Lerp(targetFOV, originalFOV, EaseInQuad(t));
                yield return null;
            }

            mainCamera.fieldOfView = originalFOV;
        }

        private IEnumerator VignetteCoroutine(Color color, float duration)
        {
            if (vignetteOverlay == null) yield break;

            Color vignetteColor = new Color(0f, 0f, 0f, vignetteIntensity);

            // Fade in
            float fadeInDuration = 0.2f;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeInDuration;
                vignetteOverlay.color = Color.Lerp(Color.clear, vignetteColor, t);
                yield return null;
            }

            // Hold
            yield return new WaitForSecondsRealtime(duration - 0.4f);

            // Fade out
            float fadeOutDuration = 0.2f;
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeOutDuration;
                vignetteOverlay.color = Color.Lerp(vignetteColor, Color.clear, t);
                yield return null;
            }

            vignetteOverlay.color = Color.clear;
        }

        private void PlayDiscoverySound(DiscoveryRarity rarity)
        {
            AudioClip clip = rarity switch
            {
                DiscoveryRarity.Ancient => ancientSound,
                DiscoveryRarity.Rare => mysterySound,
                _ => discoverySound
            };

            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private Color GetRarityColor(DiscoveryRarity rarity)
        {
            return rarity switch
            {
                DiscoveryRarity.Ancient => ancientDiscoveryColor,
                DiscoveryRarity.Rare => rareDiscoveryColor,
                _ => normalDiscoveryColor
            };
        }

        public void PlayAmbientMystery(Vector3 position)
        {
            // Subtle ambient effect for mysterious areas
            if (discoveryLight != null)
            {
                discoveryLight.transform.position = position;
                discoveryLight.color = rareDiscoveryColor;
                StartCoroutine(SubtleLightPulse(5f));
            }
        }

        private IEnumerator SubtleLightPulse(float duration)
        {
            if (discoveryLight == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pulse = (Mathf.Sin(elapsed * 2f) + 1f) / 2f;
                discoveryLight.intensity = pulse * 0.5f;
                yield return null;
            }

            discoveryLight.intensity = 0f;
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private float EaseInQuad(float t)
        {
            return t * t;
        }

        public enum DiscoveryRarity
        {
            Normal,
            Rare,
            Ancient
        }
    }
}
