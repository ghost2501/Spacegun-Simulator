using Spacegun_Simulator.Core;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Core
{
	public sealed class GameOverPage : PageBase
	{
		public override string Id => PageId.GameOver;

		// Legacy game over screen did not render a title in the header area.
		public override string Title => string.Empty;

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: false,
			ShowSidePanels: true,
			FooterHint: "Any key=Continue"
		);

		private static IReadOnlyList<string> BuildHeader()
			=> new List<string>
			{
				"",
				"",
				"",
				"      (                         ( /(                     ",
				"      )\\ )      )    )     (    )\\())  )     (  (        ",
				"     (()/(   ( /(   (     ))\\  ((_)\\  /((   ))\\ )(       ",
				"      /(_))_ )(_))  )\\  '/((_)   ((_)(_))\\ /((_|()\\      ",
				"     (_)) __((_)_ _((_))(_))    / _ \\_)((_|_))  ((_)     ",
				"       | (_ / _` | '  \\() -_)  | (_) \\ V // -_)| '_|     ",
				"        \\___\\__,_|_|_|_|\\___|   \\___/ \\_/ \\___||_|       ",
				string.Empty
			};

		private static string[]? TryLoadGameOverArtLines(string fileName)
		{
			string cwd = Directory.GetCurrentDirectory();
			string baseDir = AppContext.BaseDirectory;

			string[] candidates =
			{
				Path.Combine(cwd, "Assets", "ascii-art", fileName),
				Path.Combine(baseDir, "Assets", "ascii-art", fileName),
				Path.GetFullPath(Path.Combine(baseDir, "..", "Assets", "ascii-art", fileName)),
				Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Assets", "ascii-art", fileName)),
			};

			foreach (var p in candidates)
			{
				try
				{
					if (File.Exists(p))
						return File.ReadAllLines(p);
				}
				catch { }
			}

			return null;
		}

		public override void Render(UiContext ui)
		{
			ui.Clear();

			IReadOnlyList<string> headerLines = BuildHeader();
			if (ConsoleTextMode.AsciiOnly)
			{
				var ascii = TryLoadGameOverArtLines("GameOver-ascii-only.txt");
				if (ascii is { Length: > 0 })
					headerLines = ascii;
			}

			var header = new List<string>(headerLines);
			var (leftOverride, rightOverride) = ui.ResolveSideArt?.Invoke(Id) ?? (null, null);

			var (contentLeft, contentTop) = ui.Layout.BeginBufferedFrame(
				header,
				ui.OriginalOut,
				ui.IndentWriter,
				0, // UI pages should not apply legacy global indent
				leftOverride,
				rightOverride,
				respectSideArtHeight: false);

			try
			{
				RenderBody(ui);
				ui.WriteLine();
				ui.WriteLine(Chrome.FooterHint ?? "Any key=Continue");
			}
			finally
			{
				ui.Layout.EndBufferedFrame(contentLeft, contentTop);
			}
		}

		protected override void RenderBody(UiContext ui)
		{
			var game = ui.Game;

			ui.WriteLine();
			ui.WriteLine();

			if (game != null && game.WavesDefeated >= GameConstants.TotalWaves)
			{
				ui.WriteLine("✓ VICTORY! Campaign Complete!");
				ui.WriteLine();
				ui.WriteLine($"Waves Defeated: {game.WavesDefeated}/{GameConstants.TotalWaves}");
			}
			else
			{
				ui.WriteLine("✗ DEFEAT! Mission Failed");
				ui.WriteLine();
				ui.WriteLine($"Waves Defeated: {game?.WavesDefeated ?? 0}/{GameConstants.TotalWaves}");
			}
		}

		public override PageResult HandleInput(UiContext ui, ConsoleKeyInfo key)
		{
			// Game over: treat ANY key (including Esc/Q) as "continue".
			return PageResult.Exit;
		}
	}
}
