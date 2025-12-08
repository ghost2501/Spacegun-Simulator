namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// TRAJECTORY PLOTTER - BALLISTIC COMPUTER
    /// 
    /// Mechanical fire control aid simulating mid-20th century ballistic prediction systems.
    /// Allows players to test projectile trajectories based on launch parameters without
    /// automated targeting or validation.
    /// 
    /// PURPOSE: Exploratory trajectory calculation tool, not a solution provider.
    /// - Accepts launch velocity, elevation angle, azimuth bearing, and flight time
    /// - Calculates where projectile will be at specified flight time using ballistic physics
    /// - Shows position in Cartesian coordinates, range, and gravitational drop
    /// - Provides NO evaluation of whether trajectory is "good" or "bad"
    /// - Players use results to inform their own firing solution decisions
    /// 
    /// DESIGN PRINCIPLE: Never auto-solves. Player queries the tool as many times
    /// as desired to explore different launch parameters and flight time windows.
    /// </summary>
    public static class TrajectoryPlotter
    {
        private const float GRAVITY = 9.81f;

        // Store last test parameters for "modify and retry" feature
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
        /// Player can test multiple launch parameter combinations in a loop.
        /// Supports modifying and re-running the previous test.
        /// </summary>
        public static void ShowTrajectoryPlotterTool()
        {
            bool inTool = true;

            while (inTool)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║          TRAJECTORY PLOTTER - BALLISTIC COMPUTER          ║");
                Console.WriteLine("║     Calculate projectile position at flight time (T)      ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine("=== LAUNCH PARAMETERS ===");
                Console.WriteLine("Enter projectile launch parameters:\n");

                float launchVelocity = GetPlayerVelocityInput("Launch velocity (m/s)", lastLaunchVelocity);
                float elevationDegrees = GetPlayerElevationInput("Elevation angle (-90 to 90 degrees)", lastElevationDegrees);
                float azimuthDegrees = GetPlayerAzimuthInput("Azimuth bearing (0-360 degrees, 0=North)", lastAzimuthDegrees);

                Console.WriteLine();
                float flightTime = GetPlayerFlightTimeInput("Flight time to check (seconds)", lastFlightTime);

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
                DisplayTrajectoryResult(result);

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
                    // Continue to next iteration with stored values prepopulated
                    continue;
                }
                // Any other input (including Enter) continues with fresh prompts
            }
        }

        // ====================================================================
        // CALCULATION ENGINE
        // ====================================================================

        /// <summary>
        /// Represents the result of a trajectory calculation.
        /// </summary>
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

        /// <summary>
        /// Calculate projectile position at a given flight time using ballistic equations.
        /// 
        /// Physics formulas:
        /// - Elevation component (vertical): z(t) = v₀·sin(θ)·t - 0.5·g·t²
        /// - Horizontal component (magnitude): r(t) = v₀·cos(θ)·t
        /// - Azimuth decomposition: x(t) = r(t)·sin(φ), y(t) = r(t)·cos(φ)
        /// 
        /// Where:
        /// - v₀ = initial velocity (m/s)
        /// - θ = elevation angle (radians)
        /// - φ = azimuth angle from North, clockwise (radians)
        /// - g = 9.81 m/s² (gravity)
        /// - t = flight time (seconds)
        /// 
        /// COORDINATE SYSTEM:
        /// - X-axis: East (positive) / West (negative)
        /// - Y-axis: North (positive) / South (negative)
        /// - Z-axis: Up (positive) / Down (negative)
        /// - Azimuth: 0° = North (+Y), 90° = East (+X), 180° = South (-Y), 270° = West (-X)
        /// </summary>
        public static TrajectoryResult CalculateTrajectory(
            float launchVelocity,
            float elevationDegrees,
            float azimuthDegrees,
            float flightTime)
        {
            // Convert angles to radians
            float elevationRad = elevationDegrees * (float)Math.PI / 180f;
            float azimuthRad = azimuthDegrees * (float)Math.PI / 180f;

            // ===== VERTICAL COMPONENT (Z) =====
            // z(t) = v₀·sin(θ)·t - 0.5·g·t²
            float verticalVelocity = launchVelocity * (float)Math.Sin(elevationRad);
            float zPosition = verticalVelocity * flightTime - 0.5f * GRAVITY * flightTime * flightTime;
            float gravitationalDrop = 0.5f * GRAVITY * flightTime * flightTime;

            // ===== HORIZONTAL COMPONENT =====
            // Horizontal range: r(t) = v₀·cos(θ)·t
            float horizontalVelocity = launchVelocity * (float)Math.Cos(elevationRad);
            float horizontalRange = horizontalVelocity * flightTime;

            // ===== CARTESIAN DECOMPOSITION =====
            // Azimuth is measured clockwise from North (+Y axis)
            // x(t) = r(t)·sin(φ)  where φ is azimuth from North
            // y(t) = r(t)·cos(φ)
            // This converts compass bearing to Cartesian coordinates
            float xPosition = horizontalRange * (float)Math.Sin(azimuthRad);
            float yPosition = horizontalRange * (float)Math.Cos(azimuthRad);

            // ===== RANGE FROM ORIGIN =====
            // Total 3D distance from gun
            float rangeFromOrigin = (float)Math.Sqrt(xPosition * xPosition + yPosition * yPosition + zPosition * zPosition);

            // ===== MAXIMUM ALTITUDE CALCULATION =====
            // Max altitude occurs at t = v₀·sin(θ) / g
            float timeToMaxAltitude = Math.Max(0, verticalVelocity / GRAVITY);
            float maxAltitude = verticalVelocity * timeToMaxAltitude - 0.5f * GRAVITY * timeToMaxAltitude * timeToMaxAltitude;
            maxAltitude = Math.Max(0, maxAltitude);  // Clamp to 0 if projectile is descending

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
        /// Display trajectory calculation results in detail.
        /// </summary>
        private static void DisplayTrajectoryResult(TrajectoryResult result)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          TRAJECTORY PLOTTER CALCULATION RESULTS            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"=== INPUT PARAMETERS ===");
            Console.WriteLine($"Launch Velocity: {result.LaunchVelocity:F0} m/s");
            Console.WriteLine($"Elevation Angle: {result.ElevationAngle:F1}°");
            Console.WriteLine($"Azimuth Bearing: {result.AzimuthAngle:F1}°");
            Console.WriteLine($"Flight Time: {result.FlightTime:F2} seconds\n");

            Console.WriteLine($"=== PROJECTILE POSITION AT T+{result.FlightTime:F2}s ===\n");

            Console.WriteLine("Cartesian Coordinates:");
            Console.WriteLine($"  X: {result.ProjectilePosition.X:F1} meters");
            Console.WriteLine($"  Y: {result.ProjectilePosition.Y:F1} meters");
            Console.WriteLine($"  Z: {result.ProjectilePosition.Z:F1} meters");
            Console.WriteLine($"  Full position: {result.ProjectilePosition}\n");

            Console.WriteLine("Distance Metrics:");
            Console.WriteLine($"  Range from origin (3D): {GameConstants.FormatDistance(result.RangeFromOrigin)}");
            Console.WriteLine($"  Horizontal distance: {GameConstants.FormatDistance(result.HorizontalDistance)}");
            Console.WriteLine($"  Current altitude: {GameConstants.FormatDistance(result.ProjectilePosition.Z)}");
            Console.WriteLine($"  Gravitational drop: {GameConstants.FormatDistance(result.GravitationalDrop)}\n");

            Console.WriteLine("Flight Characteristics:");
            Console.WriteLine($"  Maximum altitude: {GameConstants.FormatDistance(result.MaxAltitudeReached)}");
            Console.WriteLine($"  Time to max altitude: {result.TimeToMaxAltitude:F2} seconds");

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
            Console.WriteLine("  • Compare multiple trajectory options");
        }

        // ====================================================================
        // INPUT HELPERS
        // ====================================================================

        /// <summary>
        /// Get player input for launch velocity in m/s.
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// </summary>
        private static float GetPlayerVelocityInput(string prompt, float defaultValue)
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

                if (float.TryParse(input, out float velocity) && velocity > 0)
                {
                    return velocity;
                }

                Console.WriteLine("Invalid input. Please enter a positive velocity value in m/s.\n");
            }
        }

        /// <summary>
        /// Get player input for elevation angle (-90 to 90 degrees).
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// Negative angles represent firing downward (at descending targets).
        /// </summary>
        private static float GetPlayerElevationInput(string prompt, float defaultValue)
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

                if (float.TryParse(input, out float angle) && angle >= -90 && angle <= 90)
                {
                    return angle;
                }

                Console.WriteLine("Invalid input. Please enter an angle between -90 and 90 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for azimuth bearing (0-360 degrees).
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// 0° = North (+Y direction), 90° = East (+X direction)
        /// </summary>
        private static float GetPlayerAzimuthInput(string prompt, float defaultValue)
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

                if (float.TryParse(input, out float bearing) && bearing >= 0 && bearing < 360)
                {
                    return bearing;
                }

                Console.WriteLine("Invalid input. Please enter a bearing between 0 and 360 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for flight time in seconds.
        /// Prepopulates with last used value, allowing player to press Enter to keep it.
        /// </summary>
        private static float GetPlayerFlightTimeInput(string prompt, float defaultValue)
        {
            while (true)
            {
                Console.Write($"{prompt} [{defaultValue:F2}s]: ");
                string input = Console.ReadLine() ?? "";

                // If empty, use default
                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (float.TryParse(input, out float time) && time >= 0)
                {
                    return time;
                }

                Console.WriteLine("Invalid input. Please enter a non-negative time value in seconds.\n");
            }
        }
    }
}