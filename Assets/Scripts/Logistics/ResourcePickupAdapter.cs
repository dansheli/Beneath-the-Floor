using UnityEngine;
using BeneathTheFloor.Digging;

namespace BeneathTheFloor.Logistics
{
    public class ResourcePickupAdapter : IPickupAdapter
    {
        private readonly ResourcePickup pickup;

        public ResourcePickupAdapter(ResourcePickup pickup)
        {
            this.pickup = pickup;
        }

        public Vector3 Position => pickup != null ? pickup.transform.position : Vector3.zero;
        public bool IsValid => pickup != null && pickup.gameObject != null && pickup.gameObject.activeInHierarchy;
        public string ResourceId => pickup != null ? pickup.resourceType.ToString() : "";
        public int Tier => 1;
        public int Amount => pickup != null ? pickup.amount : 0;
        public int CreditValue => 1;
        public GameObject GameObject => pickup != null ? pickup.gameObject : null;

        public void Collect()
        {
            if (pickup != null && pickup.gameObject != null)
            {
                Object.Destroy(pickup.gameObject);
            }
        }

        public ResourcePickup GetPickup() => pickup;
    }
}
