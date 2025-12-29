using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// FIRE SIMULATOR - SIMULATION MODE
    /// 
    /// Allows players to test firing solutions without consequences or automation.
    /// Players can iterate on parameters and see raw ballistic data for each test.
    /// 
    /// PRECISION: All formatting delegates to DifficultyConfig (single source of truth).
    /// </summary>
    public static class FireSimulator
    {
        private const float GRAVITY = 9.81f;

        // Store last test parameters for "modify and retry" feature
        // NOTE: These are initialized to -1 to indicate "not set" - will use gun's max velocity on first use
        private static double lastTestDelayTime = 5.0;
        private static double lastTestElevation = 30.0;
        private static double lastTestAzimuth = 0.0;
        private static double lastTestVelocity = -1.0;  // -1 means "use max available"
        private static bool hasLastTest = false;

        // ====================================================================
        // MAIN INTERFACE
        // ====================================================================

        /// <summary>
        /// Launch the Fire Simulator with difficulty-aware precision.
        /// Players can test multiple firing solutions in TEST MODE before committing.
        /// Supports modifying and re-running the previous test.
        /// </summary>
        public static bool ShowSimulatorTool(
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            float projectileMass,
            float muzzleVelocity,
            GameDifficulty difficulty = GameDifficulty.RealSpacegunSimulator)
        {
            var diffConfig = DifficultyConfig.GetConfig(difficulty);
            bool inSimulator = true;
            bool readyToCommit = false;

            // Initialize lastTestVelocity to max available if not set
            if (lastTestVelocity < 0 || lastTestVelocity > muzzleVelocity)
            {
                lastTestVelocity = muzzleVelocity;
            }

            while (inSimulator && !readyToCommit)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║             FIRE SIMULATOR - TEST MODE                    ║");
                Console.WriteLine("║    (Test firing solutions without consequences)            ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine($"Difficulty: {diffConfig.DisplayName}");
                Console.WriteLine("PRECISION REQUIREMENTS:");
                Console.WriteLine(diffConfig.GetPrecisionSummary());
                Console.WriteLine();

                Console.WriteLine("=== ENTERING TEST PARAMETERS ===");
                Console.WriteLine("(No firing will occur - TEST MODE ONLY)\n");

                double testDelayTime = GetPlayerTimeInput("Launch delay time (seconds)", lastTestDelayTime, diffConfig);
                double testElevation = GetPlayerElevationInput("Elevation angle (-90 to 90 degrees)", lastTestElevation, diffConfig);
                double testAzimuth = GetPlayerAzimuthInput("Azimuth bearing (0-360 degrees, 0=North)", lastTestAzimuth, diffConfig);
                double testVelocity = GetPlayerVelocityInput($"Launch velocity (0-{diffConfig.VelocityPrecision.Format(muzzleVelocity)} m/s)", lastTestVelocity, muzzleVelocity, diffConfig);

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
                    maxFlightTime,
                    diffConfig);

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
                        inSimulator = true;
                        break;

                    case "M":
                        if (hasLastTest)
                        {
                            inSimulator = true;
                        }
                        else
                        {
                            Console.WriteLine("No previous test to modify.\n");
                            System.Threading.Thread.Sleep(1000);
                        }
                        break;

                    case "C":
                        inSimulator = false;
                        readyToCommit = true;
                        break;

                    case "Q":
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

        private static double CalculateMaxFlightTime(double elevationDegrees, double velocity)
        {
            double elevationRad = elevationDegrees * Math.PI / 180.0;
            double verticalVelocity = velocity * Math.Sin(elevationRad);

            if (verticalVelocity > 0)
                return 2.0 * verticalVelocity / 9.81;
            else if (verticalVelocity < 0)
                return -verticalVelocity / 9.81;
            else
                return 30.0;
        }

        private static Vector3 CalculateProjectilePosition(
            double launchVelocity,
            double elevationDegrees,
            double azimuthDegrees,
            double flightTime)
        {
            // Use canonical calculation to prevent formula drift.
            // BallisticsCalculator signature: (flightTime, launchVelocity, elevationDeg, azimuthDeg)
            return BallisticsCalculator.CalculateProjectilePositionStatic(flightTime, launchVelocity, elevationDegrees, azimuthDegrees);
        }

        /// <summary>
        /// Display side-by-side trajectory simulation comparing projectile and target.
        /// Uses DifficultyConfig for all precision formatting.
        /// </summary>
        private static void DisplaySimulationResults(
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            double launchDelayTime,
            double elevationDegrees,
            double azimuthDegrees,
            double launchVelocity,
            float projectileMass,
            double maxFlightTime,
            DifficultyConfig diffConfig)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              SIMULATION RESULTS                           ║");
            Console.WriteLine("║         (Projectile vs Target Trajectory)                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== TEST PARAMETERS ===");
            Console.WriteLine($"Launch Delay: {diffConfig.FormatLaunchDelay(launchDelayTime)} | Elevation: {diffConfig.FormatElevation(elevationDegrees)} | Azimuth: {diffConfig.FormatAzimuth(azimuthDegrees)}");
            Console.WriteLine($"Velocity: {diffConfig.FormatVelocity(launchVelocity)} | Projectile Mass: {diffConfig.MassPrecision.Format(projectileMass)} kg");
            Console.WriteLine($"Max Flight Time: {diffConfig.FormatLaunchDelay(maxFlightTime)}\n");

            Console.WriteLine("=== TIME INTERVAL ANALYSIS ===");
            Console.WriteLine("Time │ Projectile Position        │ Projectile Range │ Target Position           │ Target Range");
            Console.WriteLine("─────┼────────────────────────────┼──────────────────┼───────────────────────────┼──────────────");

            double fineTimeStep = 0.001;
            double displayInterval = 2.0;

            for (double t = 0; t <= maxFlightTime + launchDelayTime + 5.0; t += fineTimeStep)
            {
                double projectileTime = t - launchDelayTime;
                Vector3 projectilePos;
                double projectileRange;

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

                Vector3 targetPos = enemyPosition + (enemyVelocity * t);
                double targetRange = targetPos.Magnitude;

                if (Math.Abs(t % displayInterval - fineTimeStep) < fineTimeStep || t < fineTimeStep)
                {
                    string projectilePosStr = projectileTime >= 0
                        ? diffConfig.FormatVector3(projectilePos)
                        : "(not fired yet)";

                    string targetPosStr = diffConfig.FormatVector3(targetPos);

                    Console.Write($"{diffConfig.LaunchDelayPrecision.Format(t)}s │ {projectilePosStr,-26} │ {diffConfig.FormatDistance(projectileRange),15} │ {targetPosStr,-25} │ {diffConfig.FormatDistance(targetRange),12}\n");
                }

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
        // INPUT HELPERS (with difficulty-aware precision)
        // ====================================================================

        private static double GetPlayerTimeInput(string prompt, double defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.FormatLaunchDelay(defaultValue)}]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (double.TryParse(input, out double time) && time >= 0)
                    return time;

                Console.WriteLine("Invalid input. Please enter a non-negative time value in seconds.\n");
            }
        }

        private static double GetPlayerElevationInput(string prompt, double defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.FormatElevation(defaultValue)}]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (double.TryParse(input, out double angle) && angle >= -90 && angle <= 90)
                    return angle;

                Console.WriteLine("Invalid input. Please enter an angle between -90 and 90 degrees.\n");
            }
        }

        private static double GetPlayerAzimuthInput(string prompt, double defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.FormatAzimuth(defaultValue)}]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (double.TryParse(input, out double bearing) && bearing >= 0 && bearing < 360)
                    return bearing;

                Console.WriteLine("Invalid input. Please enter a bearing between 0 and 360 degrees.\n");
            }
        }

        private static double GetPlayerVelocityInput(string prompt, double defaultValue, float muzzleVelocity, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.VelocityPrecision.Format(defaultValue)} m/s]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (double.TryParse(input, out double velocity) && velocity >= 0 && velocity <= muzzleVelocity)
                    return velocity;

                Console.WriteLine($"Invalid input. Please enter a velocity between 0 and {diffConfig.VelocityPrecision.Format(muzzleVelocity)} m/s.\n");
            }
        }
    }
}