namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Legacy enum for tool tier values.
    /// In the unified V3 system, tool tiers are managed via UpgradeStation static multipliers.
    /// This enum exists for compatibility with code that uses the old tier system.
    /// </summary>
    public enum ToolTier
    {
        /// <summary>Tier 0 - Basic wooden digger</summary>
        WoodenDigger = 0,

        /// <summary>Tier 1 - Basic pickaxe</summary>
        BasicPickaxe = 1,

        /// <summary>Tier 2 - Iron pickaxe</summary>
        IronPickaxe = 2,

        /// <summary>Tier 3 - Steel pickaxe</summary>
        SteelPickaxe = 3,

        /// <summary>Tier 4 - Sonic Pulser</summary>
        SonicPulser = 4
    }
}
