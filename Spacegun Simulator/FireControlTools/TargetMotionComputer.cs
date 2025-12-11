namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// TARGET MOTION COMPUTER
    /// 
    /// Mechanical fire control aid simulating mid-20th century motion prediction systems.
    /// Allows players to explore target trajectories by calculating predicted position
    /// at various future times without automated targeting or recommendations.
    /// 
    /// PRECISION: All formatting delegates to DifficultyConfig (single source of truth).
    /// </summary>
    public static class TargetMotionComputer
    {
        // ====================================================================
        // MAIN INTERFACE
        // ====================================================================

        /// <summary>
        /// Launch the Target Motion Computer interactive tool.
        /// Player can test multiple time offsets in a loop.
        /// Uses DifficultyConfig for all precision formatting.
        /// </summary>
        public static void ShowMotionComputerTool(Vector3 currentPosition, Vector3 currentVelocity, GameDifficulty difficulty = GameDifficulty.RealSpacegunSimulator)
        {
            var diffConfig = DifficultyConfig.GetConfig(difficulty);
            bool inTool = true;

            while (inTool)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║            MOTION COMPUTER - TRAJECTORY SOLVER            ║");
                Console.WriteLine("║     Predict target position at future times (linear)      ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine($"Difficulty: {diffConfig.DisplayName}");
                Console.WriteLine($"Precision: {diffConfig.LaunchDelayPrecision.DecimalPlaces} decimals for time\n");

                Console.WriteLine("=== CURRENT TARGET STATE (T=0) ===");
                Console.WriteLine($"Position: {diffConfig.FormatVector3(currentPosition)}");
                Console.WriteLine($"Distance from origin: {FormatDistanceWithPrecision(currentPosition.Magnitude, diffConfig)}");
                Console.WriteLine($"Velocity: {diffConfig.FormatVelocityVector(currentVelocity)}");
                Console.WriteLine($"Speed: {diffConfig.FormatVelocity(currentVelocity.Magnitude)}\n");

                // ENHANCED: Display automatic timeline T+0 to T+20
                Console.WriteLine("=== TARGET RANGE TIMELINE (T+0s to T+20s) ===\n");
                DisplayRangeTimeline(currentPosition, currentVelocity, diffConfig);

                Console.WriteLine("\n=== QUERY SPECIFIC TIME ===");
                Console.WriteLine("Enter time offset to see detailed position data.");
                Console.WriteLine("Or press [Q] to quit, or [R] to show range timeline only.\n");

                Console.Write("Time offset (seconds) or [Q]uit or [R]ange: ");
                string input = Console.ReadLine() ?? "Q";

                // Handle quit
                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("X", StringComparison.OrdinalIgnoreCase))
                {
                    inTool = false;
                    break;
                }

                // Handle range-only refresh
                if (input.Equals("R", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Parse time input (supports decimals like 3.27)
                if (!double.TryParse(input, out double timeOffset) || timeOffset < 0)
                {
                    Console.WriteLine("\n✗ Invalid input. Please enter a non-negative time value or [Q] to quit.\n");
                    System.Threading.Thread.Sleep(1500);
                    continue;
                }

                // Calculate future position
                var result = CalculateMotionAtTime(currentPosition, currentVelocity, timeOffset);

                // Display results
                Console.WriteLine();
                DisplayMotionComputerResult(result, diffConfig);

                Console.WriteLine("\nOptions:");
                Console.WriteLine("[Enter] Query another time");
                Console.WriteLine("[R] Show range timeline again");
                Console.WriteLine("[Q] Quit Motion Computer\n");
                Console.Write("Select: ");
                string choice = Console.ReadLine() ?? "";

                if (choice.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    inTool = false;
                }
            }
        }

        // ====================================================================
        // CALCULATION ENGINE
        // ====================================================================

        /// <summary>
        /// Calculate target position at a future time using linear motion assumption.
        /// Formula: Position(t) = CurrentPosition + Velocity × t
        /// </summary>
        public static MotionPredictionResult CalculateMotionAtTime(
            Vector3 currentPosition,
            Vector3 currentVelocity,
            double timeOffsetSeconds)
        {
            Vector3 displacement = currentVelocity * timeOffsetSeconds;
            Vector3 futurePosition = currentPosition + displacement;

            double futureDistance = futurePosition.Magnitude;

            double horizontalDistance = Math.Sqrt(futurePosition.X * futurePosition.X +
                                                   futurePosition.Y * futurePosition.Y);
            double elevationRad = Math.Atan2(futurePosition.Z, horizontalDistance);
            double elevationDeg = elevationRad * 180.0 / Math.PI;

            double azimuthRad = Math.Atan2(futurePosition.X, futurePosition.Y);
            double azimuthDeg = azimuthRad * 180.0 / Math.PI;
            if (azimuthDeg < 0)
                azimuthDeg += 360.0;

            return new MotionPredictionResult
            {
                TimeOffset = timeOffsetSeconds,
                PredictedPosition = futurePosition,
                PredictedDistance = futureDistance,
                PredictedElevation = elevationDeg,
                PredictedAzimuth = azimuthDeg,
                DistanceTraveled = displacement
            };
        }

        /// <summary>
        /// Format a distance value with difficulty-aware precision.
        /// Delegates to DifficultyConfig for unit-aware formatting.
        /// </summary>
        private static string FormatDistanceWithPrecision(double distanceMeters, DifficultyConfig diffConfig)
        {
            return diffConfig.FormatDistance(distanceMeters);
        }

        /// <summary>
        /// Display automatic range timeline from T+0 to T+20 seconds.
        /// Uses DifficultyConfig for all precision formatting.
        /// </summary>
        private static void DisplayRangeTimeline(Vector3 startPosition, Vector3 velocity, DifficultyConfig diffConfig)
        {
            Console.WriteLine("Time  │ Target Range      │ Status");
            Console.WriteLine("──────┼───────────────────┼─────────────────────────");

            float gunRange = 1_500_000f; // 1.5 Mm

            for (int t = 0; t <= 20; t++)
            {
                var result = CalculateMotionAtTime(startPosition, velocity, t);
                bool inRange = result.PredictedDistance <= gunRange;
                string status = inRange ? "✓ IN RANGE" : "✗ out range";

                string rangeStr = FormatDistanceWithPrecision(result.PredictedDistance, diffConfig);

                Console.WriteLine($"{t,2}s  │ {rangeStr,17} │ {status}");
            }
        }

        /// <summary>
        /// Represents the result of a motion prediction calculation.
        /// </summary>
        public class MotionPredictionResult
        {
            public double TimeOffset { get; set; }
            public Vector3 PredictedPosition { get; set; }
            public double PredictedDistance { get; set; }
            public double PredictedElevation { get; set; }
            public double PredictedAzimuth { get; set; }
            public Vector3 DistanceTraveled { get; set; }
        }

        /// <summary>
        /// Display motion computer calculation results in detail.
        /// Uses DifficultyConfig for all precision formatting.
        /// </summary>
        private static void DisplayMotionComputerResult(MotionPredictionResult result, DifficultyConfig diffConfig)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          MOTION COMPUTER CALCULATION RESULTS              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== PREDICTION AT T+{diffConfig.LaunchDelayPrecision.Format(result.TimeOffset)} SECONDS ===\n");

            Console.WriteLine("Predicted Position (Cartesian):");
            Console.WriteLine($"  X: {diffConfig.DistancePrecision.Format(result.PredictedPosition.X)} meters");
            Console.WriteLine($"  Y: {diffConfig.DistancePrecision.Format(result.PredictedPosition.Y)} meters");
            Console.WriteLine($"  Z: {diffConfig.DistancePrecision.Format(result.PredictedPosition.Z)} meters");
            Console.WriteLine($"  Full coordinates: {diffConfig.FormatVector3(result.PredictedPosition)}\n");

            Console.WriteLine("Predicted Target State:");
            Console.WriteLine($"  Target Range (3D distance): {FormatDistanceWithPrecision(result.PredictedDistance, diffConfig)}");
            Console.WriteLine($"  Elevation angle (to target): {diffConfig.FormatElevation(result.PredictedElevation)}");
            Console.WriteLine($"  Azimuth bearing (to target): {diffConfig.FormatAzimuth(result.PredictedAzimuth)}");
            Console.WriteLine($"  (These angles are derived from Cartesian coordinates above)\n");

            Console.WriteLine("Distance Traveled (motion during interval):");
            Console.WriteLine($"  ΔX: {diffConfig.DistancePrecision.Format(result.DistanceTraveled.X)} meters");
            Console.WriteLine($"  ΔY: {diffConfig.DistancePrecision.Format(result.DistanceTraveled.Y)} meters");
            Console.WriteLine($"  ΔZ: {diffConfig.DistancePrecision.Format(result.DistanceTraveled.Z)} meters");
            Console.WriteLine($"  Total displacement: {FormatDistanceWithPrecision(result.DistanceTraveled.Magnitude, diffConfig)}\n");

            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("USE THIS INFORMATION FOR FIRING SOLUTION:");
            Console.WriteLine("  • Target Range: Maximum distance your projectile must travel");
            Console.WriteLine("  • Elevation angle: Recommended elevation for this target position");
            Console.WriteLine("  • Azimuth bearing: Recommended azimuth for this target position");
            Console.WriteLine("  • Adjust launch delay to account for target movement during flight time");
        }
    }
}