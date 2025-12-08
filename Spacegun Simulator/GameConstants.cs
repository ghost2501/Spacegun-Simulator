using System.Text.Json;

namespace Spacegun_Simulator
{
    // Centralized constants and simple helpers for SI unit display and tunables.
    public static class GameConstants
    {
        // ============================================================================
        // WAVE TIER SYSTEM - Oort Cloud to Earth Defense
        // ============================================================================
        // NARRATIVE: Enemies detected in the Oort Cloud (15,000-100,000 AU away).
        // Projectile has been traveling from deep space for many years.
        // At engagement T+0s, enemy is at 1000-2000km altitude approaching Earth.
        // Gun calculates when to fire so the incoming projectile arrives at intercept.
        // Gun range represents how far from Earth the projectile can still effectively reach target.

        public class WaveTier
        {
            public int TierIndex { get; set; }
            public int StartWave { get; set; }
            public int EndWave { get; set; }

            // Distance ranges (meters) - where enemies are detected in Oort Cloud
            public double DetectionRangeMin { get; set; }
            public double DetectionRangeMax { get; set; }

            // Velocity ranges (m/s) - enemy approach velocity (UNCHANGED)
            public double VelocityMin { get; set; }
            public double VelocityMax { get; set; }

            // Maximum effective gun range (meters) - engagement envelope from 1000-2000km
            public double MaxEffectiveGunRange { get; set; }

            // Time to impact estimates (seconds) - from detection to Earth impact
            public long TimeToImpactMin { get; set; }
            public long TimeToImpactMax { get; set; }
        }

        // Constants for scaling and formatting
        public const double AU_TO_METERS = 1.496e11;  // 1 AU in meters
        public const double SPEED_OF_LIGHT = 299_792_458.0;  // m/s
        public const double SECONDS_PER_YEAR = 31557600.0;  // SI year
        public const double TACTICAL_MAX_RANGE = 2_000_000.0;  // 2000 km - maximum engagement distance

        public static readonly WaveTier[] WaveTiers = new WaveTier[]
        {
            // TIER 0: Early game - ample time, generous engagement window
            // Detection: 15,000-25,000 AU (Oort Cloud outer zone)
            // Velocity: 50-80 km/s
            // Gun range: 1500 km - allows 5-30 second intercepts with visible arc
            // Time window: 150-400 years (enemy travel time from detection to Earth)
            new WaveTier
            {
                TierIndex = 0,
                StartWave = 1,
                EndWave = 6,
                DetectionRangeMin = 15_000.0 * AU_TO_METERS,
                DetectionRangeMax = 25_000.0 * AU_TO_METERS,
                VelocityMin = 50_000,
                VelocityMax = 80_000,
                MaxEffectiveGunRange = 1_500_000.0,              // 1500 km
                TimeToImpactMin = (long)(150.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(400.0 * SECONDS_PER_YEAR)
            },
            
            // TIER 1: Mid-early game - moderate warning, standard engagement window
            // Detection: 30,000-50,000 AU (Oort Cloud mid zone)
            // Velocity: 1,500-4,000 km/s
            // Gun range: 1500 km - consistent with tier 0, but higher velocities tighten challenge
            // Time window: 40-100 years
            new WaveTier
            {
                TierIndex = 1,
                StartWave = 7,
                EndWave = 12,
                DetectionRangeMin = 30_000.0 * AU_TO_METERS,
                DetectionRangeMax = 50_000.0 * AU_TO_METERS,
                VelocityMin = 1_500_000,
                VelocityMax = 4_000_000,
                MaxEffectiveGunRange = 1_500_000.0,              // 1500 km
                TimeToImpactMin = (long)(40.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(100.0 * SECONDS_PER_YEAR)
            },
            
            // TIER 2: Mid-late game - tight timeline, compressed engagement window
            // Detection: 60,000-90,000 AU (Oort Cloud inner zone)
            // Velocity: 15,000-28,000 km/s
            // Gun range: 1200 km - reduced range increases difficulty
            // Time window: 8-20 years
            new WaveTier
            {
                TierIndex = 2,
                StartWave = 13,
                EndWave = 19,
                DetectionRangeMin = 60_000.0 * AU_TO_METERS,
                DetectionRangeMax = 90_000.0 * AU_TO_METERS,
                VelocityMin = 15_000_000,
                VelocityMax = 28_000_000,
                MaxEffectiveGunRange = 1_200_000.0,             // 1200 km
                TimeToImpactMin = (long)(8.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(20.0 * SECONDS_PER_YEAR)
            },
            
            // TIER 3: Endgame - minimal warning, minimal engagement window
            // Detection: 95,000-100,000 AU (Oort Cloud edge)
            // Velocity: 28,000-29,979 km/s (relativistic speeds)
            // Gun range: 1000 km - full tactical reach, maximum difficulty
            // Time window: 1-3 years (extreme pressure)
            new WaveTier
            {
                TierIndex = 3,
                StartWave = 20,
                EndWave = 25,
                DetectionRangeMin = 95_000.0 * AU_TO_METERS,
                DetectionRangeMax = 100_000.0 * AU_TO_METERS,
                VelocityMin = 28_000_000,
                VelocityMax = 29_979_245,
                MaxEffectiveGunRange = 1_000_000.0,             // 1000 km
                TimeToImpactMin = (long)(1.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(3.0 * SECONDS_PER_YEAR)
            }
        };

        public static WaveTier GetTierForWave(int waveNumber)
        {
            foreach (var tier in WaveTiers)
            {
                if (waveNumber >= tier.StartWave && waveNumber <= tier.EndWave)
                    return tier;
            }
            return WaveTiers[^1];
        }

        // ============================================================================
        // RESOURCE & DEVELOPMENT CONSTANTS
        // ============================================================================

        public static int TotalWaves = 25;
        public static double BarrelIntegrityLossPerShot = 0.01; // fraction
        public static double BudgetRewardBase = 100.0;
        public static double BudgetRewardPerWave = 10.0;
        public static double SteelRewardBase = 50.0;
        public static double SteelRewardPerWave = 5.0;
        public static double ExoticRewardBase = 5.0;
        public static double ExoticRewardPerWave = 2.0;
        public static double BudgetLossPerSurvivor = 50.0;
        public static double MinBudgetToContinue = 100.0;

        // Resource production rates (units per year) - WHOLE NUMBERS
        public static double SteelProductionPerYear = 100.0;
        public static double ExoticProductionPerYear = 10.0;
        public static double BudgetProductionPerYear = 50.0;

        // Extended resource types for mid/late-game progression
        public static double RareEarthElementsProductionPerYear = 5.0;
        public static double SpecializedAlloysProductionPerYear = 15.0;
        public static double PowerCellsProductionPerYear = 8.0;

        // ============================================================================
        // ENEMY GENERATION CONSTANTS
        // ============================================================================

        public static int TargetCountBase = 2;
        public static int TargetCountTierBonus = 1;
        public static int TargetCountRandomMaxExclusive = 3;

        // Type pools
        public static readonly string[] EarlyTypes = { "Scout", "Fighter", "Light Cruiser" };
        public static readonly string[] MidTypes = { "Cruiser", "Destroyer", "Heavy Fighter" };
        public static readonly string[] LateTypes = { "Battlecruiser", "Dreadnought", "Carrier" };

        // Cross-section ranges per type (square meters)
        public static readonly Dictionary<string, (double Min, double Max)> CrossSectionRanges = new()
        {
            ["Scout"] = (10.0, 30.0),
            ["Fighter"] = (20.0, 50.0),
            ["Light Cruiser"] = (40.0, 80.0),
            ["Cruiser"] = (80.0, 140.0),
            ["Destroyer"] = (100.0, 180.0),
            ["Heavy Fighter"] = (50.0, 100.0),
            ["Battlecruiser"] = (150.0, 250.0),
            ["Dreadnought"] = (250.0, 400.0),
            ["Carrier"] = (300.0, 500.0)
        };

        // Evasiveness ranges by type (0..1)
        public static readonly Dictionary<string, (double Min, double Max)> EvasivenessRanges = new()
        {
            ["Scout"] = (0.6, 0.9),
            ["Fighter"] = (0.5, 0.8),
            ["Light Cruiser"] = (0.2, 0.5),
            ["Cruiser"] = (0.2, 0.5),
            ["Destroyer"] = (0.2, 0.5),
            ["Heavy Fighter"] = (0.3, 0.6),
            ["Battlecruiser"] = (0.1, 0.4),
            ["Dreadnought"] = (0.05, 0.3),
            ["Carrier"] = (0.05, 0.3)
        };

        // Stealth chance when tier >= 2
        public static double StealthChanceForLateTiers = 0.3;

        // ============================================================================
        // DISPLAY & FORMATTING CONSTANTS
        // ============================================================================

        public const double MetersPerKilometer = 1000.0;
        public const double SecondsPerMinute = 60.0;
        public const double SecondsPerHour = 3600.0;
        public const double SecondsPerDay = 86400.0;
        public const double SecondsPerYear = 31557600.0;

        /// <summary>
        /// Format distance to appropriate AU/Gm/km units as whole numbers.
        /// </summary>
        public static string FormatDistance(double meters)
        {
            double au = meters / AU_TO_METERS;
            // Check AU first - this handles both large AU values and small ones
            if (au >= 1.0)
                return $"{Math.Round(au):F0} AU";
            if (au >= 0.01)  // Between 0.01 AU and 1 AU
                return $"{Math.Round(au, 2):F2} AU";

            // Only use other units for very small distances
            if (meters >= 1e9)
                return $"{Math.Round(meters / 1e9, 2):F2} Gm";
            if (meters >= 1e6)
                return $"{Math.Round(meters / 1e6, 1):F1} Mm";
            if (meters >= MetersPerKilometer)
                return $"{Math.Round(meters / MetersPerKilometer, 1):F1} km";

            return $"{Math.Round(meters, 1):F1} m";
        }

        /// <summary>
        /// Format velocity as percentage of light speed or km/s.
        /// </summary>
        public static string FormatVelocity(double mPerS)
        {
            double percentC = (mPerS / SPEED_OF_LIGHT) * 100.0;
            if (percentC >= 0.1)
                return $"{Math.Round(percentC, 2):F2}% c";

            if (mPerS >= 1_000_000)
                return $"{Math.Round(mPerS / 1_000_000, 2):F2} Mm/s";
            if (mPerS >= 1_000)
                return $"{Math.Round(mPerS / 1000.0, 1):F1} km/s";

            return $"{Math.Round(mPerS, 1):F1} m/s";
        }

        /// <summary>
        /// Format time to whole number years/months/days/hours/minutes/seconds.
        /// </summary>
        public static string FormatTime(double seconds)
        {
            if (seconds >= SecondsPerYear)
            {
                long years = (long)Math.Round(seconds / SecondsPerYear);
                return $"{years} year{(years != 1 ? "s" : "")}";
            }
            if (seconds >= SecondsPerDay * 30)
            {
                long months = (long)Math.Round(seconds / (SecondsPerDay * 30));
                return $"{months} month{(months != 1 ? "s" : "")}";
            }
            if (seconds >= SecondsPerDay)
            {
                long days = (long)Math.Round(seconds / SecondsPerDay);
                return $"{days} day{(days != 1 ? "s" : "")}";
            }
            if (seconds >= SecondsPerHour)
            {
                long hours = (long)Math.Round(seconds / SecondsPerHour);
                return $"{hours} hour{(hours != 1 ? "s" : "")}";
            }
            if (seconds >= SecondsPerMinute)
            {
                long minutes = (long)Math.Round(seconds / SecondsPerMinute);
                return $"{minutes} minute{(minutes != 1 ? "s" : "")}";
            }

            long secs = (long)Math.Round(seconds);
            return $"{secs} second{(secs != 1 ? "s" : "")}";
        }

        /// <summary>
        /// Round a time value (in seconds) to the nearest whole year.
        /// </summary>
        public static long RoundToWholeYear(double seconds)
        {
            return (long)Math.Round(seconds);
        }

        /// <summary>
        /// Round a distance value (in meters) to the nearest whole AU.
        /// </summary>
        public static double RoundToWholeAU(double meters)
        {
            double au = meters / AU_TO_METERS;
            return Math.Round(au) * AU_TO_METERS;
        }
    }

    // Simple config loader
    public static class GameConfigLoader
    {
        private const string ConfigPath = "Config/GameConfig.json";

        public static void LoadIfExists()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<GameConfig>(json);
                if (cfg is null) return;

                if (cfg.TotalWaves.HasValue) GameConstants.TotalWaves = cfg.TotalWaves.Value;
                if (cfg.BarrelIntegrityLossPerShot.HasValue) GameConstants.BarrelIntegrityLossPerShot = cfg.BarrelIntegrityLossPerShot.Value;
                if (cfg.BudgetRewardBase.HasValue) GameConstants.BudgetRewardBase = cfg.BudgetRewardBase.Value;
                if (cfg.BudgetRewardPerWave.HasValue) GameConstants.BudgetRewardPerWave = cfg.BudgetRewardPerWave.Value;
                if (cfg.SteelRewardBase.HasValue) GameConstants.SteelRewardBase = cfg.SteelRewardBase.Value;
                if (cfg.SteelRewardPerWave.HasValue) GameConstants.SteelRewardPerWave = cfg.SteelRewardPerWave.Value;
                if (cfg.ExoticRewardBase.HasValue) GameConstants.ExoticRewardBase = cfg.ExoticRewardBase.Value;
                if (cfg.ExoticRewardPerWave.HasValue) GameConstants.ExoticRewardPerWave = cfg.ExoticRewardPerWave.Value;
                if (cfg.BudgetLossPerSurvivor.HasValue) GameConstants.BudgetLossPerSurvivor = cfg.BudgetLossPerSurvivor.Value;
                if (cfg.MinBudgetToContinue.HasValue) GameConstants.MinBudgetToContinue = cfg.MinBudgetToContinue.Value;
                if (cfg.StealthChanceForLateTiers.HasValue) GameConstants.StealthChanceForLateTiers = cfg.StealthChanceForLateTiers.Value;
            }
            catch
            {
                // Ignore config errors, use defaults
            }
        }

        private class GameConfig
        {
            public int? TotalWaves { get; set; }
            public double? BarrelIntegrityLossPerShot { get; set; }
            public double? BudgetRewardBase { get; set; }
            public double? BudgetRewardPerWave { get; set; }
            public double? SteelRewardBase { get; set; }
            public double? SteelRewardPerWave { get; set; }
            public double? ExoticRewardBase { get; set; }
            public double? ExoticRewardPerWave { get; set; }
            public double? BudgetLossPerSurvivor { get; set; }
            public double? MinBudgetToContinue { get; set; }
            public double? StealthChanceForLateTiers { get; set; }
        }
    }
}