using Spacegun_Simulator.Core;
using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Tests;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;
using System.Text.Json;
using System.Globalization;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class TuningLabEnergyCsvColumnsPage : PageBase
    {
        public override string Id => PageId.DiagnosticsTuningLabEnergyCsvColumns;
        public override string Title => "TUNING LAB - REPORT TEMPLATE (CSV)";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: false,
            ShowSidePanels: false,
            FooterHint: "↑/↓ Select  ←/→ Switch column  Enter Add/Remove  Ctrl+↑/↓ Reorder  [ / ] Slot  (N)ame  (S)ave  (L)oad  (B)ack  (Q)uit"
        );

        // Must remain compatible with the file used by TuningLabPage.
        private sealed record CsvTemplateFile(int Version, int LastUsedSlotIndex, CsvTemplateSlot?[] Slots)
        {
            public static CsvTemplateFile CreateDefault()
            {
                var slots = new CsvTemplateSlot?[10];
                slots[0] = new CsvTemplateSlot(
                    Name: "Default",
                    SavedUtc: DateTime.UtcNow,
                    Headers: FireSimulatorDiagnostics.GetDefaultTuningLabRunCsvHeaders().ToArray(),
                    EnergyHeaders: FireSimulatorDiagnostics.GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected: false).ToArray());
                return new CsvTemplateFile(Version: 1, LastUsedSlotIndex: 0, Slots: slots);
            }
        }

        private sealed record CsvTemplateSlot(string Name, DateTime SavedUtc, string[] Headers, string[]? EnergyHeaders = null);

        private readonly List<string> _lines = new();

        private CsvTemplateFile _templates = CsvTemplateFile.CreateDefault();
        private int _slotIndex;

        private bool _editingName;
        private string _nameDraft = "";
        private string _inputBuffer = "";

        private bool _dirty;

        private List<string> _selectedHeaders = new();
        private List<string> _availableHeaders = new();

        private bool _focusSelected = true;
        private int _selectedIndex;
        private int _availableIndex;

        private string? _status;

        public override void OnEnter(UiContext ui)
        {
            LoadTemplatesBestEffort();
            LoadSlotIntoDraft();
            RebuildAvailableHeaders();
            BuildLines();
        }

        private static string GetTemplatesPath()
        {
            string dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(Path.Combine(dir, "TuningLab_CsvTemplates.json"));
        }

        private void LoadTemplatesBestEffort()
        {
            try
            {
                string path = GetTemplatesPath();
                if (!File.Exists(path))
                {
                    _templates = CsvTemplateFile.CreateDefault();
                    _slotIndex = _templates.LastUsedSlotIndex;
                    return;
                }

                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<CsvTemplateFile>(json);
                if (loaded?.Slots is null)
                {
                    _templates = CsvTemplateFile.CreateDefault();
                    _slotIndex = _templates.LastUsedSlotIndex;
                    return;
                }

                var slots = new CsvTemplateSlot?[10];
                for (int i = 0; i < Math.Min(10, loaded.Slots.Length); i++)
                    slots[i] = loaded.Slots[i];

                _templates = new CsvTemplateFile(
                    Version: 1,
                    LastUsedSlotIndex: Math.Clamp(loaded.LastUsedSlotIndex, 0, 9),
                    Slots: slots);

                _slotIndex = _templates.LastUsedSlotIndex;
            }
            catch (Exception ex)
            {
                _templates = CsvTemplateFile.CreateDefault();
                _slotIndex = _templates.LastUsedSlotIndex;
                _status = $"Load failed: {ex.Message}";
            }
        }

        private void SaveTemplatesBestEffort()
        {
            try
            {
                string path = GetTemplatesPath();
                var toSave = _templates with { LastUsedSlotIndex = _slotIndex };
                string json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _status = $"Save failed: {ex.Message}";
            }
        }

        private void LoadSlotIntoDraft()
        {
            _slotIndex = Math.Clamp(_slotIndex, 0, 9);
            var slot = _templates.Slots.Length > _slotIndex ? _templates.Slots[_slotIndex] : null;

            _nameDraft = slot?.Name ?? "";
            if (string.IsNullOrWhiteSpace(_nameDraft))
                _nameDraft = "(unnamed)";

            string[] headers = slot?.EnergyHeaders ?? slot?.Headers ?? Array.Empty<string>();
            if (headers.Length == 0)
                headers = FireSimulatorDiagnostics.GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected: false).ToArray();

            _selectedHeaders = headers.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().ToList();
            if (_selectedHeaders.Count == 0)
                _selectedHeaders = headers.Distinct().ToList();

            _selectedIndex = 0;
            _availableIndex = 0;

            _dirty = false;
        }

        private void SaveDraftToSlot()
        {
            _slotIndex = Math.Clamp(_slotIndex, 0, 9);

            var existing = _templates.Slots[_slotIndex];

            string name = string.IsNullOrWhiteSpace(_nameDraft) ? (existing?.Name ?? "(unnamed)") : _nameDraft.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = "(unnamed)";

            // Single CSV generator: this header list is THE template.
            // To preserve backward compatibility with existing template files,
            // we write to both fields.
            string[] headers = _selectedHeaders.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().ToArray();
            if (headers.Length == 0)
                headers = FireSimulatorDiagnostics.GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected: false).ToArray();

            _templates.Slots[_slotIndex] = new CsvTemplateSlot(
                Name: name,
                SavedUtc: DateTime.UtcNow,
                Headers: headers,
                EnergyHeaders: headers);

            _templates = _templates with { LastUsedSlotIndex = _slotIndex };
            SaveTemplatesBestEffort();
            _dirty = false;
        }

        private void RebuildAvailableHeaders()
        {
            IReadOnlyList<string> all = FireSimulatorDiagnostics.GetTuningLabEnergyReportAvailableCsvHeaders();

            var selected = new HashSet<string>(_selectedHeaders);

            _availableHeaders = all.Where(h => !selected.Contains(h)).ToList();
            if (_selectedHeaders.Count > 0)
                _selectedIndex = Math.Clamp(_selectedIndex, 0, _selectedHeaders.Count - 1);
            else
                _selectedIndex = 0;

            if (_availableHeaders.Count > 0)
                _availableIndex = Math.Clamp(_availableIndex, 0, _availableHeaders.Count - 1);
            else
                _availableIndex = 0;
        }

        private static string Clamp(string text, int width)
        {
            text ??= string.Empty;
            return text.Length > width ? text.Substring(0, width) : text;
        }

        private void BuildLines()
        {
            _lines.Clear();

            string slotLabel = $"Slot {_slotIndex + 1}/10";
            string nameShown = _editingName ? _inputBuffer : _nameDraft;
            if (string.IsNullOrWhiteSpace(nameShown)) nameShown = "(unnamed)";

            string dirty = _dirty ? " *" : "";

            _lines.Add($"Report Template (CSV)   {slotLabel}   Name: {nameShown}{dirty}".PadRight(DefaultFrameWidth));
            _lines.Add("Pick which columns export, and their order.".PadRight(DefaultFrameWidth));
            _lines.Add("Run+export happens from Tuning Lab (↩ / R).".PadRight(DefaultFrameWidth));
            _lines.Add("");

            if (!string.IsNullOrWhiteSpace(_status))
            {
                _lines.Add(_status!);
                _lines.Add("");
            }

            int winW;
            try { winW = Console.WindowWidth; } catch { winW = 120; }

            // Two-column layout: Selected (left) | Available (right)
            int colGap = 3;
            int colW = Math.Max(18, (winW - colGap) / 2);
            string header = $"SELECTED ({_selectedHeaders.Count})".PadRight(colW) + new string(' ', colGap) + $"AVAILABLE ({_availableHeaders.Count})";
            _lines.Add(Clamp(header, winW));

            int rows = Math.Max(_selectedHeaders.Count, _availableHeaders.Count);
            rows = Math.Max(rows, 1);

            for (int r = 0; r < rows; r++)
            {
                string left = r < _selectedHeaders.Count ? _selectedHeaders[r] : "";
                string right = r < _availableHeaders.Count ? _availableHeaders[r] : "";

                string lCursor = (_focusSelected && r == _selectedIndex) ? ">" : " ";
                string rCursor = (!_focusSelected && r == _availableIndex) ? ">" : " ";

                string leftCell = (lCursor + " " + left).PadRight(colW);
                string rightCell = rCursor + " " + right;
                _lines.Add(Clamp(leftCell + new string(' ', colGap) + rightCell, winW));
            }

            _lines.Add("");
            _lines.Add("Enter on AVAILABLE adds; Enter on SELECTED removes.".PadRight(DefaultFrameWidth));
            _lines.Add("Ctrl+↑/↓ reorders selected columns.".PadRight(DefaultFrameWidth));
        }

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

        private static string GetStatePath()
        {
            string dir = Path.Combine(UserDataPaths.GetSavesDirectory(), "TuningLab");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(Path.Combine(dir, "TuningLab_State.json"));
        }

        private static readonly GameDifficulty[] s_difficulties = new[]
        {
            GameDifficulty.NuclearOption,
            GameDifficulty.CometsAndAsteroids,
            GameDifficulty.RealSpacegunSimulator
        };

        private void ExportCsvBestEffort()
        {
            try
            {
                GameConfigLoader.LoadIfExists();

                string statePath = GetStatePath();
                StateFile? state = null;
                if (File.Exists(statePath))
                {
                    string json = File.ReadAllText(statePath);
                    state = JsonSerializer.Deserialize<StateFile>(json);
                }

                state ??= new StateFile(
                    Version: 1,
                    SelectedIndex: 0,
                    RulesetIndex: 0,
                    DifficultyIndex: 0,
                    WeaponsTechLevel: 1,
                    RadarLevel: 1,
                    SamplesPerWave: 5,
                    ShotsPerSample: 200,
                    SimulateAimError: false,
                    SmoothTierSampling: false,
                    OverrideEnemyDensity: false,
                    EnemyDensityGcm3: 7.85,
                    OverrideEnemyMaterialStrength: false,
                    EnemyBulkModulusGpa: 200.0,
                    OverrideEnemyMass: false,
                    EnemyMassKg: 1_000_000.0,
                    OverrideEnemyFractureMult: false,
                    EnemyFractureMult: 1.0,
                    OverrideEnemyVelocity: false,
                    EnemyVelocityMs: 0.0,
                    OverrideEnemyManeuverability: false,
                    EnemyManeuverability: 1.0,
                    OverrideEnemyOffense: false,
                    EnemyOffense: 1.0,
                    OverrideEnemyDefense: false,
                    EnemyDefense: 1.0,
                    OverrideBarrelLength: false,
                    BarrelLength: 100.0,
                    OverrideFireControlQuality: false,
                    FireControlQuality: 1.0,
                    OverrideMuzzleVelocityMultiplier: false,
                    MuzzleVelocityMultiplier: 1.0,
                    OverrideProjectileMass: false,
                    ProjectileMassKg: 100.0,
                    OverrideProjectileDefense: false,
                    ProjectileDefense: 0.0,
                    OverridePenetration: false,
                    Penetration: 1.0,
                    OverrideHitToleranceMultiplier: false,
                    HitToleranceMultiplier: 1.0,
                    OverridePropulsionDeltaV: false,
                    PropulsionDeltaVCapacityMs: 0.0,
                    OverridePropulsionBurnDuration: false,
                    PropulsionBurnDurationSeconds: 1.0,
                    OverridePropulsionReferenceMass: false,
                    PropulsionReferenceMassKg: 10.0,
                    PresetSlotIndex: 0);

                var diff = s_difficulties[Math.Clamp(state.DifficultyIndex, 0, s_difficulties.Length - 1)];
                var ruleset = Math.Clamp(state.RulesetIndex, 0, 1) == 0 ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;

                double effectiveMuzzleMult = state.OverrideMuzzleVelocityMultiplier
                    ? state.MuzzleVelocityMultiplier
                    : GameConstants.MuzzleVelocityMultiplier;

                var report = FireSimulatorDiagnostics.ComputeTuningEnergyReportByTier(
                    ruleset: ruleset,
                    difficulty: diff,
                    weaponsTechLevel: state.WeaponsTechLevel,
                    radarLevel: state.RadarLevel,
                    overrideEnemyMass: state.OverrideEnemyMass,
                    enemyMassKg: state.EnemyMassKg,
                    overrideEnemyFractureEnergy: state.OverrideEnemyFractureMult,
                    enemyFractureEnergy: state.EnemyFractureMult,
                    overrideEnemyDensity: state.OverrideEnemyDensity,
                    enemyDensityGcm3: state.EnemyDensityGcm3,
                    overrideEnemyMaterialStrength: state.OverrideEnemyMaterialStrength,
                    enemyBulkModulusGpa: state.EnemyBulkModulusGpa,
                    overrideEnemyManeuverability: state.OverrideEnemyManeuverability,
                    enemyManeuverability: state.EnemyManeuverability,
                    overrideEnemyOffense: state.OverrideEnemyOffense,
                    enemyOffense: state.EnemyOffense,
                    overrideEnemyDefense: state.OverrideEnemyDefense,
                    enemyDefense: state.EnemyDefense,
                    overrideBarrelLength: state.OverrideBarrelLength,
                    barrelLength: state.BarrelLength,
                    overrideFireControlQuality: state.OverrideFireControlQuality,
                    fireControlQuality: state.FireControlQuality,
                    overrideMuzzleVelocityMultiplier: true,
                    muzzleVelocityMultiplier: effectiveMuzzleMult,
                    overrideProjectileMass: state.OverrideProjectileMass,
                    projectileMassKg: state.ProjectileMassKg,
                    overrideProjectileDefense: state.OverrideProjectileDefense,
                    projectileDefense: state.ProjectileDefense,
                    overridePenetration: state.OverridePenetration,
                    penetration: state.Penetration,
                    overrideHitToleranceMultiplier: state.OverrideHitToleranceMultiplier,
                    hitToleranceMultiplier: state.HitToleranceMultiplier,
                    overridePropulsionDeltaV: state.OverridePropulsionDeltaV,
                    propulsionDeltaVCapacityMs: state.PropulsionDeltaVCapacityMs,
                    overridePropulsionBurnDuration: state.OverridePropulsionBurnDuration,
                    propulsionBurnDurationSeconds: state.PropulsionBurnDurationSeconds,
                    overridePropulsionReferenceMass: state.OverridePropulsionReferenceMass,
                    propulsionReferenceMassKg: state.PropulsionReferenceMassKg,
                    overrideEnemyVelocity: state.OverrideEnemyVelocity,
                    enemyVelocityMs: state.EnemyVelocityMs,
                    samplesPerTier: state.SamplesPerWave,
                    smoothTierSampling: state.SmoothTierSampling);

                string[] headers = _selectedHeaders.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray();
                if (headers.Length == 0)
                    headers = FireSimulatorDiagnostics.GetDefaultTuningLabEnergyReportCsvHeaders(includeMissedButDetected: false).ToArray();

                string path = FireSimulatorDiagnostics.WriteTuningLabEnergyReportCsv(
                    report,
                    includeMissedButDetected: false,
                    headersOverride: headers);

                _status = $"Exported: {path}";
            }
            catch (Exception ex)
            {
                _status = $"Export failed: {ex.Message}";
            }

            BuildLines();
        }

        protected override void RenderBody(UiContext ui)
        {
            foreach (var line in _lines)
                ui.WriteLine(line);
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.B)
                return PageResult.Back(PageId.DiagnosticsTuningLab);

            bool ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;

            // Some terminals report printable keys as Key=NoName with KeyChar set.
            // Use KeyChar fallbacks for non-editing hotkeys so saving/loading still works.
            bool isSaveHotkey = (!_editingName && (key.Key == ConsoleKey.S || key.KeyChar == 's' || key.KeyChar == 'S'))
                || (ctrl && key.Key == ConsoleKey.S);
            bool isLoadHotkey = (!_editingName && (key.Key == ConsoleKey.L || key.KeyChar == 'l' || key.KeyChar == 'L'))
                || (ctrl && key.Key == ConsoleKey.L);

            if (isSaveHotkey)
            {
                if (_editingName)
                {
                    _nameDraft = (_inputBuffer ?? "").Trim();
                    _editingName = false;
                    _inputBuffer = "";
                }

                SaveDraftToSlot();
                // Reload from disk so the UI reflects what actually persisted.
                LoadTemplatesBestEffort();
                LoadSlotIntoDraft();
                _status = $"Saved template to slot {_slotIndex + 1}.";
                RebuildAvailableHeaders();
                BuildLines();
                return PageResult.Stay;
            }

            if (isLoadHotkey)
            {
                LoadTemplatesBestEffort();
                LoadSlotIntoDraft();
                RebuildAvailableHeaders();
                _status = $"Loaded slot {_slotIndex + 1}.";
                BuildLines();
                return PageResult.Stay;
            }

            // '[' on typical keyboards
            if (key.Key == ConsoleKey.Oem4)
            {
                _slotIndex = (_slotIndex + 9) % 10;
                _templates = _templates with { LastUsedSlotIndex = _slotIndex };
                LoadSlotIntoDraft();
                RebuildAvailableHeaders();
                SaveTemplatesBestEffort();
                _status = $"Slot: {_slotIndex + 1}.";
                BuildLines();
                return PageResult.Stay;
            }

            // ']' on typical keyboards
            if (key.Key == ConsoleKey.Oem6)
            {
                _slotIndex = (_slotIndex + 1) % 10;
                _templates = _templates with { LastUsedSlotIndex = _slotIndex };
                LoadSlotIntoDraft();
                RebuildAvailableHeaders();
                SaveTemplatesBestEffort();
                _status = $"Slot: {_slotIndex + 1}.";
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.N)
            {
                _editingName = true;
                _inputBuffer = "";
                _status = "Editing name: type then Enter.";
                BuildLines();
                return PageResult.Stay;
            }

            if (_editingName)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (_inputBuffer.Length > 0)
                        _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                    BuildLines();
                    return PageResult.Stay;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    _nameDraft = (_inputBuffer ?? "").Trim();
                    _editingName = false;
                    _inputBuffer = "";
                    _dirty = true;
                    _status = "Name updated (press S to save).";
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

                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.LeftArrow)
            {
                _focusSelected = true;
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.RightArrow)
            {
                _focusSelected = false;
                BuildLines();
                return PageResult.Stay;
            }

            if (ctrl && _focusSelected && _selectedHeaders.Count > 0)
            {
                if (key.Key == ConsoleKey.UpArrow && _selectedIndex > 0)
                {
                    (_selectedHeaders[_selectedIndex - 1], _selectedHeaders[_selectedIndex]) = (_selectedHeaders[_selectedIndex], _selectedHeaders[_selectedIndex - 1]);
                    _selectedIndex--;
                    RebuildAvailableHeaders();
                    _status = "Moved up.";
                    _dirty = true;
                    BuildLines();
                    return PageResult.Stay;
                }

                if (key.Key == ConsoleKey.DownArrow && _selectedIndex < _selectedHeaders.Count - 1)
                {
                    (_selectedHeaders[_selectedIndex + 1], _selectedHeaders[_selectedIndex]) = (_selectedHeaders[_selectedIndex], _selectedHeaders[_selectedIndex + 1]);
                    _selectedIndex++;
                    RebuildAvailableHeaders();
                    _status = "Moved down.";
                    _dirty = true;
                    BuildLines();
                    return PageResult.Stay;
                }
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (_focusSelected)
                    _selectedIndex = Math.Max(0, _selectedIndex - 1);
                else
                    _availableIndex = Math.Max(0, _availableIndex - 1);

                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (_focusSelected)
                    _selectedIndex = Math.Min(Math.Max(0, _selectedHeaders.Count - 1), _selectedIndex + 1);
                else
                    _availableIndex = Math.Min(Math.Max(0, _availableHeaders.Count - 1), _availableIndex + 1);

                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (_focusSelected)
                {
                    if (_selectedHeaders.Count == 0)
                    {
                        _status = "Nothing selected to remove.";
                        BuildLines();
                        return PageResult.Stay;
                    }

                    string removed = _selectedHeaders[_selectedIndex];
                    _selectedHeaders.RemoveAt(_selectedIndex);
                    _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _selectedHeaders.Count - 1));
                    RebuildAvailableHeaders();
                    _status = $"Removed: {removed}";
                    _dirty = true;
                    BuildLines();
                    return PageResult.Stay;
                }

                if (_availableHeaders.Count == 0)
                {
                    _status = "No available headers to add.";
                    BuildLines();
                    return PageResult.Stay;
                }

                string added = _availableHeaders[Math.Clamp(_availableIndex, 0, _availableHeaders.Count - 1)];
                _selectedHeaders.Add(added);
                _selectedIndex = _selectedHeaders.Count - 1;
                RebuildAvailableHeaders();
                _status = $"Added: {added}";
                _dirty = true;
                BuildLines();
                return PageResult.Stay;
            }

            return PageResult.Stay;
        }
    }
}
