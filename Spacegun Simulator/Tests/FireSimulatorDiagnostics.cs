using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Weapons;
using Spacegun_Simulator.Detection;
using Spacegun_Simulator.Enemies;

namespace Spacegun_Simulator.Tests
{
    public static class FireSimulatorDiagnostics
    {
        public readonly record struct CheckResult(string Name, bool Passed, string Message);

        public readonly record struct TuningCurveByTierResult(
            string RulesetLabel,
            double[] ExpectedHitRateByTier,
            double[] ObservedHitRateByTier,
            int[] ShotsByTier,
            int[] HitsByTier,
            double[] AvgEnemyMassKgByTier,
            double[] AvgEnemyFractureEnergyByTier,
            double[] AvgEnemyManeuverabilityByTier,
            double[] AvgEnemyOffenseByTier,
            double[] AvgEnemyDefenseByTier,
            double[] AvgEnemyVelocityMsByTier,
            double[] DetectionRateByTier,
            double[] BallisticsOkRateByTier);

        public readonly record struct TuningEnergyReportByTierResult(
            string RulesetLabel,
            string DifficultyLabel,
            int WeaponsTechLevel,
            int RadarLevel,
            int SamplesPerTier,
            bool SmoothTierSampling,
            double BarrelLengthMeters,
            double FireControlQuality,
            double MuzzleVelocityMultiplier,
            double ProjectileMassKg,
            double ProjectileDefense,
            double Penetration,
            double HitToleranceMultiplier,
            double PropulsionDeltaVCapacityMs,
            double PropulsionBurnDurationSeconds,
            double PropulsionReferenceMassKg,
            double EffectiveMaxGunVelocityMs,
            int[] SamplesByTier,
            int[] DetectedByTier,
            int[] EnergySufficientByTier,
            int[] CanHitByTier,
            int[] BallisticsOkByTier,
            int[] EnergyGatedByTier,
            double[] AvgEffectiveFractureEnergyMJByTier,
            double[] AvgKineticEnergyMJByTier,
            double[] AvgKeToFractureRatioByTier,
            double[] AvgBaselineVelocityMsByTier,
            double[] AvgTargetCrossSectionM2ByTier,
            double[] AvgTargetRadiusMByTier);

        public static IReadOnlyList<CheckResult> RunConsistencyChecks()
        {
            var results = new List<CheckResult>();

            void Run(string name, Action action)
            {
                try
                {
                    action();
                    results.Add(new CheckResult(name, Passed: true, Message: "OK"));
                }
                catch (Exception ex)
                {
                    results.Add(new CheckResult(name, Passed: false, Message: ex.Message));
                }
            }

            Run("Tier arrays consistency", TierArraysConsistencyTests.RunAllChecks);
            Run("Weapon tech mapping", ConstantsConsistencyChecks.RunWeaponTechMappingCheck);
            Run("Barrel wear mapping", ConstantsConsistencyChecks.RunBarrelWearMappingCheck);

            return results;
        }

        public readonly record struct TechAuditResult(string CsvPath, int ScenarioCount);

        public static TechAuditResult RunTechAuditAndWriteCsv(string? outputDirectory = null)
        {
            var scenarios = TestScenarios.GetTechAuditScenarios() ?? new List<TestScenario>();
            if (scenarios.Count == 0)
                return new TechAuditResult(CsvPath: string.Empty, ScenarioCount: 0);

            var csv = new StringBuilder();

            csv.AppendLine(string.Join(",",
                "Index",
                "Tier",
                "Tech Level",
                "Core Type",
                "Mass",
                "Muzzle Velocity (Ms)",
                "Delta-V (Ms)",
                "Kinetic Energy (MJ)",
                "Projectile Pos X @T+8s",
                "Projectile Pos Y @T+8s",
                "Projectile Pos Z @T+8s"
            ));

            int idx = 1;
            const double sampleTime = 8.0;

            foreach (var scenario in scenarios)
            {
                string tier = $"Tier{scenario.TechLevel}";
                int techLevel = scenario.TechLevel;
                string coreType = scenario.CoreType;
                double massKg = scenario.ProjectileMass;
                double baseMuzzle = scenario.BaseMuzzleVelocityMs;
                double deltaV = scenario.DeltaVMs;

                double finalSpeed = baseMuzzle + deltaV;
                double projectileKEMJ = BallisticsCalculator.CalculateKineticEnergyMJ(massKg, finalSpeed);

                var pos = FiringSolution.CalculateProjectilePositionStatic(sampleTime, finalSpeed, 45.0, 0.0);

                csv.AppendLine(string.Join(",",
                    idx.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(tier),
                    techLevel.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(coreType),
                    massKg.ToString("F3", CultureInfo.InvariantCulture),
                    baseMuzzle.ToString("F3", CultureInfo.InvariantCulture),
                    deltaV.ToString("F3", CultureInfo.InvariantCulture),
                    projectileKEMJ.ToString("F3", CultureInfo.InvariantCulture),
                    pos.X.ToString("F3", CultureInfo.InvariantCulture),
                    pos.Y.ToString("F3", CultureInfo.InvariantCulture),
                    pos.Z.ToString("F3", CultureInfo.InvariantCulture)
                ));

                idx++;
            }

            string dir = string.IsNullOrWhiteSpace(outputDirectory)
                ? Directory.GetCurrentDirectory()
                : outputDirectory;

            if (string.IsNullOrWhiteSpace(dir))
                dir = Directory.GetCurrentDirectory();

            string fileName = $"TechAuditResults_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.GetFullPath(Path.Combine(dir, fileName));

            string? outDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);
            File.WriteAllText(fullPath, csv.ToString(), Encoding.UTF8);

            return new TechAuditResult(CsvPath: fullPath, ScenarioCount: scenarios.Count);
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var escaped = value.Replace("\"", "\"\"");
            if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
                return $"\"{escaped}\"";
            return escaped;
        }

        private static readonly string[] s_defaultTuningLabRunCsvHeaders = new[]
        {
            "RunUtc",
            "Ruleset",
            "Difficulty",
            "WeaponsTechLevel",
            "RadarLevel",
            "SamplesPerWave",
            "ShotsPerSample",
            "SimulateAimError",
            "SmoothTierSampling",

            "OverrideEnemyDensity","EnemyDensity_gcm3",
            "OverrideEnemyBulkModulus","EnemyBulkModulus_GPa",

            "OverrideEnemyMass","EnemyMassKg",
            "OverrideEnemyFractureMult","EnemyFractureMult",
            "OverrideEnemyVelocity","EnemyVelocityMs",
            "OverrideEnemyManeuverability","EnemyManeuverability",
            "OverrideEnemyOffense","EnemyOffense",
            "OverrideEnemyDefense","EnemyDefense",

            "BarrelLength_m",
            "Guidance",
            "MuzzleVelocityMult",
            "ProjectileVelocity_kph",
            "ProjectileMass_kg",
            "ProjectileDefense",
            "Penetration_x",
            "HitToleranceMult_x",
            "PropulsionDeltaV_ms",
            "PropulsionBurn_s",
            "PropulsionRefMass_kg",

            "AvgEnemyMassKgByTier",
            "AvgEnemyFractureEnergyByTier",
            "AvgEnemyManeuverabilityByTier",
            "AvgEnemyOffenseByTier",
            "AvgEnemyDefenseByTier",
            "AvgEnemyVelocityMsByTier",
            "DetectionRateByTier",
            "BallisticsOkRateByTier",

            "TierIndex",
            "ExpectedHitRate",
            "ObservedHitRate",
            "Shots",
            "Hits"
        };

        public static IReadOnlyList<string> GetDefaultTuningLabRunCsvHeaders()
            => s_defaultTuningLabRunCsvHeaders;

        public static string AppendTuningLabRunCsv(
            EnemyGenerationRuleset ruleset,
            GameDifficulty difficulty,
            int weaponsTechLevel,
            int radarLevel,
            int samplesPerWave,
            int shotsPerSample,
            bool simulateAimError,
            bool smoothTierSampling,
            bool overrideEnemyDensity,
            double enemyDensityGcm3,
            bool overrideEnemyMaterialStrength,
            double enemyBulkModulusGpa,
            bool overrideEnemyMass,
            double enemyMassKg,
            bool overrideEnemyFractureEnergy,
            double enemyFractureEnergy,
            bool overrideEnemyVelocity,
            double enemyVelocityMs,
            bool overrideEnemyManeuverability,
            double enemyManeuverability,
            bool overrideEnemyOffense,
            double enemyOffense,
            bool overrideEnemyDefense,
            double enemyDefense,
            double barrelLengthMeters,
            double fireControlQuality,
            double muzzleVelocityMultiplier,
            double projectileMassKg,
            double projectileDefense,
            double penetration,
            double hitToleranceMultiplier,
            double propulsionDeltaVCapacityMs,
            double propulsionBurnDurationSeconds,
            double propulsionReferenceMassKg,
            in TuningCurveByTierResult result,
            IReadOnlyList<string>? headersOverride = null)
        {
            GameConfigLoader.LoadIfExists();

            var dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);

            string fullPath = Path.GetFullPath(Path.Combine(dir, "TuningLab_Runs.csv"));

            static string FormatInv(double value, string format)
                => value.ToString(format, CultureInfo.InvariantCulture);

            static string GetDifficultyLabel(GameDifficulty d)
                => d switch
                {
                    GameDifficulty.NuclearOption => "Easy",
                    GameDifficulty.CometsAndAsteroids => "Hard",
                    GameDifficulty.RealSpacegunSimulator => "Extreme",
                    _ => d.ToString()
                };

            var headers = (headersOverride is { Count: > 0 }) ? headersOverride : s_defaultTuningLabRunCsvHeaders;
            string headerLine = string.Join(",", headers);

            string runUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            int tierCount = Math.Min(result.ExpectedHitRateByTier.Length, result.ObservedHitRateByTier.Length);

            // Copy the 'in' parameter to a local so we can safely reference it in lambdas.
            var res = result;

            int maxTech = Math.Max(1, WeaponTuning.WeaponsTechVelocityMultipliers.Length);
            weaponsTechLevel = Math.Clamp(weaponsTechLevel, 1, maxTech);

            // Effective projectile launch velocity (kph) for this run (tier-independent).
            double baseMaxGunVelocity = Math.Max(1.0, WeaponTuning.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel));
            double effectiveVelMs = Math.Max(1.0, baseMaxGunVelocity * muzzleVelocityMultiplier);
            double barrelEfficiency = Math.Min(1.0, barrelLengthMeters / 200.0);
            double barrelVelocityMultiplier = (0.5 + 0.5 * barrelEfficiency);
            effectiveVelMs = Math.Max(1.0, effectiveVelMs * barrelVelocityMultiplier);
            double projectileVelocityKph = effectiveVelMs * 3.6;

            static double SafeAtD(double[] arr, int idx, double fallback = 0.0)
                => (arr is not null && idx >= 0 && idx < arr.Length) ? arr[idx] : fallback;

            static int SafeAtI(int[] arr, int idx, int fallback = 0)
                => (arr is not null && idx >= 0 && idx < arr.Length) ? arr[idx] : fallback;

            var providers = new Dictionary<string, Func<int, string>>(StringComparer.Ordinal)
            {
                ["RunUtc"] = _ => runUtc,
                ["Ruleset"] = _ => ruleset.ToString(),
                ["Difficulty"] = _ => GetDifficultyLabel(difficulty),
                ["WeaponsTechLevel"] = _ => weaponsTechLevel.ToString(CultureInfo.InvariantCulture),
                ["RadarLevel"] = _ => radarLevel.ToString(CultureInfo.InvariantCulture),
                ["SamplesPerWave"] = _ => samplesPerWave.ToString(CultureInfo.InvariantCulture),
                ["ShotsPerSample"] = _ => shotsPerSample.ToString(CultureInfo.InvariantCulture),
                ["SimulateAimError"] = _ => simulateAimError ? "1" : "0",
                ["SmoothTierSampling"] = _ => smoothTierSampling ? "1" : "0",

                ["OverrideEnemyDensity"] = _ => overrideEnemyDensity ? "1" : "0",
                ["EnemyDensity_gcm3"] = _ => overrideEnemyDensity ? FormatInv(enemyDensityGcm3, "F3") : string.Empty,
                ["OverrideEnemyBulkModulus"] = _ => overrideEnemyMaterialStrength ? "1" : "0",
                ["EnemyBulkModulus_GPa"] = _ => overrideEnemyMaterialStrength ? FormatInv(enemyBulkModulusGpa, "F3") : string.Empty,

                ["OverrideEnemyMass"] = _ => overrideEnemyMass ? "1" : "0",
                ["EnemyMassKg"] = i => FormatInv(overrideEnemyMass ? enemyMassKg : SafeAtD(res.AvgEnemyMassKgByTier, i), "F3"),
                ["OverrideEnemyFractureMult"] = _ => overrideEnemyFractureEnergy ? "1" : "0",
                ["EnemyFractureMult"] = _ => overrideEnemyFractureEnergy ? FormatInv(enemyFractureEnergy, "F6") : string.Empty,
                ["OverrideEnemyVelocity"] = _ => overrideEnemyVelocity ? "1" : "0",
                ["EnemyVelocityMs"] = i => FormatInv(overrideEnemyVelocity ? enemyVelocityMs : SafeAtD(res.AvgEnemyVelocityMsByTier, i), "F3"),
                ["OverrideEnemyManeuverability"] = _ => overrideEnemyManeuverability ? "1" : "0",
                ["EnemyManeuverability"] = _ => overrideEnemyManeuverability ? FormatInv(enemyManeuverability, "F6") : string.Empty,
                ["OverrideEnemyOffense"] = _ => overrideEnemyOffense ? "1" : "0",
                ["EnemyOffense"] = _ => overrideEnemyOffense ? FormatInv(enemyOffense, "F6") : string.Empty,
                ["OverrideEnemyDefense"] = _ => overrideEnemyDefense ? "1" : "0",
                ["EnemyDefense"] = _ => overrideEnemyDefense ? FormatInv(enemyDefense, "F6") : string.Empty,

                ["BarrelLength_m"] = _ => FormatInv(barrelLengthMeters, "F3"),
                ["Guidance"] = _ => FormatInv(fireControlQuality, "F6"),
                ["MuzzleVelocityMult"] = _ => FormatInv(muzzleVelocityMultiplier, "F6"),
                ["ProjectileVelocity_kph"] = _ => FormatInv(projectileVelocityKph, "F3"),
                ["ProjectileMass_kg"] = _ => FormatInv(projectileMassKg, "F3"),
                ["ProjectileDefense"] = _ => FormatInv(projectileDefense, "F6"),
                ["Penetration_x"] = _ => FormatInv(penetration, "F6"),
                ["HitToleranceMult_x"] = _ => FormatInv(hitToleranceMultiplier, "F6"),
                ["PropulsionDeltaV_ms"] = _ => FormatInv(propulsionDeltaVCapacityMs, "F3"),
                ["PropulsionBurn_s"] = _ => FormatInv(propulsionBurnDurationSeconds, "F6"),
                ["PropulsionRefMass_kg"] = _ => FormatInv(propulsionReferenceMassKg, "F3"),

                ["AvgEnemyMassKgByTier"] = i => FormatInv(SafeAtD(res.AvgEnemyMassKgByTier, i), "F3"),
                ["AvgEnemyFractureEnergyByTier"] = i => FormatInv(SafeAtD(res.AvgEnemyFractureEnergyByTier, i), "F3"),
                ["AvgEnemyManeuverabilityByTier"] = i => FormatInv(SafeAtD(res.AvgEnemyManeuverabilityByTier, i), "F6"),
                ["AvgEnemyOffenseByTier"] = i => FormatInv(SafeAtD(res.AvgEnemyOffenseByTier, i), "F6"),
                ["AvgEnemyDefenseByTier"] = i => FormatInv(SafeAtD(res.AvgEnemyDefenseByTier, i), "F6"),
                ["AvgEnemyVelocityMsByTier"] = i => FormatInv(SafeAtD(res.AvgEnemyVelocityMsByTier, i), "F3"),
                ["DetectionRateByTier"] = i => FormatInv(SafeAtD(res.DetectionRateByTier, i), "F6"),
                ["BallisticsOkRateByTier"] = i => FormatInv(SafeAtD(res.BallisticsOkRateByTier, i), "F6"),

                ["TierIndex"] = i => i.ToString(CultureInfo.InvariantCulture),
                ["ExpectedHitRate"] = i => FormatInv(SafeAtD(res.ExpectedHitRateByTier, i), "F6"),
                ["ObservedHitRate"] = i => FormatInv(SafeAtD(res.ObservedHitRateByTier, i), "F6"),
                ["Shots"] = i => SafeAtI(res.ShotsByTier, i).ToString(CultureInfo.InvariantCulture),
                ["Hits"] = i => SafeAtI(res.HitsByTier, i).ToString(CultureInfo.InvariantCulture)
            };

            var rows = new StringBuilder();
            for (int i = 0; i < tierCount; i++)
            {
                var row = new string[headers.Count];
                for (int c = 0; c < headers.Count; c++)
                {
                    string key = headers[c];
                    row[c] = providers.TryGetValue(key, out var fn) ? EscapeCsv(fn(i)) : string.Empty;
                }
                rows.AppendLine(string.Join(",", row));
            }

            static void AppendWithHeaderIfNeeded(string path, string header, string rowBlock)
            {
                bool exists = File.Exists(path);
                string text = exists ? rowBlock : header + Environment.NewLine + rowBlock;

                // Prefer a share-friendly append so viewers can keep the file open.
                // If another process holds an exclusive lock (e.g., Excel), this may still fail.
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(text);
            }

            // Retry briefly in case another run is appending.
            // If the schema changed (header mismatch), write to a stable versioned file (v2, v3, ...)
            // so repeated runs keep appending to the same schema-specific file.
            static bool HeaderMatches(string path, string expectedHeader)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
                    string? firstLine = reader.ReadLine();
                    return string.Equals(firstLine, expectedHeader, StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            }

            static string GetStableTargetPath(string dir, string baseFileName, string expectedHeader)
            {
                string basePath = Path.GetFullPath(Path.Combine(dir, baseFileName));
                if (!File.Exists(basePath))
                    return basePath;
                if (HeaderMatches(basePath, expectedHeader))
                    return basePath;

                // Find the first stable vN file that either matches this schema or doesn't exist yet.
                for (int version = 2; version <= 50; version++)
                {
                    string versioned = Path.GetFullPath(Path.Combine(dir, $"TuningLab_Runs_v{version}.csv"));
                    if (!File.Exists(versioned))
                        return versioned;
                    if (HeaderMatches(versioned, expectedHeader))
                        return versioned;
                }

                // Fallback: extremely unlikely, but keep it deterministic.
                return Path.GetFullPath(Path.Combine(dir, "TuningLab_Runs_v99.csv"));
            }

            string targetPath = GetStableTargetPath(dir, "TuningLab_Runs.csv", headerLine);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    AppendWithHeaderIfNeeded(targetPath, headerLine, rows.ToString());
                    return targetPath;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(200);
                }
                catch (IOException)
                {
                    break;
                }
            }

            // Fall back to a new file if the main file is locked.
            string fallbackPath = Path.GetFullPath(Path.Combine(
                dir,
                $"TuningLab_Runs_LOCKED_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"));

            AppendWithHeaderIfNeeded(fallbackPath, headerLine, rows.ToString());
            return fallbackPath;
        }

        public readonly record struct EnemyCurveResult(string CsvPath, int RowCount);

        public readonly record struct CounterCurveResult(string CsvPath, int RowCount);

        public readonly record struct EndToEndCurveResult(string CsvPath, int RowCount);

        private static int StableSeed(string key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
            byte[] hash = SHA256.HashData(bytes);
            int seed = BitConverter.ToInt32(hash, 0);
            return seed == int.MinValue ? 0 : Math.Abs(seed);
        }

        private static double NextGaussian(Random rng)
        {
            // Box–Muller (standard normal)
            double u1 = Math.Max(1e-12, rng.NextDouble());
            double u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        internal static bool TryFindBaselineBallisticSolution(
            FiringSolution calculator,
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            double maxGunVelocity,
            double gunEffectiveRange,
            int waveNumber,
            double enemyMass,
            GameDifficulty difficulty,
            bool requireDestroy,
            out (double DelaySeconds, double ElevDeg, double AzimDeg, double VelocityMs) baseline,
            out FiringSolutionResult result)
        {
            static (double elevDeg, double azimDeg) CartesianToAngles(Vector3 position)
            {
                double horizontalDistance = Math.Sqrt(position.X * position.X + position.Y * position.Y);
                double elevationRad = Math.Atan2(position.Z, horizontalDistance);
                double elevationDeg = elevationRad * 180.0 / Math.PI;

                double azimuthRad = Math.Atan2(position.X, position.Y);
                double azimuthDeg = azimuthRad * 180.0 / Math.PI;
                if (azimuthDeg < 0) azimuthDeg += 360.0;
                return (elevationDeg, azimuthDeg);
            }

            baseline = default;
            result = new FiringSolutionResult { CanDestroy = false, CanHit = false, SolutionValid = false, InterceptDeviation = float.MaxValue };

            static bool TrySolveInterceptTime(Vector3 relativePositionAtLaunch, Vector3 targetVelocity, double projectileSpeed, out double flightTime)
            {
                // Solve |r + v*t| = s*t where s=projectileSpeed.
                // Quadratic: (v·v - s^2) t^2 + 2(r·v) t + (r·r) = 0
                double rv = (relativePositionAtLaunch.X * targetVelocity.X)
                          + (relativePositionAtLaunch.Y * targetVelocity.Y)
                          + (relativePositionAtLaunch.Z * targetVelocity.Z);
                double vv = (targetVelocity.X * targetVelocity.X)
                          + (targetVelocity.Y * targetVelocity.Y)
                          + (targetVelocity.Z * targetVelocity.Z);
                double rr = (relativePositionAtLaunch.X * relativePositionAtLaunch.X)
                          + (relativePositionAtLaunch.Y * relativePositionAtLaunch.Y)
                          + (relativePositionAtLaunch.Z * relativePositionAtLaunch.Z);

                double a = vv - (projectileSpeed * projectileSpeed);
                double b = 2.0 * rv;
                double c = rr;

                const double eps = 1e-9;
                if (Math.Abs(a) < eps)
                {
                    // Linear: b t + c = 0
                    if (Math.Abs(b) < eps)
                    {
                        flightTime = 0;
                        return false;
                    }

                    double t = -c / b;
                    if (t > 0)
                    {
                        flightTime = t;
                        return true;
                    }

                    flightTime = 0;
                    return false;
                }

                double disc = (b * b) - (4.0 * a * c);
                if (disc < 0)
                {
                    flightTime = 0;
                    return false;
                }

                double sqrt = Math.Sqrt(disc);
                double t1 = (-b - sqrt) / (2.0 * a);
                double t2 = (-b + sqrt) / (2.0 * a);

                double tMin = double.PositiveInfinity;
                if (t1 > 0) tMin = Math.Min(tMin, t1);
                if (t2 > 0) tMin = Math.Min(tMin, t2);

                if (double.IsInfinity(tMin))
                {
                    flightTime = 0;
                    return false;
                }

                flightTime = tMin;
                return true;
            }

            // Deterministic, small search space: aim at analytical intercept (no-gravity lead),
            // then apply gravity compensation and small angle tweaks.
            double[] velFracs = new[] { 1.0, 0.85, 0.70 };

            // Match solver's allowed delay window per tier.
            var tier = GameConstants.GetTierForWave(waveNumber);
            double maxLaunchDelayTime = tier.TierIndex switch
            {
                0 => 60.0,
                1 => 120.0,
                2 => 180.0,
                _ => 180.0
            };

            double delayStep = 1.0;
            foreach (double frac in velFracs)
            {
                double v = Math.Max(1.0, maxGunVelocity * frac);

                // Candidate launch delays from immediate up to tier maximum.
                for (double delay = 0.0; delay <= maxLaunchDelayTime; delay += delayStep)
                {
                    Vector3 enemyAtLaunch = enemyPosition + (enemyVelocity * delay);
                    if (!TrySolveInterceptTime(enemyAtLaunch, enemyVelocity, v, out double tof))
                        continue;

                    tof = Math.Clamp(tof, 0.001, 300.0);
                    Vector3 enemyAtIntercept = enemyAtLaunch + (enemyVelocity * tof);

                    // If the intercept point is outside gun range, don't bother.
                    if (enemyAtIntercept.Magnitude > gunEffectiveRange)
                        continue;

                    // Gravity compensation: aim above the predicted point by the drop distance.
                    // (This is an approximation but generally good enough for a baseline.)
                    double drop = 0.5 * 9.81 * tof * tof;
                    var aimPoint = new Vector3(enemyAtIntercept.X, enemyAtIntercept.Y, enemyAtIntercept.Z + drop);
                    var (elevBase, azimBase) = CartesianToAngles(aimPoint);

                    // Small deterministic angle jitter around the estimate.
                    double[] elevOffsets = new[] { 0.0, 0.25, 0.5, 1.0, 2.0, -0.25, -0.5, -1.0 };
                    double[] azimOffsets = new[] { 0.0, -2.0, 2.0, -1.0, 1.0, -0.5, 0.5 };

                    foreach (double de in elevOffsets)
                    {
                        double elev = elevBase + de;
                        if (elev < -89.9 || elev > 89.9) continue;

                        foreach (double da in azimOffsets)
                        {
                            double azim = azimBase + da;
                            if (azim < 0) azim += 360.0;
                            if (azim >= 360.0) azim -= 360.0;

                            var res = calculator.CalculateSolution(
                                enemyPosition,
                                enemyVelocity,
                                delay,
                                elev,
                                azim,
                                v,
                                (float)maxGunVelocity,
                                (float)gunEffectiveRange,
                                waveNumber,
                                enemyMass,
                                difficulty);

                            if (res.CanHit && (!requireDestroy || res.CanDestroy))
                            {
                                baseline = (delay, elev, azim, v);
                                result = res;
                                return true;
                            }

                            // Keep the best "almost" solution for debugging callers.
                            // Prefer energy-sufficient results when requireDestroy is true.
                            if ((!requireDestroy && res.CanHit) || (requireDestroy && res.CanDestroy))
                            {
                                bool better = requireDestroy
                                    ? (!result.CanDestroy || res.InterceptDeviation < result.InterceptDeviation)
                                    : (res.CanHit && (!result.CanHit || res.InterceptDeviation < result.InterceptDeviation));

                                if (better)
                                {
                                    result = res;
                                    baseline = (delay, elev, azim, v);
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static TuningCurveByTierResult ComputeTuningCurveByTier(
            EnemyGenerationRuleset ruleset,
            GameDifficulty difficulty,
            int weaponsTechLevel,
            int radarLevel,
            bool overrideEnemyMass,
            double enemyMassKg,
            bool overrideEnemyFractureEnergy,
            double enemyFractureEnergy,
            bool overrideEnemyDensity,
            double enemyDensityGcm3,
            bool overrideEnemyMaterialStrength,
            double enemyBulkModulusGpa,
            bool overrideEnemyManeuverability,
            double enemyManeuverability,
            bool overrideEnemyOffense,
            double enemyOffense,
            bool overrideEnemyDefense,
            double enemyDefense,
            bool overrideBarrelLength,
            double barrelLength,
            bool overrideFireControlQuality,
            double fireControlQuality,
            bool overrideMuzzleVelocityMultiplier,
            double muzzleVelocityMultiplier,
            bool overrideProjectileMass,
            double projectileMassKg,
            bool overrideProjectileDefense,
            double projectileDefense,
            bool overridePenetration,
            double penetration,
            bool overrideHitToleranceMultiplier,
            double hitToleranceMultiplier,
            bool overridePropulsionDeltaV,
            double propulsionDeltaVCapacityMs,
            bool overridePropulsionBurnDuration,
            double propulsionBurnDurationSeconds,
            bool overridePropulsionReferenceMass,
            double propulsionReferenceMassKg,
            bool overrideEnemyVelocity = false,
            double enemyVelocityMs = 0.0,
            int samplesPerWave = 5,
            int shotsPerSample = 200,
            bool simulateAimError = false,
            bool smoothTierSampling = false)
        {
            GameConfigLoader.LoadIfExists();

            // NOTE: ShotsPerSample mostly reduces Bernoulli noise around an already-computed per-sample probability.
            // To reduce step artifacts (e.g. 0.333333, 0.666667), we need *more independent geometries*, i.e. more samples.
            // Allow a higher cap for offline tuning runs.
            samplesPerWave = Math.Clamp(samplesPerWave, 1, 500);
            shotsPerSample = Math.Clamp(shotsPerSample, 10, 5000);
            radarLevel = Math.Clamp(radarLevel, 1, 3);

            barrelLength = Math.Clamp(barrelLength, 50.0, 300.0);
            fireControlQuality = Math.Clamp(fireControlQuality, 0.25, 5.0);
            muzzleVelocityMultiplier = Math.Clamp(muzzleVelocityMultiplier, 0.25, 3.0);
            projectileMassKg = Math.Clamp(projectileMassKg, 10.0, 10_000.0);
            projectileDefense = Math.Clamp(projectileDefense, 0.0, 1.0);
            penetration = Math.Clamp(penetration, 0.10, 5.0);
            hitToleranceMultiplier = Math.Clamp(hitToleranceMultiplier, 0.10, 5.0);
            propulsionDeltaVCapacityMs = Math.Clamp(propulsionDeltaVCapacityMs, 0.0, 20000.0);
            propulsionBurnDurationSeconds = Math.Clamp(propulsionBurnDurationSeconds, 0.1, 120.0);
            propulsionReferenceMassKg = Math.Clamp(propulsionReferenceMassKg, 0.01, 2000.0);

            enemyMassKg = Math.Clamp(enemyMassKg, 0.01, 1e12);
            enemyFractureEnergy = Math.Clamp(enemyFractureEnergy, 0.0, 1e12);
            enemyVelocityMs = Math.Clamp(enemyVelocityMs, 0.0, 1e12);
            enemyDensityGcm3 = Math.Clamp(enemyDensityGcm3, 0.0, 20.0);
            enemyBulkModulusGpa = Math.Clamp(enemyBulkModulusGpa, 0.0, 2000.0);
            // Enemy capability factors are 0..1.
            // Keeping this consistent avoids confusion where values > 1 have no additional effect.
            enemyManeuverability = Math.Clamp(enemyManeuverability, 0.0, 1.0);
            enemyOffense = Math.Clamp(enemyOffense, 0.0, 1.0);
            enemyDefense = Math.Clamp(enemyDefense, 0.0, 1.0);

            string rulesetLabel = ruleset.ToString();
            string difficultyLabel = difficulty switch
            {
                GameDifficulty.NuclearOption => "Easy",
                GameDifficulty.CometsAndAsteroids => "Hard",
                GameDifficulty.RealSpacegunSimulator => "Extreme",
                _ => difficulty.ToString()
            };

            var expectedSum = new double[GameConstants.TierCount];
            var shotsSum = new int[GameConstants.TierCount];
            var hitsSum = new int[GameConstants.TierCount];

            // Denominator for per-tier rates. In legacy mode this equals (waves in tier) * samplesPerWave.
            // In SmoothTierSampling mode this equals samplesPerWave (interpreted as samples-per-tier).
            var sampleTotal = new int[GameConstants.TierCount];

            var enemyMassSum = new double[GameConstants.TierCount];
            var enemyFractureSum = new double[GameConstants.TierCount];
            var enemyManeuverSum = new double[GameConstants.TierCount];
            var enemyOffenseSum = new double[GameConstants.TierCount];
            var enemyDefenseSum = new double[GameConstants.TierCount];
            var enemyVelocitySum = new double[GameConstants.TierCount];
            var sampleCount = new int[GameConstants.TierCount];

            var detectedCount = new int[GameConstants.TierCount];
            var ballisticsOkCount = new int[GameConstants.TierCount];

            var campaignRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|campaign"));
            var campaignType = EnemyType.GenerateForCampaign(campaignRng);

            int maxTech = Math.Max(1, WeaponTuning.WeaponsTechVelocityMultipliers.Length);
            weaponsTechLevel = Math.Clamp(weaponsTechLevel, 1, maxTech);

            const double defaultBarrelLength = 100.0;
            const double defaultFireControlQuality = 1.0;
            const double defaultMuzzleVelocityMultiplier = 1.0;
            const double defaultProjectileMassKg = 5000.0;
            const double defaultProjectileDefense = 0.0;
            const double defaultPenetration = 1.0;
            const double defaultHitToleranceMultiplier = 1.0;
            const double defaultPropulsionDeltaVCapacityMs = 0.0;
            const double defaultPropulsionBurnDurationSeconds = 1.0;
            const double defaultPropulsionReferenceMassKg = 5000.0;

            // Player muzzle velocity is tier-independent; tiers only affect enemy/wave values.
            double baseMaxGunVelocity = Math.Max(1.0, WeaponTuning.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel));
            double effectiveBarrelLength = overrideBarrelLength ? barrelLength : defaultBarrelLength;
            double effectiveFireControl = overrideFireControlQuality ? fireControlQuality : defaultFireControlQuality;
            double effectiveProjectileMass = overrideProjectileMass ? projectileMassKg : defaultProjectileMassKg;
            double effectiveProjectileDefense = overrideProjectileDefense ? projectileDefense : defaultProjectileDefense;
            double effectivePenetration = overridePenetration ? penetration : defaultPenetration;
            double effectiveHitToleranceMultiplierBase = overrideHitToleranceMultiplier ? hitToleranceMultiplier : defaultHitToleranceMultiplier;
            double effectivePropulsionDeltaVCapacityMs = overridePropulsionDeltaV ? propulsionDeltaVCapacityMs : defaultPropulsionDeltaVCapacityMs;
            double effectivePropulsionBurnDurationSeconds = overridePropulsionBurnDuration ? propulsionBurnDurationSeconds : defaultPropulsionBurnDurationSeconds;
            double effectivePropulsionReferenceMassKg = overridePropulsionReferenceMass ? propulsionReferenceMassKg : defaultPropulsionReferenceMassKg;

            double baseImpactCoupling = DevelopmentTuning.ProjectileDefaults.ImpactCoupling;
            double couplingReferenceMassKg = Math.Max(0.01, DevelopmentTuning.ProjectileDefaults.ImpactCouplingReferenceMassKg);
            double couplingMassExponent = Math.Max(0.0, DevelopmentTuning.ProjectileDefaults.ImpactCouplingMassExponent);
            double couplingMassScale = couplingMassExponent > 0.0
                ? Math.Pow(couplingReferenceMassKg / Math.Max(0.01, effectiveProjectileMass), couplingMassExponent)
                : 1.0;
            double couplingTechPerLevel = Math.Max(0.0, DevelopmentTuning.ProjectileDefaults.ImpactCouplingTechMultiplierPerWeaponsLevel);
            double couplingTechScale = couplingTechPerLevel != 1.0
                ? Math.Pow(couplingTechPerLevel, Math.Max(0, weaponsTechLevel - 1))
                : 1.0;
            double impactCoupling = Math.Clamp(baseImpactCoupling * couplingMassScale * couplingTechScale, 0.0001, 100.0);

            // Enemy density UI is in g/cm^3. 1 g/cm^3 = 1000 kg/m^3.
            double densityKgM3Override = Math.Max(0.0, enemyDensityGcm3) * 1000.0;
            bool applyDensity = overrideEnemyDensity && densityKgM3Override > 0.0;

            bool applyBulk = overrideEnemyMaterialStrength && enemyBulkModulusGpa > 0.0;

            double effectiveMaxGunVelocity = Math.Max(1.0,
                baseMaxGunVelocity * (overrideMuzzleVelocityMultiplier ? muzzleVelocityMultiplier : defaultMuzzleVelocityMultiplier));

            // Mirror gameplay's barrel-length effect on velocity cap (0.5..1.0 multiplier).
            double barrelEfficiency = Math.Min(1.0, effectiveBarrelLength / 200.0);
            double barrelVelocityMultiplier = (0.5 + 0.5 * barrelEfficiency);
            effectiveMaxGunVelocity = Math.Max(1.0, effectiveMaxGunVelocity * barrelVelocityMultiplier);

            void ProcessSample(int waveNumber, int sample)
            {
                var tier = GameConstants.GetTierForWave(waveNumber);
                int tierIndex = tier.TierIndex;
                sampleTotal[tierIndex]++;

                var rng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|wave{waveNumber}|{sample}"));
                var wave = EnemyWave.GenerateWave(waveNumber, rng, ruleset, campaignType);
                var target = wave.Targets[0];

                // TuningLab: use per-tier max enemy velocity unless explicitly overridden.
                double tierMaxVelocity = GameConstants.TierEnemyMaxVelocity[Math.Clamp(tierIndex, 0, GameConstants.TierEnemyMaxVelocity.Length - 1)];
                wave.AverageVelocity = overrideEnemyVelocity ? enemyVelocityMs : tierMaxVelocity;

                // Keep cached vectors consistent if wave generation populated them.
                if (wave.CachedEnemyVelocity.HasValue)
                {
                    var v = wave.CachedEnemyVelocity.Value;
                    double mag = v.Magnitude;
                    if (mag > 0)
                    {
                        double s = wave.AverageVelocity / mag;
                        wave.CachedEnemyVelocity = new Vector3(v.X * s, v.Y * s, v.Z * s);
                    }
                }

                enemyVelocitySum[tierIndex] += wave.AverageVelocity;

                // Enemy mass override UI is in kg; core simulation uses tons.
                double effectiveEnemyMassTons = overrideEnemyMass ? (enemyMassKg / 1000.0) : target.Mass;
                effectiveEnemyMassTons = Math.Clamp(effectiveEnemyMassTons, 0.000001, 1e12);

                double effectiveDensityKgM3 = applyDensity
                    ? densityKgM3Override
                    : (target.DensityKgM3 > 0.0 ? target.DensityKgM3 : 500.0);

                double effectiveBulkModulusGpa = applyBulk
                    ? enemyBulkModulusGpa
                    : (target.BulkModulusGpa > 0.0 ? target.BulkModulusGpa : 200.0);

                // Apply overrides back onto the generated wave so downstream systems
                // (detection + firing solver) use a consistent geometry.
                target.Mass = effectiveEnemyMassTons;
                target.DensityKgM3 = effectiveDensityKgM3;
                target.BulkModulusGpa = effectiveBulkModulusGpa;

                double derivedCrossSectionM2 = BallisticsCalculator.CalculateCrossSectionAreaM2FromMassAndDensity(
                    effectiveEnemyMassTons,
                    effectiveDensityKgM3);

                wave.AverageRadarCrossSection = derivedCrossSectionM2;
                target.CrossSection = derivedCrossSectionM2;

                // In TuningLab, the fracture energy override is interpreted as a multiplier
                // applied to the wave's base fracture energy.
                double baseEnemyFractureEnergy = BallisticsCalculator.CalculateFractureEnergyMJFromMassDensityAndBulkModulus(
                    effectiveEnemyMassTons,
                    effectiveDensityKgM3,
                    effectiveBulkModulusGpa,
                    DevelopmentTuning.TierTargetMaterial.FractureStrain);

                target.FractureEnergy = baseEnemyFractureEnergy;

                double fractureMult = overrideEnemyFractureEnergy ? Math.Clamp(enemyFractureEnergy, 0.0, 10.0) : 1.0;
                double effectiveEnemyFractureRaw = baseEnemyFractureEnergy * fractureMult;

                double effectiveEnemyManeuverability = overrideEnemyManeuverability ? enemyManeuverability : target.Maneuverability;
                double effectiveEnemyOffense = overrideEnemyOffense ? enemyOffense : target.Offense;
                double effectiveEnemyDefense = overrideEnemyDefense ? enemyDefense : target.Defense;

                    enemyMassSum[tierIndex] += effectiveEnemyMassTons * 1000.0;
                    enemyFractureSum[tierIndex] += effectiveEnemyFractureRaw;
                    enemyManeuverSum[tierIndex] += effectiveEnemyManeuverability;
                    enemyOffenseSum[tierIndex] += effectiveEnemyOffense;
                    enemyDefenseSum[tierIndex] += effectiveEnemyDefense;
                    sampleCount[tierIndex]++;

                    var detection = new DetectionSystem
                    {
                        DetectionRangeMultiplier = radarLevel switch { 1 => 1.0, 2 => 1.15, 3 => 1.30, _ => 1.30 },
                        MaxSimultaneousTargets = radarLevel switch { 1 => 5, 2 => 8, 3 => 12, _ => 12 },
                        StealthPenetration = radarLevel switch { 1 => 0.0, 2 => 0.40, 3 => 0.75, _ => 0.75 },
                        IntelResolution = radarLevel switch { 1 => 0.20, 2 => 0.50, 3 => 0.80, _ => 0.80 },
                    };

                    var det = detection.GetDetectionStatus(wave);
                    if (!det.IsDetected)
                    {
                        // Unconditional hit chance is 0; still count shots for observed rate if requested.
                        shotsSum[tierIndex] += shotsPerSample;
                        return;
                    }

                    detectedCount[tierIndex]++;

                    double stealthMult = detection.GetStealthRangeMultiplier(wave);
                    double barrelMult = Math.Clamp(effectiveBarrelLength / 100.0, 0.5, 2.0);
                    double effectiveGunRange = tier.MaxEffectiveGunRange * barrelMult * stealthMult;

                    double effectiveEnemyDefense01 = Math.Clamp(effectiveEnemyDefense, 0.0, 1.0);
                    double defenseScale = Math.Max(0.0, GameModeTuning.Current.FractureEnergyDefenseScale);
                    double armoredEnemyFracture = Math.Max(0.0, effectiveEnemyFractureRaw * (1.0 + defenseScale * effectiveEnemyDefense01));
                    double effectiveEnemyFractureEnergy = Math.Max(0.0, armoredEnemyFracture / Math.Max(0.1, effectivePenetration));

                    double massEfficiency = effectivePropulsionReferenceMassKg / (effectivePropulsionReferenceMassKg + Math.Max(0.0, effectiveProjectileMass));
                    double bestCaseDeltaV = effectivePropulsionDeltaVCapacityMs * massEfficiency;

                    // Compute an aim solution baseline using the wave's generated geometry.
                    double baselineVelocity;
                    double baselineDelay;
                    double baselineElev;
                    double baselineAzim;
                    Vector3 enemyPosition;
                    Vector3 enemyVelocity;
                    var calculator = new FiringSolution(
                        projectileMass: (float)effectiveProjectileMass,
                        enemyFractureEnergy: (float)effectiveEnemyFractureEnergy,
                        enemyMass: effectiveEnemyMassTons,
                        enemyCrossSectionM2: target.CrossSection);

                    calculator.ConfigureProjectileModifiers(
                        additionalHitToleranceMultiplier: effectiveHitToleranceMultiplierBase,
                        propulsionDeltaVCapacityMs: effectivePropulsionDeltaVCapacityMs,
                        propulsionBurnDurationSeconds: effectivePropulsionBurnDurationSeconds,
                        propulsionReferenceMassKg: effectivePropulsionReferenceMassKg);

                    try
                    {
                        // Ensure per-sample stability.
                        var problemRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|wave{waveNumber}|{sample}|problem"));
                        var firingProblem = calculator.GenerateFiringProblem(
                            wave,
                            playerGunMaxVelocity: (float)effectiveMaxGunVelocity,
                            gunEffectiveRange: (float)effectiveGunRange,
                            rng: problemRng);

                        enemyPosition = firingProblem.EnemyPosition;
                        enemyVelocity = firingProblem.EnemyVelocity;

                        // First try the problem's own recommended solution. Even if it's heuristic,
                        // it's usually a much better starting point than a generic intercept guess.
                        baselineDelay = firingProblem.CorrectLaunchDelayTime;
                        baselineElev = firingProblem.CorrectElevation;
                        baselineAzim = firingProblem.CorrectAzimuth;
                        baselineVelocity = firingProblem.CorrectVelocity;
                    }
                    catch
                    {
                        // Fallback: still produce a sensible baseline even if geometry generation fails.
                        baselineVelocity = effectiveMaxGunVelocity * 0.85;
                        baselineDelay = 0.0;

                        // If the wave didn't cache vectors (common for non-tutorial generation), use a simple
                        // straight-in approach from +X with the tier's average velocity.
                        enemyPosition = wave.CachedEnemyPosition ?? new Vector3(wave.InitialDistance, 0.0, 0.0);
                        enemyVelocity = wave.CachedEnemyVelocity ?? (overrideEnemyVelocity
                            ? new Vector3(-wave.AverageVelocity, 0.0, 0.0)
                            : Vector3.Zero);
                        baselineElev = 0.0;
                        baselineAzim = 0.0;
                    }

                    // Validate the initial baseline. If it isn't valid, fall back to a deterministic search.
                    var initialRes = calculator.CalculateSolution(
                        enemyPosition,
                        enemyVelocity,
                        baselineDelay,
                        baselineElev,
                        baselineAzim,
                        Math.Min(baselineVelocity, effectiveMaxGunVelocity),
                        (float)effectiveMaxGunVelocity,
                        (float)effectiveGunRange,
                        waveNumber,
                        effectiveEnemyMassTons,
                        difficulty);

                    if (!initialRes.CanHit)
                    {
                        // Find a valid baseline firing solution (aiming + gravity compensation) for this geometry.
                        // This models a "competent" shot rather than trusting the heuristic values.
                        if (TryFindBaselineBallisticSolution(
                            calculator,
                            enemyPosition,
                            enemyVelocity,
                            effectiveMaxGunVelocity,
                            effectiveGunRange,
                            waveNumber,
                            effectiveEnemyMassTons,
                            difficulty,
                            requireDestroy: false,
                            out var baseline,
                            out _))
                        {
                            baselineDelay = baseline.DelaySeconds;
                            baselineElev = baseline.ElevDeg;
                            baselineAzim = baseline.AzimDeg;
                            baselineVelocity = baseline.VelocityMs;
                        }
                        else
                        {
                            // If we couldn't find a valid solution, keep deterministic fallbacks.
                            baselineVelocity = effectiveMaxGunVelocity;
                            baselineDelay = 0.0;
                            baselineElev = 0.0;
                            baselineAzim = 0.0;
                        }
                    }

                    // Player baseline velocity (already within max by construction).
                    double cappedVelocity = Math.Min(baselineVelocity, effectiveMaxGunVelocity);

                    double keMj = BallisticsCalculator.CalculateKineticEnergyMJ(effectiveProjectileMass, cappedVelocity + bestCaseDeltaV);
                    bool energySufficient = keMj >= effectiveEnemyFractureEnergy;

                    double evasionChance = CombatCurves.ComputeEvasionChance(effectiveEnemyManeuverability, effectiveFireControl);
                    double interceptChance = CombatCurves.ComputeInterceptKillChance(effectiveEnemyOffense, effectiveProjectileDefense);

                    // Include solver validity (range/geometry) using the computed baseline aiming solution.
                    var baseRes = calculator.CalculateSolution(
                        enemyPosition,
                        enemyVelocity,
                        baselineDelay,
                        baselineElev,
                        baselineAzim,
                        cappedVelocity,
                        (float)effectiveMaxGunVelocity,
                        (float)effectiveGunRange,
                        waveNumber,
                        effectiveEnemyMassTons,
                        difficulty);
                    bool baseBallisticsOk = baseRes.CanHit && baseRes.CanDestroy;

                    if (baseBallisticsOk)
                        ballisticsOkCount[tierIndex]++;

                    double expectedHit = (baseBallisticsOk ? 1.0 : 0.0) * (1.0 - evasionChance) * (1.0 - interceptChance);
                    expectedHit = Math.Clamp(expectedHit, 0.0, 1.0);

                    expectedSum[tierIndex] += expectedHit;

                    // Observed: either quick Bernoulli on expectedHit, or a more realistic aim-error simulation.
                    var shotRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|wave{waveNumber}|{sample}|shots"));
                    int hits = 0;

                    if (!simulateAimError)
                    {
                        for (int i = 0; i < shotsPerSample; i++)
                            if (shotRng.NextDouble() < expectedHit) hits++;
                    }
                    else
                    {
                        // Conservative baseline aim error model.
                        const double delaySigmaSeconds = 0.50;
                        const double elevSigmaDeg = 0.50;
                        const double azimSigmaDeg = 0.50;
                        const double velSigmaFraction = 0.02;

                        for (int i = 0; i < shotsPerSample; i++)
                        {
                            if (!energySufficient)
                                continue;

                            double playerDelay = Math.Max(0.0, baselineDelay + NextGaussian(shotRng) * delaySigmaSeconds);
                            double playerElev = NextGaussian(shotRng) * elevSigmaDeg;
                            double playerAzim = NextGaussian(shotRng) * azimSigmaDeg;
                            double playerVel = Math.Max(1.0, cappedVelocity * (1.0 + NextGaussian(shotRng) * velSigmaFraction));
                            playerVel = Math.Min(playerVel, effectiveMaxGunVelocity);

                            // Aim at the known correct angles with noise around them.
                            double baseElev = baselineElev;
                            double baseAzim = baselineAzim;

                            var res = calculator.CalculateSolution(
                                enemyPosition,
                                enemyVelocity,
                                playerDelay,
                                baseElev + playerElev,
                                baseAzim + playerAzim,
                                playerVel,
                                (float)effectiveMaxGunVelocity,
                                (float)effectiveGunRange,
                                waveNumber,
                                effectiveEnemyMassTons,
                                difficulty);

                            bool hit = res.CanHit && res.CanDestroy;
                            if (!hit)
                                continue;

                            if (shotRng.NextDouble() < evasionChance)
                                continue;

                            if (shotRng.NextDouble() < interceptChance)
                                continue;

                            hits++;
                        }
                    }

                    shotsSum[tierIndex] += shotsPerSample;
                    hitsSum[tierIndex] += hits;
            }

            if (!smoothTierSampling)
            {
                for (int waveNumber = 1; waveNumber <= 25; waveNumber++)
                {
                    for (int sample = 1; sample <= samplesPerWave; sample++)
                        ProcessSample(waveNumber, sample);
                }
            }
            else
            {
                // SmoothTierSampling: interpret samplesPerWave as "samples per tier".
                // For each tier, pick a wave number uniformly from the tier's wave range per sample.
                for (int tierIndex = 0; tierIndex < GameConstants.TierCount; tierIndex++)
                {
                    var tier = GameConstants.WaveTiers[tierIndex];
                    int waveCount = Math.Max(1, tier.EndWave - tier.StartWave + 1);
                    for (int sample = 1; sample <= samplesPerWave; sample++)
                    {
                        var pickRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|tier{tierIndex}|pick{sample}"));
                        int waveNumber = tier.StartWave + pickRng.Next(0, waveCount);
                        ProcessSample(waveNumber, sample);
                    }
                }
            }

            var expectedAvg = new double[GameConstants.TierCount];
            var observedAvg = new double[GameConstants.TierCount];

            var avgEnemyMass = new double[GameConstants.TierCount];
            var avgEnemyFracture = new double[GameConstants.TierCount];
            var avgEnemyManeuver = new double[GameConstants.TierCount];
            var avgEnemyOffense = new double[GameConstants.TierCount];
            var avgEnemyDefense = new double[GameConstants.TierCount];
            var avgEnemyVelocity = new double[GameConstants.TierCount];
            var detectionRate = new double[GameConstants.TierCount];
            var ballisticsOkRate = new double[GameConstants.TierCount];

            for (int t = 0; t < GameConstants.TierCount; t++)
            {
                int denom = Math.Max(1, sampleTotal[t]);
                expectedAvg[t] = expectedSum[t] / denom;
                observedAvg[t] = shotsSum[t] > 0 ? (double)hitsSum[t] / shotsSum[t] : 0.0;

                int s = Math.Max(1, sampleCount[t]);
                avgEnemyMass[t] = enemyMassSum[t] / s;
                avgEnemyFracture[t] = enemyFractureSum[t] / s;
                avgEnemyManeuver[t] = enemyManeuverSum[t] / s;
                avgEnemyOffense[t] = enemyOffenseSum[t] / s;
                avgEnemyDefense[t] = enemyDefenseSum[t] / s;
                avgEnemyVelocity[t] = enemyVelocitySum[t] / s;

                detectionRate[t] = (double)detectedCount[t] / denom;
                ballisticsOkRate[t] = (double)ballisticsOkCount[t] / Math.Max(1, detectedCount[t]);
            }

            return new TuningCurveByTierResult(
                RulesetLabel: rulesetLabel,
                ExpectedHitRateByTier: expectedAvg,
                ObservedHitRateByTier: observedAvg,
                ShotsByTier: shotsSum,
                HitsByTier: hitsSum,
                AvgEnemyMassKgByTier: avgEnemyMass,
                AvgEnemyFractureEnergyByTier: avgEnemyFracture,
                AvgEnemyManeuverabilityByTier: avgEnemyManeuver,
                AvgEnemyOffenseByTier: avgEnemyOffense,
                AvgEnemyDefenseByTier: avgEnemyDefense,
                AvgEnemyVelocityMsByTier: avgEnemyVelocity,
                DetectionRateByTier: detectionRate,
                BallisticsOkRateByTier: ballisticsOkRate);
        }

        public static EnemyCurveResult RunEnemyCurveAndWriteCsv(string? outputDirectory = null)
        {
            GameConfigLoader.LoadIfExists();

            const int samplesPerTier = 5;
            var detection = new DetectionSystem();
            var csv = new StringBuilder();

            csv.AppendLine(string.Join(",",
                "ModeId",
                "ModeName",
                "Ruleset",
                "Difficulty",
                "TierIndex",
                "WaveNumber",
                "Sample",
                "CampaignArchetypeId",
                "CampaignName",
                "WaveArchetypeId",
                "WaveArchetypeName",
                "ShipCount",
                "InitialDistance",
                "AverageVelocity",
                "RcsRaw",
                "RcsModeAdjusted",
                "RcsDisplay",
                "HasStealthCoating",
                "Acceleration",
                "Maneuverability",
                "Defense",
                "Mass",
                "FractureEnergy",
                "WaveDistanceAU",
                "EffectiveDetectionRangeAU",
                "Detected",
                "DetectionQuality",
                "WarningTimeSeconds",
                "MinSafeTimeSeconds",
                "TimeToImpactSeconds",
                "TimeToGunRangeSecondsBase",
                "TimeToGunRangeSecondsTuned",
                "AvailableYearsRounded",
                "TierMaxEffectiveGunRange",
                "DifficultyHitToleranceMultiplier",
                "ModeHitToleranceMultiplier",
                "DifficultyTargetRcsMultiplier",
                "HitToleranceMeters"
            ));

            int rowCount = 0;
            var modes = GameModeCatalog.GetAll();

            foreach (var mode in modes)
            {
                if (mode.IsTutorial)
                {
                    for (int sample = 1; sample <= samplesPerTier; sample++)
                    {
                        int waveNumber = sample;
                        var rng = new Random(StableSeed($"{mode.Id}|tutorial|{sample}"));
                        var wave = EnemyWave.GenerateTutorialWave(waveNumber, rng);
                        var target = wave.Targets[0];

                        var diffConfig = DifficultyConfig.GetConfig(mode.Difficulty);
                        var tier = GameConstants.GetTierForWave(wave.WaveNumber);

                        double waveDistanceAu = wave.CurrentDistance / GameConstants.AU_TO_METERS;
                        var det = detection.GetDetectionStatus(wave);
                        double effectiveDetRangeAu = detection.CalculateEffectiveDetectionRange(wave);
                        double warningTimeSeconds = detection.CalculateWarningTime(wave);
                        double minSafeTimeSeconds = tier.TimeToImpactMin * 0.1;

                        double timeToImpactSeconds = wave.CurrentDistance / Math.Max(1e-9, wave.AverageVelocity);
                        double timeToGunRangeBaseSeconds = Math.Max(0.0, (wave.InitialDistance - tier.MaxEffectiveGunRange) / Math.Max(1e-9, wave.AverageVelocity));
                        double timeMult = GameModeTuning.Current.GetTimeBudgetMultiplier(mode);
                        double timeToGunRangeTunedSeconds = timeToGunRangeBaseSeconds * timeMult;
                        long availableYearsRounded = Math.Max(1, (long)Math.Round(timeToGunRangeTunedSeconds / GameConstants.SECONDS_PER_YEAR));

                        double rcsRaw = wave.AverageRadarCrossSection;
                        double rcsModeAdjusted = rcsRaw * GameModeTuning.Current.GetDetectionRcsMultiplier(mode);
                        double rcsDisplay = rcsModeAdjusted * diffConfig.TargetRcsMultiplier;

                        double hitToleranceMeters = diffConfig.IsTutorialMode
                            ? DifficultyConfig.TutorialBeachball.RadiusMeters
                            : 0.5 * (2.0 * Math.Sqrt(rcsDisplay / Math.PI)) * diffConfig.HitToleranceMultiplier * GameModeTuning.Current.GetHitToleranceMultiplier(mode);

                        csv.AppendLine(string.Join(",",
                            EscapeCsv(mode.Id.ToString()),
                            EscapeCsv(mode.DisplayName),
                            "Tutorial",
                            EscapeCsv(GameModeCatalog.GetDifficultyLabel(mode)),
                            "-1",
                            waveNumber.ToString(CultureInfo.InvariantCulture),
                            sample.ToString(CultureInfo.InvariantCulture),
                            "",
                            "",
                            EscapeCsv(wave.Archetype?.Id),
                            EscapeCsv(wave.Archetype?.Name),
                            wave.ShipCount.ToString(CultureInfo.InvariantCulture),
                            wave.InitialDistance.ToString("F3", CultureInfo.InvariantCulture),
                            wave.AverageVelocity.ToString("F3", CultureInfo.InvariantCulture),
                            rcsRaw.ToString("F6", CultureInfo.InvariantCulture),
                            rcsModeAdjusted.ToString("F6", CultureInfo.InvariantCulture),
                            rcsDisplay.ToString("F6", CultureInfo.InvariantCulture),
                            wave.HasStealthCoating ? "1" : "0",
                            target.Acceleration.ToString("F6", CultureInfo.InvariantCulture),
                            target.Maneuverability.ToString("F6", CultureInfo.InvariantCulture),
                            target.Defense.ToString("F6", CultureInfo.InvariantCulture),
                            target.Mass.ToString("F3", CultureInfo.InvariantCulture),
                            target.FractureEnergy.ToString("F3", CultureInfo.InvariantCulture),
                            waveDistanceAu.ToString("F9", CultureInfo.InvariantCulture),
                            effectiveDetRangeAu.ToString("F6", CultureInfo.InvariantCulture),
                            det.IsDetected ? "1" : "0",
                            EscapeCsv(det.Quality.ToString()),
                            warningTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            minSafeTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            timeToImpactSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            timeToGunRangeBaseSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            timeToGunRangeTunedSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            availableYearsRounded.ToString(CultureInfo.InvariantCulture),
                            tier.MaxEffectiveGunRange.ToString("F3", CultureInfo.InvariantCulture),
                            diffConfig.HitToleranceMultiplier.ToString("F6", CultureInfo.InvariantCulture),
                            GameModeTuning.Current.GetHitToleranceMultiplier(mode).ToString("F6", CultureInfo.InvariantCulture),
                            diffConfig.TargetRcsMultiplier.ToString("F6", CultureInfo.InvariantCulture),
                            hitToleranceMeters.ToString("F6", CultureInfo.InvariantCulture)
                        ));
                        rowCount++;
                    }

                    continue;
                }

                var ruleset = mode.UsesEconomyAndDevelopment ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;
                string rulesetLabel = ruleset.ToString();
                var diffConfigFull = DifficultyConfig.GetConfig(mode.Difficulty);

                var campaignRng = new Random(StableSeed($"{mode.Id}|campaign"));
                var campaignType = EnemyType.GenerateForCampaign(campaignRng);

                for (int tierIndex = 0; tierIndex < GameConstants.TierCount; tierIndex++)
                {
                    int waveNumber = GameConstants.WaveTiers[tierIndex].StartWave;

                    for (int sample = 1; sample <= samplesPerTier; sample++)
                    {
                        var rng = new Random(StableSeed($"{mode.Id}|tier{tierIndex}|wave{waveNumber}|{sample}"));
                        var wave = EnemyWave.GenerateWave(waveNumber, rng, ruleset, campaignType);
                        var target = wave.Targets[0];

                        var tier = GameConstants.GetTierForWave(wave.WaveNumber);

                        double waveDistanceAu = wave.CurrentDistance / GameConstants.AU_TO_METERS;
                        var det = detection.GetDetectionStatus(wave);
                        double effectiveDetRangeAu = detection.CalculateEffectiveDetectionRange(wave);
                        double warningTimeSeconds = detection.CalculateWarningTime(wave);
                        double minSafeTimeSeconds = tier.TimeToImpactMin * 0.1;

                        double timeToImpactSeconds = wave.CurrentDistance / Math.Max(1e-9, wave.AverageVelocity);
                        double timeToGunRangeBaseSeconds = Math.Max(0.0, (wave.InitialDistance - tier.MaxEffectiveGunRange) / Math.Max(1e-9, wave.AverageVelocity));
                        double timeMult = GameModeTuning.Current.GetTimeBudgetMultiplier(mode);
                        double timeToGunRangeTunedSeconds = timeToGunRangeBaseSeconds * timeMult;
                        long availableYearsRounded = Math.Max(1, (long)Math.Round(timeToGunRangeTunedSeconds / GameConstants.SECONDS_PER_YEAR));

                        double rcsRaw = wave.AverageRadarCrossSection;
                        double rcsModeAdjusted = rcsRaw * GameModeTuning.Current.GetDetectionRcsMultiplier(mode);
                        double rcsDisplay = rcsModeAdjusted * diffConfigFull.TargetRcsMultiplier;

                        double hitToleranceMeters;
                        if (diffConfigFull.IsTutorialMode)
                        {
                            hitToleranceMeters = DifficultyConfig.TutorialBeachball.RadiusMeters;
                        }
                        else
                        {
                            double diameterFromRcs = 2.0 * Math.Sqrt(rcsDisplay / Math.PI);
                            hitToleranceMeters = diameterFromRcs * 0.5 * diffConfigFull.HitToleranceMultiplier * GameModeTuning.Current.GetHitToleranceMultiplier(mode);
                        }

                        csv.AppendLine(string.Join(",",
                            EscapeCsv(mode.Id.ToString()),
                            EscapeCsv(mode.DisplayName),
                            EscapeCsv(rulesetLabel),
                            EscapeCsv(GameModeCatalog.GetDifficultyLabel(mode)),
                            tierIndex.ToString(CultureInfo.InvariantCulture),
                            waveNumber.ToString(CultureInfo.InvariantCulture),
                            sample.ToString(CultureInfo.InvariantCulture),
                            EscapeCsv(campaignType.Archetype.Id),
                            EscapeCsv(campaignType.CustomName),
                            EscapeCsv(wave.Archetype?.Id),
                            EscapeCsv(wave.Archetype?.Name),
                            wave.ShipCount.ToString(CultureInfo.InvariantCulture),
                            wave.InitialDistance.ToString("F3", CultureInfo.InvariantCulture),
                            wave.AverageVelocity.ToString("F3", CultureInfo.InvariantCulture),
                            rcsRaw.ToString("F6", CultureInfo.InvariantCulture),
                            rcsModeAdjusted.ToString("F6", CultureInfo.InvariantCulture),
                            rcsDisplay.ToString("F6", CultureInfo.InvariantCulture),
                            wave.HasStealthCoating ? "1" : "0",
                            target.Acceleration.ToString("F6", CultureInfo.InvariantCulture),
                            target.Maneuverability.ToString("F6", CultureInfo.InvariantCulture),
                            target.Defense.ToString("F6", CultureInfo.InvariantCulture),
                            target.Mass.ToString("F3", CultureInfo.InvariantCulture),
                            target.FractureEnergy.ToString("F3", CultureInfo.InvariantCulture),
                            waveDistanceAu.ToString("F9", CultureInfo.InvariantCulture),
                            effectiveDetRangeAu.ToString("F6", CultureInfo.InvariantCulture),
                            det.IsDetected ? "1" : "0",
                            EscapeCsv(det.Quality.ToString()),
                            warningTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            minSafeTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            timeToImpactSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            timeToGunRangeBaseSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            timeToGunRangeTunedSeconds.ToString("F3", CultureInfo.InvariantCulture),
                            availableYearsRounded.ToString(CultureInfo.InvariantCulture),
                            tier.MaxEffectiveGunRange.ToString("F3", CultureInfo.InvariantCulture),
                            diffConfigFull.HitToleranceMultiplier.ToString("F6", CultureInfo.InvariantCulture),
                            GameModeTuning.Current.GetHitToleranceMultiplier(mode).ToString("F6", CultureInfo.InvariantCulture),
                            diffConfigFull.TargetRcsMultiplier.ToString("F6", CultureInfo.InvariantCulture),
                            hitToleranceMeters.ToString("F6", CultureInfo.InvariantCulture)
                        ));
                        rowCount++;
                    }
                }
            }

            string dir = string.IsNullOrWhiteSpace(outputDirectory)
                ? Directory.GetCurrentDirectory()
                : outputDirectory;

            if (string.IsNullOrWhiteSpace(dir))
                dir = Directory.GetCurrentDirectory();

            string fileName = $"EnemyCurve_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.GetFullPath(Path.Combine(dir, fileName));

            string? outDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);

            File.WriteAllText(fullPath, csv.ToString(), Encoding.UTF8);
            return new EnemyCurveResult(CsvPath: fullPath, RowCount: rowCount);
        }

        public static CounterCurveResult RunCounterCurveAndWriteCsv(string? outputDirectory = null, int samplesPerWave = 20)
        {
            GameConfigLoader.LoadIfExists();

            samplesPerWave = Math.Clamp(samplesPerWave, 1, 500);

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",",
                "ModeId",
                "ModeName",
                "Ruleset",
                "Difficulty",
                "WaveNumber",
                "TierIndex",
                "Sample",

                // Radar config
                "RadarLevel",
                "DetectionRangeMultiplier",
                "StealthPenetration",
                "IntelResolution",
                "MaxSimultaneousTargets",

                // Gun config
                "GunConfigId",
                "BarrelLength",
                "FireControlQuality",
                "GunRangeMultiplier_Barrel",

                // Projectile config
                "ProjectileConfigId",
                "ProjectileDefense",

                // Enemy actual
                "HasStealthCoating",
                "ShipCount",
                "Acceleration",
                "Maneuverability",
                "EnemyDefense",
                "EnemyOffense",
                "Mass",
                "FractureEnergy",

                // Wave/detection
                "InitialDistance",
                "AverageVelocity",
                "WaveDistanceAU",
                "EffectiveDetectionRangeAU",
                "Detected",
                "DetectionQuality",

                // Intel estimate (noisy)
                "IntelShipCountEstimate",
                "IntelStealthAssessment",
                "IntelManeuverabilityEstimate",
                "IntelDefenseEstimate",
                "IntelOffenseEstimate",

                // Derived counters
                "StealthRangeMultiplier",
                "EffectiveGunRangeMeters",
                "EvasionChance",
                "InterceptKillChance",

                // Derived baseline effectiveness (approx)
                "HitLikelihoodApprox_ConditionalDetected",
                "HitLikelihoodApprox_Unconditional"
            ));

            int rowCount = 0;
            var modes = GameModeCatalog.GetAll();

            // Fixed config sets to keep CSV size manageable.
            var gunConfigs = new (string Id, double BarrelLength, double FireControlQuality)[]
            {
                ("G0", 100.0, 1.0),
                ("G1", 130.0, 1.30),
                ("G2", 160.0, 1.80),
                ("G3", 200.0, 2.50),
            };

            var projectileConfigs = new (string Id, double Defense)[]
            {
                ("P0", 0.00),
                ("P1", 0.25),
                ("P2", 0.50),
            };

            foreach (var mode in modes)
            {
                if (mode.IsTutorial)
                    continue;

                var ruleset = mode.UsesEconomyAndDevelopment ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;
                string rulesetLabel = ruleset.ToString();

                var campaignRng = new Random(StableSeed($"{mode.Id}|campaign"));
                var campaignType = EnemyType.GenerateForCampaign(campaignRng);

                for (int waveNumber = 1; waveNumber <= 25; waveNumber++)
                {
                    var tier = GameConstants.GetTierForWave(waveNumber);
                    int tierIndex = tier.TierIndex;

                    for (int sample = 1; sample <= samplesPerWave; sample++)
                    {
                        var rng = new Random(StableSeed($"{mode.Id}|wave{waveNumber}|{sample}"));
                        var wave = EnemyWave.GenerateWave(waveNumber, rng, ruleset, campaignType);
                        var target = wave.Targets[0];

                        for (int radarLevel = 1; radarLevel <= 3; radarLevel++)
                        {
                            var detection = new DetectionSystem
                            {
                                DetectionRangeMultiplier = radarLevel switch { 1 => 1.0, 2 => 1.15, 3 => 1.30, _ => 1.30 },
                                MaxSimultaneousTargets = radarLevel switch { 1 => 5, 2 => 8, 3 => 12, _ => 12 },
                                StealthPenetration = radarLevel switch { 1 => 0.0, 2 => 0.40, 3 => 0.75, _ => 0.75 },
                                IntelResolution = radarLevel switch { 1 => 0.20, 2 => 0.50, 3 => 0.80, _ => 0.80 },
                            };

                            double waveDistanceAu = wave.CurrentDistance / GameConstants.AU_TO_METERS;
                            var det = detection.GetDetectionStatus(wave);
                            double effectiveDetRangeAu = detection.CalculateEffectiveDetectionRange(wave);

                            // Deterministic intel RNG so this row is stable.
                            var intelRng = new Random(StableSeed($"{mode.Id}|wave{waveNumber}|{sample}|radar{radarLevel}|intel"));
                            var intel = detection.GenerateNoisyIntelEstimate(wave, intelRng);

                            double stealthMult = detection.GetStealthRangeMultiplier(wave);

                            foreach (var gunCfg in gunConfigs)
                            {
                                double barrelMult = Math.Clamp(gunCfg.BarrelLength / 100.0, 0.5, 2.0);
                                double effectiveGunRange = tier.MaxEffectiveGunRange * barrelMult * stealthMult;

                                double evasionChance = CombatCurves.ComputeEvasionChance(target.Maneuverability, gunCfg.FireControlQuality);

                                foreach (var projCfg in projectileConfigs)
                                {
                                    double interceptChance = CombatCurves.ComputeInterceptKillChance(target.Offense, projCfg.Defense);

                                    double hitLikelihoodConditional = (1.0 - evasionChance) * (1.0 - interceptChance);
                                    hitLikelihoodConditional = Math.Clamp(hitLikelihoodConditional, 0.0, 1.0);

                                    double hitLikelihoodUnconditional = (det.IsDetected ? 1.0 : 0.0) * hitLikelihoodConditional;

                                    csv.AppendLine(string.Join(",",
                                        EscapeCsv(mode.Id.ToString()),
                                        EscapeCsv(mode.DisplayName),
                                        EscapeCsv(rulesetLabel),
                                        EscapeCsv(GameModeCatalog.GetDifficultyLabel(mode)),
                                        waveNumber.ToString(CultureInfo.InvariantCulture),
                                        tierIndex.ToString(CultureInfo.InvariantCulture),
                                        sample.ToString(CultureInfo.InvariantCulture),

                                        radarLevel.ToString(CultureInfo.InvariantCulture),
                                        detection.DetectionRangeMultiplier.ToString("F6", CultureInfo.InvariantCulture),
                                        detection.StealthPenetration.ToString("F6", CultureInfo.InvariantCulture),
                                        detection.IntelResolution.ToString("F6", CultureInfo.InvariantCulture),
                                        detection.MaxSimultaneousTargets.ToString(CultureInfo.InvariantCulture),

                                        EscapeCsv(gunCfg.Id),
                                        gunCfg.BarrelLength.ToString("F3", CultureInfo.InvariantCulture),
                                        gunCfg.FireControlQuality.ToString("F6", CultureInfo.InvariantCulture),
                                        barrelMult.ToString("F6", CultureInfo.InvariantCulture),

                                        EscapeCsv(projCfg.Id),
                                        projCfg.Defense.ToString("F6", CultureInfo.InvariantCulture),

                                        wave.HasStealthCoating ? "1" : "0",
                                        wave.ShipCount.ToString(CultureInfo.InvariantCulture),
                                        target.Acceleration.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Maneuverability.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Defense.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Offense.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Mass.ToString("F3", CultureInfo.InvariantCulture),
                                        target.FractureEnergy.ToString("F3", CultureInfo.InvariantCulture),

                                        wave.InitialDistance.ToString("F3", CultureInfo.InvariantCulture),
                                        wave.AverageVelocity.ToString("F3", CultureInfo.InvariantCulture),
                                        waveDistanceAu.ToString("F9", CultureInfo.InvariantCulture),
                                        effectiveDetRangeAu.ToString("F6", CultureInfo.InvariantCulture),
                                        det.IsDetected ? "1" : "0",
                                        EscapeCsv(det.Quality.ToString()),

                                        (intel.ShipCountEstimate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                                        EscapeCsv(intel.StealthAssessment),
                                        (intel.ManeuverabilityEstimate01?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty),
                                        (intel.DefenseEstimate01?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty),
                                        (intel.OffenseEstimate01?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty),

                                        stealthMult.ToString("F6", CultureInfo.InvariantCulture),
                                        effectiveGunRange.ToString("F3", CultureInfo.InvariantCulture),
                                        evasionChance.ToString("F6", CultureInfo.InvariantCulture),
                                        interceptChance.ToString("F6", CultureInfo.InvariantCulture),
                                        hitLikelihoodConditional.ToString("F6", CultureInfo.InvariantCulture),
                                        hitLikelihoodUnconditional.ToString("F6", CultureInfo.InvariantCulture)
                                    ));

                                    rowCount++;
                                }
                            }
                        }
                    }
                }
            }

            string dir = string.IsNullOrWhiteSpace(outputDirectory)
                ? Directory.GetCurrentDirectory()
                : outputDirectory;

            if (string.IsNullOrWhiteSpace(dir))
                dir = Directory.GetCurrentDirectory();

            string fileName = $"CounterCurve_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.GetFullPath(Path.Combine(dir, fileName));

            string? outDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);

            File.WriteAllText(fullPath, csv.ToString(), Encoding.UTF8);
            return new CounterCurveResult(CsvPath: fullPath, RowCount: rowCount);
        }

        public static EndToEndCurveResult RunEndToEndCurveAndWriteCsv(
            string? outputDirectory = null,
            int samplesPerWave = 10,
            int shotsPerRow = 200)
        {
            GameConfigLoader.LoadIfExists();

            samplesPerWave = Math.Clamp(samplesPerWave, 1, 200);
            shotsPerRow = Math.Clamp(shotsPerRow, 1, 5000);

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",",
                "ModeId",
                "ModeName",
                "Ruleset",
                "Difficulty",
                "WaveNumber",
                "TierIndex",
                "Sample",

                // Radar config
                "RadarLevel",
                "DetectionRangeMultiplier",
                "StealthPenetration",
                "IntelResolution",
                "MaxSimultaneousTargets",

                // Gun config
                "GunConfigId",
                "BarrelLength",
                "FireControlQuality",
                "GunRangeMultiplier_Barrel",

                // Projectile config
                "ProjectileConfigId",
                "ProjectileDefense",

                // Ballistics baseline
                "ProjectileMassKg",
                "MaxGunVelocityMs",
                "CorrectVelocityMs",
                "CorrectLaunchDelaySeconds",
                "KineticEnergyMJ",
                "FractureEnergyMJ",
                "EnergySufficient",

                // Enemy actual
                "HasStealthCoating",
                "ShipCount",
                "Acceleration",
                "Maneuverability",
                "EnemyDefense",
                "EnemyOffense",
                "Mass",

                // Detection
                "WaveDistanceAU",
                "EffectiveDetectionRangeAU",
                "Detected",
                "DetectionQuality",

                // Derived counters
                "StealthRangeMultiplier",
                "EffectiveGunRangeMeters",
                "EvasionChance",
                "InterceptKillChance",

                // End-to-end expected + observed
                "ExpectedHitRate_ConditionalDetected",
                "ExpectedHitRate_Unconditional",
                "Shots",
                "Hits",
                "ObservedHitRate"
            ));

            int rowCount = 0;
            var modes = GameModeCatalog.GetAll();

            // Fixed config sets to keep CSV size manageable.
            var gunConfigs = new (string Id, double BarrelLength, double FireControlQuality)[]
            {
                ("G0", 100.0, 1.0),
                ("G1", 130.0, 1.30),
                ("G2", 160.0, 1.80),
                ("G3", 200.0, 2.50),
            };

            var projectileConfigs = new (string Id, double Defense)[]
            {
                ("P0", 0.00),
                ("P1", 0.25),
                ("P2", 0.50),
            };

            const double projectileMassKg = 100.0;

            foreach (var mode in modes)
            {
                if (mode.IsTutorial)
                    continue;

                var ruleset = mode.UsesEconomyAndDevelopment ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;
                string rulesetLabel = ruleset.ToString();

                var campaignRng = new Random(StableSeed($"{mode.Id}|campaign"));
                var campaignType = EnemyType.GenerateForCampaign(campaignRng);

                for (int waveNumber = 1; waveNumber <= 25; waveNumber++)
                {
                    var tier = GameConstants.GetTierForWave(waveNumber);
                    int tierIndex = tier.TierIndex;
                    double maxGunVelocity = Math.Max(1.0, GameConstants.WeaponsTechBaseVelocity[^1]);

                    for (int sample = 1; sample <= samplesPerWave; sample++)
                    {
                        var rng = new Random(StableSeed($"{mode.Id}|wave{waveNumber}|{sample}"));
                        var wave = EnemyWave.GenerateWave(waveNumber, rng, ruleset, campaignType);
                        var target = wave.Targets[0];

                        for (int radarLevel = 1; radarLevel <= 3; radarLevel++)
                        {
                            var detection = new DetectionSystem
                            {
                                DetectionRangeMultiplier = radarLevel switch { 1 => 1.0, 2 => 1.15, 3 => 1.30, _ => 1.30 },
                                MaxSimultaneousTargets = radarLevel switch { 1 => 5, 2 => 8, 3 => 12, _ => 12 },
                                StealthPenetration = radarLevel switch { 1 => 0.0, 2 => 0.40, 3 => 0.75, _ => 0.75 },
                                IntelResolution = radarLevel switch { 1 => 0.20, 2 => 0.50, 3 => 0.80, _ => 0.80 },
                            };

                            double waveDistanceAu = wave.CurrentDistance / GameConstants.AU_TO_METERS;
                            var det = detection.GetDetectionStatus(wave);
                            double effectiveDetRangeAu = detection.CalculateEffectiveDetectionRange(wave);
                            double stealthMult = detection.GetStealthRangeMultiplier(wave);

                            foreach (var gunCfg in gunConfigs)
                            {
                                double barrelMult = Math.Clamp(gunCfg.BarrelLength / 100.0, 0.5, 2.0);
                                double effectiveGunRange = tier.MaxEffectiveGunRange * barrelMult * stealthMult;

                                // Generate a deterministic firing problem at this effective range.
                                // Note: this caches vectors onto the wave instance; we intentionally generate per config.
                                var problemRng = new Random(StableSeed($"{mode.Id}|wave{waveNumber}|{sample}|radar{radarLevel}|{gunCfg.Id}|problem"));
                                var calculator = new FiringSolution(
                                    projectileMass: (float)projectileMassKg,
                                    enemyFractureEnergy: (float)target.FractureEnergy,
                                    enemyMass: target.Mass);
                                var firingProblem = calculator.GenerateFiringProblem(
                                    wave,
                                    playerGunMaxVelocity: (float)maxGunVelocity,
                                    gunEffectiveRange: (float)effectiveGunRange,
                                    rng: problemRng);

                                double correctVelocity = firingProblem.CorrectVelocity;
                                double correctDelay = firingProblem.CorrectLaunchDelayTime;

                                double keMj = BallisticsCalculator.CalculateKineticEnergyMJ(projectileMassKg, correctVelocity);
                                bool energySufficient = keMj >= target.FractureEnergy;

                                double evasionChance = CombatCurves.ComputeEvasionChance(target.Maneuverability, gunCfg.FireControlQuality);

                                foreach (var projCfg in projectileConfigs)
                                {
                                    double interceptChance = CombatCurves.ComputeInterceptKillChance(target.Offense, projCfg.Defense);

                                    double expectedConditional = (energySufficient ? 1.0 : 0.0) * (1.0 - evasionChance) * (1.0 - interceptChance);
                                    expectedConditional = Math.Clamp(expectedConditional, 0.0, 1.0);

                                    double expectedUnconditional = (det.IsDetected ? 1.0 : 0.0) * expectedConditional;

                                    int hits = 0;
                                    var shotsRng = new Random(StableSeed($"{mode.Id}|wave{waveNumber}|{sample}|radar{radarLevel}|{gunCfg.Id}|{projCfg.Id}|shots"));

                                    for (int shot = 0; shot < shotsPerRow; shot++)
                                    {
                                        if (!det.IsDetected)
                                            continue;

                                        if (!energySufficient)
                                            continue;

                                        // Evasion then intercept, matching gameplay order.
                                        if (shotsRng.NextDouble() < evasionChance)
                                            continue;

                                        if (shotsRng.NextDouble() < interceptChance)
                                            continue;

                                        hits++;
                                    }

                                    double observed = shotsPerRow > 0 ? (double)hits / shotsPerRow : 0.0;

                                    csv.AppendLine(string.Join(",",
                                        EscapeCsv(mode.Id.ToString()),
                                        EscapeCsv(mode.DisplayName),
                                        EscapeCsv(rulesetLabel),
                                        EscapeCsv(GameModeCatalog.GetDifficultyLabel(mode)),
                                        waveNumber.ToString(CultureInfo.InvariantCulture),
                                        tierIndex.ToString(CultureInfo.InvariantCulture),
                                        sample.ToString(CultureInfo.InvariantCulture),

                                        radarLevel.ToString(CultureInfo.InvariantCulture),
                                        detection.DetectionRangeMultiplier.ToString("F6", CultureInfo.InvariantCulture),
                                        detection.StealthPenetration.ToString("F6", CultureInfo.InvariantCulture),
                                        detection.IntelResolution.ToString("F6", CultureInfo.InvariantCulture),
                                        detection.MaxSimultaneousTargets.ToString(CultureInfo.InvariantCulture),

                                        EscapeCsv(gunCfg.Id),
                                        gunCfg.BarrelLength.ToString("F3", CultureInfo.InvariantCulture),
                                        gunCfg.FireControlQuality.ToString("F6", CultureInfo.InvariantCulture),
                                        barrelMult.ToString("F6", CultureInfo.InvariantCulture),

                                        EscapeCsv(projCfg.Id),
                                        projCfg.Defense.ToString("F6", CultureInfo.InvariantCulture),

                                        projectileMassKg.ToString("F3", CultureInfo.InvariantCulture),
                                        maxGunVelocity.ToString("F3", CultureInfo.InvariantCulture),
                                        correctVelocity.ToString("F3", CultureInfo.InvariantCulture),
                                        correctDelay.ToString("F6", CultureInfo.InvariantCulture),
                                        keMj.ToString("F3", CultureInfo.InvariantCulture),
                                        target.FractureEnergy.ToString("F3", CultureInfo.InvariantCulture),
                                        energySufficient ? "1" : "0",

                                        wave.HasStealthCoating ? "1" : "0",
                                        wave.ShipCount.ToString(CultureInfo.InvariantCulture),
                                        target.Acceleration.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Maneuverability.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Defense.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Offense.ToString("F6", CultureInfo.InvariantCulture),
                                        target.Mass.ToString("F3", CultureInfo.InvariantCulture),

                                        waveDistanceAu.ToString("F9", CultureInfo.InvariantCulture),
                                        effectiveDetRangeAu.ToString("F6", CultureInfo.InvariantCulture),
                                        det.IsDetected ? "1" : "0",
                                        EscapeCsv(det.Quality.ToString()),

                                        stealthMult.ToString("F6", CultureInfo.InvariantCulture),
                                        effectiveGunRange.ToString("F3", CultureInfo.InvariantCulture),
                                        evasionChance.ToString("F6", CultureInfo.InvariantCulture),
                                        interceptChance.ToString("F6", CultureInfo.InvariantCulture),

                                        expectedConditional.ToString("F6", CultureInfo.InvariantCulture),
                                        expectedUnconditional.ToString("F6", CultureInfo.InvariantCulture),
                                        shotsPerRow.ToString(CultureInfo.InvariantCulture),
                                        hits.ToString(CultureInfo.InvariantCulture),
                                        observed.ToString("F6", CultureInfo.InvariantCulture)
                                    ));

                                    rowCount++;
                                }
                            }
                        }
                    }
                }
            }

            string dir = string.IsNullOrWhiteSpace(outputDirectory)
                ? Directory.GetCurrentDirectory()
                : outputDirectory;

            if (string.IsNullOrWhiteSpace(dir))
                dir = Directory.GetCurrentDirectory();

            string fileName = $"EndToEndCurve_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.GetFullPath(Path.Combine(dir, fileName));

            string? outDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);

            File.WriteAllText(fullPath, csv.ToString(), Encoding.UTF8);
            return new EndToEndCurveResult(CsvPath: fullPath, RowCount: rowCount);
        }

        private static readonly string[] s_defaultTuningLabEnergyReportCsvHeaders =
        [
            "RunUtc",
            "Ruleset",
            "Difficulty",
            "WeaponsTechLevel",
            "RadarLevel",
            "SamplesPerTier",
            "SmoothTierSampling",
            "BarrelLength_m",
            "Guidance",
            "MuzzleVelocityMult",
            "ProjectileMass_kg",
            "ProjectileDefense",
            "Penetration_x",
            "HitToleranceMult_x",
            "PropulsionDeltaV_ms",
            "PropulsionBurn_s",
            "PropulsionRefMass_kg",
            "EffectiveMaxGunVelocity_ms",
            "TierIndex",
            "EnemyMassTonsMin",
            "EnemyMassTonsMax",
            "EnemyDensityKgM3Min",
            "EnemyDensityKgM3Max",
            "EnemyBulkModulusGpaMin",
            "EnemyBulkModulusGpaMax",
            "EnemyFractureStrain",
            "EnemyMinVelocity_ms",
            "EnemyMaxVelocity_ms",
            "Samples",
            "Detected",
            "EnergySufficient",
            "CanHit",
            "BallisticsOk",
            "EnergyGated",
            "AvgEffectiveFractureEnergy_MJ",
            "AvgKineticEnergy_MJ",
            "AvgKeToFractureRatio",
            "AvgBaselineVelocity_ms",
        ];

        private static readonly string[] s_defaultTuningLabEnergyReportCsvHeaders_WithMissedButDetected =
        [
            "RunUtc",
            "Ruleset",
            "Difficulty",
            "WeaponsTechLevel",
            "RadarLevel",
            "SamplesPerTier",
            "SmoothTierSampling",
            "BarrelLength_m",
            "Guidance",
            "MuzzleVelocityMult",
            "ProjectileMass_kg",
            "ProjectileDefense",
            "Penetration_x",
            "HitToleranceMult_x",
            "PropulsionDeltaV_ms",
            "PropulsionBurn_s",
            "PropulsionRefMass_kg",
            "EffectiveMaxGunVelocity_ms",
            "TierIndex",
            "EnemyMassTonsMin",
            "EnemyMassTonsMax",
            "EnemyDensityKgM3Min",
            "EnemyDensityKgM3Max",
            "EnemyBulkModulusGpaMin",
            "EnemyBulkModulusGpaMax",
            "EnemyFractureStrain",
            "EnemyMinVelocity_ms",
            "EnemyMaxVelocity_ms",
            "Samples",
            "Detected",
            "EnergySufficient",
            "CanHit",
            "MissedButDetected",
            "BallisticsOk",
            "EnergyGated",
            "AvgEffectiveFractureEnergy_MJ",
            "AvgKineticEnergy_MJ",
            "AvgKeToFractureRatio",
            "AvgBaselineVelocity_ms",
        ];

        private static readonly string[] s_defaultTuningLabEnergyReportCsvOptionalHeaders =
        [
            // Not part of the historical default schema; available for explicit selection.
            "AvgTargetCrossSection_M2",
            "AvgTargetRadius_m",
        ];

        public static IReadOnlyList<string> GetDefaultTuningLabEnergyReportCsvHeaders(bool includeMissedButDetected = false)
            => includeMissedButDetected ? s_defaultTuningLabEnergyReportCsvHeaders_WithMissedButDetected : s_defaultTuningLabEnergyReportCsvHeaders;

        public static IReadOnlyList<string> GetTuningLabEnergyReportAvailableCsvHeaders()
            => s_defaultTuningLabEnergyReportCsvHeaders_WithMissedButDetected
                .Concat(s_defaultTuningLabEnergyReportCsvOptionalHeaders)
                .ToArray();

        public static string WriteTuningLabEnergyReportCsv(
            in TuningEnergyReportByTierResult result,
            bool includeMissedButDetected = false,
            IReadOnlyList<string>? headersOverride = null)
        {
            GameConfigLoader.LoadIfExists();

            var dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);

            // Copy the in-parameter so we can safely reference it inside local functions.
            // (C# forbids capturing 'in' parameters in local functions/lambdas.)
            var r = result;

            string fullPath = Path.GetFullPath(Path.Combine(dir, "TuningLab_EnergyReport.csv"));
            string runUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            static string Inv(double value, string format)
                => value.ToString(format, CultureInfo.InvariantCulture);

            static double SafeAt(double[] arr, int idx, double fallback = 0.0)
                => (arr is not null && idx >= 0 && idx < arr.Length) ? arr[idx] : fallback;

            var headers = (headersOverride is { Count: > 0 })
                ? headersOverride
                : GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected);
            string headerLine = string.Join(",", headers);

            int tierCount = new[]
            {
                r.SamplesByTier.Length,
                r.DetectedByTier.Length,
                r.EnergySufficientByTier.Length,
                r.CanHitByTier.Length,
                r.BallisticsOkByTier.Length,
                r.EnergyGatedByTier.Length,
                r.AvgEffectiveFractureEnergyMJByTier.Length,
                r.AvgKineticEnergyMJByTier.Length,
                r.AvgKeToFractureRatioByTier.Length,
                r.AvgBaselineVelocityMsByTier.Length,
                r.AvgTargetCrossSectionM2ByTier.Length,
                r.AvgTargetRadiusMByTier.Length,
            }.Min();

            var rows = new StringBuilder();
            for (int i = 0; i < tierCount; i++)
            {
                int missedButDetected = Math.Max(0, r.DetectedByTier[i] - r.CanHitByTier[i]);

                string GetValue(string header)
                {
                    return header switch
                    {
                        "RunUtc" => EscapeCsv(runUtc),
                        "Ruleset" => EscapeCsv(r.RulesetLabel),
                        "Difficulty" => EscapeCsv(r.DifficultyLabel),
                        "WeaponsTechLevel" => r.WeaponsTechLevel.ToString(CultureInfo.InvariantCulture),
                        "RadarLevel" => r.RadarLevel.ToString(CultureInfo.InvariantCulture),
                        "SamplesPerTier" => r.SamplesPerTier.ToString(CultureInfo.InvariantCulture),
                        "SmoothTierSampling" => r.SmoothTierSampling ? "1" : "0",
                        "BarrelLength_m" => Inv(r.BarrelLengthMeters, "F3"),
                        "Guidance" => Inv(r.FireControlQuality, "F6"),
                        "MuzzleVelocityMult" => Inv(r.MuzzleVelocityMultiplier, "F6"),
                        "ProjectileMass_kg" => Inv(r.ProjectileMassKg, "F3"),
                        "ProjectileDefense" => Inv(r.ProjectileDefense, "F6"),
                        "Penetration_x" => Inv(r.Penetration, "F6"),
                        "HitToleranceMult_x" => Inv(r.HitToleranceMultiplier, "F6"),
                        "PropulsionDeltaV_ms" => Inv(r.PropulsionDeltaVCapacityMs, "F3"),
                        "PropulsionBurn_s" => Inv(r.PropulsionBurnDurationSeconds, "F6"),
                        "PropulsionRefMass_kg" => Inv(r.PropulsionReferenceMassKg, "F3"),
                        "EffectiveMaxGunVelocity_ms" => Inv(r.EffectiveMaxGunVelocityMs, "F3"),
                        "TierIndex" => i.ToString(CultureInfo.InvariantCulture),
                        "EnemyMassTonsMin" => Inv(SafeAt(DevelopmentTuning.TierTargetMaterial.TierEnemyMassTonsMin, i), "F3"),
                        "EnemyMassTonsMax" => Inv(SafeAt(DevelopmentTuning.TierTargetMaterial.TierEnemyMassTonsMax, i), "F3"),
                        "EnemyDensityKgM3Min" => Inv(SafeAt(DevelopmentTuning.TierTargetMaterial.TierEnemyDensityKgM3Min, i), "F3"),
                        "EnemyDensityKgM3Max" => Inv(SafeAt(DevelopmentTuning.TierTargetMaterial.TierEnemyDensityKgM3Max, i), "F3"),
                        "EnemyBulkModulusGpaMin" => Inv(SafeAt(DevelopmentTuning.TierTargetMaterial.TierEnemyBulkModulusGpaMin, i), "F3"),
                        "EnemyBulkModulusGpaMax" => Inv(SafeAt(DevelopmentTuning.TierTargetMaterial.TierEnemyBulkModulusGpaMax, i), "F3"),
                        "EnemyFractureStrain" => Inv(DevelopmentTuning.TierTargetMaterial.FractureStrain, "F6"),
                        "EnemyMinVelocity_ms" => Inv(SafeAt(DevelopmentTuning.TierVelocity.TierEnemyMinVelocity, i), "F3"),
                        "EnemyMaxVelocity_ms" => Inv(SafeAt(DevelopmentTuning.TierVelocity.TierEnemyMaxVelocity, i), "F3"),
                        "Samples" => r.SamplesByTier[i].ToString(CultureInfo.InvariantCulture),
                        "Detected" => r.DetectedByTier[i].ToString(CultureInfo.InvariantCulture),
                        "EnergySufficient" => r.EnergySufficientByTier[i].ToString(CultureInfo.InvariantCulture),
                        "CanHit" => r.CanHitByTier[i].ToString(CultureInfo.InvariantCulture),
                        "MissedButDetected" => missedButDetected.ToString(CultureInfo.InvariantCulture),
                        "BallisticsOk" => r.BallisticsOkByTier[i].ToString(CultureInfo.InvariantCulture),
                        "EnergyGated" => r.EnergyGatedByTier[i].ToString(CultureInfo.InvariantCulture),
                        "AvgEffectiveFractureEnergy_MJ" => Inv(r.AvgEffectiveFractureEnergyMJByTier[i], "F3"),
                        "AvgKineticEnergy_MJ" => Inv(r.AvgKineticEnergyMJByTier[i], "F3"),
                        "AvgKeToFractureRatio" => Inv(r.AvgKeToFractureRatioByTier[i], "F6"),
                        "AvgBaselineVelocity_ms" => Inv(r.AvgBaselineVelocityMsByTier[i], "F3"),
                        "AvgTargetCrossSection_M2" => Inv(r.AvgTargetCrossSectionM2ByTier[i], "F6"),
                        "AvgTargetRadius_m" => Inv(r.AvgTargetRadiusMByTier[i], "F6"),
                        _ => string.Empty
                    };
                }

                var row = new string[headers.Count];
                for (int c = 0; c < headers.Count; c++)
                    row[c] = GetValue(headers[c]);

                rows.AppendLine(string.Join(",", row));
            }

            bool exists = File.Exists(fullPath);
            if (exists)
            {
                try
                {
                    using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
                    string? firstLine = reader.ReadLine();
                    if (!string.Equals(firstLine?.Trim(), headerLine, StringComparison.Ordinal))
                    {
                        string suffix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                        fullPath = Path.GetFullPath(Path.Combine(dir, $"TuningLab_EnergyReport_{suffix}.csv"));
                        exists = false;
                    }
                }
                catch
                {
                    // If we can't read the existing file for any reason, fall back to writing a fresh file.
                    string suffix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    fullPath = Path.GetFullPath(Path.Combine(dir, $"TuningLab_EnergyReport_{suffix}.csv"));
                    exists = false;
                }
            }

            string text = exists ? rows.ToString() : headerLine + Environment.NewLine + rows;

            using (var stream = new FileStream(fullPath, exists ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(text);
            }

            return fullPath;
        }

        public static TuningEnergyReportByTierResult ComputeTuningEnergyReportByTier(
            EnemyGenerationRuleset ruleset,
            GameDifficulty difficulty,
            int weaponsTechLevel,
            int radarLevel,
            bool overrideEnemyMass,
            double enemyMassKg,
            bool overrideEnemyFractureEnergy,
            double enemyFractureEnergy,
            bool overrideEnemyDensity,
            double enemyDensityGcm3,
            bool overrideEnemyMaterialStrength,
            double enemyBulkModulusGpa,
            bool overrideEnemyManeuverability,
            double enemyManeuverability,
            bool overrideEnemyOffense,
            double enemyOffense,
            bool overrideEnemyDefense,
            double enemyDefense,
            bool overrideBarrelLength,
            double barrelLength,
            bool overrideFireControlQuality,
            double fireControlQuality,
            bool overrideMuzzleVelocityMultiplier,
            double muzzleVelocityMultiplier,
            bool overrideProjectileMass,
            double projectileMassKg,
            bool overrideProjectileDefense,
            double projectileDefense,
            bool overridePenetration,
            double penetration,
            bool overrideHitToleranceMultiplier,
            double hitToleranceMultiplier,
            bool overridePropulsionDeltaV,
            double propulsionDeltaVCapacityMs,
            bool overridePropulsionBurnDuration,
            double propulsionBurnDurationSeconds,
            bool overridePropulsionReferenceMass,
            double propulsionReferenceMassKg,
            bool overrideEnemyVelocity = false,
            double enemyVelocityMs = 0.0,
            int samplesPerTier = 50,
            bool smoothTierSampling = true)
        {
            GameConfigLoader.LoadIfExists();

            samplesPerTier = Math.Clamp(samplesPerTier, 1, 1000);
            radarLevel = Math.Clamp(radarLevel, 1, 3);

            barrelLength = Math.Clamp(barrelLength, 50.0, 300.0);
            fireControlQuality = Math.Clamp(fireControlQuality, 0.25, 5.0);
            muzzleVelocityMultiplier = Math.Clamp(muzzleVelocityMultiplier, 0.25, 3.0);
            projectileMassKg = Math.Clamp(projectileMassKg, 10.0, 10_000.0);
            projectileDefense = Math.Clamp(projectileDefense, 0.0, 1.0);
            penetration = Math.Clamp(penetration, 0.10, 5.0);
            hitToleranceMultiplier = Math.Clamp(hitToleranceMultiplier, 0.10, 5.0);
            propulsionDeltaVCapacityMs = Math.Clamp(propulsionDeltaVCapacityMs, 0.0, 20000.0);
            propulsionBurnDurationSeconds = Math.Clamp(propulsionBurnDurationSeconds, 0.1, 120.0);
            propulsionReferenceMassKg = Math.Clamp(propulsionReferenceMassKg, 0.01, 2000.0);

            enemyMassKg = Math.Clamp(enemyMassKg, 0.01, 1e12);
            enemyFractureEnergy = Math.Clamp(enemyFractureEnergy, 0.0, 1e12);
            enemyVelocityMs = Math.Clamp(enemyVelocityMs, 0.0, 1e12);
            enemyDensityGcm3 = Math.Clamp(enemyDensityGcm3, 0.0, 20.0);
            enemyBulkModulusGpa = Math.Clamp(enemyBulkModulusGpa, 0.0, 2000.0);
            enemyManeuverability = Math.Clamp(enemyManeuverability, 0.0, 1.0);
            enemyOffense = Math.Clamp(enemyOffense, 0.0, 1.0);
            enemyDefense = Math.Clamp(enemyDefense, 0.0, 1.0);

            string rulesetLabel = ruleset.ToString();
            string difficultyLabel = difficulty switch
            {
                GameDifficulty.NuclearOption => "Easy",
                GameDifficulty.CometsAndAsteroids => "Hard",
                GameDifficulty.RealSpacegunSimulator => "Extreme",
                _ => difficulty.ToString()
            };

            int maxTech = Math.Max(1, WeaponTuning.WeaponsTechVelocityMultipliers.Length);
            weaponsTechLevel = Math.Clamp(weaponsTechLevel, 1, maxTech);

            const double defaultBarrelLength = 100.0;
            const double defaultFireControlQuality = 1.0;
            const double defaultMuzzleVelocityMultiplier = 1.0;
            const double defaultProjectileMassKg = 5000.0;
            const double defaultProjectileDefense = 0.0;
            const double defaultPenetration = 1.0;
            const double defaultHitToleranceMultiplier = 1.0;
            const double defaultPropulsionDeltaVCapacityMs = 0.0;
            const double defaultPropulsionBurnDurationSeconds = 1.0;
            const double defaultPropulsionReferenceMassKg = 5000.0;

            double effectiveBarrelLength = overrideBarrelLength ? barrelLength : defaultBarrelLength;
            double effectiveFireControl = overrideFireControlQuality ? fireControlQuality : defaultFireControlQuality;
            double effectiveMuzzleVelocityMult = overrideMuzzleVelocityMultiplier ? muzzleVelocityMultiplier : defaultMuzzleVelocityMultiplier;
            double effectiveProjectileMass = overrideProjectileMass ? projectileMassKg : defaultProjectileMassKg;
            double effectiveProjectileDefense = overrideProjectileDefense ? projectileDefense : defaultProjectileDefense;
            double effectivePenetration = overridePenetration ? penetration : defaultPenetration;
            double effectiveHitToleranceMultiplierBase = overrideHitToleranceMultiplier ? hitToleranceMultiplier : defaultHitToleranceMultiplier;
            double effectivePropulsionDeltaVCapacityMs = overridePropulsionDeltaV ? propulsionDeltaVCapacityMs : defaultPropulsionDeltaVCapacityMs;
            double effectivePropulsionBurnDurationSeconds = overridePropulsionBurnDuration ? propulsionBurnDurationSeconds : defaultPropulsionBurnDurationSeconds;
            double effectivePropulsionReferenceMassKg = overridePropulsionReferenceMass ? propulsionReferenceMassKg : defaultPropulsionReferenceMassKg;

            double baseImpactCoupling = DevelopmentTuning.ProjectileDefaults.ImpactCoupling;
            double couplingReferenceMassKg = Math.Max(0.01, DevelopmentTuning.ProjectileDefaults.ImpactCouplingReferenceMassKg);
            double couplingMassExponent = Math.Max(0.0, DevelopmentTuning.ProjectileDefaults.ImpactCouplingMassExponent);
            double couplingMassScale = couplingMassExponent > 0.0
                ? Math.Pow(couplingReferenceMassKg / Math.Max(0.01, effectiveProjectileMass), couplingMassExponent)
                : 1.0;
            double couplingTechPerLevel = Math.Max(0.0, DevelopmentTuning.ProjectileDefaults.ImpactCouplingTechMultiplierPerWeaponsLevel);
            double couplingTechScale = couplingTechPerLevel != 1.0
                ? Math.Pow(couplingTechPerLevel, Math.Max(0, weaponsTechLevel - 1))
                : 1.0;
            double impactCoupling = Math.Clamp(baseImpactCoupling * couplingMassScale * couplingTechScale, 0.0001, 100.0);

            double baseMaxGunVelocity = Math.Max(1.0, WeaponTuning.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel));
            double effectiveMaxGunVelocity = Math.Max(1.0, baseMaxGunVelocity * effectiveMuzzleVelocityMult);
            double barrelEfficiency = Math.Min(1.0, effectiveBarrelLength / 200.0);
            double barrelVelocityMultiplier = (0.5 + 0.5 * barrelEfficiency);
            effectiveMaxGunVelocity = Math.Max(1.0, effectiveMaxGunVelocity * barrelVelocityMultiplier);

            // Enemy density UI is in g/cm^3. 1 g/cm^3 = 1000 kg/m^3.
            double densityKgM3Override = Math.Max(0.0, enemyDensityGcm3) * 1000.0;
            bool applyDensity = overrideEnemyDensity && densityKgM3Override > 0.0;
            bool applyBulk = overrideEnemyMaterialStrength && enemyBulkModulusGpa > 0.0;

            var samplesByTier = new int[GameConstants.TierCount];
            var detectedByTier = new int[GameConstants.TierCount];
            var energySufficientByTier = new int[GameConstants.TierCount];
            var canHitByTier = new int[GameConstants.TierCount];
            var ballisticsOkByTier = new int[GameConstants.TierCount];
            var energyGatedByTier = new int[GameConstants.TierCount];

            var fractureSum = new double[GameConstants.TierCount];
            var keSum = new double[GameConstants.TierCount];
            var keToFractureSum = new double[GameConstants.TierCount];
            var baselineVelSum = new double[GameConstants.TierCount];

            var targetCrossSectionSum = new double[GameConstants.TierCount];
            var targetRadiusSum = new double[GameConstants.TierCount];

            var campaignRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|campaign"));
            var campaignType = EnemyType.GenerateForCampaign(campaignRng);

            void ProcessSample(int waveNumber, int sample)
            {
                var tier = GameConstants.GetTierForWave(waveNumber);
                int tierIndex = tier.TierIndex;
                samplesByTier[tierIndex]++;

                var rng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|wave{waveNumber}|{sample}"));
                var wave = EnemyWave.GenerateWave(waveNumber, rng, ruleset, campaignType);
                var target = wave.Targets[0];

                double tierMaxVelocity = GameConstants.TierEnemyMaxVelocity[Math.Clamp(tierIndex, 0, GameConstants.TierEnemyMaxVelocity.Length - 1)];
                wave.AverageVelocity = overrideEnemyVelocity ? enemyVelocityMs : tierMaxVelocity;

                if (wave.CachedEnemyVelocity.HasValue)
                {
                    var v = wave.CachedEnemyVelocity.Value;
                    double mag = v.Magnitude;
                    if (mag > 0)
                    {
                        double s = wave.AverageVelocity / mag;
                        wave.CachedEnemyVelocity = new Vector3(v.X * s, v.Y * s, v.Z * s);
                    }
                }

                double effectiveEnemyMassTons = overrideEnemyMass ? (enemyMassKg / 1000.0) : target.Mass;
                effectiveEnemyMassTons = Math.Clamp(effectiveEnemyMassTons, 0.000001, 1e12);

                double effectiveDensityKgM3 = applyDensity
                    ? densityKgM3Override
                    : (target.DensityKgM3 > 0.0 ? target.DensityKgM3 : 500.0);

                double effectiveBulkModulusGpa = applyBulk
                    ? enemyBulkModulusGpa
                    : (target.BulkModulusGpa > 0.0 ? target.BulkModulusGpa : 200.0);

                target.Mass = effectiveEnemyMassTons;
                target.DensityKgM3 = effectiveDensityKgM3;
                target.BulkModulusGpa = effectiveBulkModulusGpa;

                double derivedCrossSectionM2 = BallisticsCalculator.CalculateCrossSectionAreaM2FromMassAndDensity(
                    effectiveEnemyMassTons,
                    effectiveDensityKgM3);
                wave.AverageRadarCrossSection = derivedCrossSectionM2;
                target.CrossSection = derivedCrossSectionM2;

                double baseEnemyFractureEnergy = BallisticsCalculator.CalculateFractureEnergyMJFromMassDensityAndBulkModulus(
                    effectiveEnemyMassTons,
                    effectiveDensityKgM3,
                    effectiveBulkModulusGpa,
                    DevelopmentTuning.TierTargetMaterial.FractureStrain);
                target.FractureEnergy = baseEnemyFractureEnergy;

                double fractureMult = overrideEnemyFractureEnergy ? Math.Clamp(enemyFractureEnergy, 0.0, 10.0) : 1.0;
                double effectiveEnemyFractureRaw = baseEnemyFractureEnergy * fractureMult;

                var detection = new DetectionSystem
                {
                    DetectionRangeMultiplier = radarLevel switch { 1 => 1.0, 2 => 1.15, 3 => 1.30, _ => 1.30 },
                    MaxSimultaneousTargets = radarLevel switch { 1 => 5, 2 => 8, 3 => 12, _ => 12 },
                    StealthPenetration = radarLevel switch { 1 => 0.0, 2 => 0.40, 3 => 0.75, _ => 0.75 },
                    IntelResolution = radarLevel switch { 1 => 0.20, 2 => 0.50, 3 => 0.80, _ => 0.80 },
                };

                var det = detection.GetDetectionStatus(wave);
                if (!det.IsDetected)
                    return;
                detectedByTier[tierIndex]++;

                double targetCrossSectionM2 = Math.Max(0.0, target.CrossSection);
                double targetRadiusM = targetCrossSectionM2 > 0.0
                    ? Math.Sqrt(targetCrossSectionM2 / Math.PI)
                    : 0.0;
                targetCrossSectionSum[tierIndex] += targetCrossSectionM2;
                targetRadiusSum[tierIndex] += targetRadiusM;

                double stealthMult = detection.GetStealthRangeMultiplier(wave);
                double barrelMult = Math.Clamp(effectiveBarrelLength / 100.0, 0.5, 2.0);
                double effectiveGunRange = tier.MaxEffectiveGunRange * barrelMult * stealthMult;

                double effectiveEnemyDefense01 = Math.Clamp(overrideEnemyDefense ? enemyDefense : target.Defense, 0.0, 1.0);
                double defenseScale = Math.Max(0.0, GameModeTuning.Current.FractureEnergyDefenseScale);
                double armoredEnemyFracture = Math.Max(0.0, effectiveEnemyFractureRaw * (1.0 + defenseScale * effectiveEnemyDefense01));
                double effectiveEnemyFractureEnergy = Math.Max(0.0, armoredEnemyFracture / (Math.Max(0.1, effectivePenetration) * impactCoupling));

                double massEfficiency = effectivePropulsionReferenceMassKg / (effectivePropulsionReferenceMassKg + Math.Max(0.0, effectiveProjectileMass));
                double bestCaseDeltaV = effectivePropulsionDeltaVCapacityMs * massEfficiency;

                var calculator = new FiringSolution(
                    projectileMass: (float)effectiveProjectileMass,
                    enemyFractureEnergy: (float)effectiveEnemyFractureEnergy,
                    enemyMass: effectiveEnemyMassTons,
                    enemyCrossSectionM2: target.CrossSection);

                calculator.ConfigureProjectileModifiers(
                    additionalHitToleranceMultiplier: effectiveHitToleranceMultiplierBase,
                    propulsionDeltaVCapacityMs: effectivePropulsionDeltaVCapacityMs,
                    propulsionBurnDurationSeconds: effectivePropulsionBurnDurationSeconds,
                    propulsionReferenceMassKg: effectivePropulsionReferenceMassKg);

                double baselineVelocity;
                double baselineDelay;
                double baselineElev;
                double baselineAzim;
                Vector3 enemyPosition;
                Vector3 enemyVelocity;

                try
                {
                    var problemRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|wave{waveNumber}|{sample}|problem"));
                    var firingProblem = calculator.GenerateFiringProblem(
                        wave,
                        playerGunMaxVelocity: (float)effectiveMaxGunVelocity,
                        gunEffectiveRange: (float)effectiveGunRange,
                        rng: problemRng);

                    enemyPosition = firingProblem.EnemyPosition;
                    enemyVelocity = firingProblem.EnemyVelocity;

                    baselineDelay = firingProblem.CorrectLaunchDelayTime;
                    baselineElev = firingProblem.CorrectElevation;
                    baselineAzim = firingProblem.CorrectAzimuth;
                    baselineVelocity = firingProblem.CorrectVelocity;
                }
                catch
                {
                    baselineVelocity = effectiveMaxGunVelocity * 0.85;
                    baselineDelay = 0.0;
                    enemyPosition = wave.CachedEnemyPosition ?? new Vector3(wave.InitialDistance, 0.0, 0.0);
                    enemyVelocity = wave.CachedEnemyVelocity ?? (overrideEnemyVelocity
                        ? new Vector3(-wave.AverageVelocity, 0.0, 0.0)
                        : Vector3.Zero);
                    baselineElev = 0.0;
                    baselineAzim = 0.0;
                }

                var initialRes = calculator.CalculateSolution(
                    enemyPosition,
                    enemyVelocity,
                    baselineDelay,
                    baselineElev,
                    baselineAzim,
                    Math.Min(baselineVelocity, effectiveMaxGunVelocity),
                    (float)effectiveMaxGunVelocity,
                    (float)effectiveGunRange,
                    waveNumber,
                    effectiveEnemyMassTons,
                    difficulty);

                if (!(initialRes.CanHit && initialRes.CanDestroy))
                {
                    if (TryFindBaselineBallisticSolution(
                        calculator,
                        enemyPosition,
                        enemyVelocity,
                        effectiveMaxGunVelocity,
                        effectiveGunRange,
                        waveNumber,
                        effectiveEnemyMassTons,
                        difficulty,
                        requireDestroy: false,
                        out var baseline,
                        out _))
                    {
                        baselineDelay = baseline.DelaySeconds;
                        baselineElev = baseline.ElevDeg;
                        baselineAzim = baseline.AzimDeg;
                        baselineVelocity = baseline.VelocityMs;
                    }
                    else
                    {
                        baselineVelocity = effectiveMaxGunVelocity;
                        baselineDelay = 0.0;
                        baselineElev = 0.0;
                        baselineAzim = 0.0;
                    }
                }

                double cappedVelocity = Math.Min(baselineVelocity, effectiveMaxGunVelocity);

                double keMj = BallisticsCalculator.CalculateKineticEnergyMJ(effectiveProjectileMass, cappedVelocity + bestCaseDeltaV);
                fractureSum[tierIndex] += effectiveEnemyFractureEnergy;
                keSum[tierIndex] += keMj;
                baselineVelSum[tierIndex] += cappedVelocity;
                keToFractureSum[tierIndex] += effectiveEnemyFractureEnergy > 0 ? (keMj / effectiveEnemyFractureEnergy) : 0.0;

                var baseRes = calculator.CalculateSolution(
                    enemyPosition,
                    enemyVelocity,
                    baselineDelay,
                    baselineElev,
                    baselineAzim,
                    cappedVelocity,
                    (float)effectiveMaxGunVelocity,
                    (float)effectiveGunRange,
                    waveNumber,
                    effectiveEnemyMassTons,
                    difficulty);

                if (baseRes.CanDestroy)
                    energySufficientByTier[tierIndex]++;

                if (baseRes.CanHit)
                    canHitByTier[tierIndex]++;

                if (baseRes.CanHit && baseRes.CanDestroy)
                    ballisticsOkByTier[tierIndex]++;

                if (baseRes.CanHit && !baseRes.CanDestroy)
                    energyGatedByTier[tierIndex]++;
            }

            if (!smoothTierSampling)
            {
                for (int waveNumber = 1; waveNumber <= 25; waveNumber++)
                    for (int sample = 1; sample <= samplesPerTier; sample++)
                        ProcessSample(waveNumber, sample);
            }
            else
            {
                for (int tierIndex = 0; tierIndex < GameConstants.TierCount; tierIndex++)
                {
                    var tier = GameConstants.WaveTiers[tierIndex];
                    int waveCount = Math.Max(1, tier.EndWave - tier.StartWave + 1);
                    for (int sample = 1; sample <= samplesPerTier; sample++)
                    {
                        var pickRng = new Random(StableSeed($"tuning|{rulesetLabel}|{difficultyLabel}|tier{tierIndex}|pick{sample}"));
                        int waveNumber = tier.StartWave + pickRng.Next(0, waveCount);
                        ProcessSample(waveNumber, sample);
                    }
                }
            }

            var avgFracture = new double[GameConstants.TierCount];
            var avgKe = new double[GameConstants.TierCount];
            var avgRatio = new double[GameConstants.TierCount];
            var avgBaselineVel = new double[GameConstants.TierCount];
            var avgCrossSectionM2 = new double[GameConstants.TierCount];
            var avgRadiusM = new double[GameConstants.TierCount];

            for (int t = 0; t < GameConstants.TierCount; t++)
            {
                int denom = Math.Max(1, detectedByTier[t]);
                avgFracture[t] = fractureSum[t] / denom;
                avgKe[t] = keSum[t] / denom;
                avgRatio[t] = keToFractureSum[t] / denom;
                avgBaselineVel[t] = baselineVelSum[t] / denom;
                avgCrossSectionM2[t] = targetCrossSectionSum[t] / denom;
                avgRadiusM[t] = targetRadiusSum[t] / denom;
            }

            return new TuningEnergyReportByTierResult(
                RulesetLabel: rulesetLabel,
                DifficultyLabel: difficultyLabel,
                WeaponsTechLevel: weaponsTechLevel,
                RadarLevel: radarLevel,
                SamplesPerTier: samplesPerTier,
                SmoothTierSampling: smoothTierSampling,
                BarrelLengthMeters: effectiveBarrelLength,
                FireControlQuality: effectiveFireControl,
                MuzzleVelocityMultiplier: effectiveMuzzleVelocityMult,
                ProjectileMassKg: effectiveProjectileMass,
                ProjectileDefense: effectiveProjectileDefense,
                Penetration: effectivePenetration,
                HitToleranceMultiplier: effectiveHitToleranceMultiplierBase,
                PropulsionDeltaVCapacityMs: effectivePropulsionDeltaVCapacityMs,
                PropulsionBurnDurationSeconds: effectivePropulsionBurnDurationSeconds,
                PropulsionReferenceMassKg: effectivePropulsionReferenceMassKg,
                EffectiveMaxGunVelocityMs: effectiveMaxGunVelocity,
                SamplesByTier: samplesByTier,
                DetectedByTier: detectedByTier,
                EnergySufficientByTier: energySufficientByTier,
                CanHitByTier: canHitByTier,
                BallisticsOkByTier: ballisticsOkByTier,
                EnergyGatedByTier: energyGatedByTier,
                AvgEffectiveFractureEnergyMJByTier: avgFracture,
                AvgKineticEnergyMJByTier: avgKe,
                AvgKeToFractureRatioByTier: avgRatio,
                AvgBaselineVelocityMsByTier: avgBaselineVel,
                AvgTargetCrossSectionM2ByTier: avgCrossSectionM2,
                AvgTargetRadiusMByTier: avgRadiusM);
        }
    }
}
