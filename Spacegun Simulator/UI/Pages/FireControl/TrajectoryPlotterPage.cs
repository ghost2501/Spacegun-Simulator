using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.FireControlTools;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
	public sealed class TrajectoryPlotterPage : PageBase
	{
		public override string Id => PageId.TrajectoryPlotter;
		public override string Title => "PLAN TRAJECTORY";

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: true,
			ShowSidePanels: true,
			AutoSaveOnEnter: false,
			AutoSaveOnExit: false,
			FooterHint: "Digits+Enter=Accept  B=Back  Enter(Result)=Back  Backspace=Edit  Esc=Menu  Q=Quit"
		);

		private enum Mode
		{
			InputVelocity = 0,
			InputElevation = 1,
			InputAzimuth = 2,
			InputFlightTime = 3,
			Result = 4
		}

		// Persist the last test parameters (mimics the legacy tool's behavior across page opens)
		private static float s_lastLaunchVelocity = 200_000f;
		private static float s_lastElevationDegrees = 45f;
		private static float s_lastAzimuthDegrees = 0f;
		private static float s_lastFlightTime = 10f;
		private static bool s_hasLastTest;

		private Mode _mode;
		private DifficultyConfig? _diff;
		private string _inputBuffer = "";
		private string _message = "";
		private readonly List<string> _lines = new();
		private int _scroll;

		private float _launchVelocity;
		private float _elevation;
		private float _azimuth;
		private float _flightTime;
		private TrajectoryPlotter.TrajectoryResult? _result;

		public override void OnEnter(UiContext ui)
		{
			var game = ui.Game;
			if (game == null)
			{
				_diff = null;
				_mode = Mode.InputVelocity;
				_message = "✗ No active game session.";
				BuildLines();
				_scroll = 0;
				return;
			}

			_diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
			InitDefaultsIfNeeded(_diff);

			_launchVelocity = s_lastLaunchVelocity;
			_elevation = s_lastElevationDegrees;
			_azimuth = s_lastAzimuthDegrees;
			_flightTime = s_lastFlightTime;
			_result = null;
			_mode = Mode.InputVelocity;
			_inputBuffer = "";
			_message = "";
			BuildLines();
			_scroll = 0;
		}

		private static void InitDefaultsIfNeeded(DifficultyConfig diff)
		{
			if (s_hasLastTest)
				return;

			if (diff.IsTutorialMode)
			{
				s_lastLaunchVelocity = (float)DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs;
				s_lastElevationDegrees = 45f;
				s_lastAzimuthDegrees = 0f;
				s_lastFlightTime = 2f;
			}
			else
			{
				s_lastLaunchVelocity = 200_000f;
				s_lastElevationDegrees = 45f;
				s_lastAzimuthDegrees = 0f;
				s_lastFlightTime = 10f;
			}
		}

		// Removed duplicated Clamp60 method; using inherited Clamp60 instead.

		private void BuildLines()
		{
			_lines.Clear();
			var diff = _diff;
			if (diff == null)
			{
				_lines.Add("✗ Trajectory Plotter unavailable.".PadRight(60));
				_lines.Add("");
				_lines.Add(Clamp60(_message));
				return;
			}

			_lines.Add(Clamp60($"Difficulty: {diff.DisplayName}"));
			_lines.Add("PRECISION REQUIREMENTS:".PadRight(60));
			foreach (var line in (diff.GetPrecisionSummary() ?? "").Split('\n'))
				_lines.Add(Clamp60(line));
			_lines.Add("");

			_lines.Add("=== LAUNCH PARAMETERS ===");
			_lines.Add(Clamp60($"Velocity: {diff.FormatVelocity(_launchVelocity)}"));
			_lines.Add(Clamp60($"Elevation: {diff.FormatElevation(_elevation)}"));
			_lines.Add(Clamp60($"Azimuth: {diff.FormatAzimuth(_azimuth)}"));
			_lines.Add(Clamp60($"Flight time: {diff.FormatLaunchDelay(_flightTime)}"));
			_lines.Add("");

			if (!string.IsNullOrWhiteSpace(_message))
			{
				_lines.Add(Clamp60(_message));
				_lines.Add("");
			}

			switch (_mode)
			{
				case Mode.InputVelocity:
					_lines.Add("Enter launch velocity (m/s).".PadRight(60));
					_lines.Add(Clamp60($"Default: {diff.VelocityPrecision.Format(_launchVelocity)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;

				case Mode.InputElevation:
					_lines.Add("Enter elevation (-90 to 90 degrees).".PadRight(60));
					_lines.Add(Clamp60($"Default: {diff.ElevationPrecision.Format(_elevation)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;

				case Mode.InputAzimuth:
					_lines.Add("Enter azimuth (0 to <360 degrees).".PadRight(60));
					_lines.Add(Clamp60($"Default: {diff.AzimuthPrecision.Format(_azimuth)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;

				case Mode.InputFlightTime:
					_lines.Add("Enter flight time (seconds).".PadRight(60));
					_lines.Add(Clamp60($"Default: {diff.LaunchDelayPrecision.Format(_flightTime)}"));
					_lines.Add(Clamp60($"> {_inputBuffer}"));
					break;

				case Mode.Result:
					BuildResultLines(diff);
					_lines.Add("");
					_lines.Add("[Enter]=Back  [M]=Modify  Arrows=Scroll".PadRight(60));
					break;
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

			_lines.Add("=== TRAJECTORY RESULT ===".PadRight(60));
			_lines.Add("");
			_lines.Add("INPUT PARAMETERS:".PadRight(60));
			_lines.Add(Clamp60($"Launch Velocity: {diff.FormatVelocity(r.LaunchVelocity)}"));
			_lines.Add(Clamp60($"Elevation Angle: {diff.FormatElevation(r.ElevationAngle)}"));
			_lines.Add(Clamp60($"Azimuth Bearing: {diff.FormatAzimuth(r.AzimuthAngle)}"));
			_lines.Add(Clamp60($"Flight Time: {diff.FormatLaunchDelay(r.FlightTime)}"));
			_lines.Add("");

			_lines.Add(Clamp60($"PROJECTILE POSITION AT T+{diff.LaunchDelayPrecision.Format(r.FlightTime)}s"));
			_lines.Add("");
			_lines.Add(Clamp60($"Position: {diff.FormatVector3(r.ProjectilePosition)}"));
			_lines.Add(Clamp60($"Range (3D): {diff.FormatDistance(r.RangeFromOrigin)}"));
			_lines.Add(Clamp60($"Horizontal distance: {diff.FormatDistance(r.HorizontalDistance)}"));
			_lines.Add(Clamp60($"Altitude: {diff.FormatDistance(r.ProjectilePosition.Z)}"));
			_lines.Add(Clamp60($"Gravitational drop: {diff.FormatDistance(r.GravitationalDrop)}"));
			_lines.Add("");
			_lines.Add("FLIGHT CHARACTERISTICS:".PadRight(60));
			_lines.Add(Clamp60($"Maximum altitude: {diff.FormatDistance(r.MaxAltitudeReached)}"));
			_lines.Add(Clamp60($"Time to max altitude: {diff.FormatLaunchDelay(r.TimeToMaxAltitude)}"));
			if (r.ProjectilePosition.Z < 0)
				_lines.Add("WARNING: Projectile below start altitude".PadRight(60));
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

				if (key.Key == ConsoleKey.M)
				{
					// Modify last (start at first field, retain values)
					_mode = Mode.InputVelocity;
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
				if (_inputBuffer.Length < 16)
					_inputBuffer += ch;
				BuildLines();
				return PageResult.Stay;
			}

			if (ch == '.' && !_inputBuffer.Contains('.'))
			{
				if (_inputBuffer.Length < 16)
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
					case Mode.InputVelocity:
						if (!TryAcceptFloat(_inputBuffer, out float v, fallback: _launchVelocity, requirePositive: true))
						{
							_message = "✗ Invalid velocity.";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_launchVelocity = v;
						_inputBuffer = "";
						_mode = Mode.InputElevation;
						BuildLines();
						return PageResult.Stay;

					case Mode.InputElevation:
						if (!TryAcceptFloat(_inputBuffer, out float e, fallback: _elevation, requirePositive: false) || e < -90f || e > 90f)
						{
							_message = "✗ Invalid elevation (must be -90..90).";
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
						if (!TryAcceptFloat(_inputBuffer, out float a, fallback: _azimuth, requirePositive: false) || a < 0f || a >= 360f)
						{
							_message = "✗ Invalid azimuth (0..359.999).";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_azimuth = a;
						_inputBuffer = "";
						_mode = Mode.InputFlightTime;
						BuildLines();
						return PageResult.Stay;

					case Mode.InputFlightTime:
						if (!TryAcceptFloat(_inputBuffer, out float t, fallback: _flightTime, requirePositive: false) || t < 0f)
						{
							_message = "✗ Invalid flight time (>=0).";
							_inputBuffer = "";
							BuildLines();
							return PageResult.Stay;
						}
						_flightTime = t;

						// Store last-test defaults
						s_lastLaunchVelocity = _launchVelocity;
						s_lastElevationDegrees = _elevation;
						s_lastAzimuthDegrees = _azimuth;
						s_lastFlightTime = _flightTime;
						s_hasLastTest = true;

						_result = TrajectoryPlotter.CalculateTrajectory(_launchVelocity, _elevation, _azimuth, _flightTime);
						_mode = Mode.Result;
						_inputBuffer = "";
						_scroll = 0;
						BuildLines();
						return PageResult.Stay;
				}
			}

			return PageResult.Stay;
		}

		private static bool TryAcceptFloat(string input, out float value, float fallback, bool requirePositive)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				value = fallback;
				return true;
			}

			if (!float.TryParse(input, out value))
			{
				value = fallback;
				return false;
			}

			if (requirePositive && value <= 0)
				return false;

			return true;
		}
	}
}
