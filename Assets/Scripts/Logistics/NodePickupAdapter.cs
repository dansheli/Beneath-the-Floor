using UnityEngine;
using BeneathTheFloor.ResourceSystem;

namespace BeneathTheFloor.Logistics
{
    public class NodePickupAdapter : IPickupAdapter
    {
        private readonly NodePickup pickup;

        public NodePickupAdapter(NodePickup pickup)
        {
            this.pickup = pickup;
        }

        public Vector3 Position => pickup != null ? pickup.transform.position : Vector3.zero;
        public bool IsValid => pickup != null && pickup.gameObject != null && pickup.gameObject.activeInHierarchy;
        public string ResourceId => pickup != null ? pickup.ResourceId : "";
        public int Tier => pickup != null ? pickup.Tier : 1;
        public int Amount => pickup != null ? pickup.Amount : 0;
        public int CreditValue => pickup != null ? pickup.CreditValue : 0;
        public GameObject GameObject => pickup != null ? pickup.gameObject : null;

        public void Collect()
        {
            if (pickup != null && pickup.gameObject != null)
            {
                Object.Destroy(pickup.gameObject);
            }
        }

        public NodePickup GetPickup() => pickup;
    }
}
