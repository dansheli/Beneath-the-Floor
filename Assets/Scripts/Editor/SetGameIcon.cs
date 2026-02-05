using UnityEngine;
using UnityEditor;

namespace BeneathTheFloor.Editor
{
    public class SetGameIcon
    {
        [MenuItem("Tools/Set Game Icon")]
        private static void SetIcon()
        {
            string iconPath = "Assets/Art/UI/GameIcon/Game icon.jpg";
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

            if (icon == null)
            {
                Debug.LogError($"[SetGameIcon] Could not load icon from {iconPath}");
                return;
            }

            // Set icons for Standalone platform
            BuildTargetGroup targetGroup = BuildTargetGroup.Standalone;
            int[] iconSizes = PlayerSettings.GetIconSizesForTargetGroup(targetGroup);
            if (iconSizes.Length > 0)
            {
                Texture2D[] icons = new Texture2D[iconSizes.Length];
                for (int i = 0; i < iconSizes.Length; i++)
                    icons[i] = icon;
                PlayerSettings.SetIconsForTargetGroup(targetGroup, icons);
            }

            // Set default icons
            int[] defaultIconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Unknown);
            if (defaultIconSizes.Length > 0)
            {
                Texture2D[] defaultIcons = new Texture2D[defaultIconSizes.Length];
                for (int i = 0; i < defaultIconSizes.Length; i++)
                    defaultIcons[i] = icon;
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, defaultIcons);
            }

            Debug.Log("[SetGameIcon] Game icon set successfully!");
            AssetDatabase.SaveAssets();
        }

        [InitializeOnLoadMethod]
        private static void AutoSetIcon()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("GameIconSet_v2", false))
                {
                    SetIcon();
                    SessionState.SetBool("GameIconSet_v2", true);
                }
            };
        }
    }
}
