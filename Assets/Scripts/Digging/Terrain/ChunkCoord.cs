using UnityEngine;
using System;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Represents a chunk coordinate in chunk-space (not world-space).
    /// Each chunk coordinate maps to a 16x16x16 voxel region.
    /// </summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ChunkCoord(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Create a ChunkCoord from a Vector3Int.
        /// </summary>
        public ChunkCoord(Vector3Int v) : this(v.x, v.y, v.z) { }

        /// <summary>
        /// Convert to Vector3Int for use with Unity APIs.
        /// </summary>
        public Vector3Int ToVector3Int() => new Vector3Int(X, Y, Z);

        /// <summary>
        /// Get neighboring chunk coordinate in a direction.
        /// </summary>
        public ChunkCoord GetNeighbor(int dx, int dy, int dz)
        {
            return new ChunkCoord(X + dx, Y + dy, Z + dz);
        }

        /// <summary>
        /// Get all 6 face-adjacent neighbors.
        /// </summary>
        public ChunkCoord[] GetFaceNeighbors()
        {
            return new ChunkCoord[]
            {
                new ChunkCoord(X + 1, Y, Z),
                new ChunkCoord(X - 1, Y, Z),
                new ChunkCoord(X, Y + 1, Z),
                new ChunkCoord(X, Y - 1, Z),
                new ChunkCoord(X, Y, Z + 1),
                new ChunkCoord(X, Y, Z - 1)
            };
        }

        /// <summary>
        /// Get all 26 neighbors (face + edge + corner adjacent).
        /// Used when checking for boundary mesh updates.
        /// </summary>
        public ChunkCoord[] GetAllNeighbors()
        {
            var neighbors = new ChunkCoord[26];
            int index = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0)
                            continue;
                        neighbors[index++] = new ChunkCoord(X + dx, Y + dy, Z + dz);
                    }
                }
            }
            return neighbors;
        }

        // IEquatable implementation for efficient dictionary/hashset usage
        public bool Equals(ChunkCoord other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            // High-quality hash combining for dictionary performance
            unchecked
            {
                int hash = X * 73856093;
                hash ^= Y * 19349663;
                hash ^= Z * 83492791;
                return hash;
            }
        }

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);

        public override string ToString() => $"Chunk({X}, {Y}, {Z})";
    }
}
