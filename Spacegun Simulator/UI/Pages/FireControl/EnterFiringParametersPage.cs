using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
    /// <summary>
    /// Collects the player's firing parameters using the page-based UI (no Console.ReadLine).
    /// This is the first step of the commit workflow.
    /// </summary>
    public sealed class EnterFiringParametersPage : PageBase
    {
        public override string Id => PageId.EnterFiringParameters;
        public override string Title => "ENTER FIRING PARAMETERS";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
            AutoSaveOnEnter: false,
            AutoSaveOnExit: false,
            FooterHint: "←/→ Adjust ΔV  Digits+↩  (F)ire  (B)ack  (M)enu  (Q)uit"
        );

        private enum Selection
        {
            DeltaVImpulse = 0,
            DeltaVControl = 1,
            DeltaVDodge = 2,
            InputDelay = 3,
            InputElevation = 4,
            InputAzimuth = 5,
            InputVelocity = 6
        }

        private readonly double _maxVelocity;
        private readonly bool _enableDeltaVAllocation;
        private readonly double _effectiveDeltaVAvailableMs;
        private readonly double _baseHitToleranceMeters;
        private readonly double _baseDefenseRating;
        private readonly double _projectileMassKg;

        private Selection _selection;
        private DifficultyConfig? _diff;
        private string _inputBuffer = "";
        private string _message = "";
        private readonly List<string> _lines = new();
        private readonly Dictionary<Selection, int> _selectionLineIndex = new();
        private int _scroll;

        public bool Submitted { get; private set; }

        public int DeltaVImpulsePercent { get; private set; } = 100;
        public int DeltaVControlPercent { get; private set; } = 0;
        public int DeltaVDodgePercent { get; private set; } = 0;

        public int DeltaVSelectedChannelIndex { get; private set; } = 0; // 0=Impulse, 1=Control, 2=Dodge

        public double LaunchDelaySeconds { get; private set; }
        public double TargetElevationDegrees { get; private set; }
        public double TargetAzimuthDegrees { get; private set; }
        public double LaunchVelocityMs { get; private set; }

        public EnterFiringParametersPage(double maxVelocity)
            : this(maxVelocity, enableDeltaVAllocation: false)
        {
        }

        public EnterFiringParametersPage(double maxVelocity, bool enableDeltaVAllocation)
            : this(
                maxVelocity,
                enableDeltaVAllocation,
                effectiveDeltaVAvailableMs: 0.0,
                baseHitToleranceMeters: 0.0,
                baseDefenseRating: 0.0)
        {
        }

        public EnterFiringParametersPage(
            double maxVelocity,
            bool enableDeltaVAllocation,
            double effectiveDeltaVAvailableMs,
            double baseHitToleranceMeters,
            double baseDefenseRating,
            double projectileMassKg = 0.0)
        {
            _maxVelocity = maxVelocity;
            _enableDeltaVAllocation = enableDeltaVAllocation;

            _effectiveDeltaVAvailableMs = Math.Max(0.0, effectiveDeltaVAvailableMs);
            _baseHitToleranceMeters = Math.Max(0.0, baseHitToleranceMeters);
            _baseDefenseRating = Math.Clamp(baseDefenseRating, 0.0, 1.0);
            _projectileMassKg = Math.Max(0.0, projectileMassKg);
        }

        public override void OnEnter(UiContext ui)
        {
            var game = ui.Game;
            if (game == null)
            {
                _diff = null;
                _message = "✗ No active game session.";
            }
            else
            {
                _diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
                _message = "";
            }

            Submitted = false;
            LaunchDelaySeconds = 0.0;
            TargetElevationDegrees = 0.0;
            TargetAzimuthDegrees = 0.0;
            LaunchVelocityMs = 0.0;

            DeltaVImpulsePercent = 100;
            DeltaVControlPercent = 0;
            DeltaVDodgePercent = 0;
            DeltaVSelectedChannelIndex = 0;

            _selection = _enableDeltaVAllocation ? Selection.DeltaVImpulse : Selection.InputDelay;
            _inputBuffer = "";
            _scroll = 0;
            BuildLines();
        }

        private void BuildLines()
        {
            _lines.Clear();
            _selectionLineIndex.Clear();

            var diff = _diff;
            if (diff == null)
            {
                _lines.Add("✗ Input unavailable.".PadRight(60));
                _lines.Add("");
                _lines.Add(Clamp60(_message));
                return;
            }

            _lines.Add(Clamp60($"Difficulty: {diff.DisplayName}"));

            // Put Δv + inputs first so they don't get pushed off-screen.
            if (_enableDeltaVAllocation)
            {
                double totalDv = _effectiveDeltaVAvailableMs;
                double allocatedDv = totalDv * ((DeltaVImpulsePercent + DeltaVControlPercent + DeltaVDodgePercent) / 100.0);

                _lines.Add("");
                _lines.Add(Clamp60($"== Total Δv [{totalDv:F0}] / Allocated [{allocatedDv:F0}] =="));

                double impulseDv = totalDv * (DeltaVImpulsePercent / 100.0);
                double controlDv = totalDv * (DeltaVControlPercent / 100.0);
                double dodgeDv = totalDv * (DeltaVDodgePercent / 100.0);

                double v0 = Math.Max(0.0, LaunchVelocityMs);
                double ke0 = BallisticsCalculator.CalculateKineticEnergyMJ(_projectileMassKg, v0);
                double keImpulse = BallisticsCalculator.CalculateKineticEnergyMJ(_projectileMassKg, v0 + impulseDv);
                double deltaKeImpulse = Math.Max(0.0, keImpulse - ke0);

                double controlTolMult = ComputeControlHitToleranceMultiplier(controlDv);
                double deltaHitTol = _baseHitToleranceMeters * (controlTolMult - 1.0);

                double dodgeDefenseBonus = ComputeDodgeDefenseBonus(dodgeDv);

                _lines.Add(Clamp60(FormatDeltaVChannelLine(
                    selection: Selection.DeltaVImpulse,
                    name: "Impulse",
                    percent: DeltaVImpulsePercent,
                    effect: $"+ {deltaKeImpulse:F1} Impact Energy (MJ)")));
                _selectionLineIndex[Selection.DeltaVImpulse] = _lines.Count - 1;
                _lines.Add(Clamp60(FormatDeltaVChannelLine(
                    selection: Selection.DeltaVControl,
                    name: "Control",
                    percent: DeltaVControlPercent,
                    effect: $"+ {deltaHitTol:F1} Hit Tolerance (m)")));
                _selectionLineIndex[Selection.DeltaVControl] = _lines.Count - 1;
                _lines.Add(Clamp60(FormatDeltaVChannelLine(
                    selection: Selection.DeltaVDodge,
                    name: "Dodge",
                    percent: DeltaVDodgePercent,
                    effect: $"+ {dodgeDefenseBonus:F2} Defense")));
                _selectionLineIndex[Selection.DeltaVDodge] = _lines.Count - 1;
            }

            _lines.Add("");
            _lines.Add("== Input Firing Solution ==".PadRight(60));

            _lines.Add(Clamp60(FormatInputLine(diff, Selection.InputDelay, "Delay", diff.FormatLaunchDelay(LaunchDelaySeconds))));
            _selectionLineIndex[Selection.InputDelay] = _lines.Count - 1;
            _lines.Add(Clamp60(FormatInputLine(diff, Selection.InputElevation, "Elevation", diff.FormatElevation(TargetElevationDegrees))));
            _selectionLineIndex[Selection.InputElevation] = _lines.Count - 1;
            _lines.Add(Clamp60(FormatInputLine(diff, Selection.InputAzimuth, "Azimuth", diff.FormatAzimuth(TargetAzimuthDegrees))));
            _selectionLineIndex[Selection.InputAzimuth] = _lines.Count - 1;
            _lines.Add(Clamp60(FormatInputLine(diff, Selection.InputVelocity, "Velocity", diff.FormatVelocity(LaunchVelocityMs))));
            _selectionLineIndex[Selection.InputVelocity] = _lines.Count - 1;
            _lines.Add("");

            // Restored context that used to be visible.
            _lines.Add("PRECISION REQUIREMENTS:".PadRight(60));
            foreach (var line in (diff.GetPrecisionSummary() ?? "").Split('\n'))
                _lines.Add(Clamp60(line));
            _lines.Add("");

            _lines.Add("=== CURRENT INPUT ===".PadRight(60));
            _lines.Add(Clamp60($"Launch delay: {diff.FormatLaunchDelay(LaunchDelaySeconds)}"));
            _lines.Add(Clamp60($"Target elevation: {diff.FormatElevation(TargetElevationDegrees)}"));
            _lines.Add(Clamp60($"Target azimuth: {diff.FormatAzimuth(TargetAzimuthDegrees)}"));
            _lines.Add(Clamp60($"Launch velocity: {diff.FormatVelocity(LaunchVelocityMs)}"));
            _lines.Add("");

            if (!string.IsNullOrWhiteSpace(_message))
            {
                _lines.Add(Clamp60(_message));
                _lines.Add("");
            }
        }

        protected override void RenderBody(UiContext ui)
        {
            if (_lines.Count == 0)
                BuildLines();

            int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
            int maxScroll = Math.Max(0, _lines.Count - viewport);
            if (_scroll < 0) _scroll = 0;
            if (_scroll > maxScroll) _scroll = maxScroll;

            int end = Math.Min(_lines.Count, _scroll + viewport);
            for (int i = _scroll; i < end; i++)
                ui.WriteLine(Clamp60(_lines[i]));
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            const int pageStep = 6;

            if (key.Key == ConsoleKey.F)
            {
                Submitted = true;
                return PageResult.Exit;
            }

            if (key.Key == ConsoleKey.B)
            {
                if (_selection == FirstSelectable())
                    return PageResult.Back();

                MoveSelection(-1);
                BuildLines();
                return PageResult.Stay;
            }

            // Explicit scroll controls (selection stays put).
            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow: _scroll -= 1; return PageResult.Stay;
                    case ConsoleKey.DownArrow: _scroll += 1; return PageResult.Stay;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.PageUp:
                    _scroll -= pageStep;
                    return PageResult.Stay;
                case ConsoleKey.PageDown:
                    _scroll += pageStep;
                    return PageResult.Stay;
                case ConsoleKey.UpArrow:
                    MoveSelection(-1);
                    BuildLines();
                    EnsureSelectionVisible(ui);
                    return PageResult.Stay;
                case ConsoleKey.DownArrow:
                    MoveSelection(1);
                    BuildLines();
                    EnsureSelectionVisible(ui);
                    return PageResult.Stay;
            }

            if (_diff == null)
                return PageResult.Exit;

            if (IsDeltaVSelection(_selection))
            {
                if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
                {
                    int baseStep = 5;
                    int step = (key.Modifiers & ConsoleModifiers.Shift) != 0 ? 1 : baseStep;
                    int delta = key.Key == ConsoleKey.RightArrow ? step : -step;

                    int selectedIndex = _selection switch
                    {
                        Selection.DeltaVImpulse => 0,
                        Selection.DeltaVControl => 1,
                        Selection.DeltaVDodge => 2,
                        _ => 0
                    };

                    AdjustDeltaVSplit(selectedIndex, delta);
                    BuildLines();
                    EnsureSelectionVisible(ui);
                    return PageResult.Stay;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    SetSelection(Selection.InputDelay);
                    BuildLines();
                    EnsureSelectionVisible(ui);
                    return PageResult.Stay;
                }

                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (_inputBuffer.Length > 0)
                    _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                BuildLines();
                EnsureSelectionVisible(ui);
                return PageResult.Stay;
            }

            char ch = key.KeyChar;
            if (ch >= '0' && ch <= '9')
            {
                if (_inputBuffer.Length < 18)
                    _inputBuffer += ch;
                BuildLines();
                return PageResult.Stay;
            }

            if (ch == '.' && !_inputBuffer.Contains('.'))
            {
                if (_inputBuffer.Length < 18)
                    _inputBuffer += ch;
                BuildLines();
                return PageResult.Stay;
            }

            if (ch == '-' && _selection == Selection.InputElevation && _inputBuffer.Length == 0)
            {
                _inputBuffer = "-";
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                _message = "";

                switch (_selection)
                {
                    case Selection.InputDelay:
                        if (!TryAcceptDouble(_inputBuffer, out double d, fallback: LaunchDelaySeconds) || d < 0)
                        {
                            _message = "✗ Invalid delay (>=0).";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        LaunchDelaySeconds = d;
                        _inputBuffer = "";
                        SetSelection(Selection.InputElevation);
                        BuildLines();
                        EnsureSelectionVisible(ui);
                        return PageResult.Stay;

                    case Selection.InputElevation:
                        if (!TryAcceptDouble(_inputBuffer, out double e, fallback: TargetElevationDegrees) || e < -90 || e > 90)
                        {
                            _message = "✗ Invalid elevation (-90..90).";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        TargetElevationDegrees = e;
                        _inputBuffer = "";
                        SetSelection(Selection.InputAzimuth);
                        BuildLines();
                        EnsureSelectionVisible(ui);
                        return PageResult.Stay;

                    case Selection.InputAzimuth:
                        if (!TryAcceptDouble(_inputBuffer, out double a, fallback: TargetAzimuthDegrees))
                        {
                            _message = "✗ Invalid azimuth.";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        a %= 360.0;
                        if (a < 0) a += 360.0;
                        TargetAzimuthDegrees = a;
                        _inputBuffer = "";
                        SetSelection(Selection.InputVelocity);
                        BuildLines();
                        EnsureSelectionVisible(ui);
                        return PageResult.Stay;

                    case Selection.InputVelocity:
                        if (!TryAcceptDouble(_inputBuffer, out double v, fallback: LaunchVelocityMs) || v < 0 || v > _maxVelocity)
                        {
                            _message = "✗ Invalid velocity.";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        LaunchVelocityMs = v;
                        _inputBuffer = "";

                        _message = "Ready. Press (F)ire to commit.";
                        BuildLines();
                        EnsureSelectionVisible(ui);
                        return PageResult.Stay;
                }
            }

            return PageResult.Stay;
        }

        private static bool TryAcceptDouble(string input, out double value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                value = fallback;
                return true;
            }

            if (!double.TryParse(input, out value))
            {
                value = fallback;
                return false;
            }

            return true;
        }

        private string FormatDeltaVChannelLine(Selection selection, string name, int percent, string effect)
        {
            string marker = (_selection == selection) ? ">" : " ";
            string bar = BuildBar(percent, width: 10);
            return $"{marker} {name,-7} [{bar}] {effect}";
        }

        private string FormatInputLine(DifficultyConfig diff, Selection selection, string label, string formattedValue)
        {
            string marker = (_selection == selection) ? ">" : " ";
            string shown = (_selection == selection && !string.IsNullOrWhiteSpace(_inputBuffer)) ? _inputBuffer : formattedValue;
            return $"{marker} {label,-9} {formattedValue}   > {shown}";
        }

        private static string BuildBar(int percent, int width)
        {
            percent = Math.Clamp(percent, 0, 100);
            width = Math.Max(1, width);
            int filled = (int)Math.Round(width * (percent / 100.0), MidpointRounding.AwayFromZero);
            filled = Math.Clamp(filled, 0, width);
            return new string('#', filled) + new string('x', width - filled);
        }

        private static bool IsDeltaVSelection(Selection selection)
            => selection is Selection.DeltaVImpulse or Selection.DeltaVControl or Selection.DeltaVDodge;

        private Selection FirstSelectable()
            => _enableDeltaVAllocation ? Selection.DeltaVImpulse : Selection.InputDelay;

        private Selection LastSelectable()
            => Selection.InputVelocity;

        private void MoveSelection(int dir)
        {
            var first = FirstSelectable();
            var last = LastSelectable();
            int count = (int)last - (int)first + 1;
            int offset = (int)_selection - (int)first;
            int next = (offset + dir) % count;
            if (next < 0) next += count;
            SetSelection((Selection)((int)first + next));
        }

        private void SetSelection(Selection selection)
        {
            if (_selection != selection)
                _inputBuffer = "";

            _selection = selection;
            if (IsDeltaVSelection(selection))
            {
                DeltaVSelectedChannelIndex = selection switch
                {
                    Selection.DeltaVImpulse => 0,
                    Selection.DeltaVControl => 1,
                    Selection.DeltaVDodge => 2,
                    _ => 0
                };
            }
        }

        private void EnsureSelectionVisible(UiContext ui)
        {
            if (_lines.Count == 0)
                return;

            if (!_selectionLineIndex.TryGetValue(_selection, out int lineIndex))
                return;

            int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
            int maxScroll = Math.Max(0, _lines.Count - viewport);

            if (lineIndex < _scroll)
                _scroll = lineIndex;
            else if (lineIndex >= _scroll + viewport)
                _scroll = lineIndex - viewport + 1;

            _scroll = Math.Clamp(_scroll, 0, maxScroll);
        }

        private void AdjustDeltaVSplit(int selectedIndex, int delta)
        {
            selectedIndex = Math.Clamp(selectedIndex, 0, 2);

            int[] pcts = [DeltaVImpulsePercent, DeltaVControlPercent, DeltaVDodgePercent];
            int current = pcts[selectedIndex];
            int desired = Math.Clamp(current + delta, 0, 100);
            int change = desired - current;
            if (change == 0)
                return;

            // Increasing the selected channel must take from the others.
            if (change > 0)
            {
                int othersTotal = pcts[0] + pcts[1] + pcts[2] - current;
                int actual = Math.Min(change, othersTotal);
                if (actual <= 0)
                    return;

                pcts[selectedIndex] += actual;
                int remaining = actual;

                // Take from the largest other buckets first.
                while (remaining > 0)
                {
                    int donor = -1;
                    int donorValue = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == selectedIndex) continue;
                        if (pcts[i] > donorValue)
                        {
                            donor = i;
                            donorValue = pcts[i];
                        }
                    }

                    if (donor < 0 || donorValue <= 0)
                        break;

                    int take = Math.Min(remaining, pcts[donor]);
                    pcts[donor] -= take;
                    remaining -= take;
                }
            }
            else
            {
                // Decreasing selected channel: distribute to the smallest other buckets first.
                int give = -change;
                pcts[selectedIndex] -= give;

                int remaining = give;
                while (remaining > 0)
                {
                    int receiver = -1;
                    int receiverValue = int.MaxValue;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == selectedIndex) continue;
                        if (pcts[i] < receiverValue)
                        {
                            receiver = i;
                            receiverValue = pcts[i];
                        }
                    }

                    if (receiver < 0)
                        break;

                    int add = remaining;
                    pcts[receiver] += add;
                    remaining -= add;
                }
            }

            // Normalize (safety) to sum 100.
            int sum = pcts[0] + pcts[1] + pcts[2];
            if (sum != 100)
            {
                int fix = 100 - sum;
                pcts[selectedIndex] = Math.Clamp(pcts[selectedIndex] + fix, 0, 100);
            }

            DeltaVImpulsePercent = pcts[0];
            DeltaVControlPercent = pcts[1];
            DeltaVDodgePercent = pcts[2];
        }

        private static double ComputeControlHitToleranceMultiplier(double effectiveDeltaVMs)
        {
            // Mirrors CommitFiringSolutionFlow.ComputeControlGuidanceBonus.
            // 0 m/s => 1.00x, 2000 m/s => ~1.38x, 5000 m/s => ~1.62x; cap at 1.75x.
            if (effectiveDeltaVMs <= 0.0) return 1.0;
            double x = Math.Clamp(effectiveDeltaVMs / 2000.0, 0.0, 20.0);
            double bonus = 0.75 * (1.0 - Math.Exp(-x));
            return Math.Clamp(1.0 + bonus, 1.0, 1.75);
        }

        private static double ComputeDodgeDefenseBonus(double effectiveDeltaVMs)
        {
            // Diminishing returns; cap at +0.25 defense.
            if (effectiveDeltaVMs <= 0.0) return 0.0;
            double x = Math.Clamp(effectiveDeltaVMs / 2500.0, 0.0, 20.0);
            return Math.Clamp(0.25 * (1.0 - Math.Exp(-x)), 0.0, 0.25);
        }
    }
}

