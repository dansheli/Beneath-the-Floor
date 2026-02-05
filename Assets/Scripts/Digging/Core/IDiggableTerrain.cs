using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Interface for terrain systems that support digging operations.
    /// Allows DiggingSystem to work with different terrain implementations.
    /// This abstraction enables future swapping of terrain backends.
    /// </summary>
    public interface IDiggableTerrain
    {
        /// <summary>
        /// Attempt to dig at the specified world position.
        /// </summary>
        /// <param name="worldPosition">Center of the dig sphere in world space.</param>
        /// <param name="radius">Radius of the dig sphere in meters.</param>
        /// <param name="strength">Dig strength (0-1, how much density to remove).</param>
        /// <returns>True if any terrain was modified.</returns>
        bool TryDig(Vector3 worldPosition, float radius, float strength);

        /// <summary>
        /// Get the density value at a world position.
        /// Returns 1.0 for solid, 0.0 for air, interpolated values at surface.
        /// </summary>
        /// <param name="worldPosition">Position to sample in world space.</param>
        /// <returns>Density value (0.0 = empty, 1.0 = solid).</returns>
        float GetDensityAt(Vector3 worldPosition);

        /// <summary>
        /// Check if a world position is within the terrain bounds.
        /// </summary>
        /// <param name="worldPosition">Position to check.</param>
        /// <returns>True if position is within terrain bounds.</returns>
        bool IsWithinBounds(Vector3 worldPosition);

        /// <summary>
        /// Get the bounds of the entire terrain in world space.
        /// </summary>
        Bounds GetWorldBounds();

        /// <summary>
        /// Force regeneration of all dirty chunks.
        /// Call this after batch modifications if immediate visual update is needed.
        /// </summary>
        void FlushDirtyChunks();
    }
}
