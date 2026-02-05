using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// Schedules and batches MeshCollider updates to prevent excessive physics rebuilds.
    /// Collider updates are expensive - this class ensures they happen in controlled batches
    /// with a configurable delay to coalesce rapid dig operations.
    ///
    /// Usage:
    /// - Call ScheduleUpdate(chunk) whenever a chunk's mesh changes
    /// - Call ProcessScheduledUpdates() each frame to process due updates
    ///
    /// FUTURE OPTIMIZATION: Could be extended to prioritize chunks near player
    /// or use Unity's async physics cooking when available.
    /// </summary>
    public class ColliderUpdateScheduler
    {
        /// <summary>
        /// Entry in the update queue.
        /// </summary>
        private struct ScheduledUpdate
        {
            public VoxelChunk Chunk;
            public float ScheduledTime;
        }

        // Configuration
        private readonly float _updateDelay;
        private readonly int _maxUpdatesPerFrame;

        // Pending updates: ChunkCoord -> scheduled time
        private readonly Dictionary<ChunkCoord, ScheduledUpdate> _pendingUpdates;

        // Chunks ready to be processed this frame
        private readonly List<VoxelChunk> _readyChunks;

        // Statistics
        public int PendingCount => _pendingUpdates.Count;
        public int TotalUpdatesProcessed { get; private set; }

        /// <summary>
        /// Create a new collider update scheduler.
        /// </summary>
        /// <param name="updateDelay">Seconds to wait before updating a collider after scheduling.</param>
        /// <param name="maxUpdatesPerFrame">Maximum collider updates per frame (0 = unlimited).</param>
        public ColliderUpdateScheduler(float updateDelay = 0.1f, int maxUpdatesPerFrame = 4)
        {
            _updateDelay = Mathf.Max(0f, updateDelay);
            _maxUpdatesPerFrame = maxUpdatesPerFrame;
            _pendingUpdates = new Dictionary<ChunkCoord, ScheduledUpdate>(32);
            _readyChunks = new List<VoxelChunk>(16);
        }

        /// <summary>
        /// Schedule a collider update for a chunk.
        /// If the chunk is already scheduled, the timer is reset.
        /// </summary>
        /// <param name="chunk">The chunk that needs a collider update.</param>
        public void ScheduleUpdate(VoxelChunk chunk)
        {
            if (chunk == null)
                return;

            var update = new ScheduledUpdate
            {
                Chunk = chunk,
                ScheduledTime = Time.time + _updateDelay
            };

            // Add or update the scheduled entry
            _pendingUpdates[chunk.Coord] = update;
        }

        /// <summary>
        /// Schedule an immediate update for a chunk (no delay).
        /// Use sparingly - immediate updates can cause frame spikes.
        /// </summary>
        /// <param name="chunk">The chunk that needs a collider update.</param>
        public void ScheduleImmediateUpdate(VoxelChunk chunk)
        {
            if (chunk == null)
                return;

            var update = new ScheduledUpdate
            {
                Chunk = chunk,
                ScheduledTime = 0f // Process immediately
            };

            _pendingUpdates[chunk.Coord] = update;
        }

        /// <summary>
        /// Cancel a scheduled update for a chunk.
        /// Call this if a chunk is being destroyed.
        /// </summary>
        /// <param name="coord">The chunk coordinate to cancel.</param>
        public void CancelUpdate(ChunkCoord coord)
        {
            _pendingUpdates.Remove(coord);
        }

        /// <summary>
        /// Process scheduled updates that are due.
        /// Call this once per frame from a MonoBehaviour.Update().
        /// </summary>
        /// <returns>Number of colliders updated this frame.</returns>
        public int ProcessScheduledUpdates()
        {
            float currentTime = Time.time;

            // Collect chunks that are ready for update
            _readyChunks.Clear();

            foreach (var kvp in _pendingUpdates)
            {
                if (kvp.Value.ScheduledTime <= currentTime)
                {
                    _readyChunks.Add(kvp.Value.Chunk);
                }
            }

            // Limit updates per frame if configured
            int updateCount = _readyChunks.Count;
            if (_maxUpdatesPerFrame > 0 && updateCount > _maxUpdatesPerFrame)
            {
                updateCount = _maxUpdatesPerFrame;
            }

            // Process ready chunks
            for (int i = 0; i < updateCount; i++)
            {
                VoxelChunk chunk = _readyChunks[i];

                // Remove from pending
                _pendingUpdates.Remove(chunk.Coord);

                // Update the collider
                chunk.UpdateCollider();
                TotalUpdatesProcessed++;
            }

            return updateCount;
        }

        /// <summary>
        /// Force update all pending colliders immediately.
        /// Use sparingly - can cause significant frame time.
        /// </summary>
        /// <returns>Number of colliders updated.</returns>
        public int FlushAllUpdates()
        {
            int count = 0;

            foreach (var kvp in _pendingUpdates)
            {
                kvp.Value.Chunk.UpdateCollider();
                count++;
                TotalUpdatesProcessed++;
            }

            _pendingUpdates.Clear();
            return count;
        }

        /// <summary>
        /// Clear all scheduled updates without processing them.
        /// Call this when resetting the terrain system.
        /// </summary>
        public void Clear()
        {
            _pendingUpdates.Clear();
            _readyChunks.Clear();
        }

        /// <summary>
        /// Get debug information about the scheduler state.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"ColliderScheduler: {_pendingUpdates.Count} pending, {TotalUpdatesProcessed} total processed";
        }
    }
}
