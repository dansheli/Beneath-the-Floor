using UnityEngine;
using UnityEngine.Events;

namespace BeneathTheFloor.Energy
{
    public class EnergyConsumer : MonoBehaviour
    {
        [Header("Consumer Settings")]
        [SerializeField] private string consumerName = "Energy Consumer";
        [SerializeField] private float consumptionRate = 2f;
        [SerializeField] private bool isConsuming = false;
        [SerializeField] private bool autoConsume = false;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject poweredIndicator;
        [SerializeField] private GameObject unpoweredIndicator;
        [SerializeField] private Light[] poweredLights;
        [SerializeField] private Color poweredColor = Color.green;
        [SerializeField] private Color unpoweredColor = Color.red;

        // Events
        public UnityAction OnPowerGained;
        public UnityAction OnPowerLost;

        // Properties
        public string ConsumerName => consumerName;
        public bool IsConsuming => isConsuming;
        public bool IsPowered { get; private set; } = true;
        public float ConsumptionRate => consumptionRate;

        private void Start()
        {
            // Register with EnergyManager
            EnergyManager.Instance?.RegisterConsumer(this);

            UpdateVisuals();
        }

        private void OnDestroy()
        {
            EnergyManager.Instance?.UnregisterConsumer(this);
        }

        private void Update()
        {
            if (!isConsuming) return;

            if (autoConsume)
            {
                TryConsumeEnergy();
            }
        }

        public float GetConsumptionRate()
        {
            return isConsuming ? consumptionRate : 0;
        }

        public void StartConsuming()
        {
            isConsuming = true;
        }

        public void StopConsuming()
        {
            isConsuming = false;
        }

        public bool TryConsumeEnergy()
        {
            if (!isConsuming) return false;

            var energyManager = EnergyManager.Instance;
            if (energyManager == null) return false;

            float amountToConsume = consumptionRate * Time.deltaTime;
            bool success = energyManager.ConsumeEnergy(amountToConsume);

            // Update powered state
            bool wasPowered = IsPowered;
            IsPowered = success;

            if (IsPowered != wasPowered)
            {
                UpdateVisuals();

                if (IsPowered)
                {
                    OnPowerGained?.Invoke();
                }
                else
                {
                    OnPowerLost?.Invoke();
                }
            }

            return success;
        }

        public bool ConsumeFixedAmount(float amount)
        {
            var energyManager = EnergyManager.Instance;
            if (energyManager == null) return false;

            return energyManager.ConsumeEnergy(amount);
        }

        public bool HasEnergyFor(float amount)
        {
            var energyManager = EnergyManager.Instance;
            if (energyManager == null) return false;

            return energyManager.HasEnergy(amount);
        }

        public void SetConsumptionRate(float rate)
        {
            consumptionRate = Mathf.Max(0, rate);
        }

        private void UpdateVisuals()
        {
            if (poweredIndicator != null)
            {
                poweredIndicator.SetActive(IsPowered && isConsuming);
            }

            if (unpoweredIndicator != null)
            {
                unpoweredIndicator.SetActive(!IsPowered && isConsuming);
            }

            if (poweredLights != null)
            {
                Color targetColor = IsPowered ? poweredColor : unpoweredColor;
                foreach (var light in poweredLights)
                {
                    if (light != null)
                    {
                        light.color = targetColor;
                        light.enabled = isConsuming;
                    }
                }
            }
        }

        public void ForcePowerState(bool powered)
        {
            bool wasPowered = IsPowered;
            IsPowered = powered;

            if (IsPowered != wasPowered)
            {
                UpdateVisuals();

                if (IsPowered)
                {
                    OnPowerGained?.Invoke();
                }
                else
                {
                    OnPowerLost?.Invoke();
                }
            }
        }
    }
}
