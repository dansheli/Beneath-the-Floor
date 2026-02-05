using UnityEngine;
using UnityEngine.Events;

namespace BeneathTheFloor.Energy
{
    public class EnergySource : MonoBehaviour
    {
        [Header("Source Settings")]
        [SerializeField] private string sourceName = "Energy Source";
        [SerializeField] private EnergySourceType sourceType = EnergySourceType.Battery;
        [SerializeField] private float generationRate = 5f;
        [SerializeField] private bool isActive = true;

        [Header("Fuel Settings (for generators)")]
        [SerializeField] private bool requiresFuel = false;
        [SerializeField] private ResourceType fuelType = ResourceType.Coal;
        [SerializeField] private float fuelConsumptionRate = 0.1f;
        [SerializeField] private float currentFuel = 0f;
        [SerializeField] private float maxFuel = 100f;

        [Header("Battery Settings")]
        [SerializeField] private bool isBattery = false;
        [SerializeField] private float storedEnergy = 50f;
        [SerializeField] private float maxStoredEnergy = 100f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private Light sourceLight;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.red;
        [SerializeField] private ParticleSystem activeParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip runningSound;

        // Events
        public UnityAction OnSourceActivated;
        public UnityAction OnSourceDeactivated;
        public UnityAction OnFuelDepleted;

        // Properties
        public string SourceName => sourceName;
        public EnergySourceType SourceType => sourceType;
        public bool IsActive => isActive && (!requiresFuel || currentFuel > 0) && (!isBattery || storedEnergy > 0);
        public float CurrentFuel => currentFuel;
        public float MaxFuel => maxFuel;
        public float FuelPercent => maxFuel > 0 ? currentFuel / maxFuel : 0;
        public float StoredEnergy => storedEnergy;
        public float MaxStoredEnergy => maxStoredEnergy;

        private bool wasActive = false;

        private void Start()
        {
            // Register with EnergyManager
            EnergyManager.Instance?.RegisterSource(this);

            UpdateVisuals();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnDestroy()
        {
            EnergyManager.Instance?.UnregisterSource(this);
        }

        private void Update()
        {
            if (!isActive) return;

            // Consume fuel if required
            if (requiresFuel && currentFuel > 0)
            {
                currentFuel -= fuelConsumptionRate * Time.deltaTime;

                if (currentFuel <= 0)
                {
                    currentFuel = 0;
                    OnFuelDepleted?.Invoke();
                    Debug.Log($"[EnergySource] {sourceName} ran out of fuel!");
                }
            }

            // Drain battery if applicable
            if (isBattery && storedEnergy > 0)
            {
                // Battery drains as it provides power
                float drain = generationRate * Time.deltaTime;
                storedEnergy = Mathf.Max(0, storedEnergy - drain);
            }

            // Check state changes
            bool currentlyActive = IsActive;
            if (currentlyActive != wasActive)
            {
                wasActive = currentlyActive;
                UpdateVisuals();

                if (currentlyActive)
                {
                    OnSourceActivated?.Invoke();
                }
                else
                {
                    OnSourceDeactivated?.Invoke();
                }
            }
        }

        public float GetGenerationRate()
        {
            if (!IsActive) return 0;
            return generationRate;
        }

        public void SetActive(bool active)
        {
            isActive = active;
            UpdateVisuals();
        }

        public void Toggle()
        {
            SetActive(!isActive);
        }

        public bool AddFuel(ResourceType type, int amount)
        {
            if (!requiresFuel || type != fuelType)
            {
                return false;
            }

            // Consume from inventory
            var inventory = Inventory.InventorySystem.Instance;
            if (inventory != null && inventory.GetResourceCount(type) >= amount)
            {
                inventory.RemoveResource(type, amount);

                // Add fuel (conversion rate could vary)
                float fuelValue = amount * 10f; // 1 coal = 10 fuel units
                currentFuel = Mathf.Min(currentFuel + fuelValue, maxFuel);

                Debug.Log($"[EnergySource] Added {amount} {type} as fuel. Current fuel: {currentFuel:F1}");
                return true;
            }

            return false;
        }

        public void RefillFuel()
        {
            currentFuel = maxFuel;
        }

        public bool RechargeStorage(float amount)
        {
            if (!isBattery) return false;

            float previousEnergy = storedEnergy;
            storedEnergy = Mathf.Min(storedEnergy + amount, maxStoredEnergy);

            return storedEnergy > previousEnergy;
        }

        public void SetGenerationRate(float rate)
        {
            generationRate = Mathf.Max(0, rate);
        }

        private void UpdateVisuals()
        {
            bool active = IsActive;

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(active);
            }

            if (sourceLight != null)
            {
                sourceLight.color = active ? activeColor : inactiveColor;
                sourceLight.enabled = isActive; // Show even when out of fuel but "on"
            }

            if (activeParticles != null)
            {
                if (active)
                {
                    if (!activeParticles.isPlaying)
                    {
                        activeParticles.Play();
                    }
                }
                else
                {
                    activeParticles.Stop();
                }
            }

            // Handle audio
            if (audioSource != null && runningSound != null)
            {
                if (active && !audioSource.isPlaying)
                {
                    audioSource.clip = runningSound;
                    audioSource.loop = true;
                    audioSource.Play();
                }
                else if (!active && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }
        }

        public string GetStatusText()
        {
            string status = $"{sourceName} [{sourceType}]\n";
            status += $"Status: {(IsActive ? "Active" : "Inactive")}\n";
            status += $"Output: {GetGenerationRate():F1}/s\n";

            if (requiresFuel)
            {
                status += $"Fuel: {currentFuel:F1}/{maxFuel:F0} ({FuelPercent * 100:F0}%)\n";
            }

            if (isBattery)
            {
                float batteryPercent = maxStoredEnergy > 0 ? storedEnergy / maxStoredEnergy * 100 : 0;
                status += $"Charge: {storedEnergy:F1}/{maxStoredEnergy:F0} ({batteryPercent:F0}%)\n";
            }

            return status;
        }
    }

    public enum EnergySourceType
    {
        Battery,
        FuelGenerator,
        SolarPanel,
        AncientNode,
        Manual
    }
}
