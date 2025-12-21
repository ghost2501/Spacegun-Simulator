using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Spacegun_Simulator
{
    // Reusable screen layout helper extracted to its own file.
    // Loads default side art from Assets/ascii art and supports per-page overrides.
    internal sealed class ScreenLayout
    {
        public int Offset { get; }
        public int FrameWidth { get; }

        private string[] leftArt = Array.Empty<string>();
        private string[] rightArt = Array.Empty<string>();

        // Debug / diagnostics: expose small read-only views so callers can verify art was loaded.
        public IReadOnlyList<string> LeftArt => leftArt;
        public IReadOnlyList<string> RightArt => rightArt;

        // State used by the buffered-frame helpers (BeginBufferedFrame / EndBufferedFrame).
        private PageBuffer? _activeBuffer;
        private TextWriter? _stagedOriginalOut;
        private TextWriter? _stagedIndentWriter;

        // Controls whether layout diagnostics are emitted to Console.Error.
        private readonly bool _diagnosticsEnabled;

        // Added optional diagnostics flag (defaults to false so normal gameplay is quiet).
        public ScreenLayout(int offset = 20, int frameWidth = 60, bool enableDiagnostics = false)
        {
            Offset = Math.Max(0, offset);
            FrameWidth = Math.Max(10, frameWidth);
            _diagnosticsEnabled = enableDiagnostics;

            try
            {
                leftArt = TryLoadArt(null, "SideLeft.txt");
                rightArt = TryLoadArt(null, "SideRight.txt");
            }
            catch
            {
                leftArt = Array.Empty<string>();
                rightArt = Array.Empty<string>();
            }
        }

        /// <summary>
        /// Load side art from explicit file paths. If a path is null the method will
        /// attempt to use the default assets location before falling back to empty art.
        /// </summary>
        public void LoadSideArt(string? leftFilePath, string? rightFilePath)
        {
            leftArt = TryLoadArt(leftFilePath, "SideLeft.txt");
            rightArt = TryLoadArt(rightFilePath, "SideRight.txt");
        }

        // Helper: prefer explicit path, otherwise try default asset locations.
        private string[] TryLoadArt(string? explicitPath, string defaultFileName)
        {
            try
            {
                // If caller provided an explicit path, prefer it (and log if it exists).
                if (!string.IsNullOrEmpty(explicitPath))
                {
                    if (File.Exists(explicitPath))
                    {
                        var lines = File.ReadAllLines(explicitPath);
                        LogArtLoad(explicitPath, lines.Length);
                        return lines;
                    }
                    // explicit path provided but not found - fall through to candidate search
                }

                // Candidate locations to search (cover common run-time working dirs).
                string[] candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets", "ascii art", defaultFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "ascii art", defaultFileName),
                    // Also try the project's directory relative to base (useful when running from IDE)
                    Path.Combine(AppContext.BaseDirectory, "..", "Assets", "ascii art", defaultFileName),
                    // Try assembly location's parent (defensive)
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "Assets", "ascii art", defaultFileName)
                };

                foreach (var c in candidates)
                {
                    try
                    {
                        if (File.Exists(c))
                        {
                            var lines = File.ReadAllLines(c);
                            LogArtLoad(c, lines.Length);
                            return lines;
                        }
                    }
                    catch
                    {
                        // ignore per-file IO errors and continue searching
                    }
                }
            }
            catch
            {
                // ignore IO errors and fall through to empty
            }

            // Nothing found — return empty and log for diagnostics
            LogArtLoad(null, 0, defaultFileName);
            return Array.Empty<string>();
        }

        // Simple diagnostic logging to Console.Error so it won't be captured by the indented writer.
        // When diagnostics are disabled this writes only to the file (if possible) and does not print to console.
        private void LogArtLoad(string? path, int lineCount, string requested = "")
        {
            try
            {
                string name = string.IsNullOrEmpty(path) ? requested : Path.GetFileName(path);
                string location = string.IsNullOrEmpty(path) ? "(not found)" : path;
                string msg = $"[ScreenLayout] Loaded '{name}' -> {(lineCount > 0 ? $"{lineCount} lines" : "no lines")} from: {location}";

                if (_diagnosticsEnabled)
                {
                    try { Console.Error.WriteLine(msg); } catch { }
                }

                try
                {
                    var file = Path.Combine(AppContext.BaseDirectory, "layout_debug.txt");
                    File.AppendAllText(file, DateTime.UtcNow.ToString("s") + " " + msg + Environment.NewLine);
                }
                catch { }
            }
            catch
            {
                // swallow logging failures
            }
        }

        public void WriteLineOffset(string text)
        {
            try
            {
                Console.Write(new string(' ', Offset));
                Console.WriteLine(text);
            }
            catch
            {
                Console.WriteLine(text);
            }
        }

        public void WriteCenteredInFrame(string text)
        {
            int winW;
            try { winW = Console.WindowWidth; } catch { winW = FrameWidth + Offset; }

            int frameLeft = Math.Max(0, (winW - FrameWidth) / 2);

            string content = text ?? string.Empty;
            if (content.Length > FrameWidth)
                content = content.Substring(0, FrameWidth);
            else
                content = content.PadRight(FrameWidth);

            try
            {
                Console.SetCursorPosition(frameLeft, Console.CursorTop);
            }
            catch
            {
                // ignore very small consoles
            }

            Console.WriteLine(content);
        }

        /// <summary>
        /// Render a title / ascii-art screen consistently using the ScreenLayout's centering/clipping logic.
        /// The method temporarily restores <paramref name="originalOut"/> to draw absolute art then restores
        /// <paramref name="indentWriter"/> for the normal UI output.
        /// </summary>
        public void ShowTitleScreen(TextWriter? originalOut, TextWriter? indentWriter, string? explicitTitlePath = null)
        {
            // Load ASCII art from known locations, fall back to an embedded small art fallback.
            string[] lines = null;
            var candidates = new[]
            {
                explicitTitlePath,
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "ascii art", "TitleScreen.txt"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "ascii art", "TitleScreen.txt"),
                Path.Combine(AppContext.BaseDirectory, "..", "Assets", "ascii art", "TitleScreen.txt")
            };

            foreach (var p in candidates)
            {
                if (string.IsNullOrEmpty(p)) continue;
                try
                {
                    if (File.Exists(p))
                    {
                        lines = File.ReadAllLines(p);
                        break;
                    }
                }
                catch
                {
                    // ignore and continue
                }
            }

            // Embedded fallback (small version of TitleScreen.txt) if file missing
            if (lines == null || lines.Length == 0)
            {
                lines = new[]
                {
                    "                o                                                       ",
                    "                                     .                                  ",
                    "                                                                  .     ",
                    "                 *                                            .:'       ",
                    "                                                          _.::'         ",
                    "       |             +                                   (_.'           ",
                    "      -+-     _|_                                    '                  ",
                    "       |       |         ┏━┛┏━┃┏━┃┏━┛┏━┛┏━┛┃ ┃┏━                        ",
                    "                         ━━┃┏━┛┏━┃┃  ┏━┛┃ ┃┃ ┃┃ ┃                       ",
                    "                         ━━┛┛  ┛ ┛━━┛━━┛━━┛━━┛┛ ┛                       ",
                    "                         ┏━┛┛┏┏ ┃ ┃┃  ┏━┃━┏┛┏━┃┏━┃                      ",
                    "                         ━━┃┃┃┃┃┃ ┃┃  ┏━┃ ┃ ┃ ┃┏┏┛         '            ",
                    "                         ━━┛┛┛┛┛━━┛━━┛┛ ┛ ┛ ━━┛┛ ┛                      ",
                    "   .      .                                                '            ",
                    "                           */                                           ",
                    "                         x                                          '   ",
                    "                       x                                                ",
                    "                      x                                                 ",
                    "'            -       x  *            .                  .         o     ",
                    "                    x                                            +     +",
                    "                   x                 +                    .-.           ",
                    "                  x                                        ) )       +  ",
                    "         .       x                                        '-´           ",
                    "                x                  '                                    ",
                    "              x               '  o                    '                 "
                };
            }

            int artHeight = lines.Length;
            int artWidth = 0;
            for (int i = 0; i < lines.Length; i++)
                artWidth = Math.Max(artWidth, lines[i]?.Length ?? 0);

            // Use the raw output to draw at absolute positions.
            Console.SetOut(originalOut ?? Console.Out);

            int winW = 0, winH = 0;
            try { winW = Console.WindowWidth; winH = Console.WindowHeight; } catch { }

            int left = Math.Max(0, (winW - artWidth) / 2);
            int top = Math.Max(1, (winH - artHeight) / 2 - 1);

            Console.Clear();

            // Draw the art, clipped if necessary
            for (int i = 0; i < artHeight; i++)
            {
                try
                {
                    Console.SetCursorPosition(left, top + i);
                }
                catch
                {
                    // ignore small consoles / SetCursorPosition failures
                    Console.WriteLine();
                }

                string line = lines[i] ?? string.Empty;
                if (line.Length > winW - left)
                    line = line.Substring(0, Math.Max(0, winW - left));
                try
                {
                    Console.Write(line);
                }
                catch
                {
                    // ignore drawing errors for tiny consoles
                }
            }

            // Blinking prompt centered below the art
            string prompt = "Press any key to continue";
            int promptLeft = Math.Max(0, (winW - prompt.Length) / 2);
            int promptTop = top + artHeight + 1;
            if (promptTop >= winH) promptTop = Math.Max(0, winH - 2);

            bool visible = true;
            bool prevCursorVisible = true;
            try
            {
                prevCursorVisible = Console.CursorVisible;
            }
            catch { }

            try
            {
                Console.CursorVisible = false;
                while (!Console.KeyAvailable)
                {
                    try
                    {
                        Console.SetCursorPosition(promptLeft, promptTop);
                        if (visible)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write(prompt);
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.Write(new string(' ', prompt.Length));
                        }
                    }
                    catch
                    {
                        // ignore draw errors on very small consoles
                    }

                    visible = !visible;
                    System.Threading.Thread.Sleep(500);
                }

                // Consume the key
                Console.ReadKey(true);

                // Clear prompt after keypress
                try
                {
                    Console.SetCursorPosition(promptLeft, promptTop);
                    Console.Write(new string(' ', Math.Min(prompt.Length, Console.WindowWidth - promptLeft)));
                }
                catch { }
            }
            finally
            {
                try { Console.CursorVisible = prevCursorVisible; } catch { }
                // Restore the indented writer for normal UI output
                Console.SetOut(indentWriter ?? Console.Out);
            }
        }

        // Central helper: compute clamped left/right widths and padLeft for rendering.
        // If the side art is wider than the window allows, the side widths are scaled down
        // so left + FrameWidth + right <= winW. The algorithm preserves relative side proportions.
        private (int leftW, int rightW, int totalWidth, int padLeft) ComputeClampedMetrics(IList<string> centerLines, string[] useLeft, string[] useRight, bool includeOffset)
        {
            int leftW = 0;
            for (int i = 0; i < useLeft.Length; i++)
                leftW = Math.Max(leftW, useLeft[i]?.Length ?? 0);

            int rightW = 0;
            for (int i = 0; i < useRight.Length; i++)
                rightW = Math.Max(rightW, useRight[i]?.Length ?? 0);

            int totalWidth = leftW + FrameWidth + rightW;

            int winW;
            try { winW = Console.WindowWidth; } catch { winW = totalWidth + (includeOffset ? Offset : 0); }

            // If the combined width exceeds window, shrink sides proportionally
            if (totalWidth > winW)
            {
                int availableForSides = Math.Max(0, winW - FrameWidth);
                int origSides = leftW + rightW;
                if (origSides == 0)
                {
                    leftW = rightW = 0;
                }
                else
                {
                    double ratio = availableForSides / (double)origSides;
                    // Preserve at least zero; use floor on left to keep deterministic result.
                    int newLeft = (int)Math.Floor(leftW * ratio);
                    int newRight = availableForSides - newLeft;
                    leftW = Math.Max(0, newLeft);
                    rightW = Math.Max(0, newRight);
                }

                totalWidth = leftW + FrameWidth + rightW;
            }

            int padLeft = Math.Max(0, (winW - totalWidth) / 2) + (includeOffset ? Offset : 0);

            // Diagnostic: record computed metrics
            LogLayoutDebug("ComputeClampedMetrics", $"includeOffset={includeOffset} winW={winW} leftW={leftW} rightW={rightW} frameW={FrameWidth} totalWidth={totalWidth} padLeft={padLeft}");

            return (leftW, rightW, totalWidth, padLeft);
        }

        /// <summary>
        /// Render center content with the currently loaded side art.
        /// </summary>
        public void RenderWithSides(IList<string> centerLines)
            => RenderWithSides(centerLines, null, null);

        /// <summary>
        /// Render center content with optional per-page side-art overrides.
        /// If leftOverridePath/rightOverridePath are null the previously-loaded defaults are used.
        /// This method will clamp side art widths to avoid overflowing the window width.
        /// </summary>
        public void RenderWithSides(IList<string> centerLines, string? leftOverridePath, string? rightOverridePath)
        {
            string[] useLeft = leftArt;
            string[] useRight = rightArt;

            if (!string.IsNullOrEmpty(leftOverridePath))
            {
                try { if (File.Exists(leftOverridePath)) useLeft = File.ReadAllLines(leftOverridePath); } catch { /* ignore */ }
            }

            if (!string.IsNullOrEmpty(rightOverridePath))
            {
                try { if (File.Exists(rightOverridePath)) useRight = File.ReadAllLines(rightOverridePath); } catch { /* ignore */ }
            }

            // Compute clamped widths using Offset (RenderWithSides uses Offset)
            var (leftW, rightW, totalWidth, padLeft) = ComputeClampedMetrics(centerLines, useLeft, useRight, includeOffset: true);

            int totalRowsCount = Math.Max(Math.Max(useLeft.Length, useRight.Length), centerLines?.Count ?? 0);

            for (int row = 0; row < totalRowsCount; row++)
            {
                string l = row < useLeft.Length ? useLeft[row] ?? string.Empty : string.Empty;
                string c = (centerLines != null && row < centerLines.Count) ? centerLines[row] ?? string.Empty : string.Empty;
                string r = row < useRight.Length ? useRight[row] ?? string.Empty : string.Empty;

                if (l.Length > leftW) l = l.Substring(0, leftW); else l = l.PadRight(leftW);
                if (c.Length > FrameWidth) c = c.Substring(0, FrameWidth); else c = c.PadRight(FrameWidth);
                if (r.Length > rightW) r = r.Substring(0, rightW); else r = r.PadRight(rightW);

                string line = l + c + r;

                // Clip to the actual console width remaining after padLeft.
                int available = Math.Max(0, Console.WindowWidth - padLeft);
                if (line.Length > available)
                    line = line.Substring(0, Math.Max(0, available));

                try
                {
                    Console.SetCursorPosition(padLeft, Console.CursorTop);
                }
                catch
                {
                    // ignore
                }

                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Render center content with the currently loaded side art, ignoring the configured Offset.
        /// This is useful when the caller temporarily restores the original Console.Out (no global indent)
        /// and wants the frame centered inside the raw window width.
        /// </summary>
        public void RenderWithSides_NoOffset(IList<string> centerLines, string? leftOverridePath = null, string? rightOverridePath = null)
        {
            string[] useLeft = leftArt;
            string[] useRight = rightArt;

            if (!string.IsNullOrEmpty(leftOverridePath))
            {
                try { if (File.Exists(leftOverridePath)) useLeft = File.ReadAllLines(leftOverridePath); } catch { /* ignore */ }
            }

            if (!string.IsNullOrEmpty(rightOverridePath))
            {
                try { if (File.Exists(rightOverridePath)) useRight = File.ReadAllLines(rightOverridePath); } catch { /* ignore */ }
            }

            // Compute clamped widths without Offset
            var (leftW, rightW, totalWidth, padLeft) = ComputeClampedMetrics(centerLines, useLeft, useRight, includeOffset: false);

            int totalRows = Math.Max(Math.Max(useLeft.Length, useRight.Length), centerLines?.Count ?? 0);

            for (int row = 0; row < totalRows; row++)
            {
                string l = row < useLeft.Length ? useLeft[row] ?? string.Empty : string.Empty;
                string c = (centerLines != null && row < centerLines.Count) ? centerLines[row] ?? string.Empty : string.Empty;
                string r = row < useRight.Length ? useRight[row] ?? string.Empty : string.Empty;

                if (l.Length > leftW) l = l.Substring(0, leftW); else l = l.PadRight(leftW);
                if (c.Length > FrameWidth) c = c.Substring(0, FrameWidth); else c = c.PadRight(FrameWidth);
                if (r.Length > rightW) r = r.Substring(0, rightW); else r = r.PadRight(rightW);

                string line = l + c + r;

                int available = Math.Max(0, Console.WindowWidth - padLeft);
                if (line.Length > available)
                    line = line.Substring(0, Math.Max(0, available));

                try
                {
                    Console.SetCursorPosition(padLeft, Console.CursorTop);
                }
                catch
                {
                    // ignore
                }

                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Calculate content left when centering WITHOUT using configured Offset.
        /// Matches the math used by RenderWithSides_NoOffset.
        /// </summary>
        public int CalculateContentLeft_NoOffset(System.Collections.Generic.IList<string> centerLines, string? leftOverridePath = null, string? rightOverridePath = null)
        {
            string[] useLeft = leftArt;
            string[] useRight = rightArt;

            if (!string.IsNullOrEmpty(leftOverridePath))
            {
                try { if (File.Exists(leftOverridePath)) useLeft = File.ReadAllLines(leftOverridePath); } catch { /* ignore */ }
            }

            if (!string.IsNullOrEmpty(rightOverridePath))
            {
                try { if (File.Exists(rightOverridePath)) useRight = File.ReadAllLines(rightOverridePath); } catch { /* ignore */ }
            }

            var (leftW, rightW, totalWidth, padLeft) = ComputeClampedMetrics(centerLines, useLeft, useRight, includeOffset: false);

            // content begins after the left art portion
            return padLeft + leftW;
        }

        /// <summary>
        /// Calculate content left when centering USING the configured Offset.
        /// Matches the math used by RenderWithSides.
        /// </summary>
        public int CalculateContentLeft(System.Collections.Generic.IList<string> centerLines, string? leftOverridePath = null, string? rightOverridePath = null)
        {
            string[] useLeft = leftArt;
            string[] useRight = rightArt;

            if (!string.IsNullOrEmpty(leftOverridePath))
            {
                try { if (File.Exists(leftOverridePath)) useLeft = File.ReadAllLines(leftOverridePath); } catch { /* ignore */ }
            }

            if (!string.IsNullOrEmpty(rightOverridePath))
            {
                try { if (File.Exists(rightOverridePath)) useRight = File.ReadAllLines(rightOverridePath); } catch { /* ignore */ }
            }

            var (leftW, rightW, totalWidth, padLeft) = ComputeClampedMetrics(centerLines, useLeft, useRight, includeOffset: true);

            // Content begins after the left-art portion
            return padLeft + leftW;
        }

        // Factory for creating PageBuffer instances so buffering behaviour is centralized.
        public PageBuffer CreatePageBuffer(int indentLength) => new PageBuffer(indentLength);

        // High-level convenience: render framed header and return the content column.
        // If noOffset==true the frame is centered in the raw window (no configured Offset).
        public int RenderFrame(System.Collections.Generic.IList<string> centerLines, TextWriter? originalOut, TextWriter indentWriter, int globalIndent, bool noOffset = true, string? leftOverridePath = null, string? rightOverridePath = null)
        {
            // Draw using raw writer, compute left column, restore indent writer and position cursor.
            Console.SetOut(originalOut ?? Console.Out);
            if (noOffset)
                RenderWithSides_NoOffset(centerLines, leftOverridePath, rightOverridePath);
            else
                RenderWithSides(centerLines, leftOverridePath, rightOverridePath);

            int contentLeftNoOffset = noOffset
                ? CalculateContentLeft_NoOffset(centerLines, leftOverridePath, rightOverridePath)
                : CalculateContentLeft(centerLines, leftOverridePath, rightOverridePath);

            // Restore indented writer
            Console.SetOut(indentWriter);

            try { Console.SetCursorPosition(Math.Max(0, contentLeftNoOffset + globalIndent), Console.CursorTop); } catch { }

            // Return the column callers expect (raw content left + global indent)
            return contentLeftNoOffset + globalIndent;
        }

        /// <summary>
        /// Begin a buffered frame: renders the framed header raw (no global indent),
        /// installs a centralized PageBuffer as Console.Out and returns the content coords.
        /// </summary>
        // Backwards-compatible overload: callers that still pass a globalIndent (old contract)
        // will receive the column adjusted by globalIndent. Delegates to the raw no-indent variant.
        public (int contentLeft, int contentTop) BeginBufferedFrame(System.Collections.Generic.IList<string> centerLines, TextWriter? originalOut, TextWriter? indentWriter, int globalIndent, string? leftOverridePath = null, string? rightOverridePath = null, bool respectSideArtHeight = false)
        {
            var (contentLeftNoOffset, contentTop) = BeginBufferedFrame(centerLines, originalOut, indentWriter, leftOverridePath, rightOverridePath, respectSideArtHeight);
            return (contentLeftNoOffset + globalIndent, contentTop);
        }

        /// <summary>
        /// Begin a buffered frame: renders the framed header raw (no global indent),
        /// installs a centralized PageBuffer as Console.Out and returns the content coords.
        /// </summary>
        public (int contentLeft, int contentTop) BeginBufferedFrame(System.Collections.Generic.IList<string> centerLines, TextWriter? originalOut, TextWriter? indentWriter, string? leftOverridePath = null, string? rightOverridePath = null, bool respectSideArtHeight = false)
        {
            // Clear and draw side art + center frame using raw console
            Console.Clear();
            try { Console.SetOut(originalOut ?? Console.Out); } catch { /* ignore */ }

            int startRow;
            try { startRow = Console.CursorTop; } catch { startRow = 0; }

            // Prepare the side-art arrays the same way RenderWithSides_NoOffset does
            string[] useLeft = leftArt;
            string[] useRight = rightArt;

            if (!string.IsNullOrEmpty(leftOverridePath))
            {
                try { if (File.Exists(leftOverridePath)) useLeft = File.ReadAllLines(leftOverridePath); } catch { /* ignore */ }
            }

            if (!string.IsNullOrEmpty(rightOverridePath))
            {
                try { if (File.Exists(rightOverridePath)) useRight = File.ReadAllLines(rightOverridePath); } catch { /* ignore */ }
            }

            // Render frame without the configured Offset so positions are raw
            RenderWithSides_NoOffset(centerLines, leftOverridePath, rightOverridePath);

            // Raw content left inside the window (no indent)
            int contentLeftNoOffset = CalculateContentLeft_NoOffset(centerLines, leftOverridePath, rightOverridePath);

            // Determine contentTop:
            // - By default place buffered content immediately after the center header (centerLines.Count).
            // - If caller explicitly requests respectSideArtHeight=true, fall back to the taller-of-side-art behaviour.
            int contentTop;
            if (respectSideArtHeight)
            {
                int headerHeight = Math.Max(Math.Max(useLeft.Length, useRight.Length), centerLines?.Count ?? 0);
                contentTop = startRow + headerHeight;
            }
            else
            {
                contentTop = startRow + (centerLines?.Count ?? 0);
            }

            // Install centralized buffer
            _activeBuffer = CreatePageBuffer(indentLength: 0);
            _stagedOriginalOut = originalOut;
            _stagedIndentWriter = indentWriter;

            try { Console.SetOut(_activeBuffer); } catch { /* ignore */ }

            // Return raw (no-offset) content-left and top. Caller is responsible for mapping this into
            // any global indent it maintains (for example, an IndentTextWriter).
            return (contentLeftNoOffset, contentTop);
        }

        /// <summary>
        /// End the buffered frame by flushing the installed buffer into the raw console
        /// at the provided contentLeft/contentTop and restore the indented writer.
        /// </summary>
        public void EndBufferedFrame(int contentLeft, int contentTop)
        {
            var buf = _activeBuffer;
            if (buf == null) return;

            // Restore raw writer so flush writes at absolute coordinates
            try { Console.SetOut(_stagedOriginalOut ?? Console.Out); } catch { /* ignore */ }

            try
            {
                // Heuristic flush coordinate (prefer RAW no-offset coordinate when both candidates fit).
                int flushLeftCandidateRaw = Math.Max(0, contentLeft); // assume caller passed raw no-offset column
                int flushLeftCandidateOffset = Math.Max(0, contentLeft - Offset); // assume caller passed column including Offset

                int flushLeft = flushLeftCandidateRaw;

                try
                {
                    int winW = Console.WindowWidth;

                    // Prefer the raw candidate when it fits in the window.
                    if (flushLeftCandidateRaw + FrameWidth <= winW)
                    {
                        flushLeft = flushLeftCandidateRaw;
                    }
                    else if (flushLeftCandidateOffset + FrameWidth <= winW)
                    {
                        flushLeft = flushLeftCandidateOffset;
                    }
                    else
                    {
                        // Neither candidate fits fully; clamp to a visible column.
                        flushLeft = Math.Min(flushLeftCandidateRaw, Math.Max(0, winW - 1));
                    }
                }
                catch
                {
                    // Fallback: prefer raw candidate if we can't query window metrics.
                    flushLeft = flushLeftCandidateRaw;
                }

                buf.FlushToConsole(flushLeft, contentTop);
            }
            catch
            {
                /* swallow drawing errors */
            }

            // Restore indented writer for normal output
            try { Console.SetOut(_stagedIndentWriter ?? Console.Out); } catch { /* ignore */ }

            _activeBuffer = null;
            _stagedOriginalOut = null;
            _stagedIndentWriter = null;
        }

        // Lightweight layout diagnostics helper (reintroduced to avoid CS0103).
        // Writes to layout_debug.txt in AppContext.BaseDirectory and to Console.Error only when diagnostics enabled.
        private void LogLayoutDebug(string tag, string message)
        {
            try
            {
                string log = $"{DateTime.UtcNow:O} [{tag}] {message}";
                if (_diagnosticsEnabled)
                {
                    try { Console.Error.WriteLine(log); } catch { }
                }
                try
                {
                    var file = Path.Combine(AppContext.BaseDirectory, "layout_debug.txt");
                    File.AppendAllText(file, log + Environment.NewLine);
                }
                catch { }
            }
            catch { }
        }
    }

    // Buffering writer that collects text (with simple indentation) and can flush to console.
    // Centralized so both ConsoleUI and future renderers reuse the exact same logic.
    public sealed class PageBuffer : TextWriter
    {
        private readonly StringBuilder _sb = new();
        private readonly string _indent;
        private bool _beginLine = true;
        private readonly object _lock = new();

        public PageBuffer(int indentLength)
        {
            _indent = new string(' ', Math.Max(0, indentLength));
        }

        public override Encoding Encoding => System.Text.Encoding.UTF8;

        private void WriteCharInternal(char c)
        {
            if (_beginLine)
            {
                _sb.Append(_indent);
                _beginLine = false;
            }

            _sb.Append(c);
            if (c == '\n') _beginLine = true;
        }

        public override void Write(char value)
        {
            lock (_lock) { WriteCharInternal(value); }
        }

        public override void Write(string? value)
        {
            if (value == null) return;
            lock (_lock)
            {
                for (int i = 0; i < value.Length; i++)
                    WriteCharInternal(value[i]);
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (index < 0 || count < 0 || index + count > buffer.Length) throw new ArgumentOutOfRangeException();

            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                    WriteCharInternal(buffer[index + i]);
            }
        }

        public override void WriteLine()
        {
            lock (_lock)
            {
                if (_beginLine)
                {
                    _sb.Append(_indent);
                    _beginLine = false;
                }
                _sb.AppendLine();
                _beginLine = true;
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_lock)
            {
                Write(value);
                _sb.AppendLine();
                _beginLine = true;
            }
        }

        // Flush the buffered text to the console at given left/top coordinates.
        public void FlushToConsole(int left, int top)
        {
            string text;
            lock (_lock)
            {
                text = _sb.ToString();
                _sb.Clear();
            }

            var lines = text.Replace("\r\n", "\n").Split('\n');
            int row = top;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;

                try
                {
                    // Ensure row within buffer height
                    int safeRow = Math.Max(0, Math.Min(row, Console.BufferHeight - 1));
                    int safeCol = Math.Max(0, Math.Min(left, Console.BufferWidth - 1));
                    Console.SetCursorPosition(safeCol, safeRow);

                    // Clip the line to remaining width
                    int available = Math.Max(0, Console.WindowWidth - safeCol);
                    if (line.Length > available) line = line.Substring(0, available);

                    Console.Write(line);
                }
                catch
                {
                    // ignore drawing errors on tiny consoles or race conditions
                }

                row++;
            }
        }
    }
}