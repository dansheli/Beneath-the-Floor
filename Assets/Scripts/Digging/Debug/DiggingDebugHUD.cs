using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Debug HUD for visualizing digging system state.
    /// Shows chunk count, dig stats, and performance info.
    /// </summary>
    public class DiggingDebugHUD : MonoBehaviour
    {
        // Set to true to enable debug HUD (F3)
        private const bool ENABLE_DEBUG = false;

        [Header("Display")]
        [SerializeField] private bool showHUD = false;  // Disabled by default
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        [Header("Position")]
        [SerializeField] private float xOffset = 10f;
        [SerializeField] private float yOffset = 10f;

        // References
        private ChunkManager _chunkManager;
        private DiggingSystem _diggingSystem;

        // Stats
        private int _digsThisSecond;
        private int _digsLastSecond;
        private float _lastSecondTime;
        private float _lastDigVolume;

        private void Start()
        {
            // Force disable HUD (override scene-serialized value)
            showHUD = false;

            _chunkManager = ChunkManager.Instance;
            _diggingSystem = DiggingSystem.Instance;

            if (_diggingSystem != null)
            {
                _diggingSystem.OnDigCompleted += OnDigCompleted;
            }
        }

        private void OnDestroy()
        {
            if (_diggingSystem != null)
            {
                _diggingSystem.OnDigCompleted -= OnDigCompleted;
            }
        }

        private void OnDigCompleted(DigResult result)
        {
            _digsThisSecond++;
            _lastDigVolume = result.VolumeRemoved;
        }

        private void Update()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG) return;

            // Toggle HUD
            if (Input.GetKeyDown(toggleKey))
            {
                showHUD = !showHUD;
            }

            // Update per-second stats
            if (Time.time - _lastSecondTime >= 1f)
            {
                _digsLastSecond = _digsThisSecond;
                _digsThisSecond = 0;
                _lastSecondTime = Time.time;
            }
#pragma warning restore CS0162
        }

        private void OnGUI()
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (!ENABLE_DEBUG || !showHUD)
                return;
#pragma warning restore CS0162

            // Set up style
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;

            // Build info string
            string info = "<b>Digging System Debug</b>\n";
            info += $"System: <color=lime>ACTIVE</color>\n\n";

            // Chunk Manager info
            if (_chunkManager != null)
            {
                info += $"<b>Chunks:</b> {_chunkManager.LoadedChunkCount}\n";
                info += $"Voxel Size: {_chunkManager.VoxelSize:F3}m\n";
                info += $"Chunk Size: {_chunkManager.ChunkSize}³\n\n";
            }
            else
            {
                info += "<color=red>ChunkManager not found!</color>\n\n";
            }

            // Digging System info
            if (_diggingSystem != null)
            {
                info += $"<b>Digging:</b>\n";
                info += $"Radius: {_diggingSystem.DigRadius:F2}m\n";
                info += $"Strength: {_diggingSystem.DigStrength:F2}\n";
                info += $"Active: {(_diggingSystem.IsDigging ? "<color=yellow>YES</color>" : "No")}\n";
                info += $"Digs/sec: {_digsLastSecond}\n";
                info += $"Last volume: {_lastDigVolume:F4}m³\n\n";

                if (_diggingSystem.LastHitValid)
                {
                    Vector3 hit = _diggingSystem.LastHitPoint;
                    info += $"Hit: ({hit.x:F1}, {hit.y:F1}, {hit.z:F1})\n";

                    if (_chunkManager != null)
                    {
                        float density = _chunkManager.GetDensityAt(hit);
                        info += $"Density: {density:F2}\n";
                    }
                }
                else
                {
                    info += "Hit: <color=gray>None</color>\n";
                }
            }
            else
            {
                info += "<color=red>DiggingSystem not found!</color>\n";
            }

            info += $"\n<size=10>Press {toggleKey} to toggle</size>";

            // Calculate box size
            GUIContent content = new GUIContent(info);
            Vector2 size = style.CalcSize(content);
            size.x = Mathf.Max(200, size.x + 20);
            size.y += 10;

            // Draw box
            Rect rect = new Rect(xOffset, yOffset, size.x, size.y);
            GUI.Box(rect, "");

            // Draw text with rich text
            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.richText = true;
            textStyle.padding = new RectOffset(10, 10, 5, 5);
            GUI.Label(rect, info, textStyle);
        }
    }
}
