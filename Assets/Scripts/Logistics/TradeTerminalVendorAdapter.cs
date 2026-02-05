using UnityEngine;
using BeneathTheFloor.Economy;
using BeneathTheFloor.Energy;

namespace BeneathTheFloor.Logistics
{
    public class TradeTerminalVendorAdapter : ITradeTerminalVendor
    {
        private const int EnergyDrinkCost = 25;
        private const int LampCost = 50;

        public bool TryBuyEnergyDrink()
        {
            var currency = CurrencyManager.Instance;
            if (currency == null) return false;
            if (!currency.Spend(EnergyDrinkCost)) return false;

            var energy = EnergyManager.Instance;
            if (energy != null)
            {
                energy.AddDrinks(1);
            }
            Debug.Log("[LogisticsVendor] Purchased energy drink for robot delivery");
            return true;
        }

        public bool TryBuyLamp()
        {
            var currency = CurrencyManager.Instance;
            if (currency == null) return false;
            if (!currency.Spend(LampCost)) return false;

            Debug.Log("[LogisticsVendor] Purchased lamp for robot delivery");
            return true;
        }
    }
}
