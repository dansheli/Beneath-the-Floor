using UnityEngine;
using BeneathTheFloor.Lighting;

namespace BeneathTheFloor.Tools
{
    /// <summary>
    /// Makes the Core Shard detectable by the radar in Mode 2 (CoreShard).
    /// Attach this to the Core Shard object in the scene.
    /// Automatically disables when the player picks up the shard.
    /// </summary>
    public class CoreShardRadarTarget : MonoBehaviour
    {
        [SerializeField] private int radarPriority = 15; // Higher than chests (10)
        [SerializeField] private string radarDisplayName = "Core Shard";

        private RadarTarget radarTarget;

        private void Start()
        {
            radarTarget = gameObject.AddComponent<RadarTarget>();
            radarTarget.SetPriority(radarPriority);
            radarTarget.SetTargetName(radarDisplayName);
            radarTarget.SetTargetMode(2); // CoreShard mode
        }

        private void Update()
        {
            if (CoreShardPickup.IsHoldingShard)
            {
                if (radarTarget != null) radarTarget.enabled = false;
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (radarTarget != null) radarTarget.enabled = false;
        }
    }
}
