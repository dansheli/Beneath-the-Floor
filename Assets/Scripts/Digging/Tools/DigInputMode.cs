namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Input mode for digging tools.
    /// </summary>
    public enum DigInputMode
    {
        /// <summary>
        /// Single dig per click. Tool completes one dig action per mouse press.
        /// </summary>
        Click,

        /// <summary>
        /// Continuous digging while button is held. Tool digs repeatedly.
        /// </summary>
        Hold
    }
}
