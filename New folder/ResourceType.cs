namespace Spacegun_Simulator
{
    /// <summary>
    /// Resource type enumeration covering all 3 tiers.
    /// TIER 1 (Basic): Steel, Budget, Power Cells
    /// TIER 2 (Intermediate): Specialized Alloys, Rare Earth Elements
    /// TIER 3 (Advanced): Exotic Materials, Advanced Ore
    /// </summary>
    public enum ResourceType
    {
        // TIER 1 - Basic Resources (always available)
        Steel,
        Budget,
        PowerCells,

        // TIER 2 - Intermediate Resources (unlock at Mining/Production II)
        SpecializedAlloys,
        RareEarthElements,

        // TIER 3 - Advanced Resources (unlock at Mining/Production III)
        ExoticMaterials,
        AdvancedOre
    }

    /// <summary>
    /// Resource metadata and helper methods.
    /// Maps resource types to display names, tiers, and unlock conditions.
    /// </summary>
    public static class ResourceTypeHelper
    {
        /// <summary>
        /// Get the tech tier required to unlock this resource.
        /// Returns 1 for Tier 1 (always available), 2 for Tier 2, 3 for Tier 3.
        /// </summary>
        public static int GetUnlockTier(ResourceType resource)
        {
            return resource switch
            {
                // Tier 1 - Always available
                ResourceType.Steel => 1,
                ResourceType.Budget => 1,
                ResourceType.PowerCells => 1,

                // Tier 2 - Unlock at Mining/Production II
                ResourceType.SpecializedAlloys => 2,
                ResourceType.RareEarthElements => 2,

                // Tier 3 - Unlock at Mining/Production III
                ResourceType.ExoticMaterials => 3,
                ResourceType.AdvancedOre => 3,

                _ => 1
            };
        }

        /// <summary>
        /// Get display name for the resource.
        /// </summary>
        public static string GetDisplayName(ResourceType resource)
        {
            return resource switch
            {
                ResourceType.Steel => "Steel",
                ResourceType.Budget => "Budget",
                ResourceType.PowerCells => "Power Cells",
                ResourceType.SpecializedAlloys => "Specialized Alloys",
                ResourceType.RareEarthElements => "Rare Earth Elements",
                ResourceType.ExoticMaterials => "Exotic Materials",
                ResourceType.AdvancedOre => "Advanced Ore",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get unit of measurement for this resource.
        /// </summary>
        public static string GetUnit(ResourceType resource)
        {
            return resource switch
            {
                ResourceType.Steel => "tons",
                ResourceType.Budget => "currency",
                ResourceType.PowerCells => "units",
                ResourceType.SpecializedAlloys => "tons",
                ResourceType.RareEarthElements => "units",
                ResourceType.ExoticMaterials => "units",
                ResourceType.AdvancedOre => "units",
                _ => "units"
            };
        }

        /// <summary>
        /// Get base production rate per year for this resource.
        /// Before difficulty scaling, before tech bonuses.
        /// </summary>
        public static double GetBaseProductionRate(ResourceType resource)
        {
            return resource switch
            {
                ResourceType.Steel => GameConstants.SteelProductionPerYear,
                ResourceType.Budget => GameConstants.BudgetProductionPerYear,
                ResourceType.PowerCells => GameConstants.PowerCellsProductionPerYear,
                ResourceType.SpecializedAlloys => GameConstants.SpecializedAlloysProductionPerYear,
                ResourceType.RareEarthElements => GameConstants.RareEarthElementsProductionPerYear,
                ResourceType.ExoticMaterials => GameConstants.ExoticProductionPerYear,
                ResourceType.AdvancedOre => GameConstants.ExoticProductionPerYear * 0.8,  // Slightly less than exotic
                _ => 0
            };
        }
    }
}