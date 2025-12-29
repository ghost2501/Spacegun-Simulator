using System;
using System.Collections.Generic;
using System.Threading;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
	public sealed class FiringVisualizationPage : PageBase
	{
		public override string Id => PageId.FiringVisualization;
		public override string Title => "FIRING VISUALIZATION";

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: true,
			ShowSidePanels: true,
			AutoSaveOnEnter: false,
			AutoSaveOnExit: false,
			FooterHint: "Any key=Continue  Esc=Menu  Q=Quit"
		);

		private readonly Vector3 _enemyPosition;
		private readonly Vector3 _enemyVelocity;
		private readonly double _launchDelayTime;
		private readonly double _elevation;
		private readonly double _azimuth;
		private readonly double _velocity;
		private readonly double _maxFlightTime;
		private readonly bool _isHit;

		private bool _rendered;

		public FiringVisualizationPage(
			Vector3 enemyPosition,
			Vector3 enemyVelocity,
			double launchDelayTime,
			double elevation,
			double azimuth,
			double velocity,
			double maxFlightTime,
			bool isHit)
		{
			_enemyPosition = enemyPosition;
			_enemyVelocity = enemyVelocity;
			_launchDelayTime = launchDelayTime;
			_elevation = elevation;
			_azimuth = azimuth;
			_velocity = velocity;
			_maxFlightTime = maxFlightTime;
			_isHit = isHit;
		}

		public override void Render(UiContext ui)
		{
			// This page renders an animated sequence synchronously.
			// UiController will read a key after Render returns.
			if (_rendered)
			{
				base.Render(ui);
				return;
			}

			_rendered = true;

			const int width = 60;
			const int height = 20;
			const double frameDelayMs = 50;

			double maxDistance = Math.Max(_enemyPosition.Magnitude * 1.2, 100);
			double scaleX = (width - 10) / maxDistance;
			double scaleY = (height - 4) / (maxDistance * 0.5);

			double timeStep = _maxFlightTime / 100.0;
			if (timeStep <= 0) timeStep = 0.01;

			var projectilePositions = new List<(double time, double x, double y)>();
			var targetPositions = new List<(double time, double x, double y)>();

			for (double t = 0; t <= _maxFlightTime; t += timeStep)
			{
				var projVec = BallisticsCalculator.CalculateProjectilePositionStatic(t, _velocity, _elevation, _azimuth);
				double projX = projVec.Magnitude;
				double projZ = projVec.Z;
				projectilePositions.Add((t, projX, projZ));

				double totalTime = _launchDelayTime + t;
				Vector3 targetPos = _enemyPosition + (_enemyVelocity * totalTime);
				double targetHorizontalDist = Math.Sqrt(targetPos.X * targetPos.X + targetPos.Y * targetPos.Y);
				targetPositions.Add((t, targetHorizontalDist, targetPos.Z));
			}

			var header = new List<string>();

			var art = ui.BuildHeaderArt?.Invoke(Id);
			if (art != null)
				foreach (var line in art) header.Add(line ?? "");

			header.Add("               FIRING VISUALIZATION                          ");
			header.Add(string.Empty);

			var (leftOverride, rightOverride) = ui.ResolveSideArt?.Invoke(Id) ?? (null, null);

			TextWriter? priorOut = null;
			bool priorCursorVisible = true;
			try
			{
				priorOut = Console.Out;
				try { priorCursorVisible = Console.CursorVisible; } catch { priorCursorVisible = true; }
				try { Console.CursorVisible = false; } catch { }

				// IMPORTANT: do NOT use BeginBufferedFrame here.
				// It installs a PageBuffer, which would buffer every animation frame and flush them at the end.
				Console.SetOut(ui.OriginalOut ?? Console.Out);
				Console.Clear();

				int startRow;
				try { startRow = Console.CursorTop; } catch { startRow = 0; }

				int targetRows;
				try { targetRows = Math.Max(0, (Console.WindowHeight - 2) - startRow); }
				catch { targetRows = header.Count; }

				var drawLines = new List<string>(header);
				while (drawLines.Count < targetRows)
					drawLines.Add("");

				ui.Layout.RenderWithSides_NoOffset(drawLines, leftOverride, rightOverride);

				int contentLeft = ui.Layout.CalculateContentLeft_NoOffset(header, leftOverride, rightOverride);
				int contentTop = startRow + header.Count;

				int left = contentLeft;
				int top = contentTop;

				char[,] buffer = new char[height, width];

				int frameCount = projectilePositions.Count;
				for (int frame = 0; frame < frameCount; frame++)
				{
					for (int row = 0; row < height; row++)
						for (int col = 0; col < width; col++)
							buffer[row, col] = ' ';

					for (int col = 0; col < width; col++)
						buffer[height - 1, col] = '─';

					buffer[height - 1, 0] = '└';
					buffer[0, 0] = '│';
					for (int row = 1; row < height - 1; row++)
						buffer[row, 0] = '│';

					for (int i = 0; i <= frame; i++)
					{
						var (_, tx, ty) = targetPositions[i];
						int targetCol = (int)(tx * scaleX) + 2;
						int targetRow = height - 2 - (int)(ty * scaleY);

						if (targetCol >= 0 && targetCol < width && targetRow >= 0 && targetRow < height - 1)
						{
							if (i == frame)
								buffer[targetRow, targetCol] = '●';
							else if (i % 5 == 0)
								buffer[targetRow, targetCol] = '·';
						}
					}

					for (int i = Math.Max(0, frame - 10); i <= frame; i++)
					{
						var (_, px, py) = projectilePositions[i];
						int projCol = (int)(px * scaleX) + 2;
						int projRow = height - 2 - (int)(py * scaleY);

						if (projCol >= 0 && projCol < width && projRow >= 0 && projRow < height - 1)
						{
							if (i == frame)
								buffer[projRow, projCol] = '◆';
							else
								buffer[projRow, projCol] = '·';
						}
					}

					buffer[height - 2, 2] = '▲';

					for (int row = 0; row < height; row++)
					{
						try { Console.SetCursorPosition(left, top + row); } catch { }
						for (int col = 0; col < width; col++)
							Console.Write(buffer[row, col]);
					}

					var (time, projDist, projAlt) = projectilePositions[frame];
					var (_, tgtDist, tgtAlt) = targetPositions[frame];
					double separation = Math.Sqrt(Math.Pow(projDist - tgtDist, 2) + Math.Pow(projAlt - tgtAlt, 2));

					try { Console.SetCursorPosition(left, top + height + 1); } catch { }
					Console.Write($"Time: {time:F2}s  |  Projectile: {projDist:F0}m @ {projAlt:F1}m".PadRight(width));

					try { Console.SetCursorPosition(left, top + height + 2); } catch { }
					Console.Write($"Target: {tgtDist:F0}m @ {tgtAlt:F1}m  |  Separation: {separation:F1}m".PadRight(width));

					try { Console.SetCursorPosition(left, top + height + 4); } catch { }
					Console.Write("◆=Projectile   ●=Target   ▲=Gun".PadRight(width));

					Thread.Sleep((int)frameDelayMs);
				}

				// Overlay the final result inside the plot area (centered)
				string resultText = _isHit ? "★ DIRECT HIT! ★" : "✗ MISS ✗";
				string promptText = "Press any key to continue...";

				int resultRow = top + Math.Max(1, height / 2);
				int promptRow = top + Math.Max(1, height - 2);
				int resultCol = left + Math.Max(0, (width - resultText.Length) / 2);
				int promptCol = left + Math.Max(0, (width - promptText.Length) / 2);

				try
				{
					// Clear a small band so the text is readable
					for (int r = -1; r <= 1; r++)
					{
						int rr = resultRow + r;
						Console.SetCursorPosition(left, rr);
						Console.Write(new string(' ', width));
					}
				}
				catch { }

				try { Console.SetCursorPosition(resultCol, resultRow); } catch { }
				if (_isHit)
					Console.ForegroundColor = ConsoleColor.Green;
				else
					Console.ForegroundColor = ConsoleColor.Red;
				Console.Write(resultText);
				Console.ResetColor();

				try
				{
					Console.SetCursorPosition(left, promptRow);
					Console.Write(new string(' ', width));
					Console.SetCursorPosition(promptCol, promptRow);
					Console.Write(promptText);
				}
				catch { }
			}
			finally
			{
				try { Console.SetOut(priorOut ?? (ui.IndentWriter ?? Console.Out)); } catch { }
				try { Console.CursorVisible = priorCursorVisible; } catch { }
			}
		}

		protected override void RenderBody(UiContext ui)
		{
			// Not used - custom Render handles animation.
		}

		protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
		{
			return PageResult.Exit;
		}
	}
}
