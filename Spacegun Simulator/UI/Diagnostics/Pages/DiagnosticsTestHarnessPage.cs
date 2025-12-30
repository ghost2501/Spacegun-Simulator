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

        public override void OnEnter(UiContext ui)
        {
            _lastChecks = null;
            _lastAudit = null;
            BuildLines();
        }

        private void BuildLines()
        {
            _lines.Clear();
            _lines.Add("Runs automated diagnostics checks.".PadRight(60));
            _lines.Add("");

            if (_lastChecks == null && _lastAudit == null)
            {
                _lines.Add("Press Enter to run checks and export CSV.".PadRight(60));
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
                if (audit.ScenarioCount <= 0 || string.IsNullOrWhiteSpace(audit.CsvPath))
                {
                    _lines.Add("Tech audit: no scenarios found.".PadRight(60));
                }
                else
                {
                    _lines.Add(Clamp60($"Tech audit: {audit.ScenarioCount} scenarios"));
                    _lines.Add(Clamp60($"CSV: {audit.CsvPath}"));
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

            BuildLines();

            ui.Clear();
            return PageResult.Stay;
        }
    }
}
