namespace Spacegun_Simulator
{
    // ============================================================================
    // DETECTION SYSTEM
    // ============================================================================

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
            // Detection uses tier-based ranges multiplied by this value
            DetectionRangeMultiplier = 1.0;
            TrackingAccuracy = 0.6;
            RefreshRate = 10.0;
            MaxSimultaneousTargets = 5;
            RadarType = RadarType.GroundBased;
            HasSpaceBasedRadar = false;
            HasQuantumEntanglementComm = false;
            SignalProcessingPower = 1.0;
        }

        /// <summary>
        /// Get the base detection range for a wave's tier, before modifiers.
        /// Uses the tier's maximum detection range as baseline.
        /// </summary>
        private double GetTierBaseRange(EnemyWave wave)
        {
            var tier = GameConstants.GetTierForWave(wave.WaveNumber);
            return tier.DetectionRangeMax * DetectionRangeMultiplier;
        }

        public double CalculateWarningTime(EnemyWave wave)
        {
            double detectionRange = CalculateEffectiveRange(wave);

            if (wave.CurrentDistance > detectionRange)
            {
                return 0;
            }

            double timeToImpact = wave.CurrentDistance / wave.AverageVelocity;
            return Math.Max(0, timeToImpact);
        }

        public double CalculateEffectiveRange(EnemyWave wave)
        {
            // Start with tier's maximum detection range
            double baseRange = GetTierBaseRange(wave);

            // RCS modifier: larger objects easier to detect
            double rcsModifier = Math.Log10(wave.AverageRadarCrossSection) / 10.0;
            baseRange *= (1.0 + rcsModifier);

            // Stealth coating reduces detection range significantly
            if (wave.HasStealthCoating)
            {
                baseRange *= 0.3;
            }

            // Ground-based radar penalty only if no space-based supplement
            if (RadarType == RadarType.GroundBased && !HasSpaceBasedRadar)
            {
                baseRange *= 0.85;  // Slight penalty for ground-based limitations
            }

            // Space-based radar extends range
            if (HasSpaceBasedRadar)
            {
                baseRange *= 1.5;
            }

            return baseRange;
        }

        public DetectionStatus GetDetectionStatus(EnemyWave wave)
        {
            double warningTime = CalculateWarningTime(wave);
            double effectiveRange = CalculateEffectiveRange(wave);

            if (wave.CurrentDistance > effectiveRange)
            {
                return new DetectionStatus
                {
                    IsDetected = false,
                    WarningTime = 0,
                    Quality = DetectionQuality.None,
                    Message = "No contacts detected"
                };
            }

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

            // Adjust minimum safe time based on tier
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
                Message = $"Tracking {wave.TargetCount} contacts, {GameConstants.FormatTime(warningTime)} to engagement"
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
