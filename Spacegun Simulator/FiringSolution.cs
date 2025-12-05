using System;

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

    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static Vector3 Zero => new(0f, 0f, 0f);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
        public static Vector3 operator /(Vector3 v, float s) => new(v.X / s, v.Y / s, v.Z / s);

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

    /// <summary>
    /// Represents a complete firing problem generated for the player.
    /// Enemy is at 1000-1200km at T+0s. Player must calculate intercept parameters.
    /// </summary>
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
        private const float GRAVITY = 9.81f;
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
        /// </summary>
        public static Vector3 AnglesToCartesian(float elevationDeg, float azimuthDeg, float distance)
        {
            float elevationRad = elevationDeg * (float)Math.PI / 180f;
            float azimuthRad = azimuthDeg * (float)Math.PI / 180f;

            float horizontalDistance = distance * (float)Math.Cos(elevationRad);
            float z = distance * (float)Math.Sin(elevationRad);

            float x = horizontalDistance * (float)Math.Sin(azimuthRad);
            float y = horizontalDistance * (float)Math.Cos(azimuthRad);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Convert 3D Cartesian coordinates back to elevation/azimuth angles.
        /// </summary>
        public static (float elevation, float azimuth) CartesianToAngles(Vector3 position)
        {
            float distance = position.Magnitude;
            if (distance < 0.0001f)
                return (0, 0);

            float elevation = (float)Math.Atan2(position.Z,
                (float)Math.Sqrt(position.X * position.X + position.Y * position.Y));
            float azimuth = (float)Math.Atan2(position.X, position.Y);

            float elevationDeg = elevation * 180f / (float)Math.PI;
            float azimuthDeg = azimuth * 180f / (float)Math.PI;

            if (azimuthDeg < 0) azimuthDeg += 360f;

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
        /// Calculate hit tolerance as 0.5 × target diameter.
        /// </summary>
        private float CalculateHitTolerance()
        {
            float diameter = CalculateTargetDiameter();
            return diameter * 0.5f;
        }

        /// <summary>
        /// Calculate projectile position at time T (measured from firing, not engagement start).
        /// </summary>
        private Vector3 CalculateProjectilePosition(float flightTime, float launchVelocity, float elevationDeg, float azimuthDeg)
        {
            float elevationRad = elevationDeg * (float)Math.PI / 180f;
            float azimuthRad = azimuthDeg * (float)Math.PI / 180f;

            float vz = launchVelocity * (float)Math.Sin(elevationRad);
            float vHorizontal = launchVelocity * (float)Math.Cos(elevationRad);

            float vx = vHorizontal * (float)Math.Sin(azimuthRad);
            float vy = vHorizontal * (float)Math.Cos(azimuthRad);

            float x = vx * flightTime;
            float y = vy * flightTime;
            float z = vz * flightTime - 0.5f * GRAVITY * flightTime * flightTime;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Calculate enemy position at time T (measured from engagement start).
        /// </summary>
        private Vector3 CalculateEnemyPosition(float engagementTime, Vector3 initialPosition, Vector3 velocityVector)
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
            float initialEngagementDistance = 0f)  // Add parameter with default
        {
            if (wave is null) throw new ArgumentNullException(nameof(wave));
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            var target = wave.Targets[0];
            this.enemyMass = target.Mass;

            // STEP 1: Pick a random intercept time (5-30 seconds from engagement start)
            float interceptTime = 5f + (float)(rng.NextDouble() * 25f);

            // STEP 2: Generate T+0s angles (starting position)
            float approachElev = 20f + (float)(rng.NextDouble() * 50f);           // 20-70° elevation
            float approachAzim = (float)(rng.NextDouble() * 360f);               // 0-360° azimuth
            
            // Use provided distance or generate new one
            float approachDistance = initialEngagementDistance > 0f 
                ? initialEngagementDistance
                : 1_500_000 + (float)(rng.NextDouble() * 500_000);  // 1500-2000 km fallback

            Vector3 enemyAtT0 = AnglesToCartesian(approachElev, approachAzim, approachDistance);

            // STEP 3: Generate intercept angles with ENFORCED DRAMATIC ARC
            // Elevation delta: 45-90 degrees
            // Azimuth delta: 90-120 degrees
            float elevDelta = 45f + (float)(rng.NextDouble() * 45f);  // 45-90° change
            float azimDelta = 90f + (float)(rng.NextDouble() * 30f);  // 90-120° change

            // Apply elevation delta (can increase or decrease)
            float elevDirection = rng.NextDouble() < 0.5 ? -1f : 1f;
            float interceptElev = approachElev + (elevDirection * elevDelta);
            interceptElev = Math.Max(5f, Math.Min(85f, interceptElev));  // Clamp to valid range (5-85°)

            // Apply azimuth delta (clockwise or counter-clockwise)
            float azimDirection = rng.NextDouble() < 0.5 ? -1f : 1f;
            float interceptAzim = approachAzim + (azimDirection * azimDelta);
            if (interceptAzim < 0) interceptAzim += 360f;
            if (interceptAzim >= 360f) interceptAzim -= 360f;

            // Intercept distance: closer to Earth (20-50% closer)
            float distanceReduction = 0.2f + (float)(rng.NextDouble() * 0.3f);
            float interceptDistance = approachDistance * (1f - distanceReduction);
            interceptDistance = Math.Max(1_000_000f, Math.Min(2_000_000f, interceptDistance));

            Vector3 interceptPoint = AnglesToCartesian(interceptElev, interceptAzim, interceptDistance);

            // STEP 4: Work backwards to find enemy velocity
            // Calculate velocity vector to move from T+0s position to intercept position
            Vector3 displacement = interceptPoint - enemyAtT0;
            Vector3 enemyVelocity = (displacement * (1f / interceptTime));

            // Verify the velocity magnitude matches the wave average
            float calculatedSpeed = enemyVelocity.Magnitude;
            float speedDifference = Math.Abs(calculatedSpeed - (float)wave.AverageVelocity);
            if (speedDifference > (float)wave.AverageVelocity * 0.1f)
            {
                // Velocity doesn't match - normalize it to match wave velocity
                enemyVelocity = (enemyVelocity / calculatedSpeed) * (float)wave.AverageVelocity;
            }

            // STEP 5: Calculate angle change (for narrative - visible arc)
            float elevationDeltaActual = Math.Abs(interceptElev - approachElev);
            float azimuthDeltaActual = Math.Abs(interceptAzim - approachAzim);
            if (azimuthDeltaActual > 180f) azimuthDeltaActual = 360f - azimuthDeltaActual;  // Get shortest arc

            // STEP 6: Calculate correct solution parameters
            float correctVelocity = playerGunMaxVelocity * 0.85f;
            float correctElevation = interceptElev;
            float correctAzimuth = interceptAzim;

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
                CorrectElevation = correctElevation,
                CorrectAzimuth = correctAzimuth,
                CorrectVelocity = correctVelocity
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

            // Dynamically determine search window based on enemy velocity
            // Slow enemies need longer windows to stay in gun range
            float enemySpeed = enemyVelocity.Magnitude;
            float maxSearchTime = enemySpeed > 0 
                ? Math.Min(600f, (gunEffectiveRange * 1.5f) / enemySpeed)  // Time for enemy to travel 1.5x gun range
                : 600f;
            
            Console.WriteLine($"        Enemy speed: {enemySpeed:F0} m/s, search window: 2-{maxSearchTime:F1}s");

            // Search for intercept within dynamic time window
            for (float engagementTime = 2f; engagementTime <= maxSearchTime; engagementTime += 0.5f)
            {
                Vector3 enemyAtEngagementT = CalculateEnemyPosition(engagementTime, enemyInitialPosition, enemyVelocity);

                // Enemy must still be above horizon
                if (enemyAtEngagementT.Z <= 0)
                    continue;

                // Enemy must be within gun range by this time
                float distanceToEnemy = enemyAtEngagementT.Magnitude;
                if (distanceToEnemy > gunEffectiveRange)
                    continue;

                // Search for launch parameters with coarse angles
                for (float elev = 5f; elev <= 85f; elev += 5f)
                {
                    for (float azim = 0f; azim < 360f; azim += 30f)
                    {
                        for (float vel = minVelocity; vel <= maxGunVelocity; vel += Math.Max(5000f, (maxGunVelocity - minVelocity) / 5f))
                        {
                            // Test reasonable flight times
                            for (float flightTime = 0.5f; flightTime <= Math.Min(30f, engagementTime); flightTime += 0.5f)
                            {
                                // Enemy position at intercept time
                                Vector3 enemyAtIntercept = CalculateEnemyPosition(engagementTime + flightTime, enemyInitialPosition, enemyVelocity);

                                // Projectile position after flight
                                Vector3 projectileAtIntercept = CalculateProjectilePosition(flightTime, vel, elev, azim);

                                // Check if intercept is valid
                                Vector3 deviation = projectileAtIntercept - enemyAtIntercept;
                                float distance = deviation.Magnitude;

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
        /// </summary>
        public FiringSolutionResult CalculateSolution(
            Vector3 enemyInitialPosition,
            Vector3 enemyVelocityVector,
            float playerLaunchDelayTime,
            float playerTargetElevation,
            float playerTargetAzimuth,
            float playerLaunchVelocity,
            float maxGunVelocity,
            float gunEffectiveRange,
            int waveNumber = 1,
            double enemyMass = 10000.0)
        {
            this.enemyMass = enemyMass;

            var tier = GameConstants.GetTierForWave(waveNumber);
            float minLaunchDelayTime = 0f;  // ALLOW 0s - immediate fire is valid
            float maxLaunchDelayTime = tier.TierIndex switch
            {
                0 => 60f,
                1 => 120f,
                2 => 180f,
                _ => 180f
            };

            // Validate launch delay time - allow 0s as a valid option
            if (playerLaunchDelayTime < minLaunchDelayTime || playerLaunchDelayTime > maxLaunchDelayTime)
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);

            if (playerTargetElevation < -90 || playerTargetElevation > 90)
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);

            if (playerTargetAzimuth < 0 || playerTargetAzimuth >= 360)
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);

            if (playerLaunchVelocity <= 0 || playerLaunchVelocity > maxGunVelocity)
                return InvalidResult(enemyInitialPosition, playerLaunchDelayTime);

            // Calculate actual flight time from velocity and angles
            // Projectile position = launchVelocity * time (minus gravity drop on Z)
            // We need to find when projectile reaches intercept point
            // Use iterative approach: try different flight times
            Vector3 bestInterceptPoint = Vector3.Zero;
            float bestDeviation = float.MaxValue;
            float bestFlightTime = 10f;
            
            // STEP 1: Search for best intercept flight time
            for (float testFlightTime = 0.1f; testFlightTime <= 60f; testFlightTime += 0.1f)
            {
                // Projectile position after testFlightTime seconds of flight
                Vector3 projectileAtFlight = CalculateProjectilePosition(testFlightTime, playerLaunchVelocity, playerTargetElevation, playerTargetAzimuth);

                // Enemy position at T+playerLaunchDelayTime + testFlightTime
                Vector3 enemyAtFlight = CalculateEnemyPosition(playerLaunchDelayTime + testFlightTime, enemyInitialPosition, enemyVelocityVector);

                // Check if they intercept (within tolerance)
                Vector3 deviation = projectileAtFlight - enemyAtFlight;
                float distance = deviation.Magnitude;

                // Track best intercept
                if (distance < bestDeviation)
                {
                    bestDeviation = distance;
                    bestFlightTime = testFlightTime;
                    bestInterceptPoint = enemyAtFlight;
                }
            }

            float minVelocity = CalculateRequiredVelocity();
            double playerKE_MJ = CalculateKineticEnergyMJ(playerLaunchVelocity);
            
            // Check intercept is within gun range
            if (bestInterceptPoint.Magnitude > gunEffectiveRange)
                return InvalidResult(bestInterceptPoint, playerLaunchDelayTime);

            float hitTolerance = CalculateHitTolerance();
            bool canHit = bestDeviation < hitTolerance;
            bool hasEnergy = playerKE_MJ >= enemyFractureEnergy;
            bool isValid = hasEnergy && canHit;

            return new FiringSolutionResult
            {
                CanDestroy = hasEnergy,
                CanHit = canHit,
                SolutionValid = isValid,
                EnemyInterceptPoint = bestInterceptPoint,
                LaunchDelayTime = playerLaunchDelayTime,
                TargetElevation = playerTargetElevation,
                TargetAzimuth = playerTargetAzimuth,
                MinVelocityRequired = minVelocity,
                MaxVelocityAvailable = maxGunVelocity,
                ProjectileVelocity = playerLaunchVelocity,
                KineticEnergyMJ = playerKE_MJ,
                FractureEnergyRequired = enemyFractureEnergy,
                InterceptDeviation = bestDeviation,
                Message = isValid ? "✓ Direct hit!" : "✗ Miss"
            };
        }

        private FiringSolutionResult InvalidResult(Vector3 enemyInterceptPoint, float launchDelayTime)
        {
            return new FiringSolutionResult
            {
                CanDestroy = false,
                CanHit = false,
                SolutionValid = false,
                EnemyInterceptPoint = enemyInterceptPoint,
                LaunchDelayTime = launchDelayTime,
                FractureEnergyRequired = enemyFractureEnergy,
                Message = "✗ Miss"
            };
        }
    }
}