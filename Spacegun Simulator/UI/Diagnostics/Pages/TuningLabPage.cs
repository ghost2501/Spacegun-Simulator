using Spacegun_Simulator.Core;
using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Tests;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;
using System.Globalization;
using System.Text;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class TuningLabPage : PageBase
    {
        public override string Id => PageId.DiagnosticsTuningLab;
        public override string Title => "TUNING LAB";

        private enum Field
        {
            Mode = 0,
            Difficulty = 1,
            RadarLevel = 2,
            SamplesPerWave = 3,
            ShotsPerSample = 4,
            SimulateAimError = 5,

            BarrelLength = 6,
            Guidance = 7,
            MuzzleVelocityMult = 8,
            ProjectileMass = 9,
            ProjectileDefense = 10,
            Penetration = 11,
            HitToleranceMult = 12,
            PropulsionDeltaV = 13,
            PropulsionBurn = 14,
            PropulsionRefMass = 15,

            EnemyMass = 16,
            EnemyFractureEnergy = 17,
            EnemyManeuverability = 18,
            EnemyOffense = 19,

            Run = 20,
        }

        private static readonly Field[] s_fieldOrder = new[]
        {
            Field.Mode,
            Field.Difficulty,
            Field.RadarLevel,
            Field.SamplesPerWave,
            Field.ShotsPerSample,
            Field.SimulateAimError,

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
            Field.EnemyManeuverability,
            Field.EnemyOffense,

            Field.Run,
        };

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: false,
            ShowSidePanels: false,
            FooterHint: "↑/↓ Select  Digits=Type  Backspace=Edit  Space=Toggle  ↩ Commit+Run  (R)un  (B)ack  (Q)uit"
        );

        private int _selectedIndex;
        private int _scroll;
        private string _inputBuffer = "";

        private string? _lastCsvPath;
        private string? _lastCsvError;

        private int _rulesetIndex;
        private int _difficultyIndex;
        private int _radarLevel = 1;

        private bool _overrideEnemyMass;
        private double _enemyMassKg = 1_000_000.0;

        private bool _overrideEnemyFractureEnergy;
        private double _enemyFractureEnergy = 10_000.0;

        private bool _overrideEnemyManeuverability;
        private double _enemyManeuverability = 1.0;

        private bool _overrideEnemyOffense;
        private double _enemyOffense = 1.0;

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

        private FireSimulatorDiagnostics.TuningCurveByTierResult? _last;
        private string? _lastError;
        private bool _resultsStale;
        private string? _statusMessage;

        private readonly List<string> _lines = new();
        private readonly Dictionary<int, int> _selectableLineIndex = new();

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
        private const double DefaultProjectileMassKg = 100.0;
        private const double DefaultProjectileDefense = 0.0;
        private const double DefaultPenetration = 1.0;
        private const double DefaultHitToleranceMultiplier = 1.0;
        private const double DefaultPropulsionDeltaVCapacityMs = 0.0;
        private const double DefaultPropulsionBurnDurationSeconds = 1.0;
        private const double DefaultPropulsionReferenceMassKg = 10.0;

        public override void OnEnter(UiContext ui)
        {
            _selectedIndex = 0;
            _scroll = 0;
            _inputBuffer = "";
            _last = null;
            _lastError = null;
            _resultsStale = false;
            _statusMessage = "Press ↩ or (R)un to compute.";
            _lastCsvPath = null;
            _lastCsvError = null;
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

        private string FormatPlainRow(Field f, string label, string value, string? editHint = null)
        {
            string hint = string.IsNullOrWhiteSpace(editHint) ? "" : $"  ({editHint})";
            return $"{Cursor(f)} {label,-22} {value}{hint}";
        }

        private void BuildLines()
        {
            _lines.Clear();
            _selectableLineIndex.Clear();

            string rulesetLabel = s_rulesets[Math.Clamp(_rulesetIndex, 0, s_rulesets.Length - 1)];
            var diff = s_difficulties[Math.Clamp(_difficultyIndex, 0, s_difficulties.Length - 1)];

            double barrelLen = _overrideBarrelLength ? _barrelLength : DefaultBarrelLength;
            double fireControl = _overrideFireControlQuality ? _fireControlQuality : DefaultFireControlQuality;
            double muzzleMult = _overrideMuzzleVelocityMultiplier ? _muzzleVelocityMultiplier : DefaultMuzzleVelocityMultiplier;
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

            if (!string.IsNullOrWhiteSpace(_lastCsvPath))
            {
                _lines.Add($"CSV: {_lastCsvPath}");
                _lines.Add("");
            }
            else if (!string.IsNullOrWhiteSpace(_lastCsvError))
            {
                _lines.Add($"CSV export error: {_lastCsvError}");
                _lines.Add("");
            }

            void AddSelectable(Field f, string line)
            {
                int idx = Array.IndexOf(s_fieldOrder, f);
                if (idx >= 0)
                    _selectableLineIndex[idx] = _lines.Count;
                _lines.Add(line);
            }

            AddSelectable(Field.Mode, FormatPlainRow(Field.Mode, "Mode", rulesetLabel, editHint: "←/→"));
            AddSelectable(Field.Difficulty, FormatPlainRow(Field.Difficulty, "Difficulty", GetDifficultyLabel(diff), editHint: "←/→"));
            AddSelectable(Field.RadarLevel, FormatPlainRow(Field.RadarLevel, "RadarLevel", _radarLevel.ToString(), editHint: "type"));
            AddSelectable(Field.SamplesPerWave, FormatPlainRow(Field.SamplesPerWave, "SamplesPerWave", _samplesPerWave.ToString(), editHint: "type"));
            AddSelectable(Field.ShotsPerSample, FormatPlainRow(Field.ShotsPerSample, "ShotsPerSample", _shotsPerSample.ToString(), editHint: "type"));
            AddSelectable(Field.SimulateAimError, FormatPlainRow(Field.SimulateAimError, "SimulateAimError", _simulateAimError ? "On" : "Off", editHint: "space"));

            _lines.Add("");
            _lines.Add("== Player / Projectile ==");
            AddSelectable(Field.BarrelLength, FormatNumericRow(Field.BarrelLength, "BarrelLength (m)", _overrideBarrelLength, barrelLen.ToString("F0")));
            AddSelectable(Field.Guidance, FormatNumericRow(Field.Guidance, "Guidance", _overrideFireControlQuality, fireControl.ToString("F2")));
            AddSelectable(Field.MuzzleVelocityMult, FormatNumericRow(Field.MuzzleVelocityMult, "MuzzleVelocityMult", _overrideMuzzleVelocityMultiplier, muzzleMult.ToString("F2")));
            AddSelectable(Field.ProjectileMass, FormatNumericRow(Field.ProjectileMass, "ProjectileMass (kg)", _overrideProjectileMass, projMass.ToString("F0")));
            AddSelectable(Field.ProjectileDefense, FormatNumericRow(Field.ProjectileDefense, "ProjectileDefense", _overrideProjectileDefense, projDef.ToString("F2")));
            AddSelectable(Field.Penetration, FormatNumericRow(Field.Penetration, "Penetration (x)", _overridePenetration, penetration.ToString("F2")));
            AddSelectable(Field.HitToleranceMult, FormatNumericRow(Field.HitToleranceMult, "HitToleranceMult (x)", _overrideHitToleranceMultiplier, hitTolMult.ToString("F2")));
            AddSelectable(Field.PropulsionDeltaV, FormatNumericRow(Field.PropulsionDeltaV, "PropulsionΔv (m/s)", _overridePropulsionDeltaV, deltaVCap.ToString("F0")));
            AddSelectable(Field.PropulsionBurn, FormatNumericRow(Field.PropulsionBurn, "PropulsionBurn (s)", _overridePropulsionBurnDuration, burnSec.ToString("F1")));
            AddSelectable(Field.PropulsionRefMass, FormatNumericRow(Field.PropulsionRefMass, "PropulsionRefMass (kg)", _overridePropulsionReferenceMass, refMass.ToString("F0")));

            _lines.Add("");
            _lines.Add("== Enemy Overrides ==");
            AddSelectable(Field.EnemyMass, FormatNumericRow(Field.EnemyMass, "EnemyMass (kg)", _overrideEnemyMass, _overrideEnemyMass ? _enemyMassKg.ToString("F0") : "(wave)"));
            AddSelectable(Field.EnemyFractureEnergy, FormatNumericRow(Field.EnemyFractureEnergy, "EnemyFractureEnergy", _overrideEnemyFractureEnergy, _overrideEnemyFractureEnergy ? _enemyFractureEnergy.ToString("F0") : "(wave)"));
            AddSelectable(Field.EnemyManeuverability, FormatNumericRow(Field.EnemyManeuverability, "EnemyManeuverability", _overrideEnemyManeuverability, _overrideEnemyManeuverability ? _enemyManeuverability.ToString("F2") : "(wave)"));
            AddSelectable(Field.EnemyOffense, FormatNumericRow(Field.EnemyOffense, "EnemyOffense", _overrideEnemyOffense, _overrideEnemyOffense ? _enemyOffense.ToString("F2") : "(wave)"));

            _lines.Add("");
            string runState = _last is null ? "(no results yet)" : (_resultsStale ? "(results: stale)" : "(results: current)");
            AddSelectable(Field.Run, FormatPlainRow(Field.Run, "Run Test", runState, editHint: "R / Enter"));

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
                MoveSelection(-1);
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                MoveSelection(1);
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
            {
                int dir = key.Key == ConsoleKey.RightArrow ? 1 : -1;

                switch (SelectedField)
                {
                    case Field.Mode:
                        _rulesetIndex = (_rulesetIndex + dir + s_rulesets.Length) % s_rulesets.Length;
                        break;
                    case Field.Difficulty:
                        _difficultyIndex = (_difficultyIndex + dir + s_difficulties.Length) % s_difficulties.Length;
                        break;
                }

                _resultsStale = _last is not null;
                _lastError = null;
                _statusMessage = "Changed. Press ↩ or (R)un to recompute.";
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Spacebar)
            {
                switch (SelectedField)
                {
                    case Field.SimulateAimError:
                        _simulateAimError = !_simulateAimError;
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
                    case Field.EnemyManeuverability: _overrideEnemyManeuverability = !_overrideEnemyManeuverability; break;
                    case Field.EnemyOffense: _overrideEnemyOffense = !_overrideEnemyOffense; break;
                }

                _resultsStale = _last is not null;
                _lastError = null;
                _statusMessage = "Changed. Press ↩ or (R)un to recompute.";
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.R)
            {
                RunTest();
                ui.Clear();
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
            if ((ch >= '0' && ch <= '9') || ch == '.' || ch == '-')
            {
                if (_inputBuffer.Length < 24)
                    _inputBuffer += ch;
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key != ConsoleKey.Enter)
                return PageResult.Stay;

            // Enter always runs. If the user typed a value, it commits it first.
            if (!string.IsNullOrWhiteSpace(_inputBuffer))
            {
                if (!double.TryParse(_inputBuffer, out double d))
                {
                    _lastError = "Invalid number.";
                    BuildLines();
                    return PageResult.Stay;
                }

                switch (SelectedField)
                {
                    case Field.RadarLevel:
                        _radarLevel = Math.Clamp((int)Math.Round(d), 1, 3);
                        break;
                    case Field.SamplesPerWave:
                        _samplesPerWave = Math.Clamp((int)Math.Round(d), 1, 50);
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
                        _projectileMassKg = Math.Clamp(d, 10.0, 5000.0);
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
                        _enemyFractureEnergy = Math.Clamp(d, 0.0, 1e12);
                        break;
                    case Field.EnemyManeuverability:
                        _overrideEnemyManeuverability = true;
                        _enemyManeuverability = Math.Clamp(d, 0.0, 10.0);
                        break;
                    case Field.EnemyOffense:
                        _overrideEnemyOffense = true;
                        _enemyOffense = Math.Clamp(d, 0.0, 10.0);
                        break;
                }

                _inputBuffer = "";
                _lastError = null;
            }

            RunTest();
            ui.Clear();
            return PageResult.Stay;
        }

        private void RunTest()
        {
            try
            {
                var diff = s_difficulties[Math.Clamp(_difficultyIndex, 0, s_difficulties.Length - 1)];
                var ruleset = _rulesetIndex == 0 ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;

                _last = FireSimulatorDiagnostics.ComputeTuningCurveByTier(
                    ruleset: ruleset,
                    difficulty: diff,
                    radarLevel: _radarLevel,
                    overrideEnemyMass: _overrideEnemyMass,
                    enemyMassKg: _enemyMassKg,
                    overrideEnemyFractureEnergy: _overrideEnemyFractureEnergy,
                    enemyFractureEnergy: _enemyFractureEnergy,
                    overrideEnemyManeuverability: _overrideEnemyManeuverability,
                    enemyManeuverability: _enemyManeuverability,
                    overrideEnemyOffense: _overrideEnemyOffense,
                    enemyOffense: _enemyOffense,
                    overrideBarrelLength: _overrideBarrelLength,
                    barrelLength: _barrelLength,
                    overrideFireControlQuality: _overrideFireControlQuality,
                    fireControlQuality: _fireControlQuality,
                    overrideMuzzleVelocityMultiplier: _overrideMuzzleVelocityMultiplier,
                    muzzleVelocityMultiplier: _muzzleVelocityMultiplier,
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
                    simulateAimError: _simulateAimError);

                _lastError = null;
                _resultsStale = false;
                _lastCsvPath = null;
                _lastCsvError = null;

                try
                {
                    _lastCsvPath = WriteRunCsv(ruleset, diff, _last.Value);
                    _statusMessage = "Computed.";
                }
                catch (Exception csvEx)
                {
                    _lastCsvPath = null;
                    _lastCsvError = csvEx.Message;
                    _statusMessage = "Computed (CSV export failed).";
                }
            }
            catch (Exception ex)
            {
                _last = null;
                _lastError = ex.Message;
                _resultsStale = false;
                _statusMessage = "Compute failed.";
                _lastCsvPath = null;
                _lastCsvError = null;
            }

            BuildLines();
        }

        private static string EscapeCsv(string? value)
        {
            value ??= string.Empty;
            bool mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            if (!mustQuote)
                return value;
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        private static string FormatInv(double value, string format)
            => value.ToString(format, CultureInfo.InvariantCulture);

        private string WriteRunCsv(EnemyGenerationRuleset ruleset, GameDifficulty difficulty, FireSimulatorDiagnostics.TuningCurveByTierResult result)
        {
            // Effective player parameters (defaults unless overridden)
            double barrelLen = _overrideBarrelLength ? _barrelLength : DefaultBarrelLength;
            double fireControl = _overrideFireControlQuality ? _fireControlQuality : DefaultFireControlQuality;
            double muzzleMult = _overrideMuzzleVelocityMultiplier ? _muzzleVelocityMultiplier : DefaultMuzzleVelocityMultiplier;
            double projMass = _overrideProjectileMass ? _projectileMassKg : DefaultProjectileMassKg;
            double projDef = _overrideProjectileDefense ? _projectileDefense : DefaultProjectileDefense;
            double penetration = _overridePenetration ? _penetration : DefaultPenetration;
            double hitTolMult = _overrideHitToleranceMultiplier ? _hitToleranceMultiplier : DefaultHitToleranceMultiplier;
            double deltaVCap = _overridePropulsionDeltaV ? _propulsionDeltaVCapacityMs : DefaultPropulsionDeltaVCapacityMs;
            double burnSec = _overridePropulsionBurnDuration ? _propulsionBurnDurationSeconds : DefaultPropulsionBurnDurationSeconds;
            double refMass = _overridePropulsionReferenceMass ? _propulsionReferenceMassKg : DefaultPropulsionReferenceMassKg;

            return FireSimulatorDiagnostics.AppendTuningLabRunCsv(
                ruleset: ruleset,
                difficulty: difficulty,
                radarLevel: _radarLevel,
                samplesPerWave: _samplesPerWave,
                shotsPerSample: _shotsPerSample,
                simulateAimError: _simulateAimError,
                overrideEnemyMass: _overrideEnemyMass,
                enemyMassKg: _enemyMassKg,
                overrideEnemyFractureEnergy: _overrideEnemyFractureEnergy,
                enemyFractureEnergy: _enemyFractureEnergy,
                overrideEnemyManeuverability: _overrideEnemyManeuverability,
                enemyManeuverability: _enemyManeuverability,
                overrideEnemyOffense: _overrideEnemyOffense,
                enemyOffense: _enemyOffense,
                barrelLengthMeters: barrelLen,
                fireControlQuality: fireControl,
                muzzleVelocityMultiplier: muzzleMult,
                projectileMassKg: projMass,
                projectileDefense: projDef,
                penetration: penetration,
                hitToleranceMultiplier: hitTolMult,
                propulsionDeltaVCapacityMs: deltaVCap,
                propulsionBurnDurationSeconds: burnSec,
                propulsionReferenceMassKg: refMass,
                result: result);
        }
    }
}
