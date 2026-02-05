using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Digging;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Logistics
{
    public class LootScanner : MonoBehaviour
    {
        [SerializeField] private LogisticsRobotConfig config;

        private float scanTimer;

        // Static claimed set: pickups targeted by any logistics robot
        private static readonly HashSet<GameObject> claimedPickups = new HashSet<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            claimedPickups.Clear();
        }

        private IPickupAdapter currentTarget;
        private GameObject currentTargetObj;

        public IPickupAdapter CurrentTarget => currentTarget != null && currentTarget.IsValid ? currentTarget : null;

        public void Init(LogisticsRobotConfig cfg)
        {
            config = cfg;
        }

        public void SetRadius(float radius)
        {
            _overrideRadius = radius;
        }

        private float _overrideRadius = -1f;
        private float ActiveRadius => _overrideRadius > 0 ? _overrideRadius : (config != null ? config.collectRadius : 5f);

        private void Update()
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = config != null ? config.scanInterval : 0.5f;
                Scan();
            }

            // Validate current target
            if (currentTarget != null && !currentTarget.IsValid)
            {
                ReleaseClaim();
            }
        }

        private float debugLogTimer;

        private void Scan()
        {
            // Clean up stale claims
            claimedPickups.RemoveWhere(p => p == null || !p.activeInHierarchy);

            float radius = ActiveRadius;
            float bestDist = float.MaxValue;
            GameObject bestObj = null;
            bool bestIsNode = false;

            // Periodic debug log
            debugLogTimer -= (config != null ? config.scanInterval : 0.5f);

            // --- Scan NodePickup objects (new resource system) ---
            var nodePickups = FindObjectsOfType<NodePickup>();

            if (debugLogTimer <= 0f)
            {
                debugLogTimer = 3f;
                int oldCount = ResourcePickup.ActivePickups != null ? ResourcePickup.ActivePickups.Count : 0;
                Debug.Log($"[LootScanner] Scanning: radius={radius:F1}, nodePickups={nodePickups.Length}, oldPickups={oldCount}, pos={transform.position:F1}");
            }

            for (int i = 0; i < nodePickups.Length; i++)
            {
                var np = nodePickups[i];
                if (np == null || !np.gameObject.activeInHierarchy) continue;
                if (claimedPickups.Contains(np.gameObject) && np.gameObject != currentTargetObj) continue;

                float dist = Vector3.Distance(transform.position, np.transform.position);
                if (dist <= radius && dist < bestDist)
                {
                    bestDist = dist;
                    bestObj = np.gameObject;
                    bestIsNode = true;
                }
            }

            // --- Also scan old ResourcePickup objects (fallback) ---
            var pickups = ResourcePickup.ActivePickups;
            for (int i = 0; i < pickups.Count; i++)
            {
                var p = pickups[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                if (claimedPickups.Contains(p.gameObject) && p.gameObject != currentTargetObj) continue;

                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist <= radius && dist < bestDist)
                {
                    bestDist = dist;
                    bestObj = p.gameObject;
                    bestIsNode = false;
                }
            }

            if (bestObj != currentTargetObj)
            {
                ReleaseClaim();
                if (bestObj != null)
                {
                    currentTargetObj = bestObj;
                    if (bestIsNode)
                    {
                        var np = bestObj.GetComponent<NodePickup>();
                        currentTarget = new NodePickupAdapter(np);
                        Debug.Log($"[LootScanner] Targeting NodePickup: {np.ResourceId} at {bestObj.transform.position:F1}, dist={bestDist:F1}");
                    }
                    else
                    {
                        var rp = bestObj.GetComponent<ResourcePickup>();
                        currentTarget = new ResourcePickupAdapter(rp);
                        Debug.Log($"[LootScanner] Targeting ResourcePickup: {rp.resourceType} at {bestObj.transform.position:F1}, dist={bestDist:F1}");
                    }
                    claimedPickups.Add(bestObj);
                }
            }
        }

        public void ReleaseClaim()
        {
            if (currentTargetObj != null)
            {
                claimedPickups.Remove(currentTargetObj);
                currentTargetObj = null;
            }
            currentTarget = null;
        }

        private void OnDisable()
        {
            ReleaseClaim();
        }
    }
}
