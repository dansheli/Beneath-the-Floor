using UnityEngine;
using System.Collections;

namespace BeneathTheFloor.Machines
{
    public class MachineVisualFeedback : MonoBehaviour
    {
        [Header("Lights")]
        [SerializeField] private Light[] machineLights;
        [SerializeField] private Color idleColor = Color.gray;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color processingColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color unpoweredColor = new Color(0.2f, 0.1f, 0.1f);

        [Header("Indicators")]
        [SerializeField] private GameObject idleIndicator;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private GameObject processingIndicator;
        [SerializeField] private GameObject errorIndicator;

        [Header("Particles")]
        [SerializeField] private ParticleSystem smokeParticles;
        [SerializeField] private ParticleSystem sparkParticles;
        [SerializeField] private ParticleSystem glowParticles;

        [Header("Material Emission")]
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private string emissionProperty = "_EmissionColor";
        [SerializeField] private float emissionIntensity = 2f;

        [Header("Animation")]
        [SerializeField] private bool pulseWhenActive = true;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMinIntensity = 0.5f;

        private MachineState currentState = MachineState.Idle;
        private Coroutine pulseCoroutine;

        public enum MachineState
        {
            Idle,
            Active,
            Processing,
            Error,
            Unpowered
        }

        private void Start()
        {
            SetState(MachineState.Idle);
        }

        public void SetState(MachineState state)
        {
            currentState = state;

            // Stop any running animations
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            // Update all visual elements
            UpdateLights();
            UpdateIndicators();
            UpdateParticles();
            UpdateEmission();

            // Start pulse animation if needed
            if (pulseWhenActive && (state == MachineState.Active || state == MachineState.Processing))
            {
                pulseCoroutine = StartCoroutine(PulseAnimation());
            }
        }

        private void UpdateLights()
        {
            Color targetColor = GetStateColor(currentState);

            foreach (var light in machineLights)
            {
                if (light != null)
                {
                    light.color = targetColor;
                    light.enabled = currentState != MachineState.Unpowered;
                }
            }
        }

        private void UpdateIndicators()
        {
            // Disable all indicators
            SetIndicatorActive(idleIndicator, false);
            SetIndicatorActive(activeIndicator, false);
            SetIndicatorActive(processingIndicator, false);
            SetIndicatorActive(errorIndicator, false);

            // Enable appropriate indicator
            switch (currentState)
            {
                case MachineState.Idle:
                    SetIndicatorActive(idleIndicator, true);
                    break;
                case MachineState.Active:
                    SetIndicatorActive(activeIndicator, true);
                    break;
                case MachineState.Processing:
                    SetIndicatorActive(processingIndicator, true);
                    break;
                case MachineState.Error:
                    SetIndicatorActive(errorIndicator, true);
                    break;
            }
        }

        private void UpdateParticles()
        {
            // Smoke - only when processing
            if (smokeParticles != null)
            {
                if (currentState == MachineState.Processing)
                {
                    if (!smokeParticles.isPlaying) smokeParticles.Play();
                }
                else
                {
                    smokeParticles.Stop();
                }
            }

            // Sparks - when error
            if (sparkParticles != null)
            {
                if (currentState == MachineState.Error)
                {
                    if (!sparkParticles.isPlaying) sparkParticles.Play();
                }
                else
                {
                    sparkParticles.Stop();
                }
            }

            // Glow - when active or processing
            if (glowParticles != null)
            {
                if (currentState == MachineState.Active || currentState == MachineState.Processing)
                {
                    if (!glowParticles.isPlaying) glowParticles.Play();
                }
                else
                {
                    glowParticles.Stop();
                }
            }
        }

        private void UpdateEmission()
        {
            Color emissionColor = GetStateColor(currentState) * emissionIntensity;

            if (currentState == MachineState.Unpowered)
            {
                emissionColor = Color.black;
            }

            foreach (var renderer in emissiveRenderers)
            {
                if (renderer != null)
                {
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(emissionProperty, emissionColor);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        private Color GetStateColor(MachineState state)
        {
            return state switch
            {
                MachineState.Idle => idleColor,
                MachineState.Active => activeColor,
                MachineState.Processing => processingColor,
                MachineState.Error => errorColor,
                MachineState.Unpowered => unpoweredColor,
                _ => idleColor
            };
        }

        private void SetIndicatorActive(GameObject indicator, bool active)
        {
            if (indicator != null)
            {
                indicator.SetActive(active);
            }
        }

        private IEnumerator PulseAnimation()
        {
            while (true)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                float intensity = Mathf.Lerp(pulseMinIntensity, 1f, t);

                // Pulse lights
                foreach (var light in machineLights)
                {
                    if (light != null)
                    {
                        light.intensity = intensity;
                    }
                }

                // Pulse emission
                Color baseColor = GetStateColor(currentState);
                Color emissionColor = baseColor * emissionIntensity * intensity;

                foreach (var renderer in emissiveRenderers)
                {
                    if (renderer != null)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        block.SetColor(emissionProperty, emissionColor);
                        renderer.SetPropertyBlock(block);
                    }
                }

                yield return null;
            }
        }

        public void Flash(Color color, float duration = 0.3f)
        {
            StartCoroutine(FlashCoroutine(color, duration));
        }

        private IEnumerator FlashCoroutine(Color color, float duration)
        {
            Color originalColor = GetStateColor(currentState);

            // Set flash color
            foreach (var light in machineLights)
            {
                if (light != null) light.color = color;
            }

            yield return new WaitForSeconds(duration);

            // Restore original color
            foreach (var light in machineLights)
            {
                if (light != null) light.color = originalColor;
            }
        }

        public void SetPowered(bool powered)
        {
            if (!powered)
            {
                SetState(MachineState.Unpowered);
            }
            else if (currentState == MachineState.Unpowered)
            {
                SetState(MachineState.Idle);
            }
        }
    }
}
