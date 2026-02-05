using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Represents a single dig operation.
    /// Immutable struct containing all data needed to execute a dig.
    /// </summary>
    public readonly struct DigOperation
    {
        /// <summary>
        /// World-space center of the dig sphere.
        /// </summary>
        public readonly Vector3 WorldPosition;

        /// <summary>
        /// Radius of the dig sphere in meters.
        /// </summary>
        public readonly float Radius;

        /// <summary>
        /// Strength of the dig (0-1). Higher values remove more density per frame.
        /// </summary>
        public readonly float Strength;

        /// <summary>
        /// Timestamp when this operation was created.
        /// </summary>
        public readonly float Timestamp;

        public DigOperation(Vector3 worldPosition, float radius, float strength)
        {
            WorldPosition = worldPosition;
            Radius = Mathf.Max(0.01f, radius);
            Strength = Mathf.Clamp01(strength);
            Timestamp = Time.time;
        }

        /// <summary>
        /// Create a dig operation with default strength.
        /// </summary>
        public static DigOperation Create(Vector3 position, float radius)
        {
            return new DigOperation(position, radius, 1.0f);
        }

        /// <summary>
        /// Create a dig operation with custom strength.
        /// </summary>
        public static DigOperation Create(Vector3 position, float radius, float strength)
        {
            return new DigOperation(position, radius, strength);
        }

        public override string ToString()
        {
            return $"DigOp(pos={WorldPosition}, r={Radius:F2}, str={Strength:F2})";
        }
    }

    /// <summary>
    /// Result of a dig operation.
    /// </summary>
    public readonly struct DigResult
    {
        /// <summary>
        /// Whether any terrain was modified.
        /// </summary>
        public readonly bool Success;

        /// <summary>
        /// Number of chunks that were modified.
        /// </summary>
        public readonly int ChunksModified;

        /// <summary>
        /// Approximate volume of terrain removed in cubic meters.
        /// </summary>
        public readonly float VolumeRemoved;

        /// <summary>
        /// The original dig operation.
        /// </summary>
        public readonly DigOperation Operation;

        public DigResult(bool success, int chunksModified, float volumeRemoved, DigOperation operation)
        {
            Success = success;
            ChunksModified = chunksModified;
            VolumeRemoved = volumeRemoved;
            Operation = operation;
        }

        public static DigResult Failed(DigOperation operation)
        {
            return new DigResult(false, 0, 0f, operation);
        }

        public override string ToString()
        {
            return $"DigResult(success={Success}, chunks={ChunksModified}, vol={VolumeRemoved:F3})";
        }
    }
}
