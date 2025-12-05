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

        // ====================================================================
        // MAIN INTERFACE
        // ====================================================================

        /// <summary>
        /// Launch the Fire Simulator.
        /// Players can test multiple firing solutions in TEST MODE before committing.
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

                float testDelayTime = GetPlayerTimeInput("Launch delay time (seconds): ");
                float testElevation = GetPlayerElevationInput("Elevation angle (-90 to 90 degrees): ");
                float testAzimuth = GetPlayerAzimuthInput("Azimuth bearing (0-360 degrees): ");
                float testVelocity = GetPlayerVelocityInput($"Launch velocity (0-{muzzleVelocity:F0} m/s): ");

                Console.WriteLine();

                // Test the scenario
                float maxFlightTime = CalculateMaxFlightTime(testElevation, testVelocity);
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
                Console.WriteLine("[C] Commit these parameters and FIRE");
                Console.WriteLine("[Q] Cancel and return to menu\n");
                Console.Write("Select: ");
                string choice = Console.ReadLine() ?? "T";

                switch (choice.ToUpper())
                {
                    case "T":
                        // Loop back to test again
                        inSimulator = true;
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
        private static float CalculateMaxFlightTime(float elevationDegrees, float velocity)
        {
            float elevationRad = elevationDegrees * (float)Math.PI / 180f;
            float verticalVelocity = velocity * (float)Math.Sin(elevationRad);

            // Time to return to z=0: t = 2*v*sin(θ) / g
            if (verticalVelocity > 0)
                return 2f * verticalVelocity / GRAVITY;
            else if (verticalVelocity < 0)
                return -verticalVelocity / GRAVITY;  // Only descending
            else
                return 30f;  // Arbitrary max for flat trajectory
        }

        /// <summary>
        /// Display side-by-side trajectory simulation comparing projectile and target.
        /// </summary>
        private static void DisplaySimulationResults(
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            float launchDelayTime,
            float elevationDegrees,
            float azimuthDegrees,
            float launchVelocity,
            float projectileMass,
            float maxFlightTime)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              SIMULATION RESULTS                           ║");
            Console.WriteLine("║         (Projectile vs Target Trajectory)                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== TEST PARAMETERS ===");
            Console.WriteLine($"Launch Delay: {launchDelayTime:F2}s | Elevation: {elevationDegrees:F1}° | Azimuth: {azimuthDegrees:F1}°");
            Console.WriteLine($"Velocity: {launchVelocity:F0} m/s | Projectile Mass: {projectileMass:F1} kg");
            Console.WriteLine($"Max Flight Time: {maxFlightTime:F2} seconds\n");

            // Calculate trajectory data for key time intervals
            Console.WriteLine("=== TIME INTERVAL ANALYSIS ===");
            Console.WriteLine("Time │ Projectile Position        │ Projectile Range │ Target Position           │ Target Range");
            Console.WriteLine("─────┼────────────────────────────┼──────────────────┼───────────────────────────┼──────────────");

            float timeStep = Math.Max(1f, maxFlightTime / 10f);  // 10-point sample or 1-second intervals

            for (float t = 0; t <= maxFlightTime + launchDelayTime + 5f; t += timeStep)
            {
                // Projectile position (starts at T = launchDelayTime)
                float projectileTime = t - launchDelayTime;
                Vector3 projectilePos;
                float projectileRange;

                if (projectileTime >= 0)
                {
                    projectilePos = CalculateProjectilePosition(launchVelocity, elevationDegrees, azimuthDegrees, projectileTime);
                    projectileRange = projectilePos.Magnitude;
                }
                else
                {
                    projectilePos = Vector3.Zero;
                    projectileRange = 0;
                }

                // Target position at time T (from launch delay onset)
                Vector3 targetPos = enemyPosition + (enemyVelocity * t);
                float targetRange = targetPos.Magnitude;

                // Format output
                string projectilePosStr = projectileTime >= 0
                    ? $"({projectilePos.X:F0}, {projectilePos.Y:F0}, {projectilePos.Z:F0})"
                    : "(not fired yet)";

                string targetPosStr = $"({targetPos.X:F0}, {targetPos.Y:F0}, {targetPos.Z:F0})";

                Console.Write($"{t:F1}s │ {projectilePosStr:,-26} │ {GameConstants.FormatDistance(projectileRange):>15} │ {targetPosStr:,-25} │ {GameConstants.FormatDistance(targetRange):>12}\n");
            }

            Console.WriteLine();
            Console.WriteLine("═════════════════════════════════════════════════════════════════\n");
            Console.WriteLine("INTERPRETATION NOTES:");
            Console.WriteLine("  • Compare projectile and target positions at each time");
            Console.WriteLine("  • Look for where positions are closest together");
            Console.WriteLine("  • Intercept occurs when ranges are approximately equal");
            Console.WriteLine("  • Test different delay times if trajectories don't converge");
        }

        /// <summary>
        /// Calculate projectile position using ballistic equations.
        /// </summary>
        private static Vector3 CalculateProjectilePosition(
            float launchVelocity,
            float elevationDegrees,
            float azimuthDegrees,
            float flightTime)
        {
            float elevationRad = elevationDegrees * (float)Math.PI / 180f;
            float azimuthRad = azimuthDegrees * (float)Math.PI / 180f;

            // Vertical component
            float verticalVelocity = launchVelocity * (float)Math.Sin(elevationRad);
            float z = verticalVelocity * flightTime - 0.5f * GRAVITY * flightTime * flightTime;

            // Horizontal component
            float horizontalVelocity = launchVelocity * (float)Math.Cos(elevationRad);
            float horizontalDistance = horizontalVelocity * flightTime;

            // Cartesian decomposition
            float x = horizontalDistance * (float)Math.Cos(azimuthRad);
            float y = horizontalDistance * (float)Math.Sin(azimuthRad);

            return new Vector3(x, y, z);
        }

        // ====================================================================
        // INPUT HELPERS
        // ====================================================================

        /// <summary>
        /// Get player input for launch delay time.
        /// </summary>
        private static float GetPlayerTimeInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float time) && time >= 0)
                {
                    return time;
                }

                Console.WriteLine("Invalid input. Please enter a non-negative time value in seconds.\n");
            }
        }

        /// <summary>
        /// Get player input for elevation angle.
        /// </summary>
        private static float GetPlayerElevationInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float angle) && angle >= -90 && angle <= 90)
                {
                    return angle;
                }

                Console.WriteLine("Invalid input. Please enter an angle between -90 and 90 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for azimuth bearing.
        /// </summary>
        private static float GetPlayerAzimuthInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float bearing) && bearing >= 0 && bearing < 360)
                {
                    return bearing;
                }

                Console.WriteLine("Invalid input. Please enter a bearing between 0 and 360 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for launch velocity.
        /// </summary>
        private static float GetPlayerVelocityInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float velocity) && velocity >= 0)
                {
                    return velocity;
                }

                Console.WriteLine("Invalid input. Please enter a non-negative velocity value in m/s.\n");
            }
        }
    }
}