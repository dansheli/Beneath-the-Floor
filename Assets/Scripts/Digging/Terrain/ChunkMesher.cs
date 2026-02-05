using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Marching Cubes mesh generator for individual chunks.
    /// Handles seamless boundary stitching by sampling from neighboring chunks.
    ///
    /// FUTURE OPTIMIZATION: This class is structured to allow easy conversion to
    /// Unity Job System + Burst Compiler. The core algorithm is stateless and
    /// operates on arrays that can be converted to NativeArrays.
    /// </summary>
    public static class ChunkMesher
    {
        // Configuration
        public const int CHUNK_SIZE = 16;
        public const float ISO_LEVEL = 0.5f;

        // Vertex welding precision (1mm)
        private const float WELD_EPSILON = 0.001f;

        /// <summary>
        /// Delegate for sampling density from world/chunk space.
        /// Allows the mesher to sample from neighboring chunks transparently.
        /// </summary>
        public delegate float DensitySampler(int localX, int localY, int localZ);

        /// <summary>
        /// Generate mesh data for a chunk using Marching Cubes.
        /// Uses vertex welding for smooth normals across cube boundaries.
        /// </summary>
        /// <param name="sampler">Function to sample density. Must handle coordinates 0-16 inclusive (17 samples per axis).</param>
        /// <param name="voxelSize">Size of each voxel in world units (meters).</param>
        /// <param name="vertices">Output list of vertices.</param>
        /// <param name="triangles">Output list of triangle indices.</param>
        /// <param name="normals">Output list of normals.</param>
        public static void GenerateMesh(
            DensitySampler sampler,
            float voxelSize,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> normals)
        {
            // Clear output lists
            vertices.Clear();
            triangles.Clear();
            normals.Clear();

            // Vertex welding dictionary: hash -> vertex index
            var vertexMap = new Dictionary<int, int>(4096);

            // Edge vertex cache for current slice (reduces dictionary lookups)
            // Format: edgeVertices[y, z, edgeIndex] = vertex index or -1
            // We process X slices, so cache Y-Z planes
            var edgeVertices = new int[CHUNK_SIZE + 1, CHUNK_SIZE + 1, 12];

            // Initialize edge cache to -1 (no vertex)
            for (int y = 0; y <= CHUNK_SIZE; y++)
            {
                for (int z = 0; z <= CHUNK_SIZE; z++)
                {
                    for (int e = 0; e < 12; e++)
                    {
                        edgeVertices[y, z, e] = -1;
                    }
                }
            }

            // Temporary arrays for cube processing
            float[] cubeCorners = new float[8];
            Vector3[] edgePositions = new Vector3[12];
            int[] edgeIndices = new int[12];

            // Process each cube in the chunk
            // Note: We process CHUNK_SIZE cubes per axis, but sample CHUNK_SIZE+1 densities
            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    for (int z = 0; z < CHUNK_SIZE; z++)
                    {
                        ProcessCube(
                            x, y, z,
                            sampler,
                            voxelSize,
                            vertices,
                            triangles,
                            vertexMap,
                            cubeCorners,
                            edgePositions,
                            edgeIndices);
                    }
                }
            }

            // Calculate smooth normals for front faces
            CalculateNormals(vertices, triangles, normals);

            // NOTE: Double-sided rendering is handled by shader (Cull Off + SV_IsFrontFace)
            // Do NOT duplicate triangles here - that breaks normal calculations for backfaces
        }

        /// <summary>
        /// Process a single cube using Marching Cubes algorithm.
        /// </summary>
        private static void ProcessCube(
            int x, int y, int z,
            DensitySampler sampler,
            float voxelSize,
            List<Vector3> vertices,
            List<int> triangles,
            Dictionary<int, int> vertexMap,
            float[] cubeCorners,
            Vector3[] edgePositions,
            int[] edgeIndices)
        {
            // Sample the 8 corners of this cube
            // Corner numbering matches MarchingCubesTables
            cubeCorners[0] = sampler(x, y, z);
            cubeCorners[1] = sampler(x + 1, y, z);
            cubeCorners[2] = sampler(x + 1, y, z + 1);
            cubeCorners[3] = sampler(x, y, z + 1);
            cubeCorners[4] = sampler(x, y + 1, z);
            cubeCorners[5] = sampler(x + 1, y + 1, z);
            cubeCorners[6] = sampler(x + 1, y + 1, z + 1);
            cubeCorners[7] = sampler(x, y + 1, z + 1);

            // Calculate cube index (8-bit value indicating which corners are INSIDE the solid)
            // Density model: 0 = air, 1 = solid, ISO_LEVEL = 0.5
            // Set bit when corner is SOLID (density > ISO_LEVEL) so normals face toward air/player
            int cubeIndex = 0;
            if (cubeCorners[0] > ISO_LEVEL) cubeIndex |= 1;
            if (cubeCorners[1] > ISO_LEVEL) cubeIndex |= 2;
            if (cubeCorners[2] > ISO_LEVEL) cubeIndex |= 4;
            if (cubeCorners[3] > ISO_LEVEL) cubeIndex |= 8;
            if (cubeCorners[4] > ISO_LEVEL) cubeIndex |= 16;
            if (cubeCorners[5] > ISO_LEVEL) cubeIndex |= 32;
            if (cubeCorners[6] > ISO_LEVEL) cubeIndex |= 64;
            if (cubeCorners[7] > ISO_LEVEL) cubeIndex |= 128;

            // Skip fully inside or outside cubes
            if (cubeIndex == 0 || cubeIndex == 255)
                return;

            // Get edge mask for this configuration
            int edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];

            // Calculate vertex positions for active edges
            Vector3 cubeOrigin = new Vector3(x, y, z) * voxelSize;

            for (int edge = 0; edge < 12; edge++)
            {
                if ((edgeMask & (1 << edge)) == 0)
                    continue;

                // Get the two vertices this edge connects
                int v0 = MarchingCubesTables.EdgeVertices[edge, 0];
                int v1 = MarchingCubesTables.EdgeVertices[edge, 1];

                // Get corner positions
                Vector3 p0 = GetCornerPosition(v0) * voxelSize + cubeOrigin;
                Vector3 p1 = GetCornerPosition(v1) * voxelSize + cubeOrigin;

                // Interpolate along edge based on density values
                float d0 = cubeCorners[v0];
                float d1 = cubeCorners[v1];
                float t = (ISO_LEVEL - d0) / (d1 - d0);
                t = Mathf.Clamp01(t);

                Vector3 edgePos = Vector3.Lerp(p0, p1, t);
                edgePositions[edge] = edgePos;

                // Get or create welded vertex
                edgeIndices[edge] = GetOrAddWeldedVertex(edgePos, vertices, vertexMap);
            }

            // Generate triangles from the triangle table
            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex, i] != -1; i += 3)
            {
                int e0 = MarchingCubesTables.TriTable[cubeIndex, i];
                int e1 = MarchingCubesTables.TriTable[cubeIndex, i + 1];
                int e2 = MarchingCubesTables.TriTable[cubeIndex, i + 2];

                // Add triangle with correct winding order
                triangles.Add(edgeIndices[e0]);
                triangles.Add(edgeIndices[e1]);
                triangles.Add(edgeIndices[e2]);
            }
        }

        /// <summary>
        /// Get corner position offset for a vertex index (0-7).
        /// </summary>
        private static Vector3 GetCornerPosition(int vertexIndex)
        {
            return new Vector3(
                MarchingCubesTables.VertexOffsets[vertexIndex, 0],
                MarchingCubesTables.VertexOffsets[vertexIndex, 1],
                MarchingCubesTables.VertexOffsets[vertexIndex, 2]);
        }

        /// <summary>
        /// Get or add a welded vertex at the given position.
        /// Uses spatial hashing for O(1) lookup.
        /// </summary>
        private static int GetOrAddWeldedVertex(
            Vector3 position,
            List<Vector3> vertices,
            Dictionary<int, int> vertexMap)
        {
            int hash = GetPositionHash(position);

            if (vertexMap.TryGetValue(hash, out int existingIndex))
            {
                // Check if positions are actually close enough
                // (hash collisions are possible but rare with our precision)
                Vector3 existing = vertices[existingIndex];
                if (Vector3.SqrMagnitude(existing - position) < WELD_EPSILON * WELD_EPSILON)
                {
                    return existingIndex;
                }
            }

            // Add new vertex
            int newIndex = vertices.Count;
            vertices.Add(position);
            vertexMap[hash] = newIndex;
            return newIndex;
        }

        /// <summary>
        /// Compute spatial hash for a position.
        /// Quantizes to WELD_EPSILON precision.
        /// </summary>
        private static int GetPositionHash(Vector3 v)
        {
            // Quantize to welding precision
            int x = Mathf.RoundToInt(v.x / WELD_EPSILON);
            int y = Mathf.RoundToInt(v.y / WELD_EPSILON);
            int z = Mathf.RoundToInt(v.z / WELD_EPSILON);

            // Combine using prime multipliers for good distribution
            unchecked
            {
                return x * 73856093 ^ y * 19349663 ^ z * 83492791;
            }
        }

        /// <summary>
        /// Calculate smooth normals by averaging face normals at each vertex.
        /// </summary>
        private static void CalculateNormals(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> normals)
        {
            // Initialize normals to zero
            normals.Clear();
            for (int i = 0; i < vertices.Count; i++)
            {
                normals.Add(Vector3.zero);
            }

            // Accumulate face normals at each vertex
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                // Calculate face normal (weighted by triangle area)
                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector3 faceNormal = Vector3.Cross(edge1, edge2);
                // Don't normalize - larger triangles contribute more (area weighting)

                // Accumulate at each vertex
                normals[i0] += faceNormal;
                normals[i1] += faceNormal;
                normals[i2] += faceNormal;
            }

            // Normalize all accumulated normals
            for (int i = 0; i < normals.Count; i++)
            {
                Vector3 n = normals[i];
                if (n.sqrMagnitude > 0.0001f)
                {
                    normals[i] = n.normalized;
                }
                else
                {
                    normals[i] = Vector3.up; // Fallback for degenerate cases
                }
            }
        }

        /// <summary>
        /// Generate mesh with pre-allocated lists (reduces GC allocations).
        /// Call this version for repeated mesh generation.
        /// Generates double-sided mesh for visibility from both directions.
        /// </summary>
        public static void GenerateMeshNonAlloc(
            DensitySampler sampler,
            float voxelSize,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> normals,
            Dictionary<int, int> vertexMapReuse)
        {
            // Clear output lists
            vertices.Clear();
            triangles.Clear();
            normals.Clear();
            vertexMapReuse.Clear();

            // Temporary arrays for cube processing (stack allocated)
            float[] cubeCorners = new float[8];
            Vector3[] edgePositions = new Vector3[12];
            int[] edgeIndices = new int[12];

            // Process each cube in the chunk
            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    for (int z = 0; z < CHUNK_SIZE; z++)
                    {
                        ProcessCube(
                            x, y, z,
                            sampler,
                            voxelSize,
                            vertices,
                            triangles,
                            vertexMapReuse,
                            cubeCorners,
                            edgePositions,
                            edgeIndices);
                    }
                }
            }

            // Calculate smooth normals for front faces
            CalculateNormals(vertices, triangles, normals);

            // NOTE: Double-sided rendering is handled by shader (Cull Off + SV_IsFrontFace)
            // Do NOT duplicate triangles here - that breaks normal calculations for backfaces
        }

        #region Debug Utilities

        /// <summary>
        /// Estimate the number of vertices for a chunk with given fill ratio.
        /// Useful for pre-allocating lists.
        /// </summary>
        public static int EstimateVertexCount(float fillRatio)
        {
            // Surface area increases with more mixed solid/air regions
            // Empirically, ~6 vertices per surface voxel on average
            float surfaceRatio = 4f * fillRatio * (1f - fillRatio); // Max at 50% fill
            int surfaceVoxels = Mathf.RoundToInt(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE * surfaceRatio);
            return Mathf.Max(100, surfaceVoxels * 6);
        }

        /// <summary>
        /// Estimate triangle count from vertex count.
        /// </summary>
        public static int EstimateTriangleCount(int vertexCount)
        {
            // Roughly 1.5-2 triangles per vertex in typical terrain
            return vertexCount * 2;
        }

        #endregion
    }
}
