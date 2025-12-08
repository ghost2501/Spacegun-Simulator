namespace Spacegun_Simulator
{
    // ============================================================================
    // FIRING SOLUTION - 3D Ballistic Problem Generation & Validation
    // ============================================================================
    // ENGAGEMENT FLOW:
    // At T+0s: Enemy is at 1000-1200km distance with known position, velocity vector
    // Player calculates: LaunchDelayTime, Elevation, Azimuth, LaunchVelocity
    // At T+LaunchDelayTime: Gun fires
    // At T+LaunchDelayTime+FlightTime: Projectile intercepts target (within gun range)
    //
    // X = LaunchDelayTime + FlightTime = Total time from engagement start to intercept
    // 
    // PRECISION: All calculations use double precision (64-bit) to achieve <1m accuracy
    // at ranges of 1,000+ km. Vector3 coordinates stored as double.

    public struct Vector3
    {
        public double X;  // DOUBLE PRECISION for sub-meter accuracy
        public double Y;
        public double Z;

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

        public static Vector3 Zero => new(0.0, 0.0, 0.0);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 v, double s) => new(v.X * s, v.Y * s, v.Z * s);
        public static Vector3 operator /(Vector3 v, double s) => new(v.X / s, v.Y / s, v.Z / s);

        public override string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
    }

    public class FiringSolutionResult
    {
        public bool CanDestroy { get; set; }
        public bool CanHit { get; set; }
        public bool SolutionValid { get; set; }
        public Vector3? EnemyInterceptPoint { get; set; }
        public float LaunchDelayTime { get; set; }
        public float TargetElevation { get; set; }
        public float TargetAzimuth { get; set; }
        public float MinVelocityRequired { get; set; }
        public float MaxVelocityAvailable { get; set; }
        public float ProjectileVelocity { get; set; }
        public double KineticEnergyMJ { get; set; }
        public double FractureEnergyRequired { get; set; }
        public float InterceptDeviation { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class FiringProblem
    {
        public Vector3 EnemyPosition { get; set; }
        public Vector3 EnemyVelocity { get; set; }
        public float ApproachElevation { get; set; }
        public float ApproachAzimuth { get; set; }
        public float EngagementDistance { get; set; }
        public float ApproachSpeed { get; set; }
        public double FractureEnergyRequired { get; set; }
        public float CorrectLaunchDelayTime { get; set; }
        public float CorrectElevation { get; set; }
        public float CorrectAzimuth { get; set; }
        public float CorrectVelocity { get; set; }
    }

    public class FiringSolution
    {
        private const double GRAVITY = 9.81;  // CHANGED: float to double
        private const double STANDARD_SHIP_DENSITY_KG_M3 = 500.0;
        private const double METRIC_TONS_TO_KG = 1000.0;

        private float projectileMass;
        private float enemyFractureEnergy;
        private double enemyMass;

        public FiringSolution(float projectileMass, float enemyFractureEnergy, double enemyMass = 10000.0)
        {
            this.projectileMass = projectileMass;
            this.enemyFractureEnergy = enemyFractureEnergy;
            this.enemyMass = enemyMass;
        }

        /// <summary>
        /// Convert elevation/azimuth angles (in degrees) to 3D Cartesian coordinates.
        /// PRECISION: Using double for angle conversions and vector creation.
        /// 
        /// COORDINATE SYSTEM (Right-Handed Standard):
        /// X-axis: East (positive) / West (negative)
        /// Y-axis: North (positive) / South (negative)  
        /// Z-axis: Up (positive) / Down (negative)
        /// 
        /// AZIMUTH (bearing from North, clockwise):
        /// 0° = North (+Y)
        /// 90° = East (+X)
        /// 180° = South (-Y)
        /// 270° = West (-X)
        /// 
        /// ELEVATION (angle from horizontal):
        /// 0° = Horizontal (in XY plane)
        /// 90° = Straight up (+Z)
        /// -90° = Straight down (-Z)
        /// </summary>
        public static Vector3 AnglesToCartesian(double elevationDeg, double azimuthDeg, double distance)
        {
            double elevationRad = elevationDeg * Math.PI / 180.0;
            double azimuthRad = azimuthDeg * Math.PI / 180.0;

            // Horizontal distance in XY plane
            double horizontalDistance = distance * Math.Cos(elevationRad);

            // Vertical distance along Z-axis
            double z = distance * Math.Sin(elevationRad);

            // Decompose horizontal distance into X (East) and Y (North) components
            // Azimuth 0° = North (+Y), 90° = East (+X)
            double x = horizontalDistance * Math.Sin(azimuthRad);
            double y = horizontalDistance * Math.Cos(azimuthRad);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Convert 3D Cartesian coordinates back to elevation/azimuth angles.
        /// PRECISION: Using double throughout for high accuracy.
        /// 
        /// COORDINATE SYSTEM (Right-Handed Standard):
        /// X-axis: East (positive) / West (negative)
        /// Y-axis: North (positive) / South (negative)
        /// Z-axis: Up (positive) / Down (negative)
        /// 
        /// Returns (elevation in degrees, azimuth in degrees [0-360))
        /// </summary>
        public static (double elevation, double azimuth) CartesianToAngles(Vector3 position)
        {
            double distance = position.Magnitude;
            if (distance < 0.0001)
                return (0, 0);

            // Elevation: angle from horizontal plane
            double elevation = Math.Atan2(position.Z,
                Math.Sqrt(position.X * position.X + position.Y * position.Y));

            // Azimuth: bearing from North, clockwise
            // 0° = North (+Y), 90° = East (+X)
            double azimuth = Math.Atan2(position.X, position.Y);

            double elevationDeg = elevation * 180.0 / Math.PI;
            double azimuthDeg = azimuth * 180.0 / Math.PI;

            // Normalize azimuth to [0, 360)
            if (azimuthDeg < 0) azimuthDeg += 360.0;

            return (elevationDeg, azimuthDeg);
        }

        public float CalculateRequiredVelocity()
        {
            float requiredVelocity = (float)Math.Sqrt(2 * enemyFractureEnergy / projectileMass);
            return requiredVelocity;
        }

        public double CalculateKineticEnergyMJ(float velocity)
        {
            double v = velocity;
            double m = projectileMass;
            double energyJoules = 0.5 * m * v * v;
            return energyJoules / 1_000_000.0;
        }

        /// <summary>
        /// Calculate target diameter from mass using spherical geometry and uniform density.
        /// </summary>
        private float CalculateTargetDiameter()
        {
            double massKg = enemyMass * METRIC_TONS_TO_KG;
            double volumeM3 = massKg / STANDARD_SHIP_DENSITY_KG_M3;
            double radiusM = Math.Pow(3.0 * volumeM3 / (4.0 * Math.PI), 1.0 / 3.0);
            return (float)(radiusM * 2.0);
        }

        /// <summary>
        /// Calculate hit tolerance as 0.5 × target diameter, modified by difficulty settings.
        /// 
        /// DIFFICULTY MODIFIERS:
        /// - NuclearOption: 100x tolerance (warhead blast radius)
        /// - CometsAndAsteroids: Target RCS already 10x larger (asteroid size)
        /// - RealSpacegunSimulator: No modifiers (pure ballistics)
        /// </summary>
        private float CalculateHitTolerance(double rcsMultiplier = 1.0, double toleranceMultiplier = 1.0)
        {
            // Calculate base diameter
            double massKg = enemyMass * METRIC_TONS_TO_KG;
            double volumeM3 = massKg / STANDARD_SHIP_DENSITY_KG_M3;
            double radiusM = Math.Pow(3.0 * volumeM3 / (4.0 * Math.PI), 1.0 / 3.0);
            double diameterM = radiusM * 2.0;

            // Apply RCS multiplier (affects apparent size)
            // RCS = π * (diameter/2)² for a sphere
            // If RCS is 10x larger, effective diameter increases by sqrt(10) ≈ 3.16x
            if (rcsMultiplier > 1.0)
            {
                double rcsLinear = Math.Sqrt(rcsMultiplier);
                diameterM *= rcsLinear;
            }

            // Base tolerance is 0.5 × diameter
            double baseTolerance = diameterM * 0.5;

            // Apply tolerance multiplier (for warhead blast radius)
            return (float)(baseTolerance * toleranceMultiplier);
        }

        /// <summary>
        /// Calculate projectile position at time T (measured from firing, not engagement start).
        /// PRECISION: Using double for all time and position calculations to maintain sub-meter accuracy.
        /// </summary>
        private Vector3 CalculateProjectilePosition(double flightTime, double launchVelocity, double elevationDeg, double azimuthDeg)
        {
            double elevationRad = elevationDeg * Math.PI / 180.0;
            double azimuthRad = azimuthDeg * Math.PI / 180.0;

            double vz = launchVelocity * Math.Sin(elevationRad);
            double vHorizontal = launchVelocity * Math.Cos(elevationRad);

            double vx = vHorizontal * Math.Sin(azimuthRad);
            double vy = vHorizontal * Math.Cos(azimuthRad);

            double x = vx * flightTime;
            double y = vy * flightTime;
            double z = vz * flightTime - 0.5 * GRAVITY * flightTime * flightTime;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Calculate enemy position at time T (measured from engagement start).
        /// PRECISION: Using double for all time calculations to maintain sub-meter accuracy.
        /// </summary>
        private Vector3 CalculateEnemyPosition(double engagementTime, Vector3 initialPosition, Vector3 velocityVector)
        {
            return initialPosition + (velocityVector * engagementTime);
        }

        /// <summary>
        /// Generate a complete firing problem for the player.
        /// REVERSED LOGIC: Pick intercept point first, then work backwards to T+0s.
        /// 
        /// At T+0s: Enemy is at 1000-2000km altitude, approaching with known velocity
        /// At T+X: Enemy reaches the intercept point (closer to Earth, significantly different angle)
        /// Player must calculate when to fire so projectile arrives at that point at T+X
        /// 
        /// DRAMATIC ARC: Enforces large elevation and azimuth deltas (45-90° and 90-120° respectively)
        /// to create a visually striking arc across the sky during the 5-30 second flight.
        /// </summary>
        public FiringProblem GenerateFiringProblem(
            EnemyWave wave,
            float playerGunMaxVelocity,
            float gunEffectiveRange,
            Random rng,
            float initialEngagementDistance = 0f)
        {
            if (wave is null) throw new ArgumentNullException(nameof(wave));
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            var target = wave.Targets[0];
            this.enemyMass = target.Mass;

            // ===== CRITICAL: Check if we have cached vectors from a save =====
            if (wave.CachedEnemyPosition.HasValue && wave.CachedEnemyVelocity.HasValue)
            {
                // Restore from save - use exact cached vectors
                Vector3 enemyAtT0 = wave.CachedEnemyPosition.Value;
                Vector3 enemyVelocity = wave.CachedEnemyVelocity.Value;
                float approachElev = wave.ApproachElevation;
                float approachAzim = wave.ApproachAzimuth;

                // Calculate engagement distance from cached position
                float approachDistance = (float)enemyAtT0.Magnitude;

                // For restored waves, use a fixed intercept time to maintain consistency
                float interceptTime = 10f;  // Use a reasonable fixed value for restored waves

                Vector3 interceptPoint = enemyAtT0 + (enemyVelocity * interceptTime);

                // Convert intercept point back to angles
                var (interceptElev, interceptAzim) = CartesianToAngles(interceptPoint);
                float interceptDistance = (float)interceptPoint.Magnitude;

                // Calculate correct solution parameters
                float correctVelocity = playerGunMaxVelocity * 0.85f;

                Console.WriteLine($"\n[WAVE RESTORE] Using cached trajectory data from save");
                Console.WriteLine($"  Enemy Position: {enemyAtT0}");
                Console.WriteLine($"  Enemy Velocity: {enemyVelocity}");
                Console.WriteLine($"  Approach: {approachElev:F1}° elev, {approachAzim:F1}° azim, {approachDistance:F0}m");

                return new FiringProblem
                {
                    EnemyPosition = enemyAtT0,
                    EnemyVelocity = enemyVelocity,
                    ApproachElevation = approachElev,
                    ApproachAzimuth = approachAzim,
                    EngagementDistance = approachDistance,
                    ApproachSpeed = (float)wave.AverageVelocity,
                    FractureEnergyRequired = target.FractureEnergy,
                    CorrectLaunchDelayTime = Math.Max(1f, interceptTime - 10f),
                    CorrectElevation = (float)interceptElev,
                    CorrectAzimuth = (float)interceptAzim,
                    CorrectVelocity = correctVelocity
                };
            }

            // ===== FRESH WAVE: Generate new random trajectory =====
            // STEP 1: Pick a random intercept time (5-30 seconds from engagement start)
            float interceptTime_Fresh = 5f + (float)(rng.NextDouble() * 25f);

            // STEP 2: Generate T+0s angles (starting position)
            float approachElev_Fresh = 20f + (float)(rng.NextDouble() * 50f);           // 20-70° elevation
            float approachAzim_Fresh = (float)(rng.NextDouble() * 360f);               // 0-360° azimuth

            // Use provided distance or generate new one
            float approachDistance_Fresh = initialEngagementDistance > 0f
                ? initialEngagementDistance
                : 1_500_000 + (float)(rng.NextDouble() * 500_000);  // 1500-2000 km fallback

            Vector3 enemyAtT0_Fresh = AnglesToCartesian(approachElev_Fresh, approachAzim_Fresh, approachDistance_Fresh);

            // STEP 3: Generate intercept angles with ENFORCED DRAMATIC ARC
            // Elevation delta: 45-90 degrees
            // Azimuth delta: 90-120 degrees
            float elevDelta = 45f + (float)(rng.NextDouble() * 45f);  // 45-90° change
            float azimDelta = 90f + (float)(rng.NextDouble() * 30f);  // 90-120° change

            // Apply elevation delta (can increase or decrease)
            float elevDirection = rng.NextDouble() < 0.5 ? -1f : 1f;
            float interceptElev_Fresh = approachElev_Fresh + (elevDirection * elevDelta);
            interceptElev_Fresh = Math.Max(5f, Math.Min(85f, interceptElev_Fresh));  // Clamp to valid range (5-85°)

            // Apply azimuth delta (clockwise or counter-clockwise)
            float azimDirection = rng.NextDouble() < 0.5 ? -1f : 1f;
            float interceptAzim_Fresh = approachAzim_Fresh + (azimDirection * azimDelta);
            if (interceptAzim_Fresh < 0) interceptAzim_Fresh += 360f;
            if (interceptAzim_Fresh >= 360f) interceptAzim_Fresh -= 360f;

            // Intercept distance: closer to Earth (20-50% closer)
            float distanceReduction = 0.2f + (float)(rng.NextDouble() * 0.3f);
            float interceptDistance_Fresh = approachDistance_Fresh * (1f - distanceReduction);
            interceptDistance_Fresh = Math.Max(1_000_000f, Math.Min(2_000_000f, interceptDistance_Fresh));

            Vector3 interceptPoint_Fresh = AnglesToCartesian(interceptElev_Fresh, interceptAzim_Fresh, interceptDistance_Fresh);

            // STEP 4: Work backwards to find enemy velocity
            // Calculate velocity vector to move from T+0s position to intercept position
            Vector3 displacement = interceptPoint_Fresh - enemyAtT0_Fresh;
            Vector3 enemyVelocity_Fresh = (displacement * (1.0 / interceptTime_Fresh));

            // Verify the velocity magnitude matches the wave average
            double calculatedSpeed = enemyVelocity_Fresh.Magnitude;
            double speedDifference = Math.Abs(calculatedSpeed - wave.AverageVelocity);
            if (speedDifference > wave.AverageVelocity * 0.1)
            {
                // Velocity doesn't match - normalize it to match wave velocity
                enemyVelocity_Fresh = (enemyVelocity_Fresh / calculatedSpeed) * wave.AverageVelocity;
            }

            // ===== CRITICAL: Cache the generated vectors for save/restore =====
            wave.CachedEnemyPosition = enemyAtT0_Fresh;
            wave.CachedEnemyVelocity = enemyVelocity_Fresh;

            // STEP 5: Calculate angle change (for narrative - visible arc)
            float elevationDeltaActual = Math.Abs(interceptElev_Fresh - approachElev_Fresh);
            float azimuthDeltaActual = Math.Abs(interceptAzim_Fresh - approachAzim_Fresh);
            if (azimuthDeltaActual > 180f) azimuthDeltaActual = 360f - azimuthDeltaActual;  // Get shortest arc

            // STEP 6: Calculate correct solution parameters
            float correctVelocity_Fresh = playerGunMaxVelocity * 0.85f;
            float correctElevation_Fresh = interceptElev_Fresh;
            float correctAzimuth_Fresh = interceptAzim_Fresh;

            Console.WriteLine($"\n[WAVE GEN] Generated fresh trajectory data");
            Console.WriteLine($"  Enemy Position: {enemyAtT0_Fresh}");
            Console.WriteLine($"  Enemy Velocity: {enemyVelocity_Fresh}");
            Console.WriteLine($"  Approach: {approachElev_Fresh:F1}° elev, {approachAzim_Fresh:F1}° azim, {approachDistance_Fresh:F0}m");

            return new FiringProblem
            {
                EnemyPosition = enemyAtT0_Fresh,
                EnemyVelocity = enemyVelocity_Fresh,
                ApproachElevation = approachElev_Fresh,
                ApproachAzimuth = approachAzim_Fresh,
                EngagementDistance = approachDistance_Fresh,
                ApproachSpeed = (float)wave.AverageVelocity,
                FractureEnergyRequired = target.FractureEnergy,
                CorrectLaunchDelayTime = Math.Max(1f, interceptTime_Fresh - 10f),
                CorrectElevation = correctElevation_Fresh,
                CorrectAzimuth = correctAzimuth_Fresh,
                CorrectVelocity = correctVelocity_Fresh
            };
        }

        /// <summary>
        /// Find a valid intercept solution.
        /// Dynamically adjusts search window based on enemy velocity.
        /// </summary>
        private bool FindValidSolution(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            out (float LaunchDelayTime, float Elevation, float Azimuth, float Velocity) solution)
        {
            solution = default;

            float minVelocity = CalculateRequiredVelocity();
            if (minVelocity > maxGunVelocity)
            {
                Console.WriteLine($"      ✗ Min velocity required ({minVelocity:F0} m/s) exceeds max gun velocity ({maxGunVelocity:F0} m/s)");
                return false;
            }

            float hitTolerance = CalculateHitTolerance();
            Console.WriteLine($"      Searching for valid solution:");
            Console.WriteLine($"        Hit tolerance: {hitTolerance:F1} m");
            Console.WriteLine($"        Gun range: {gunEffectiveRange:F0} m");

            double enemySpeed = enemyVelocity.Magnitude;
            float maxSearchTime = enemySpeed > 0
                ? Math.Min(600f, (gunEffectiveRange * 1.5f) / (float)enemySpeed)
                : 600f;

            Console.WriteLine($"        Enemy speed: {enemySpeed:F0} m/s, search window: 2-{maxSearchTime:F1}s");

            for (float engagementTime = 2f; engagementTime <= maxSearchTime; engagementTime += 0.5f)
            {
                Vector3 enemyAtEngagementT = CalculateEnemyPosition(engagementTime, enemyInitialPosition, enemyVelocity);

                if (enemyAtEngagementT.Z <= 0)
                    continue;

                double distanceToEnemy = enemyAtEngagementT.Magnitude;
                if (distanceToEnemy > gunEffectiveRange)
                    continue;

                for (float elev = 5f; elev <= 85f; elev += 5f)
                {
                    for (float azim = 0f; azim < 360f; azim += 30f)
                    {
                        for (float vel = minVelocity; vel <= maxGunVelocity; vel += Math.Max(5000f, (maxGunVelocity - minVelocity) / 5f))
                        {
                            for (float flightTime = 0.5f; flightTime <= Math.Min(30f, engagementTime); flightTime += 0.5f)
                            {
                                Vector3 enemyAtIntercept = CalculateEnemyPosition(engagementTime + flightTime, enemyInitialPosition, enemyVelocity);
                                Vector3 projectileAtIntercept = CalculateProjectilePosition(flightTime, vel, elev, azim);

                                Vector3 deviation = projectileAtIntercept - enemyAtIntercept;
                                double distance = deviation.Magnitude;

                                if (distance < hitTolerance && enemyAtIntercept.Magnitude <= gunEffectiveRange)
                                {
                                    float launchDelayTime = engagementTime;
                                    Console.WriteLine($"        ✓ Solution found: Launch at T+{launchDelayTime:F1}s, intercept at T+{engagementTime + flightTime:F1}s");
                                    solution = (launchDelayTime, elev, azim, vel);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"      ✗ No valid solution found");
            return false;
        }

        /// <summary>
        /// Validate player solution given launch delay time and other parameters.
        /// LaunchDelayTime represents when to fire from engagement T+0s.
        /// Allows 0s as a valid option for immediate firing at engagement start.
        /// PRECISION: All time calculations use double precision for sub-meter accuracy.
        /// </summary>
        public FiringSolutionResult CalculateSolution(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocityVector,
            double playerLaunchDelayTime,
            double playerTargetElevation,
            double playerTargetAzimuth,
            double playerLaunchVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            int waveNumber = 1,
            double enemyMass = 10000.0,
            GameDifficulty difficulty = GameDifficulty.RealSpacegunSimulator)
        {
            this.enemyMass = enemyMass;

            // Get difficulty configuration
            var diffConfig = DifficultyConfig.GetConfig(difficulty);

            var tier = GameConstants.GetTierForWave(waveNumber);
            double minLaunchDelayTime = 0.0;
            double maxLaunchDelayTime = tier.TierIndex switch
            {
                0 => 60.0,
                1 => 120.0,
                2 => 180.0,
                _ => 180.0
            };

            Console.WriteLine($"\n[FIRING SOLUTION CALC] Starting validation");
            Console.WriteLine($"  Difficulty: {diffConfig.DisplayName}");
            Console.WriteLine($"  Gun Effective Range: {gunEffectiveRange:F0} m ({gunEffectiveRange / 1_000_000:F2} Mm)");
            Console.WriteLine($"  Player Launch Delay: {playerLaunchDelayTime:F5}s");
            Console.WriteLine($"  Player Elevation: {playerTargetElevation:F1}°");
            Console.WriteLine($"  Player Azimuth: {playerTargetAzimuth:F1}°");
            Console.WriteLine($"  Player Velocity: {playerLaunchVelocity:F0} m/s");

            if (playerLaunchDelayTime < minLaunchDelayTime || playerLaunchDelayTime > maxLaunchDelayTime)
            {
                Console.WriteLine($"  ✗ VALIDATION FAILED: Launch delay time {playerLaunchDelayTime:F5}s outside range [{minLaunchDelayTime:F5}s, {maxLaunchDelayTime:F5}s]");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            if (playerTargetElevation < -90 || playerTargetElevation > 90)
            {
                Console.WriteLine($"  ✗ VALIDATION FAILED: Elevation {playerTargetElevation:F1}° outside range [-90°, 90°]");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            if (playerTargetAzimuth < 0 || playerTargetAzimuth >= 360)
            {
                Console.WriteLine($"  ✗ VALIDATION FAILED: Azimuth {playerTargetAzimuth:F1}° outside range [0°, 360°)");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            if (playerLaunchVelocity <= 0 || playerLaunchVelocity > maxGunVelocity)
            {
                Console.WriteLine($"  ✗ VALIDATION FAILED: Velocity {playerLaunchVelocity:F0} m/s outside range (0, {maxGunVelocity:F0} m/s]");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            Console.WriteLine($"  ✓ All parameter validations passed");

            float minVelocity = CalculateRequiredVelocity();
            double playerKE_MJ = CalculateKineticEnergyMJ((float)playerLaunchVelocity);

            Console.WriteLine($"  Energy: {playerKE_MJ:F0} MJ required (fracture: {enemyFractureEnergy:F0} MJ)");

            Vector3 bestInterceptPoint = Vector3.Zero;
            double bestDeviation = double.MaxValue;
            double bestFlightTime = 0.0;

            double horizontalVelocity = playerLaunchVelocity * Math.Cos(playerTargetElevation * Math.PI / 180.0);
            double estimatedMaxFlightTime = horizontalVelocity > 0
                ? (gunEffectiveRange * 1.5) / horizontalVelocity
                : 60.0;

            Console.WriteLine($"  Estimated max flight time: {estimatedMaxFlightTime:F5}s");
            Console.WriteLine($"  Searching for intercept point with fine time resolution...");

            for (double testFlightTime = 0.001; testFlightTime <= estimatedMaxFlightTime; testFlightTime += 0.001)
            {
                Vector3 projectileAtFlight = CalculateProjectilePosition(testFlightTime, playerLaunchVelocity, playerTargetElevation, playerTargetAzimuth);
                Vector3 enemyAtFlight = CalculateEnemyPosition(playerLaunchDelayTime + testFlightTime, enemyInitialPosition, enemyVelocityVector);

                Vector3 deviation = projectileAtFlight - enemyAtFlight;
                double distance = deviation.Magnitude;

                if (distance < bestDeviation)
                {
                    bestDeviation = distance;
                    bestFlightTime = testFlightTime;
                    bestInterceptPoint = enemyAtFlight;
                }

                if (testFlightTime > 5.0 && bestDeviation < 1000.0 && distance > bestDeviation * 1.5)
                {
                    Console.WriteLine($"  Early exit: Projectile passed target at T+{testFlightTime:F5}s");
                    break;
                }
            }

            Console.WriteLine($"  Best intercept found at T+{playerLaunchDelayTime + bestFlightTime:F5}s (flight time: {bestFlightTime:F5}s)");
            Console.WriteLine($"    Intercept point: {bestInterceptPoint}");
            Console.WriteLine($"    Intercept distance from origin: {bestInterceptPoint.Magnitude:F0} m ({bestInterceptPoint.Magnitude / 1_000_000:F2} Mm)");
            Console.WriteLine($"    Deviation from target: {bestDeviation:F1} m");

            double interceptDistance = bestInterceptPoint.Magnitude;
            Console.WriteLine($"  Range check: {interceptDistance:F0} m vs {gunEffectiveRange:F0} m limit");

            if (interceptDistance > gunEffectiveRange)
            {
                Console.WriteLine($"  ✗ RANGE CHECK FAILED: Intercept point {interceptDistance:F0} m exceeds gun range {gunEffectiveRange:F0} m");
                Console.WriteLine($"    Margin: {gunEffectiveRange - interceptDistance:F0} m SHORT");
                return InvalidResult(bestInterceptPoint, playerLaunchDelayTime, playerKE_MJ);
            }

            Console.WriteLine($"  ✓ Range check passed (margin: {gunEffectiveRange - interceptDistance:F0} m)");

            // Calculate hit tolerance WITH difficulty modifiers
            float hitTolerance = CalculateHitTolerance(
                diffConfig.TargetRcsMultiplier,
                diffConfig.HitToleranceMultiplier);

            Console.WriteLine($"  Hit tolerance: {hitTolerance:F1} m, Actual deviation: {bestDeviation:F1} m");

            bool canHit = bestDeviation < hitTolerance;

            // In Easy mode (Nuclear Option), warhead guarantees destruction - no velocity check needed
            bool hasEnergy = difficulty == GameDifficulty.NuclearOption
                ? true  // Warhead always destroys regardless of velocity
                : playerKE_MJ >= enemyFractureEnergy;  // Other modes require kinetic energy

            bool isValid = hasEnergy && canHit;

            Console.WriteLine($"  Can destroy: {hasEnergy} ({(difficulty == GameDifficulty.NuclearOption ? "warhead guaranteed" : $"{playerKE_MJ:F0} vs {enemyFractureEnergy:F0} MJ")})");
            Console.WriteLine($"  Can hit: {canHit} ({bestDeviation:F1} vs {hitTolerance:F1} m)");
            Console.WriteLine($"  Solution valid: {isValid}");

            return new FiringSolutionResult
            {
                CanDestroy = hasEnergy,
                CanHit = canHit,
                SolutionValid = isValid,
                EnemyInterceptPoint = bestInterceptPoint,
                LaunchDelayTime = (float)playerLaunchDelayTime,
                TargetElevation = (float)playerTargetElevation,
                TargetAzimuth = (float)playerTargetAzimuth,
                MinVelocityRequired = minVelocity,
                MaxVelocityAvailable = maxGunVelocity,
                ProjectileVelocity = (float)playerLaunchVelocity,
                KineticEnergyMJ = playerKE_MJ,
                FractureEnergyRequired = enemyFractureEnergy,
                InterceptDeviation = (float)bestDeviation,
                Message = isValid ? "✓ Direct hit!" : "✗ Miss"
            };
        }

        private FiringSolutionResult InvalidResult(Vector3 enemyInterceptPoint, double launchDelayTime, double kineticEnergyMJ = 0.0)
        {
            return new FiringSolutionResult
            {
                CanDestroy = false,
                CanHit = false,
                SolutionValid = false,
                EnemyInterceptPoint = enemyInterceptPoint,
                LaunchDelayTime = (float)launchDelayTime,
                KineticEnergyMJ = kineticEnergyMJ,
                FractureEnergyRequired = enemyFractureEnergy,
                Message = "✗ Miss"
            };
        }
    }
}