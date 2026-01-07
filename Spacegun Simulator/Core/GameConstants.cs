using System.Text.Json;
using System.Diagnostics;
using Spacegun_Simulator.Economy;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Development.Weapons;

namespace Spacegun_Simulator.Core
{
    // Centralized constants and simple helpers for SI unit display and tunables.
    public static class GameConstants
    {
        // ====================================================================
        // NOTE: TierCount is the single source of truth for how many tiers exist.
        // Update this value if WaveTiers length changes and keep arrays in sync.
        // ====================================================================
        public const int TierCount = 5;

        // ====================================================================
        // Barrel wear tunables (canonical single source)
        // - DefaultBarrelWearPerShot is the canonical variable tests and systems should use.
        //
        // Migration note:
        // - Prefer GameConstants.DefaultBarrelWearPerShot from now on.
        // - GameConfig.json still supports the old key "BarrelIntegrityLossPerShot" and
        //   the loader maps it into DefaultBarrelWearPerShot to preserve old configs.
        // ====================================================================
        public static double DefaultBarrelWearPerShot
        {
            get => WeaponTuning.DefaultBarrelWearPerShot;
            set => WeaponTuning.DefaultBarrelWearPerShot = value;
        }

        // ============================================================================
        // WAVE TIER SYSTEM - Oort Cloud to Earth Defense
        // ============================================================================

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

        // ====================================================================
        // Weapons muzzle velocity tuning
        // - BaseMuzzleVelocityMs is the single source of truth (whole number).
        // - Tech augments via multipliers.
        // ====================================================================
        public static int BaseMuzzleVelocityMs
        {
            get => WeaponTuning.BaseMuzzleVelocityMs;
            set => WeaponTuning.BaseMuzzleVelocityMs = Math.Max(1, value);
        }

        /// <summary>
        /// Global multiplier applied to the player's base muzzle velocity.
        /// Default is 1.0.
        /// </summary>
        public static double MuzzleVelocityMultiplier { get; set; } = 1.0;

        public static double[] WeaponsTechVelocityMultipliers => WeaponTuning.WeaponsTechVelocityMultipliers;

        // Legacy/compatibility: derived per-tech base velocities.
        public static double[] WeaponsTechBaseVelocity
        {
            get
            {
                var mults = WeaponsTechVelocityMultipliers;
                var arr = new double[mults.Length];
                for (int i = 0; i < mults.Length; i++)
                    arr[i] = BaseMuzzleVelocityMs * mults[i] * Math.Max(0.0, MuzzleVelocityMultiplier);
                return arr;
            }
        }

        public static readonly WaveTier[] WaveTiers = new WaveTier[]
        {
            // TIER 0: Early game (Waves 1-5)
            new WaveTier
            {
                TierIndex = 0,
                StartWave = 1,
                EndWave = 5,
                DetectionRangeMin = 15_000.0 * AU_TO_METERS,
                DetectionRangeMax = 25_000.0 * AU_TO_METERS,
                VelocityMin = 50_000,           // 50 km/s
                VelocityMax = 90_000,           // 90 km/s
                MaxEffectiveGunRange = 1_500_000.0,
                TimeToImpactMin = (long)(150.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(400.0 * SECONDS_PER_YEAR)
            },

            // TIER 1: Mid-game (Waves 6-10)
            new WaveTier
            {
                TierIndex = 1,
                StartWave = 6,
                EndWave = 10,
                DetectionRangeMin = 60_000.0 * AU_TO_METERS,
                DetectionRangeMax = 100_000.0 * AU_TO_METERS,
                VelocityMin = 200_000,          // 200 km/s
                VelocityMax = 500_000,          // 500 km/s
                MaxEffectiveGunRange = 5_000_000.0,
                TimeToImpactMin = (long)(15.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(40.0 * SECONDS_PER_YEAR)
            },

            // TIER 2: Late-game (Waves 11-15)
            new WaveTier
            {
                TierIndex = 2,
                StartWave = 11,
                EndWave = 15,
                DetectionRangeMin = 50_000.0 * AU_TO_METERS,
                DetectionRangeMax = 150_000.0 * AU_TO_METERS,
                VelocityMin = 1_000_000,        // 1,000 km/s
                VelocityMax = 3_000_000,        // 3,000 km/s
                MaxEffectiveGunRange = 15_000_000.0,
                TimeToImpactMin = (long)(2.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(8.0 * SECONDS_PER_YEAR)
            },

            // TIER 3: Endgame (Waves 16-20)
            new WaveTier
            {
                TierIndex = 3,
                StartWave = 16,
                EndWave = 20,
                DetectionRangeMin = 100_000.0 * AU_TO_METERS,
                DetectionRangeMax = 325_000.0 * AU_TO_METERS,
                VelocityMin = 3_000_000,        // 3,000 km/s
                VelocityMax = 6_500_000,        // 6,500 km/s
                MaxEffectiveGunRange = 30_000_000.0,
                TimeToImpactMin = (long)(1.5 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(5.0 * SECONDS_PER_YEAR)
            },

            // TIER 4: Final tier (Waves 21-25)
            new WaveTier
            {
                TierIndex = 4,
                StartWave = 21,
                EndWave = 25,
                DetectionRangeMin = 150_000.0 * AU_TO_METERS,
                DetectionRangeMax = 500_000.0 * AU_TO_METERS,
                VelocityMin = 5_000_000,        // 5,000 km/s
                VelocityMax = 10_000_000,       // 10,000 km/s
                MaxEffectiveGunRange = 45_000_000.0,
                TimeToImpactMin = (long)(1.0 * SECONDS_PER_YEAR),
                TimeToImpactMax = (long)(2.0 * SECONDS_PER_YEAR)
            }
        };

        // Added GetTierForWave implementation so dependent code compiles.
        public static WaveTier GetTierForWave(int waveNumber)
        {
            foreach (var tier in WaveTiers)
            {
                if (waveNumber >= tier.StartWave && waveNumber <= tier.EndWave)
                    return tier;
            }

            // Fallback: return last tier if wave number out of range
            return WaveTiers[^1];
        }

        // Validate tier arrays on static initialization to catch mismatches early.
        static GameConstants()
        {
            // Basic length assertions
            Debug.Assert(WaveTiers.Length == TierCount, $"WaveTiers length ({WaveTiers.Length}) must equal TierCount ({TierCount}).");

            Debug.Assert(TierEnemyMinVelocity.Length == TierCount, $"TierEnemyMinVelocity length ({TierEnemyMinVelocity.Length}) must equal TierCount ({TierCount}).");
            Debug.Assert(TierEnemyMaxVelocity.Length == TierCount, $"TierEnemyMaxVelocity length ({TierEnemyMaxVelocity.Length}) must equal TierCount ({TierCount}).");
        }

        // ============================================================================
        // RESOURCE & DEVELOPMENT CONSTANTS
        // ============================================================================

        public static int TotalWaves = 25;
        public static double BudgetRewardBase
        {
            get => ResourceEconomyTuning.BudgetRewardBase;
            set => ResourceEconomyTuning.BudgetRewardBase = value;
        }

        public static double BudgetRewardPerWave
        {
            get => ResourceEconomyTuning.BudgetRewardPerWave;
            set => ResourceEconomyTuning.BudgetRewardPerWave = value;
        }

        public static double SteelRewardBase
        {
            get => ResourceEconomyTuning.SteelRewardBase;
            set => ResourceEconomyTuning.SteelRewardBase = value;
        }

        public static double SteelRewardPerWave
        {
            get => ResourceEconomyTuning.SteelRewardPerWave;
            set => ResourceEconomyTuning.SteelRewardPerWave = value;
        }

        public static double ExoticRewardBase
        {
            get => ResourceEconomyTuning.ExoticRewardBase;
            set => ResourceEconomyTuning.ExoticRewardBase = value;
        }

        public static double ExoticRewardPerWave
        {
            get => ResourceEconomyTuning.ExoticRewardPerWave;
            set => ResourceEconomyTuning.ExoticRewardPerWave = value;
        }

        public static double MinBudgetToContinue
        {
            get => ResourceEconomyTuning.MinBudgetToContinue;
            set => ResourceEconomyTuning.MinBudgetToContinue = value;
        }

        // Resource production rates (units per year) - WHOLE NUMBERS
        public static double SteelProductionPerYear
        {
            get => ResourceEconomyTuning.SteelProductionPerYear;
            set => ResourceEconomyTuning.SteelProductionPerYear = value;
        }

        public static double ExoticProductionPerYear
        {
            get => ResourceEconomyTuning.ExoticProductionPerYear;
            set => ResourceEconomyTuning.ExoticProductionPerYear = value;
        }

        public static double BudgetProductionPerYear
        {
            get => ResourceEconomyTuning.BudgetProductionPerYear;
            set => ResourceEconomyTuning.BudgetProductionPerYear = value;
        }

        // Extended resource types for mid/late-game progression
        public static double RareEarthElementsProductionPerYear
        {
            get => ResourceEconomyTuning.RareEarthElementsProductionPerYear;
            set => ResourceEconomyTuning.RareEarthElementsProductionPerYear = value;
        }

        public static double SpecializedAlloysProductionPerYear
        {
            get => ResourceEconomyTuning.SpecializedAlloysProductionPerYear;
            set => ResourceEconomyTuning.SpecializedAlloysProductionPerYear = value;
        }

        public static double PowerCellsProductionPerYear
        {
            get => ResourceEconomyTuning.PowerCellsProductionPerYear;
            set => ResourceEconomyTuning.PowerCellsProductionPerYear = value;
        }

        // ============================================================================
        // ENEMY GENERATION CONSTANTS
        // ============================================================================

        public static int TargetCountBase
        {
            get => EnemyTuning.TargetCountBase;
            set => DevelopmentTuning.Apply(new DevelopmentTuningConfig
            {
                EnemyTuning = new EnemyTuningConfig { TargetCountBase = value }
            });
        }

        public static int TargetCountTierBonus
        {
            get => EnemyTuning.TargetCountTierBonus;
            set => DevelopmentTuning.Apply(new DevelopmentTuningConfig
            {
                EnemyTuning = new EnemyTuningConfig { TargetCountTierBonus = value }
            });
        }

        public static int TargetCountRandomMaxExclusive
        {
            get => EnemyTuning.TargetCountRandomMaxExclusive;
            set => DevelopmentTuning.Apply(new DevelopmentTuningConfig
            {
                EnemyTuning = new EnemyTuningConfig { TargetCountRandomMaxExclusive = value }
            });
        }

        // Type pools
        public static string[] EarlyTypes => EnemyTuning.EarlyTypes;
        public static string[] MidTypes => EnemyTuning.MidTypes;
        public static string[] LateTypes => EnemyTuning.LateTypes;

        // Cross-section ranges per type (square meters)
        public static Dictionary<string, (double Min, double Max)> CrossSectionRanges => EnemyTuning.CrossSectionRanges;

        // Stealth chance when tier >= 2
        public static double StealthChanceForLateTiers
        {
            get => EnemyTuning.StealthChanceForLateTiers;
            set => DevelopmentTuning.Apply(new DevelopmentTuningConfig
            {
                EnemyTuning = new EnemyTuningConfig { StealthChanceForLateTiers = value }
            });
        }

        // ============================================================================
        // DISPLAY & FORMATTING CONSTANTS
        // ============================================================================

        public const double MetersPerKilometer = 1000.0;
        public const double SecondsPerMinute = 60.0;
        public const double SecondsPerHour = 3600.0;
        public const double SecondsPerDay = 86400.0;
        // NOTE: SecondsPerYear removed (use SECONDS_PER_YEAR)

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
            if (seconds >= SECONDS_PER_YEAR)
            {
                long years = (long)Math.Round(seconds / SECONDS_PER_YEAR);
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
            // Keep behaviour unchanged aside from using canonical constant where needed.
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

        // ===== TIER-BASED VELOCITY CONSTRAINTS (NEW) =====
        /// <summary>
        /// Minimum enemy velocity for each tier (m/s).
        /// Used for diagnostics/test-scenario sampling; not required to match WaveTiers.
        /// </summary>
        public static double[] TierEnemyMinVelocity => TierVelocityTuning.TierEnemyMinVelocity;

        /// <summary>
        /// Maximum enemy velocity for each tier (m/s).
        /// Used for diagnostics/test-scenario sampling; not required to match WaveTiers.
        /// </summary>
        public static double[] TierEnemyMaxVelocity => TierVelocityTuning.TierEnemyMaxVelocity;

        /// <summary>
        /// Get enemy velocity constraints for a specific tier.
        /// Uses TierCount for bounds checking.
        /// </summary>
        public static (double EnemyMin, double EnemyMax) GetTierEnemyVelocityConstraints(int tierIndex)
            => TierVelocityTuning.GetTierEnemyVelocityConstraints(tierIndex, TierCount);

        /// <summary>
        /// Get player test velocity for a specific tier.
        /// Calculated to achieve 10-20 second intercept time in engagement scenarios.
        /// This simulates an appropriately upgraded player weapon per tier.
        /// </summary>
        public static double GetTestPlayerVelocityForTier(int tierIndex)
            => TestScenarioTuning.GetTestPlayerVelocityForTier(tierIndex);

        /// <summary>
        /// Get test gun range for a specific tier.
        /// </summary>
        public static double GetTestGunRangeForTier(int tierIndex)
            => TestScenarioTuning.GetTestGunRangeForTier(tierIndex);

        /// <summary>
        /// Get test engagement distance for a specific tier.
        /// </summary>
        public static double GetTestEngagementDistanceForTier(int tierIndex)
            => TestScenarioTuning.GetTestEngagementDistanceForTier(tierIndex);

        /// <summary>
        /// Calculate expected intercept time for a test scenario.
        /// Formula: Distance / (Enemy Velocity + Player Velocity)
        /// </summary>
        public static double CalculateExpectedInterceptTime(
            double engagementDistance,
            double enemyVelocity,
            double playerVelocity)
            => TestScenarioTuning.CalculateExpectedInterceptTime(engagementDistance, enemyVelocity, playerVelocity);
    }

    // Simple config loader
    public static class GameConfigLoader
    {
        private const string ConfigPath = "Config/GameConfig.json";

        private static string? TryResolveConfigPath(string relativePath)
        {
            // Support running from:
            // - project directory ("Config/..." exists)
            // - repo root ("Spacegun Simulator/Config/..." exists)
            // - bin output (if configs are copied alongside output)
            //
            // Keep this conservative: a small fixed set of probes.
            string rel = relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar);

            string[] candidates = new[]
            {
                System.IO.Path.Combine(Environment.CurrentDirectory, rel),
                System.IO.Path.Combine(Environment.CurrentDirectory, "Spacegun Simulator", rel),
                System.IO.Path.Combine(AppContext.BaseDirectory, rel),
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (System.IO.File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // Ignore and keep probing.
                }
            }

            return null;
        }

        public static void LoadIfExists()
        {
            try
            {
                string? resolved = TryResolveConfigPath(ConfigPath);
                if (resolved is null) return;

                var json = System.IO.File.ReadAllText(resolved);
                var cfg = JsonSerializer.Deserialize<GameConfig>(json);
                if (cfg is null) return;

                if (cfg.TotalWaves.HasValue) GameConstants.TotalWaves = cfg.TotalWaves.Value;
                if (cfg.BarrelIntegrityLossPerShot.HasValue) GameConstants.DefaultBarrelWearPerShot = cfg.BarrelIntegrityLossPerShot.Value;
                if (cfg.BaseMuzzleVelocityMs.HasValue) GameConstants.BaseMuzzleVelocityMs = cfg.BaseMuzzleVelocityMs.Value;
                if (cfg.MuzzleVelocityMultiplier.HasValue) GameConstants.MuzzleVelocityMultiplier = Math.Clamp(cfg.MuzzleVelocityMultiplier.Value, 0.25, 3.0);
                if (cfg.BudgetRewardBase.HasValue) GameConstants.BudgetRewardBase = cfg.BudgetRewardBase.Value;
                if (cfg.BudgetRewardPerWave.HasValue) GameConstants.BudgetRewardPerWave = cfg.BudgetRewardPerWave.Value;
                if (cfg.SteelRewardBase.HasValue) GameConstants.SteelRewardBase = cfg.SteelRewardBase.Value;
                if (cfg.SteelRewardPerWave.HasValue) GameConstants.SteelRewardPerWave = cfg.SteelRewardPerWave.Value;
                if (cfg.ExoticRewardBase.HasValue) GameConstants.ExoticRewardBase = cfg.ExoticRewardBase.Value;
                if (cfg.ExoticRewardPerWave.HasValue) GameConstants.ExoticRewardPerWave = cfg.ExoticRewardPerWave.Value;
                if (cfg.MinBudgetToContinue.HasValue) GameConstants.MinBudgetToContinue = cfg.MinBudgetToContinue.Value;
                if (cfg.StealthChanceForLateTiers.HasValue) GameConstants.StealthChanceForLateTiers = cfg.StealthChanceForLateTiers.Value;

                // Optional: mode tuning
                GameModeTuning.ApplyFromConfig(cfg.Modes);
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
            public int? BaseMuzzleVelocityMs { get; set; }
            public double? MuzzleVelocityMultiplier { get; set; }
            public double? BudgetRewardBase { get; set; }
            public double? BudgetRewardPerWave { get; set; }
            public double? SteelRewardBase { get; set; }
            public double? SteelRewardPerWave { get; set; }
            public double? ExoticRewardBase { get; set; }
            public double? ExoticRewardPerWave { get; set; }
            public double? MinBudgetToContinue { get; set; }
            public double? StealthChanceForLateTiers { get; set; }

            public GameModeTuning? Modes { get; set; }
        }
    }
}