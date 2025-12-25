namespace Spacegun_Simulator
{
    /// <summary>
    /// Random event system for waves.
    /// Events trigger every 3rd wave (3, 6, 9, 12, etc.)
    /// Types: Positive (buff), Negative (nerf), Neutral (discovery)
    /// Effects scale by tier (earlier tiers have smaller effects).
    /// </summary>
    public class RandomEvent
    {
        public enum EventType
        {
            Positive,
            Negative,
            Neutral
        }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public EventType Type { get; set; }

        /// <summary>
        /// Multiplier applied to resource production this wave.
        /// > 1.0 = buff, < 1.0 = nerf, = 1.0 = neutral
        /// </summary>
        public double ProductionMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Check if this wave should have a random event.
        /// Events occur every 3rd wave (waves 3, 6, 9, 12, etc.)
        /// </summary>
        public static bool ShouldHaveEvent(int waveNumber)
        {
            return waveNumber > 0 && waveNumber % 3 == 0;
        }

        /// <summary>
        /// Generate a random event for a given wave.
        /// Event type and intensity scale with wave number/tier.
        /// </summary>
        public static RandomEvent GenerateEvent(int waveNumber, Random rng)
        {
            if (!ShouldHaveEvent(waveNumber))
                return null!;  // No event for this wave

            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            // Determine event type (weighted towards balance)
            double roll = rng.NextDouble();
            EventType eventType = roll switch
            {
                < 0.30 => EventType.Positive,
                < 0.60 => EventType.Negative,
                _ => EventType.Neutral
            };

            // Generate event with tier-scaled intensity
            return eventType switch
            {
                EventType.Positive => GeneratePositiveEvent(waveNumber, tierIndex, rng),
                EventType.Negative => GenerateNegativeEvent(waveNumber, tierIndex, rng),
                EventType.Neutral => GenerateNeutralEvent(waveNumber, tierIndex, rng),
                _ => GenerateNeutralEvent(waveNumber, tierIndex, rng)
            };
        }

        private static RandomEvent GeneratePositiveEvent(int waveNumber, int tierIndex, Random rng)
        {
            // Buff scales with tier: Tier 0 = +20%, Tier 1 = +35%, Tier 2 = +50%
            double multiplier = tierIndex switch
            {
                0 => 1.20,  // Early tiers: modest buff
                1 => 1.35,
                2 => 1.50,  // Late tiers: strong buff
                _ => 1.20
            };

            string[] titles = new[]
            {
                "Rich Deposit Found",
                "Mining Breakthrough",
                "Favorable Conditions",
                "Resource Surge",
                "Geological Anomaly",
                "Stellar Alignment"
            };

            string selectedTitle = titles[rng.Next(titles.Length)];

            return new RandomEvent
            {
                Title = selectedTitle,
                Description = $"Production increased by {(multiplier - 1) * 100:F0}% this wave.",
                Type = EventType.Positive,
                ProductionMultiplier = multiplier
            };
        }

        private static RandomEvent GenerateNegativeEvent(int waveNumber, int tierIndex, Random rng)
        {
            // Nerf scales with tier: Tier 0 = -15%, Tier 1 = -30%, Tier 2 = -50%
            double multiplier = tierIndex switch
            {
                0 => 0.85,   // Early tiers: modest penalty
                1 => 0.70,
                2 => 0.50,   // Late tiers: severe penalty
                _ => 0.85
            };

            string[] titles = new[]
            {
                "Mining Accident",
                "Equipment Failure",
                "Resource Contamination",
                "Seismic Activity",
                "System Malfunction",
                "Supply Chain Disruption"
            };

            string selectedTitle = titles[rng.Next(titles.Length)];

            return new RandomEvent
            {
                Title = selectedTitle,
                Description = $"Production reduced by {(1 - multiplier) * 100:F0}% this wave.",
                Type = EventType.Negative,
                ProductionMultiplier = multiplier
            };
        }

        private static RandomEvent GenerateNeutralEvent(int waveNumber, int tierIndex, Random rng)
        {
            string[] titles = new[]
            {
                "New Deposit Located",
                "Research Advancement",
                "Technology Transfer",
                "Sector Analysis Complete",
                "Survey Successful",
                "Data Synthesis Finished"
            };

            string selectedTitle = titles[rng.Next(titles.Length)];

            return new RandomEvent
            {
                Title = selectedTitle,
                Description = "A new resource type will become available for research next wave.",
                Type = EventType.Neutral,
                ProductionMultiplier = 1.0
            };
        }
    }
}