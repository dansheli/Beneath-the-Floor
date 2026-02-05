using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Save data for a single voxel chunk.
    /// Only stores chunks that have been modified to minimize save file size.
    /// </summary>
    [Serializable]
    public class ChunkSaveData
    {
        public int coordX;
        public int coordY;
        public int coordZ;
        public float[] densities;

        public ChunkSaveData() { }

        public ChunkSaveData(ChunkCoord coord, float[] densityData)
        {
            coordX = coord.X;
            coordY = coord.Y;
            coordZ = coord.Z;
            densities = densityData;
        }

        public ChunkCoord GetCoord()
        {
            return new ChunkCoord(coordX, coordY, coordZ);
        }
    }

    /// <summary>
    /// Complete save data for voxel terrain.
    /// </summary>
    [Serializable]
    public class TerrainSaveData
    {
        public int version = 1;
        public string savedAt;
        public float voxelSize;
        public int chunkSize;
        public int modifiedChunkCount;
        public List<ChunkSaveData> modifiedChunks = new List<ChunkSaveData>();

        public TerrainSaveData()
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public bool IsValid()
        {
            return version > 0 && modifiedChunks != null;
        }
    }
}
