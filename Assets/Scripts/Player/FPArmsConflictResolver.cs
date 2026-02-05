using UnityEngine;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Resolves conflicts with the FPS Hands asset's built-in scripts.
    /// Add this component to any scene that uses the FPS hands asset.
    /// Runs very early to disable conflicting scripts before they can cause errors.
    /// </summary>
    [DefaultExecutionOrder(-10000)] // Run extremely early
    public class FPArmsConflictResolver : MonoBehaviour
    {
        private static bool hasRun = false;

        private void Awake()
        {
            if (hasRun) return;
            hasRun = true;

            DisableConflictingScripts();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            hasRun = false;
        }

        private void DisableConflictingScripts()
        {
            // Find all MonoBehaviours in scene
            var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);

            string[] scriptsToDisable = {
                "fps_controller",
                "fps_hands_anim_script",
                "mouse_look"
            };

            int disabled = 0;

            foreach (var behaviour in allBehaviours)
            {
                if (behaviour == null) continue;

                string typeName = behaviour.GetType().Name;

                foreach (string scriptName in scriptsToDisable)
                {
                    if (typeName.Equals(scriptName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        behaviour.enabled = false;
                        disabled++;
                        break;
                    }
                }
            }

            if (disabled > 0)
            {
                Debug.Log($"[FPArmsConflictResolver] Disabled {disabled} conflicting FPS hands asset scripts");
            }
        }
    }
}
