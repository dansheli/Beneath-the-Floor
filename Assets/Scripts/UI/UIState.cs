using UnityEngine;

namespace BeneathTheFloor.UI
{
    /// <summary>
    /// Central UI state manager to track when UIs are open.
    /// Used by gameplay systems (DiggingSystem, InteractionSystem) to block input.
    /// </summary>
    public static class UIState
    {
        private static bool _isMachineUIOpen = false;
        private static bool _isInventoryUIOpen = false;
        private static bool _isPauseMenuOpen = false;
        private static bool _isNoteUIOpen = false;
        private static int _escapeConsumedFrame = -1;

        /// <summary>
        /// True when any machine UI (Workbench, Refinery, UpgradeStation) is open.
        /// </summary>
        public static bool IsMachineUIOpen
        {
            get => _isMachineUIOpen;
            set
            {
                if (_isMachineUIOpen != value)
                {
                    _isMachineUIOpen = value;
                    UpdateCursorState();
                }
            }
        }

        /// <summary>
        /// True when the inventory UI is open.
        /// </summary>
        public static bool IsInventoryUIOpen
        {
            get => _isInventoryUIOpen;
            set
            {
                if (_isInventoryUIOpen != value)
                {
                    _isInventoryUIOpen = value;
                    UpdateCursorState();
                }
            }
        }

        /// <summary>
        /// True when the pause menu is open.
        /// </summary>
        public static bool IsPauseMenuOpen
        {
            get => _isPauseMenuOpen;
            set
            {
                if (_isPauseMenuOpen != value)
                {
                    _isPauseMenuOpen = value;
                    UpdateCursorState();
                }
            }
        }

        /// <summary>
        /// True when a readable note UI is open.
        /// </summary>
        public static bool IsNoteUIOpen
        {
            get => _isNoteUIOpen;
            set
            {
                if (_isNoteUIOpen != value)
                {
                    _isNoteUIOpen = value;
                    UpdateCursorState();
                }
            }
        }

        /// <summary>
        /// True if any UI that blocks gameplay is open.
        /// </summary>
        public static bool IsAnyUIOpen => _isMachineUIOpen || _isInventoryUIOpen || _isPauseMenuOpen || _isNoteUIOpen;

        /// <summary>
        /// True if Escape was consumed by another UI this frame (prevents pause menu from opening).
        /// Automatically resets each frame by checking the frame number.
        /// </summary>
        public static bool WasEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;

        /// <summary>
        /// Call this when a UI consumes the Escape key to prevent pause menu from also triggering.
        /// </summary>
        public static void ConsumeEscape()
        {
            _escapeConsumedFrame = Time.frameCount;
        }

        /// <summary>
        /// Helper method to check if gameplay input should be blocked.
        /// </summary>
        public static bool ShouldBlockGameplayInput()
        {
            return IsAnyUIOpen;
        }

        /// <summary>
        /// Update cursor visibility and lock state based on current UI state.
        /// </summary>
        private static void UpdateCursorState()
        {
            if (IsAnyUIOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
