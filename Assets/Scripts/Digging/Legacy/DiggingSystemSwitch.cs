using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Legacy compatibility stub for DiggingSystemSwitch.
    /// Since the system has been unified to V3, this always returns true for V3.
    ///
    /// This exists to maintain compatibility with code that checked which digging version was active.
    /// New code should not need to check this - the unified DiggingSystem is the only version.
    /// </summary>
    public static class DiggingSystemSwitch
    {
        /// <summary>
        /// Returns true if V3 digging system should be used.
        /// Always returns true since the system has been unified to V3.
        /// </summary>
        public static bool ShouldUseV3()
        {
            return true;
        }

        /// <summary>
        /// Returns true if V2 digging system should be used.
        /// Always returns false since the system has been unified to V3.
        /// </summary>
        public static bool ShouldUseV2()
        {
            return false;
        }
    }
}
