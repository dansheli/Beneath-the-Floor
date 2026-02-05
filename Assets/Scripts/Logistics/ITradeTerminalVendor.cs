namespace BeneathTheFloor.Logistics
{
    public interface ITradeTerminalVendor
    {
        bool TryBuyEnergyDrink();
        bool TryBuyLamp();
    }
}
