using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeneathTheFloor.WorldRooms
{
    /// <summary>
    /// Defines which shaft side a wall or opening is on.
    /// </summary>
    public enum ShaftSide
    {
        North,  // +Z
        South,  // -Z
        East,   // +X
        West    // -X
    }

    /// <summary>
    /// Configuration for the entire modular shaft wall system.
    /// </summary>
    [CreateAssetMenu(fileName = "ModularShaftConfig", menuName = "Beneath The Floor/Shaft/Modular Shaft Config")]
    public class ModularShaftConfig : ScriptableObject
    {
        [Header("Shaft Dimensions")]
        [Tooltip("Half-width of the shaft (distance from center to wall)")]
        [Min(1f)]
        public float shaftHalfWidth = 6f;

        [Tooltip("Half-length of the shaft (distance from center to wall)")]
        [Min(1f)]
        public float shaftHalfLength = 6f;

        [Tooltip("Maximum depth of the shaft in meters")]
        [Min(10f)]
        public float maxDepth = 220f;

        [Header("Segment Library")]
        [Tooltip("Available segment configurations (different heights)")]
        public List<ShaftWallSegmentConfig> segmentConfigs = new List<ShaftWallSegmentConfig>();

        [Tooltip("Default segment config to use when no specific size is needed")]
        public ShaftWallSegmentConfig defaultSegmentConfig;

        [Header("Side Configuration")]
        [Tooltip("Which sides of the shaft can have openings for rooms")]
        public ShaftSideConfig northSide = new ShaftSideConfig { enabled = true };
        public ShaftSideConfig southSide = new ShaftSideConfig { enabled = true };
        public ShaftSideConfig eastSide = new ShaftSideConfig { enabled = true };
        public ShaftSideConfig westSide = new ShaftSideConfig { enabled = true };

        [Header("Visual Settings")]
        [Tooltip("Material for shaft walls (if not set in prefabs)")]
        public Material wallMaterial;

        // Cache for fast lookup by height
        private Dictionary<float, ShaftWallSegmentConfig> _configByHeight;
        private bool _cacheBuilt = false;

        /// <summary>
        /// Gets the segment config closest to the requested height.
        /// </summary>
        public ShaftWallSegmentConfig GetSegmentConfigForHeight(float targetHeight)
        {
            if (segmentConfigs == null || segmentConfigs.Count == 0)
                return defaultSegmentConfig;

            ShaftWallSegmentConfig bestMatch = null;
            float bestDiff = float.MaxValue;

            foreach (var config in segmentConfigs)
            {
                if (config == null) continue;

                float diff = Mathf.Abs(config.height - targetHeight);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestMatch = config;
                }
            }

            return bestMatch ?? defaultSegmentConfig;
        }

        /// <summary>
        /// Gets the segment config with exact height match, or null.
        /// </summary>
        public ShaftWallSegmentConfig GetExactSegmentConfig(float height)
        {
            if (!_cacheBuilt) BuildCache();

            _configByHeight.TryGetValue(height, out var config);
            return config;
        }

        /// <summary>
        /// Gets all available segment heights.
        /// </summary>
        public List<float> GetAvailableHeights()
        {
            var heights = new List<float>();
            foreach (var config in segmentConfigs)
            {
                if (config != null)
                    heights.Add(config.height);
            }
            heights.Sort();
            return heights;
        }

        /// <summary>
        /// Gets the side configuration for a specific side.
        /// </summary>
        public ShaftSideConfig GetSideConfig(ShaftSide side)
        {
            return side switch
            {
                ShaftSide.North => northSide,
                ShaftSide.South => southSide,
                ShaftSide.East => eastSide,
                ShaftSide.West => westSide,
                _ => northSide
            };
        }

        /// <summary>
        /// Checks if a side can have openings.
        /// </summary>
        public bool IsSideEnabled(ShaftSide side)
        {
            return GetSideConfig(side).enabled;
        }

        /// <summary>
        /// Gets the world direction vector for a shaft side.
        /// </summary>
        public static Vector3 GetSideDirection(ShaftSide side)
        {
            return side switch
            {
                ShaftSide.North => Vector3.forward,
                ShaftSide.South => Vector3.back,
                ShaftSide.East => Vector3.right,
                ShaftSide.West => Vector3.left,
                _ => Vector3.forward
            };
        }

        /// <summary>
        /// Gets the rotation for a wall segment on a specific side.
        /// </summary>
        public static Quaternion GetSideRotation(ShaftSide side)
        {
            return side switch
            {
                ShaftSide.North => Quaternion.Euler(0, 0, 0),
                ShaftSide.South => Quaternion.Euler(0, 180, 0),
                ShaftSide.East => Quaternion.Euler(0, 90, 0),
                ShaftSide.West => Quaternion.Euler(0, -90, 0),
                _ => Quaternion.identity
            };
        }

        private void BuildCache()
        {
            _configByHeight = new Dictionary<float, ShaftWallSegmentConfig>();
            foreach (var config in segmentConfigs)
            {
                if (config != null && !_configByHeight.ContainsKey(config.height))
                {
                    _configByHeight[config.height] = config;
                }
            }
            _cacheBuilt = true;
        }

        private void OnEnable()
        {
            _cacheBuilt = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cacheBuilt = false;

            if (defaultSegmentConfig == null && segmentConfigs.Count > 0)
            {
                defaultSegmentConfig = segmentConfigs[0];
            }
        }
#endif
    }

    /// <summary>
    /// Configuration for a single side of the shaft.
    /// </summary>
    [Serializable]
    public class ShaftSideConfig
    {
        [Tooltip("Whether this side can have room openings")]
        public bool enabled = true;

        [Tooltip("Optional override segment config for this side")]
        public ShaftWallSegmentConfig segmentOverride;

        [Tooltip("Specific depths where openings are pre-placed (empty = dynamic only)")]
        public List<float> preplacedOpeningDepths = new List<float>();
    }
}
