using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.FireControl
{
	public sealed class FiringResultsPage : PageBase
	{
		public override string Id => PageId.FiringResults;
		public override string Title => "RESULTS";

		public override PageChrome Chrome { get; } = new(
			ShowStatusBar: true,
			ShowSidePanels: true,
			AutoSaveOnEnter: false,
			AutoSaveOnExit: false,
			FooterHint: "Press Any Key to Continue. (M)enu (Q)uit"
		);

		private readonly List<string> _lines;
		private int _scroll;

		public FiringResultsPage(IEnumerable<string> lines)
		{
			_lines = lines != null ? new List<string>(lines) : new List<string>();
		}

		public override void OnEnter(UiContext ui)
		{
			_scroll = 0;
		}

		protected override void RenderBody(UiContext ui)
		{
			if (_lines.Count == 0)
			{
				ui.WriteLine("(No results.)");
				return;
			}

			int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
			int maxScroll = Math.Max(0, _lines.Count - viewport);
			_scroll = Math.Clamp(_scroll, 0, maxScroll);

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
			}

			return PageResult.Exit;
		}
	}
}
