namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// BALLISTIC TABLES & REFERENCE CHARTS (TIER & DIFFICULTY-LINKED)
    /// 
    /// Provides dynamically-generated lookup tables for ballistic calculations.
    /// Tables adapt to:
    /// - Tier (0-3): Enemy velocity and gun range adjust table ranges
    /// - Difficulty: Hit tolerances and RCS multipliers are displayed
    /// 
    /// PURPOSE: Educational tool for players to verify calculations without solving problems.
    /// Values are pulled from GameConstants and DifficultyConfig, ensuring consistency.
    /// 
    /// PRECISION: All formatting delegates to DifficultyConfig (single source of truth).
    /// </summary>
    public static class BallisticsTablesReference
    {
        // Constants from game physics
        private const float GRAVITY = 9.81f;

        // ====================================================================
        // TABLE 1: TIME-OF-FLIGHT REFERENCE (TIER-ADAPTIVE)
        // ====================================================================

        /// <summary>
        /// Calculate time-of-flight from velocity, range, and elevation angle.
        /// Time of flight = Range / (Velocity × cos(elevation))
        /// NOTE: This is simplified horizontal range calculation. Gravity affects vertical component.
        /// </summary>
        public static float CalculateTimeOfFlight(
            float velocityMs,
            float rangeMeters,
            float elevationDegrees)
        {
            if (velocityMs <= 0)
                return 0f;

            float elevationRad = elevationDegrees * (float)Math.PI / 180f;
            float horizontalVelocity = velocityMs * (float)Math.Cos(elevationRad);

            if (horizontalVelocity <= 0)
                return 0f;

            return rangeMeters / horizontalVelocity;
        }

        /// <summary>
        /// Display Time-of-Flight Table with difficulty-appropriate precision.
        /// Uses DifficultyConfig as single source of truth for precision.
        /// </summary>
        public static void DisplayTimeOfFlightTable(int? tierIndex = null, GameDifficulty? difficulty = null)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         TABLE 1: TIME-OF-FLIGHT REFERENCE                 ║");
            Console.WriteLine("║    (Tier-adaptive: velocity & ranges match your tier)     ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var tier = tierIndex.HasValue
                ? GameConstants.WaveTiers[Math.Min(tierIndex.Value, 3)]
                : GameConstants.WaveTiers[0];

            var diffConfig = difficulty.HasValue
                ? DifficultyConfig.GetConfig(difficulty.Value)
                : DifficultyConfig.GetConfig(GameDifficulty.RealSpacegunSimulator);

            double minVel = tier.VelocityMin;
            double maxVel = tier.VelocityMax;
            double gunRange = tier.MaxEffectiveGunRange;

            Console.WriteLine($"Tier {tier.TierIndex} | Difficulty: {diffConfig.DisplayName}");
            Console.WriteLine($"Precision: {diffConfig.LaunchDelayPrecision.DecimalPlaces} decimals for time, {diffConfig.ElevationPrecision.DecimalPlaces} for angles");
            Console.WriteLine($"Enemy Velocity: {GameConstants.FormatVelocity(minVel)}-{GameConstants.FormatVelocity(maxVel)} | Gun Range: {GameConstants.FormatDistance(gunRange)}\n");

            // Generate velocity samples across the tier range
            int velSamples = 6;
            var velocities = new List<float>();
            for (int i = 0; i < velSamples; i++)
            {
                double fraction = i / (double)(velSamples - 1);
                double vel = minVel + (maxVel - minVel) * fraction;
                velocities.Add((float)vel);
            }

            // Generate range samples: from 50% to 95% of gun range
            int rangeSamples = 6;
            var ranges = new List<int>();
            for (int i = 0; i < rangeSamples; i++)
            {
                double fraction = 0.5 + (0.45 * i / (double)(rangeSamples - 1));  // 50% to 95%
                int rangeKm = (int)(gunRange / 1000.0 * fraction);
                ranges.Add(rangeKm);
            }

            float[] elevations = { 15f, 30f, 45f, 60f };

            foreach (float elev in elevations)
            {
                Console.WriteLine($"ELEVATION: {diffConfig.ElevationPrecision.Format(elev)}°");
                
                // Build header
                Console.Write("Velocity  │");
                foreach (int r in ranges)
                    Console.Write($" {r:D5}km │");
                Console.WriteLine();

                Console.Write("──────────┼");
                for (int i = 0; i < ranges.Count; i++)
                    Console.Write("────────┼");
                Console.WriteLine();

                // Display rows
                foreach (float vel in velocities)
                {
                    Console.Write($"{GameConstants.FormatVelocity(vel),9} │");
                    foreach (int rangeKm in ranges)
                    {
                        float tof = CalculateTimeOfFlight(vel, rangeKm * 1000f, elev);
                        // Use centralized precision from DifficultyConfig
                        string tofStr = diffConfig.LaunchDelayPrecision.Format(tof).PadRight(6);
                        Console.Write($" {tofStr}s │");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine();
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("INTERPRETATION GUIDE:");
            Console.WriteLine("  • This table is customized for Tier " + tier.TierIndex);
            Console.WriteLine("  • Difficulty: " + diffConfig.DisplayName);
            Console.WriteLine("  • Precision requirements:\n" + diffConfig.GetPrecisionSummary());
            Console.WriteLine("  • Velocity ranges: " + GameConstants.FormatVelocity(minVel) + " to " + GameConstants.FormatVelocity(maxVel));
            Console.WriteLine("  • Engagement ranges: 50-95% of your gun range (" + GameConstants.FormatDistance(gunRange) + ")");
            Console.WriteLine("  • Lower velocity = longer flight time");
            Console.WriteLine("  • Higher elevation = longer flight time\n");
        }

        // ====================================================================
        // TABLE 2: GRAVITY DROP REFERENCE (CONTEXT-AWARE ACCURACY)
        // ====================================================================

        public static float CalculateGravityDrop(float flightTimeSeconds)
        {
            return 0.5f * GRAVITY * flightTimeSeconds * flightTimeSeconds;
        }

        /// <summary>
        /// Calculate gravity drop as a percentage of a reference engagement range.
        /// This shows whether gravity is actually significant for the tier.
        /// </summary>
        public static double CalculateGravityDropPercentage(float flightTimeSeconds, double engagementRangeMeters)
        {
            if (engagementRangeMeters <= 0)
                return 0.0;

            float dropM = CalculateGravityDrop(flightTimeSeconds);
            return (dropM / engagementRangeMeters) * 100.0;
        }

        /// <summary>
        /// Get human-readable significance level for gravity drop percentage.
        /// </summary>
        private static string GetSignificanceLevel(double percentage)
        {
            return percentage switch
            {
                < 0.01 => "NEGLIGIBLE (< 0.01%)",
                < 0.1 => "MINIMAL (< 0.1%)",
                < 1.0 => "MINOR",
                < 5.0 => "MODERATE",
                < 10.0 => "SIGNIFICANT",
                _ => "CRITICAL (> 10%)"
            };
        }

        /// <summary>
        /// Display Gravity Drop Table with difficulty-appropriate precision.
        /// Uses DifficultyConfig as single source of truth for precision.
        /// </summary>
        public static void DisplayGravityDropTable(int? tierIndex = null, GameDifficulty? difficulty = null)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           TABLE 2: GRAVITY DROP REFERENCE                 ║");
            Console.WriteLine("║      Altitude loss over time with % impact analysis       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var tier = tierIndex.HasValue
                ? GameConstants.WaveTiers[Math.Min(tierIndex.Value, 3)]
                : GameConstants.WaveTiers[0];

            var diffConfig = difficulty.HasValue
                ? DifficultyConfig.GetConfig(difficulty.Value)
                : DifficultyConfig.GetConfig(GameDifficulty.RealSpacegunSimulator);

            double engagementRange = tier.MaxEffectiveGunRange * 0.5;  // Reference: 50% of gun range
            double typicalFlightTime = (engagementRange / tier.VelocityMax) * 1.5;  // Typical engagement scenario

            Console.WriteLine($"Tier {tier.TierIndex} | Difficulty: {diffConfig.DisplayName}");
            Console.WriteLine($"Reference Engagement Range: {GameConstants.FormatDistance(engagementRange)}");
            Console.WriteLine($"Typical Flight Time (est.): {diffConfig.FormatLaunchDelay(typicalFlightTime)}\n");

            // Generate appropriate flight time range for tier
            float minFlightTime = tier.TierIndex switch
            {
                0 => 0.1f,   // Tier 0: shorter flights possible
                1 => 0.01f,  // Tier 1: millisecond flights
                2 => 0.001f, // Tier 2: sub-millisecond flights
                3 => 0.0001f,// Tier 3: ultra-short flights
                _ => 0.1f
            };

            float maxFlightTime = tier.TierIndex switch
            {
                0 => 30f,    // Tier 0: up to 30 seconds
                1 => 10f,    // Tier 1: up to 10 seconds
                2 => 5f,     // Tier 2: up to 5 seconds
                3 => 1f,     // Tier 3: up to 1 second
                _ => 30f
            };

            Console.WriteLine("=== VERTICAL DROP WITH IMPACT ANALYSIS ===");
            Console.WriteLine("Flight Time │ Drop (meters) │ Drop (km) │ % of Range │ Significance");
            Console.WriteLine("─────────────┼───────────────┼───────────┼────────────┼──────────────");

            // Generate samples
            int samples = 20;
            for (int i = 0; i <= samples; i++)
            {
                double fraction = i / (double)samples;
                float t = minFlightTime + (maxFlightTime - minFlightTime) * (float)fraction;

                float dropM = CalculateGravityDrop(t);
                float dropKm = dropM / 1000f;
                double dropPercent = CalculateGravityDropPercentage(t, engagementRange);

                // Determine significance based on percentage
                string significance = GetSignificanceLevel(dropPercent);

                // Use centralized precision from DifficultyConfig
                string timeStr = diffConfig.LaunchDelayPrecision.Format(t).PadLeft(11);
                string dropMStr = diffConfig.DistancePrecision.Format(dropM).PadLeft(13);
                string dropKmStr = (dropKm).ToString($"F{diffConfig.DistancePrecision.DecimalPlaces + 1}").PadLeft(9);
                string percentStr = $"{dropPercent.ToString($"F{diffConfig.DistancePrecision.DecimalPlaces + 1}")}%".PadLeft(10);

                Console.WriteLine($"{timeStr} │ {dropMStr} │ {dropKmStr} │ {percentStr} │ {significance}");
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine($"ENGAGEMENT CONTEXT (Tier {tier.TierIndex}):");
            Console.WriteLine($"  Reference range: {GameConstants.FormatDistance(engagementRange)}");
            Console.WriteLine($"  Typical flight time: {diffConfig.FormatLaunchDelay(typicalFlightTime)}");
            double typicalGravityDrop = CalculateGravityDrop((float)typicalFlightTime);
            double typicalPercent = CalculateGravityDropPercentage((float)typicalFlightTime, engagementRange);
            Console.WriteLine($"  Gravity drop at typical flight: {diffConfig.FormatDistance(typicalGravityDrop)} ({typicalPercent:F2}% of range)\n");

            if (tier.TierIndex <= 1)
            {
                if (typicalPercent < 0.1)
                {
                    Console.WriteLine("⚠️  GRAVITY IS NEGLIGIBLE FOR THIS TIER");
                    Console.WriteLine("  Your typical engagement flights are too short for gravity to matter.");
                    Console.WriteLine("  Focus on velocity vectors, not ballistic drop.\n");
                }
                else
                {
                    Console.WriteLine("⚠️  GRAVITY IS SIGNIFICANT FOR THIS TIER");
                    Console.WriteLine("  You MUST account for gravity drop with elevation compensation.");
                    Console.WriteLine("  Use this table to estimate the adjustment needed.\n");
                }
            }
            else
            {
                Console.WriteLine("⚠️  GRAVITY IS NEGLIGIBLE FOR THIS TIER");
                Console.WriteLine("  At relativistic speeds and short flight times, gravity drop is");
                Console.WriteLine("  effectively zero. Focus your targeting on velocity accuracy.\n");
            }

            Console.WriteLine("PRECISION REQUIREMENTS:");
            Console.WriteLine(diffConfig.GetPrecisionSummary());
        }

        // ====================================================================
        // TABLE 3: ENERGY VS VELOCITY (TIER & DIFFICULTY-LINKED)
        // ====================================================================

        public static double CalculateKineticEnergyMJ(double massKg, double velocityMs)
        {
            double energyJoules = 0.5 * massKg * velocityMs * velocityMs;
            return energyJoules / 1_000_000.0;
        }

        /// <summary>
        /// Display Energy Reference Table adapted to tier and difficulty.
        /// Uses DifficultyConfig as single source of truth for precision.
        /// </summary>
        public static void DisplayEnergyReferenceTable(int? tierIndex = null, GameDifficulty? difficulty = null)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        TABLE 3: KINETIC ENERGY REFERENCE                  ║");
            Console.WriteLine("║    Energy delivery across tier velocity ranges            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var tier = tierIndex.HasValue
                ? GameConstants.WaveTiers[Math.Min(tierIndex.Value, 3)]
                : GameConstants.WaveTiers[0];

            var diffConfig = difficulty.HasValue
                ? DifficultyConfig.GetConfig(difficulty.Value)
                : DifficultyConfig.GetConfig(GameDifficulty.RealSpacegunSimulator);

            Console.WriteLine($"Tier {tier.TierIndex} | Difficulty: {diffConfig.DisplayName}");
            Console.WriteLine($"Enemy Velocity Range: {GameConstants.FormatVelocity(tier.VelocityMin)}-{GameConstants.FormatVelocity(tier.VelocityMax)}\n");

            // Generate velocity samples for this tier
            int velSamples = 5;
            var velocities = new List<double>();
            for (int i = 0; i < velSamples; i++)
            {
                double fraction = i / (double)(velSamples - 1);
                double vel = tier.VelocityMin + (tier.VelocityMax - tier.VelocityMin) * fraction;
                velocities.Add(vel);
            }

            double[] masses = { 10, 25, 50, 100 };

            Console.WriteLine("=== KINETIC ENERGY BY MASS AND VELOCITY (in MJ) ===");
            
            // Build header
            Console.Write("Mass (kg) │");
            foreach (double vel in velocities)
                Console.Write($" {GameConstants.FormatVelocity(vel),8} │");
            Console.WriteLine();

            Console.Write("──────────┼");
            for (int i = 0; i < velocities.Count; i++)
                Console.Write("──────────┼");
            Console.WriteLine();

            // Display rows
            foreach (double mass in masses)
            {
                Console.Write($"{diffConfig.MassPrecision.Format(mass),9} │");
                foreach (double vel in velocities)
                {
                    double energy = CalculateKineticEnergyMJ(mass, vel);
                    string energyStr = energy < 1000000
                        ? $"{diffConfig.EnergyPrecision.Format(energy)} MJ"
                        : $"{(energy / 1_000_000).ToString($"F{diffConfig.EnergyPrecision.DecimalPlaces}")} PJ";
                    Console.Write($" {energyStr,9} │");
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine($"DIFFICULTY MODIFIER: Hit Tolerance x{diffConfig.HitToleranceMultiplier}, Target RCS x{diffConfig.TargetRcsMultiplier}\n");
            Console.WriteLine("KEY OBSERVATIONS:");
            Console.WriteLine("  • Doubling velocity increases energy by 4× (quadratic)");
            Console.WriteLine("  • Doubling mass increases energy by 2× (linear)");
            Console.WriteLine("  • Velocity is MORE important than mass\n");
        }

        // ====================================================================
        // TABLE 4: RANGE COVERAGE (TIER-ADAPTIVE)
        // ====================================================================

        /// <summary>
        /// Format a distance value with difficulty-aware precision.
        /// Delegates to DifficultyConfig for unit-aware formatting.
        /// </summary>
        private static string FormatDistanceWithPrecision(double distanceMeters, DifficultyConfig diffConfig)
        {
            return diffConfig.FormatDistance(distanceMeters);
        }

        /// <summary>
        /// Display Range Coverage Table with difficulty-appropriate precision.
        /// Uses DifficultyConfig as single source of truth for precision.
        /// </summary>
        public static void DisplayRangeCoverageTable(int? tierIndex = null, GameDifficulty? difficulty = null)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           TABLE 4: RANGE COVERAGE REFERENCE               ║");
            Console.WriteLine("║      Distance traveled in typical tier engagement times   ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var tier = tierIndex.HasValue
                ? GameConstants.WaveTiers[Math.Min(tierIndex.Value, 3)]
                : GameConstants.WaveTiers[0];

            var diffConfig = difficulty.HasValue
                ? DifficultyConfig.GetConfig(difficulty.Value)
                : DifficultyConfig.GetConfig(GameDifficulty.RealSpacegunSimulator);

            double minVel = tier.VelocityMin;
            double maxVel = tier.VelocityMax;
            double gunRange = tier.MaxEffectiveGunRange;

            Console.WriteLine($"Tier {tier.TierIndex} | Difficulty: {diffConfig.DisplayName}");
            Console.WriteLine($"Gun Range: {GameConstants.FormatDistance(gunRange)}\n");

            // Generate velocity samples
            int velSamples = 5;
            var velocities = new List<float>();
            for (int i = 0; i < velSamples; i++)
            {
                double fraction = i / (double)(velSamples - 1);
                double vel = minVel + (maxVel - minVel) * fraction;
                velocities.Add((float)vel);
            }

            // Generate time samples appropriate for tier
            var times = tier.TierIndex switch
            {
                0 => new float[] { 1f, 5f, 10f, 15f, 20f, 30f },
                1 => new float[] { 0.1f, 0.5f, 1f, 2f, 5f, 10f },
                2 => new float[] { 0.01f, 0.05f, 0.1f, 0.5f, 1f, 5f },
                3 => new float[] { 0.001f, 0.005f, 0.01f, 0.05f, 0.1f, 1f },
                _ => new float[] { 1f, 5f, 10f, 15f, 20f, 30f }
            };

            Console.WriteLine("=== DISTANCE COVERED BY VELOCITY OVER TIME ===");
            Console.Write("Velocity  │");
            foreach (float t in times)
                Console.Write($"  {diffConfig.LaunchDelayPrecision.Format(t)}s  │");
            Console.WriteLine();

            Console.Write("──────────┼");
            for (int i = 0; i < times.Length; i++)
                Console.Write("──────────┼");
            Console.WriteLine();

            foreach (float vel in velocities)
            {
                Console.Write($"{GameConstants.FormatVelocity(vel),9} │");

                foreach (float t in times)
                {
                    float distance = vel * t;
                    // Use centralized precision from DifficultyConfig
                    string distanceStr = FormatDistanceWithPrecision(distance, diffConfig);

                    Console.Write($" {distanceStr,8}  │");
                }

                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine($"ENGAGEMENT ENVELOPE:");
            Console.WriteLine($"  • Gun Range: {GameConstants.FormatDistance(gunRange)}");
            Console.WriteLine($"  • Target enters gun range at T+0");
            Console.WriteLine($"  • Must intercept before target exits optimal range\n");
            Console.WriteLine("PRECISION REQUIREMENTS:");
            Console.WriteLine(diffConfig.GetPrecisionSummary());
        }

        // ====================================================================
        // MAIN REFERENCE MENU (TIER & DIFFICULTY-AWARE)
        // ====================================================================

        /// <summary>
        /// Display reference charts menu with tier & difficulty context.
        /// </summary>
        public static void ShowReferencesMenu(int? currentTierIndex = null, GameDifficulty? currentDifficulty = null)
        {
            bool inMenu = true;

            while (inMenu)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║           FIRE CONTROL REFERENCE CHARTS                   ║");
                Console.WriteLine("║      (Tier & Difficulty-Adapted for Your Scenario)        ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                var diffConfig = currentDifficulty.HasValue
                    ? DifficultyConfig.GetConfig(currentDifficulty.Value)
                    : DifficultyConfig.GetConfig(GameDifficulty.RealSpacegunSimulator);

                if (currentTierIndex.HasValue)
                    Console.WriteLine($"Current Tier: {currentTierIndex.Value} | Difficulty: {diffConfig.DisplayName}\n");

                Console.WriteLine("PRECISION REQUIREMENTS:");
                Console.WriteLine(diffConfig.GetPrecisionSummary());
                Console.WriteLine();

                Console.WriteLine("SELECT A REFERENCE TABLE:\n");
                Console.WriteLine("[1] Time-of-Flight Table (tier-scaled flight times)");
                Console.WriteLine("[2] Gravity Drop Table (tier-adjusted altitude loss)");
                Console.WriteLine("[3] Kinetic Energy Table (difficulty-aware damage)");
                Console.WriteLine("[4] Range Coverage Table (tier-specific distances)");
                Console.WriteLine("[5] View All Tables");
                Console.WriteLine("[0] Return to Firing Solution\n");

                Console.Write("Select reference: ");
                string input = Console.ReadLine() ?? "0";

                switch (input)
                {
                    case "1":
                        DisplayTimeOfFlightTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "2":
                        DisplayGravityDropTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "3":
                        DisplayEnergyReferenceTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "4":
                        DisplayRangeCoverageTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "5":
                        DisplayTimeOfFlightTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();

                        DisplayGravityDropTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();

                        DisplayEnergyReferenceTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();

                        DisplayRangeCoverageTable(currentTierIndex, currentDifficulty);
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "0":
                        inMenu = false;
                        break;

                    default:
                        Console.WriteLine("Invalid selection. Please try again.\n");
                        System.Threading.Thread.Sleep(1000);
                        break;
                }
            }
        }
    }
}