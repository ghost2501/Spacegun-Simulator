using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Phases
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
            FooterHint: "Digits+[Enter]=Accept  B=Back  Backspace=Edit  Esc=Menu  Q=Quit"
        );

        private enum Mode
        {
            InputDelay = 0,
            InputElevation = 1,
            InputAzimuth = 2,
            InputVelocity = 3
        }

        private readonly double _maxVelocity;

        private Mode _mode;
        private DifficultyConfig? _diff;
        private string _inputBuffer = "";
        private string _message = "";
        private readonly List<string> _lines = new();
        private int _scroll;

        public bool Submitted { get; private set; }

        public double LaunchDelaySeconds { get; private set; }
        public double TargetElevationDegrees { get; private set; }
        public double TargetAzimuthDegrees { get; private set; }
        public double LaunchVelocityMs { get; private set; }

        public EnterFiringParametersPage(double maxVelocity)
        {
            _maxVelocity = maxVelocity;
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

            _mode = Mode.InputDelay;
            _inputBuffer = "";
            _scroll = 0;
            BuildLines();
        }

        private void BuildLines()
        {
            _lines.Clear();

            var diff = _diff;
            if (diff == null)
            {
                _lines.Add("✗ Input unavailable.".PadRight(60));
                _lines.Add("");
                _lines.Add(Clamp60(_message));
                return;
            }

            _lines.Add(Clamp60($"Difficulty: {diff.DisplayName}"));
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

            switch (_mode)
            {
                case Mode.InputDelay:
                    _lines.Add("Enter launch delay time (seconds).".PadRight(60));
                    _lines.Add(Clamp60($"Default: {diff.FormatLaunchDelay(LaunchDelaySeconds)}"));
                    _lines.Add(Clamp60($"> {_inputBuffer}"));
                    break;

                case Mode.InputElevation:
                    _lines.Add("Enter target elevation (-90 to 90 degrees).".PadRight(60));
                    _lines.Add(Clamp60($"Default: {diff.FormatElevation(TargetElevationDegrees)}"));
                    _lines.Add(Clamp60($"> {_inputBuffer}"));
                    break;

                case Mode.InputAzimuth:
                    _lines.Add("Enter target azimuth (0-360 degrees, 0=North).".PadRight(60));
                    _lines.Add(Clamp60($"Default: {diff.FormatAzimuth(TargetAzimuthDegrees)}"));
                    _lines.Add(Clamp60($"> {_inputBuffer}"));
                    break;

                case Mode.InputVelocity:
                    _lines.Add(Clamp60($"Enter launch velocity (0-{diff.VelocityPrecision.Format(_maxVelocity)} m/s)."));
                    _lines.Add(Clamp60($"Default: {diff.FormatVelocity(LaunchVelocityMs)}"));
                    _lines.Add(Clamp60($"> {_inputBuffer}"));
                    break;
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
            const int lineStep = 1;
            const int pageStep = 6;

            if (key.Key == ConsoleKey.B)
            {
                if (_mode == Mode.InputDelay)
                    return PageResult.Back();

                _mode = (Mode)((int)_mode - 1);
                _inputBuffer = "";
                _message = "";
                _scroll = 0;
                BuildLines();
                return PageResult.Stay;
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow: _scroll -= lineStep; return PageResult.Stay;
                case ConsoleKey.DownArrow: _scroll += lineStep; return PageResult.Stay;
                case ConsoleKey.PageUp: _scroll -= pageStep; return PageResult.Stay;
                case ConsoleKey.PageDown: _scroll += pageStep; return PageResult.Stay;
            }

            if (_diff == null)
                return PageResult.Exit;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (_inputBuffer.Length > 0)
                    _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                BuildLines();
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

            if (ch == '-' && _mode == Mode.InputElevation && _inputBuffer.Length == 0)
            {
                _inputBuffer = "-";
                BuildLines();
                return PageResult.Stay;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                _message = "";

                switch (_mode)
                {
                    case Mode.InputDelay:
                        if (!TryAcceptDouble(_inputBuffer, out double d, fallback: LaunchDelaySeconds) || d < 0)
                        {
                            _message = "✗ Invalid delay (>=0).";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        LaunchDelaySeconds = d;
                        _inputBuffer = "";
                        _mode = Mode.InputElevation;
                        BuildLines();
                        return PageResult.Stay;

                    case Mode.InputElevation:
                        if (!TryAcceptDouble(_inputBuffer, out double e, fallback: TargetElevationDegrees) || e < -90 || e > 90)
                        {
                            _message = "✗ Invalid elevation (-90..90).";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        TargetElevationDegrees = e;
                        _inputBuffer = "";
                        _mode = Mode.InputAzimuth;
                        BuildLines();
                        return PageResult.Stay;

                    case Mode.InputAzimuth:
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
                        _mode = Mode.InputVelocity;
                        BuildLines();
                        return PageResult.Stay;

                    case Mode.InputVelocity:
                        if (!TryAcceptDouble(_inputBuffer, out double v, fallback: LaunchVelocityMs) || v < 0 || v > _maxVelocity)
                        {
                            _message = "✗ Invalid velocity.";
                            _inputBuffer = "";
                            BuildLines();
                            return PageResult.Stay;
                        }
                        LaunchVelocityMs = v;
                        _inputBuffer = "";

                        Submitted = true;
                        return PageResult.Exit;
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
    }
    }

