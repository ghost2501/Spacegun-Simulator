using Spacegun_Simulator.FireControlTools;
using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
	public sealed class BallisticsTablesPage : PageBase
	{
		public override string Id => PageId.BallisticsTables;
		public override string Title => "CALCULATE REQUIREMENTS";

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: true,
			ShowSidePanels: true,
			AutoSaveOnEnter: false,
			AutoSaveOnExit: false,
			FooterHint: "[1-4]=Tables  [5]=All  Enter/B=Back  Arrows=Scroll  Esc=Menu  Q=Quit"
		);

		private enum Mode
		{
			Menu = 0,
			TimeOfFlight = 1,
			GravityDrop = 2,
			KineticEnergy = 3,
			RangeCoverage = 4,
			All = 5
		}

		private Mode _mode;
		private DifficultyConfig? _diff;
		private int _tierIndex;
		private GameDifficulty _difficulty;
		private double _gunRangeMeters;
		private double _tierVelMin;
		private double _tierVelMax;

		private readonly List<string> _lines = new();
		private int _scroll;
		private string _message = "";

		public override void OnEnter(UiContext ui)
		{
			var game = ui.Game;
			if (game == null)
			{
				_diff = null;
				_message = "✗ No active game session.";
				_mode = Mode.Menu;
				BuildLines();
				_scroll = 0;
				return;
			}

			_difficulty = game.SelectedDifficulty;
			_diff = DifficultyConfig.GetConfig(_difficulty);
			_tierIndex = game.CurrentWaveNumber > 0 ? GameConstants.GetTierForWave(game.CurrentWaveNumber).TierIndex : 0;
			var tier = GameConstants.WaveTiers[Math.Min(_tierIndex, 3)];
			_gunRangeMeters = tier.MaxEffectiveGunRange;
			_tierVelMin = tier.VelocityMin;
			_tierVelMax = tier.VelocityMax;

			_mode = Mode.Menu;
			_message = "";
			BuildLines();
			_scroll = 0;
		}

		private void BuildLines()
		{
			_lines.Clear();
			var diff = _diff;
			if (diff == null)
			{
				_lines.Add("✗ Ballistics Tables unavailable.".PadRight(60));
				_lines.Add("");
				_lines.Add(Clamp60(_message));
				return;
			}

			_lines.Add(Clamp60($"Tier {_tierIndex} | Difficulty: {diff.DisplayName}"));
			_lines.Add(Clamp60($"Enemy Velocity: {GameConstants.FormatVelocity(_tierVelMin)}-{GameConstants.FormatVelocity(_tierVelMax)}"));
			_lines.Add(Clamp60($"Gun Range: {GameConstants.FormatDistance(_gunRangeMeters)}"));
			_lines.Add("");
			_lines.Add("PRECISION REQUIREMENTS:".PadRight(60));
			foreach (var line in (diff.GetPrecisionSummary() ?? "").Split('\n'))
				_lines.Add(Clamp60(line));
			_lines.Add("");

			if (!string.IsNullOrWhiteSpace(_message))
			{
				_lines.Add(Clamp60(_message));
				_lines.Add("");
			}

			switch (_mode)
			{
				case Mode.Menu:
					_lines.Add("SELECT A REFERENCE TABLE:".PadRight(60));
					_lines.Add("[1] Time-of-Flight".PadRight(60));
					_lines.Add("[2] Gravity Drop".PadRight(60));
					_lines.Add("[3] Kinetic Energy".PadRight(60));
					_lines.Add("[4] Range Coverage".PadRight(60));
					_lines.Add("[5] View All".PadRight(60));
					_lines.Add("".PadRight(60));
					_lines.Add("Tip: Use arrows to scroll tables.".PadRight(60));
					break;

				case Mode.TimeOfFlight:
					AppendSection(BuildTimeOfFlightLines(diff));
					break;
				case Mode.GravityDrop:
					AppendSection(BuildGravityDropLines(diff));
					break;
				case Mode.KineticEnergy:
					AppendSection(BuildKineticEnergyLines(diff));
					break;
				case Mode.RangeCoverage:
					AppendSection(BuildRangeCoverageLines(diff));
					break;
				case Mode.All:
					AppendSection(BuildTimeOfFlightLines(diff));
					_lines.Add("");
					AppendSection(BuildGravityDropLines(diff));
					_lines.Add("");
					AppendSection(BuildKineticEnergyLines(diff));
					_lines.Add("");
					AppendSection(BuildRangeCoverageLines(diff));
					break;
			}
		}

		private void AppendSection(IEnumerable<string> sectionLines)
		{
			foreach (var line in sectionLines)
				_lines.Add(Clamp60(line));
			_lines.Add("");
			_lines.Add("[Enter/B]=Back".PadRight(60));
		}

		private IEnumerable<string> BuildTimeOfFlightLines(DifficultyConfig diff)
		{
			yield return "=== TABLE 1: TIME-OF-FLIGHT (COMPACT) ===";
			yield return "Times depend on elevation + range.";
			yield return "";

			var tier = GameConstants.WaveTiers[Math.Min(_tierIndex, 3)];
			double minVel = tier.VelocityMin;
			double maxVel = tier.VelocityMax;
			double gunRange = tier.MaxEffectiveGunRange;

			double[] velocities =
			{
				minVel,
				minVel + (maxVel - minVel) * 0.33,
				minVel + (maxVel - minVel) * 0.66,
				maxVel
			};

			double[] ranges =
			{
				gunRange * 0.50,
				gunRange * 0.75,
				gunRange * 0.95
			};

			float[] elevations = { 15f, 30f, 45f, 60f };

			foreach (var elev in elevations)
			{
				yield return $"ELEVATION {diff.ElevationPrecision.Format(elev)}°";
				foreach (var range in ranges)
				{
					yield return $" Range {diff.FormatDistance(range)}:";
					foreach (var vel in velocities)
					{
						float tof = BallisticsTablesReference.CalculateTimeOfFlight((float)vel, (float)range, elev);
						yield return $"  v={GameConstants.FormatVelocity(vel)} -> t={diff.LaunchDelayPrecision.Format(tof)}s";
					}
				}
				yield return "";
			}
		}

		private IEnumerable<string> BuildGravityDropLines(DifficultyConfig diff)
		{
			yield return "=== TABLE 2: GRAVITY DROP (COMPACT) ===";
			var tier = GameConstants.WaveTiers[Math.Min(_tierIndex, 3)];
			double engagementRange = tier.MaxEffectiveGunRange * 0.50;
			yield return $"Ref range: {diff.FormatDistance(engagementRange)}";
			yield return "t      | drop      | %range";
			yield return "-------+-----------+--------";

			float[] times = tier.TierIndex switch
			{
				0 => new float[] { 0.1f, 0.5f, 1f, 2f, 5f, 10f, 15f, 20f, 25f, 30f },
				1 => new float[] { 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 7.5f, 10f },
				2 => new float[] { 0.001f, 0.005f, 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f },
				3 => new float[] { 0.0001f, 0.0005f, 0.001f, 0.005f, 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 1f },
				_ => new float[] { 0.1f, 0.5f, 1f, 2f, 5f, 10f, 15f, 20f, 25f, 30f }
			};

			foreach (var t in times)
			{
				float dropM = BallisticsTablesReference.CalculateGravityDrop(t);
				double pct = BallisticsTablesReference.CalculateGravityDropPercentage(t, engagementRange);
				string timeStr = diff.LaunchDelayPrecision.Format(t) + "s";
				string dropStr = diff.DistancePrecision.Format(dropM) + "m";
				string pctStr = pct.ToString($"F{Math.Max(1, diff.DistancePrecision.DecimalPlaces)}") + "%";
				yield return $"{timeStr,-6} | {dropStr,9} | {pctStr,6}";
			}
		}

		private IEnumerable<string> BuildKineticEnergyLines(DifficultyConfig diff)
		{
			yield return "=== TABLE 3: KINETIC ENERGY (COMPACT) ===";
			var tier = GameConstants.WaveTiers[Math.Min(_tierIndex, 3)];
			yield return $"Velocity range: {GameConstants.FormatVelocity(tier.VelocityMin)}-{GameConstants.FormatVelocity(tier.VelocityMax)}";
			yield return "";

			double[] velocities =
			{
				tier.VelocityMin,
				tier.VelocityMin + (tier.VelocityMax - tier.VelocityMin) * 0.50,
				tier.VelocityMax
			};
			double[] masses = { 10, 25, 50, 100 };

			foreach (var mass in masses)
			{
				yield return $"Mass {diff.MassPrecision.Format(mass)} kg";
				foreach (var vel in velocities)
				{
					double energy = BallisticsTablesReference.CalculateKineticEnergyMJ(mass, vel);
					string energyStr = energy < 1_000_000
						? $"{diff.EnergyPrecision.Format(energy)} MJ"
						: $"{(energy / 1_000_000).ToString($"F{diff.EnergyPrecision.DecimalPlaces}")} PJ";
					yield return $"  v={GameConstants.FormatVelocity(vel)} -> {energyStr}";
				}
				yield return "";
			}
		}

		private IEnumerable<string> BuildRangeCoverageLines(DifficultyConfig diff)
		{
			yield return "=== TABLE 4: RANGE COVERAGE (COMPACT) ===";
			var tier = GameConstants.WaveTiers[Math.Min(_tierIndex, 3)];
			yield return $"Gun Range: {GameConstants.FormatDistance(tier.MaxEffectiveGunRange)}";
			yield return "";

			float[] times = tier.TierIndex switch
			{
				0 => new float[] { 1f, 5f, 10f, 20f, 30f },
				1 => new float[] { 0.1f, 0.5f, 1f, 5f, 10f },
				2 => new float[] { 0.01f, 0.05f, 0.1f, 1f, 5f },
				3 => new float[] { 0.001f, 0.005f, 0.01f, 0.1f, 1f },
				_ => new float[] { 1f, 5f, 10f, 20f, 30f }
			};

			double[] velocities =
			{
				tier.VelocityMin,
				tier.VelocityMin + (tier.VelocityMax - tier.VelocityMin) * 0.50,
				tier.VelocityMax
			};

			foreach (var vel in velocities)
			{
				yield return $"Velocity {GameConstants.FormatVelocity(vel)}";
				foreach (var t in times)
				{
					double dist = vel * t;
					yield return $"  t={diff.LaunchDelayPrecision.Format(t)}s -> {diff.FormatDistance(dist)}";
				}
				yield return "";
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
			if (key.Key is ConsoleKey.B or ConsoleKey.Enter)
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

			// Note: selecting a table is always available via 1-5, even while viewing a table.

			switch (key.Key)
			{
				case ConsoleKey.D1:
				case ConsoleKey.NumPad1:
					_mode = Mode.TimeOfFlight;
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
				case ConsoleKey.D2:
				case ConsoleKey.NumPad2:
					_mode = Mode.GravityDrop;
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
				case ConsoleKey.D3:
				case ConsoleKey.NumPad3:
					_mode = Mode.KineticEnergy;
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
				case ConsoleKey.D4:
				case ConsoleKey.NumPad4:
					_mode = Mode.RangeCoverage;
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
				case ConsoleKey.D5:
				case ConsoleKey.NumPad5:
					_mode = Mode.All;
					_scroll = 0;
					BuildLines();
					return PageResult.Stay;
			}

			return PageResult.Stay;
		}
	}
}
