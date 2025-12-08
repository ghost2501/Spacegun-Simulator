namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// FIRE SIMULATOR - SIMULATION MODE
    /// 
    /// Allows players to test firing solutions without consequences or automation.
    /// Players can iterate on parameters and see raw ballistic data for each test.
    /// 
    /// PURPOSE: Safe testing environment for exploring solutions.
    /// - Accept launch parameters (delay, elevation, azimuth, velocity)
    /// - Simulate projectile and target trajectories over time
    /// - Display both trajectories separately (player judges intercept)
    /// - Allow unlimited testing iterations
    /// - No feedback, hints, or validation
    /// - Clear "TEST MODE" vs "FIRE FOR REAL" distinction
    /// 
    /// DESIGN PRINCIPLE: Show raw data only. Player interprets results.
    /// No auto-calculation of hits, misses, or suitability.
    /// </summary>
    public static class FireSimulator
    {
        private const float GRAVITY = 9.81f;

        // Store last test parameters for "modify and retry" feature
        private static double lastTestDelayTime = 5.0;
        private static double lastTestElevation = 30.0;
        private static double lastTestAzimuth = 0.0;
        private static double lastTestVelocity = 200_000.0;
        private static bool hasLastTest = false;

        // ====================================================================
        // MAIN INTERFACE
        // ====================================================================

        /// <summary>
        /// Launch the Fire Simulator.
        /// Players can test multiple firing solutions in TEST MODE before committing.
        /// Supports modifying and re-running the previous test.
        /// </summary>
        public static bool ShowSimulatorTool(
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            float projectileMass,
            float muzzleVelocity)
        {
            bool inSimulator = true;
            bool readyToCommit = false;

            while (inSimulator && !readyToCommit)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║             FIRE SIMULATOR - TEST MODE                    ║");
                Console.WriteLine("║    (Test firing solutions without consequences)            ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine("=== ENTERING TEST PARAMETERS ===");
                Console.WriteLine("(No firing will occur - TEST MODE ONLY)\n");

                double testDelayTime = GetPlayerTimeInput("Launch delay time (seconds)", lastTestDelayTime);
                double testElevation = GetPlayerElevationInput("Elevation angle (-90 to 90 degrees)", lastTestElevation);
                double testAzimuth = GetPlayerAzimuthInput("Azimuth bearing (0-360 degrees, 0=North)", lastTestAzimuth);
                double testVelocity = GetPlayerVelocityInput($"Launch velocity (0-{muzzleVelocity:F0} m/s)", lastTestVelocity, muzzleVelocity);

                // Store for later modification
                lastTestDelayTime = testDelayTime;
                lastTestElevation = testElevation;
                lastTestAzimuth = testAzimuth;
                lastTestVelocity = testVelocity;
                hasLastTest = true;

                Console.WriteLine();

                // Test the scenario
                double maxFlightTime = CalculateMaxFlightTime(testElevation, testVelocity);
                DisplaySimulationResults(
                    enemyPosition,
                    enemyVelocity,
                    testDelayTime,
                    testElevation,
                    testAzimuth,
                    testVelocity,
                    projectileMass,
                    maxFlightTime);

                // Loop options
                Console.WriteLine("\n=== TEST COMPLETE ===\n");
                Console.WriteLine("[T] Test different parameters");
                if (hasLastTest)
                {
                    Console.WriteLine("[M] Modify last test and run again");
                }
                Console.WriteLine("[C] Commit these parameters and FIRE");
                Console.WriteLine("[Q] Cancel and return to menu\n");
                Console.Write("Select: ");
                string choice = Console.ReadLine() ?? "T";

                switch (choice.ToUpper())
                {
                    case "T":
                        // Loop back to test again with fresh prompts
                        inSimulator = true;
                        break;

                    case "M":
                        if (hasLastTest)
                        {
                            // Continue to next iteration with stored values prepopulated
                            inSimulator = true;
                        }
                        else
                        {
                            Console.WriteLine("No previous test to modify.\n");
                            System.Threading.Thread.Sleep(1000);
                        }
                        break;

                    case "C":
                        // Player wants to use these parameters
                        inSimulator = false;
                        readyToCommit = true;
                        break;

                    case "Q":
                        // Return to menu without committing
                        inSimulator = false;
                        readyToCommit = false;
                        break;

                    default:
                        Console.WriteLine("Invalid selection.\n");
                        System.Threading.Thread.Sleep(1000);
                        continue;
                }
            }

            return readyToCommit;
        }

        // ====================================================================
        // SIMULATION ENGINE
        // ====================================================================

        /// <summary>
        /// Calculate maximum flight time before projectile descends to ground level.
        /// </summary>
        private static double CalculateMaxFlightTime(double elevationDegrees, double velocity)
        {
            double elevationRad = elevationDegrees * Math.PI / 180.0;
            double verticalVelocity = velocity * Math.Sin(elevationRad);

            // Time to return to z=0: t = 2*v*sin(θ) / g
            if (verticalVelocity > 0)
                return 2.0 * verticalVelocity / 9.81;
            else if (verticalVelocity < 0)
                return -verticalVelocity / 9.81;
            else
                return 30.0;  // Arbitrary max for flat trajectory
        }

        /// <summary>
        /// Calculate projectile position using ballistic equations.
        /// 
        /// PRECISION: Using double for all time calculations to maintain sub-meter accuracy.
        /// 
        /// COORDINATE SYSTEM (Right-Handed Standard):
        /// X-axis: East (positive) / West (negative)
        /// Y-axis: North (positive) / South (negative)
        /// Z-axis: Up (positive) / Down (negative)
        /// 
        /// AZIMUTH (bearing from North, clockwise - Standard Compass Convention):
        /// 0° = North (+Y)
        /// 90° = East (+X)
        /// 180° = South (-Y)
        /// 270° = West (-X)
        /// 
        /// AZIMUTH DECOMPOSITION (Cartesian):
        /// x(t) = r(t)·sin(φ)  where φ is azimuth from North clockwise
        /// y(t) = r(t)·cos(φ)
        /// This converts compass bearing to Cartesian coordinates correctly.
        /// </summary>
        private static Vector3 CalculateProjectilePosition(
            double launchVelocity,
            double elevationDegrees,
            double azimuthDegrees,
            double flightTime)
        {
            double elevationRad = elevationDegrees * Math.PI / 180.0;
            double azimuthRad = azimuthDegrees * Math.PI / 180.0;

            double verticalVelocity = launchVelocity * Math.Sin(elevationRad);
            double z = verticalVelocity * flightTime - 0.5 * 9.81 * flightTime * flightTime;

            double horizontalVelocity = launchVelocity * Math.Cos(elevationRad);
            double horizontalDistance = horizontalVelocity * flightTime;

            // Azimuth is measured clockwise from North (+Y axis)
            // x = r·sin(φ) points East when φ = 90°
            // y = r·cos(φ) points North when φ = 0°
            double x = horizontalDistance * Math.Sin(azimuthRad);
            double y = horizontalDistance * Math.Cos(azimuthRad);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Display side-by-side trajectory simulation comparing projectile and target.
        /// PRECISION: Fine time resolution (1ms) for accurate trajectory comparison.
        /// </summary>
        private static void DisplaySimulationResults(
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            double launchDelayTime,
            double elevationDegrees,
            double azimuthDegrees,
            double launchVelocity,
            float projectileMass,
            double maxFlightTime)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              SIMULATION RESULTS                           ║");
            Console.WriteLine("║         (Projectile vs Target Trajectory)                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== TEST PARAMETERS ===");
            Console.WriteLine($"Launch Delay: {launchDelayTime:F5}s | Elevation: {elevationDegrees:F1}° | Azimuth: {azimuthDegrees:F1}°");
            Console.WriteLine($"Velocity: {launchVelocity:F0} m/s | Projectile Mass: {projectileMass:F1} kg");
            Console.WriteLine($"Max Flight Time: {maxFlightTime:F5} seconds\n");

            // Calculate trajectory data for key time intervals
            Console.WriteLine("=== TIME INTERVAL ANALYSIS ===");
            Console.WriteLine("Time │ Projectile Position        │ Projectile Range │ Target Position           │ Target Range");
            Console.WriteLine("─────┼────────────────────────────┼──────────────────┼───────────────────────────┼──────────────");

            // PRECISION: Use FINE time resolution (0.001 second = 1ms)
            double fineTimeStep = 0.001;  // 1ms resolution for accuracy
            double displayInterval = 2.0;  // Display every 2 seconds for readability

            for (double t = 0; t <= maxFlightTime + launchDelayTime + 5.0; t += fineTimeStep)
            {
                // Projectile position (starts at T = launchDelayTime)
                double projectileTime = t - launchDelayTime;
                Vector3 projectilePos;
                double projectileRange;  // CHANGED: float to double

                if (projectileTime >= 0)
                {
                    projectilePos = CalculateProjectilePosition(launchVelocity, elevationDegrees, azimuthDegrees, projectileTime);
                    projectileRange = projectilePos.Magnitude;  // Now double
                }
                else
                {
                    projectilePos = Vector3.Zero;
                    projectileRange = 0;
                }

                // Target position at time T (from launch delay onset)
                Vector3 targetPos = enemyPosition + (enemyVelocity * t);
                double targetRange = targetPos.Magnitude;  // CHANGED: float to double

                // Only display every displayInterval seconds to avoid clutter
                if (Math.Abs(t % displayInterval - fineTimeStep) < fineTimeStep || t < fineTimeStep)
                {
                    // Format output
                    string projectilePosStr = projectileTime >= 0
                        ? $"({projectilePos.X:F0}, {projectilePos.Y:F0}, {projectilePos.Z:F0})"
                        : "(not fired yet)";

                    string targetPosStr = $"({targetPos.X:F0}, {targetPos.Y:F0}, {targetPos.Z:F0})";

                    Console.Write($"{t:F5}s │ {projectilePosStr:,-26} │ {GameConstants.FormatDistance(projectileRange):>15} │ {targetPosStr:,-25} │ {GameConstants.FormatDistance(targetRange):>12}\n");
                }

                // Early exit if projectile has clearly passed or is beyond reasonable range
                if (projectileTime > 30.0 || projectileRange > 3_000_000)
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("═════════════════════════════════════════════════════════════════\n");
            Console.WriteLine("INTERPRETATION NOTES:");
            Console.WriteLine("  • Compare projectile and target positions at each time");
            Console.WriteLine("  • Look for where positions are closest together");
            Console.WriteLine("  • Intercept occurs when ranges are approximately equal");
            Console.WriteLine("  • Test different delay times if trajectories don't converge");
        }

        // ====================================================================
        // INPUT HELPERS
        // ====================================================================

        /// <summary>
        /// Get player input for launch delay time.
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// </summary>
        private static double GetPlayerTimeInput(string prompt, double defaultValue)
        {
            while (true)
            {
                Console.Write($"{prompt} [{defaultValue:F5}s]: ");
                string input = Console.ReadLine() ?? "";

                // If empty, use default
                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (double.TryParse(input, out double time) && time >= 0)
                {
                    return time;
                }

                Console.WriteLine("Invalid input. Please enter a non-negative time value in seconds.\n");
            }
        }

        /// <summary>
        /// Get player input for elevation angle.
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// </summary>
        private static double GetPlayerElevationInput(string prompt, double defaultValue)
        {
            while (true)
            {
                Console.Write($"{prompt} [{defaultValue:F1}°]: ");
                string input = Console.ReadLine() ?? "";

                // If empty, use default
                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (double.TryParse(input, out double angle) && angle >= -90 && angle <= 90)
                {
                    return angle;
                }

                Console.WriteLine("Invalid input. Please enter an angle between -90 and 90 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for azimuth bearing.
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// 0° = North (+Y direction), 90° = East (+X direction)
        /// </summary>
        private static double GetPlayerAzimuthInput(string prompt, double defaultValue)
        {
            while (true)
            {
                Console.Write($"{prompt} [{defaultValue:F1}°]: ");
                string input = Console.ReadLine() ?? "";

                // If empty, use default
                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (double.TryParse(input, out double bearing) && bearing >= 0 && bearing < 360)
                {
                    return bearing;
                }

                Console.WriteLine("Invalid input. Please enter a bearing between 0 and 360 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for launch velocity.
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// </summary>
        private static double GetPlayerVelocityInput(string prompt, double defaultValue, float muzzleVelocity)
        {
            while (true)
            {
                Console.Write($"{prompt} [{defaultValue:F0} m/s]: ");
                string input = Console.ReadLine() ?? "";

                // If empty, use default
                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (double.TryParse(input, out double velocity) && velocity >= 0 && velocity <= muzzleVelocity)
                {
                    return velocity;
                }

                Console.WriteLine($"Invalid input. Please enter a velocity between 0 and {muzzleVelocity:F0} m/s.\n");
            }
        }
    }
}