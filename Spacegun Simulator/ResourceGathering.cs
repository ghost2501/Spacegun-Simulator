namespace Spacegun_Simulator
{
    /// <summary>
    /// Handles all resource gathering calculations:
    /// - Base production rates (years × production per year)
    /// - Difficulty scaling (modulates production)
    /// - Tech bonuses (mining/production level increases yield)
    /// - Resource conversions (instant, 1:1 or custom ratios)
    /// - Random event modifiers
    /// </summary>
    public class ResourceGathering
    {
        /// <summary>
        /// Get the effective production rate for a resource this wave.
        /// Factors in: difficulty scaling, tech bonuses, and wave events.
        /// Formula: BaseRate × DifficultyMultiplier × TechBonus × EventMultiplier
        /// </summary>
        public static double GetEffectiveProductionRate(
            ResourceType resource,
            TechTree techTree,
            GameDifficulty difficulty,
            double eventMultiplier = 1.0)
        {
            // Start with base production rate
            double baseRate = ResourceTypeHelper.GetBaseProductionRate(resource);

            // Apply difficulty scaling (harder = lower production)
            double difficultyMultiplier = GetDifficultyMultiplier(difficulty);

            // Apply tech bonuses (mining/production level)
            double techBonus = techTree.GetProductionBonus(resource);

            // If tech bonus is 0, resource is not yet unlocked
            if (techBonus == 0)
                return 0;

            // Apply random event modifier (from this wave if applicable)
            double finalRate = baseRate * difficultyMultiplier * techBonus * eventMultiplier;

            return finalRate;
        }

        /// <summary>
        /// Get the difficulty multiplier for resource production.
        /// Nuclear Option = 1.0x (easy, no penalty)
        /// Comets and Asteroids = 0.85x (15% penalty, balanced)
        /// Alien Invasion = 0.70x (30% penalty, hard)
        /// </summary>
        public static double GetDifficultyMultiplier(GameDifficulty difficulty)
        {
            return difficulty switch
            {
                GameDifficulty.NuclearOption => 1.0,           // Easy - full production
                GameDifficulty.CometsAndAsteroids => 0.85,     // Normal - slight penalty
                GameDifficulty.RealSpacegunSimulator => 0.70,  // Hard - significant penalty (changed from AlienInvasion)
                _ => 0.85
            };
        }

        /// <summary>
        /// Convert one resource type to another.
        /// Conversion is instant but consumes the source resource.
        /// Standard ratio: 2 source = 1 target (loss represents processing cost).
        /// Some conversions may have custom ratios.
        /// </summary>
        public static bool TryConvertResource(
            Dictionary<string, double> resources,
            ResourceType from,
            ResourceType to,
            int amount)
        {
            // Get the conversion ratio (how many source needed per target)
            int ratio = GetConversionRatio(from, to);

            if (ratio == 0)
                return false;  // Invalid conversion

            int sourceCost = amount * ratio;

            // Get dictionary keys
            string fromKey = ResourceTypeHelper.GetDisplayName(from);
            string toKey = ResourceTypeHelper.GetDisplayName(to);

            // Check if we have enough source material
            if (!resources.ContainsKey(fromKey) || resources[fromKey] < sourceCost)
                return false;

            // Perform conversion
            resources[fromKey] -= sourceCost;

            if (!resources.ContainsKey(toKey))
                resources[toKey] = 0;

            resources[toKey] += amount;
            return true;
        }

        /// <summary>
        /// Get the conversion ratio from one resource to another.
        /// Returns how many of the source resource are needed per one unit of target.
        /// Returns 0 if conversion is invalid.
        /// </summary>
        private static int GetConversionRatio(ResourceType from, ResourceType to)
        {
            // Can't convert to/from budget
            if (from == ResourceType.Budget || to == ResourceType.Budget)
                return 0;

            // Same resource - no conversion needed
            if (from == to)
                return 0;

            // Tier 1 to Tier 2 conversions (2:1 loss due to processing)
            if ((from == ResourceType.Steel || from == ResourceType.PowerCells) &&
                (to == ResourceType.SpecializedAlloys || to == ResourceType.RareEarthElements))
                return 2;  // 2 source = 1 target

            // Tier 2 to Tier 3 conversions (2:1 loss)
            if ((from == ResourceType.SpecializedAlloys || from == ResourceType.RareEarthElements) &&
                (to == ResourceType.ExoticMaterials || to == ResourceType.AdvancedOre))
                return 2;

            // No other conversions allowed
            return 0;
        }

        /// <summary>
        /// Calculate gathered resources for a single year of allocation.
        /// Takes into account difficulty and tech bonuses.
        /// </summary>
        public static Dictionary<string, double> CalculateYearProduction(
            TechTree techTree,
            GameDifficulty difficulty,
            double eventMultiplier = 1.0)
        {
            var production = new Dictionary<string, double>();

            // Calculate production for each available resource
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                double rate = GetEffectiveProductionRate(resource, techTree, difficulty, eventMultiplier);
                
                if (rate > 0)
                {
                    string key = ResourceTypeHelper.GetDisplayName(resource);
                    production[key] = rate;
                }
            }

            return production;
        }
    }
}