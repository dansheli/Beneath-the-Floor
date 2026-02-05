using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Simple implementation of IDigToolProvider for testing and basic gameplay.
    ///
    /// USAGE:
    /// 1. Attach to Player or any GameObject
    /// 2. Toggle hasToolEquipped to simulate equipping/unequipping
    /// 3. DiggingSystem will automatically find this if on same object
    ///
    /// FUTURE: Replace with proper inventory/tool system integration.
    /// </summary>
    public class SimpleDigToolProvider : MonoBehaviour, IDigToolProvider
    {
        [Header("Tool State")]
        [Tooltip("Simulate having a dig tool equipped.")]
        [SerializeField] private bool hasToolEquipped = true;

        [Header("Tool Properties")]
        [SerializeField] private string toolId = "basic_pickaxe";
        [SerializeField] private string toolDisplayName = "Basic Pickaxe";
        [SerializeField] private float radiusMultiplier = 1.0f;
        [SerializeField] private float strengthMultiplier = 1.0f;
        [SerializeField] private float maxHardness = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool logDigEvents = true;

        // Track dig count for testing
        private int _digCount = 0;

        private void Start()
        {
            // Auto-register with DiggingSystem if on same object or if it exists
            var diggingSystem = GetComponent<DiggingSystem>();
            if (diggingSystem == null)
            {
                diggingSystem = DiggingSystem.Instance;
            }

            if (diggingSystem != null)
            {
                diggingSystem.SetToolProvider(this);
            }
        }

        #region IDigToolProvider Implementation

        /// <summary>
        /// Returns true if tool is equipped.
        /// </summary>
        public bool HasValidDigTool()
        {
            return hasToolEquipped;
        }

        /// <summary>
        /// Get current tool info.
        /// </summary>
        public DigToolInfo? GetCurrentDigTool()
        {
            if (!hasToolEquipped)
            {
                return null;
            }

            return new DigToolInfo
            {
                ToolId = toolId,
                DisplayName = toolDisplayName,
                RadiusMultiplier = radiusMultiplier,
                StrengthMultiplier = strengthMultiplier,
                MaxHardness = maxHardness
            };
        }

        /// <summary>
        /// Called when dig completes.
        /// </summary>
        public void OnDigPerformed(DigResult result)
        {
            _digCount++;

            if (logDigEvents)
            {
                Debug.Log($"[SimpleDigToolProvider] Dig #{_digCount} with {toolDisplayName}: removed {result.VolumeRemoved:F4}m³");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Equip the dig tool.
        /// </summary>
        public void EquipTool()
        {
            hasToolEquipped = true;
            Debug.Log($"[SimpleDigToolProvider] Equipped: {toolDisplayName}");
        }

        /// <summary>
        /// Unequip the dig tool.
        /// </summary>
        public void UnequipTool()
        {
            hasToolEquipped = false;
            Debug.Log("[SimpleDigToolProvider] Tool unequipped");
        }

        /// <summary>
        /// Toggle tool equipped state.
        /// </summary>
        public void ToggleTool()
        {
            if (hasToolEquipped)
                UnequipTool();
            else
                EquipTool();
        }

        /// <summary>
        /// Set tool properties at runtime.
        /// </summary>
        public void SetToolProperties(string id, string displayName, float radius, float strength, float hardness)
        {
            toolId = id;
            toolDisplayName = displayName;
            radiusMultiplier = radius;
            strengthMultiplier = strength;
            maxHardness = hardness;
        }

        /// <summary>
        /// Get total digs performed with this tool.
        /// </summary>
        public int GetDigCount()
        {
            return _digCount;
        }

        #endregion
    }
}
