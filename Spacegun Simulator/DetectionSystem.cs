namespace Spacegun_Simulator
{
    // ============================================================================
    // DETECTION SYSTEM
    // ============================================================================

    public class DetectionSystem
    {
        public double MaxDetectionRange { get; set; }
        public double TrackingAccuracy { get; set; }
        public double RefreshRate { get; set; }
        public int MaxSimultaneousTargets { get; set; }
        public RadarType RadarType { get; set; }
        public bool HasSpaceBasedRadar { get; set; }
        public bool HasQuantumEntanglementComm { get; set; }
        public double SignalProcessingPower { get; set; }

        public DetectionSystem()
        {
            MaxDetectionRange = 1_000_000;
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
            double baseRange = MaxDetectionRange;

            double rcsModifier = Math.Log10(wave.AverageRadarCrossSection) / 10.0;
            baseRange *= (1.0 + rcsModifier);

            if (wave.HasStealthCoating)
            {
                baseRange *= 0.3;
            }

            if (RadarType == RadarType.GroundBased)
            {
                baseRange *= 0.7;
            }

            if (HasSpaceBasedRadar)
            {
                baseRange *= 2.0;
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

            double minimumSafeTime = 300;
            if (warningTime < minimumSafeTime)
            {
                return new DetectionStatus
                {
                    IsDetected = true,
                    WarningTime = warningTime,
                    Quality = DetectionQuality.Emergency,
                    Message = $"EMERGENCY: Insufficient warning time ({warningTime:F0}s)"
                };
            }

            return new DetectionStatus
            {
                IsDetected = true,
                WarningTime = warningTime,
                Quality = DetectionQuality.Optimal,
                Message = $"Tracking {wave.TargetCount} contacts, {warningTime:F0}s to engagement"
            };
        }
    }

    public class DetectionStatus
    {
        public bool IsDetected { get; set; }
        public double WarningTime { get; set; }
        public DetectionQuality Quality { get; set; }
        public string Message { get; set; } = string.Empty;  // Add this
    }
}
