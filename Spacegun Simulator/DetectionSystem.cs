namespace Spacegun_Simulator
{
    // ============================================================================
    // DETECTION SYSTEM - Detection Phase Only
    // ============================================================================
    // IMPORTANT: Detection operates on a completely separate scale from engagement.
    // Detection distance is measured in AU (Oort Cloud distances).
    // Engagement distance is measured in meters (tactical 1000-1200km range).
    // These are NOT compared against each other.

    public class DetectionSystem
    {
        /// <summary>
        /// Multiplier applied to the tier's base detection range.
        /// 1.0 = standard detection, > 1.0 = improved through research
        /// </summary>
        public double DetectionRangeMultiplier { get; set; }
        
        public double TrackingAccuracy { get; set; }
        public double RefreshRate { get; set; }
        public int MaxSimultaneousTargets { get; set; }
        public RadarType RadarType { get; set; }
        public bool HasSpaceBasedRadar { get; set; }
        public bool HasQuantumEntanglementComm { get; set; }
        public double SignalProcessingPower { get; set; }

        public DetectionSystem()
        {
            DetectionRangeMultiplier = 1.0;
            TrackingAccuracy = 0.6;
            RefreshRate = 10.0;
            MaxSimultaneousTargets = 5;
            RadarType = RadarType.GroundBased;
            HasSpaceBasedRadar = false;
            HasQuantumEntanglementComm = false;
            SignalProcessingPower = 1.0;
        }

        public double CalculateWarningTime(EnemyWave wave)
        {
            // Calculate time until impact at current distance
            double timeToImpact = wave.CurrentDistance / wave.AverageVelocity;
            return Math.Max(0, timeToImpact);
        }

        /// <summary>
        /// Determine if wave is within DETECTION range (in AU).
        /// CRITICAL: This operates on detection-phase distance (AU), NOT engagement distance (meters).
        /// Detection range is purely for determining if the wave is detected in the detection phase.
        /// Engagement happens at a fixed 1000-1200km range regardless of detection distance.
        /// </summary>
        public double CalculateEffectiveDetectionRange(EnemyWave wave)
        {
            var tier = GameConstants.GetTierForWave(wave.WaveNumber);
            
            // Base detection range in AU (from tier definition)
            double detectionRangeAU = tier.DetectionRangeMax / GameConstants.AU_TO_METERS;

            // Stealth coating reduces detection range significantly
            if (wave.HasStealthCoating)
            {
                detectionRangeAU *= 0.1;  // 90% reduction with stealth
            }

            // Space-based radar extends range
            if (HasSpaceBasedRadar)
            {
                detectionRangeAU *= 1.2;  // 20% bonus
            }

            return detectionRangeAU;
        }

        public DetectionStatus GetDetectionStatus(EnemyWave wave)
        {
            double warningTime = CalculateWarningTime(wave);
            
            // Get detection range in AU
            double effectiveDetectionRangeAU = CalculateEffectiveDetectionRange(wave);
            
            // Convert wave distance to AU for comparison
            double waveDistanceAU = wave.CurrentDistance / GameConstants.AU_TO_METERS;

            // Check if detected (comparing AU to AU)
            if (waveDistanceAU > effectiveDetectionRangeAU)
            {
                return new DetectionStatus
                {
                    IsDetected = false,
                    WarningTime = 0,
                    Quality = DetectionQuality.None,
                    Message = "No contacts detected"
                };
            }

            // Check tracking capacity
            if (wave.TargetCount > MaxSimultaneousTargets)
            {
                return new DetectionStatus
                {
                    IsDetected = true,
                    WarningTime = warningTime,
                    Quality = DetectionQuality.Degraded,
                    Message = $"WARNING: Tracking capacity exceeded ({wave.TargetCount}/{MaxSimultaneousTargets} targets)"
                };
            }

            // Check minimum safe time based on tier
            var tier = GameConstants.GetTierForWave(wave.WaveNumber);
            double minimumSafeTime = tier.TimeToImpactMin * 0.1;  // 10% of minimum viable time

            if (warningTime < minimumSafeTime)
            {
                return new DetectionStatus
                {
                    IsDetected = true,
                    WarningTime = warningTime,
                    Quality = DetectionQuality.Emergency,
                    Message = $"EMERGENCY: Insufficient warning time ({GameConstants.FormatTime(warningTime)})"
                };
            }

            return new DetectionStatus
            {
                IsDetected = true,
                WarningTime = warningTime,
                Quality = DetectionQuality.Optimal,
                Message = $"Tracking {wave.TargetCount} contact{(wave.TargetCount != 1 ? "s" : "")}, {GameConstants.FormatTime(warningTime)} to engagement"
            };
        }
    }

    public class DetectionStatus
    {
        public bool IsDetected { get; set; }
        public double WarningTime { get; set; }
        public DetectionQuality Quality { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
