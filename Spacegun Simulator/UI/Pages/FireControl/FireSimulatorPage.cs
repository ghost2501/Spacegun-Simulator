using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
	public sealed class FireSimulatorPage : PageBase
	{
		public override string Id => PageId.FireSimulator;
		public override string Title => "SIMULATION (TEST MODE)";

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: true,
			ShowSidePanels: true,
			AutoSaveOnEnter: false,
			AutoSaveOnExit: false,
			FooterHint: "Digits+↩ (E)Edit (R)eset (B)ack (M)enu (Q)uit"
		);

		private enum Mode
		{
			InputDelay = 0,
			InputElevation = 1,
			InputAzimuth = 2,
			InputVelocity = 3,
			Result = 4
		}

		private const double Gravity = 9.81;

		// Persist last-test defaults like the legacy tool
		private static double s_lastDelayTime = 5.0;
		private static double s_lastElevation = 30.0;
		private static double s_lastAzimuth = 0.0;
		private static double s_lastVelocity = -1.0; // -1 means "use max"

		private Mode _mode;
		private DifficultyConfig? _diff;
		private Vector3 _enemyPos;
		private Vector3 _enemyVel;
		private double _projectileMassKg;
		private double _muzzleVelocity;

		private double _delayTime;
		private double _elevation;
		private double _azimuth;
		private double _velocity;

		private string _inputBuffer = "";
		private string _message = "";
		private readonly List<string> _lines = new();
		private int _scroll;
		private List<string> _resultLines = new();

		public override void OnEnter(UiContext ui)
		{
			var game = ui.Game;
			var firingProblem = game?.CurrentFiringProblem;
			var spec = game?.SelectedGunProjectileSpec;

			if (game == null || firingProblem == null || spec == null)
			{
				_diff = null;
				_message = "✗ Firing context not initialized.";
				_mode = Mode.InputDelay;
				BuildLines();
				_scroll = 0;
				return;
			}

			_diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
			_enemyPos = firingProblem.EnemyPosition;
			_enemyVel = firingProblem.EnemyVelocity;
			_projectileMassKg = spec.ProjectileMassKg;
			_muzzleVelocity = spec.MuzzleVelocityMs;

			if (s_lastVelocity < 0 || s_lastVelocity > _muzzleVelocity)
				s_lastVelocity = _muzzleVelocity;

			_delayTime = s_lastDelayTime;
			_elevation = s_lastElevation;
			_azimuth = s_lastAzimuth;
			_velocity = s_lastVelocity;
			_mode = Mode.InputDelay;
			_inputBuffer = "";
			_message = "";
			_resultLines = new List<string>();
			BuildLines();
			_scroll = 0;
		}

		private void BuildLines()
		{
			_lines.Clear();
			var diff = _diff;
			if (diff == null)
			{
				_lines.Add("✗ Fire Simulator unavailable.".PadRight(60));
				_lines.Add("");
				_lines.Add(Clamp60(_message));
				return;
			}

			_lines.Add(Clamp60($"Difficulty: {diff.DisplayName}"));
			_lines.Add("PRECISION REQUIREMENTS:".PadRight(60));
			foreach (var line in (diff.GetPrecisionSummary() ?? "").Split('\n'))
				_lines.Add(Clamp60(line));
			_lines.Add("");

			_lines.Add("=== TEST PARAMETERS ===".PadRight(60));
			_lines.Add(Clamp60($"Launch Delay: {diff.FormatLaunchDelay(_delayTime)}"));
			_lines.Add(Clamp60($"Elevation: {diff.FormatElevation(_elevation)}"));
			_lines.Add(Clamp60($"Azimuth: {diff.FormatAzimuth(_azimuth)}"));
			_lines.Add(Clamp60($"Velocity: {diff.FormatVelocity(_velocity)}"));
			_lines.Add(Clamp60($"Projectile Mass: {diff.MassPrecision.Format(_projectileMassKg)} kg"));
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
					_lines.Add(Clamp60($"Default: {diff.FormatLaunchDelay(_delayTime)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;
				case Mode.InputElevation:
					_lines.Add("Enter elevation (-90 to 90 degrees).".PadRight(60));
					_lines.Add(Clamp60($"Default: {diff.FormatElevation(_elevation)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;
				case Mode.InputAzimuth:
					_lines.Add("Enter azimuth (0 to <360 degrees).".PadRight(60));
					_lines.Add(Clamp60($"Default: {diff.FormatAzimuth(_azimuth)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;
				case Mode.InputVelocity:
					_lines.Add(Clamp60($"Enter launch velocity (0-{diff.VelocityPrecision.Format(_muzzleVelocity)} m/s)."));
					_lines.Add(Clamp60($"Default: {diff.VelocityPrecision.Format(_velocity)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;
				case Mode.Result:
					foreach (var l in _resultLines)
						_lines.Add(Clamp60(l));
					_lines.Add("");
					_lines.Add("[↩]=Back  [R]=Reset  [E]=Edit".PadRight(60));
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
			if (key.Key == ConsoleKey.B)
				return PageResult.Back();

			const int lineStep = 1;
			const int pageStep = 6;

			switch (key.Key)
			{
				case ConsoleKey.UpArrow: _scroll -= lineStep; return PageResult.Stay;
				case ConsoleKey.DownArrow: _scroll += lineStep; return PageResult.Stay;
				case ConsoleKey.PageUp: _scroll -= pageStep; return PageResult.Stay;
				case ConsoleKey.PageDown: _scroll += pageStep; return PageResult.Stay;
			}

			if (_diff == null)
				return PageResult.Exit;

			if (_mode == Mode.Result)
			{
				if (key.Key == ConsoleKey.Enter)
					return PageResult.Back();

				if (key.Key == ConsoleKey.R)
				{
					_delayTime = s_lastDelayTime;
					_elevation = s_lastElevation;
					_azimuth = s_lastAzimuth;
					_velocity = s_lastVelocity;
					_mode = Mode.InputDelay;
					_inputBuffer = "";
					_message = "";
					_resultLines = new List<string>();
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
				}
				if (key.Key == ConsoleKey.E)
				{
					_mode = Mode.InputDelay;
					_inputBuffer = "";
					_message = "";
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
				}
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

			if (ch == '-' && (_mode == Mode.InputElevation) && _inputBuffer.Length == 0)
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
						if (!TryAcceptDouble(_inputBuffer, out double d, fallback: _delayTime) || d < 0)
						{
							_message = "✗ Invalid delay (>=0).";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_delayTime = d;
						_inputBuffer = "";
						_mode = Mode.InputElevation;
						BuildLines();
						return PageResult.Stay;

					case Mode.InputElevation:
						if (!TryAcceptDouble(_inputBuffer, out double e, fallback: _elevation) || e < -90 || e > 90)
						{
							_message = "✗ Invalid elevation (-90..90).";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_elevation = e;
						_inputBuffer = "";
						_mode = Mode.InputAzimuth;
						BuildLines();
						return PageResult.Stay;

					case Mode.InputAzimuth:
						if (!TryAcceptDouble(_inputBuffer, out double a, fallback: _azimuth) || a < 0 || a >= 360)
						{
							_message = "✗ Invalid azimuth (0..359.999).";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_azimuth = a;
						_inputBuffer = "";
						_mode = Mode.InputVelocity;
						BuildLines();
						return PageResult.Stay;

					case Mode.InputVelocity:
						if (!TryAcceptDouble(_inputBuffer, out double v, fallback: _velocity) || v < 0 || v > _muzzleVelocity)
						{
							_message = "✗ Invalid velocity.";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_velocity = v;

						// Store defaults
						s_lastDelayTime = _delayTime;
						s_lastElevation = _elevation;
						s_lastAzimuth = _azimuth;
						s_lastVelocity = _velocity;

						_resultLines = BuildSimulationLines(_diff!, _enemyPos, _enemyVel, _delayTime, _elevation, _azimuth, _velocity, _projectileMassKg);
						_mode = Mode.Result;
						_inputBuffer = "";
						_scroll = 0;
						BuildLines();
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

		private static double CalculateMaxFlightTime(double elevationDegrees, double velocity)
		{
			double elevationRad = elevationDegrees * Math.PI / 180.0;
			double verticalVelocity = velocity * Math.Sin(elevationRad);

			if (verticalVelocity > 0)
				return 2.0 * verticalVelocity / Gravity;
			if (verticalVelocity < 0)
				return -verticalVelocity / Gravity;
			return 30.0;
		}

		private static List<string> BuildSimulationLines(
			DifficultyConfig diff,
			Vector3 enemyPosition,
			Vector3 enemyVelocity,
			double launchDelayTime,
			double elevationDegrees,
			double azimuthDegrees,
			double launchVelocity,
			double projectileMassKg)
		{
			var lines = new List<string>();

			double maxFlightTime = CalculateMaxFlightTime(elevationDegrees, launchVelocity);
			lines.Add("=== SIMULATION RESULTS ===");
			lines.Add("(Projectile vs Target Trajectory)".PadRight(60));
			lines.Add("");
			lines.Add(Clamp60($"Launch Delay: {diff.FormatLaunchDelay(launchDelayTime)}  Elev: {diff.FormatElevation(elevationDegrees)}"));
			lines.Add(Clamp60($"Az: {diff.FormatAzimuth(azimuthDegrees)}  Vel: {diff.FormatVelocity(launchVelocity)}"));
			lines.Add(Clamp60($"Projectile Mass: {diff.MassPrecision.Format(projectileMassKg)} kg"));
			lines.Add(Clamp60($"Max Flight Time: {diff.FormatLaunchDelay(maxFlightTime)}"));
			lines.Add("");

			lines.Add("Time | Projectile Range | Target Range".PadRight(60));
			lines.Add("-----+-----------------+------------".PadRight(60));

			double displayInterval = 2.0;
			for (double t = 0; t <= maxFlightTime + launchDelayTime + 5.0; t += displayInterval)
			{
				double projectileTime = t - launchDelayTime;
				Vector3 projectilePos;
				double projectileRange;

				if (projectileTime >= 0)
				{
					projectilePos = BallisticsCalculator.CalculateProjectilePositionStatic(projectileTime, launchVelocity, elevationDegrees, azimuthDegrees);
					projectileRange = projectilePos.Magnitude;
				}
				else
				{
					projectilePos = Vector3.Zero;
					projectileRange = 0;
				}

				Vector3 targetPos = enemyPosition + (enemyVelocity * t);
				double targetRange = targetPos.Magnitude;

				lines.Add(Clamp60($"{diff.LaunchDelayPrecision.Format(t)}s | {diff.FormatDistance(projectileRange),15} | {diff.FormatDistance(targetRange),10}"));

				if (projectileTime > 30.0 || projectileRange > 3_000_000)
					break;
			}

			lines.Add("");
			lines.Add("INTERPRETATION NOTES:".PadRight(60));
			lines.Add("  • Compare ranges over time".PadRight(60));
			lines.Add("  • Intercept when ranges match".PadRight(60));
			lines.Add("  • Adjust delay if needed".PadRight(60));
			return lines;
		}
	}
}
