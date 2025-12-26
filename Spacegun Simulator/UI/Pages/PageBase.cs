using System;
using Spacegun_Simulator;
using System.Collections.Generic;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages
{
    /// <summary>
    /// Base class that centralizes default behavior so you don't duplicate it in every page.
    /// </summary>
    public abstract class PageBase : IPage
    {
        public abstract string Id { get; }
        public abstract string Title { get; }

        public virtual PageChrome Chrome { get; } = new();

        public virtual void OnEnter(UiContext ui) { }
        public virtual void OnExit(UiContext ui) { }

        private static string CenterToWidth(string text, int width)
        {
            text ??= "";
            if (text.Length >= width) return text.Substring(0, width);
            int padLeft = (width - text.Length) / 2;
            return new string(' ', padLeft) + text;
        }

        private static string ClampToWidth(string text, int width)
        {
            text ??= "";
            return text.Length > width ? text.Substring(0, width) : text;
        }

        // Limits lines written so the pinned footer cannot be pushed off-screen.
        private sealed class LineLimitedWriter : System.IO.TextWriter
        {
            private readonly System.IO.TextWriter _inner;
            private readonly int _maxLines;
            private int _linesWritten;
            private bool _blocked;

            public LineLimitedWriter(System.IO.TextWriter inner, int maxLines)
            {
                _inner = inner;
                _maxLines = Math.Max(0, maxLines);
            }

            public int LinesWritten => _linesWritten;

            public override System.Text.Encoding Encoding => _inner.Encoding;

            public override void Write(char value)
            {
                if (_blocked) return;

                _inner.Write(value);

                if (value == '\n')
                {
                    _linesWritten++;
                    if (_linesWritten >= _maxLines)
                        _blocked = true;
                }
            }

            public override void Write(string? value)
            {
                if (_blocked || string.IsNullOrEmpty(value)) return;
                foreach (var ch in value)
                    Write(ch);
            }

            public override void WriteLine(string? value)
            {
                if (_blocked) return;
                Write(value);
                Write('\n');
            }

            public override void WriteLine()
            {
                if (_blocked) return;
                Write('\n');
            }
        }

        public virtual void Render(UiContext ui)
        {
            ui.Clear();

            if (Chrome.ShowSidePanels)
            {
                var header = new List<string>();

                // Header art bridge
                if (Chrome.ShowStatusBar)
                {
                    var art = ui.BuildHeaderArt?.Invoke(Id);
                    if (art != null)
                        foreach (var line in art) header.Add(line ?? "");
                }

                // Optional title (blank title suppresses it)
                if (!string.IsNullOrWhiteSpace(Title))
                    header.Add(CenterToWidth(Title, 60));

                // Optional single-line status (below title)
                if (Chrome.ShowStatusBar)
                {
                    var status = ui.BuildStatusBar?.Invoke();
                    if (!string.IsNullOrWhiteSpace(status))
                        header.Add(ClampToWidth(status, 60));
                }

                header.Add("");

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
                    // ---- pinned footer computation ----
                    var footerArt = (Chrome.ShowStatusBar ? ui.BuildFooterArt?.Invoke(Id) : null) ?? Array.Empty<string>();
                    var footerBar = (Chrome.ShowStatusBar ? ui.BuildFooterBar?.Invoke() : null);
                    bool hasFooterBar = !string.IsNullOrWhiteSpace(footerBar);

                    int footerArtLines = footerArt.Count;
                    int reservedFooterLines = footerArtLines + (hasFooterBar ? 1 : 0) + 1; // +1 for hint line

                    int winH;
                    try { winH = Console.WindowHeight; }
                    catch { winH = 30; }

                    // IMPORTANT: -1 to avoid console scrolling when writing the last line
                    int available = Math.Max(0, (winH - 2) - contentTop);
                    int viewportHeight = Math.Max(0, available - reservedFooterLines);

                    ui.ContentViewportHeight = viewportHeight;

                    // Render BODY into a line-limited writer so it cannot push footer away.
                    var pageBufferWriter = Console.Out;
                    var limited = new LineLimitedWriter(pageBufferWriter, viewportHeight);
                    Console.SetOut(limited);

                    RenderBody(ui);

                    // Restore writer for padding + pinned footer.
                    Console.SetOut(pageBufferWriter);

                    // Pad body to exactly fill viewport so footer is pinned.
                    int remaining = viewportHeight - limited.LinesWritten;
                    for (int i = 0; i < remaining; i++)
                        Console.WriteLine();

                    // Pinned footer bar (optional)
                    if (hasFooterBar)
                        Console.WriteLine(ClampToWidth(footerBar!, 60));

                    // Pinned footer hint (always one line)
                    var hint = Chrome.FooterHint ?? "↑/↓ scroll  Any key continue  [Esc] Menu  [Q] Quit.";
                    Console.WriteLine(ClampToWidth(hint, 60));

                    // Pinned footer art (optional)
                    foreach (var line in footerArt)
                        Console.WriteLine(ClampToWidth(line ?? "", 60));
                }
                catch (Exception ex)
                {
                    ui.DebugLog($"ERROR Render failed on page '{Id}': {ex}");
                    throw;
                }
                finally
                {
                    ui.Layout.EndBufferedFrame(contentLeft, contentTop);
                }

                return;
            }

            // Fallback: no side panels
            if (!string.IsNullOrWhiteSpace(Title))
                ui.WriteLine($"=== {Title} ===");

            ui.WriteLine();
            RenderBody(ui);
            ui.WriteLine();
            ui.WriteLine(Chrome.FooterHint ?? "Press [Esc] for Menu, [Q] to quit.");
        }

        protected abstract void RenderBody(UiContext ui);

        public virtual PageResult HandleInput(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Q)
                return PageResult.Exit;

            if (key.Key == ConsoleKey.Escape)
            {
                ui.RequestReturnToMenu = true;
                return PageResult.Exit;
            }


            return HandleInputBody(ui, key);
        }

        protected virtual PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
            => PageResult.Stay;
    }
}
