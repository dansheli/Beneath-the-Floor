using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Detects floating terrain after digging and removes it.
    /// Uses bounded search - pieces not touching the scan boundary and without support are floating.
    /// </summary>
    public class FloatingTerrainDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("Minimum voxels for a piece to be removed.")]
        [SerializeField] private int minVoxelsForChunk = 1;

        [Tooltip("Maximum voxels for a floating piece.")]
        [SerializeField] private int maxVoxelsForChunk = 50;

        [Header("Scan Settings")]
        [Tooltip("How far around the dig to scan (in voxels).")]
        [SerializeField] private int scanRadius = 5;

        [Header("Feature Toggle")]
        [Tooltip("Disable until a better detection method is implemented.")]
        [SerializeField] private bool enableDetection = false;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private ChunkManager _chunkManager;

        private static readonly Vector3Int[] Directions = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        private const float SURFACE_THRESHOLD = 0.5f;

        public static FloatingTerrainDetector Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (enableDebugLogs)
                Debug.Log("[FloatingDetector] Started");

            _chunkManager = ChunkManager.Instance ?? FindObjectOfType<ChunkManager>();

            if (_chunkManager == null)
            {
                Debug.LogError("[FloatingDetector] No ChunkManager found!");
                enabled = false;
                return;
            }

            _chunkManager.OnDigCompleted += OnDigCompleted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_chunkManager != null)
                _chunkManager.OnDigCompleted -= OnDigCompleted;
        }

        private void OnDigCompleted(DigResult result)
        {
            if (!enableDetection) return;
            if (!result.Success) return;
            CheckForFloatingTerrain(result.Operation.WorldPosition);
        }

        private void CheckForFloatingTerrain(Vector3 digCenter)
        {
            float voxelSize = _chunkManager.VoxelSize;
            Vector3Int centerVoxel = WorldToVoxel(digCenter, voxelSize);

            // Step 1: Collect all solid surface voxels in scan area (bounded)
            Dictionary<Vector3Int, bool> solidVoxels = new Dictionary<Vector3Int, bool>();

            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                for (int dy = -scanRadius; dy <= scanRadius; dy++)
                {
                    for (int dz = -scanRadius; dz <= scanRadius; dz++)
                    {
                        Vector3Int voxel = centerVoxel + new Vector3Int(dx, dy, dz);
                        Vector3 worldPos = VoxelToWorld(voxel, voxelSize);

                        if (_chunkManager.GetDensityAt(worldPos) >= SURFACE_THRESHOLD)
                        {
                            // Check if surface voxel (has air neighbor)
                            bool isSurface = false;
                            foreach (var dir in Directions)
                            {
                                Vector3 nWorld = VoxelToWorld(voxel + dir, voxelSize);
                                if (_chunkManager.GetDensityAt(nWorld) < SURFACE_THRESHOLD)
                                {
                                    isSurface = true;
                                    break;
                                }
                            }

                            if (isSurface)
                            {
                                // Mark if at boundary
                                bool atBoundary = Mathf.Abs(dx) == scanRadius ||
                                                  Mathf.Abs(dy) == scanRadius ||
                                                  Mathf.Abs(dz) == scanRadius;
                                solidVoxels[voxel] = atBoundary;
                            }
                        }
                    }
                }
            }

            if (solidVoxels.Count == 0) return;

            // Step 2: Group into components (only within scan area)
            var components = FindBoundedComponents(solidVoxels, centerVoxel);

            if (enableDebugLogs)
                Debug.Log($"[FloatingDetector] Found {components.Count} bounded components");

            // Step 3: Remove floating components
            foreach (var comp in components)
            {
                // If touches boundary = connected to main terrain = KEEP
                if (comp.TouchesBoundary)
                    continue;

                // Small isolated piece within scan area = likely floating
                if (comp.Voxels.Count >= minVoxelsForChunk && comp.Voxels.Count <= maxVoxelsForChunk)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[FloatingDetector] Removing isolated piece ({comp.Voxels.Count} voxels)");
                    RemoveVoxels(comp.Voxels, voxelSize);
                }
                else if (comp.Voxels.Count < minVoxelsForChunk)
                {
                    // Tiny piece - remove anyway
                    RemoveVoxels(comp.Voxels, voxelSize);
                }
            }
        }

        private struct ComponentInfo
        {
            public HashSet<Vector3Int> Voxels;
            public bool TouchesBoundary;
        }

        private List<ComponentInfo> FindBoundedComponents(Dictionary<Vector3Int, bool> voxels, Vector3Int center)
        {
            List<ComponentInfo> components = new List<ComponentInfo>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            foreach (var kvp in voxels)
            {
                Vector3Int startVoxel = kvp.Key;
                if (visited.Contains(startVoxel)) continue;

                // Flood fill within our voxel set only
                HashSet<Vector3Int> component = new HashSet<Vector3Int>();
                Queue<Vector3Int> queue = new Queue<Vector3Int>();
                bool touchesBoundary = false;

                queue.Enqueue(startVoxel);
                visited.Add(startVoxel);

                while (queue.Count > 0)
                {
                    Vector3Int current = queue.Dequeue();
                    component.Add(current);

                    // Check if this voxel is at boundary
                    if (voxels.TryGetValue(current, out bool atBoundary) && atBoundary)
                        touchesBoundary = true;

                    // Only expand to neighbors within our voxel set
                    foreach (var dir in Directions)
                    {
                        Vector3Int neighbor = current + dir;
                        if (visited.Contains(neighbor)) continue;

                        if (voxels.ContainsKey(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                components.Add(new ComponentInfo
                {
                    Voxels = component,
                    TouchesBoundary = touchesBoundary
                });
            }

            return components;
        }

        private void RemoveVoxels(HashSet<Vector3Int> voxels, float voxelSize)
        {
            foreach (var voxel in voxels)
            {
                Vector3 worldPos = VoxelToWorld(voxel, voxelSize);
                _chunkManager.SetDensityDirect(worldPos, 0f);
            }
            _chunkManager.FlushDirtyChunks();
        }

        private Vector3Int WorldToVoxel(Vector3 worldPos, float voxelSize)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / voxelSize),
                Mathf.FloorToInt(worldPos.y / voxelSize),
                Mathf.FloorToInt(worldPos.z / voxelSize)
            );
        }

        private Vector3 VoxelToWorld(Vector3Int voxel, float voxelSize)
        {
            return new Vector3(
                (voxel.x + 0.5f) * voxelSize,
                (voxel.y + 0.5f) * voxelSize,
                (voxel.z + 0.5f) * voxelSize
            );
        }
    }
}
