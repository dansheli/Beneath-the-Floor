using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;
using BeneathTheFloor.Energy;

namespace BeneathTheFloor.Crafting
{
    public enum RecipeType
    {
        Workbench,
        Refinery,
        Special
    }

    public class CraftingManager : MonoBehaviour
    {
        [Header("Recipe Database")]
        [SerializeField] private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

        [Header("Unlocked Recipes")]
        [SerializeField] private List<CraftingRecipe> unlockedRecipes = new List<CraftingRecipe>();

        [Header("Settings")]
        [SerializeField] private bool createDefaultRecipes = true;

        [Header("Debug")]
        #pragma warning disable CS0414 // Reserved for debug logging
        [SerializeField] private bool enableDebugLogs = false;
        #pragma warning restore CS0414

        public static CraftingManager Instance { get; private set; }

        // Events
        public UnityAction<CraftingRecipe> OnRecipeUnlocked;
        public UnityAction<CraftingRecipe, ItemSO, int> OnItemCrafted;
        public UnityAction<CraftingRecipe> OnCraftingStarted;
        public UnityAction<CraftingRecipe> OnCraftingFailed;

        // In-memory runtime recipes (for when no ScriptableObjects are assigned)
        private List<RuntimeRecipe> runtimeRecipes = new List<RuntimeRecipe>();

        public IReadOnlyList<CraftingRecipe> AllRecipes => allRecipes;
        public IReadOnlyList<CraftingRecipe> UnlockedRecipes => unlockedRecipes;
        public IReadOnlyList<RuntimeRecipe> RuntimeRecipes => runtimeRecipes;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // DontDestroyOnLoad only works on root GameObjects
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);

                InitializeRecipes();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeRecipes()
        {
            unlockedRecipes.Clear();

            foreach (var recipe in allRecipes)
            {
                if (recipe != null && recipe.isUnlockedByDefault)
                {
                    unlockedRecipes.Add(recipe);
                }
            }

            // Always create default runtime recipes if enabled (regardless of ScriptableObject recipes)
            if (createDefaultRecipes)
            {
                CreateDefaultRuntimeRecipes();
            }
        }

        private void CreateDefaultRuntimeRecipes()
        {
            runtimeRecipes.Clear(); // Ensure we start fresh

            // === WORKBENCH RECIPES ===

            // Wooden Plank: Dirt x3 -> WoodenPlank x1
            runtimeRecipes.Add(new RuntimeRecipe
            {
                recipeId = "wooden_plank",
                recipeName = "Wooden Plank",
                description = "A basic wooden plank for construction.",
                recipeType = RecipeType.Workbench,
                requiredResources = new List<ResourceRequirement>
                {
                    new ResourceRequirement { resourceType = ResourceType.Dirt, amount = 3 }
                },
                outputResourceType = ResourceType.WoodenPlank,
                outputAmount = 1,
                craftingTime = 0.5f, // Reduced for faster testing
                requiredWorkbenchTier = 1,
                isUnlocked = true
            });

            // Metal Plate: IronIngot x2 -> MetalPlate x1
            runtimeRecipes.Add(new RuntimeRecipe
            {
                recipeId = "metal_plate",
                recipeName = "Metal Plate",
                description = "A sturdy metal plate for advanced crafting.",
                recipeType = RecipeType.Workbench,
                requiredResources = new List<ResourceRequirement>
                {
                    new ResourceRequirement { resourceType = ResourceType.IronIngot, amount = 2 }
                },
                outputResourceType = ResourceType.MetalPlate,
                outputAmount = 1,
                craftingTime = 3f,
                requiredWorkbenchTier = 1,
                isUnlocked = true
            });

            // Copper Wire: CopperIngot x1 -> CopperWire x2
            runtimeRecipes.Add(new RuntimeRecipe
            {
                recipeId = "copper_wire",
                recipeName = "Copper Wire",
                description = "Thin copper wires for electrical components.",
                recipeType = RecipeType.Workbench,
                requiredResources = new List<ResourceRequirement>
                {
                    new ResourceRequirement { resourceType = ResourceType.CopperIngot, amount = 1 }
                },
                outputResourceType = ResourceType.CopperWire,
                outputAmount = 2,
                craftingTime = 2f,
                requiredWorkbenchTier = 1,
                isUnlocked = true
            });

            // Basic Circuit: CopperWire x2, RefinedCoal x1 -> Circuit x1
            runtimeRecipes.Add(new RuntimeRecipe
            {
                recipeId = "basic_circuit",
                recipeName = "Basic Circuit",
                description = "A simple circuit board for machinery.",
                recipeType = RecipeType.Workbench,
                requiredResources = new List<ResourceRequirement>
                {
                    new ResourceRequirement { resourceType = ResourceType.CopperWire, amount = 2 },
                    new ResourceRequirement { resourceType = ResourceType.RefinedCoal, amount = 1 }
                },
                outputResourceType = ResourceType.Circuit,
                outputAmount = 1,
                craftingTime = 4f,
                requiredWorkbenchTier = 2,
                isUnlocked = true
            });

            // Tool recipe: Wooden Shovel+ (outputs a tool, special handling)
            runtimeRecipes.Add(new RuntimeRecipe
            {
                recipeId = "wooden_shovel_plus",
                recipeName = "Wooden Shovel+",
                description = "An upgraded wooden shovel with better dig power.",
                recipeType = RecipeType.Workbench,
                requiredResources = new List<ResourceRequirement>
                {
                    new ResourceRequirement { resourceType = ResourceType.WoodenPlank, amount = 2 },
                    new ResourceRequirement { resourceType = ResourceType.Dirt, amount = 2 }
                },
                outputResourceType = null, // Tool output
                outputAmount = 1,
                craftingTime = 5f,
                requiredWorkbenchTier = 1,
                isUnlocked = true,
                isToolOutput = true,
                outputToolName = "Wooden Shovel+",
                outputToolTier = 2,
                outputToolDigSpeed = 1.5f,
                outputToolDurability = 150
            });

        }

        /// <summary>
        /// Get all workbench recipes for the given tier (both runtime and SO).
        /// </summary>
        public List<RuntimeRecipe> GetWorkbenchRecipes(int tier)
        {
            List<RuntimeRecipe> result = new List<RuntimeRecipe>();
            foreach (var recipe in runtimeRecipes)
            {
                if (recipe.recipeType == RecipeType.Workbench &&
                    recipe.requiredWorkbenchTier <= tier &&
                    recipe.isUnlocked)
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all refinery recipes for the given tier.
        /// </summary>
        public List<RuntimeRecipe> GetRefineryRecipes(int tier)
        {
            List<RuntimeRecipe> result = new List<RuntimeRecipe>();
            foreach (var recipe in runtimeRecipes)
            {
                if (recipe.recipeType == RecipeType.Refinery &&
                    recipe.requiredWorkbenchTier <= tier &&
                    recipe.isUnlocked)
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        public bool CanCraftRecipe(CraftingRecipe recipe)
        {
            if (recipe == null) return false;
            if (!unlockedRecipes.Contains(recipe)) return false;

            var inventory = Inventory.InventorySystem.Instance;
            return recipe.CanCraft(inventory);
        }

        public bool TryCraft(CraftingRecipe recipe)
        {
            if (!CanCraftRecipe(recipe))
            {
                OnCraftingFailed?.Invoke(recipe);
                return false;
            }

            // Consume resources
            var inventory = Inventory.InventorySystem.Instance;
            recipe.ConsumeResources(inventory);

            OnCraftingStarted?.Invoke(recipe);
            return true;
        }

        public void CompleteCraft(CraftingRecipe recipe)
        {
            if (recipe == null || recipe.outputItem == null) return;

            // Add crafted item to inventory
            // For now, if it's a tool, we convert it to ToolData
            if (recipe.outputItem is ToolItemSO toolItem)
            {
                var toolData = CreateToolData(toolItem);
                Inventory.InventorySystem.Instance?.EquipTool(toolData);
            }
            else if (recipe.outputItem is MaterialItemSO materialItem)
            {
                // Add as resource if it maps to a ResourceType
                if (materialItem.sourceResource.HasValue)
                {
                    Inventory.InventorySystem.Instance?.AddResource(
                        materialItem.sourceResource.Value,
                        recipe.outputAmount
                    );
                }
            }

            OnItemCrafted?.Invoke(recipe, recipe.outputItem, recipe.outputAmount);
        }

        private ToolData CreateToolData(ToolItemSO toolSO)
        {
            return new ToolData
            {
                toolName = toolSO.itemName,
                tier = toolSO.tier,
                digSpeed = toolSO.digSpeed,
                durability = toolSO.maxDurability,
                maxDurability = toolSO.maxDurability,
                maxDepth = toolSO.maxDigDepth,
                icon = toolSO.icon,
                prefab = toolSO.heldPrefab
            };
        }

        public void UnlockRecipe(CraftingRecipe recipe)
        {
            if (recipe == null || unlockedRecipes.Contains(recipe)) return;

            unlockedRecipes.Add(recipe);
            OnRecipeUnlocked?.Invoke(recipe);
        }

        public void UnlockRecipeById(string recipeId)
        {
            var recipe = allRecipes.Find(r => r.recipeId == recipeId);
            if (recipe != null)
            {
                UnlockRecipe(recipe);
            }
        }

        public bool IsRecipeUnlocked(CraftingRecipe recipe)
        {
            return unlockedRecipes.Contains(recipe);
        }

        public List<CraftingRecipe> GetCraftableRecipes()
        {
            List<CraftingRecipe> craftable = new List<CraftingRecipe>();

            foreach (var recipe in unlockedRecipes)
            {
                if (CanCraftRecipe(recipe))
                {
                    craftable.Add(recipe);
                }
            }

            return craftable;
        }

        public List<CraftingRecipe> GetRecipesForWorkbench(int workbenchTier)
        {
            List<CraftingRecipe> recipes = new List<CraftingRecipe>();

            foreach (var recipe in unlockedRecipes)
            {
                if (recipe.requiredWorkbenchTier <= workbenchTier)
                {
                    recipes.Add(recipe);
                }
            }

            return recipes;
        }

        public void AddRecipeToDatabase(CraftingRecipe recipe)
        {
            if (recipe != null && !allRecipes.Contains(recipe))
            {
                allRecipes.Add(recipe);
            }
        }

        #region Runtime Recipe Methods

        public List<RuntimeRecipe> GetRuntimeRecipesForWorkbench(int tier)
        {
            List<RuntimeRecipe> result = new List<RuntimeRecipe>();
            foreach (var recipe in runtimeRecipes)
            {
                if (recipe.recipeType == RecipeType.Workbench &&
                    recipe.requiredWorkbenchTier <= tier &&
                    recipe.isUnlocked)
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        public bool CanCraftRuntimeRecipe(RuntimeRecipe recipe)
        {
            if (recipe == null || !recipe.isUnlocked) return false;

            var inventory = Inventory.InventorySystem.Instance;
            if (inventory == null) return false;

            // Check energy
            if (recipe.energyCost > 0)
            {
                var energyManager = EnergyManager.Instance;
                if (energyManager == null || !energyManager.HasEnergy(recipe.energyCost))
                {
                    return false;
                }
            }

            foreach (var req in recipe.requiredResources)
            {
                if (inventory.GetResourceCount(req.resourceType) < req.amount)
                {
                    return false;
                }
            }

            return true;
        }

        public void Craft(RuntimeRecipe recipe, Action onSuccess, Action<string> onFail)
        {
            if (!CanCraftRuntimeRecipe(recipe))
            {
                string missing = GetMissingResourcesText(recipe);
                onFail?.Invoke(missing);
                return;
            }

            // Consume energy
            if (recipe.energyCost > 0)
            {
                EnergyManager.Instance?.ConsumeEnergy(recipe.energyCost);
            }

            // Consume resources
            var inventory = Inventory.InventorySystem.Instance;
            foreach (var req in recipe.requiredResources)
            {
                inventory.RemoveResource(req.resourceType, req.amount);
            }

            // Add output
            if (recipe.isToolOutput)
            {
                // Create tool
                var toolData = new ToolData
                {
                    toolName = recipe.outputToolName,
                    tier = recipe.outputToolTier,
                    digSpeed = recipe.outputToolDigSpeed,
                    durability = recipe.outputToolDurability,
                    maxDurability = recipe.outputToolDurability,
                    maxDepth = 100
                };
                inventory.AddTool(toolData);
            }
            else if (recipe.outputResourceType.HasValue)
            {
                inventory.AddResource(recipe.outputResourceType.Value, recipe.outputAmount);
            }

            onSuccess?.Invoke();
        }

        public string GetMissingResourcesText(RuntimeRecipe recipe)
        {
            if (recipe == null) return "Invalid recipe";

            var inventory = Inventory.InventorySystem.Instance;
            List<string> missing = new List<string>();

            // Check energy
            if (recipe.energyCost > 0)
            {
                float currentEnergy = EnergyManager.Instance?.CurrentEnergy ?? 0;
                if (currentEnergy < recipe.energyCost)
                {
                    missing.Add($"Energy: need {recipe.energyCost}, have {currentEnergy:F0}");
                }
            }

            foreach (var req in recipe.requiredResources)
            {
                int have = inventory?.GetResourceCount(req.resourceType) ?? 0;
                if (have < req.amount)
                {
                    missing.Add($"{req.resourceType}: need {req.amount}, have {have}");
                }
            }

            if (missing.Count == 0) return "";
            return "Missing: " + string.Join(", ", missing);
        }

        public string GetRequirementsText(RuntimeRecipe recipe)
        {
            if (recipe == null) return "";

            var inventory = Inventory.InventorySystem.Instance;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (var req in recipe.requiredResources)
            {
                int have = inventory?.GetResourceCount(req.resourceType) ?? 0;
                string color = have >= req.amount ? "green" : "red";
                sb.AppendLine($"<color={color}>{req.resourceType}: {have}/{req.amount}</color>");
            }

            // Show energy cost if applicable
            if (recipe.energyCost > 0)
            {
                float currentEnergy = EnergyManager.Instance?.CurrentEnergy ?? 0;
                string color = currentEnergy >= recipe.energyCost ? "green" : "red";
                sb.AppendLine($"<color={color}>Energy: {currentEnergy:F0}/{recipe.energyCost}</color>");
            }

            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Runtime recipe class for in-memory recipes when ScriptableObjects aren't assigned
    /// </summary>
    [System.Serializable]
    public class RuntimeRecipe
    {
        public string recipeId;
        public string recipeName;
        public string description;
        public RecipeType recipeType;
        public List<ResourceRequirement> requiredResources = new List<ResourceRequirement>();
        public ResourceType? outputResourceType;
        public int outputAmount = 1;
        public float craftingTime = 2f;
        public int requiredWorkbenchTier = 1;
        public float energyCost = 0f;
        public bool isUnlocked = true;

        // Tool output (when outputResourceType is null)
        public bool isToolOutput = false;
        public string outputToolName;
        public int outputToolTier = 1;
        public float outputToolDigSpeed = 1f;
        public int outputToolDurability = 100;

        public string GetOutputText()
        {
            if (isToolOutput)
            {
                return $"{outputToolName} (Tier {outputToolTier})";
            }
            return $"{outputAmount}x {outputResourceType}";
        }
    }
}
