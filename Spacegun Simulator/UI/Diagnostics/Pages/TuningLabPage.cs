using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Weapons;
using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Tests;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;
using System.Globalization;
using System.Text.Json;
using System.Text;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class TuningLabPage : PageBase
    {
        public override string Id => PageId.DiagnosticsTuningLab;
        public override string Title => "TUNING LAB";

        private enum Field
        {
            PresetSlot = -14,
            PresetName = -13,
            SavePreset = -12,
            LoadPreset = -11,

            CsvTemplateSlot = -10,

            EnergyCsvColumns = -4,

            Mode = 0,
            Difficulty = 1,
            WeaponsTechLevel = 2,
            RadarLevel = 3,
            SamplesPerWave = 4,
            ShotsPerSample = 5,
            SimulateAimError = 6,

            SmoothTierSampling = 24,

            BarrelLength = 7,
            Guidance = 8,
            MuzzleVelocityMult = 9,
            ProjectileMass = 10,
            ProjectileDefense = 11,
            Penetration = 12,
            HitToleranceMult = 13,
            PropulsionDeltaV = 14,
            PropulsionBurn = 15,
            PropulsionRefMass = 16,

            EnemyMass = 17,
            EnemyFractureEnergy = 18,
            EnemyVelocity = 19,
            EnemyDensity = 25,
            EnemyMaterialStrength = 26,
            EnemyManeuverability = 20,
            EnemyOffense = 21,
            EnemyDefense = 22,

            Run = 23,
        }

        private static readonly Field[] s_fieldOrder = new[]
        {
            Field.PresetSlot,
            Field.PresetName,
            Field.SavePreset,
            Field.LoadPreset,

            Field.CsvTemplateSlot,

            Field.EnergyCsvColumns,
            Field.Mode,
            Field.Difficulty,
            Field.WeaponsTechLevel,
            Field.RadarLevel,
            Field.SamplesPerWave,
            Field.ShotsPerSample,
            Field.SimulateAimError,
            Field.SmoothTierSampling,

            Field.BarrelLength,
            Field.Guidance,
            Field.MuzzleVelocityMult,
            Field.ProjectileMass,
            Field.ProjectileDefense,
            Field.Penetration,
            Field.HitToleranceMult,
            Field.PropulsionDeltaV,
            Field.PropulsionBurn,
            Field.PropulsionRefMass,

            Field.EnemyMass,
            Field.EnemyFractureEnergy,
            Field.EnemyVelocity,
            Field.EnemyDensity,
            Field.EnemyMaterialStrength,
            Field.EnemyManeuverability,
            Field.EnemyOffense,
            Field.EnemyDefense,

            Field.Run,
        };

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: false,
            ShowSidePanels: false,
            FooterHint: "↑/↓ Select  Digits/Letters=Type  Backspace=Edit  Space=Toggle  (S)avePreset  (L)oadPreset  ↩ Run+CSV  (R)un+CSV  (B)ack  (Q)uit"
        );

        private int _selectedIndex;
        private int _scroll;
        private string _inputBuffer = "";

        private int _rulesetIndex;
        private int _difficultyIndex;
        private int _weaponsTechLevel = 1;
        private int _radarLevel = 1;

        private int _presetSlotIndex;
        private PresetFile _presets = PresetFile.CreateEmpty();
        private readonly string[] _presetNameDraftBySlot = new string[10];

        private int _csvTemplateSlotIndex;
        private CsvTemplateFile _csvTemplates = CsvTemplateFile.CreateDefault();

        private bool _overrideEnemyMass;
        private double _enemyMassKg = 1_000_000.0;

        private bool _overrideEnemyFractureEnergy;
        // In TuningLab this is a multiplier applied to the wave's default fracture energy.
        private double _enemyFractureEnergy = 1.0;

        private bool _overrideEnemyVelocity;
        private double _enemyVelocityMs = 0.0;

        private bool _overrideEnemyDensity;
        // g/cm^3 (UI unit). 1 g/cm^3 = 1000 kg/m^3
        private double _enemyDensityGcm3 = 7.85;

        private bool _overrideEnemyMaterialStrength;
        // Isothermal bulk modulus in GPa.
        private double _enemyBulkModulusGpa = 200.0;

        private bool _overrideEnemyManeuverability;
        private double _enemyManeuverability = 1.0;

        private bool _overrideEnemyOffense;
        private double _enemyOffense = 1.0;

        private bool _overrideEnemyDefense;
        private double _enemyDefense = 1.0;

        private bool _overrideBarrelLength;
        private double _barrelLength = 100.0;

        private bool _overrideFireControlQuality;
        private double _fireControlQuality = 1.0;

        private bool _overrideMuzzleVelocityMultiplier;
        private double _muzzleVelocityMultiplier = 1.0;

        private bool _overrideProjectileMass;
        private double _projectileMassKg = 100.0;

        private bool _overrideProjectileDefense;
        private double _projectileDefense = 0.0;

        private bool _overridePenetration;
        private double _penetration = 1.0;

        private bool _overrideHitToleranceMultiplier;
        private double _hitToleranceMultiplier = 1.0;

        private bool _overridePropulsionDeltaV;
        private double _propulsionDeltaVCapacityMs = 0.0;

        private bool _overridePropulsionBurnDuration;
        private double _propulsionBurnDurationSeconds = 1.0;

        private bool _overridePropulsionReferenceMass;
        private double _propulsionReferenceMassKg = 10.0;

        private int _samplesPerWave = 5;
        private int _shotsPerSample = 200;

        private bool _simulateAimError;
        private bool _smoothTierSampling;

        private FireSimulatorDiagnostics.TuningCurveByTierResult? _last;
        private string? _lastError;
        private bool _resultsStale;
        private string? _statusMessage;

        private readonly List<string> _lines = new();
        private readonly Dictionary<int, int> _selectableLineIndex = new();

        private sealed record PresetFile(int Version, PresetSlot?[] Slots)
        {
            public static PresetFile CreateEmpty()
            {
                var slots = new PresetSlot?[10];
                return new PresetFile(Version: 1, Slots: slots);
            }
        }

        private sealed record CsvTemplateFile(int Version, int LastUsedSlotIndex, CsvTemplateSlot?[] Slots)
        {
            public static CsvTemplateFile CreateDefault()
            {
                var slots = new CsvTemplateSlot?[10];

                // Seed slot 1 with the current default schema so users have something to edit.
                slots[0] = new CsvTemplateSlot(
                    Name: "Default",
                    SavedUtc: DateTime.UtcNow,
                    Headers: FireSimulatorDiagnostics.GetDefaultTuningLabRunCsvHeaders().ToArray(),
                    EnergyHeaders: FireSimulatorDiagnostics.GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected: false).ToArray());

                return new CsvTemplateFile(Version: 1, LastUsedSlotIndex: 0, Slots: slots);
            }
        }

        private sealed record CsvTemplateSlot(string Name, DateTime SavedUtc, string[] Headers, string[]? EnergyHeaders = null);

        private sealed record PresetSlot(
            string Name,
            DateTime SavedUtc,
            int RulesetIndex,
            int DifficultyIndex,
            int RadarLevel,
            int SamplesPerWave,
            int ShotsPerSample,
            bool SimulateAimError,
            bool OverrideEnemyMass,
            double EnemyMassKg,
            bool OverrideEnemyFractureMult,
            double EnemyFractureMult,
            bool OverrideEnemyManeuverability,
            double EnemyManeuverability,
            bool OverrideEnemyOffense,
            double EnemyOffense,
            bool OverrideEnemyDefense,
            double EnemyDefense,
            bool OverrideBarrelLength,
            double BarrelLength,
            bool OverrideFireControlQuality,
            double FireControlQuality,
            bool OverrideMuzzleVelocityMultiplier,
            double MuzzleVelocityMultiplier,
            bool OverrideProjectileMass,
            double ProjectileMassKg,
            bool OverrideProjectileDefense,
            double ProjectileDefense,
            bool OverridePenetration,
            double Penetration,
            bool OverrideHitToleranceMultiplier,
            double HitToleranceMultiplier,
            bool OverridePropulsionDeltaV,
            double PropulsionDeltaVCapacityMs,
            bool OverridePropulsionBurnDuration,
            double PropulsionBurnDurationSeconds,
            bool OverridePropulsionReferenceMass,
            double PropulsionReferenceMassKg,
            int WeaponsTechLevel = 1,
            bool OverrideEnemyVelocity = false,
            double EnemyVelocityMs = 0.0,
            bool SmoothTierSampling = false,
            bool OverrideEnemyDensity = false,
            double EnemyDensityGcm3 = 7.85,
            bool OverrideEnemyMaterialStrength = false,
            double EnemyBulkModulusGpa = 200.0);

        private sealed record StateFile(
            int Version,
            int SelectedIndex,
            int RulesetIndex,
            int DifficultyIndex,
            int RadarLevel,
            int SamplesPerWave,
            int ShotsPerSample,
            bool SimulateAimError,
            bool OverrideEnemyMass,
            double EnemyMassKg,
            bool OverrideEnemyFractureMult,
            double EnemyFractureMult,
            bool OverrideEnemyManeuverability,
            double EnemyManeuverability,
            bool OverrideEnemyOffense,
            double EnemyOffense,
            bool OverrideEnemyDefense,
            double EnemyDefense,
            bool OverrideBarrelLength,
            double BarrelLength,
            bool OverrideFireControlQuality,
            double FireControlQuality,
            bool OverrideMuzzleVelocityMultiplier,
            double MuzzleVelocityMultiplier,
            bool OverrideProjectileMass,
            double ProjectileMassKg,
            bool OverrideProjectileDefense,
            double ProjectileDefense,
            bool OverridePenetration,
            double Penetration,
            bool OverrideHitToleranceMultiplier,
            double HitToleranceMultiplier,
            bool OverridePropulsionDeltaV,
            double PropulsionDeltaVCapacityMs,
            bool OverridePropulsionBurnDuration,
            double PropulsionBurnDurationSeconds,
            bool OverridePropulsionReferenceMass,
            double PropulsionReferenceMassKg,
            int PresetSlotIndex,
            int WeaponsTechLevel = 1,
            bool OverrideEnemyVelocity = false,
            double EnemyVelocityMs = 0.0,
            bool SmoothTierSampling = false,
            bool OverrideEnemyDensity = false,
            double EnemyDensityGcm3 = 7.85,
            bool OverrideEnemyMaterialStrength = false,
            double EnemyBulkModulusGpa = 200.0);

        private static string GetPresetsPath()
        {
            string dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(Path.Combine(dir, "TuningLab_Presets.json"));
        }

        private static string GetStatePath()
        {
            string dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(Path.Combine(dir, "TuningLab_State.json"));
        }

        private static string GetCsvTemplatesPath()
        {
            string dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(Path.Combine(dir, "TuningLab_CsvTemplates.json"));
        }

        private void LoadCsvTemplatesBestEffort(bool preserveSelectedSlotIndex)
        {
            try
            {
                int existingSlotIndex = Math.Clamp(_csvTemplateSlotIndex, 0, 9);

                string path = GetCsvTemplatesPath();
                if (!File.Exists(path))
                {
                    _csvTemplates = CsvTemplateFile.CreateDefault();
                    _csvTemplateSlotIndex = preserveSelectedSlotIndex
                        ? existingSlotIndex
                        : _csvTemplates.LastUsedSlotIndex;

                    SaveCsvTemplatesLastUsedSlotIndexBestEffort();
                    return;
                }

                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<CsvTemplateFile>(json);
                if (loaded?.Slots is null)
                {
                    _csvTemplates = CsvTemplateFile.CreateDefault();
                    _csvTemplateSlotIndex = preserveSelectedSlotIndex
                        ? existingSlotIndex
                        : _csvTemplates.LastUsedSlotIndex;
                    return;
                }

                var slots = new CsvTemplateSlot?[10];
                for (int i = 0; i < Math.Min(10, loaded.Slots.Length); i++)
                    slots[i] = loaded.Slots[i];

                _csvTemplates = new CsvTemplateFile(Version: 1, LastUsedSlotIndex: Math.Clamp(loaded.LastUsedSlotIndex, 0, 9), Slots: slots);

                _csvTemplateSlotIndex = preserveSelectedSlotIndex
                    ? existingSlotIndex
                    : _csvTemplates.LastUsedSlotIndex;
            }
            catch
            {
                _csvTemplates = CsvTemplateFile.CreateDefault();
                _csvTemplateSlotIndex = preserveSelectedSlotIndex
                    ? Math.Clamp(_csvTemplateSlotIndex, 0, 9)
                    : 0;
            }
        }

        private void SaveCsvTemplatesLastUsedSlotIndexBestEffort()
        {
            try
            {
                string path = GetCsvTemplatesPath();

                // IMPORTANT: This page only persists the selected slot index.
                // It must not overwrite template slot contents that may have been edited in
                // the CSV Columns page. So we always reload slot contents from disk before writing.
                CsvTemplateFile toSave;

                if (File.Exists(path))
                {
                    string jsonExisting = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<CsvTemplateFile>(jsonExisting);
                    if (loaded?.Slots is not null)
                    {
                        var slots = new CsvTemplateSlot?[10];
                        for (int i = 0; i < Math.Min(10, loaded.Slots.Length); i++)
                            slots[i] = loaded.Slots[i];
                        toSave = new CsvTemplateFile(Version: 1, LastUsedSlotIndex: Math.Clamp(_csvTemplateSlotIndex, 0, 9), Slots: slots);
                    }
                    else
                    {
                        toSave = _csvTemplates with { LastUsedSlotIndex = Math.Clamp(_csvTemplateSlotIndex, 0, 9) };
                    }
                }
                else
                {
                    toSave = _csvTemplates with { LastUsedSlotIndex = Math.Clamp(_csvTemplateSlotIndex, 0, 9) };
                }

                _csvTemplates = toSave;
                string json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void LoadPresetsBestEffort()
        {
            try
            {
                string path = GetPresetsPath();
                if (!File.Exists(path))
                {
                    _presets = PresetFile.CreateEmpty();
                    return;
                }

                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<PresetFile>(json);
                if (loaded?.Slots is null)
                {
                    _presets = PresetFile.CreateEmpty();
                    return;
                }

                var slots = new PresetSlot?[10];
                for (int i = 0; i < Math.Min(10, loaded.Slots.Length); i++)
                    slots[i] = loaded.Slots[i];

                _presets = new PresetFile(Version: 1, Slots: slots);

                for (int i = 0; i < _presetNameDraftBySlot.Length; i++)
                    _presetNameDraftBySlot[i] = _presets.Slots[i]?.Name ?? string.Empty;
            }
            catch
            {
                _presets = PresetFile.CreateEmpty();
                for (int i = 0; i < _presetNameDraftBySlot.Length; i++)
                    _presetNameDraftBySlot[i] = string.Empty;
            }
        }

        private void SavePresetsBestEffort()
        {
            try
            {
                string path = GetPresetsPath();
                string json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void SaveCurrentToPresetSlot(int slotIndex)
        {
            slotIndex = Math.Clamp(slotIndex, 0, 9);
            string name = _presetNameDraftBySlot[slotIndex];
            if (string.IsNullOrWhiteSpace(name))
                name = "(unnamed)";

            _presets.Slots[slotIndex] = new PresetSlot(
                Name: name,
                SavedUtc: DateTime.UtcNow,
                RulesetIndex: _rulesetIndex,
                DifficultyIndex: _difficultyIndex,
                WeaponsTechLevel: _weaponsTechLevel,
                RadarLevel: _radarLevel,
                SamplesPerWave: _samplesPerWave,
                ShotsPerSample: _shotsPerSample,
                SimulateAimError: _simulateAimError,
                OverrideEnemyMass: _overrideEnemyMass,
                EnemyMassKg: _enemyMassKg,
                OverrideEnemyFractureMult: _overrideEnemyFractureEnergy,
                EnemyFractureMult: _enemyFractureEnergy,
                OverrideEnemyVelocity: _overrideEnemyVelocity,
                EnemyVelocityMs: _enemyVelocityMs,
                OverrideEnemyDensity: _overrideEnemyDensity,
                EnemyDensityGcm3: _enemyDensityGcm3,
                OverrideEnemyMaterialStrength: _overrideEnemyMaterialStrength,
                EnemyBulkModulusGpa: _enemyBulkModulusGpa,
                OverrideEnemyManeuverability: _overrideEnemyManeuverability,
                EnemyManeuverability: _enemyManeuverability,
                OverrideEnemyOffense: _overrideEnemyOffense,
                EnemyOffense: _enemyOffense,
                OverrideEnemyDefense: _overrideEnemyDefense,
                EnemyDefense: _enemyDefense,
                OverrideBarrelLength: _overrideBarrelLength,
                BarrelLength: _barrelLength,
                OverrideFireControlQuality: _overrideFireControlQuality,
                FireControlQuality: _fireControlQuality,
                OverrideMuzzleVelocityMultiplier: _overrideMuzzleVelocityMultiplier,
                MuzzleVelocityMultiplier: _muzzleVelocityMultiplier,
                OverrideProjectileMass: _overrideProjectileMass,
                ProjectileMassKg: _projectileMassKg,
                OverrideProjectileDefense: _overrideProjectileDefense,
                ProjectileDefense: _projectileDefense,
                OverridePenetration: _overridePenetration,
                Penetration: _penetration,
                OverrideHitToleranceMultiplier: _overrideHitToleranceMultiplier,
                HitToleranceMultiplier: _hitToleranceMultiplier,
                OverridePropulsionDeltaV: _overridePropulsionDeltaV,
                PropulsionDeltaVCapacityMs: _propulsionDeltaVCapacityMs,
                OverridePropulsionBurnDuration: _overridePropulsionBurnDuration,
                PropulsionBurnDurationSeconds: _propulsionBurnDurationSeconds,
                OverridePropulsionReferenceMass: _overridePropulsionReferenceMass,
                PropulsionReferenceMassKg: _propulsionReferenceMassKg,
                SmoothTierSampling: _smoothTierSampling);

            SavePresetsBestEffort();
        }

        private void LoadPresetSlot(int slotIndex)
        {
            slotIndex = Math.Clamp(slotIndex, 0, 9);
            var slot = _presets.Slots[slotIndex];
            if (slot is null)
            {
                _statusMessage = $"Preset slot {slotIndex + 1} is empty.";
                return;
            }

            _rulesetIndex = slot.RulesetIndex;
            _difficultyIndex = slot.DifficultyIndex;
            _weaponsTechLevel = slot.WeaponsTechLevel;
            _radarLevel = slot.RadarLevel;
            _samplesPerWave = slot.SamplesPerWave;
            _shotsPerSample = slot.ShotsPerSample;
            _simulateAimError = slot.SimulateAimError;
            _smoothTierSampling = slot.SmoothTierSampling;

            _overrideEnemyMass = slot.OverrideEnemyMass;
            _enemyMassKg = slot.EnemyMassKg;
            _overrideEnemyFractureEnergy = slot.OverrideEnemyFractureMult;
            _enemyFractureEnergy = slot.EnemyFractureMult;
            _overrideEnemyVelocity = slot.OverrideEnemyVelocity;
            _enemyVelocityMs = slot.EnemyVelocityMs;
            _overrideEnemyDensity = slot.OverrideEnemyDensity;
            _enemyDensityGcm3 = slot.EnemyDensityGcm3;
            _overrideEnemyMaterialStrength = slot.OverrideEnemyMaterialStrength;
            _enemyBulkModulusGpa = slot.EnemyBulkModulusGpa;
            _overrideEnemyManeuverability = slot.OverrideEnemyManeuverability;
            _enemyManeuverability = slot.EnemyManeuverability;
            _overrideEnemyOffense = slot.OverrideEnemyOffense;
            _enemyOffense = slot.EnemyOffense;
            _overrideEnemyDefense = slot.OverrideEnemyDefense;
            _enemyDefense = slot.EnemyDefense;

            _overrideBarrelLength = slot.OverrideBarrelLength;
            _barrelLength = slot.BarrelLength;
            _overrideFireControlQuality = slot.OverrideFireControlQuality;
            _fireControlQuality = slot.FireControlQuality;
            _overrideMuzzleVelocityMultiplier = slot.OverrideMuzzleVelocityMultiplier;
            _muzzleVelocityMultiplier = slot.MuzzleVelocityMultiplier;
            _overrideProjectileMass = slot.OverrideProjectileMass;
            _projectileMassKg = slot.ProjectileMassKg;
            _overrideProjectileDefense = slot.OverrideProjectileDefense;
            _projectileDefense = slot.ProjectileDefense;
            _overridePenetration = slot.OverridePenetration;
            _penetration = slot.Penetration;
            _overrideHitToleranceMultiplier = slot.OverrideHitToleranceMultiplier;
            _hitToleranceMultiplier = slot.HitToleranceMultiplier;
            _overridePropulsionDeltaV = slot.OverridePropulsionDeltaV;
            _propulsionDeltaVCapacityMs = slot.PropulsionDeltaVCapacityMs;
            _overridePropulsionBurnDuration = slot.OverridePropulsionBurnDuration;
            _propulsionBurnDurationSeconds = slot.PropulsionBurnDurationSeconds;
            _overridePropulsionReferenceMass = slot.OverridePropulsionReferenceMass;
            _propulsionReferenceMassKg = slot.PropulsionReferenceMassKg;

            _statusMessage = $"Loaded preset slot {slotIndex + 1}. Press ↩ or (R)un to compute.";
            _resultsStale = _last is not null;
            _lastError = null;

            SaveStateBestEffort();
        }

        private void LoadStateBestEffort()
        {
            try
            {
                string path = GetStatePath();
                if (!File.Exists(path))
                    return;

                string json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<StateFile>(json);
                if (s is null)
                    return;

                _selectedIndex = Math.Clamp(s.SelectedIndex, 0, s_fieldOrder.Length - 1);
                _rulesetIndex = s.RulesetIndex;
                _difficultyIndex = s.DifficultyIndex;
                _weaponsTechLevel = s.WeaponsTechLevel;
                _radarLevel = s.RadarLevel;
                _samplesPerWave = s.SamplesPerWave;
                _shotsPerSample = s.ShotsPerSample;
                _simulateAimError = s.SimulateAimError;
                _smoothTierSampling = s.SmoothTierSampling;

                _overrideEnemyMass = s.OverrideEnemyMass;
                _enemyMassKg = s.EnemyMassKg;
                _overrideEnemyFractureEnergy = s.OverrideEnemyFractureMult;
                _enemyFractureEnergy = s.EnemyFractureMult;
                _overrideEnemyVelocity = s.OverrideEnemyVelocity;
                _enemyVelocityMs = s.EnemyVelocityMs;
                _overrideEnemyDensity = s.OverrideEnemyDensity;
                _enemyDensityGcm3 = s.EnemyDensityGcm3;
                _overrideEnemyMaterialStrength = s.OverrideEnemyMaterialStrength;
                _enemyBulkModulusGpa = s.EnemyBulkModulusGpa;
                _overrideEnemyManeuverability = s.OverrideEnemyManeuverability;
                _enemyManeuverability = s.EnemyManeuverability;
                _overrideEnemyOffense = s.OverrideEnemyOffense;
                _enemyOffense = s.EnemyOffense;
                _overrideEnemyDefense = s.OverrideEnemyDefense;
                _enemyDefense = s.EnemyDefense;

                _overrideBarrelLength = s.OverrideBarrelLength;
                _barrelLength = s.BarrelLength;
                _overrideFireControlQuality = s.OverrideFireControlQuality;
                _fireControlQuality = s.FireControlQuality;
                _overrideMuzzleVelocityMultiplier = s.OverrideMuzzleVelocityMultiplier;
                _muzzleVelocityMultiplier = s.MuzzleVelocityMultiplier;
                _overrideProjectileMass = s.OverrideProjectileMass;
                _projectileMassKg = s.ProjectileMassKg;
                _overrideProjectileDefense = s.OverrideProjectileDefense;
                _projectileDefense = s.ProjectileDefense;
                _overridePenetration = s.OverridePenetration;
                _penetration = s.Penetration;
                _overrideHitToleranceMultiplier = s.OverrideHitToleranceMultiplier;
                _hitToleranceMultiplier = s.HitToleranceMultiplier;
                _overridePropulsionDeltaV = s.OverridePropulsionDeltaV;
                _propulsionDeltaVCapacityMs = s.PropulsionDeltaVCapacityMs;
                _overridePropulsionBurnDuration = s.OverridePropulsionBurnDuration;
                _propulsionBurnDurationSeconds = s.PropulsionBurnDurationSeconds;
                _overridePropulsionReferenceMass = s.OverridePropulsionReferenceMass;
                _propulsionReferenceMassKg = s.PropulsionReferenceMassKg;

                _presetSlotIndex = Math.Clamp(s.PresetSlotIndex, 0, 9);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void SaveStateBestEffort()
        {
            try
            {
                var s = new StateFile(
                    Version: 1,
                    SelectedIndex: _selectedIndex,
                    RulesetIndex: _rulesetIndex,
                    DifficultyIndex: _difficultyIndex,
                    WeaponsTechLevel: _weaponsTechLevel,
                    RadarLevel: _radarLevel,
                    SamplesPerWave: _samplesPerWave,
                    ShotsPerSample: _shotsPerSample,
                    SimulateAimError: _simulateAimError,
                    SmoothTierSampling: _smoothTierSampling,
                    OverrideEnemyMass: _overrideEnemyMass,
                    EnemyMassKg: _enemyMassKg,
                    OverrideEnemyFractureMult: _overrideEnemyFractureEnergy,
                    EnemyFractureMult: _enemyFractureEnergy,
                    OverrideEnemyVelocity: _overrideEnemyVelocity,
                    EnemyVelocityMs: _enemyVelocityMs,
                    OverrideEnemyDensity: _overrideEnemyDensity,
                    EnemyDensityGcm3: _enemyDensityGcm3,
                    OverrideEnemyMaterialStrength: _overrideEnemyMaterialStrength,
                    EnemyBulkModulusGpa: _enemyBulkModulusGpa,
                    OverrideEnemyManeuverability: _overrideEnemyManeuverability,
                    EnemyManeuverability: _enemyManeuverability,
                    OverrideEnemyOffense: _overrideEnemyOffense,
                    EnemyOffense: _enemyOffense,
                    OverrideEnemyDefense: _overrideEnemyDefense,
                    EnemyDefense: _enemyDefense,
                    OverrideBarrelLength: _overrideBarrelLength,
                    BarrelLength: _barrelLength,
                    OverrideFireControlQuality: _overrideFireControlQuality,
                    FireControlQuality: _fireControlQuality,
                    OverrideMuzzleVelocityMultiplier: _overrideMuzzleVelocityMultiplier,
                    MuzzleVelocityMultiplier: _muzzleVelocityMultiplier,
                    OverrideProjectileMass: _overrideProjectileMass,
                    ProjectileMassKg: _projectileMassKg,
                    OverrideProjectileDefense: _overrideProjectileDefense,
                    ProjectileDefense: _projectileDefense,
                    OverridePenetration: _overridePenetration,
                    Penetration: _penetration,
                    OverrideHitToleranceMultiplier: _overrideHitToleranceMultiplier,
                    HitToleranceMultiplier: _hitToleranceMultiplier,
                    OverridePropulsionDeltaV: _overridePropulsionDeltaV,
                    PropulsionDeltaVCapacityMs: _propulsionDeltaVCapacityMs,
                    OverridePropulsionBurnDuration: _overridePropulsionBurnDuration,
                    PropulsionBurnDurationSeconds: _propulsionBurnDurationSeconds,
                    OverridePropulsionReferenceMass: _overridePropulsionReferenceMass,
                    PropulsionReferenceMassKg: _propulsionReferenceMassKg,
                    PresetSlotIndex: _presetSlotIndex);

                string path = GetStatePath();
                string json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static readonly string[] s_rulesets = new[] { "Full", "Pure" };

        private static readonly GameDifficulty[] s_difficulties = new[]
        {
            GameDifficulty.NuclearOption,
            GameDifficulty.CometsAndAsteroids,
            GameDifficulty.RealSpacegunSimulator
        };

        private static string GetDifficultyLabel(GameDifficulty difficulty)
            => difficulty switch
            {
                GameDifficulty.NuclearOption => "Easy",
                GameDifficulty.CometsAndAsteroids => "Hard",
                GameDifficulty.RealSpacegunSimulator => "Extreme",
                _ => difficulty.ToString()
            };

        private const double DefaultBarrelLength = 100.0;
        private const double DefaultFireControlQuality = 1.0;
        private const double DefaultMuzzleVelocityMultiplier = 1.0;
        private const double DefaultProjectileMassKg = 5000.0;
        private const double DefaultProjectileDefense = 0.0;
        private const double DefaultPenetration = 1.0;
        private const double DefaultHitToleranceMultiplier = 1.0;
        private const double DefaultPropulsionDeltaVCapacityMs = 0.0;
        private const double DefaultPropulsionBurnDurationSeconds = 1.0;
        private const double DefaultPropulsionReferenceMassKg = 5000.0;

        public override void OnEnter(UiContext ui)
        {
            _selectedIndex = 0;
            _scroll = 0;
            _inputBuffer = "";
            _last = null;
            _lastError = null;
            _resultsStale = false;
            _statusMessage = "Press ↩ or (R)un to compute.";

            // Load config early so UI defaults reflect GameConfig values.
            GameConfigLoader.LoadIfExists();

            LoadPresetsBestEffort();
            LoadStateBestEffort();
            LoadCsvTemplatesBestEffort(preserveSelectedSlotIndex: false);

            // If the user hasn't set a custom value yet, keep the field in sync with config.
            if (!_overrideMuzzleVelocityMultiplier && Math.Abs(_muzzleVelocityMultiplier - 1.0) < 1e-9)
                _muzzleVelocityMultiplier = GameConstants.MuzzleVelocityMultiplier;

            BuildLines();
        }

        private static string Sparkline01(double[] values)
        {
            if (values.Length == 0)
                return string.Empty;

            const string blocks = "▁▂▃▄▅▆▇█";
            var sb = new System.Text.StringBuilder(values.Length);
            foreach (var v in values)
            {
                double clamped = Math.Clamp(v, 0.0, 1.0);
                int idx = (int)Math.Round(clamped * (blocks.Length - 1));
                idx = Math.Clamp(idx, 0, blocks.Length - 1);
                sb.Append(blocks[idx]);
            }
            return sb.ToString();
        }

        private static string ClampToWidth(string text, int width)
        {
            text ??= "";
            return text.Length > width ? text.Substring(0, width) : text;
        }

        private Field SelectedField
            => s_fieldOrder[Math.Clamp(_selectedIndex, 0, s_fieldOrder.Length - 1)];

        private bool IsSelected(Field f) => f == SelectedField;

        private void MoveSelection(int dir)
        {
            int next = (_selectedIndex + dir) % s_fieldOrder.Length;
            if (next < 0) next += s_fieldOrder.Length;
            _selectedIndex = next;
            _inputBuffer = "";
        }

        private void EnsureSelectionVisible(int viewportHeight)
        {
            if (!_selectableLineIndex.TryGetValue(_selectedIndex, out int lineIndex))
                return;

            int maxScroll = Math.Max(0, _lines.Count - Math.Max(0, viewportHeight));
            if (lineIndex < _scroll)
                _scroll = lineIndex;
            else if (lineIndex >= _scroll + viewportHeight)
                _scroll = lineIndex - viewportHeight + 1;
            _scroll = Math.Clamp(_scroll, 0, maxScroll);
        }

        private string Cursor(Field f) => IsSelected(f) ? ">" : " ";

        private static string ToggleTag(bool isCustom, string defaultLabel = "Default", string customLabel = "Custom")
            => isCustom ? $"[{customLabel}]" : $"[{defaultLabel}]";

        private string FormatNumericRow(Field f, string label, bool isCustom, string value)
        {
            string shown = (IsSelected(f) && !string.IsNullOrWhiteSpace(_inputBuffer)) ? _inputBuffer : value;
            return $"{Cursor(f)} {label,-22} {ToggleTag(isCustom),-9} {value,-12} > {shown}";
        }

        private string FormatTextRow(Field f, string label, string value)
        {
            string shown = (IsSelected(f) && !string.IsNullOrWhiteSpace(_inputBuffer)) ? _inputBuffer : value;
            return $"{Cursor(f)} {label,-22} {shown}";
        }

        private string FormatPlainRow(Field f, string label, string value, string? editHint = null)
        {
            string hint = string.IsNullOrWhiteSpace(editHint) ? "" : $"  ({editHint})";
            return $"{Cursor(f)} {label,-22} {value}{hint}";
        }

        private string FormatEditablePlainRow(Field f, string label, string value, string? editHint = null)
        {
            string shown = (IsSelected(f) && !string.IsNullOrWhiteSpace(_inputBuffer)) ? _inputBuffer : value;
            string hint = string.IsNullOrWhiteSpace(editHint) ? "" : $"  ({editHint})";
            return $"{Cursor(f)} {label,-22} {shown}{hint}";
        }

        private void BuildLines()
        {
            _lines.Clear();
            _selectableLineIndex.Clear();

            string rulesetLabel = s_rulesets[Math.Clamp(_rulesetIndex, 0, s_rulesets.Length - 1)];
            var diff = s_difficulties[Math.Clamp(_difficultyIndex, 0, s_difficulties.Length - 1)];

            double barrelLen = _overrideBarrelLength ? _barrelLength : DefaultBarrelLength;
            double fireControl = _overrideFireControlQuality ? _fireControlQuality : DefaultFireControlQuality;
            double muzzleMult = _overrideMuzzleVelocityMultiplier ? _muzzleVelocityMultiplier : GameConstants.MuzzleVelocityMultiplier;
            double projMass = _overrideProjectileMass ? _projectileMassKg : DefaultProjectileMassKg;
            double projDef = _overrideProjectileDefense ? _projectileDefense : DefaultProjectileDefense;
            double penetration = _overridePenetration ? _penetration : DefaultPenetration;
            double hitTolMult = _overrideHitToleranceMultiplier ? _hitToleranceMultiplier : DefaultHitToleranceMultiplier;
            double deltaVCap = _overridePropulsionDeltaV ? _propulsionDeltaVCapacityMs : DefaultPropulsionDeltaVCapacityMs;
            double burnSec = _overridePropulsionBurnDuration ? _propulsionBurnDurationSeconds : DefaultPropulsionBurnDurationSeconds;
            double refMass = _overridePropulsionReferenceMass ? _propulsionReferenceMassKg : DefaultPropulsionReferenceMassKg;

            _lines.Add("Tune baseline parameters and visualize the curve.");
            _lines.Add("(Fast Monte Carlo; no spreadsheets.)");
            _lines.Add("");

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                _lines.Add(_statusMessage!);
                _lines.Add("");
            }

            void AddSelectable(Field f, string line)
            {
                int idx = Array.IndexOf(s_fieldOrder, f);
                if (idx >= 0)
                    _selectableLineIndex[idx] = _lines.Count;
                _lines.Add(line);
            }

            _lines.Add("== Presets ==");
            string slotLabel = _presets.Slots[_presetSlotIndex]?.Name ?? string.Empty;
            slotLabel = string.IsNullOrWhiteSpace(slotLabel) ? "(empty)" : slotLabel;
            AddSelectable(Field.PresetSlot, FormatPlainRow(Field.PresetSlot, "Preset Slot", $"{_presetSlotIndex + 1}/10 {slotLabel}", editHint: "←/→"));
            string nameDraft = _presetNameDraftBySlot[_presetSlotIndex] ?? string.Empty;
            AddSelectable(Field.PresetName, FormatTextRow(Field.PresetName, "Preset Name", nameDraft));
            AddSelectable(Field.SavePreset, FormatPlainRow(Field.SavePreset, "Save Preset to Slot", "", editHint: "S / Enter"));
            AddSelectable(Field.LoadPreset, FormatPlainRow(Field.LoadPreset, "Load Preset from Slot", "", editHint: "L / Enter"));
            _lines.Add("");

            _lines.Add("== Report Template (CSV) ==");
            string csvSlotLabel = _csvTemplates.Slots[_csvTemplateSlotIndex]?.Name ?? string.Empty;
            csvSlotLabel = string.IsNullOrWhiteSpace(csvSlotLabel) ? "(empty)" : csvSlotLabel;
            AddSelectable(Field.CsvTemplateSlot, FormatPlainRow(Field.CsvTemplateSlot, "Template Slot", $"{_csvTemplateSlotIndex + 1}/10 {csvSlotLabel}", editHint: "←/→"));
            AddSelectable(Field.EnergyCsvColumns, FormatPlainRow(Field.EnergyCsvColumns, "Edit Report Columns", "", editHint: "Enter"));
            _lines.Add("");

            AddSelectable(Field.Mode, FormatPlainRow(Field.Mode, "Mode", rulesetLabel, editHint: "←/→"));
            AddSelectable(Field.Difficulty, FormatPlainRow(Field.Difficulty, "Difficulty", GetDifficultyLabel(diff), editHint: "←/→"));
            AddSelectable(Field.WeaponsTechLevel, FormatEditablePlainRow(Field.WeaponsTechLevel, "Weapons Tech Level", _weaponsTechLevel.ToString(CultureInfo.InvariantCulture), editHint: "←/→ or type"));
            AddSelectable(Field.RadarLevel, FormatEditablePlainRow(Field.RadarLevel, "Radar Level", _radarLevel.ToString(), editHint: "type"));
            AddSelectable(Field.SamplesPerWave, FormatEditablePlainRow(Field.SamplesPerWave, "Samples / Wave", _samplesPerWave.ToString(), editHint: "type"));
            AddSelectable(Field.ShotsPerSample, FormatEditablePlainRow(Field.ShotsPerSample, "Shots / Sample", _shotsPerSample.ToString(), editHint: "type"));
            AddSelectable(Field.SimulateAimError, FormatPlainRow(Field.SimulateAimError, "Simulate Aim Error", _simulateAimError ? "On" : "Off", editHint: "space"));
            AddSelectable(Field.SmoothTierSampling, FormatPlainRow(Field.SmoothTierSampling, "Smooth Tier Sampling", _smoothTierSampling ? "On" : "Off", editHint: "space"));

            var tierMap = new StringBuilder();
            for (int i = 0; i < GameConstants.WaveTiers.Length; i++)
            {
                var t = GameConstants.WaveTiers[i];
                if (i > 0) tierMap.Append("  ");
                tierMap.Append($"{t.TierIndex}:{t.StartWave}-{t.EndWave}");
            }
            _lines.Add($"Tiers: {GameConstants.TierCount} (Waves 1-{GameConstants.TotalWaves})");
            _lines.Add($"Tier mapping (tier:waves): {tierMap}");

            _lines.Add("");
            _lines.Add("== Player / Projectile ==");
            AddSelectable(Field.BarrelLength, FormatNumericRow(Field.BarrelLength, "Barrel Length (m)", _overrideBarrelLength, barrelLen.ToString("F0")));
            AddSelectable(Field.Guidance, FormatNumericRow(Field.Guidance, "Fire Control Quality (x)", _overrideFireControlQuality, fireControl.ToString("F2")));
            AddSelectable(Field.MuzzleVelocityMult, FormatNumericRow(Field.MuzzleVelocityMult, "Muzzle Velocity Multiplier (x)", _overrideMuzzleVelocityMultiplier, muzzleMult.ToString("F2")));
            AddSelectable(Field.ProjectileMass, FormatNumericRow(Field.ProjectileMass, "Projectile Mass (kg)", _overrideProjectileMass, projMass.ToString("F0")));
            AddSelectable(Field.ProjectileDefense, FormatNumericRow(Field.ProjectileDefense, "Projectile Defense", _overrideProjectileDefense, projDef.ToString("F2")));
            AddSelectable(Field.Penetration, FormatNumericRow(Field.Penetration, "Penetration (x)", _overridePenetration, penetration.ToString("F2")));
            AddSelectable(Field.HitToleranceMult, FormatNumericRow(Field.HitToleranceMult, "Hit Tolerance Multiplier (x)", _overrideHitToleranceMultiplier, hitTolMult.ToString("F2")));
            AddSelectable(Field.PropulsionDeltaV, FormatNumericRow(Field.PropulsionDeltaV, "Propulsion Δv (m/s)", _overridePropulsionDeltaV, deltaVCap.ToString("F0")));
            AddSelectable(Field.PropulsionBurn, FormatNumericRow(Field.PropulsionBurn, "Propulsion Burn (s)", _overridePropulsionBurnDuration, burnSec.ToString("F1")));
            AddSelectable(Field.PropulsionRefMass, FormatNumericRow(Field.PropulsionRefMass, "Propulsion Ref Mass (kg)", _overridePropulsionReferenceMass, refMass.ToString("F0")));

            _lines.Add("");
            _lines.Add("== Enemy Overrides ==");
            AddSelectable(Field.EnemyMass, FormatNumericRow(Field.EnemyMass, "Enemy Mass (kg)", _overrideEnemyMass, _overrideEnemyMass ? _enemyMassKg.ToString("F0") : "(wave)"));
            AddSelectable(Field.EnemyFractureEnergy, FormatNumericRow(Field.EnemyFractureEnergy, "Enemy Fracture Multiplier (x)", _overrideEnemyFractureEnergy, _overrideEnemyFractureEnergy ? _enemyFractureEnergy.ToString("F2") : "(wave)"));
            AddSelectable(Field.EnemyVelocity, FormatNumericRow(Field.EnemyVelocity, "Enemy Velocity (m/s)", _overrideEnemyVelocity, _overrideEnemyVelocity ? _enemyVelocityMs.ToString("F0") : "(tier max)"));
            AddSelectable(Field.EnemyDensity, FormatNumericRow(Field.EnemyDensity, "Enemy Density (g/cm³)", _overrideEnemyDensity, _overrideEnemyDensity ? _enemyDensityGcm3.ToString("F2") : "(off)"));
            AddSelectable(Field.EnemyMaterialStrength, FormatNumericRow(Field.EnemyMaterialStrength, "Enemy Bulk Modulus (GPa)", _overrideEnemyMaterialStrength, _overrideEnemyMaterialStrength ? _enemyBulkModulusGpa.ToString("F0") : "(off)"));
            AddSelectable(Field.EnemyManeuverability, FormatNumericRow(Field.EnemyManeuverability, "Enemy Maneuverability", _overrideEnemyManeuverability, _overrideEnemyManeuverability ? _enemyManeuverability.ToString("F2") : "(wave)"));
            AddSelectable(Field.EnemyOffense, FormatNumericRow(Field.EnemyOffense, "Enemy Offense", _overrideEnemyOffense, _overrideEnemyOffense ? _enemyOffense.ToString("F2") : "(wave)"));
            AddSelectable(Field.EnemyDefense, FormatNumericRow(Field.EnemyDefense, "Enemy Defense", _overrideEnemyDefense, _overrideEnemyDefense ? _enemyDefense.ToString("F2") : "(wave)"));

            _lines.Add("");
            string runState = _last is null ? "(no results yet)" : (_resultsStale ? "(results: stale)" : "(results: current)");
            AddSelectable(Field.Run, FormatPlainRow(Field.Run, "Run & Export", runState, editHint: "R / Enter"));

            _lines.Add("");

            if (!string.IsNullOrWhiteSpace(_lastError))
            {
                _lines.Add($"ERROR: {_lastError}");
                return;
            }

            if (_last is null)
            {
                return;
            }

            var r = _last.Value;
            var expected = r.ExpectedHitRateByTier;
            var observed = r.ObservedHitRateByTier;

            _lines.Add("Tier  Expected  Observed   Shots   Hits");
            for (int i = 0; i < Math.Min(expected.Length, observed.Length); i++)
            {
                int shots = i < r.ShotsByTier.Length ? r.ShotsByTier[i] : 0;
                int hits = i < r.HitsByTier.Length ? r.HitsByTier[i] : 0;
                _lines.Add($"{i,4}  {expected[i],8:0.000}  {observed[i],8:0.000}  {shots,6}  {hits,6}");
            }
            if (_resultsStale)
                _lines.Add("NOTE: Results are stale (press ↩/(R)un to refresh).");
        }

        protected override void RenderBody(UiContext ui)
        {
            if (_lines.Count == 0)
                BuildLines();

            int winW;
            int winH;
            try { winW = Console.WindowWidth; } catch { winW = 120; }
            try { winH = Console.WindowHeight; } catch { winH = 40; }

            // No-side-panel rendering uses the whole console; reserve a couple lines
            // for PageBase's extra blank line + footer hint.
            int viewport = Math.Max(10, winH - 6);
            int maxScroll = Math.Max(0, _lines.Count - viewport);
            _scroll = Math.Clamp(_scroll, 0, maxScroll);

            EnsureSelectionVisible(viewport);

            int end = Math.Min(_lines.Count, _scroll + viewport);
            for (int i = _scroll; i < end; i++)
                ui.WriteLine(ClampToWidth(_lines[i], winW));
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                if (key.Key == ConsoleKey.S)
                {
                    CommitInputBufferIfNeeded(markResultsStale: true);
                    SaveCurrentToPresetSlot(_presetSlotIndex);
                    _statusMessage = $"Saved preset slot {_presetSlotIndex + 1}.";
                    BuildLines();
                    return PageResult.Stay;
                }

                if (key.Key == ConsoleKey.L)
                {
                    CommitInputBufferIfNeeded(markResultsStale: false);
                    LoadPresetSlot(_presetSlotIndex);
                    BuildLines();
                    return PageResult.Stay;
                }
            }

            // When editing preset/template text, treat printable characters as text input (even if they
            // match global hotkeys like 'R' or 'B'). Navigation keys still work as normal.
            if (SelectedField == Field.PresetName)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (_inputBuffer.Length > 0)
                        _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                    BuildLines();
                    return PageResult.Stay;
                }

                char tch = key.KeyChar;
                if (tch >= ' ' && tch <= '~')
                {
                    if (_inputBuffer.Length < 32)
                        _inputBuffer += tch;
                    BuildLines();
                    return PageResult.Stay;
                }
            }

            if (key.Key == ConsoleKey.B)
                return PageResult.Back(PageId.TestModeMenu);

            // Manual scrolling (selection stays put).
            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        _scroll = Math.Max(0, _scroll - 1);
                        BuildLines();
                        return PageResult.Stay;
                    case ConsoleKey.DownArrow:
                        _scroll++;
                        BuildLines();
                        return PageResult.Stay;
                }
            }

            if (key.Key == ConsoleKey.PageUp)
            {
                _scroll = Math.Max(0, _scroll - 6);
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.PageDown)
            {
                _scroll += 6;
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                CommitInputBufferIfNeeded(markResultsStale: true);
                MoveSelection(-1);
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                CommitInputBufferIfNeeded(markResultsStale: true);
                MoveSelection(1);
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
            {
                CommitInputBufferIfNeeded(markResultsStale: true);
                int dir = key.Key == ConsoleKey.RightArrow ? 1 : -1;

                switch (SelectedField)
                {
                    case Field.PresetSlot:
                        _presetSlotIndex = (_presetSlotIndex + dir + 10) % 10;
                        break;
                    case Field.CsvTemplateSlot:
                        _csvTemplateSlotIndex = (_csvTemplateSlotIndex + dir + 10) % 10;
                        SaveCsvTemplatesLastUsedSlotIndexBestEffort();
                        break;
                    case Field.Mode:
                        _rulesetIndex = (_rulesetIndex + dir + s_rulesets.Length) % s_rulesets.Length;
                        break;
                    case Field.Difficulty:
                        _difficultyIndex = (_difficultyIndex + dir + s_difficulties.Length) % s_difficulties.Length;
                        break;
                    case Field.WeaponsTechLevel:
                    {
                        int maxTech = Math.Max(1, WeaponTuning.WeaponsTechVelocityMultipliers.Length);
                        _weaponsTechLevel = Math.Clamp(_weaponsTechLevel + dir, 1, maxTech);
                        break;
                    }
                }

                _resultsStale = _last is not null;
                _lastError = null;
                _statusMessage = "Changed. Press ↩ or (R)un to recompute.";
                SaveStateBestEffort();
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Spacebar)
            {
                CommitInputBufferIfNeeded(markResultsStale: true);
                switch (SelectedField)
                {
                    case Field.SimulateAimError:
                        _simulateAimError = !_simulateAimError;
                        break;

                    case Field.SmoothTierSampling:
                        _smoothTierSampling = !_smoothTierSampling;
                        break;

                    case Field.BarrelLength: _overrideBarrelLength = !_overrideBarrelLength; break;
                    case Field.Guidance: _overrideFireControlQuality = !_overrideFireControlQuality; break;
                    case Field.MuzzleVelocityMult: _overrideMuzzleVelocityMultiplier = !_overrideMuzzleVelocityMultiplier; break;
                    case Field.ProjectileMass: _overrideProjectileMass = !_overrideProjectileMass; break;
                    case Field.ProjectileDefense: _overrideProjectileDefense = !_overrideProjectileDefense; break;
                    case Field.Penetration: _overridePenetration = !_overridePenetration; break;
                    case Field.HitToleranceMult: _overrideHitToleranceMultiplier = !_overrideHitToleranceMultiplier; break;
                    case Field.PropulsionDeltaV: _overridePropulsionDeltaV = !_overridePropulsionDeltaV; break;
                    case Field.PropulsionBurn: _overridePropulsionBurnDuration = !_overridePropulsionBurnDuration; break;
                    case Field.PropulsionRefMass: _overridePropulsionReferenceMass = !_overridePropulsionReferenceMass; break;

                    case Field.EnemyMass: _overrideEnemyMass = !_overrideEnemyMass; break;
                    case Field.EnemyFractureEnergy: _overrideEnemyFractureEnergy = !_overrideEnemyFractureEnergy; break;
                    case Field.EnemyVelocity: _overrideEnemyVelocity = !_overrideEnemyVelocity; break;
                    case Field.EnemyDensity: _overrideEnemyDensity = !_overrideEnemyDensity; break;
                    case Field.EnemyMaterialStrength: _overrideEnemyMaterialStrength = !_overrideEnemyMaterialStrength; break;
                    case Field.EnemyManeuverability: _overrideEnemyManeuverability = !_overrideEnemyManeuverability; break;
                    case Field.EnemyOffense: _overrideEnemyOffense = !_overrideEnemyOffense; break;
                    case Field.EnemyDefense: _overrideEnemyDefense = !_overrideEnemyDefense; break;
                }

                _resultsStale = _last is not null;
                _lastError = null;
                _statusMessage = "Changed. Press ↩ or (R)un to recompute.";
                SaveStateBestEffort();
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.R)
            {
                CommitInputBufferIfNeeded(markResultsStale: false);
                RunTest();
                ui.Clear();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.S)
            {
                CommitInputBufferIfNeeded(markResultsStale: true);
                SaveCurrentToPresetSlot(_presetSlotIndex);
                _statusMessage = $"Saved preset slot {_presetSlotIndex + 1}.";
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.L)
            {
                CommitInputBufferIfNeeded(markResultsStale: false);
                LoadPresetSlot(_presetSlotIndex);
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (_inputBuffer.Length > 0)
                    _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                BuildLines();
                return PageResult.Stay;
            }

            char ch = key.KeyChar;
            if (((ch >= '0' && ch <= '9') || ch == '.' || ch == '-')
                && (SelectedField == Field.WeaponsTechLevel
                    || SelectedField == Field.RadarLevel
                    || SelectedField == Field.SamplesPerWave
                    || SelectedField == Field.ShotsPerSample
                    || SelectedField == Field.BarrelLength
                    || SelectedField == Field.Guidance
                    || SelectedField == Field.MuzzleVelocityMult
                    || SelectedField == Field.ProjectileMass
                    || SelectedField == Field.ProjectileDefense
                    || SelectedField == Field.Penetration
                    || SelectedField == Field.HitToleranceMult
                    || SelectedField == Field.PropulsionDeltaV
                    || SelectedField == Field.PropulsionBurn
                    || SelectedField == Field.PropulsionRefMass
                    || SelectedField == Field.EnemyMass
                    || SelectedField == Field.EnemyFractureEnergy
                        || SelectedField == Field.EnemyVelocity
                    || SelectedField == Field.EnemyDensity
                    || SelectedField == Field.EnemyMaterialStrength
                    || SelectedField == Field.EnemyManeuverability
                    || SelectedField == Field.EnemyOffense
                    || SelectedField == Field.EnemyDefense))
            {
                if (_inputBuffer.Length < 24)
                    _inputBuffer += ch;
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key != ConsoleKey.Enter)
                return PageResult.Stay;

            if (SelectedField == Field.SavePreset)
            {
                CommitInputBufferIfNeeded(markResultsStale: true);
                SaveCurrentToPresetSlot(_presetSlotIndex);
                _statusMessage = $"Saved preset slot {_presetSlotIndex + 1}.";
                BuildLines();
                return PageResult.Stay;
            }

            if (SelectedField == Field.LoadPreset)
            {
                CommitInputBufferIfNeeded(markResultsStale: false);
                LoadPresetSlot(_presetSlotIndex);
                BuildLines();
                return PageResult.Stay;
            }

            if (SelectedField == Field.EnergyCsvColumns)
            {
                BuildLines();
                return PageResult.Go(PageId.DiagnosticsTuningLabEnergyCsvColumns);
            }

            // Enter runs; it also commits any pending edits first.
            CommitInputBufferIfNeeded(markResultsStale: false);
            RunTest();
            ui.Clear();
            return PageResult.Stay;
        }


        private void CommitInputBufferIfNeeded(bool markResultsStale)
        {
            if (string.IsNullOrWhiteSpace(_inputBuffer))
                return;

            if (SelectedField == Field.PresetName)
            {
                _presetNameDraftBySlot[_presetSlotIndex] = _inputBuffer.Trim();
                _inputBuffer = "";
                SaveStateBestEffort();
                return;
            }

            if (!double.TryParse(_inputBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                && !double.TryParse(_inputBuffer, out d))
            {
                _lastError = "Invalid number.";
                _inputBuffer = "";
                return;
            }

            switch (SelectedField)
            {
                case Field.WeaponsTechLevel:
                {
                    int maxTech = Math.Max(1, WeaponTuning.WeaponsTechVelocityMultipliers.Length);
                    _weaponsTechLevel = Math.Clamp((int)Math.Round(d), 1, maxTech);
                    break;
                }
                case Field.RadarLevel:
                    _radarLevel = Math.Clamp((int)Math.Round(d), 1, 3);
                    break;
                case Field.SamplesPerWave:
                    _samplesPerWave = Math.Clamp((int)Math.Round(d), 1, 500);
                    break;
                case Field.ShotsPerSample:
                    _shotsPerSample = Math.Clamp((int)Math.Round(d), 10, 5000);
                    break;

                case Field.BarrelLength:
                    _overrideBarrelLength = true;
                    _barrelLength = Math.Clamp(d, 50.0, 300.0);
                    break;
                case Field.Guidance:
                    _overrideFireControlQuality = true;
                    _fireControlQuality = Math.Clamp(d, 0.25, 5.0);
                    break;
                case Field.MuzzleVelocityMult:
                    _overrideMuzzleVelocityMultiplier = true;
                    _muzzleVelocityMultiplier = Math.Clamp(d, 0.25, 3.0);
                    break;
                case Field.ProjectileMass:
                    _overrideProjectileMass = true;
                    _projectileMassKg = Math.Clamp(d, 10.0, 10_000.0);
                    break;
                case Field.ProjectileDefense:
                    _overrideProjectileDefense = true;
                    _projectileDefense = Math.Clamp(d, 0.0, 1.0);
                    break;
                case Field.Penetration:
                    _overridePenetration = true;
                    _penetration = Math.Clamp(d, 0.10, 5.0);
                    break;
                case Field.HitToleranceMult:
                    _overrideHitToleranceMultiplier = true;
                    _hitToleranceMultiplier = Math.Clamp(d, 0.10, 5.0);
                    break;
                case Field.PropulsionDeltaV:
                    _overridePropulsionDeltaV = true;
                    _propulsionDeltaVCapacityMs = Math.Clamp(d, 0.0, 20000.0);
                    break;
                case Field.PropulsionBurn:
                    _overridePropulsionBurnDuration = true;
                    _propulsionBurnDurationSeconds = Math.Clamp(d, 0.1, 120.0);
                    break;
                case Field.PropulsionRefMass:
                    _overridePropulsionReferenceMass = true;
                    _propulsionReferenceMassKg = Math.Clamp(d, 0.01, 2000.0);
                    break;

                case Field.EnemyMass:
                    _overrideEnemyMass = true;
                    _enemyMassKg = Math.Clamp(d, 0.01, 1e12);
                    break;
                case Field.EnemyFractureEnergy:
                    _overrideEnemyFractureEnergy = true;
                    _enemyFractureEnergy = Math.Clamp(d, 0.0, 10.0);
                    break;
                case Field.EnemyVelocity:
                    _overrideEnemyVelocity = true;
                    _enemyVelocityMs = Math.Clamp(d, 0.0, 1e12);
                    break;
                case Field.EnemyDensity:
                    _overrideEnemyDensity = true;
                    _enemyDensityGcm3 = Math.Clamp(d, 0.0, 20.0);
                    break;
                case Field.EnemyMaterialStrength:
                    _overrideEnemyMaterialStrength = true;
                    _enemyBulkModulusGpa = Math.Clamp(d, 0.0, 2000.0);
                    break;
                case Field.EnemyManeuverability:
                    _overrideEnemyManeuverability = true;
                    _enemyManeuverability = Math.Clamp(d, 0.0, 1.0);
                    break;
                case Field.EnemyOffense:
                    _overrideEnemyOffense = true;
                    _enemyOffense = Math.Clamp(d, 0.0, 1.0);
                    break;
                case Field.EnemyDefense:
                    _overrideEnemyDefense = true;
                    _enemyDefense = Math.Clamp(d, 0.0, 1.0);
                    break;
            }

            _inputBuffer = "";
            _lastError = null;

            if (markResultsStale)
            {
                _resultsStale = _last is not null;
                _statusMessage = "Changed. Press ↩ or (R)un to recompute.";
            }

            SaveStateBestEffort();
        }

        private void RunTest()
        {
            try
            {
                GameConfigLoader.LoadIfExists();
                LoadCsvTemplatesBestEffort(preserveSelectedSlotIndex: true);

                var diff = s_difficulties[Math.Clamp(_difficultyIndex, 0, s_difficulties.Length - 1)];
                var ruleset = _rulesetIndex == 0 ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;

                // When not overridden in TuningLab, use the config-backed multiplier.
                double effectiveMuzzleMult = _overrideMuzzleVelocityMultiplier
                    ? _muzzleVelocityMultiplier
                    : GameConstants.MuzzleVelocityMultiplier;

                _last = FireSimulatorDiagnostics.ComputeTuningCurveByTier(
                    ruleset: ruleset,
                    difficulty: diff,
                    weaponsTechLevel: _weaponsTechLevel,
                    radarLevel: _radarLevel,
                    overrideEnemyMass: _overrideEnemyMass,
                    enemyMassKg: _enemyMassKg,
                    overrideEnemyFractureEnergy: _overrideEnemyFractureEnergy,
                    enemyFractureEnergy: _enemyFractureEnergy,
                    overrideEnemyDensity: _overrideEnemyDensity,
                    enemyDensityGcm3: _enemyDensityGcm3,
                    overrideEnemyMaterialStrength: _overrideEnemyMaterialStrength,
                    enemyBulkModulusGpa: _enemyBulkModulusGpa,
                    overrideEnemyManeuverability: _overrideEnemyManeuverability,
                    enemyManeuverability: _enemyManeuverability,
                    overrideEnemyOffense: _overrideEnemyOffense,
                    enemyOffense: _enemyOffense,
                    overrideEnemyDefense: _overrideEnemyDefense,
                    enemyDefense: _enemyDefense,
                    overrideBarrelLength: _overrideBarrelLength,
                    barrelLength: _barrelLength,
                    overrideFireControlQuality: _overrideFireControlQuality,
                    fireControlQuality: _fireControlQuality,
                    overrideMuzzleVelocityMultiplier: true,
                    muzzleVelocityMultiplier: effectiveMuzzleMult,
                    overrideProjectileMass: _overrideProjectileMass,
                    projectileMassKg: _projectileMassKg,
                    overrideProjectileDefense: _overrideProjectileDefense,
                    projectileDefense: _projectileDefense,
                    overridePenetration: _overridePenetration,
                    penetration: _penetration,
                    overrideHitToleranceMultiplier: _overrideHitToleranceMultiplier,
                    hitToleranceMultiplier: _hitToleranceMultiplier,
                    overridePropulsionDeltaV: _overridePropulsionDeltaV,
                    propulsionDeltaVCapacityMs: _propulsionDeltaVCapacityMs,
                    overridePropulsionBurnDuration: _overridePropulsionBurnDuration,
                    propulsionBurnDurationSeconds: _propulsionBurnDurationSeconds,
                    overridePropulsionReferenceMass: _overridePropulsionReferenceMass,
                    propulsionReferenceMassKg: _propulsionReferenceMassKg,
                    samplesPerWave: _samplesPerWave,
                    shotsPerSample: _shotsPerSample,
                    simulateAimError: _simulateAimError,
                    smoothTierSampling: _smoothTierSampling,
                    overrideEnemyVelocity: _overrideEnemyVelocity,
                    enemyVelocityMs: _enemyVelocityMs);

                _lastError = null;
                _resultsStale = false;

                // After a successful compute, export the CSV report immediately.
                // This keeps the workflow to a single "run + report" system.
                try
                {
                    var report = FireSimulatorDiagnostics.ComputeTuningEnergyReportByTier(
                        ruleset: ruleset,
                        difficulty: diff,
                        weaponsTechLevel: _weaponsTechLevel,
                        radarLevel: _radarLevel,
                        overrideEnemyMass: _overrideEnemyMass,
                        enemyMassKg: _enemyMassKg,
                        overrideEnemyFractureEnergy: _overrideEnemyFractureEnergy,
                        enemyFractureEnergy: _enemyFractureEnergy,
                        overrideEnemyDensity: _overrideEnemyDensity,
                        enemyDensityGcm3: _enemyDensityGcm3,
                        overrideEnemyMaterialStrength: _overrideEnemyMaterialStrength,
                        enemyBulkModulusGpa: _enemyBulkModulusGpa,
                        overrideEnemyManeuverability: _overrideEnemyManeuverability,
                        enemyManeuverability: _enemyManeuverability,
                        overrideEnemyOffense: _overrideEnemyOffense,
                        enemyOffense: _enemyOffense,
                        overrideEnemyDefense: _overrideEnemyDefense,
                        enemyDefense: _enemyDefense,
                        overrideBarrelLength: _overrideBarrelLength,
                        barrelLength: _barrelLength,
                        overrideFireControlQuality: _overrideFireControlQuality,
                        fireControlQuality: _fireControlQuality,
                        overrideMuzzleVelocityMultiplier: true,
                        muzzleVelocityMultiplier: effectiveMuzzleMult,
                        overrideProjectileMass: _overrideProjectileMass,
                        projectileMassKg: _projectileMassKg,
                        overrideProjectileDefense: _overrideProjectileDefense,
                        projectileDefense: _projectileDefense,
                        overridePenetration: _overridePenetration,
                        penetration: _penetration,
                        overrideHitToleranceMultiplier: _overrideHitToleranceMultiplier,
                        hitToleranceMultiplier: _hitToleranceMultiplier,
                        overridePropulsionDeltaV: _overridePropulsionDeltaV,
                        propulsionDeltaVCapacityMs: _propulsionDeltaVCapacityMs,
                        overridePropulsionBurnDuration: _overridePropulsionBurnDuration,
                        propulsionBurnDurationSeconds: _propulsionBurnDurationSeconds,
                        overridePropulsionReferenceMass: _overridePropulsionReferenceMass,
                        propulsionReferenceMassKg: _propulsionReferenceMassKg,
                        overrideEnemyVelocity: _overrideEnemyVelocity,
                        enemyVelocityMs: _enemyVelocityMs,
                        samplesPerTier: _samplesPerWave,
                        smoothTierSampling: _smoothTierSampling);

                    int idx = Math.Clamp(_csvTemplateSlotIndex, 0, 9);
                    var slot = (_csvTemplates.Slots is { Length: > 0 } && idx < _csvTemplates.Slots.Length)
                        ? _csvTemplates.Slots[idx]
                        : null;

                    IReadOnlyList<string> headers = (slot?.EnergyHeaders is { Length: > 0 })
                        ? slot.EnergyHeaders
                        : (slot?.Headers is { Length: > 0 })
                            ? slot.Headers
                            : FireSimulatorDiagnostics.GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected: false);

                    string path = FireSimulatorDiagnostics.WriteTuningLabEnergyReportCsv(
                        report,
                        includeMissedButDetected: false,
                        headersOverride: headers);

                    _statusMessage = $"Computed + Exported: {Path.GetFileName(path)}";
                }
                catch (Exception ex)
                {
                    _statusMessage = $"Computed; CSV export failed: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                _last = null;
                _lastError = ex.Message;
                _resultsStale = false;
                _statusMessage = "Compute failed.";
            }

            BuildLines();
        }
    }
}
