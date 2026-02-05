// ============================================================================
// DEPRECATED: This file has been moved to Legacy/Workbench/ and is no longer in use.
// The Workbench system has been replaced by UpgradeStation as the single upgrade point.
// This file is kept for reference only and should NOT be compiled.
// If you need to reference this code, copy what you need to your new implementation.
// ============================================================================
#if false // Disabled - remove this line only if you need to compile for reference

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Crafting;

namespace BeneathTheFloor.Machines
{
    public class Workbench : MonoBehaviour, IInteractable
    {
        [Header("Workbench Settings")]
        [SerializeField] private string workbenchName = "Workbench";
        [SerializeField] private int tier = 1;
        [SerializeField] private float craftingSpeedMultiplier = 1f;

        [Header("Energy")]
        [SerializeField] private bool requiresEnergy = false;
        [SerializeField] private float energyPerCraft = 5f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private Light workLight;
        [SerializeField] private ParticleSystem craftingParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip craftingSound;
        [SerializeField] private AudioClip craftCompleteSound;

        // State
        private bool isOpen = false;
        private bool isCrafting = false;
        private float currentCraftProgress = 0f;
        private CraftingRecipe currentRecipe;

        // Events
        public UnityAction OnWorkbenchOpened;
        public UnityAction OnWorkbenchClosed;
        public UnityAction<float> OnCraftingProgress;
        public UnityAction<CraftingRecipe> OnCraftingComplete;

        public static Workbench CurrentWorkbench { get; private set; }

        // Properties
        public bool CanInteract => !isCrafting;
        public bool IsOpen => isOpen;
        public bool IsCrafting => isCrafting;
        public int Tier => tier;
        public float CraftProgress => currentCraftProgress;
        public CraftingRecipe CurrentRecipe => currentRecipe;
        public string WorkbenchName => workbenchName;

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
        }

        private void Start()
        {
            SetActiveIndicator(false);
        }

        private void Update()
        {
            // Close workbench if player presses Escape or moves too far
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWorkbench();
            }
        }

        public string GetInteractionText()
        {
            if (isCrafting)
            {
                return $"Crafting... {Mathf.RoundToInt(currentCraftProgress * 100)}%";
            }
            return $"Press E to use {workbenchName}";
        }

        public void Interact(GameObject interactor)
        {
            if (isCrafting) return;

            if (isOpen)
            {
                CloseWorkbench();
            }
            else
            {
                OpenWorkbench();
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

        public void OpenWorkbench()
        {
            isOpen = true;
            CurrentWorkbench = this;

            // Tell interaction system that UI is open (prevents further interactions)
            if (InteractionSystem.Instance != null)
            {
                InteractionSystem.Instance.SetUIOpen(true);
            }

            // Play sound
            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            // Show UI
            if (WorkbenchUI.Instance != null)
            {
                WorkbenchUI.Instance.ShowUI(this);
            }
            else
            {
                Debug.LogError("[Workbench] WorkbenchUI.Instance is NULL! Make sure WorkbenchUI exists in the scene.");
            }

            // Disable player movement
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = false;
            }

            // Set UIState so other systems know workbench is open
            UI.UIState.IsMachineUIOpen = true;

            // Unlock cursor for UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            OnWorkbenchOpened?.Invoke();
            Debug.Log($"[Workbench] Opened {workbenchName}");
        }

        public void CloseWorkbench()
        {
            if (isCrafting) return;

            isOpen = false;
            CurrentWorkbench = null;

            // Hide UI
            WorkbenchUI.Instance?.HideUI();

            // Tell interaction system that UI is closed
            if (InteractionSystem.Instance != null)
            {
                InteractionSystem.Instance.SetUIOpen(false);
            }

            // Enable player movement
            if (Player.FirstPersonController.Instance != null)
            {
                Player.FirstPersonController.Instance.CanMove = true;
            }

            // Clear UIState - this handles cursor lock
            UI.UIState.IsMachineUIOpen = false;

            // Force cursor lock if no other UI is open
            if (!UI.UIState.IsAnyUIOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            OnWorkbenchClosed?.Invoke();
            Debug.Log($"[Workbench] Closed {workbenchName}");
        }

        public bool StartCrafting(CraftingRecipe recipe)
        {
            if (isCrafting || recipe == null) return false;

            // Check tier requirement
            if (recipe.requiredWorkbenchTier > tier)
            {
                Debug.Log($"[Workbench] Recipe requires tier {recipe.requiredWorkbenchTier}, workbench is tier {tier}");
                return false;
            }

            // Check energy if required
            if (requiresEnergy)
            {
                var energyManager = Energy.EnergyManager.Instance;
                if (energyManager != null && !energyManager.HasEnergy(energyPerCraft + recipe.energyCost))
                {
                    Debug.Log("[Workbench] Not enough energy");
                    return false;
                }
            }

            // Try to start crafting through CraftingManager
            if (!CraftingManager.Instance.TryCraft(recipe))
            {
                return false;
            }

            currentRecipe = recipe;
            StartCoroutine(CraftingCoroutine(recipe));
            return true;
        }

        private IEnumerator CraftingCoroutine(CraftingRecipe recipe)
        {
            isCrafting = true;
            currentCraftProgress = 0f;

            SetActiveIndicator(true);

            // Play crafting sound
            if (craftingSound != null && audioSource != null)
            {
                audioSource.clip = craftingSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // Start particles
            if (craftingParticles != null)
            {
                craftingParticles.Play();
            }

            // Consume energy
            if (requiresEnergy)
            {
                Energy.EnergyManager.Instance?.ConsumeEnergy(energyPerCraft + recipe.energyCost);
            }

            float craftTime = recipe.craftingTime / craftingSpeedMultiplier;

            while (currentCraftProgress < 1f)
            {
                currentCraftProgress += Time.deltaTime / craftTime;
                OnCraftingProgress?.Invoke(currentCraftProgress);
                yield return null;
            }

            currentCraftProgress = 1f;

            // Stop sounds and effects
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            if (craftingParticles != null)
            {
                craftingParticles.Stop();
            }

            // Play complete sound
            if (craftCompleteSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(craftCompleteSound);
            }

            // Complete the craft
            CraftingManager.Instance.CompleteCraft(recipe);

            OnCraftingComplete?.Invoke(recipe);

            isCrafting = false;
            currentRecipe = null;
            currentCraftProgress = 0f;

            SetActiveIndicator(false);

            Debug.Log($"[Workbench] Completed crafting {recipe.recipeName}");
        }

        public void CancelCrafting()
        {
            if (!isCrafting) return;

            StopAllCoroutines();

            // Refund resources (partial or full based on progress)
            // For simplicity, no refund on cancel

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            if (craftingParticles != null)
            {
                craftingParticles.Stop();
            }

            isCrafting = false;
            currentRecipe = null;
            currentCraftProgress = 0f;

            SetActiveIndicator(false);

            Debug.Log("[Workbench] Crafting cancelled");
        }

        private void SetActiveIndicator(bool active)
        {
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(active);
            }

            if (workLight != null)
            {
                workLight.enabled = active;
            }
        }

        public List<CraftingRecipe> GetAvailableRecipes()
        {
            if (CraftingManager.Instance == null) return new List<CraftingRecipe>();
            return CraftingManager.Instance.GetRecipesForWorkbench(tier);
        }

        public void UpgradeTier(int newTier)
        {
            tier = newTier;
            Debug.Log($"[Workbench] Upgraded to tier {tier}");
        }

        public void SetCraftingSpeedMultiplier(float multiplier)
        {
            craftingSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        }
    }
}

#endif // End of disabled code block
