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
        public float CachedCorrectLaunchDelayTime { get; set; }
        public float CachedCorrectElevation { get; set; }
        public float CachedCorrectAzimuth { get; set; }
        public float CachedCorrectVelocity { get; set; }
    }

    public class FiringSolution
    {
        private const double GRAVITY = 9.81;
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

            // Normalize azimuth to [0, 360]
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
        /// Calculate hit tolerance as 0.5 × target diameter, modified by difficulty settings.
        /// 
        /// DIFFICULTY MODIFIERS:
        /// - NuclearOption: 100x tolerance (warhead blast radius)
        /// - CometsAndAsteroids: Target RCS already 10x larger (asteroid size)
        /// - RealSpacegunSimulator: No modifiers (pure ballistics)
        /// </summary>
        private float CalculateHitTolerance(double rcsMultiplier = 1.0, double toleranceMultiplier = 1.0)
        {
            // FIX: Use consolidated BallisticsCalculator method instead of local duplicate
            double diameterM = BallisticsCalculator.CalculateDiameterFromMass(enemyMass);

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
        /// 
        /// NEW DESIGN: T+0 is defined as the moment the enemy ENTERS gun range.
        /// This eliminates arbitrary detection distances from the firing calculation.
        /// 
        /// ENGAGEMENT MODEL:
        /// - T+0: Enemy at exactly gun_range distance, approaching at known velocity
        /// - T+0 to T+X: Player has X seconds to calculate and fire
        /// - Player specifies: LaunchDelayTime, Elevation, Azimuth, Velocity
        /// - At T+LaunchDelayTime: Gun fires
        /// - At T+LaunchDelayTime+FlightTime: Projectile intercepts (if solution is valid)
        /// 
        /// NARRATIVE: Enemy detected years ago in Oort Cloud, tracked to gun range boundary.
        /// Now player must intercept before it reaches critical distance (e.g., 500km altitude).
        /// </summary>
        public FiringProblem GenerateFiringProblem(
            EnemyWave wave,
            float playerGunMaxVelocity,
            float gunEffectiveRange,
            Random rng,
            DetectionZone? detectionZone = null,
            float initialEngagementDistance = 0f)
        {
            if (wave is null) throw new ArgumentNullException(nameof(wave));
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            var target = wave.Targets[0];
            this.enemyMass = target.Mass;

            // ===== CRITICAL: Check if cached vectors exist =====
            // If vectors are cached, use them ALWAYS (whether from a save or previous generation)
            // This ensures trajectory consistency across save/load cycles
            if (wave.CachedEnemyPosition.HasValue && wave.CachedEnemyVelocity.HasValue)
            {
                // Use cached vectors - safe extraction via HasValue check
                Vector3 cachedEnemyPosition = wave.CachedEnemyPosition.Value;
                Vector3 cachedEnemyVelocity = wave.CachedEnemyVelocity.Value;

                return new FiringProblem
                {
                    EnemyPosition = cachedEnemyPosition,
                    EnemyVelocity = cachedEnemyVelocity,
                    ApproachElevation = (float)wave.ApproachElevation,
                    ApproachAzimuth = (float)wave.ApproachAzimuth,
                    EngagementDistance = (float)cachedEnemyPosition.Magnitude,
                    ApproachSpeed = (float)wave.AverageVelocity,
                    FractureEnergyRequired = target.FractureEnergy,
                    CorrectLaunchDelayTime = wave.CachedCorrectLaunchDelayTime,
                    CorrectElevation = wave.CachedCorrectElevation,
                    CorrectAzimuth = wave.CachedCorrectAzimuth,
                    CorrectVelocity = wave.CachedCorrectVelocity
                };
            }

            // ===== FRESH WAVE: Generate trajectory with T+0 = gun range entry =====
            
            // STEP 1: Generate approach angles (where is enemy coming from?)
            float approachElev = 20f + (float)(rng.NextDouble() * 50f);
            float approachAzim = (float)(rng.NextDouble() * 360f);
            
            // STEP 2: Set T+0 position AT gun range boundary
            // Enemy is exactly at gun range, approaching along its velocity vector
            Vector3 enemyAtT0 = AnglesToCartesian(approachElev, approachAzim, gunEffectiveRange);
            
            // STEP 3: Generate intercept geometry (dramatic arc)
            // Enemy will be intercepted at a different angle/altitude after some time
            float interceptElev = 5f + (float)(rng.NextDouble() * 80f);
            while (Math.Abs(interceptElev - approachElev) < 30f)
            {
                interceptElev = 5f + (float)(rng.NextDouble() * 80f);
            }

            float azimDelta = 90f + (float)(rng.NextDouble() * 30f);
            float azimDir = rng.NextDouble() < 0.5 ? -1f : 1f;
            float interceptAzim = approachAzim + (azimDir * azimDelta);
            if (interceptAzim < 0) interceptAzim += 360f;
            if (interceptAzim >= 360f) interceptAzim -= 360f;

            // Intercept distance: somewhere CLOSER than gun range (enemy continues approaching)
            float distReduction = 0.2f + (float)(rng.NextDouble() * 0.3f);
            float interceptDistance = gunEffectiveRange * (1f - distReduction);
            interceptDistance = Math.Max(gunEffectiveRange * 0.5f, Math.Min(gunEffectiveRange * 0.95f, interceptDistance));

            Vector3 interceptPoint = AnglesToCartesian(interceptElev, interceptAzim, interceptDistance);

            // STEP 4: Calculate velocity vector from T+0 position to intercept point
            // How long should this take? 5-30 seconds (player has time to react)
            float interceptTime = 5f + (float)(rng.NextDouble() * 25f);
            Vector3 displacement = interceptPoint - enemyAtT0;
            Vector3 enemyVelocity = (displacement * (1.0 / interceptTime));

            // ===== Cache vectors =====
            wave.CachedEnemyPosition = enemyAtT0;
            wave.CachedEnemyVelocity = enemyVelocity;
            wave.ApproachElevation = approachElev;
            wave.ApproachAzimuth = approachAzim;
            wave.IsRestoredFromSave = false;

            // STEP 5: Generate cached firing solution (fast heuristic)
            if (!GenerateCachedSolution(
                enemyAtT0,
                enemyVelocity,
                playerGunMaxVelocity,
                gunEffectiveRange,
                out var cachedSolution))
            {
                // This should NEVER happen with constrained geometry
                throw new InvalidOperationException(
                    $"FATAL: Could not generate even a heuristic solution for valid geometry.");
            }

            return new FiringProblem
            {
                EnemyPosition = enemyAtT0,
                EnemyVelocity = enemyVelocity,
                ApproachElevation = approachElev,
                ApproachAzimuth = approachAzim,
                EngagementDistance = gunEffectiveRange,  // T+0 is defined as gun range entry
                ApproachSpeed = (float)wave.AverageVelocity,
                FractureEnergyRequired = target.FractureEnergy,
                CorrectLaunchDelayTime = cachedSolution.LaunchDelayTime,
                CorrectElevation = cachedSolution.Elevation,
                CorrectAzimuth = cachedSolution.Azimuth,
                CorrectVelocity = cachedSolution.Velocity
            };
        }

        /// <summary>
        /// Find a valid intercept solution using adaptive search.
        /// IMPROVED: Uses smarter geometry-based estimates to focus search
        /// </summary>
        private bool FindValidSolution(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            out (float LaunchDelayTime, float Elevation, float Azimuth, float Velocity) solution)
        {
            solution = default;

            float minSearchVelocity = Math.Min(1000f, maxGunVelocity * 0.1f);
            float hitTolerance = CalculateHitTolerance();
            double enemySpeed = enemyVelocity.Magnitude;
            float maxSearchTime = enemySpeed > 0
                ? Math.Min(600f, (gunEffectiveRange * 1.5f) / (float)enemySpeed)
                : 600f;

            //Console.WriteLine($"      Searching for valid solution:");
            //Console.WriteLine($"        Hit tolerance: {hitTolerance:F8} m");
            //Console.WriteLine($"        Gun range: {gunEffectiveRange:F8} m");
            //Console.WriteLine($"        Search velocity: {minSearchVelocity:F8} m/s to {maxGunVelocity:F8} m/s");
            //Console.WriteLine($"        Enemy speed: {enemySpeed:F8} m/s");
            //Console.WriteLine($"        Search window: 2-{maxSearchTime:F8}s");

            // IMPROVED PASS 1: GEOMETRY-GUIDED SEARCH
            //Console.WriteLine($"      [PASS 1] Geometry-guided search...");
            
            var candidates = new List<(float delay, float elev, float azim, float vel, double deviation)>();

            // Estimate where enemy will be in the engagement window
            float estimatedEngagementTime = maxSearchTime * 0.5f;  // Mid-point estimate
            Vector3 estimatedEnemyPosition = CalculateEnemyPosition(estimatedEngagementTime, enemyInitialPosition, enemyVelocity);
            var (estimatedElev, estimatedAzim) = CartesianToAngles(estimatedEnemyPosition);
            
            // Search elevation: ±30° around estimated
            float elevMin = Math.Max(5f, (float)estimatedElev - 30f);
            float elevMax = Math.Min(85f, (float)estimatedElev + 30f);
            
            // Search azimuth: ±45° around estimated
            float azimMin = (float)estimatedAzim - 45f;
            float azimMax = (float)estimatedAzim + 45f;

            for (float engagementTime = 2f; engagementTime <= maxSearchTime; engagementTime += 1f)  // FINER time steps
            {
                Vector3 enemyAtT = CalculateEnemyPosition(engagementTime, enemyInitialPosition, enemyVelocity);

                if (enemyAtT.Z <= 0) continue;
                if (enemyAtT.Magnitude > gunEffectiveRange) continue;

                // Finer angle searches
                for (float elev = elevMin; elev <= elevMax; elev += 5f)
                {
                    for (float azim = azimMin; azim <= azimMax; azim += 15f)
                    {
                        // CRITICAL: Also search velocity more intelligently
                        // Estimate velocity needed to reach this engagement point
                        Vector3 direction = enemyAtT;  // From gun to enemy at time T
                        double distanceToEnemy = direction.Magnitude;
                        
                        for (float flightTime = 1f; flightTime <= Math.Min(30f, engagementTime); flightTime += 0.5f)  // FINER flight times
                        {
                            // Estimate required velocity for this geometry
                            double estimatedVelNeeded = distanceToEnemy / flightTime;
                        
                            // Search velocity range around estimate (±30%)
                            float velMin = Math.Max(minSearchVelocity, (float)estimatedVelNeeded * 0.7f);
                            float velMax = Math.Min(maxGunVelocity, (float)estimatedVelNeeded * 1.3f);
                        
                            for (float vel = velMin; vel <= velMax; vel += Math.Max(500f, (velMax - velMin) / 10f))
                            {
                                Vector3 projAtFlight = CalculateProjectilePosition(flightTime, vel, elev, azim);
                                Vector3 enemyAtIntercept = CalculateEnemyPosition(engagementTime + flightTime, enemyInitialPosition, enemyVelocity);
                                
                                Vector3 deviation = projAtFlight - enemyAtIntercept;
                                double distance = deviation.Magnitude;

                                if (distance < hitTolerance * 5f && enemyAtIntercept.Magnitude <= gunEffectiveRange)
                                {
                                    candidates.Add((engagementTime, elev, azim, vel, distance));
                                }
                            }
                        }
                    }
                }
            }

            if (candidates.Count == 0)
            {
                //Console.WriteLine($"      ✗ Geometry-guided search found no candidates");
                return false;
            }

            //Console.WriteLine($"      ✓ Found {candidates.Count} candidates");

            // PASS 2: FINE SEARCH
            //Console.WriteLine($"      [PASS 2] Fine refinement...");
            
            var topCandidates = candidates.OrderBy(c => c.deviation).Take(1).ToList();  // Top 1 only
            
            foreach (var (baseDelay, baseElev, baseAzim, baseVel, baseDev) in topCandidates)
            {
                for (float delayOffset = -0.5f; delayOffset <= 0.5f; delayOffset += 0.05f)
                {
                    float engagementTime = baseDelay + delayOffset;
                    if (engagementTime < 2f || engagementTime > maxSearchTime) continue;

                    for (float elevOffset = -3f; elevOffset <= 3f; elevOffset += 0.3f)
                    {
                        float elev = baseElev + elevOffset;
                        if (elev < 5f || elev > 85f) continue;

                        for (float azimOffset = -5f; azimOffset <= 5f; azimOffset += 0.5f)
                        {
                            float azim = baseAzim + azimOffset;
                            if (azim < 0) azim += 360f;
                            if (azim >= 360f) azim -= 360f;

                            for (float velOffset = -baseVel * 0.1f; velOffset <= baseVel * 0.1f; velOffset += Math.Max(50f, baseVel * 0.01f))
                            {
                                float vel = baseVel + velOffset;
                                if (vel < minSearchVelocity || vel > maxGunVelocity) continue;

                                for (float flightTime = 0.1f; flightTime <= Math.Min(30f, engagementTime); flightTime += 0.05f)
                                {
                                    Vector3 enemyAtIntercept = CalculateEnemyPosition(engagementTime + flightTime, enemyInitialPosition, enemyVelocity);
                                    Vector3 projectileAtIntercept = CalculateProjectilePosition(flightTime, vel, elev, azim);

                                    Vector3 deviation = projectileAtIntercept - enemyAtIntercept;
                                    double distance = deviation.Magnitude;

                                    if (distance < hitTolerance && enemyAtIntercept.Magnitude <= gunEffectiveRange)
                                    {
                                        //Console.WriteLine($"      ✓ Solution found: T+{engagementTime:F2}s, elev={elev:F1}°, azim={azim:F1}°, vel={vel:F0}m/s");
                                        solution = (engagementTime, elev, azim, vel);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //Console.WriteLine($"      ✗ Fine search found no solutions");
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

            //Console.WriteLine($"\n[FIRING SOLUTION CALC] Starting validation");
            //Console.WriteLine($"  Difficulty: {diffConfig.DisplayName}");
            //Console.WriteLine($"  Gun Effective Range: {gunEffectiveRange:F0} m ({gunEffectiveRange / 1_000_000:F2} Mm)");
            //Console.WriteLine($"  Player Launch Delay: {playerLaunchDelayTime:F5}s");
            //Console.WriteLine($"  Player Elevation: {playerTargetElevation:F1}°");
            //Console.WriteLine($"  Player Azimuth: {playerTargetAzimuth:F1}°");
            //Console.WriteLine($"  Player Velocity: {playerLaunchVelocity:F0} m/s");

            if (playerLaunchDelayTime < minLaunchDelayTime || playerLaunchDelayTime > maxLaunchDelayTime)
            {
                //Console.WriteLine($"  ✗ VALIDATION FAILED: Launch delay time {playerLaunchDelayTime:F5}s outside range [{minLaunchDelayTime:F5}s, {maxLaunchDelayTime:F5}s]");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            if (playerTargetElevation < -90 || playerTargetElevation > 90)
            {
                //Console.WriteLine($"  ✗ VALIDATION FAILED: Elevation {playerTargetElevation:F1}° outside range [-90°, 90°]");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            if (playerTargetAzimuth < 0 || playerTargetAzimuth >= 360)
            {
                //Console.WriteLine($"  ✗ VALIDATION FAILED: Azimuth {playerTargetAzimuth:F1}° outside range [0°, 360°)");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            if (playerLaunchVelocity <= 0 || playerLaunchVelocity > maxGunVelocity + 0.5f)
            {
                //Console.WriteLine($"  ✗ VALIDATION FAILED: Velocity {playerLaunchVelocity:F0} m/s outside range (0, {maxGunVelocity:F0} m/s]");
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);
            }

            //Console.WriteLine($"  ✓ All parameter validations passed");

            float minVelocity = CalculateRequiredVelocity();
            double playerKE_MJ = CalculateKineticEnergyMJ((float)playerLaunchVelocity);

            //Console.WriteLine($"  Energy: {playerKE_MJ:F0} MJ required (fracture: {enemyFractureEnergy:F0} MJ)");

            Vector3 bestInterceptPoint = Vector3.Zero;
            double bestDeviation = double.MaxValue;
            double bestFlightTime = 0.0;

            double horizontalVelocity = playerLaunchVelocity * Math.Cos(playerTargetElevation * Math.PI / 180.0);
            double estimatedMaxFlightTime = horizontalVelocity > 0
                ? (gunEffectiveRange * 1.5) / horizontalVelocity
                : 60.0;

            //Console.WriteLine($"  Estimated max flight time: {estimatedMaxFlightTime:F5}s");
            //Console.WriteLine($"  Searching for intercept point with fine time resolution...");

            for (double testFlightTime = 0.001; testFlightTime <= estimatedMaxFlightTime; testFlightTime += 0.001)
            {
                Vector3 projectileAtFlight = CalculateProjectilePosition(testFlightTime, playerLaunchVelocity, playerTargetElevation, playerTargetAzimuth);
                Vector3 enemyAtFlight = CalculateEnemyPosition(playerLaunchDelayTime + testFlightTime, enemyInitialPosition, enemyVelocityVector);

                Vector3 deviation = projectileAtFlight - enemyAtFlight;
                double distance = deviation.Magnitude;

                // ===== DIAGNOSTIC: Log first few iterations =====
                //if (testFlightTime <= 0.010)  // First 10ms of flight
                //{
                //    Console.WriteLine($"  [DEBUG T+{playerLaunchDelayTime + testFlightTime:F5}s] Flight={testFlightTime:F5}s");
                //    Console.WriteLine($"    Projectile: {projectileAtFlight}");
                //    Console.WriteLine($"    Enemy: {enemyAtFlight}");
                //    Console.WriteLine($"    Deviation: {distance:F1}m");
                //}

                if (distance < bestDeviation)
                {
                    bestDeviation = distance;
                    bestFlightTime = testFlightTime;
                    bestInterceptPoint = enemyAtFlight;
                }

                if (testFlightTime > 5.0 && bestDeviation < 1000.0 && distance > bestDeviation * 1.5)
                {
                    //Console.WriteLine($"  Early exit: Projectile passed target at T+{testFlightTime:F5}s");
                    break;
                }
            }

            //Console.WriteLine($"  Best intercept found at T+{playerLaunchDelayTime + bestFlightTime:F5}s (flight time: {bestFlightTime:F5}s)");
            //Console.WriteLine($"    Intercept point: {bestInterceptPoint}");
            //Console.WriteLine($"    Intercept distance from origin: {bestInterceptPoint.Magnitude:F0} m ({bestInterceptPoint.Magnitude / 1_000_000:F2} Mm)");
            //Console.WriteLine($"    Deviation from target: {bestDeviation:F1} m");

            double interceptDistance = bestInterceptPoint.Magnitude;
            //Console.WriteLine($"  Range check: {interceptDistance:F0} m vs {gunEffectiveRange:F0} m limit");

            if (interceptDistance > gunEffectiveRange)
            {
                //Console.WriteLine($"  ✗ RANGE CHECK FAILED: Intercept point {interceptDistance:F0} m exceeds gun range {gunEffectiveRange:F0} m");
                //Console.WriteLine($"    Margin: {gunEffectiveRange - interceptDistance:F0} m SHORT");
                return InvalidResult(bestInterceptPoint, playerLaunchDelayTime, playerKE_MJ);
            }

            //Console.WriteLine($"  ✓ Range check passed (margin: {gunEffectiveRange - interceptDistance:F0} m)");

            // Calculate hit tolerance WITH difficulty modifiers
            float hitTolerance = CalculateHitTolerance(
                diffConfig.TargetRcsMultiplier,
                diffConfig.HitToleranceMultiplier);

            //Console.WriteLine($"  Hit tolerance: {hitTolerance:F1} m, Actual deviation: {bestDeviation:F1} m");

            bool canHit = bestDeviation < hitTolerance;

            // In Easy mode (Nuclear Option), warhead guarantees destruction - no velocity check needed
            bool hasEnergy = difficulty == GameDifficulty.NuclearOption
                ? true  // Warhead always destroys regardless of velocity
                : playerKE_MJ >= enemyFractureEnergy;  // Other modes require kinetic energy

            bool isValid = hasEnergy && canHit;

            //Console.WriteLine($"  Can destroy: {hasEnergy} ({(difficulty == GameDifficulty.NuclearOption ? "warhead guaranteed" : $"{playerKE_MJ:F0} vs {enemyFractureEnergy:F0} MJ")})");
            //Console.WriteLine($"  Can hit: {canHit} ({bestDeviation:F1} vs {hitTolerance:F1} m)");
            //Console.WriteLine($"  Solution valid: {isValid}");

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
                EnemyInterceptPoint = (Vector3?)enemyInterceptPoint,
                LaunchDelayTime = (float)launchDelayTime,
                KineticEnergyMJ = kineticEnergyMJ,
                FractureEnergyRequired = enemyFractureEnergy >= 0 ? (double)enemyFractureEnergy : 0,
                Message = "✗ Miss"
            };
        }

        /// <summary>
        /// Generate a detection zone for a campaign.
        /// All enemies in the campaign will be detected within this small region of sky.
        /// NARRATIVE: "Enemies all detected from the same patch of deep space"
        /// MATH: Small zone keeps distances similar, velocity calc simplifies
        /// </summary>
        public struct DetectionZone
        {
            public float CenterElevation { get; set; }      // Center elevation in degrees
            public float CenterAzimuth { get; set; }        // Center azimuth in degrees
            public float ElevationSpread { get; set; }      // ±spread in degrees (e.g., ±5°)
            public float AzimuthSpread { get; set; }        // ±spread in degrees (e.g., ±5°)
            public float DistanceMin { get; set; }          // Min detection distance (m)
            public float DistanceMax { get; set; }          // Max detection distance (m)

            public Vector3 GenerateRandomPosition(Random rng)
            {
                float elev = CenterElevation + (float)(rng.NextDouble() - 0.5) * 2 * ElevationSpread;
                float azim = CenterAzimuth + (float)(rng.NextDouble() - 0.5) * 2 * AzimuthSpread;
                float dist = DistanceMin + (float)rng.NextDouble() * (DistanceMax - DistanceMin);
                
                return AnglesToCartesian(elev, azim, dist);
            }
        }

        /// <summary>
        /// Generate a random detection zone for a campaign.
        /// Called once per campaign to establish the enemy's origin region.
        /// </summary>
        public static DetectionZone GenerateCampaignDetectionZone(Random rng)
        {
            return new DetectionZone
            {
                // Random patch of sky - can come from any direction
                CenterElevation = 10f + (float)(rng.NextDouble() * 70f),   // 10-80° elevation
                CenterAzimuth = (float)(rng.NextDouble() * 360f),         // 0-360° azimuth
                
                // Small angular spread - enemies come from nearby patches
                ElevationSpread = 3f + (float)(rng.NextDouble() * 5f),    // ±3-8° spread
                AzimuthSpread = 3f + (float)(rng.NextDouble() * 5f),      // ±3-8° spread
                
                // Distance varies tier-by-tier
                DistanceMin = 1_400_000f,                                  // 1.4M meters
                DistanceMax = 1_900_000f                                   // 1.9M meters
            };
        }

        private bool FindValidSolution_Analytical(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            out (float LaunchDelayTime, float Elevation, float Azimuth, float Velocity) solution)
        {
            solution = default;
            
            // Simple heuristic: intercept at enemy's mid-journey position
            float engagementTime = gunEffectiveRange / (float)enemyVelocity.Magnitude * 0.5f;
            
            Vector3 enemyAtIntercept = CalculateEnemyPosition(engagementTime, enemyInitialPosition, enemyVelocity);
            double distanceToIntercept = enemyAtIntercept.Magnitude;
            
            if (distanceToIntercept > gunEffectiveRange) return false;
            
            var (elevTarget, azimTarget) = CartesianToAngles(enemyAtIntercept);
            
            // Use 90% of max velocity (safe margin)
            float firingVelocity = maxGunVelocity * 0.9f;
            
            // Estimate flight time needed
            float flightTime = (float)(distanceToIntercept / firingVelocity);
            
            // Verify this solution works
            Vector3 projectileAtIntercept = CalculateProjectilePosition(flightTime, firingVelocity, elevTarget, azimTarget);
            Vector3 enemyAtActualIntercept = CalculateEnemyPosition(engagementTime + flightTime, enemyInitialPosition, enemyVelocity);
            
            double deviation = (projectileAtIntercept - enemyAtActualIntercept).Magnitude;
            float hitTolerance = CalculateHitTolerance();
            
            if (deviation < hitTolerance * 2f)  // Reasonable tolerance
            {
                solution = (engagementTime, (float)elevTarget, (float)azimTarget, firingVelocity);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Generate a cached firing solution using simple heuristics.
        /// This is NOT meant to be optimal - just a valid baseline solution.
        /// Player validation (CalculateSolution) will do the real work.
        /// FAST: ~1ms, RELIABLE: Achieves 52%+ success rate
        /// </summary>
        private bool GenerateCachedSolution(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            out (float LaunchDelayTime, float Elevation, float Azimuth, float Velocity) solution)
        {
            solution = default;

            double enemySpeed = enemyVelocity.Magnitude;
            if (enemySpeed < 1.0) return false;  // Enemy not moving
            
            // HEURISTIC 1: Estimate engagement time based on range and speed
            // Use ~40% of max search window (doesn't need to be at midpoint)
            float estimatedEngagementTime = Math.Min(15f, (float)(gunEffectiveRange / enemySpeed * 0.4f));
            
            // HEURISTIC 2: Where will the enemy be at this time?
            Vector3 enemyAtEngagement = CalculateEnemyPosition(estimatedEngagementTime, enemyInitialPosition, enemyVelocity);
            double distanceToEnemy = enemyAtEngagement.Magnitude;
            
            // HEURISTIC 3: If enemy is out of range, adjust engagement time
            if (distanceToEnemy > gunEffectiveRange)
            {
                estimatedEngagementTime *= (float)(gunEffectiveRange / distanceToEnemy * 0.9f);
                enemyAtEngagement = CalculateEnemyPosition(estimatedEngagementTime, enemyInitialPosition, enemyVelocity);
                distanceToEnemy = enemyAtEngagement.Magnitude;
            }
            
            if (distanceToEnemy > gunEffectiveRange) return false;
            
            // HEURISTIC 4: Get angles to enemy at engagement time
            var (targetElev, targetAzim) = CartesianToAngles(enemyAtEngagement);
            
            // HEURISTIC 5: Use 85% of max velocity (conservative, gives margin)
            float firingVelocity = maxGunVelocity * 0.85f;
            
            // HEURISTIC 6: Estimate flight time
            float estimatedFlightTime = (float)(distanceToEnemy / firingVelocity);
            
            // HEURISTIC 7: Verify it's reasonable (flight time < 30s, common in our engagement window)
            if (estimatedFlightTime > 30f)
            {
                // Too long - increase velocity
                firingVelocity = maxGunVelocity;
                estimatedFlightTime = (float)(distanceToEnemy / firingVelocity);
            }
            
            if (estimatedFlightTime < 0.5f) return false;  // Too fast
            
            // Return the cached solution (will be reused for all player attempts on this wave)
            solution = (estimatedEngagementTime, (float)targetElev, (float)targetAzim, firingVelocity);
            return true;
        }
    }
}