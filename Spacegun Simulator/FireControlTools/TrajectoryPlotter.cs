namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// TRAJECTORY PLOTTER - BALLISTIC COMPUTER
    /// 
    /// Mechanical fire control aid simulating mid-20th century ballistic prediction systems.
    /// Allows players to test projectile trajectories based on launch parameters.
    /// 
    /// PRECISION: All formatting delegates to DifficultyConfig (single source of truth).
    /// </summary>
    public static class TrajectoryPlotter
    {
        private const float GRAVITY = 9.81f;

        // Store last test parameters for "modify and retry" feature
        // NOTE: These are reset to appropriate values based on difficulty when tool starts
        private static float lastLaunchVelocity = 200_000f;
        private static float lastElevationDegrees = 45f;
        private static float lastAzimuthDegrees = 0f;
        private static float lastFlightTime = 10f;
        private static bool hasLastTest = false;

        // ====================================================================
        // MAIN INTERFACE
        // ====================================================================

        /// <summary>
        /// Launch the Trajectory Plotter interactive tool.
        /// Uses DifficultyConfig for all precision formatting.
        ///
        /// NOTE: Accepts optional rendering helpers so the caller (ConsoleUI) can supply
        /// a ScreenLayout and raw/indented writers to render the boxed header consistently.
        /// If those are not provided the method falls back to the inline boxed header.
        /// </summary>
        internal static void ShowTrajectoryPlotterTool(
            GameDifficulty difficulty = GameDifficulty.RealSpacegunSimulator,
            ScreenLayout? layout = null,
            TextWriter? originalOut = null,
            TextWriter? indentWriter = null,
            int globalIndent = 0)
        {
            var diffConfig = DifficultyConfig.GetConfig(difficulty);
            bool inTool = true;

            // Initialize defaults based on difficulty mode
            if (!hasLastTest)
            {
                if (diffConfig.IsTutorialMode)
                {
                    // Tutorial: Use potato cannon specs
                    lastLaunchVelocity = (float)DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs;  // 50 m/s
                    lastElevationDegrees = 45f;
                    lastAzimuthDegrees = 0f;
                    lastFlightTime = 2f;  // Shorter flight time for tutorial ranges
                }
                else
                {
                    // Standard game: Use high-velocity defaults
                    lastLaunchVelocity = 200_000f;
                    lastElevationDegrees = 45f;
                    lastAzimuthDegrees = 0f;
                    lastFlightTime = 10f;
                }
            }

            while (inTool)
            {
                // Prefer centralized layout rendering when provided by caller.
                if (layout != null)
                {
                    var centerLines = new System.Collections.Generic.List<string>
                    {
                        "╔═══════════════════════════════════════════════════════════╗",
                        "║          TRAJECTORY PLOTTER - BALLISTIC COMPUTER          ║",
                        "║     Calculate projectile position at flight time (T)      ║",
                        "╚═══════════════════════════════════════════════════════════╝",
                        string.Empty
                    };

                    try
                    {
                        layout.RenderFrame(centerLines, originalOut, indentWriter ?? Console.Out, globalIndent, noOffset: true);
                    }
                    catch
                    {
                        // fallback to inline rendering
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║          TRAJECTORY PLOTTER - BALLISTIC COMPUTER          ║");
                        Console.WriteLine("║     Calculate projectile position at flight time (T)      ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
                    }
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║          TRAJECTORY PLOTTER - BALLISTIC COMPUTER          ║");
                    Console.WriteLine("║     Calculate projectile position at flight time (T)      ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
                }

                Console.WriteLine($"Difficulty: {diffConfig.DisplayName}");
                Console.WriteLine("PRECISION REQUIREMENTS:");
                Console.WriteLine(diffConfig.GetPrecisionSummary());
                Console.WriteLine();

                Console.WriteLine("=== LAUNCH PARAMETERS ===");
                Console.WriteLine("Enter projectile launch parameters:\n");

                float launchVelocity = GetPlayerVelocityInput("Launch velocity (m/s)", lastLaunchVelocity, diffConfig);
                float elevationDegrees = GetPlayerElevationInput("Elevation angle (-90 to 90 degrees)", lastElevationDegrees, diffConfig);
                float azimuthDegrees = GetPlayerAzimuthInput("Azimuth bearing (0-360 degrees, 0=North)", lastAzimuthDegrees, diffConfig);

                Console.WriteLine();
                float flightTime = GetPlayerFlightTimeInput("Flight time to check (seconds)", lastFlightTime, diffConfig);

                // Store for later modification
                lastLaunchVelocity = launchVelocity;
                lastElevationDegrees = elevationDegrees;
                lastAzimuthDegrees = azimuthDegrees;
                lastFlightTime = flightTime;
                hasLastTest = true;

                // Calculate trajectory
                var result = CalculateTrajectory(launchVelocity, elevationDegrees, azimuthDegrees, flightTime);

                // Display results
                Console.WriteLine();
                DisplayTrajectoryResult(result, diffConfig);

                // Menu options
                Console.WriteLine("\nOptions:");
                Console.WriteLine("[Enter] Try a new trajectory");
                if (hasLastTest)
                {
                    Console.WriteLine("[M] Modify last test and run again");
                }
                Console.WriteLine("[Q] Quit Trajectory Plotter\n");
                Console.Write("Select: ");
                string choice = Console.ReadLine() ?? "";

                if (choice.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    inTool = false;
                }
                else if (choice.Equals("M", StringComparison.OrdinalIgnoreCase) && hasLastTest)
                {
                    continue;
                }
            }
        }

        // ====================================================================
        // CALCULATION ENGINE
        // ====================================================================

        public class TrajectoryResult
        {
            public float LaunchVelocity { get; set; }
            public float ElevationAngle { get; set; }
            public float AzimuthAngle { get; set; }
            public float FlightTime { get; set; }
            public Vector3 ProjectilePosition { get; set; }
            public float RangeFromOrigin { get; set; }
            public float GravitationalDrop { get; set; }
            public float MaxAltitudeReached { get; set; }
            public float TimeToMaxAltitude { get; set; }
            public float HorizontalDistance { get; set; }
        }

        public static TrajectoryResult CalculateTrajectory(
            float launchVelocity,
            float elevationDegrees,
            float azimuthDegrees,
            float flightTime)
        {
            // Use canonical projectile position calculation to keep formulas centralized.
            var pos = BallisticsCalculator.CalculateProjectilePositionStatic(flightTime, launchVelocity, elevationDegrees, azimuthDegrees);

            // Convert to floats for the result object (preserve existing API)
            float xPosition = (float)pos.X;
            float yPosition = (float)pos.Y;
            float zPosition = (float)pos.Z;

            // Vertical velocity for drop/time-to-max-altitude calculations
            float elevationRad = elevationDegrees * (float)Math.PI / 180f;
            float verticalVelocity = launchVelocity * (float)Math.Sin(elevationRad);

            float gravitationalDrop = 0.5f * GRAVITY * flightTime * flightTime;

            float horizontalVelocity = launchVelocity * (float)Math.Cos(elevationRad);
            float horizontalRange = horizontalVelocity * flightTime;

            float rangeFromOrigin = (float)Math.Sqrt(xPosition * xPosition + yPosition * yPosition + zPosition * zPosition);

            float timeToMaxAltitude = Math.Max(0f, verticalVelocity / GRAVITY);
            float maxAltitude = verticalVelocity * timeToMaxAltitude - 0.5f * GRAVITY * timeToMaxAltitude * timeToMaxAltitude;
            maxAltitude = Math.Max(0f, maxAltitude);

            return new TrajectoryResult
            {
                LaunchVelocity = launchVelocity,
                ElevationAngle = elevationDegrees,
                AzimuthAngle = azimuthDegrees,
                FlightTime = flightTime,
                ProjectilePosition = new Vector3(xPosition, yPosition, zPosition),
                RangeFromOrigin = rangeFromOrigin,
                GravitationalDrop = gravitationalDrop,
                MaxAltitudeReached = maxAltitude,
                TimeToMaxAltitude = timeToMaxAltitude,
                HorizontalDistance = horizontalRange
            };
        }

        // ====================================================================
        // DISPLAY FORMATTING
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
        /// Display trajectory calculation results in detail.
        /// Uses DifficultyConfig for all precision formatting.
        /// </summary>
        private static void DisplayTrajectoryResult(TrajectoryResult result, DifficultyConfig diffConfig)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          TRAJECTORY PLOTTER CALCULATION RESULTS            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== INPUT PARAMETERS ===");
            Console.WriteLine($"Launch Velocity: {diffConfig.FormatVelocity(result.LaunchVelocity)}");
            Console.WriteLine($"Elevation Angle: {diffConfig.FormatElevation(result.ElevationAngle)}");
            Console.WriteLine($"Azimuth Bearing: {diffConfig.FormatAzimuth(result.AzimuthAngle)}");
            Console.WriteLine($"Flight Time: {diffConfig.FormatLaunchDelay(result.FlightTime)}\n");

            Console.WriteLine($"=== PROJECTILE POSITION AT T+{diffConfig.LaunchDelayPrecision.Format(result.FlightTime)}s ===\n");

            Console.WriteLine("Cartesian Coordinates:");
            Console.WriteLine($"  X: {diffConfig.DistancePrecision.Format(result.ProjectilePosition.X)} meters");
            Console.WriteLine($"  Y: {diffConfig.DistancePrecision.Format(result.ProjectilePosition.Y)} meters");
            Console.WriteLine($"  Z: {diffConfig.DistancePrecision.Format(result.ProjectilePosition.Z)} meters");
            Console.WriteLine($"  Full position: {diffConfig.FormatVector3(result.ProjectilePosition)}\n");

            Console.WriteLine("Distance Metrics:");
            Console.WriteLine($"  Range from origin (3D): {FormatDistanceWithPrecision(result.RangeFromOrigin, diffConfig)}");
            Console.WriteLine($"  Horizontal distance: {FormatDistanceWithPrecision(result.HorizontalDistance, diffConfig)}");
            Console.WriteLine($"  Current altitude: {FormatDistanceWithPrecision(result.ProjectilePosition.Z, diffConfig)}");
            Console.WriteLine($"  Gravitational drop: {FormatDistanceWithPrecision(result.GravitationalDrop, diffConfig)}\n");

            Console.WriteLine("Flight Characteristics:");
            Console.WriteLine($"  Maximum altitude: {FormatDistanceWithPrecision(result.MaxAltitudeReached, diffConfig)}");
            Console.WriteLine($"  Time to max altitude: {diffConfig.FormatLaunchDelay(result.TimeToMaxAltitude)}");

            if (result.ProjectilePosition.Z < 0)
            {
                Console.WriteLine($"  ⚠ Projectile has descended below starting altitude\n");
            }
            else
            {
                Console.WriteLine();
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("USE THIS INFORMATION TO:");
            Console.WriteLine("  • Understand how velocity, elevation, and azimuth affect trajectory");
            Console.WriteLine("  • Explore different launch angles for same velocity");
            Console.WriteLine("  • Predict projectile position at various flight times");
            Console.WriteLine("  • Estimate intercept timing with target motion data");
        }

        // ====================================================================
        // INPUT HELPERS (with precision-aware defaults)
        // ====================================================================

        private static float GetPlayerVelocityInput(string prompt, float defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.VelocityPrecision.Format(defaultValue)} m/s]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (float.TryParse(input, out float velocity) && velocity > 0)
                    return velocity;

                Console.WriteLine("Invalid input. Please enter a positive velocity value in m/s.\n");
            }
        }

        private static float GetPlayerElevationInput(string prompt, float defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.ElevationPrecision.Format(defaultValue)}°]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (float.TryParse(input, out float angle) && angle >= -90 && angle <= 90)
                    return angle;

                Console.WriteLine("Invalid input. Please enter an angle between -90 and 90 degrees.\n");
            }
        }

        private static float GetPlayerAzimuthInput(string prompt, float defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.AzimuthPrecision.Format(defaultValue)}°]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (float.TryParse(input, out float bearing) && bearing >= 0 && bearing < 360)
                    return bearing;

                Console.WriteLine("Invalid input. Please enter a bearing between 0 and 360 degrees.\n");
            }
        }

        private static float GetPlayerFlightTimeInput(string prompt, float defaultValue, DifficultyConfig diffConfig)
        {
            while (true)
            {
                Console.Write($"{prompt} [{diffConfig.LaunchDelayPrecision.Format(defaultValue)}s]: ");
                string input = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    return defaultValue;

                if (float.TryParse(input, out float time) && time >= 0)
                    return time;

                Console.WriteLine("Invalid input. Please enter a non-negative time value in seconds.\n");
            }
        }
    }
}