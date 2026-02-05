namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// All underground resource types organized by depth layer.
    /// </summary>
    public enum UndergroundResourceType
    {
        // No resource / empty dig
        None = 0,

        // Layer 1 - Soft Depth (5-20m)
        Dirt,
        SoftStone,
        Clay,
        IronNugget,
        CopperFragment,

        // Layer 2 - Hard Soil (20-40m)
        HardSoil,
        Stone,
        Sandstone,
        IronChunk,
        CopperPiece,
        Coal,

        // Layer 3 - Heat & Pressure (40-70m)
        HardSoilDeep,
        HardStone,
        Heatstone,
        Quartz,
        CrystalDust,
        AncientOre,
        Gold,

        // Layer 4 - Crystal Roots (70-110m)
        CrystalShard,
        PurpleQuartz,
        CrystalStone,
        DeepCrystalVein,
        LuminousDust,

        // Layer 5 - Crystal Chamber (110-160m)
        CrystalCoreFragment,
        BlueGreyOre,

        // Layer 6 - Final Depth (160-220m)
        DeepBlackStone,
        CoreCrystalChunk,
        RareMachineParts
    }
}
