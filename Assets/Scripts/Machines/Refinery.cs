using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Energy;

namespace BeneathTheFloor.Machines
{
    public class Refinery : MonoBehaviour, IInteractable
    {
        [Header("Refinery Settings")]
        [SerializeField] private string refineryName = "Refinery";
        [SerializeField] private int tier = 1;
        [SerializeField] private float processingSpeedMultiplier = 1f;
        [SerializeField] private float efficiencyMultiplier = 1f;

        [Header("Processing")]
        [SerializeField] private List<RefiningRecipe> refiningRecipes = new List<RefiningRecipe>();

        [Header("Energy")]
        [SerializeField] private bool requiresEnergy = true;
        [SerializeField] private float energyPerSecond = 2f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private Light processingLight;
        [SerializeField] private Color idleColor = Color.gray;
        [SerializeField] private Color processingColor = new Color(1f, 0.5f, 0f);
        [SerializeField] private ParticleSystem smokeParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip processingSound;
        [SerializeField] private AudioClip completeSound;

        // State
        private bool isOpen = false;
        private bool isProcessing = false;
        private float currentProcessProgress = 0f;
        private RefiningRecipe currentRecipe;
        private int inputAmount = 0;

        // Queue of items to process
        private Queue<QueuedRefining> processingQueue = new Queue<QueuedRefining>();

        // Events
        public UnityAction OnRefineryOpened;
        public UnityAction OnRefineryClosed;
        public UnityAction<float> OnProcessingProgress;
        public UnityAction<ResourceType, int> OnProcessingComplete;
        public UnityAction OnEnergyDepleted;

        public static Refinery CurrentRefinery { get; private set; }

        // Properties
        public bool CanInteract => true;
        public bool IsOpen => isOpen;
        public bool IsProcessing => isProcessing;
        public int Tier => tier;
        public float ProcessProgress => currentProcessProgress;
        public string RefineryName => refineryName;
        public IReadOnlyList<RefiningRecipe> Recipes => refiningRecipes;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            InitializeDefaultRecipes();
        }

        private void Start()
        {
            SetProcessingVisuals(false);
        }

        private void Update()
        {
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                UI.UIState.ConsumeEscape();
                CloseRefinery();
            }
        }

        private void InitializeDefaultRecipes()
        {
            if (refiningRecipes.Count == 0)
            {
                // Default refining recipes using proper refined resource types
                refiningRecipes.Add(new RefiningRecipe
                {
                    inputResource = ResourceType.Dirt,
                    outputResource = ResourceType.CompressedDirt,
                    inputAmount = 5,
                    outputAmount = 1,
                    processingTime = 3f,
                    displayName = "Compress Dirt"
                });

                refiningRecipes.Add(new RefiningRecipe
                {
                    inputResource = ResourceType.Clay,
                    outputResource = ResourceType.HardenedClay,
                    inputAmount = 2,
                    outputAmount = 1,
                    processingTime = 5f,
                    displayName = "Harden Clay"
                });

                refiningRecipes.Add(new RefiningRecipe
                {
                    inputResource = ResourceType.Coal,
                    outputResource = ResourceType.RefinedCoal,
                    inputAmount = 3,
                    outputAmount = 2,
                    processingTime = 4f,
                    displayName = "Refine Coal"
                });

                refiningRecipes.Add(new RefiningRecipe
                {
                    inputResource = ResourceType.IronOre,
                    outputResource = ResourceType.IronIngot,
                    inputAmount = 2,
                    outputAmount = 1,
                    processingTime = 6f,
                    displayName = "Smelt Iron Ore"
                });

                refiningRecipes.Add(new RefiningRecipe
                {
                    inputResource = ResourceType.Copper,
                    outputResource = ResourceType.CopperIngot,
                    inputAmount = 2,
                    outputAmount = 1,
                    processingTime = 5f,
                    displayName = "Smelt Copper"
                });
            }
        }

        public string GetInteractionText()
        {
            if (isProcessing)
            {
                return $"Processing... {Mathf.RoundToInt(currentProcessProgress * 100)}%";
            }
            return $"Press E to use {refineryName}";
        }

        public void Interact(GameObject interactor)
        {
            if (isOpen)
            {
                CloseRefinery();
            }
            else
            {
                OpenRefinery();
            }
        }

        public void OnHoverEnter()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }

        public void OnHoverExit()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }

        public void OpenRefinery()
        {
            isOpen = true;
            CurrentRefinery = this;
            UI.UIState.IsMachineUIOpen = true;

            // Tell interaction system that UI is open
            if (Interaction.InteractionSystem.Instance != null)
            {
                Interaction.InteractionSystem.Instance.SetUIOpen(true);
            }

            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            RefineryUI.Instance?.ShowUI(this);

            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            OnRefineryOpened?.Invoke();
            Debug.Log($"[Refinery] Opened {refineryName}");
        }

        public void CloseRefinery()
        {
            isOpen = false;
            CurrentRefinery = null;

            RefineryUI.Instance?.HideUI();

            // Tell interaction system that UI is closed
            if (Interaction.InteractionSystem.Instance != null)
            {
                Interaction.InteractionSystem.Instance.SetUIOpen(false);
            }

            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = true;
            }

            // Clear UI state flag
            UI.UIState.IsMachineUIOpen = false;

            // Set cursor state
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Ensure game is unpaused
            Time.timeScale = 1f;

            OnRefineryClosed?.Invoke();
            Debug.Log($"[Refinery] Closed {refineryName}");
        }

        public bool CanProcess(RefiningRecipe recipe, int amount = 1)
        {
            if (recipe == null) return false;

            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return false;

            int required = recipe.inputAmount * amount;
            return inventory.GetResourceCount(recipe.inputResource) >= required;
        }

        public bool StartProcessing(RefiningRecipe recipe, int batchCount = 1)
        {
            if (recipe == null || batchCount <= 0) return false;
            if (!CanProcess(recipe, batchCount)) return false;

            // Check energy
            if (requiresEnergy && EnergyManager.Instance != null)
            {
                if (!EnergyManager.Instance.HasEnergy(energyPerSecond))
                {
                    Debug.Log("[Refinery] Not enough energy to start");
                    return false;
                }
            }

            // Consume input resources
            var inventory = Inventory.InventorySystem.Instance;
            int totalInput = recipe.inputAmount * batchCount;
            inventory.RemoveResource(recipe.inputResource, totalInput);

            // Queue the processing
            processingQueue.Enqueue(new QueuedRefining
            {
                recipe = recipe,
                batchCount = batchCount
            });

            // Start processing if not already
            if (!isProcessing)
            {
                StartCoroutine(ProcessingCoroutine());
            }

            Debug.Log($"[Refinery] Queued {batchCount}x {recipe.displayName}");
            return true;
        }

        private IEnumerator ProcessingCoroutine()
        {
            isProcessing = true;
            SetProcessingVisuals(true);

            // Play processing sound
            if (processingSound != null && audioSource != null)
            {
                audioSource.clip = processingSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            while (processingQueue.Count > 0)
            {
                var queued = processingQueue.Dequeue();
                currentRecipe = queued.recipe;
                inputAmount = queued.batchCount;

                float totalTime = currentRecipe.processingTime / processingSpeedMultiplier;
                currentProcessProgress = 0f;

                while (currentProcessProgress < 1f)
                {
                    // Check energy
                    if (requiresEnergy && EnergyManager.Instance != null)
                    {
                        if (!EnergyManager.Instance.ConsumeEnergy(energyPerSecond * Time.deltaTime))
                        {
                            // Pause processing - out of energy
                            OnEnergyDepleted?.Invoke();
                            SetProcessingVisuals(false);

                            // Wait for energy
                            while (EnergyManager.Instance != null &&
                                   !EnergyManager.Instance.HasEnergy(energyPerSecond * Time.deltaTime))
                            {
                                yield return new WaitForSeconds(0.5f);
                            }

                            SetProcessingVisuals(true);
                        }
                    }

                    currentProcessProgress += Time.deltaTime / totalTime;
                    OnProcessingProgress?.Invoke(currentProcessProgress);
                    yield return null;
                }

                // Complete this batch
                CompleteProcessing(currentRecipe, inputAmount);
            }

            // All done
            isProcessing = false;
            currentRecipe = null;
            currentProcessProgress = 0f;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            SetProcessingVisuals(false);
        }

        private void CompleteProcessing(RefiningRecipe recipe, int batchCount)
        {
            // Calculate output with efficiency
            int baseOutput = recipe.outputAmount * batchCount;
            int bonusOutput = Mathf.FloorToInt(baseOutput * (efficiencyMultiplier - 1f));
            int totalOutput = baseOutput + bonusOutput;

            // Add to inventory
            var inventory = Inventory.InventorySystem.Instance;
            inventory?.AddResource(recipe.outputResource, totalOutput);

            // Play complete sound
            if (completeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(completeSound);
            }

            OnProcessingComplete?.Invoke(recipe.outputResource, totalOutput);
            Debug.Log($"[Refinery] Completed: {totalOutput}x {recipe.outputResource}");
        }

        private void SetProcessingVisuals(bool active)
        {
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(active);
            }

            if (processingLight != null)
            {
                processingLight.color = active ? processingColor : idleColor;
                processingLight.enabled = active;
            }

            if (smokeParticles != null)
            {
                if (active)
                {
                    smokeParticles.Play();
                }
                else
                {
                    smokeParticles.Stop();
                }
            }
        }

        public RefiningRecipe GetRecipeForResource(ResourceType inputType)
        {
            return refiningRecipes.Find(r => r.inputResource == inputType);
        }

        public void UpgradeTier(int newTier)
        {
            tier = newTier;
            Debug.Log($"[Refinery] Upgraded to tier {tier}");
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            processingSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void SetEfficiencyMultiplier(float multiplier)
        {
            efficiencyMultiplier = Mathf.Max(1f, multiplier);
        }
    }

    [System.Serializable]
    public class RefiningRecipe
    {
        public string displayName;
        public ResourceType inputResource;
        public ResourceType outputResource;
        public int inputAmount = 1;
        public int outputAmount = 1;
        public float processingTime = 5f;
        public Sprite icon;
    }

    public class QueuedRefining
    {
        public RefiningRecipe recipe;
        public int batchCount;
    }
}
