using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace Spacegun_Simulator
{
    // Centralized constants and simple helpers for SI unit display and tunables.
    public static class GameConstants
    {
        // Tunables (can be overridden by config)
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

        // --- NEW: Wave / target tunables ---
        // Distances: base and random variance per tier (meters)
        public static double[] InitialDistanceBaseByTier = { 50_000_000, 150_000_000, 300_000_000, 384_400_000 };
        public static double[] InitialDistanceVarianceByTier = { 50_000_000, 100_000_000, 150_000_000, 200_000_000 };

        // Velocities: base and random variance per tier (m/s)
        public static readonly double[] VelocityBaseByTier = { 8_000, 15_000, 30_000, 50_000, 100_000 };
        public static readonly double[] VelocityVarianceByTier = { 4_000, 10_000, 20_000, 50_000, 200_000 };

        // Targets per wave
        public static int TargetCountBase = 2;
        public static int TargetCountTierBonus = 1;
        public static int TargetCountRandomMaxExclusive = 3; // rng.Next(0,3)

        // Type pools (static to avoid allocations)
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

        // Evasiveness ranges by type (0..1) - already correct!
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

        // Unit helpers
        public const double MetersPerKilometer = 1000.0;
        public const double SecondsPerMinute = 60.0;

        public static string FormatDistance(double meters)
            => meters >= 1e6
                ? $"{meters / 1e6:F1} Gm" // gigameters
                : meters >= MetersPerKilometer
                    ? $"{meters / MetersPerKilometer:F1} km"
                    : $"{meters:F1} m";

        public static string FormatVelocity(double mPerS)
            => mPerS >= 1_000_000
                ? $"{mPerS / 1_000_000:F2} Mm/s"
                : mPerS >= 1_000
                    ? $"{mPerS / 1000.0:F2} km/s"
                    : $"{mPerS:F1} m/s";

        public static string FormatTime(double seconds)
            => seconds >= SecondsPerMinute
                ? $"{seconds / SecondsPerMinute:F1} minutes"
                : $"{seconds:F1} s";
    }

    // Simple config loader: reads Config/GameConfig.json if present and sets a subset of constants.
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
                if (cfg.MinBudgetToContinue.HasValue) GameConstants.MinBudgetToContinue = cfg.MinBudgetToContinue.Value;

                // Optional: expand config mappings here for the new tunables if desired.
            }
            catch
            {
                // ignore config errors; defaults remain
            }
        }

        private class GameConfig
        {
            public int? TotalWaves { get; set; }
            public double? BarrelIntegrityLossPerShot { get; set; }
            public double? BudgetRewardBase { get; set; }
            public double? BudgetRewardPerWave { get; set; }
            public double? SteelRewardBase { get; set; }
            public double? MinBudgetToContinue { get; set; }

            // Add new optional config fields here when you want to expose them to JSON.
        }
    }
}