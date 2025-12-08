namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// BALLISTIC TABLES & REFERENCE CHARTS
    /// 
    /// Provides pre-calculated lookup tables for ballistic calculations.
    /// Simulates physical reference materials (paper charts, tables) that a mid-20th
    /// century artillery gunner would use with mechanical fire control computers.
    /// 
    /// PURPOSE: Educational tool for players to verify calculations without solving problems.
    /// - Shows time-of-flight for different velocities and ranges
    /// - Shows gravity drop over various flight times
    /// - Allows players to manually look up values and understand relationships
    /// 
    /// DESIGN PRINCIPLE: Never auto-solves. Player uses tables as reference only.
    /// </summary>
    public static class BallisticsTablesReference
    {
        // Constants from game physics
        private const float GRAVITY = 9.81f;

        // ====================================================================
        // TABLE 1: TIME-OF-FLIGHT REFERENCE
        // ====================================================================
        // Shows how long a projectile takes to reach various ranges at different velocities
        // and elevation angles. Useful for estimating launch delay time.
        //
        // Rows: Velocity (m/s)
        // Columns: Range (km)
        // Values: Time of flight in seconds at various elevation angles

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
        /// Display Time-of-Flight Table.
        /// Shows estimated flight times for typical velocities across range bands.
        /// </summary>
        public static void DisplayTimeOfFlightTable()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         TABLE 1: TIME-OF-FLIGHT REFERENCE                 ║");
            Console.WriteLine("║    (Simplified: horizontal range only, gravity not incl.) ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("Use this table to estimate flight times at different velocities and ranges.\n");

            // Typical velocities in m/s (from weapon specs)
            float[] velocities = { 50_000f, 100_000f, 150_000f, 200_000f, 250_000f, 300_000f, 350_000f };

            // Typical ranges in km
            int[] rangesKm = { 200, 400, 600, 800, 1000, 1200, 1400, 1600, 1800, 2000 };

            // Elevation angles for reference
            float[] elevations = { 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f };

            // Display multiple tables for different elevation angles
            foreach (float elev in elevations)
            {
                Console.WriteLine($"=== ELEVATION ANGLE: {elev}° ===");
                Console.WriteLine("     Range→   200km   400km   600km   800km  1000km  1200km  1400km  1600km  1800km  2000km");
                Console.WriteLine("Vel ↓");

                foreach (float vel in velocities)
                {
                    Console.Write($"{vel / 1000:F0}k m/s ");

                    foreach (int rangeKm in rangesKm)
                    {
                        float rangeM = rangeKm * 1000f;
                        float tof = CalculateTimeOfFlight(vel, rangeM, elev);
                        Console.Write($"  {tof:F2}s  ");
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }

            Console.WriteLine("\n═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("INTERPRETATION GUIDE:");
            Console.WriteLine("  • Lower velocity = longer flight time (projectile is slower)");
            Console.WriteLine("  • Higher velocity = shorter flight time (projectile is faster)");
            Console.WriteLine("  • Higher elevation = longer flight time (curved path)");
            Console.WriteLine("  • Lower elevation = shorter flight time (flatter trajectory)");
            Console.WriteLine("\nUSE THIS TO:");
            Console.WriteLine("  1. Estimate how long your projectile will take to reach target");
            Console.WriteLine("  2. Verify if your launch delay time is reasonable");
            Console.WriteLine("  3. Compare different velocity options\n");
        }

        // ====================================================================
        // TABLE 2: GRAVITY DROP REFERENCE
        // ====================================================================
        // Shows vertical drop due to gravity over various flight times.
        // Formula: drop = 0.5 × 9.81 × t²
        //
        // Critical for understanding elevation angle adjustments.

        /// <summary>
        /// Calculate vertical drop due to gravity.
        /// Formula: drop = 0.5 × g × t²
        /// </summary>
        public static float CalculateGravityDrop(float flightTimeSeconds)
        {
            return 0.5f * GRAVITY * flightTimeSeconds * flightTimeSeconds;
        }

        /// <summary>
        /// Display Gravity Drop Table.
        /// Shows how much vertical distance projectile loses due to gravity over time.
        /// </summary>
        public static void DisplayGravityDropTable()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           TABLE 2: GRAVITY DROP REFERENCE                 ║");
            Console.WriteLine("║      How much altitude is lost due to gravity over time   ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("Use this table to understand how gravity affects projectile trajectory.\n");

            Console.WriteLine("=== VERTICAL DROP BY FLIGHT TIME ===");
            Console.WriteLine("Flight Time (s) │ Drop (meters) │ Drop (km)");
            Console.WriteLine("────────────────┼───────────────┼───────────");

            // Flight times from 1 to 30 seconds in 1-second increments
            for (float t = 1f; t <= 30f; t += 1f)
            {
                float dropM = CalculateGravityDrop(t);
                float dropKm = dropM / 1000f;

                string timeStr = t.ToString("F1").PadLeft(15);
                string dropMStr = dropM.ToString("F1").PadLeft(13);
                string dropKmStr = dropKm.ToString("F3").PadLeft(9);

                Console.WriteLine($"{timeStr} │ {dropMStr} │ {dropKmStr}");
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("CRITICAL INSIGHTS:");
            Console.WriteLine("  • After 5 seconds:  ~122.6m drop (minor impact)");
            Console.WriteLine("  • After 10 seconds: ~490.5m drop (MAJOR - requires elevation adjustment!)");
            Console.WriteLine("  • After 20 seconds: ~1,962m drop (EXTREME - nearly 2km altitude loss!)");
            Console.WriteLine("  • After 30 seconds: ~4,414m drop (catastrophic - target likely missed)\n");
            Console.WriteLine("ELEVATION ADJUSTMENT RULES OF THUMB:");
            Console.WriteLine("  • Short flights (5s): Minimal elevation change needed");
            Console.WriteLine("  • Medium flights (10-15s): Significant elevation compensation required");
            Console.WriteLine("  • Long flights (20s+): You MUST aim much higher to compensate for drop\n");
            Console.WriteLine("PRACTICAL EXAMPLES (at 1000km range):");
            Console.WriteLine("  • 5s flight: ~0.07° elevation needed");
            Console.WriteLine("  • 10s flight: ~0.28° elevation needed");
            Console.WriteLine("  • 20s flight: ~1.12° elevation needed");
            Console.WriteLine("  • 30s flight: ~2.53° elevation needed\n");
            Console.WriteLine("USE THIS TO:");
            Console.WriteLine("  1. Understand why you need to aim HIGHER for longer flights");
            Console.WriteLine("  2. Calculate approximate elevation compensation needed");
            Console.WriteLine("  3. Avoid overshooting (aiming too high) or undershooting (too low)\n");
        }
        // ====================================================================
        // TABLE 3: QUICK REFERENCE - ENERGY VS VELOCITY
        // ====================================================================
        // Shows kinetic energy at different velocities for typical projectile masses.

        /// <summary>
        /// Calculate kinetic energy in megajoules.
        /// Formula: KE = 0.5 × mass × velocity²
        /// </summary>
        public static double CalculateKineticEnergyMJ(double massPounds, double velocityMs)
        {
            double energyJoules = 0.5 * massPounds * velocityMs * velocityMs;
            return energyJoules / 1_000_000.0;
        }

        /// <summary>
        /// Display Energy Reference Table.
        /// Shows kinetic energy at different velocities for weapon specs.
        /// </summary>
        public static void DisplayEnergyReferenceTable()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        TABLE 3: KINETIC ENERGY REFERENCE                  ║");
            Console.WriteLine("║    Energy delivered by different projectile/velocity combos║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("Use this table to estimate weapon capability against different targets.\n");

            // Projectile masses (kg) from game weapons
            double[] masses = { 10, 15, 25, 50, 100 };

            // Velocity ranges (m/s)
            double[] velocities = { 50_000, 75_000, 100_000, 150_000, 200_000, 250_000, 300_000, 350_000 };

            Console.WriteLine("=== KINETIC ENERGY BY MASS AND VELOCITY (in MJ) ===");
            Console.WriteLine("Mass (kg) │ 50km/s  │ 75km/s  │ 100km/s │ 150km/s │ 200km/s │ 250km/s │ 300km/s │ 350km/s");
            Console.WriteLine("──────────┼─────────┼─────────┼─────────┼─────────┼─────────┼─────────┼─────────┼─────────");

            foreach (double mass in masses)
            {
                string massStr = mass.ToString("F0").PadLeft(9);
                Console.Write($"{massStr} │");

                foreach (double vel in velocities)
                {
                    double energy = CalculateKineticEnergyMJ(mass, vel);
                    string energyStr = $"{energy:F0} MJ".PadLeft(7);
                    Console.Write($"{energyStr} │");
                }

                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("KEY OBSERVATIONS:");
            Console.WriteLine("  • Doubling velocity increases energy by 4× (quadratic relationship)");
            Console.WriteLine("  • Doubling mass increases energy by 2× (linear relationship)");
            Console.WriteLine("  • 100kg @ 100km/s = 5,000 MJ (massive destructive power)");
            Console.WriteLine("  • 10kg @ 100km/s = 500 MJ (still significant for early game)");
            Console.WriteLine("\nUSE THIS TO:");
            Console.WriteLine("  1. Verify your weapon choice meets energy requirement");
            Console.WriteLine("  2. Understand velocity is more important than mass");
            Console.WriteLine("  3. Plan which weapon to select in development phase\n");
        }

        // ====================================================================
        // TABLE 4: RANGE REFERENCE - Gun Effective Range by Velocity
        // ====================================================================

        /// <summary>
        /// Display Range Coverage Table.
        /// Shows effective gun range at different velocities and time windows.
        /// </summary>
        public static void DisplayRangeCoverageTable()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           TABLE 4: RANGE COVERAGE REFERENCE               ║");
            Console.WriteLine("║      How far projectiles can travel in different times    ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("Use this table to estimate if your projectile can reach the target.\n");

            Console.WriteLine("=== DISTANCE COVERED BY VELOCITY OVER TIME ===");
            Console.WriteLine("Velocity  │ 5s Distance │ 10s Distance │ 15s Distance │ 20s Distance │ 30s Distance");
            Console.WriteLine("──────────┼─────────────┼──────────────┼──────────────┼──────────────┼──────────────");

            // Velocities in m/s (realistic weapons)
            float[] velocities = { 50_000f, 75_000f, 100_000f, 150_000f, 200_000f, 250_000f, 300_000f, 350_000f };

            // Time intervals (seconds)
            float[] times = { 5f, 10f, 15f, 20f, 30f };

            foreach (float vel in velocities)
            {
                Console.Write($"{vel / 1000:F0}k m/s │");

                foreach (float t in times)
                {
                    float distance = vel * t;
                    string distanceStr = distance >= 1_000_000
                        ? $"{distance / 1_000_000:F2}Mm"
                        : $"{distance / 1000:F0}km";

                    Console.Write($" {distanceStr:>10} │");
                }

                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("TYPICAL ENGAGEMENT WINDOW:");
            Console.WriteLine("  • Gun range: 1,000-1,500 km (1-2 million meters)");
            Console.WriteLine("  • Flight time: 5-30 seconds");
            Console.WriteLine("  • Example: 200km/s weapon can reach 1000km in 5 seconds");
            Console.WriteLine("\nUSE THIS TO:");
            Console.WriteLine("  1. Estimate if your weapon can reach the target");
            Console.WriteLine("  2. Verify flight time is reasonable for engagement distance");
            Console.WriteLine("  3. Plan launch delay to ensure intercept happens in gun range\n");
        }

        // ====================================================================
        // MAIN REFERENCE MENU
        // ====================================================================

        /// <summary>
        /// Display reference charts menu and handle navigation.
        /// </summary>
        public static void ShowReferencesMenu()
        {
            bool inMenu = true;

            while (inMenu)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║           FIRE CONTROL REFERENCE CHARTS                   ║");
                Console.WriteLine("║   Historical Artillery Fire Control Tables & Data         ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine("SELECT A REFERENCE TABLE:\n");
                Console.WriteLine("[1] Time-of-Flight Table (flight time estimates)");
                Console.WriteLine("[2] Gravity Drop Table (altitude loss over time)");
                Console.WriteLine("[3] Kinetic Energy Table (damage calculations)");
                Console.WriteLine("[4] Range Coverage Table (distance traveled)");
                Console.WriteLine("[5] View All Tables");
                Console.WriteLine("[0] Return to Firing Solution\n");

                Console.Write("Select reference: ");
                string input = Console.ReadLine() ?? "0";

                switch (input)
                {
                    case "1":
                        DisplayTimeOfFlightTable();
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "2":
                        DisplayGravityDropTable();
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "3":
                        DisplayEnergyReferenceTable();
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "4":
                        DisplayRangeCoverageTable();
                        Console.WriteLine("\nPress any key to return to menu...");
                        Console.ReadKey();
                        break;

                    case "5":
                        DisplayTimeOfFlightTable();
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();

                        DisplayGravityDropTable();
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();

                        DisplayEnergyReferenceTable();
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();

                        DisplayRangeCoverageTable();
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