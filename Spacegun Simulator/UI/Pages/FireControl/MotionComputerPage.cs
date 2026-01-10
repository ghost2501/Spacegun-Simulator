using Spacegun_Simulator.FireControlTools;
using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
	public sealed class MotionComputerPage : PageBase
	{
		public override string Id => PageId.MotionComputer;
		public override string Title => "PREDICT TARGET POSITION";

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: true,
			ShowSidePanels: true,
			AutoSaveOnEnter: false,
			AutoSaveOnExit: false,
			FooterHint: "Seconds+↩ ↩(empty)/(B)ack (R)Timeline (M)enu (Q)uit"
		);

		private enum Mode
		{
			Timeline = 0,
			Result = 1
		}

		private Mode _mode;
		private DifficultyConfig? _diff;
		private Vector3 _pos;
		private Vector3 _vel;
		private double _gunRangeMeters;

		private readonly List<string> _lines = new();
		private int _scroll;
		private string _inputBuffer = "";
		private TargetMotionComputer.MotionPredictionResult? _result;

		public override void OnEnter(UiContext ui)
		{
			var game = ui.Game;
			var firingProblem = game?.CurrentFiringProblem;
			if (game == null || firingProblem == null)
			{
				_mode = Mode.Timeline;
				_diff = null;
				_pos = Vector3.Zero;
				_vel = Vector3.Zero;
				_gunRangeMeters = 0;
				_inputBuffer = "";
				_result = null;
				BuildUnavailableLines();
				_scroll = 0;
				return;
			}

			_diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
			_pos = firingProblem.EnemyPosition;
			_vel = firingProblem.EnemyVelocity;
			_gunRangeMeters = _diff.IsTutorialMode
				? DifficultyConfig.TutorialPotatoCannon.EffectiveRangeMeters
				: game.GetCurrentEffectiveGunRangeMeters();
			_mode = Mode.Timeline;
			_inputBuffer = "";
			_result = null;
			BuildLines();
			_scroll = 0;
		}

		private void BuildUnavailableLines()
		{
			_lines.Clear();
			_lines.Add("✗ Motion Computer unavailable.".PadRight(60));
			_lines.Add("");
			_lines.Add("Firing problem not initialized.".PadRight(60));
			_lines.Add("");
			_lines.Add("Press [Esc] to return to menu.".PadRight(60));
		}

		private void BuildLines()
		{
			_lines.Clear();

			var diff = _diff;
			if (diff == null)
			{
				BuildUnavailableLines();
				return;
			}

			_lines.Add(Clamp60($"Difficulty: {diff.DisplayName}"));
			_lines.Add(Clamp60($"Precision: {diff.LaunchDelayPrecision.DecimalPlaces} decimals for time"));
			_lines.Add("");

			_lines.Add("=== CURRENT TARGET STATE (T=0) ===");
			_lines.Add(Clamp60($"Position: {diff.FormatVector3(_pos)}"));
			_lines.Add(Clamp60($"Distance from origin: {diff.FormatDistance(_pos.Magnitude)}"));
			_lines.Add(Clamp60($"Velocity: {diff.FormatVelocityVector(_vel)}"));
			_lines.Add(Clamp60($"Speed: {diff.FormatVelocity(_vel.Magnitude)}"));
			_lines.Add("");

			switch (_mode)
			{
				case Mode.Timeline:
					BuildTimelineLines(diff);
					_lines.Add("");
					_lines.Add("=== QUERY SPECIFIC TIME ===");
					_lines.Add("Type a time offset (seconds) and press [Enter].");
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;

				case Mode.Result:
					BuildResultLines(diff);
					_lines.Add("");
					_lines.Add("[Enter]=Back   [R]=Timeline".PadRight(60));
					break;
			}
		}

		private void BuildTimelineLines(DifficultyConfig diff)
		{
			_lines.Add("=== TARGET RANGE TIMELINE (T+0s to T+20s) ===");
			_lines.Add("");
			_lines.Add("Time | Target Range      | Status".PadRight(60));
			_lines.Add("-----+-------------------+----------------------".PadRight(60));

			double gunRange = _gunRangeMeters;
			for (int t = 0; t <= 20; t++)
			{
				var r = TargetMotionComputer.CalculateMotionAtTime(_pos, _vel, t);
				bool inRange = r.PredictedDistance <= gunRange;
				string status = inRange ? "IN RANGE" : "OUT OF RANGE";
				string rangeStr = diff.FormatDistance(r.PredictedDistance);
				_lines.Add(Clamp60($"{t,2}s  | {rangeStr,17} | {status}"));
			}
		}

		private void BuildResultLines(DifficultyConfig diff)
		{
			var r = _result;
			if (r == null)
			{
				_lines.Add("(No result computed.)".PadRight(60));
				return;
			}

			_lines.Add("=== MOTION COMPUTER RESULT ===");
			_lines.Add("");
			_lines.Add(Clamp60($"Prediction at T+{diff.LaunchDelayPrecision.Format(r.TimeOffset)}s"));
			_lines.Add("");
			_lines.Add("Predicted Position:".PadRight(60));
			_lines.Add(Clamp60($"  {diff.FormatVector3(r.PredictedPosition)}"));
			_lines.Add(Clamp60($"  Range: {diff.FormatDistance(r.PredictedDistance)}"));
			_lines.Add(Clamp60($"  Elevation: {diff.FormatElevation(r.PredictedElevation)}"));
			_lines.Add(Clamp60($"  Azimuth: {diff.FormatAzimuth(r.PredictedAzimuth)}"));
			_lines.Add("");
			_lines.Add("Distance Traveled:".PadRight(60));
			_lines.Add(Clamp60($"  {diff.FormatVector3(r.DistanceTraveled)}"));
			_lines.Add(Clamp60($"  Total: {diff.FormatDistance(r.DistanceTraveled.Magnitude)}"));
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

			switch (key.Key)
			{
				case ConsoleKey.UpArrow: _scroll -= lineStep; return PageResult.Stay;
				case ConsoleKey.DownArrow: _scroll += lineStep; return PageResult.Stay;
				case ConsoleKey.PageUp: _scroll -= pageStep; return PageResult.Stay;
				case ConsoleKey.PageDown: _scroll += pageStep; return PageResult.Stay;
				case ConsoleKey.R:
					_mode = Mode.Timeline;
					_result = null;
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
			}

			if (_diff == null)
				return PageResult.Stay;

			if (key.Key == ConsoleKey.B)
				return PageResult.Back();

			if (_mode == Mode.Result && key.Key == ConsoleKey.Enter)
				return PageResult.Back();

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

			if (key.Key == ConsoleKey.Enter)
			{
				if (string.IsNullOrWhiteSpace(_inputBuffer))
					return PageResult.Back();

				if (!double.TryParse(_inputBuffer, out double t) || t < 0)
				{
					_lines.Add(Clamp60("✗ Invalid time. Enter a non-negative number."));
					_scroll = 0;
					_inputBuffer = "";
					BuildLines();
					return PageResult.Stay;
				}

				_result = TargetMotionComputer.CalculateMotionAtTime(_pos, _vel, t);
				_mode = Mode.Result;
				_inputBuffer = "";
				_scroll = 0;
				BuildLines();
				return PageResult.Stay;
			}

			return PageResult.Stay;
		}
	}
}
