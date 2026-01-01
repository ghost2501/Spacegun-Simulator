using Spacegun_Simulator.Tests;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class DiagnosticsTestHarnessPage : PageBase
    {
        public override string Id => PageId.DiagnosticsTestHarness;
        public override string Title => "TEST HARNESS";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
			FooterHint: "Run(↩) (B)ack (M)enu (Q)uit"
        );

        private readonly List<string> _lines = new();
        private IReadOnlyList<FireSimulatorDiagnostics.CheckResult>? _lastChecks;
        private FireSimulatorDiagnostics.TechAuditResult? _lastAudit;
        private FireSimulatorDiagnostics.EnemyCurveResult? _lastEnemyCurve;
        private FireSimulatorDiagnostics.CounterCurveResult? _lastCounterCurve;
        private FireSimulatorDiagnostics.EndToEndCurveResult? _lastEndToEndCurve;

        public override void OnEnter(UiContext ui)
        {
            _lastChecks = null;
            _lastAudit = null;
            _lastEnemyCurve = null;
            _lastCounterCurve = null;
            _lastEndToEndCurve = null;
            BuildLines();
        }

        private void BuildLines()
        {
            _lines.Clear();
            _lines.Add("Runs automated diagnostics checks.".PadRight(60));
            _lines.Add("");

            if (_lastChecks == null && _lastAudit == null && _lastEnemyCurve == null && _lastCounterCurve == null && _lastEndToEndCurve == null)
            {
                _lines.Add("Press Enter to run checks and export CSVs.".PadRight(60));
                return;
            }

            if (_lastChecks != null)
            {
                int pass = 0;
                foreach (var r in _lastChecks)
                    if (r.Passed) pass++;

                _lines.Add(Clamp60($"Consistency checks: {pass}/{_lastChecks.Count} passed"));
                foreach (var r in _lastChecks)
                {
                    string mark = r.Passed ? "OK" : "FAIL";
                    string msg = r.Passed ? "" : $" - {r.Message}";
                    _lines.Add(Clamp60($"  {mark}: {r.Name}{msg}"));
                }

                _lines.Add("");
            }

            if (_lastAudit != null)
            {
                var audit = _lastAudit.Value;
                if (!string.IsNullOrWhiteSpace(audit.CsvPath) && audit.CsvPath.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    _lines.Add(Clamp60($"Tech audit: {audit.CsvPath}"));
                }
                else if (audit.ScenarioCount <= 0 || string.IsNullOrWhiteSpace(audit.CsvPath))
                {
                    _lines.Add("Tech audit: no scenarios found.".PadRight(60));
                }
                else
                {
                    _lines.Add(Clamp60($"Tech audit: {audit.ScenarioCount} scenarios"));
                    _lines.Add(Clamp60($"CSV: {audit.CsvPath}"));
                }
            }

            if (_lastEnemyCurve != null)
            {
                var curve = _lastEnemyCurve.Value;
                if (!string.IsNullOrWhiteSpace(curve.CsvPath) && curve.CsvPath.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    _lines.Add("");
                    _lines.Add(Clamp60($"Enemy curve: {curve.CsvPath}"));
                }
                else if (curve.RowCount <= 0 || string.IsNullOrWhiteSpace(curve.CsvPath))
                {
                    _lines.Add("");
                    _lines.Add("Enemy curve: no rows exported.".PadRight(60));
                }
                else
                {
                    _lines.Add("");
                    _lines.Add(Clamp60($"Enemy curve: {curve.RowCount} rows"));
                    _lines.Add(Clamp60($"CSV: {curve.CsvPath}"));
                }
            }

            if (_lastCounterCurve != null)
            {
                var curve = _lastCounterCurve.Value;
                if (!string.IsNullOrWhiteSpace(curve.CsvPath) && curve.CsvPath.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    _lines.Add("");
                    _lines.Add(Clamp60($"Balance curve: {curve.CsvPath}"));
                }
                else if (curve.RowCount <= 0 || string.IsNullOrWhiteSpace(curve.CsvPath))
                {
                    _lines.Add("");
                    _lines.Add("Balance curve: no rows exported.".PadRight(60));
                }
                else
                {
                    _lines.Add("");
                    _lines.Add(Clamp60($"Balance curve: {curve.RowCount} rows"));
                    _lines.Add(Clamp60($"CSV: {curve.CsvPath}"));
                }
            }

            if (_lastEndToEndCurve != null)
            {
                var curve = _lastEndToEndCurve.Value;
                if (!string.IsNullOrWhiteSpace(curve.CsvPath) && curve.CsvPath.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    _lines.Add("");
                    _lines.Add(Clamp60($"End-to-end curve: {curve.CsvPath}"));
                }
                else if (curve.RowCount <= 0 || string.IsNullOrWhiteSpace(curve.CsvPath))
                {
                    _lines.Add("");
                    _lines.Add("End-to-end curve: no rows exported.".PadRight(60));
                }
                else
                {
                    _lines.Add("");
                    _lines.Add(Clamp60($"End-to-end curve: {curve.RowCount} rows"));
                    _lines.Add(Clamp60($"CSV: {curve.CsvPath}"));
                }
            }

            _lines.Add("");
            _lines.Add("Press Enter to run again.".PadRight(60));
        }

        protected override void RenderBody(UiContext ui)
        {
            foreach (var line in _lines)
                ui.WriteLine(Clamp60(line));
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.B)
                return PageResult.Back(PageId.TestModeMenu);

            if (key.Key != ConsoleKey.Enter)
                return PageResult.Stay;

            // Run legacy harness.
            ui.Clear();
            try
            {
                _lastChecks = FireSimulatorDiagnostics.RunConsistencyChecks();
            }
            catch (Exception ex)
            {
                _lastChecks = new[]
                {
                    new FireSimulatorDiagnostics.CheckResult("Consistency checks", Passed: false, Message: ex.Message)
                };
            }

            try
            {
                _lastAudit = FireSimulatorDiagnostics.RunTechAuditAndWriteCsv();
            }
            catch (Exception ex)
            {
                _lastAudit = new FireSimulatorDiagnostics.TechAuditResult(CsvPath: $"ERROR: {ex.Message}", ScenarioCount: 0);
            }

            try
            {
                _lastEnemyCurve = FireSimulatorDiagnostics.RunEnemyCurveAndWriteCsv();
            }
            catch (Exception ex)
            {
                _lastEnemyCurve = new FireSimulatorDiagnostics.EnemyCurveResult(CsvPath: $"ERROR: {ex.Message}", RowCount: 0);
            }

            try
            {
                _lastCounterCurve = FireSimulatorDiagnostics.RunCounterCurveAndWriteCsv();
            }
            catch (Exception ex)
            {
                _lastCounterCurve = new FireSimulatorDiagnostics.CounterCurveResult(CsvPath: $"ERROR: {ex.Message}", RowCount: 0);
            }

            try
            {
                _lastEndToEndCurve = FireSimulatorDiagnostics.RunEndToEndCurveAndWriteCsv();
            }
            catch (Exception ex)
            {
                _lastEndToEndCurve = new FireSimulatorDiagnostics.EndToEndCurveResult(CsvPath: $"ERROR: {ex.Message}", RowCount: 0);
            }

            BuildLines();

            ui.Clear();
            return PageResult.Stay;
        }
    }
}
