using UnityEngine;
using System.Collections.Generic;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Logistics
{
    [System.Serializable]
    public struct CargoEntry
    {
        public string resourceId;
        public int tier;
        public int quantity;
        public int creditValuePerUnit;
    }

    public class RobotCargoBuffer : MonoBehaviour
    {
        [SerializeField] private LogisticsRobotConfig config;

        private Dictionary<string, CargoEntry> cargo = new Dictionary<string, CargoEntry>();

        public int MaxDistinctTypes
        {
            get
            {
                int baseSlots = config != null ? config.baseDistinctTypeSlots : 1;
                int upgradeLevel = FirstRoomUpgradeStationUI.LogisticsCapacityLevel;
                return baseSlots + upgradeLevel;
            }
        }

        public int MaxPerType => config != null ? config.totalCapacityPerType : 99;

        public bool IsEmpty => cargo.Count == 0;

        public bool IsFull
        {
            get
            {
                if (cargo.Count >= MaxDistinctTypes) return true;
                foreach (var kv in cargo)
                {
                    if (kv.Value.quantity >= MaxPerType) return true;
                }
                return false;
            }
        }

        public bool CanAccept(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return false;
            if (cargo.ContainsKey(resourceId))
            {
                return cargo[resourceId].quantity < MaxPerType;
            }
            return cargo.Count < MaxDistinctTypes;
        }

        public bool TryAddCargo(string resourceId, int tier, int amount, int creditValuePerUnit)
        {
            if (string.IsNullOrEmpty(resourceId)) return false;

            if (cargo.ContainsKey(resourceId))
            {
                var entry = cargo[resourceId];
                if (entry.quantity + amount > MaxPerType) return false;
                entry.quantity += amount;
                cargo[resourceId] = entry;
                return true;
            }

            if (cargo.Count >= MaxDistinctTypes) return false;

            cargo[resourceId] = new CargoEntry
            {
                resourceId = resourceId,
                tier = tier,
                quantity = amount,
                creditValuePerUnit = creditValuePerUnit
            };
            return true;
        }

        public List<CargoEntry> GetAllCargo()
        {
            var list = new List<CargoEntry>(cargo.Count);
            foreach (var kv in cargo)
                list.Add(kv.Value);
            return list;
        }

        public void Clear()
        {
            cargo.Clear();
        }

        public void Init(LogisticsRobotConfig cfg)
        {
            config = cfg;
        }

        public string GetCargoSummary()
        {
            if (cargo.Count == 0) return "Empty";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in cargo)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{kv.Key} x{kv.Value.quantity}");
            }
            return sb.ToString();
        }
    }
}
