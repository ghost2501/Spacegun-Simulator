using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Ballistics
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

        private static List<double> SolveCubicReal(double a3, double a2, double a1, double a0)
        {
            var roots = new List<double>(3);

            const double eps = 1e-12;
            if (Math.Abs(a3) < eps)
            {
                // Degenerate: quadratic/linear.
                if (Math.Abs(a2) < eps)
                {
                    if (Math.Abs(a1) < eps) return roots;
                    roots.Add(-a0 / a1);
                    return roots;
                }

                double disc = (a1 * a1) - (4.0 * a2 * a0);
                if (disc < 0) return roots;
                double sqrt = Math.Sqrt(disc);
                roots.Add((-a1 - sqrt) / (2.0 * a2));
                roots.Add((-a1 + sqrt) / (2.0 * a2));
                return roots;
            }

            // Normalize: x^3 + ax^2 + bx + c = 0
            double a = a2 / a3;
            double b = a1 / a3;
            double c = a0 / a3;

            // Depressed cubic: x = u - a/3
            double aOver3 = a / 3.0;
            double p = b - (a * a) / 3.0;
            double q = (2.0 * a * a * a) / 27.0 - (a * b) / 3.0 + c;

            double halfQ = q / 2.0;
            double thirdP = p / 3.0;
            double discC = (halfQ * halfQ) + (thirdP * thirdP * thirdP);

            if (discC > eps)
            {
                // One real root.
                double sqrtDisc = Math.Sqrt(discC);
                double u = Math.Cbrt(-halfQ + sqrtDisc);
                double v = Math.Cbrt(-halfQ - sqrtDisc);
                roots.Add((u + v) - aOver3);
                return roots;
            }

            if (Math.Abs(discC) <= eps)
            {
                // Multiple real roots, at least two equal.
                double u = Math.Cbrt(-halfQ);
                roots.Add((2.0 * u) - aOver3);
                roots.Add((-u) - aOver3);
                return roots;
            }

            // Three distinct real roots.
            double r = Math.Sqrt(-thirdP * thirdP * thirdP);
            double phi = Math.Acos(Math.Clamp(-halfQ / r, -1.0, 1.0));
            double t = 2.0 * Math.Sqrt(-thirdP);

            roots.Add(t * Math.Cos(phi / 3.0) - aOver3);
            roots.Add(t * Math.Cos((phi + 2.0 * Math.PI) / 3.0) - aOver3);
            roots.Add(t * Math.Cos((phi + 4.0 * Math.PI) / 3.0) - aOver3);
            return roots;
        }

        private float projectileMass;
        private float enemyFractureEnergy;
        private double enemyMass;
        private double enemyCrossSectionM2;

        // Projectile modifiers (resolved at fire time)
        // Delta-V affects impact KE only (trajectory remains constant-velocity per solver model).
        private double additionalHitToleranceMultiplier = 1.0;
        private double propulsionDeltaVCapacityMs = 0.0;
        private double propulsionBurnDurationSeconds = 1.0;
        private double propulsionReferenceMassKg = 10.0;

        public FiringSolution(
            float projectileMass,
            float enemyFractureEnergy,
            double enemyMass = 10000.0,
            double enemyCrossSectionM2 = 0.0)
        {
            this.projectileMass = projectileMass;
            this.enemyFractureEnergy = enemyFractureEnergy;
            this.enemyMass = enemyMass;
            this.enemyCrossSectionM2 = enemyCrossSectionM2;
        }

        public void ConfigureProjectileModifiers(
            double additionalHitToleranceMultiplier,
            double propulsionDeltaVCapacityMs,
            double propulsionBurnDurationSeconds,
            double propulsionReferenceMassKg)
        {
            this.additionalHitToleranceMultiplier = Math.Max(0.1, additionalHitToleranceMultiplier);
            this.propulsionDeltaVCapacityMs = Math.Max(0.0, propulsionDeltaVCapacityMs);
            this.propulsionBurnDurationSeconds = Math.Max(0.1, propulsionBurnDurationSeconds);
            this.propulsionReferenceMassKg = Math.Max(0.01, propulsionReferenceMassKg);
        }

        public void ConfigureProjectileModifiers(in ResolvedShotStats stats)
        {
            ConfigureProjectileModifiers(
                additionalHitToleranceMultiplier: stats.AdditionalHitToleranceMultiplier,
                propulsionDeltaVCapacityMs: stats.PropulsionDeltaVCapacityMs,
                propulsionBurnDurationSeconds: stats.PropulsionBurnDurationSeconds,
                propulsionReferenceMassKg: stats.PropulsionReferenceMassKg);
        }

        private double CalculateEffectiveDeltaV(double flightTimeSeconds)
        {
            if (propulsionDeltaVCapacityMs <= 0.0) return 0.0;

            double burnRateMsPerSecond = propulsionDeltaVCapacityMs / propulsionBurnDurationSeconds;
            double burnLimitedDeltaV = Math.Min(flightTimeSeconds * burnRateMsPerSecond, propulsionDeltaVCapacityMs);

            double massKg = Math.Max(0.0, (double)projectileMass);
            double massEfficiency = propulsionReferenceMassKg / (propulsionReferenceMassKg + massKg);

            return burnLimitedDeltaV * massEfficiency;
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

        /// <summary>
        /// Calculate hit tolerance as 0.5 × target diameter, derived from target RCS area (m^2)
        /// and modified by difficulty settings.
        /// </summary>
        private float CalculateHitTolerance(
            double rcsMultiplier = 1.0,
            double toleranceMultiplier = 1.0,
            GameDifficulty difficulty = GameDifficulty.RealSpacegunSimulator,
            int waveNumber = 0)
        {
            // TUTORIAL MODE: Fixed 1m tolerance (beachball radius)
            // The mass-based diameter calculation doesn't work for lightweight objects
            var diffConfig = DifficultyConfig.GetConfig(difficulty);
            if (diffConfig.IsTutorialMode)
            {
                return (float)DifficultyConfig.TutorialBeachball.RadiusMeters;  // 1.0m
            }

            // STANDARD MODE: Derive diameter from RCS area.
            // Canonical meaning: enemyCrossSectionM2 is an area in m^2.
            double baseCrossSectionM2 = enemyCrossSectionM2;
            if (baseCrossSectionM2 <= 0.0)
            {
                // Back-compat fallback: infer cross-section from mass using the shared density assumption.
                double fallbackDiameterM = BallisticsCalculator.CalculateDiameterFromMass(enemyMass);
                double fallbackRadiusM = Math.Max(0.0, fallbackDiameterM) * 0.5;
                baseCrossSectionM2 = Math.PI * fallbackRadiusM * fallbackRadiusM;
            }

            double effectiveCrossSectionM2 = Math.Max(0.0, baseCrossSectionM2) * Math.Max(0.0, rcsMultiplier);
            double diameterM = 2.0 * Math.Sqrt(effectiveCrossSectionM2 / Math.PI);

            // Base tolerance is 0.5 × diameter
            double baseTolerance = diameterM * 0.5;

            // Optional per-tier tolerance scaling.
            // Default to 1.0 if no per-tier overrides are configured.
            double tierToleranceMult = 1.0;
            var tierMults = diffConfig.TierHitToleranceMultipliers;
            if (tierMults is { Length: > 0 } && waveNumber > 0)
            {
                int tierIndex = GameConstants.GetTierForWave(waveNumber).TierIndex;
                tierIndex = Math.Clamp(tierIndex, 0, tierMults.Length - 1);
                tierToleranceMult = Math.Max(0.0, tierMults[tierIndex]);
            }

            // Apply tolerance multiplier (for warhead blast radius)
            return (float)(baseTolerance * toleranceMultiplier * additionalHitToleranceMultiplier * tierToleranceMult);
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

        // Added: public static wrapper to compute projectile position identical to the instance method.
        // This allows test code to reuse the solver's exact trajectory math.

        public static Vector3 CalculateProjectilePositionStatic(double flightTime, double launchVelocity, double elevationDeg, double azimuthDeg)
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
        /// Computes hit tolerance in meters using the same formula as the solver.
        /// Intended for UI/tooling so displayed tolerances match the solver's actual checks.
        /// </summary>
        public static double CalculateHitToleranceMeters(
            GameDifficulty difficulty,
            int waveNumber,
            double enemyCrossSectionM2,
            double enemyMass,
            double additionalHitToleranceMultiplier)
        {
            var diffConfig = DifficultyConfig.GetConfig(difficulty);

            if (diffConfig.IsTutorialMode)
                return DifficultyConfig.TutorialBeachball.RadiusMeters;

            double baseCrossSectionM2 = enemyCrossSectionM2;
            if (baseCrossSectionM2 <= 0.0)
            {
                double fallbackDiameterM = BallisticsCalculator.CalculateDiameterFromMass(enemyMass);
                double fallbackRadiusM = Math.Max(0.0, fallbackDiameterM) * 0.5;
                baseCrossSectionM2 = Math.PI * fallbackRadiusM * fallbackRadiusM;
            }

            double effectiveCrossSectionM2 = Math.Max(0.0, baseCrossSectionM2) * Math.Max(0.0, diffConfig.TargetRcsMultiplier);
            double diameterM = 2.0 * Math.Sqrt(effectiveCrossSectionM2 / Math.PI);
            double baseTolerance = diameterM * 0.5;

            double tierToleranceMult = 1.0;
            var tierMults = diffConfig.TierHitToleranceMultipliers;
            if (tierMults is { Length: > 0 } && waveNumber > 0)
            {
                int tierIndex = GameConstants.GetTierForWave(waveNumber).TierIndex;
                tierIndex = Math.Clamp(tierIndex, 0, tierMults.Length - 1);
                tierToleranceMult = Math.Max(0.0, tierMults[tierIndex]);
            }

            return baseTolerance * diffConfig.HitToleranceMultiplier * additionalHitToleranceMultiplier * tierToleranceMult;
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

                // Ensure detection and engagement agree on the exact same speed.
                // Detection uses wave.AverageVelocity; engagement uses cachedEnemyVelocity magnitude.
                // We force wave.AverageVelocity (and target.Velocity) to match the cached vector.
                double cachedSpeed = cachedEnemyVelocity.Magnitude;
                wave.AverageVelocity = cachedSpeed;
                if (wave.Targets.Count > 0)
                    wave.Targets[0].Velocity = cachedSpeed;

                // If a wave was generated with cached vectors but without a cached "correct" solution
                // (e.g., gun range changed and the firing problem was regenerated), compute a fresh
                // baseline now and persist it back onto the wave.
                bool hasCachedSolution = wave.CachedCorrectVelocity > 0.0f;
                if (!hasCachedSolution)
                {
                    if (!GenerateCachedSolution(
                        cachedEnemyPosition,
                        cachedEnemyVelocity,
                        playerGunMaxVelocity,
                        gunEffectiveRange,
                        wave.WaveNumber,
                        out var computedCachedSolution))
                    {
                        var (fallbackElev, fallbackAzim) = CartesianToAngles(cachedEnemyPosition);
                        computedCachedSolution = (0.0f, (float)fallbackElev, (float)fallbackAzim, playerGunMaxVelocity);
                    }

                    wave.CachedCorrectLaunchDelayTime = computedCachedSolution.LaunchDelayTime;
                    wave.CachedCorrectElevation = computedCachedSolution.Elevation;
                    wave.CachedCorrectAzimuth = computedCachedSolution.Azimuth;
                    wave.CachedCorrectVelocity = computedCachedSolution.Velocity;
                }

                return new FiringProblem
                {
                    EnemyPosition = cachedEnemyPosition,
                    EnemyVelocity = cachedEnemyVelocity,
                    ApproachElevation = (float)wave.ApproachElevation,
                    ApproachAzimuth = (float)wave.ApproachAzimuth,
                    EngagementDistance = (float)cachedEnemyPosition.Magnitude,
                    ApproachSpeed = (float)wave.AverageVelocity,
                    FractureEnergyRequired = (double)enemyFractureEnergy,
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

            // IMPORTANT: The wave's speed is generated during detection phase (wave.AverageVelocity)
            // and displayed/used as the warning-time velocity. Engagement must use the exact same speed.
            // We pick a direction from the geometry above, then scale to wave.AverageVelocity.
            double dispMag = displacement.Magnitude;
            Vector3 direction;
            if (dispMag > 1e-9)
            {
                direction = displacement / dispMag;
            }
            else
            {
                // Degenerate geometry: default toward the origin.
                double posMag = enemyAtT0.Magnitude;
                direction = posMag > 1e-9 ? (enemyAtT0 / -posMag) : new Vector3(-1.0, 0.0, 0.0);
            }

            Vector3 enemyVelocity = direction * wave.AverageVelocity;
            if (wave.Targets.Count > 0)
                wave.Targets[0].Velocity = wave.AverageVelocity;

            // ===== Cache vectors =====
            wave.CachedEnemyPosition = enemyAtT0;
            wave.CachedEnemyVelocity = enemyVelocity;
            wave.ApproachElevation = approachElev;
            wave.ApproachAzimuth = approachAzim;
            wave.IsRestoredFromSave = false;

            // STEP 5: Generate cached firing solution (fast heuristic).
            // This is intended as a baseline/hint, not as a proof of solvability.
            // Diagnostics and gameplay validation use CalculateSolution (and can run more robust searches if needed).
            if (!GenerateCachedSolution(
                enemyAtT0,
                enemyVelocity,
                playerGunMaxVelocity,
                gunEffectiveRange,
                wave.WaveNumber,
                out var cachedSolution))
            {
                // Last-resort baseline: aim directly at the current target position at max velocity.
                var (fallbackElev, fallbackAzim) = CartesianToAngles(enemyAtT0);
                cachedSolution = (0.0f, (float)fallbackElev, (float)fallbackAzim, playerGunMaxVelocity);
            }

            // Persist the cached "correct" solution onto the wave so subsequent calls that reuse cached
            // vectors (e.g., after gun range changes) do not regress to default zero values.
            wave.CachedCorrectLaunchDelayTime = cachedSolution.LaunchDelayTime;
            wave.CachedCorrectElevation = cachedSolution.Elevation;
            wave.CachedCorrectAzimuth = cachedSolution.Azimuth;
            wave.CachedCorrectVelocity = cachedSolution.Velocity;

            return new FiringProblem
            {
                EnemyPosition = enemyAtT0,
                EnemyVelocity = enemyVelocity,
                ApproachElevation = approachElev,
                ApproachAzimuth = approachAzim,
                EngagementDistance = gunEffectiveRange,  // T+0 is defined as gun range entry
                ApproachSpeed = (float)wave.AverageVelocity,
                FractureEnergyRequired = (double)enemyFractureEnergy,
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
            int waveNumber,
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

            for (float engagementTime = 0f; engagementTime <= maxSearchTime; engagementTime += 0.5f)  // FINER time steps
            {
                Vector3 enemyAtT = CalculateEnemyPosition(engagementTime, enemyInitialPosition, enemyVelocity);

                // NOTE: Do not reject Z<=0 here.
                // In this coordinate system, valid engagement geometry can cross Z=0 within the allowed window,
                // and the solver itself does not require Z>0.
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

                        for (float flightTime = 1f; flightTime <= 30f; flightTime += 0.5f)  // FINER flight times
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
                    if (engagementTime < 0f || engagementTime > maxSearchTime) continue;

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

                                for (float flightTime = 0.1f; flightTime <= 30f; flightTime += 0.05f)
                                {
                                    Vector3 enemyAtIntercept = CalculateEnemyPosition(engagementTime + flightTime, enemyInitialPosition, enemyVelocity);
                                    Vector3 projectileAtIntercept = CalculateProjectilePosition(flightTime, vel, elev, azim);

                                    Vector3 deviation = projectileAtIntercept - enemyAtIntercept;
                                    double distance = deviation.Magnitude;

                                    if (distance < hitTolerance && enemyAtIntercept.Magnitude <= gunEffectiveRange)
                                    {
                                        // Validate using the authoritative solver so we never cache an invalid "correct" solution.
                                        var check = CalculateSolution(
                                            enemyInitialPosition,
                                            enemyVelocity,
                                            engagementTime,
                                            elev,
                                            azim,
                                            vel,
                                            maxGunVelocity,
                                            gunEffectiveRange,
                                            waveNumber,
                                            enemyMass,
                                            GameDifficulty.RealSpacegunSimulator);

                                        if (check.SolutionValid)
                                        {
                                            solution = (engagementTime, elev, azim, vel);
                                            return true;
                                        }
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

        private bool TryFindDeterministicValidSolution(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            int waveNumber,
            out (float LaunchDelayTime, float Elevation, float Azimuth, float Velocity) solution)
        {
            solution = default;

            static (double elevDeg, double azimDeg) CartesianToAnglesLocal(Vector3 position)
            {
                double horizontalDistance = Math.Sqrt(position.X * position.X + position.Y * position.Y);
                double elevationRad = Math.Atan2(position.Z, horizontalDistance);
                double elevationDeg = elevationRad * 180.0 / Math.PI;

                double azimuthRad = Math.Atan2(position.X, position.Y);
                double azimuthDeg = azimuthRad * 180.0 / Math.PI;
                if (azimuthDeg < 0) azimuthDeg += 360.0;
                return (elevationDeg, azimuthDeg);
            }

            static bool TrySolveInterceptTime(Vector3 relativePositionAtLaunch, Vector3 targetVelocity, double projectileSpeed, out double flightTime)
            {
                // Solve |r + v*t| = s*t where s=projectileSpeed.
                double rv = (relativePositionAtLaunch.X * targetVelocity.X)
                          + (relativePositionAtLaunch.Y * targetVelocity.Y)
                          + (relativePositionAtLaunch.Z * targetVelocity.Z);
                double vv = (targetVelocity.X * targetVelocity.X)
                          + (targetVelocity.Y * targetVelocity.Y)
                          + (targetVelocity.Z * targetVelocity.Z);
                double rr = (relativePositionAtLaunch.X * relativePositionAtLaunch.X)
                          + (relativePositionAtLaunch.Y * relativePositionAtLaunch.Y)
                          + (relativePositionAtLaunch.Z * relativePositionAtLaunch.Z);

                double a = vv - (projectileSpeed * projectileSpeed);
                double b = 2.0 * rv;
                double c = rr;

                const double eps = 1e-9;
                if (Math.Abs(a) < eps)
                {
                    if (Math.Abs(b) < eps)
                    {
                        flightTime = 0;
                        return false;
                    }

                    double t = -c / b;
                    if (t > 0)
                    {
                        flightTime = t;
                        return true;
                    }

                    flightTime = 0;
                    return false;
                }

                double disc = (b * b) - (4.0 * a * c);
                if (disc < 0)
                {
                    flightTime = 0;
                    return false;
                }

                double sqrt = Math.Sqrt(disc);
                double t1 = (-b - sqrt) / (2.0 * a);
                double t2 = (-b + sqrt) / (2.0 * a);

                double tMin = double.PositiveInfinity;
                if (t1 > 0) tMin = Math.Min(tMin, t1);
                if (t2 > 0) tMin = Math.Min(tMin, t2);

                if (double.IsInfinity(tMin))
                {
                    flightTime = 0;
                    return false;
                }

                flightTime = tMin;
                return true;
            }

            // Match solver's allowed delay window per tier.
            var tier = GameConstants.GetTierForWave(waveNumber);
            double maxLaunchDelayTime = tier.TierIndex switch
            {
                0 => 60.0,
                1 => 120.0,
                2 => 180.0,
                _ => 180.0
            };

            double[] velFracs = new[] { 1.0, 0.85, 0.70 };
            double[] elevOffsets = new[] { 0.0, 0.25, 0.5, 1.0, 2.0, -0.25, -0.5, -1.0 };
            double[] azimOffsets = new[] { 0.0, -2.0, 2.0, -1.0, 1.0, -0.5, 0.5 };

            bool bestHitFound = false;
            double bestHitDeviation = double.PositiveInfinity;
            bool bestHitHasEnergy = false;
            (float delay, float elev, float azim, float vel) bestHit = default;

            foreach (double frac in velFracs)
            {
                double v = Math.Max(1.0, maxGunVelocity * frac);

                for (double delay = 0.0; delay <= maxLaunchDelayTime; delay += 1.0)
                {
                    Vector3 enemyAtLaunch = enemyInitialPosition + (enemyVelocity * delay);
                    if (!TrySolveInterceptTime(enemyAtLaunch, enemyVelocity, v, out double tof))
                        continue;

                    tof = Math.Clamp(tof, 0.001, 300.0);
                    Vector3 enemyAtIntercept = enemyAtLaunch + (enemyVelocity * tof);
                    if (enemyAtIntercept.Magnitude > gunEffectiveRange)
                        continue;

                    double drop = 0.5 * 9.81 * tof * tof;
                    var aimPoint = new Vector3(enemyAtIntercept.X, enemyAtIntercept.Y, enemyAtIntercept.Z + drop);
                    var (elevBase, azimBase) = CartesianToAnglesLocal(aimPoint);

                    foreach (double de in elevOffsets)
                    {
                        double elev = elevBase + de;
                        if (elev < -89.9 || elev > 89.9) continue;

                        foreach (double da in azimOffsets)
                        {
                            double azim = azimBase + da;
                            if (azim < 0) azim += 360.0;
                            if (azim >= 360.0) azim -= 360.0;

                            var res = CalculateSolution(
                                enemyInitialPosition,
                                enemyVelocity,
                                delay,
                                elev,
                                azim,
                                v,
                                (float)maxGunVelocity,
                                (float)gunEffectiveRange,
                                waveNumber,
                                enemyMass,
                                GameDifficulty.RealSpacegunSimulator);

                            // Prefer fully successful solutions first.
                            if (res.CanHit && res.CanDestroy)
                            {
                                solution = ((float)delay, (float)elev, (float)azim, (float)v);
                                return true;
                            }

                            // Otherwise, keep the best accuracy-only solution so the game can
                            // surface "can hit but can't destroy" as a meaningful state.
                            if (res.CanHit)
                            {
                                double dev = Math.Max(0.0, res.InterceptDeviation);
                                bool take = !bestHitFound;

                                // Prefer smaller deviation; on ties, prefer higher velocity.
                                if (bestHitFound)
                                {
                                    const double devEps = 1e-6;
                                    if (dev + devEps < bestHitDeviation) take = true;
                                    else if (Math.Abs(dev - bestHitDeviation) <= devEps && v > bestHit.vel) take = true;
                                }

                                if (take)
                                {
                                    bestHitFound = true;
                                    bestHitDeviation = dev;
                                    bestHitHasEnergy = res.CanDestroy;
                                    bestHit = ((float)delay, (float)elev, (float)azim, (float)v);
                                }
                            }
                        }
                    }
                }
            }

            if (bestHitFound)
            {
                solution = bestHit;
                return true;
            }

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

            float hitTolerance = CalculateHitTolerance(
                diffConfig.TargetRcsMultiplier,
                diffConfig.HitToleranceMultiplier,
                difficulty,
                waveNumber);

            float minVelocity = CalculateRequiredVelocity();
            // Energy is computed after estimating intercept flight time, so propulsion Delta-V can
            // contribute to impact KE without changing trajectory math.

            Vector3 bestInterceptPoint = Vector3.Zero;
            double bestDeviation = double.MaxValue;
            double bestFlightTime = 0.0;
            bool foundInRange = false;

            // Analytic closest-approach solve.
            // We minimize D(t)=|P(t)-E(t)|^2 over flight time t>=0, where:
            // P(t) is projectile ballistic position after firing (with gravity in Z)
            // E(t) is enemy linear motion from engagement start.
            // D'(t)=0 yields a cubic, which we solve exactly for candidate extrema.

            double elevRad = playerTargetElevation * Math.PI / 180.0;
            double azimRad = playerTargetAzimuth * Math.PI / 180.0;
            double vz = playerLaunchVelocity * Math.Sin(elevRad);
            double vH = playerLaunchVelocity * Math.Cos(elevRad);
            double vx = vH * Math.Sin(azimRad);
            double vy = vH * Math.Cos(azimRad);

            Vector3 enemyAtLaunch = CalculateEnemyPosition(playerLaunchDelayTime, enemyInitialPosition, enemyVelocityVector);

            double dvx = vx - enemyVelocityVector.X;
            double dvy = vy - enemyVelocityVector.Y;
            double dvz = vz - enemyVelocityVector.Z;

            // Cubic coefficients for D'(t)=0 (see derivation in chat).
            // c3 t^3 + c2 t^2 + c1 t + c0 = 0
            double c3 = 0.5 * GRAVITY * GRAVITY;
            double c2 = -1.5 * GRAVITY * dvz;
            double c1 = (dvx * dvx) + (dvy * dvy) + (dvz * dvz) + (enemyAtLaunch.Z * GRAVITY);
            double c0 = (-dvx * enemyAtLaunch.X) + (-dvy * enemyAtLaunch.Y) - (enemyAtLaunch.Z * dvz);

            double horizontalVelocity = Math.Max(1.0, vH);
            double estimatedMaxFlightTime = (gunEffectiveRange * 1.5) / horizontalVelocity;
            double maxT = Math.Clamp(estimatedMaxFlightTime, 0.001, 300.0);

            void ConsiderCandidate(double t)
            {
                if (t <= 0.0 || t > maxT) return;

                Vector3 projectileAtFlight = CalculateProjectilePosition(t, playerLaunchVelocity, playerTargetElevation, playerTargetAzimuth);
                Vector3 enemyAtFlight = enemyAtLaunch + (enemyVelocityVector * t);
                if (enemyAtFlight.Magnitude > gunEffectiveRange) return;

                Vector3 deviation = projectileAtFlight - enemyAtFlight;
                double distance = deviation.Magnitude;
                if (distance < bestDeviation)
                {
                    foundInRange = true;
                    bestDeviation = distance;
                    bestFlightTime = t;
                    bestInterceptPoint = enemyAtFlight;
                }
            }

            // Endpoints + cubic extrema.
            ConsiderCandidate(0.001);
            ConsiderCandidate(maxT);

            foreach (double root in SolveCubicReal(c3, c2, c1, c0))
            {
                ConsiderCandidate(root);
            }

            // Small neighborhood sampling around best t to counter numerical edge cases.
            if (foundInRange)
            {
                double step = Math.Clamp(hitTolerance / Math.Max(1.0, playerLaunchVelocity + enemyVelocityVector.Magnitude), 1e-4, 0.02);
                for (int i = -5; i <= 5; i++)
                {
                    ConsiderCandidate(bestFlightTime + i * step);
                }
            }

            if (!foundInRange)
            {
                // No in-range intercept point was ever considered.
                // Return a consistent invalid result rather than accidentally using (0,0,0).
                double impactVelocityEstimate = playerLaunchVelocity + CalculateEffectiveDeltaV(0.0);
                double playerKeMjEstimate = BallisticsCalculator.CalculateKineticEnergyMJ(this.projectileMass, impactVelocityEstimate);
                return InvalidResult(enemyAtLaunch, playerLaunchDelayTime, playerKeMjEstimate);
            }

            //Console.WriteLine($"  Best intercept found at T+{playerLaunchDelayTime + bestFlightTime:F5}s (flight time: {bestFlightTime:F5}s)");
            //Console.WriteLine($"    Intercept point: {bestInterceptPoint}");
            //Console.WriteLine($"    Intercept distance from origin: {bestInterceptPoint.Magnitude:F0} m ({bestInterceptPoint.Magnitude / 1_000_000:F2} Mm)");
            //Console.WriteLine($"    Deviation from target: {bestDeviation:F1} m");

            double interceptDistance = bestInterceptPoint.Magnitude;
            //Console.WriteLine($"  Range check: {interceptDistance:F0} m vs {gunEffectiveRange:F0} m limit");

            double impactVelocity = playerLaunchVelocity + CalculateEffectiveDeltaV(bestFlightTime);
            double playerKE_MJ = BallisticsCalculator.CalculateKineticEnergyMJ(this.projectileMass, impactVelocity);

            if (interceptDistance > gunEffectiveRange)
            {
                //Console.WriteLine($"  ✗ RANGE CHECK FAILED: Intercept point {interceptDistance:F0} m exceeds gun range {gunEffectiveRange:F0} m");
                //Console.WriteLine($"    Margin: {gunEffectiveRange - interceptDistance:F0} m SHORT");
                return InvalidResult(bestInterceptPoint, playerLaunchDelayTime, playerKE_MJ);
            }

            //Console.WriteLine($"  ✓ Range check passed (margin: {gunEffectiveRange - interceptDistance:F0} m)");

            //Console.WriteLine($"  Hit tolerance: {hitTolerance:F1} m, Actual deviation: {bestDeviation:F1} m");

            bool canHit = bestDeviation <= hitTolerance;  // Changed from < to <=

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
                ImpactVelocityMs = impactVelocity,
                FlightTimeSeconds = bestFlightTime,
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
                ImpactVelocityMs = 0.0,
                FlightTimeSeconds = 0.0,
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
            int waveNumber,
            out (float LaunchDelayTime, float Elevation, float Azimuth, float Velocity) solution)
        {
            solution = default;

            double enemySpeed = enemyVelocity.Magnitude;
            if (enemySpeed < 1e-6) return false;

            static bool TrySolveInterceptTime(Vector3 relativePositionAtLaunch, Vector3 targetVelocity, double projectileSpeed, out double flightTime)
            {
                // Solve |r + v*t| = s*t where s=projectileSpeed.
                // Quadratic: (v·v - s^2) t^2 + 2(r·v) t + (r·r) = 0
                double rv = (relativePositionAtLaunch.X * targetVelocity.X)
                          + (relativePositionAtLaunch.Y * targetVelocity.Y)
                          + (relativePositionAtLaunch.Z * targetVelocity.Z);
                double vv = (targetVelocity.X * targetVelocity.X)
                          + (targetVelocity.Y * targetVelocity.Y)
                          + (targetVelocity.Z * targetVelocity.Z);
                double rr = (relativePositionAtLaunch.X * relativePositionAtLaunch.X)
                          + (relativePositionAtLaunch.Y * relativePositionAtLaunch.Y)
                          + (relativePositionAtLaunch.Z * relativePositionAtLaunch.Z);

                double a = vv - (projectileSpeed * projectileSpeed);
                double b = 2.0 * rv;
                double c = rr;

                const double eps = 1e-9;
                if (Math.Abs(a) < eps)
                {
                    if (Math.Abs(b) < eps)
                    {
                        flightTime = 0;
                        return false;
                    }

                    double t = -c / b;
                    if (t > 0)
                    {
                        flightTime = t;
                        return true;
                    }

                    flightTime = 0;
                    return false;
                }

                double disc = (b * b) - (4.0 * a * c);
                if (disc < 0)
                {
                    flightTime = 0;
                    return false;
                }

                double sqrt = Math.Sqrt(disc);
                double t1 = (-b - sqrt) / (2.0 * a);
                double t2 = (-b + sqrt) / (2.0 * a);

                double tMin = double.PositiveInfinity;
                if (t1 > 0) tMin = Math.Min(tMin, t1);
                if (t2 > 0) tMin = Math.Min(tMin, t2);

                if (double.IsInfinity(tMin))
                {
                    flightTime = 0;
                    return false;
                }

                flightTime = tMin;
                return true;
            }

            // Match solver's allowed delay window per tier.
            var tier = GameConstants.GetTierForWave(waveNumber);
            double maxLaunchDelayTime = tier.TierIndex switch
            {
                0 => 60.0,
                1 => 120.0,
                2 => 180.0,
                _ => 180.0
            };

            // Small, deterministic candidate set.
            double[] delayCandidates = new[] { 0.0, 1.0, 2.0, 5.0, 10.0, 15.0, 20.0, 30.0, 45.0, 60.0, 90.0, 120.0, 150.0, 180.0 };
            double[] velFracs = new[] { 1.0, 0.85, 0.70 };
            double[] elevOffsets = new[] { 0.0, 0.25, -0.25, 0.5, -0.5 };
            double[] azimOffsets = new[] { 0.0, 0.5, -0.5 };

            foreach (double frac in velFracs)
            {
                double v = Math.Max(1.0, maxGunVelocity * frac);

                foreach (double delay in delayCandidates)
                {
                    if (delay > maxLaunchDelayTime)
                        continue;

                    Vector3 enemyAtLaunch = enemyInitialPosition + (enemyVelocity * delay);
                    if (!TrySolveInterceptTime(enemyAtLaunch, enemyVelocity, v, out double tof))
                        continue;

                    tof = Math.Clamp(tof, 0.000001, 300.0);
                    Vector3 enemyAtIntercept = enemyAtLaunch + (enemyVelocity * tof);
                    if (enemyAtIntercept.Magnitude > gunEffectiveRange)
                        continue;

                    // Gravity compensation: aim above the predicted point by the drop distance.
                    double drop = 0.5 * GRAVITY * tof * tof;
                    var aimPoint = new Vector3(enemyAtIntercept.X, enemyAtIntercept.Y, enemyAtIntercept.Z + drop);
                    var (elevBase, azimBase) = CartesianToAngles(aimPoint);

                    foreach (double de in elevOffsets)
                    {
                        double elev = elevBase + de;
                        if (elev < -89.9 || elev > 89.9)
                            continue;

                        foreach (double da in azimOffsets)
                        {
                            double azim = azimBase + da;
                            if (azim < 0) azim += 360.0;
                            if (azim >= 360.0) azim -= 360.0;

                            // Validate against the solver using the strict non-tutorial rules.
                            var res = CalculateSolution(
                                enemyInitialPosition,
                                enemyVelocity,
                                delay,
                                elev,
                                azim,
                                v,
                                maxGunVelocity,
                                gunEffectiveRange,
                                waveNumber,
                                enemyMass,
                                GameDifficulty.RealSpacegunSimulator);

                            if (res.SolutionValid && res.CanHit)
                            {
                                solution = ((float)delay, (float)elev, (float)azim, (float)v);
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}