using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace Spacegun_Simulator
{
    // Centralized constants and simple helpers for SI unit display and tunables.
    public static class GameConstants
    {
        // ============================================================================
        // WAVE TIER SYSTEM - Oort Cloud Scale (100,000 AU board)
        // ============================================================================

        /// <summary>
        /// 25-wave campaign spanning from Oort Cloud (100,000 AU) to Earth
        /// Tier 0 (Waves 1-6):   Detection at ~20,000 AU, 150-year warning
        /// Tier 1 (Waves 7-12):  Detection at ~40,000 AU, 40-100 year warning
        /// Tier 2 (Waves 13-19): Detection at ~70,000 AU, 8-20 year warning
        /// Tier 3 (Waves 20-25): Detection at ~100,000 AU, 1-3 year warning
        /// </summary>
        public class WaveTier
        {
            public int TierIndex { get; set; }
            public int StartWave { get; set; }
            public int EndWave { get; set; }

            // Distance ranges (meters) - where enemies are detected
            public double DetectionRangeMin { get; set; }
            public double DetectionRangeMax { get; set; }

            // Velocity ranges (m/s)
            public double VelocityMin { get; set; }
            public double VelocityMax { get; set; }

            // Maximum effective gun range for this tier (meters)
            public double MaxEffectiveGunRange { get; set; }

            // Time to impact estimates (seconds) - WHOLE NUMBERS ONLY
            public long TimeToImpactMin { get; set; }
            public long TimeToImpactMax { get; set; }
        }

        // Tier definitions using Oort Cloud scale (1-100,000 AU)
        public const double AU_TO_METERS = 1.496e11;  // 1 AU in meters
        public const double SPEED_OF_LIGHT = 299_792_458.0;  // m/s
        public const double SECONDS_PER_YEAR = 31557600.0;  // SI year

        public static readonly WaveTier[] WaveTiers = new WaveTier[]
        {
            // TIER 0: Oort Cloud perimeter detection
            // Distance: 15,000-25,000 AU (detected far away)
            // Gun range: 1 AU (must wait for enemy to approach)
            // 150+ year warning allows time to upgrade
            new WaveTier
            {
                TierIndex = 0,
                StartWave = 1,
                EndWave = 6,
                DetectionRangeMin = 15_000.0 * AU_TO_METERS,      // 15,000 AU (detection)
                DetectionRangeMax = 25_000.0 * AU_TO_METERS,      // 25,000 AU (detection)
                VelocityMin = 500_000,                           // 500 km/s
                VelocityMax = 800_000,                           // 800 km/s
                MaxEffectiveGunRange = 1.0 * AU_TO_METERS,       // 1 AU (must get close)
                TimeToImpactMin = (long)(150.0 * SECONDS_PER_YEAR),      // 150 years
                TimeToImpactMax = (long)(400.0 * SECONDS_PER_YEAR)       // 400 years
            },
            
            // TIER 1: Deep space detection
            // Distance: 30,000-50,000 AU (detected far away)
            // Gun range: 2 AU (upgraded range)
            // 40-100 year warning
            new WaveTier
            {
                TierIndex = 1,
                StartWave = 7,
                EndWave = 12,
                DetectionRangeMin = 30_000.0 * AU_TO_METERS,     // 30,000 AU (detection)
                DetectionRangeMax = 50_000.0 * AU_TO_METERS,     // 50,000 AU (detection)
                VelocityMin = 15_000_000,                        // 15,000 km/s (5% c)
                VelocityMax = 40_000_000,                        // 40,000 km/s (13% c)
                MaxEffectiveGunRange = 2.0 * AU_TO_METERS,       // 2 AU (upgraded)
                TimeToImpactMin = (long)(40.0 * SECONDS_PER_YEAR),       // 40 years
                TimeToImpactMax = (long)(100.0 * SECONDS_PER_YEAR)       // 100 years
            },
            
            // TIER 2: Inner Oort Cloud detection
            // Distance: 60,000-90,000 AU (detected far away)
            // Gun range: 3 AU (further upgraded)
            // 8-20 year warning
            new WaveTier
            {
                TierIndex = 2,
                StartWave = 13,
                EndWave = 19,
                DetectionRangeMin = 60_000.0 * AU_TO_METERS,     // 60,000 AU (detection)
                DetectionRangeMax = 90_000.0 * AU_TO_METERS,     // 90,000 AU (detection)
                VelocityMin = 150_000_000,                       // 150,000 km/s (50% c)
                VelocityMax = 280_000_000,                       // 280,000 km/s (93% c)
                MaxEffectiveGunRange = 3.0 * AU_TO_METERS,       // 3 AU (further upgraded)
                TimeToImpactMin = (long)(8.0 * SECONDS_PER_YEAR),        // 8 years
                TimeToImpactMax = (long)(20.0 * SECONDS_PER_YEAR)        // 20 years
            },
            
            // TIER 3: Near-Earth space, energy weapons
            // Distance: 95,000-100,000 AU (detected far away)
            // Gun range: 5 AU (maximum range)
            // 1-3 year warning (barely any time!)
            new WaveTier
            {
                TierIndex = 3,
                StartWave = 20,
                EndWave = 25,
                DetectionRangeMin = 95_000.0 * AU_TO_METERS,     // 95,000 AU (detection)
                DetectionRangeMax = 100_000.0 * AU_TO_METERS,    // 100,000 AU (detection)
                VelocityMin = 280_000_000,                       // 280,000 km/s (93% c)
                VelocityMax = 299_792_458,                       // Speed of light (100% c)
                MaxEffectiveGunRange = 5.0 * AU_TO_METERS,       // 5 AU (maximum)
                TimeToImpactMin = (long)(1.0 * SECONDS_PER_YEAR),        // 1 year (almost no time!)
                TimeToImpactMax = (long)(3.0 * SECONDS_PER_YEAR)         // 3 years
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

        // ============================================================================
        // ENEMY GENERATION CONSTANTS
        // ============================================================================

        // Targets per wave
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

        // Armor / HP tunables
        public static int HpBase = 200;
        public static int HpPerTier = 200;
        public static int HpRandomVariance = 300;

        public static int ArmorThicknessBase = 50;
        public static int ArmorThicknessPerTier = 50;
        public static int ArmorThicknessRandomVariance = 100;

        public static double ArmorQualityBase = 1.0;
        public static double ArmorQualityPerTier = 0.3;
        public static double ArmorQualityRandomVariance = 0.5;

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
            }
            catch
            {
                // ignore config errors
            }
        }

        private class GameConfig
        {
            public int? TotalWaves { get; set; }
            public double? BarrelIntegrityLossPerShot { get; set; }
        }
    }
}