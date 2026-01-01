using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages
{
    /// <summary>
    /// Base class that centralizes default behavior so you don't duplicate it in every page.
    /// </summary>
    public abstract class PageBase : IPage
    {
		// Must match ScreenLayout.FrameWidth (default 60) to avoid a visible 1-column seam.
        protected const int DefaultFrameWidth = 60;
		protected const int DefaultScrollPageStep = 6;

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

        private static string FormatFooterHint(string hint, int width)
        {
            hint ??= "";
            hint = hint.Trim();
            if (hint.Length == 0) return hint;

            // Split into option "chunks" and then justify them across the available width.
            // Chunk boundaries are detected when a new token begins (e.g. "(M)enu", "1-4=Tables", "Digits+↩=Input").
            var items = new List<string>();
            int len = hint.Length;
            int start = 0;
            while (start < len && char.IsWhiteSpace(hint[start])) start++;
            int i = start;
            while (i < len)
            {
                if (char.IsWhiteSpace(hint[i]))
                {
                    int j = i;
                    while (j < len && char.IsWhiteSpace(hint[j])) j++;
                    if (j >= len) break;

                    bool splitHere = false;

                    // If there are 2+ spaces (or other whitespace) between tokens,
                    // treat it as an explicit chunk separator.
                    if (j - i >= 2)
                        splitHere = true;

                    // If the next token starts with '(' or '[', treat it as a new chunk.
                    char next = hint[j];
                    if (next == '(' || next == '[')
                        splitHere = true;

                    // If the next token contains '=', treat it as a new chunk.
                    if (!splitHere)
                    {
                        int k = j;
                        while (k < len && !char.IsWhiteSpace(hint[k]))
                        {
                            if (hint[k] == '=') { splitHere = true; break; }
                            k++;
                        }
                    }

                    // If the next token begins with a digit, treat it as a new chunk.
                    if (!splitHere && char.IsDigit(next))
                        splitHere = true;

                    if (splitHere)
                    {
                        var part = hint.Substring(start, i - start).Trim();
                        if (part.Length > 0) items.Add(part);
                        start = j;
                        i = j;
                        continue;
                    }
                }

                i++;
            }

            var last = hint.Substring(start).Trim();
            if (last.Length > 0) items.Add(last);

            // Only justify when we have enough chunks for it to matter.
            if (items.Count < 3)
                return hint;

            int gaps = items.Count - 1;
            int itemsLen = 0;
            foreach (var it in items) itemsLen += it.Length;

            int minSpaces = gaps; // 1 space per gap
            int remaining = width - (itemsLen + minSpaces);
            if (remaining <= 0)
                return string.Join(' ', items);

            int extraEach = remaining / gaps;
            int extraRemainder = remaining % gaps;

            var sb = new System.Text.StringBuilder(width);
            for (int idx = 0; idx < items.Count; idx++)
            {
                sb.Append(items[idx]);
                if (idx < gaps)
                {
                    int spaces = 1 + extraEach + (idx < extraRemainder ? 1 : 0);
                    sb.Append(' ', spaces);
                }
            }

            return sb.ToString();
        }

        protected static string Clamp60(string text)
            => ClampToWidth(text, DefaultFrameWidth);

        protected static string Center60(string text)
            => CenterToWidth(text, DefaultFrameWidth);

        protected static int GetViewportHeight(UiContext ui, int fallback = 18)
            => ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : fallback;

        protected static void ClampScroll(ref int scroll, int lineCount, int viewportHeight)
        {
            int maxScroll = Math.Max(0, lineCount - Math.Max(0, viewportHeight));
            scroll = Math.Clamp(scroll, 0, maxScroll);
        }

        protected static bool TryHandleScrollKeys(ConsoleKeyInfo key, ref int scroll, int lineStep = 1, int pageStep = DefaultScrollPageStep)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow: scroll -= lineStep; return true;
                case ConsoleKey.DownArrow: scroll += lineStep; return true;
                case ConsoleKey.PageUp: scroll -= pageStep; return true;
                case ConsoleKey.PageDown: scroll += pageStep; return true;
                default: return false;
            }
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

        private sealed class WordWrapWriter : System.IO.TextWriter
        {
            private readonly System.IO.TextWriter _inner;
            private readonly int _width;
            private readonly System.Text.StringBuilder _line = new();

            public WordWrapWriter(System.IO.TextWriter inner, int width)
            {
                _inner = inner;
                _width = Math.Max(10, width);
            }

            public override System.Text.Encoding Encoding => _inner.Encoding;

            private void FlushLine(bool emitNewline)
            {
                if (_line.Length > 0)
                {
                    _inner.Write(_line.ToString());
                    _line.Clear();
                }

                if (emitNewline)
                    _inner.Write('\n');
            }

            private void WrapIfNeeded()
            {
                while (_line.Length > _width)
                {
                    int breakAt = -1;
                    for (int i = Math.Min(_width, _line.Length - 1); i >= 0; i--)
                    {
                        if (_line[i] == ' ')
                        {
                            breakAt = i;
                            break;
                        }
                    }

                    if (breakAt <= 0)
                        breakAt = _width;

                    string head = _line.ToString(0, breakAt).TrimEnd();
                    _inner.Write(head);
                    _inner.Write('\n');

                    int remove = breakAt;
                    while (remove < _line.Length && _line[remove] == ' ') remove++;
                    _line.Remove(0, remove);
                }
            }

            public override void Write(char value)
            {
                if (value == '\r')
                    return;

                if (value == '\n')
                {
                    WrapIfNeeded();
                    FlushLine(emitNewline: true);
                    return;
                }

                _line.Append(value);
                WrapIfNeeded();
            }

            public override void Write(string? value)
            {
                if (string.IsNullOrEmpty(value))
                    return;

                foreach (var ch in value)
                    Write(ch);
            }

            public override void WriteLine(string? value)
            {
                Write(value);
                Write('\n');
            }

            public override void WriteLine()
                => Write('\n');

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    FlushLine(emitNewline: false);
                base.Dispose(disposing);
            }
        }

        public virtual void Render(UiContext ui)
        {
            ui.Clear();

            int frameWidth = ui.Layout.FrameWidth > 0 ? ui.Layout.FrameWidth : DefaultFrameWidth;

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
                    header.Add(CenterToWidth(Title, frameWidth));

                // Optional single-line status (below title)
                if (Chrome.ShowStatusBar)
                {
                    var status = ui.BuildStatusBar?.Invoke();
                    if (!string.IsNullOrWhiteSpace(status))
                        header.Add(ClampToWidth(status, frameWidth));
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

                ui.FrameContentLeftNoOffset = contentLeft;
                ui.FrameContentTopNoOffset = contentTop;

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

                    // IMPORTANT: subtract 2 to avoid console scroll at bottom (your current tuned value)
                    int available = Math.Max(0, (winH - 2) - contentTop);
                    int viewportHeight = Math.Max(0, available - reservedFooterLines);

                    ui.ContentViewportHeight = viewportHeight;

                    // Render BODY into a line-limited writer so it cannot push footer away.
                    var pageBufferWriter = Console.Out;
                    var limited = new LineLimitedWriter(pageBufferWriter, viewportHeight);
                    var wrapped = new WordWrapWriter(limited, frameWidth);
                    Console.SetOut(wrapped);

                    RenderBody(ui);

                    // Restore writer for padding + pinned footer.
                    Console.SetOut(pageBufferWriter);

                    // Pad body to exactly fill viewport so footer is pinned.
                    int remaining = viewportHeight - limited.LinesWritten;
                    for (int i = 0; i < remaining; i++)
                        Console.WriteLine();

                    // Pinned footer bar (optional)
                    if (hasFooterBar)
                        Console.WriteLine(ClampToWidth(footerBar!, frameWidth).PadRight(frameWidth));

                    // Pinned footer hint (always one line)
                    var hint = Chrome.FooterHint ?? "(M)enu (Q)uit";
                    hint = FormatFooterHint(hint, frameWidth);
                    Console.WriteLine(ClampToWidth(hint, frameWidth).PadRight(frameWidth));

                    // Pinned footer art (optional)
                    foreach (var line in footerArt)
                        Console.WriteLine(ClampToWidth(line ?? "", frameWidth).PadRight(frameWidth));
                }
                catch (Exception ex)
                {
                    // Debug Page Migration
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
            var hint2 = Chrome.FooterHint ?? "(M)enu (Q)uit";
			hint2 = FormatFooterHint(hint2, frameWidth);
			ui.WriteLine(ClampToWidth(hint2, frameWidth));
        }

        protected abstract void RenderBody(UiContext ui);

        public virtual PageResult HandleInput(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Q)
                return HandleQuit(ui, key);

            if (key.Key == ConsoleKey.Escape)
                return HandleEscape(ui, key);

            if (key.Key == ConsoleKey.M)
                return HandleMenu(ui, key);

            return HandleInputBody(ui, key);
        }

        protected virtual PageResult HandleQuit(UiContext ui, ConsoleKeyInfo key)
        {
            ui.RequestExitGame = true;
            return PageResult.Exit;
        }

        protected virtual PageResult HandleEscape(UiContext ui, ConsoleKeyInfo key)
        {
            // Default behavior:
            // - During gameplay, ESC requests a session-level return-to-menu.
            // - During boot UI, ESC simply exits the UI flow.
            if (ui.Game != null)
                ui.RequestReturnToMenu = true;

            return PageResult.Exit;
        }

        protected virtual PageResult HandleMenu(UiContext ui, ConsoleKeyInfo key)
        {
            // Default behavior:
            // - During gameplay, M requests a session-level return-to-menu.
            // - During boot UI, M has no default meaning.
            if (ui.Game != null)
            {
                ui.RequestReturnToMenu = true;
                return PageResult.Exit;
            }

            return PageResult.Stay;
        }

        protected virtual PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
            => PageResult.Stay;
    }
}
