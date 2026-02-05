// ============================================================================
// DEPRECATED: This file has been moved to Legacy/Workbench/ and is no longer in use.
// The Workbench system has been replaced by UpgradeStation as the single upgrade point.
// This file is kept for reference only and should NOT be compiled.
// If you need to reference this code, copy what you need to your new implementation.
// ============================================================================
#if false // Disabled - remove this line only if you need to compile for reference

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using BeneathTheFloor.Crafting;
using BeneathTheFloor.UI;

namespace BeneathTheFloor.Machines
{
    /// <summary>
    /// WorkbenchUI manages the workbench crafting interface.
    /// DEPRECATED: This UI is no longer used. UpgradeStation is now the single upgrade/crafting point.
    /// </summary>
    public class WorkbenchUI : MonoBehaviour
    {
        [Header("Static UI References")]
        [SerializeField] private GameObject workbenchPanel;
        [SerializeField] private Transform recipeListContent;
        [SerializeField] private GameObject recipeButtonPrefab;

        [Header("Recipe Details")]
        [SerializeField] private GameObject recipeDetailsPanel;
        [SerializeField] private Image recipeIcon;
        [SerializeField] private TextMeshProUGUI recipeNameText;
        [SerializeField] private TextMeshProUGUI recipeDescriptionText;
        [SerializeField] private TextMeshProUGUI requirementsText;
        [SerializeField] private TextMeshProUGUI outputText;
        [SerializeField] private Button craftButton;
        [SerializeField] private TextMeshProUGUI craftButtonText;

        [Header("Progress")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        [Header("Status Message")]
        [SerializeField] private TextMeshProUGUI statusText;

        public static WorkbenchUI Instance { get; private set; }

        private Workbench currentWorkbench;
        private CraftingRecipe selectedRecipe;
        private RuntimeRecipe selectedRuntimeRecipe;
        private List<GameObject> recipeButtons = new List<GameObject>();
        private bool isCrafting = false;

        // NOTE: Full implementation removed for brevity.
        // This file is kept for reference only.
        // See the original file in version control if you need the full implementation.

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ShowUI(Workbench workbench)
        {
            Debug.LogWarning("[WorkbenchUI] DEPRECATED: Workbench system has been replaced by UpgradeStation");
        }

        public void HideUI()
        {
            if (workbenchPanel != null)
            {
                workbenchPanel.SetActive(false);
            }
        }
    }
}

#endif // End of disabled code block
