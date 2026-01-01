using System.Text;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Screen
{
    /// <summary>
    /// Layout helper: side art + frame rendering + buffered content placement.
    /// Layout helper: side art + frame rendering + buffered content placement.
    /// Keep method signatures stable (compat overloads included).
    /// </summary>
    public sealed class ScreenLayout
    {
        public int Offset { get; }
        public int FrameWidth { get; }

        private string[] _leftArt = Array.Empty<string>();
        private string[] _rightArt = Array.Empty<string>();

        public IReadOnlyList<string> LeftArt => _leftArt;
        public IReadOnlyList<string> RightArt => _rightArt;

        // Buffered-frame state
        private PageBuffer? _activeBuffer;
        private TextWriter? _stagedOriginalOut;
        private TextWriter? _stagedIndentWriter;

        private readonly bool _diagnosticsEnabled;

        public ScreenLayout(int offset = 20, int frameWidth = 60, bool enableDiagnostics = false)
        {
            Offset = Math.Max(0, offset);
            FrameWidth = Math.Max(10, frameWidth);
            _diagnosticsEnabled = enableDiagnostics;

            _leftArt = TryLoadArt(null, "SideLeft.txt");
            _rightArt = TryLoadArt(null, "SideRight.txt");
        }

        public void LoadSideArt(string? leftFilePath, string? rightFilePath)
        {
            _leftArt = TryLoadArt(leftFilePath, "SideLeft.txt");
            _rightArt = TryLoadArt(rightFilePath, "SideRight.txt");
        }

        // ---------------------------
        // Art loading helpers
        // ---------------------------

        private static string[] TryReadAllLinesOrFallback(string? maybePath, string[] fallback)
        {
            try
            {
                if (!string.IsNullOrEmpty(maybePath) && File.Exists(maybePath))
                    return File.ReadAllLines(maybePath);
            }
            catch { }
            return fallback;
        }

        private string[] TryLoadArt(string? explicitPath, string defaultFileName)
        {
            try
            {
                if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
                    return File.ReadAllLines(explicitPath);

                string[] candidates =
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets", "ascii-art", defaultFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "ascii-art", defaultFileName),
                    Path.Combine(AppContext.BaseDirectory, "..", "Assets", "ascii-art", defaultFileName),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "Assets", "ascii-art", defaultFileName),
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c))
                        return File.ReadAllLines(c);
                }
            }
            catch { }

            return Array.Empty<string>();
        }

        // ---------------------------
        // Rendering primitives
        // ---------------------------

        public void RenderWithSides(IList<string> centerLines)
            => RenderWithSides(centerLines, null, null);

        public void RenderWithSides(IList<string> centerLines, string? leftOverridePath, string? rightOverridePath)
            => RenderInternal(centerLines, leftOverridePath, rightOverridePath, includeOffset: true);

        public void RenderWithSides_NoOffset(IList<string> centerLines, string? leftOverridePath = null, string? rightOverridePath = null)
            => RenderInternal(centerLines, leftOverridePath, rightOverridePath, includeOffset: false);

        /// <summary>
        /// Compatibility method: some callers use RenderFrame(..., noOffset: true).
        /// We keep a flexible signature and delegate to RenderWithSides / RenderWithSides_NoOffset.
        /// </summary>
        public void RenderFrame(
            IList<string> frameLines,
            string? header = null,
            string? leftTitle = null,
            string? rightTitle = null,
            string? leftOverride = null,
            string? rightOverride = null,
            bool noOffset = false)
        {
            // This implementation assumes frameLines already contain what should appear in the center column.
            // The header/leftTitle/rightTitle parameters are present for compat; your existing call sites may pass them.
            if (noOffset)
                RenderWithSides_NoOffset(frameLines, leftOverride, rightOverride);
            else
                RenderWithSides(frameLines, leftOverride, rightOverride);
        }
        /// <summary>
        /// Compatibility overload used by FireControlTools (TargetMotionComputer, TrajectoryPlotter, etc.).
        /// Signature matches: RenderFrame(lines, originalOut, indentWriter, globalIndent, noOffset: true)
        /// </summary>
        public void RenderFrame(
            IList<string> frameLines,
            TextWriter? originalOut,
            TextWriter indentWriter,
            int globalIndent,
            bool noOffset = false,
            string? leftOverride = null,
            string? rightOverride = null)
        {
            // Temporarily switch output to "raw" to draw the frame cleanly
            var stagedOriginal = originalOut ?? Console.Out;
            var stagedIndent = indentWriter ?? Console.Out;

            Console.SetOut(stagedOriginal);

            if (noOffset)
                RenderWithSides_NoOffset(frameLines, leftOverride, rightOverride);
            else
                RenderWithSides(frameLines, leftOverride, rightOverride);

            // Restore output to an indented writer for subsequent content
            // (globalIndent is applied via a PageBuffer wrapper)
            Console.SetOut(new PageBuffer(globalIndent));
        }

        private void RenderInternal(IList<string> centerLines, string? leftOverridePath, string? rightOverridePath, bool includeOffset)
        {
            var useLeft = TryReadAllLinesOrFallback(leftOverridePath, _leftArt);
            var useRight = TryReadAllLinesOrFallback(rightOverridePath, _rightArt);

            var (leftW, rightW, totalWidth, padLeft) = ComputeClampedMetrics(centerLines, useLeft, useRight, includeOffset);

            int rows = Math.Max(Math.Max(useLeft.Length, useRight.Length), centerLines.Count);

            for (int i = 0; i < rows; i++)
            {
                string l = i < useLeft.Length ? (useLeft[i] ?? "") : "";
                string c = i < centerLines.Count ? (centerLines[i] ?? "") : "";
                string r = i < useRight.Length ? (useRight[i] ?? "") : "";

                l = ConsoleTextMode.Sanitize(l);
                c = ConsoleTextMode.Sanitize(c);
                r = ConsoleTextMode.Sanitize(r);

                l = leftW <= 0 ? "" : (l.Length > leftW ? l.Substring(0, leftW) : l.PadRight(leftW));
                c = c.Length > FrameWidth ? c.Substring(0, FrameWidth) : c.PadRight(FrameWidth);
                r = rightW <= 0 ? "" : (r.Length > rightW ? r.Substring(0, rightW) : r.PadRight(rightW));

                string line = l + c + r;

                int winW;
                try { winW = Console.WindowWidth; }
                catch { winW = totalWidth + (includeOffset ? Offset : 0); }

                // IMPORTANT: avoid writing into the last console column.
                // Many console hosts auto-wrap when the last column is written,
                // which makes each logical row consume an extra line.
                int avail = Math.Max(0, winW - padLeft - 1);
                if (line.Length > avail) line = line.Substring(0, avail);

                try { Console.SetCursorPosition(padLeft, Console.CursorTop); } catch { }
                Console.WriteLine(line);
            }
        }

        private (int leftW, int rightW, int totalWidth, int padLeft)
            ComputeClampedMetrics(IList<string> centerLines, string[] useLeft, string[] useRight, bool includeOffset)
        {
            int leftW = 0;
            foreach (var l in useLeft) leftW = Math.Max(leftW, ConsoleTextMode.Sanitize(l).Length);

            int rightW = 0;
            foreach (var r in useRight) rightW = Math.Max(rightW, ConsoleTextMode.Sanitize(r).Length);

            int totalWidth = leftW + FrameWidth + rightW;

            int winW;
            try { winW = Console.WindowWidth; }
            catch { winW = totalWidth + (includeOffset ? Offset : 0); }

            // Keep a 1-column safety margin to avoid auto-wrap.
            int safeWinW = Math.Max(0, winW - 1);

            if (totalWidth > safeWinW)
            {
                int available = Math.Max(0, safeWinW - FrameWidth);
                int sides = leftW + rightW;

                if (sides > 0)
                {
                    double ratio = available / (double)sides;
                    leftW = (int)Math.Floor(leftW * ratio);
                    rightW = Math.Max(0, available - leftW);
                }
                else
                {
                    leftW = rightW = 0;
                }

                totalWidth = leftW + FrameWidth + rightW;
            }

            int padLeft = Math.Max(0, (winW - totalWidth) / 2) + (includeOffset ? Offset : 0);
            return (leftW, rightW, totalWidth, padLeft);
        }

        // ---------------------------
        // Content-left helpers (compat overloads)
        // ---------------------------

        /// <summary>
        /// Called by legacy code: CalculateContentLeft_NoOffset(lines, leftOverride, rightOverride)
        /// </summary>
        public int CalculateContentLeft_NoOffset(IList<string> frameLines, string? leftOverride, string? rightOverride)
        {
            var useLeft = TryReadAllLinesOrFallback(leftOverride, _leftArt);
            var useRight = TryReadAllLinesOrFallback(rightOverride, _rightArt);

            var (leftW, _, _, padLeft) = ComputeClampedMetrics(frameLines, useLeft, useRight, includeOffset: false);
            return padLeft + leftW;
        }

        public int CalculateContentLeft_NoOffset(IList<string> frameLines)
            => CalculateContentLeft_NoOffset(frameLines, null, null);

        /// <summary>
        /// Called by legacy code: CalculateContentLeft(lines, leftOverride, rightOverride)
        /// </summary>
        public int CalculateContentLeft(IList<string> frameLines, string? leftOverride, string? rightOverride)
        {
            var useLeft = TryReadAllLinesOrFallback(leftOverride, _leftArt);
            var useRight = TryReadAllLinesOrFallback(rightOverride, _rightArt);

            var (leftW, _, _, padLeft) = ComputeClampedMetrics(frameLines, useLeft, useRight, includeOffset: true);
            return padLeft + leftW;
        }

        public int CalculateContentLeft(IList<string> frameLines)
            => CalculateContentLeft(frameLines, null, null);

        // ---------------------------
        // Buffered frame API (compat overloads)
        // ---------------------------

        public PageBuffer CreatePageBuffer(int indentLength) => new PageBuffer(indentLength);

        /// <summary>
        /// Back-compat overload used by older tooling.
        /// </summary>
        public (int contentLeft, int contentTop) BeginBufferedFrame(
            IList<string> frameLines,
            TextWriter? originalOut,
            TextWriter? indentWriter,
            string? leftOverride,
            string? rightOverride)
        {
            // globalIndent=0, respectSideArtHeight=false
            return BeginBufferedFrame(frameLines, originalOut, indentWriter, 0, leftOverride, rightOverride, respectSideArtHeight: false);
        }

        /// <summary>
        /// PageArtOverrides calls this exact 7-arg signature:
        /// BeginBufferedFrame(headerLines, _originalOut, _indentWriter, _globalIndent, leftOverride, rightOverride, respectSideArtHeight)
        /// </summary>
        public (int contentLeft, int contentTop) BeginBufferedFrame(
            IList<string> frameLines,
            TextWriter? originalOut,
            TextWriter? indentWriter,
            int globalIndent,
            string? leftOverride,
            string? rightOverride,
            bool respectSideArtHeight)
        {
            _stagedOriginalOut = originalOut ?? Console.Out;
            _stagedIndentWriter = indentWriter ?? Console.Out;

            Console.Clear();
            Console.SetOut(_stagedOriginalOut);

            int startRow;
            try { startRow = Console.CursorTop; } catch { startRow = 0; }

            // Legacy buffered rendering expects the "no-offset" frame
            // Pad out the draw height to the full window height so side panels extend
            // down to meet pinned footer art rendered later by PageBase.
            int targetRows;
            try
            {
                targetRows = Math.Max(0, (Console.WindowHeight - 2) - startRow);
            }
            catch
            {
                targetRows = frameLines.Count;
            }

            var drawLines = new List<string>(frameLines);
            while (drawLines.Count < targetRows)
                drawLines.Add("");

            // Draw padded frame so left/right panels extend to full window height.
            RenderWithSides_NoOffset(drawLines, leftOverride, rightOverride);


            // contentTop must account for side art height if requested
            int renderedRows = frameLines.Count;
            if (respectSideArtHeight)
            {
                var useLeft = TryReadAllLinesOrFallback(leftOverride, _leftArt);
                var useRight = TryReadAllLinesOrFallback(rightOverride, _rightArt);
                renderedRows = Math.Max(renderedRows, Math.Max(useLeft.Length, useRight.Length));
            }

            int contentLeft = CalculateContentLeft_NoOffset(frameLines, leftOverride, rightOverride);
            int contentTop = startRow + renderedRows;

            _activeBuffer = CreatePageBuffer(globalIndent);
            Console.SetOut(_activeBuffer);

            if (_diagnosticsEnabled)
            {
                try { Console.Error.WriteLine($"[ScreenLayout] BeginBufferedFrame left={contentLeft} top={contentTop} indent={globalIndent} respectSideArtHeight={respectSideArtHeight}"); }
                catch { }
            }

            return (contentLeft, contentTop);
        }

        /// <summary>
        /// Expected by legacy code: flush buffered content and restore output.
        /// </summary>
        public int EndBufferedFrame(int contentLeft, int contentTop)
        {
            if (_activeBuffer == null)
            {
                try { Console.SetOut(_stagedIndentWriter ?? Console.Out); } catch { }
                return contentTop;
            }

            Console.SetOut(_stagedOriginalOut ?? Console.Out);
            int written = _activeBuffer.FlushToConsole(contentLeft, contentTop);

            Console.SetOut(_stagedIndentWriter ?? Console.Out);
            _activeBuffer = null;

            return contentTop + written;
        }
    }

    /// <summary>
    /// Buffered writer used to capture text then flush to a fixed console coordinate.
    /// </summary>
    public sealed class PageBuffer : TextWriter
    {
        private readonly StringBuilder _sb = new();
        private readonly string _indent;
        private bool _beginLine = true;

        public PageBuffer(int indentLength)
        {
            _indent = new string(' ', Math.Max(0, indentLength));
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (_beginLine)
            {
                _sb.Append(_indent);
                _beginLine = false;
            }

            _sb.Append(value);
            if (value == '\n') _beginLine = true;
        }

        public override void WriteLine()
        {
            Write('\n');
        }

        public int FlushToConsole(int left, int top)
        {
            var lines = _sb.ToString().Split('\n');
            int row = top;

            foreach (var line in lines)
            {
                try
                {
                    Console.SetCursorPosition(left, row++);
                    int winW;
                    try { winW = Console.WindowWidth; }
                    catch { winW = left + line.Length + 1; }

                    int avail = Math.Max(0, winW - left - 1);
                    if (line.Length > avail)
                        Console.Write(line.Substring(0, avail));
                    else
                        Console.Write(line);
                }
                catch { }
            }

            _sb.Clear();
            return lines.Length;
        }
    }
}
