namespace Spacegun_Simulator
{
    /// <summary>
    /// Manages tech research and unlocks during the resource allocation phase.
    /// Players can spend years to research the next tech level (I→II, II→III).
    /// Tech upgrades are immediate but cost resources/time.
    /// </summary>
    public class TechUnlock
    {
        public TechTree.TechType TechType { get; set; }
        public int FromLevel { get; set; }
        public int ToLevel { get; set; }
        public ResourceCost ResearchCost { get; set; } = ResourceCost.None;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Get all available tech unlocks the player can currently research.
        /// Only returns techs that can be upgraded (< level 3).
        /// </summary>
        public static List<TechUnlock> GetAvailableUnlocks(TechTree techTree)
        {
            var available = new List<TechUnlock>();

            foreach (TechTree.TechType tech in System.Enum.GetValues(typeof(TechTree.TechType)))
            {
                if (!techTree.CanResearch(tech))
                    continue;

                int currentLevel = techTree.CurrentLevel[tech];
                int nextLevel = currentLevel + 1;

                var cost = techTree.GetResearchCost(tech);
                var description = TechTree.GetTechDescription(tech, nextLevel);

                available.Add(new TechUnlock
                {
                    TechType = tech,
                    FromLevel = currentLevel,
                    ToLevel = nextLevel,
                    ResearchCost = cost,
                    Description = description
                });
            }

            return available;
        }

        /// <summary>
        /// Display all available tech unlocks with their costs.
        /// </summary>
        public static void DisplayAvailableTechs(TechTree techTree, Dictionary<string, double> accumulatedResources)
        {
            var available = GetAvailableUnlocks(techTree);

            if (available.Count == 0)
            {
                Console.WriteLine("✗ No techs available for research.\n");
                return;
            }

            Console.WriteLine("=== AVAILABLE TECH RESEARCH ===\n");

            for (int i = 0; i < available.Count; i++)
            {
                var unlock = available[i];
                bool canAfford = CanAffordResearch(unlock, accumulatedResources);
                string affordMark = canAfford ? "✓" : "✗";

                Console.WriteLine($"{affordMark} [{i + 1}] {unlock.TechType} ({unlock.FromLevel} → {unlock.ToLevel})");
                Console.WriteLine($"    {unlock.Description}");
                Console.WriteLine($"    Cost: {unlock.ResearchCost.Budget:F0} Budget, {unlock.ResearchCost.Steel:F0} Steel, {unlock.ResearchCost.ExoticMaterials:F0} Exotic");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Check if player can afford to research a tech.
        /// </summary>
        public static bool CanAffordResearch(TechUnlock unlock, Dictionary<string, double> accumulatedResources)
        {
            double budget = accumulatedResources.ContainsKey("Budget") ? accumulatedResources["Budget"] : 0;
            double steel = accumulatedResources.ContainsKey("Steel") ? accumulatedResources["Steel"] : 0;
            double exotic = accumulatedResources.ContainsKey("Exotic") ? accumulatedResources["Exotic"] : 0;

            return budget >= unlock.ResearchCost.Budget &&
                   steel >= unlock.ResearchCost.Steel &&
                   exotic >= unlock.ResearchCost.ExoticMaterials;
        }

        /// <summary>
        /// Research a tech if affordable.
        /// Returns true if successful.
        /// </summary>
        public static bool ResearchTech(
            TechUnlock unlock,
            TechTree techTree,
            Dictionary<string, double> accumulatedResources)
        {
            if (!CanAffordResearch(unlock, accumulatedResources))
                return false;

            // Deduct cost
            accumulatedResources["Budget"] -= unlock.ResearchCost.Budget;
            accumulatedResources["Steel"] -= unlock.ResearchCost.Steel;
            accumulatedResources["Exotic"] -= unlock.ResearchCost.ExoticMaterials;

            // Apply tech upgrade
            return techTree.ResearchTech(unlock.TechType);
        }
    }
}