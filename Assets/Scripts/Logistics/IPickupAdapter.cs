using UnityEngine;

namespace BeneathTheFloor.Logistics
{
    public interface IPickupAdapter
    {
        Vector3 Position { get; }
        bool IsValid { get; }
        string ResourceId { get; }
        int Tier { get; }
        int Amount { get; }
        int CreditValue { get; }
        void Collect();
        GameObject GameObject { get; }
    }
}
