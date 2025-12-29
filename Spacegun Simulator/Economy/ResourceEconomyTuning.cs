namespace Spacegun_Simulator.Economy
{
    /// <summary>
    /// Economy / progression knobs that gate development decisions.
    /// GameConstants forwards to these for compatibility.
    /// </summary>
    public static class ResourceEconomyTuning
    {
        public static double BudgetRewardBase = 100.0;
        public static double BudgetRewardPerWave = 10.0;

        public static double SteelRewardBase = 50.0;
        public static double SteelRewardPerWave = 5.0;

        public static double ExoticRewardBase = 5.0;
        public static double ExoticRewardPerWave = 2.0;
        public static double MinBudgetToContinue = 100.0;

        // Resource production rates (units per year) - WHOLE NUMBERS
        public static double SteelProductionPerYear = 100.0;
        public static double ExoticProductionPerYear = 10.0;
        public static double BudgetProductionPerYear = 50.0;

        // Extended resource types for mid/late-game progression
        public static double RareEarthElementsProductionPerYear = 5.0;
        public static double SpecializedAlloysProductionPerYear = 15.0;
        public static double PowerCellsProductionPerYear = 8.0;
    }
}
