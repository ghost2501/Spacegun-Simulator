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

                Console.WriteLine("=== CALCULATE FUTURE POSITION ===");
                Console.WriteLine("Enter time offset from now (seconds).");
                Console.WriteLine("(Motion Computer will calculate where target will be at that time)\n");

                Console.Write("Time offset (seconds): ");
                string input = Console.ReadLine() ?? "0";

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase) || 
                    input.Equals("X", StringComparison.OrdinalIgnoreCase))
                {
                    inTool = false;
                    break;
                }

                if (!float.TryParse(input, out float timeOffset) || timeOffset < 0)
                {
                    Console.WriteLine("\n✗ Invalid input. Please enter a non-negative time value in seconds.\n");
                    System.Threading.Thread.Sleep(1500);
                    continue;
                }

                // Calculate future position
                var result = CalculateMotionAtTime(currentPosition, currentVelocity, timeOffset);

                // Display results
                Console.WriteLine();
                DisplayMotionComputerResult(result, timeOffset);

                Console.WriteLine("\nOptions:");
                Console.WriteLine("[Enter] Try another time");
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
        /// Represents the result of a motion prediction calculation.
        /// </summary>
        public class MotionPredictionResult
        {
            public float TimeOffset { get; set; }
            public Vector3 PredictedPosition { get; set; }
            public float PredictedDistance { get; set; }
            public float PredictedElevation { get; set; }
            public float PredictedAzimuth { get; set; }
            public Vector3 DistanceTraveled { get; set; }
        }

        /// <summary>
        /// Calculate target position at a future time using linear motion assumption.
        /// Formula: Position(t) = CurrentPosition + Velocity × t
        /// </summary>
        public static MotionPredictionResult CalculateMotionAtTime(
            Vector3 currentPosition,
            Vector3 currentVelocity,
            float timeOffsetSeconds)
        {
            // Linear extrapolation: future position = current position + (velocity × time)
            Vector3 displacement = currentVelocity * timeOffsetSeconds;
            Vector3 futurePosition = currentPosition + displacement;

            // Calculate range (distance from origin)
            float futureDistance = futurePosition.Magnitude;

            // Calculate elevation angle
            // Elevation: atan2(Z, sqrt(X² + Y²))
            // Range 0° = horizon plane, 90° = zenith, -90° = nadir
            float horizontalDistance = (float)Math.Sqrt(futurePosition.X * futurePosition.X + 
                                                        futurePosition.Y * futurePosition.Y);
            float elevationRad = (float)Math.Atan2(futurePosition.Z, horizontalDistance);
            float elevationDeg = elevationRad * 180f / (float)Math.PI;

            // Calculate azimuth bearing
            // Azimuth: atan2(Y, X), converted to 0-360 range
            // 0° = North (+X direction), 90° = East (+Y direction)
            float azimuthRad = (float)Math.Atan2(futurePosition.Y, futurePosition.X);
            float azimuthDeg = azimuthRad * 180f / (float)Math.PI;
            if (azimuthDeg < 0)
                azimuthDeg += 360f;

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

        // ====================================================================
        // DISPLAY FORMATTING
        // ====================================================================

        /// <summary>
        /// Display motion computer calculation results in detail.
        /// </summary>
        private static void DisplayMotionComputerResult(MotionPredictionResult result, float timeOffset)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          MOTION COMPUTER CALCULATION RESULTS              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== PREDICTION AT T+{timeOffset:F2} SECONDS ===\n");

            Console.WriteLine("Predicted Position (Cartesian):");
            Console.WriteLine($"  X: {result.PredictedPosition.X:F1} meters");
            Console.WriteLine($"  Y: {result.PredictedPosition.Y:F1} meters");
            Console.WriteLine($"  Z: {result.PredictedPosition.Z:F1} meters");
            Console.WriteLine($"  Full coordinates: {result.PredictedPosition}\n");

            Console.WriteLine("Predicted Target State:");
            Console.WriteLine($"  Distance from origin: {GameConstants.FormatDistance(result.PredictedDistance)}");
            Console.WriteLine($"  Elevation angle: {result.PredictedElevation:F1}°");
            Console.WriteLine($"  Azimuth bearing: {result.PredictedAzimuth:F1}°\n");

            Console.WriteLine("Distance Traveled (motion during interval):");
            Console.WriteLine($"  ΔX: {result.DistanceTraveled.X:F1} meters");
            Console.WriteLine($"  ΔY: {result.DistanceTraveled.Y:F1} meters");
            Console.WriteLine($"  ΔZ: {result.DistanceTraveled.Z:F1} meters");
            Console.WriteLine($"  Total displacement: {GameConstants.FormatDistance(result.DistanceTraveled.Magnitude)}\n");

            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("USE THIS INFORMATION TO:");
            Console.WriteLine("  • Understand how target moves over different time windows");
            Console.WriteLine("  • Compare positions at different launch delay times");
            Console.WriteLine("  • Estimate elevation/azimuth angles for your solution");
            Console.WriteLine("  • Verify target will be within engagement parameters");
        }
    }
}