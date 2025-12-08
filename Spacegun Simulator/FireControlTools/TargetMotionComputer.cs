namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// TARGET MOTION COMPUTER
    /// 
    /// Mechanical fire control aid simulating mid-20th century motion prediction systems.
    /// Allows players to explore target trajectories by calculating predicted position
    /// at various future times without automated targeting or recommendations.
    /// 
    /// PURPOSE: Exploratory calculation tool, not a solution provider.
    /// - Accepts a time offset from engagement start
    /// - Calculates where target will be at that time using linear extrapolation
    /// - Shows position in Cartesian coordinates, range, elevation, and azimuth
    /// - Provides NO evaluation of whether predictions are "good" or "bad"
    /// - Players use results to inform their own firing solution decisions
    /// 
    /// DESIGN PRINCIPLE: Never auto-solves. Player queries the tool as many times
    /// as desired to explore different time windows and trajectories.
    /// </summary>
    public static class TargetMotionComputer
    {
        // ====================================================================
        // MAIN INTERFACE
        // ====================================================================

        /// <summary>
        /// Launch the Target Motion Computer interactive tool.
        /// Player can test multiple time offsets in a loop.
        /// </summary>
        public static void ShowMotionComputerTool(Vector3 currentPosition, Vector3 currentVelocity)
        {
            bool inTool = true;

            while (inTool)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║            MOTION COMPUTER - TRAJECTORY SOLVER            ║");
                Console.WriteLine("║     Predict target position at future times (linear)      ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine("=== CURRENT TARGET STATE (T=0) ===");
                Console.WriteLine($"Position: ({currentPosition.X:F1}, {currentPosition.Y:F1}, {currentPosition.Z:F1})");
                Console.WriteLine($"Distance from origin: {currentPosition.Magnitude:F0} meters");
                Console.WriteLine($"Velocity: ({currentVelocity.X:F1}, {currentVelocity.Y:F1}, {currentVelocity.Z:F1}) m/s");
                Console.WriteLine($"Speed: {currentVelocity.Magnitude:F0} m/s\n");

                // ENHANCED: Display automatic timeline T+0 to T+20
                Console.WriteLine("=== TARGET RANGE TIMELINE (T+0s to T+20s) ===\n");
                DisplayRangeTimeline(currentPosition, currentVelocity);

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
                    continue; // Loop back to show timeline again
                }

                // Parse time input (supports decimals like 3.27)
                if (!double.TryParse(input, out double timeOffset) || timeOffset < 0)  // CHANGED: float to double
                {
                    Console.WriteLine("\n✗ Invalid input. Please enter a non-negative time value or [Q] to quit.\n");
                    System.Threading.Thread.Sleep(1500);
                    continue;
                }

                // Calculate future position
                var result = CalculateMotionAtTime(currentPosition, currentVelocity, timeOffset);

                // Display results
                Console.WriteLine();
                DisplayMotionComputerResult(result, timeOffset);

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
                else if (choice.Equals("R", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Loop back to show timeline
                }
                // Otherwise, loop continues to show main menu again
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
            double timeOffsetSeconds)  // CHANGED: float to double
        {
            // Linear extrapolation: future position = current position + (velocity × time)
            Vector3 displacement = currentVelocity * timeOffsetSeconds;
            Vector3 futurePosition = currentPosition + displacement;

            // Calculate range (distance from origin)
            double futureDistance = futurePosition.Magnitude;  // CHANGED: float to double

            // Calculate elevation angle
            // Elevation: atan2(Z, sqrt(X² + Y²))
            // Range 0° = horizon plane, 90° = zenith, -90° = nadir
            double horizontalDistance = Math.Sqrt(futurePosition.X * futurePosition.X +
                                                   futurePosition.Y * futurePosition.Y);  // CHANGED: float to double
            double elevationRad = Math.Atan2(futurePosition.Z, horizontalDistance);  // CHANGED: float to double
            double elevationDeg = elevationRad * 180.0 / Math.PI;  // CHANGED: float to double

            // Calculate azimuth bearing
            // Azimuth: atan2(X, Y), converted to 0-360 range
            // 0° = North (+Y direction), 90° = East (+X direction)
            double azimuthRad = Math.Atan2(futurePosition.X, futurePosition.Y);  // CHANGED: float to double
            double azimuthDeg = azimuthRad * 180.0 / Math.PI;  // CHANGED: float to double
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
        /// Display automatic range timeline from T+0 to T+20 seconds in 1-second intervals.
        /// Helps players quickly identify when target enters/exits gun range.
        /// </summary>
        private static void DisplayRangeTimeline(Vector3 startPosition, Vector3 velocity)
        {
            Console.WriteLine("Time  │ Target Range      │ Status");
            Console.WriteLine("──────┼───────────────────┼─────────────────────────");

            float gunRange = 1_500_000f; // 1.5 Mm

            for (int t = 0; t <= 20; t++)
            {
                var result = CalculateMotionAtTime(startPosition, velocity, t);  // Now accepts double
                bool inRange = result.PredictedDistance <= gunRange;
                string status = inRange ? "✓ IN RANGE" : "✗ out range";

                Console.WriteLine($"{t,2}s  │ {GameConstants.FormatDistance(result.PredictedDistance):>17} │ {status}");
            }
        }

        /// <summary>
        /// Represents the result of a motion prediction calculation.
        /// </summary>
        public class MotionPredictionResult
        {
            public double TimeOffset { get; set; }  // CHANGED: float to double
            public Vector3 PredictedPosition { get; set; }
            public double PredictedDistance { get; set; }  // CHANGED: float to double
            public double PredictedElevation { get; set; }  // CHANGED: float to double
            public double PredictedAzimuth { get; set; }  // CHANGED: float to double
            public Vector3 DistanceTraveled { get; set; }
        }

        /// <summary>
        /// Display motion computer calculation results in detail.
        /// </summary>
        private static void DisplayMotionComputerResult(MotionPredictionResult result, double timeOffset)  // CHANGED: float to double
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          MOTION COMPUTER CALCULATION RESULTS              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== PREDICTION AT T+{result.TimeOffset:F2} SECONDS ===\n");

            Console.WriteLine("Predicted Position (Cartesian):");
            Console.WriteLine($"  X: {result.PredictedPosition.X:F1} meters");
            Console.WriteLine($"  Y: {result.PredictedPosition.Y:F1} meters");
            Console.WriteLine($"  Z: {result.PredictedPosition.Z:F1} meters");
            Console.WriteLine($"  Full coordinates: {result.PredictedPosition}\n");

            Console.WriteLine("Predicted Target State:");
            Console.WriteLine($"  Target Range (3D distance): {GameConstants.FormatDistance(result.PredictedDistance)}");
            Console.WriteLine($"  Elevation angle (to target): {result.PredictedElevation:F1}°");
            Console.WriteLine($"  Azimuth bearing (to target): {result.PredictedAzimuth:F1}°");
            Console.WriteLine($"  (These angles are derived from Cartesian coordinates above)\n");

            Console.WriteLine("Distance Traveled (motion during interval):");
            Console.WriteLine($"  ΔX: {result.DistanceTraveled.X:F1} meters");
            Console.WriteLine($"  ΔY: {result.DistanceTraveled.Y:F1} meters");
            Console.WriteLine($"  ΔZ: {result.DistanceTraveled.Z:F1} meters");
            Console.WriteLine($"  Total displacement: {GameConstants.FormatDistance(result.DistanceTraveled.Magnitude)}\n");

            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("USE THIS INFORMATION FOR FIRING SOLUTION:");
            Console.WriteLine("  • Target Range: Maximum distance your projectile must travel");
            Console.WriteLine("  • Elevation angle: Recommended elevation for this target position");
            Console.WriteLine("  • Azimuth bearing: Recommended azimuth for this target position");
            Console.WriteLine("  • If you fire at these angles, compare projectile vs target trajectories");
            Console.WriteLine("  • Adjust launch delay to account for target movement during flight time");
        }
    }
}