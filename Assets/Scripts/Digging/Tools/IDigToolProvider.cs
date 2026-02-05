using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Interface for providing dig tool information to the digging system.
    /// Implement this interface on your tool/inventory system to gate digging.
    ///
    /// USAGE:
    /// - Assign an IDigToolProvider to DiggingSystem via SetToolProvider()
    /// - DiggingSystem will query HasValidDigTool() before every dig attempt
    /// - If no tool provider is set, digging is BLOCKED by default
    ///
    /// FUTURE INTEGRATION:
    /// - Tool type can affect dig radius/strength
    /// - Tool durability can be reduced on successful digs
    /// - Different tools can dig different terrain layers
    /// </summary>
    public interface IDigToolProvider
    {
        /// <summary>
        /// Returns true if the player currently has a valid digging tool equipped.
        /// </summary>
        bool HasValidDigTool();

        /// <summary>
        /// Get the currently equipped dig tool's parameters.
        /// Returns null if no valid tool is equipped.
        /// </summary>
        DigToolInfo? GetCurrentDigTool();

        /// <summary>
        /// Called when a dig operation completes successfully.
        /// Use this to reduce tool durability, trigger animations, etc.
        /// </summary>
        void OnDigPerformed(DigResult result);
    }

    /// <summary>
    /// Information about a dig tool's capabilities.
    /// </summary>
    public struct DigToolInfo
    {
        /// <summary>
        /// Unique identifier for the tool (e.g., "pickaxe_iron", "shovel_basic").
        /// </summary>
        public string ToolId;

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Dig radius multiplier (1.0 = default).
        /// </summary>
        public float RadiusMultiplier;

        /// <summary>
        /// Dig strength multiplier (1.0 = default).
        /// </summary>
        public float StrengthMultiplier;

        /// <summary>
        /// Maximum terrain hardness this tool can dig.
        /// Higher = can dig harder terrain.
        /// </summary>
        public float MaxHardness;

        /// <summary>
        /// Create a basic dig tool info with default multipliers.
        /// </summary>
        public static DigToolInfo Default(string toolId, string displayName)
        {
            return new DigToolInfo
            {
                ToolId = toolId,
                DisplayName = displayName,
                RadiusMultiplier = 1.0f,
                StrengthMultiplier = 1.0f,
                MaxHardness = 1.0f
            };
        }
    }
}
