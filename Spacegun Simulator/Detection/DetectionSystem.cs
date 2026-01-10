using Spacegun_Simulator.Core;
using Spacegun_Simulator.Enemies;

namespace Spacegun_Simulator.Detection
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
        public readonly record struct NoisyIntelEstimate(
            int? ThreatCountEstimate,
            string StealthAssessment,
            double? ManeuverabilityEstimate01,
            double? DefenseEstimate01,
            double? OffenseEstimate01,
            double PercentNoise,
            double IntelResolution
        );

        /// <summary>
        /// Multiplier applied to the tier's base detection range.
        /// 1.0 = standard detection, > 1.0 = improved through research
        /// </summary>
        public double DetectionRangeMultiplier { get; set; }

        /// <summary>
        /// How much stealth coating is countered (0.0..1.0).
        /// 0 = stealth applies fully; 1 = stealth has no effect.
        /// </summary>
        public double StealthPenetration { get; set; }

        /// <summary>
        /// Controls how much detail is available in detection intel (0.0..1.0).
        /// Higher values reduce noise and reveal more properties.
        /// </summary>
        public double IntelResolution { get; set; }

        public int MaxSimultaneousTargets { get; set; }
        public RadarType RadarType { get; set; }
        public bool HasSpaceBasedRadar { get; set; }

        public DetectionSystem()
        {
            DetectionRangeMultiplier = 1.0;
            StealthPenetration = 0.0;
            IntelResolution = 0.0;
            MaxSimultaneousTargets = 5;
            RadarType = RadarType.GroundBased;
            HasSpaceBasedRadar = false;
        }

        /// <summary>
        /// Returns the effective range multiplier for stealth-coated waves, after applying StealthPenetration.
        /// Used for both detection range and (by design) gun effective range debuff.
        /// </summary>
        public double GetStealthRangeMultiplier(EnemyWave wave)
        {
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            if (!wave.HasStealthCoating) return 1.0;

            // Base stealth effect is 90% reduction. Penetration linearly restores that.
            double p = Math.Clamp(StealthPenetration, 0.0, 1.0);
            return (0.1 * (1.0 - p)) + (1.0 * p);
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

            // Apply research multiplier first so other modifiers scale consistently.
            detectionRangeAU *= Math.Max(0.0, DetectionRangeMultiplier);

            // Stealth coating reduces detection range significantly
            if (wave.HasStealthCoating)
            {
                detectionRangeAU *= GetStealthRangeMultiplier(wave);
            }

            // Space-based radar extends range
            if (HasSpaceBasedRadar)
            {
                detectionRangeAU *= 1.2;  // 20% bonus
            }

            return detectionRangeAU;
        }

        public string GenerateNoisyIntelSummary(EnemyWave wave, Random rng)
        {
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var estimate = GenerateNoisyIntelEstimate(wave, rng);
            if (estimate.ThreatCountEstimate == null)
                return "Intel: insufficient resolution for reliable estimates.";

            string threatCountText = estimate.IntelResolution >= 0.9 && estimate.PercentNoise <= 0.12
                ? $"~{estimate.ThreatCountEstimate.Value} (high confidence)"
                : $"~{estimate.ThreatCountEstimate.Value}";

            var parts = new List<string>
            {
                $"Intel: threat count {threatCountText}.",
                $"Stealth coating: {estimate.StealthAssessment}."
            };

            if (estimate.ManeuverabilityEstimate01.HasValue)
                parts.Add($"Maneuverability: {estimate.ManeuverabilityEstimate01.Value:P0} (est.)");
            if (estimate.DefenseEstimate01.HasValue)
                parts.Add($"Defense: {estimate.DefenseEstimate01.Value:P0} (est.)");
            if (estimate.OffenseEstimate01.HasValue)
                parts.Add($"Offense: {estimate.OffenseEstimate01.Value:P0} (est.)");

            return string.Join(" ", parts);
        }

        public NoisyIntelEstimate GenerateNoisyIntelEstimate(EnemyWave wave, Random rng)
        {
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // Always keep this as a noisy estimate (never exact), per design.
            double r = Math.Clamp(IntelResolution, 0.0, 1.0);

            // If resolution is too low, don't pretend we know anything.
            if (r < 0.15)
            {
                return new NoisyIntelEstimate(
                    ThreatCountEstimate: null,
                    StealthAssessment: "unknown",
                    ManeuverabilityEstimate01: null,
                    DefenseEstimate01: null,
                    OffenseEstimate01: null,
                    PercentNoise: 1.0,
                    IntelResolution: r);
            }

            // Noise shrinks as resolution increases.
            // Example: r=0.2 => +/-60% ; r=0.8 => +/-15%
            double percentNoise = 0.75 - (0.6 * r);
            percentNoise = Math.Clamp(percentNoise, 0.1, 0.75);

            int threatCountEstimate = EstimateInt(wave.ThreatCount, percentNoise, rng, min: 1, max: 999);

            string stealthText;
            if (r < 0.35)
            {
                stealthText = "unknown";
            }
            else
            {
                // Treat stealth as detectable, but still imperfect.
                double detectionChance = wave.HasStealthCoating ? (0.55 + 0.4 * r) : (0.45 - 0.25 * r);
                detectionChance = Math.Clamp(detectionChance, 0.05, 0.95);
                bool flagged = rng.NextDouble() < detectionChance;
                stealthText = flagged ? "possible" : "unlikely";
            }

            double? manEst = null;
            double? defEst = null;
            double? offEst = null;
            if (wave.Targets != null && wave.Targets.Count > 0 && r >= 0.5)
            {
                var t = wave.Targets[0];
                manEst = Estimate01(t.Maneuverability, percentNoise, rng);
                defEst = Estimate01(t.Defense, percentNoise, rng);
                if (t.Offense > 0.0)
                    offEst = Estimate01(t.Offense, percentNoise, rng);
            }

            return new NoisyIntelEstimate(
                ThreatCountEstimate: threatCountEstimate,
                StealthAssessment: stealthText,
                ManeuverabilityEstimate01: manEst,
                DefenseEstimate01: defEst,
                OffenseEstimate01: offEst,
                PercentNoise: percentNoise,
                IntelResolution: r);
        }

        private static int EstimateInt(int actual, double percentNoise, Random rng, int min, int max)
        {
            double noise = (rng.NextDouble() * 2.0 - 1.0) * percentNoise;
            int estimate = (int)Math.Round(actual * (1.0 + noise));
            return Math.Clamp(estimate, min, max);
        }

        private static double Estimate01(double actual, double percentNoise, Random rng)
        {
            if (actual <= 0) return 0;
            double noise = (rng.NextDouble() * 2.0 - 1.0) * percentNoise;
            return Math.Clamp(actual * (1.0 + noise), 0.0, 1.0);
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

    public enum RadarType
    {
        GroundBased,
        MountainTop,
        SpaceBased,
        LunarBased,
        DeepSpace
    }

    public enum DetectionQuality
    {
        None,
        Emergency,
        Degraded,
        Adequate,
        Optimal
    }

    public class DetectionStatus
    {
        public bool IsDetected { get; set; }
        public double WarningTime { get; set; }
        public DetectionQuality Quality { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
