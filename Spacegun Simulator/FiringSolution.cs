using System;

namespace Spacegun_Simulator
{
    // ============================================================================
    // FIRING SOLUTION - 3D Ballistic Intercept System
    // ============================================================================
    // Calculates 3D ballistic trajectories for intercepting moving targets.
    //
    // PLAYER IS GIVEN:
    // - Enemy's current position expressed as ELEVATION and AZIMUTH (in sky)
    // - Enemy's velocity vector expressed as ELEVATION RATE and AZIMUTH RATE (degrees/second)
    // - Target fracture energy
    // - Gun's max velocity
    //
    // PLAYER MUST CALCULATE AND INPUT:
    // 1. INTERCEPT TIME (seconds) - When will projectile and enemy meet?
    // 2. LAUNCH VELOCITY (m/s) - How hard to fire?
    // 3. TARGET ELEVATION (degrees) - Where in the sky to aim?
    // 4. TARGET AZIMUTH (degrees) - Which compass direction to aim?
    //
    // SYSTEM CALCULATES:
    // - Where enemy will be at player's intercept time
    // - Whether projectile trajectory reaches that point
    // - Kinetic energy at impact
    // - Solution validity

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

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);

        public override string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
    }

    public class FiringSolutionResult
    {
        public bool CanDestroy { get; set; }
        public bool CanHit { get; set; }
        public bool SolutionValid { get; set; }
        public Vector3? EnemyInterceptPoint { get; set; }
        public float InterceptTime { get; set; }
        public float TargetElevation { get; set; }
        public float TargetAzimuth { get; set; }
        public float MinVelocityRequired { get; set; }
        public float MaxVelocityAvailable { get; set; }
        public float ProjectileVelocity { get; set; }
        public double KineticEnergyMJ { get; set; }
        public double FractureEnergyRequired { get; set; }
        public float InterceptDeviation { get; set; } // Distance off in meters
        public string Message { get; set; } = string.Empty;
    }

    public class FiringSolution
    {
        private const float GRAVITY = 9.81f; // m/s²
        private float projectileMass;
        private float enemyFractureEnergy;

        public FiringSolution(float projectileMass, float enemyFractureEnergy)
        {
            this.projectileMass = projectileMass;
            this.enemyFractureEnergy = enemyFractureEnergy;
        }

        /// <summary>
        /// Convert elevation/azimuth angles (in degrees) to 3D Cartesian coordinates.
        /// Assumes distance of 1 AU (150 million km) for reference frame.
        /// Elevation: 0° = horizon, 90° = zenith
        /// Azimuth: 0° = North, 90° = East, 180° = South, 270° = West
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
        /// Returns (elevation, azimuth) as a tuple.
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

        /// <summary>
        /// Calculate minimum projectile velocity needed to destroy enemy.
        /// Using KE = 0.5 * m * v^2, solve for v
        /// </summary>
        public float CalculateRequiredVelocity()
        {
            float requiredVelocity = (float)Math.Sqrt(2 * enemyFractureEnergy / projectileMass);
            return requiredVelocity;
        }

        /// <summary>
        /// Calculate kinetic energy in megajoules from mass and velocity.
        /// KE = 0.5 * m * v^2, result in MJ
        /// FIXED: Use explicit double casting to avoid integer overflow
        /// </summary>
        public double CalculateKineticEnergyMJ(float velocity)
        {
            // Convert to double to maintain precision through calculation
            double v = velocity;
            double m = projectileMass;
            
            double energyJoules = 0.5 * m * v * v;
            return energyJoules / 1_000_000.0;
        }

        /// <summary>
        /// Calculate projectile position at time t given launch parameters.
        /// Assumes launch from origin (0,0,0).
        /// </summary>
        private Vector3 CalculateProjectilePosition(float time, float launchVelocity, float elevationDeg, float azimuthDeg)
        {
            float elevationRad = elevationDeg * (float)Math.PI / 180f;
            float azimuthRad = azimuthDeg * (float)Math.PI / 180f;

            // Velocity components
            float vz = launchVelocity * (float)Math.Sin(elevationRad); // Vertical
            float vHorizontal = launchVelocity * (float)Math.Cos(elevationRad); // Horizontal magnitude

            float vx = vHorizontal * (float)Math.Sin(azimuthRad); // East component
            float vy = vHorizontal * (float)Math.Cos(azimuthRad); // North component

            // Position = v*t - 0.5*g*t²
            float x = vx * time;
            float y = vy * time;
            float z = vz * time - 0.5f * GRAVITY * time * time; // Gravity pulls down

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Calculate enemy position at given time.
        /// Enemy starts at initial 3D position and moves along velocity vector.
        /// </summary>
        private Vector3 CalculateEnemyPosition(float time, Vector3 initialPosition, Vector3 velocityVector)
        {
            return initialPosition + (velocityVector * time);
        }

        /// <summary>
        /// Calculate the maximum intercept time before target descends below horizon.
        /// Returns the time when target reaches Z = 0 (horizon).
        /// </summary>
        private float CalculateMaxInterceptTime(Vector3 enemyCurrentPosition, Vector3 enemyVelocityVector)
        {
            // Target position: P(t) = P0 + V*t
            // We need: Z(t) = Z0 + Vz*t > 0
            // Solve for when Z(t) = 0: t = -Z0 / Vz (if Vz < 0)
            
            if (enemyVelocityVector.Z >= 0)
            {
                // Enemy ascending or level - no time constraint
                return float.MaxValue;
            }

            // Enemy descending - calculate when it reaches horizon
            float maxTime = -enemyCurrentPosition.Z / enemyVelocityVector.Z;
            
            // Return 95% of max time to provide safety margin
            return maxTime * 0.95f;
        }

        /// <summary>
        /// Validate if a valid firing solution is theoretically possible for these parameters.
        /// During wave generation, checks if ANY valid solution exists (not if random params hit).
        /// Returns true if playable scenario is possible.
        /// </summary>
        public bool CanProduceValidSolution(
            Vector3 enemyCurrentPosition,
            Vector3 enemyVelocityVector,
            float maxGunVelocity,
            float minInterceptTime,
            float maxInterceptTime)
        {
            // Test multiple intercept times within the valid range
            float step = 0.5f;
            int testsRun = 0;
            int validSolutionsFound = 0;
            float minRequiredVelocity = CalculateRequiredVelocity();
            
            // If minimum required velocity exceeds gun capability, no solution possible
            if (minRequiredVelocity > maxGunVelocity)
            {
                Console.WriteLine($"      Min velocity required ({minRequiredVelocity:F0} m/s) exceeds max gun velocity ({maxGunVelocity:F0} m/s)");
                return false;
            }
            
            for (float t = minInterceptTime; t <= maxInterceptTime; t += step)
            {
                // Calculate where enemy will be at this time
                Vector3 enemyAtT = CalculateEnemyPosition(t, enemyCurrentPosition, enemyVelocityVector);
                
                // CRITICAL: Enemy must be above horizon
                if (enemyAtT.Z <= 0)
                    continue;
                
                // Try various elevation angles (sample the space)
                for (float elev = 5f; elev <= 80f; elev += 15f)
                {
                    for (float azim = 0f; azim < 360f; azim += 90f)
                    {
                        // Clamp velocity range to ensure we test from min required to max available
                        float velStart = Math.Min(minRequiredVelocity, maxGunVelocity);
                        float velStep = Math.Max(1000f, (maxGunVelocity - velStart) / 4f);  // Test at least 4 points
                        
                        for (float vel = velStart; vel <= maxGunVelocity; vel += velStep)
                        {
                            testsRun++;
                            Vector3 projectileAtT = CalculateProjectilePosition(t, vel, elev, azim);
                            
                            // Check projectile doesn't go below ground during flight
                            bool projectileAboveGround = true;
                            for (float checkT = 0; checkT <= t; checkT += Math.Max(0.1f, t / 20f))
                            {
                                Vector3 checkPos = CalculateProjectilePosition(checkT, vel, elev, azim);
                                if (checkPos.Z < 0)
                                {
                                    projectileAboveGround = false;
                                    break;
                                }
                            }
                            
                            if (!projectileAboveGround)
                                continue;
                            
                            // Final elevation must be 5° to 80° (relaxed from 0-85 for better margin)
                            var (finalElev, _) = CartesianToAngles(projectileAtT);
                            if (finalElev < 5f || finalElev > 80f)
                                continue;
                            
                            // RELAXED: Check if projectile has sufficient energy
                            // (We don't need perfect intercept during wave gen - just energy potential)
                            double projectileKE_MJ = CalculateKineticEnergyMJ(vel);
                            if (projectileKE_MJ < enemyFractureEnergy)
                                continue;
                            
                            // If all structural checks pass, a valid solution exists
                            validSolutionsFound++;
                            Console.WriteLine($"      Found valid solution: t={t:F2}s, elev={elev:F0}°, vel={vel:F0} m/s → KE={projectileKE_MJ:F1} MJ");
                            return true;  // ONE valid solution proves the wave is playable
                        }
                    }
                }
            }
            
            Console.WriteLine($"      No valid solution found: {testsRun} combinations tested, {validSolutionsFound} viable approaches.");
            return false;
        }

        /// <summary>
        /// Calculate complete 3D ballistic firing solution.
        /// Silently rejects invalid inputs without user-facing error messages.
        /// </summary>
        public FiringSolutionResult CalculateSolution(
            Vector3 enemyCurrentPosition,
            Vector3 enemyVelocityVector,
            float playerInterceptTime,
            float playerTargetElevation,
            float playerTargetAzimuth,
            float playerLaunchVelocity,
            float maxGunVelocity,
            int waveNumber = 1)
        {
            // Determine weapon tier for intercept time constraints
            var tier = GameConstants.GetTierForWave(waveNumber);
            float minInterceptTime = 2f;
            float maxInterceptTime = tier.TierIndex switch
            {
                0 => 15f,    // Early game: 2-15 seconds
                1 => 30f,    // Mid game: 2-30 seconds
                2 => 60f,    // Late game: 2-60 seconds
                _ => 60f     // Default to late game
            };

            // Silent validation - no messages to player
            if (playerInterceptTime <= 0 || playerInterceptTime < minInterceptTime || playerInterceptTime > maxInterceptTime)
            {
                return InvalidResult(enemyCurrentPosition, playerInterceptTime);
            }

            if (playerTargetElevation < -90 || playerTargetElevation > 90)
            {
                return InvalidResult(enemyCurrentPosition, playerInterceptTime);
            }

            if (playerTargetAzimuth < 0 || playerTargetAzimuth >= 360)
            {
                return InvalidResult(enemyCurrentPosition, playerInterceptTime);
            }

            if (playerLaunchVelocity <= 0 || playerLaunchVelocity > maxGunVelocity)
            {
                return InvalidResult(enemyCurrentPosition, playerInterceptTime);
            }

            // Calculate trajectories
            Vector3 enemyAtInterceptTime = CalculateEnemyPosition(playerInterceptTime, enemyCurrentPosition, enemyVelocityVector);
            Vector3 projectileAtInterceptTime = CalculateProjectilePosition(
                playerInterceptTime,
                playerLaunchVelocity,
                playerTargetElevation,
                playerTargetAzimuth);

            // Calculate kinetic energy NOW (before any early returns)
            float minVelocity = CalculateRequiredVelocity();
            double playerKE_MJ = CalculateKineticEnergyMJ(playerLaunchVelocity);

            // Check projectile doesn't go below ground
            bool projectileAboveGround = true;
            for (float t = 0; t <= playerInterceptTime; t += Math.Max(0.1f, playerInterceptTime / 20f))
            {
                Vector3 pos = CalculateProjectilePosition(t, playerLaunchVelocity, playerTargetElevation, playerTargetAzimuth);
                if (pos.Z < 0)
                {
                    projectileAboveGround = false;
                    break;
                }
            }

            if (!projectileAboveGround)
            {
                return InvalidResult(enemyAtInterceptTime, playerInterceptTime, minVelocity, playerKE_MJ);
            }

            // CRITICAL: Final elevation must be in valid firing range
            // Allow negative elevations for descending targets, but cap at -45° floor
            var (finalElevation, _) = CartesianToAngles(projectileAtInterceptTime);
            if (finalElevation < -45 || finalElevation > 85)
            {
                return InvalidResult(enemyAtInterceptTime, playerInterceptTime, minVelocity, playerKE_MJ);
            }

            // Calculate miss distance
            Vector3 deviationVector = projectileAtInterceptTime - enemyAtInterceptTime;
            float interceptDeviation = deviationVector.Magnitude;

            // Hit check
            bool canHit = interceptDeviation < 1.0f;

            // Energy check
            bool hasEnergy = playerKE_MJ >= enemyFractureEnergy;

            bool isValid = hasEnergy && canHit;

            // Simple feedback - only hit/miss, no detailed explanations
            string message = isValid ? "✓ Direct hit!" : "✗ Miss";

            return new FiringSolutionResult
            {
                CanDestroy = hasEnergy,
                CanHit = canHit,
                SolutionValid = isValid,
                EnemyInterceptPoint = enemyAtInterceptTime,
                InterceptTime = playerInterceptTime,
                TargetElevation = playerTargetElevation,
                TargetAzimuth = playerTargetAzimuth,
                MinVelocityRequired = minVelocity,
                MaxVelocityAvailable = maxGunVelocity,
                ProjectileVelocity = playerLaunchVelocity,
                KineticEnergyMJ = playerKE_MJ,  // ← NOW SET EARLY
                FractureEnergyRequired = enemyFractureEnergy,
                InterceptDeviation = interceptDeviation,
                Message = message
            };
        }

        private FiringSolutionResult InvalidResult(Vector3 enemyInterceptPoint, float interceptTime)
        {
            return new FiringSolutionResult
            {
                CanDestroy = false,
                CanHit = false,
                SolutionValid = false,
                EnemyInterceptPoint = enemyInterceptPoint,
                InterceptTime = interceptTime,
                FractureEnergyRequired = enemyFractureEnergy,
                Message = "✗ Miss"
            };
        }

        private FiringSolutionResult InvalidResult(Vector3 enemyInterceptPoint, float interceptTime, float minVelocity, double playerKE_MJ)
        {
            return new FiringSolutionResult
            {
                CanDestroy = false,
                CanHit = false,
                SolutionValid = false,
                EnemyInterceptPoint = enemyInterceptPoint,
                InterceptTime = interceptTime,
                MinVelocityRequired = minVelocity,
                KineticEnergyMJ = playerKE_MJ,  // ← POPULATE ON INVALID RESULTS TOO
                FractureEnergyRequired = enemyFractureEnergy,
                Message = "✗ Miss"
            };
        }
    }
}