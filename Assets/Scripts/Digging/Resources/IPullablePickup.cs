namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Interface for pickups that can be pulled by the magnet system.
    /// Implemented by ResourcePickup (digging drops) and NodePickup (fixed node drops).
    /// </summary>
    public interface IPullablePickup
    {
        /// <summary>
        /// Amount of resource this pickup contains.
        /// </summary>
        int Amount { get; }

        /// <summary>
        /// Check if this pickup CAN be collected (inventory has room).
        /// Used to prevent pulling when inventory is full.
        /// </summary>
        bool CanPickup();

        /// <summary>
        /// Attempt to collect this pickup into the player's inventory.
        /// Returns true if successful, false if inventory is full.
        /// </summary>
        bool TryPickup();

        /// <summary>
        /// Get display name for UI (e.g., "Iron Ore" or "Rare Node Fragment").
        /// </summary>
        string GetDisplayName();

        /// <summary>
        /// Set visual highlight state (glow effect when aimed at).
        /// </summary>
        void SetHighlighted(bool highlighted);
    }
}
