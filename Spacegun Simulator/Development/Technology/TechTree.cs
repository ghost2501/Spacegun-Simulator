using Spacegun_Simulator.Economy;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Development.Technology
{
    /// <summary>
    /// Tech tree structure for campaign progression.
    /// 5 main trees: Radar, Mining, Production, Weapons, Projectiles
    /// 3 levels per tree (I, II, III)
    /// Player starts at level I in all trees and can research to II and III.
    /// </summary>
    public class TechTree
    {
        public enum TechType
        {
            Radar,
            Mining,
            Production,
            Weapons,
            Projectiles
        }

        /// <summary>
        /// Current tech level for each tree (1-3, starts at 1).
        /// </summary>
        public Dictionary<TechType, int> CurrentLevel { get; set; } = new();

        public TechTree()
        {
            // Initialize all tech trees to level 1
            CurrentLevel[TechType.Radar] = 1;
            CurrentLevel[TechType.Mining] = 1;
            CurrentLevel[TechType.Production] = 1;
            CurrentLevel[TechType.Weapons] = 1;
            CurrentLevel[TechType.Projectiles] = 1;
        }

        /// <summary>
        /// Check if a tech can be researched (is at max level I or II, has prerequisites met).
        /// </summary>
        public bool CanResearch(TechType tech)
        {
            if (!CurrentLevel.ContainsKey(tech))
                return false;

            // Can only research to level 3 (next level after current)
            return CurrentLevel[tech] < 3;
        }

        /// <summary>
        /// Get the cost to research the next level of a tech tree.
        /// </summary>
        public ResourceCost GetResearchCost(TechType tech)
        {
            int nextLevel = CurrentLevel.ContainsKey(tech) ? CurrentLevel[tech] + 1 : 2;

            // Cost scales by level: II = moderate, III = expensive
            return nextLevel switch
            {
                2 => DevelopmentTuning.TechTree.ResearchCostToLevel2,
                3 => DevelopmentTuning.TechTree.ResearchCostToLevel3,
                _ => ResourceCost.None
            };
        }

        /// <summary>
        /// Research the next level of a tech tree.
        /// Returns true if successful.
        /// </summary>
        public bool ResearchTech(TechType tech)
        {
            if (!CanResearch(tech))
                return false;

            if (!CurrentLevel.ContainsKey(tech))
                CurrentLevel[tech] = 1;

            CurrentLevel[tech]++;
            return true;
        }

        /// <summary>
        /// Get the production bonus multiplier for a resource based on tech level.
        /// Mining I = 1.0x (base), Mining II = 1.2x, Mining III = 1.5x
        /// Production works the same way.
        /// </summary>
        public double GetProductionBonus(ResourceType resource)
        {
            int unlockTier = ResourceTypeHelper.GetUnlockTier(resource);

            // Resource requires Mining/Production tech of at least that tier
            TechType requiredTech = resource switch
            {
                ResourceType.Steel => TechType.Mining,
                ResourceType.SpecializedAlloys => TechType.Mining,
                ResourceType.RareEarthElements => TechType.Mining,
                ResourceType.AdvancedOre => TechType.Mining,
                ResourceType.ExoticMaterials => TechType.Production,
                ResourceType.PowerCells => TechType.Production,
                _ => TechType.Mining
            };

            // Check if we have the required tech level to even gather this resource
            if (!CurrentLevel.ContainsKey(requiredTech) || CurrentLevel[requiredTech] < unlockTier)
            {
                return 0;  // Can't gather this resource yet
            }

            // Get bonus multiplier from tech level
            int techLevel = CurrentLevel[requiredTech];
            var bonuses = DevelopmentTuning.TechTree.ProductionBonusByLevel;
            if (bonuses is null || bonuses.Length < 3)
                return 1.0;

            return techLevel switch
            {
                1 => bonuses[0],
                2 => bonuses[1],
                3 => bonuses[2],
                _ => bonuses[0]
            };
        }

        /// <summary>
        /// Get a description of what a tech tier unlocks.
        /// </summary>
        public static string GetTechDescription(TechType tech, int level)
        {
            return (tech, level) switch
            {
                (TechType.Radar, 1) => "Basic ground-based detection",
                (TechType.Radar, 2) => "Extended detection range",
                (TechType.Radar, 3) => "Space-based detection systems",

                (TechType.Mining, 1) => "Basic resource extraction (Steel, Budget, Power Cells)",
                (TechType.Mining, 2) => "Advanced mining (+20% yield, Specialized Alloys, Rare Earth)",
                (TechType.Mining, 3) => "Expert mining (+50% yield, Advanced Ore access)",

                (TechType.Production, 1) => "Basic processing",
                (TechType.Production, 2) => "Refined production (+20% yield)",
                (TechType.Production, 3) => "Advanced synthesis (+50% yield, Exotic Materials)",

                (TechType.Weapons, 1) => "Chemical propulsion systems",
                (TechType.Weapons, 2) => "Railgun technology",
                (TechType.Weapons, 3) => "Energy weapons research",

                (TechType.Projectiles, 1) => "Kinetic penetrators",
                (TechType.Projectiles, 2) => "Shaped charge warheads",
                (TechType.Projectiles, 3) => "Guided munitions",

                _ => "Unknown"
            };
        }
    }
}