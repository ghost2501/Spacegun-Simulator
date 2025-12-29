using System;
using System.IO;

namespace Spacegun_Simulator.UI.Screen
{
    /// <summary>
    /// Helpers for prompting for Console.ReadLine input aligned to the left edge of the center frame.
    /// This exists as a bridge while legacy ReadLine-based flows are migrated into page-based UI.
    /// </summary>
    public static class FramedPrompts
    {
        /// <summary>
        /// Creates a writer that indents output so subsequent Console.Write/WriteLine calls start at frame-left.
        /// </summary>
        public static TextWriter CreateFrameWriter(TextWriter rawOut, int frameContentLeftNoOffset)
            => new FrameIndentWriter(rawOut, frameContentLeftNoOffset);

        /// <summary>
        /// Sets Console.Out and anchors the cursor at the provided raw (no-indent) coordinates.
        /// </summary>
        public static void Anchor(TextWriter frameWriter, int leftNoOffset, int rowNoOffset)
        {
            try { Console.SetOut(frameWriter); } catch { }
            try { Console.SetCursorPosition(Math.Max(0, leftNoOffset), Math.Max(0, rowNoOffset)); } catch { }
        }

        public static string? ReadLineAt(TextWriter frameWriter, int leftNoOffset, int rowNoOffset, string prompt)
        {
            Anchor(frameWriter, leftNoOffset, rowNoOffset);
            Console.Write(prompt);
            return Console.ReadLine();
        }

        public static float ReadFloatAt(
            TextWriter frameWriter,
            int leftNoOffset,
            ref int rowNoOffset,
            string prompt,
            Func<float, bool> predicate)
        {
            while (true)
            {
                string? text = ReadLineAt(frameWriter, leftNoOffset, rowNoOffset, prompt);

                // Console's input echo does not pass through Console.Out; cursor moved anyway.
                try { rowNoOffset = Console.CursorTop; }
                catch { rowNoOffset++; }

                if (string.IsNullOrWhiteSpace(text))
                    return 0f;

                if (float.TryParse(
                        text.Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float value)
                    && predicate(value))
                    return value;

                Anchor(frameWriter, leftNoOffset, rowNoOffset);
                Console.WriteLine("Invalid value. Please try again.");
                try { rowNoOffset = Console.CursorTop; }
                catch { rowNoOffset++; }
            }
        }

        /// <summary>
        /// Places the input cursor inside the center frame using raw (no-indent) coordinates returned by ScreenLayout.
        /// This is primarily used by legacy flows that still call Console.ReadLine after buffered frame rendering.
        /// </summary>
        /// <param name="restoreWriter">Writer to restore (typically the global indent writer used by legacy UI).</param>
        /// <param name="globalIndent">Global indent spaces applied by the legacy writer.</param>
        /// <param name="contentLeftNoOffset">Raw column where the center frame content starts.</param>
        /// <param name="promptRowNoOffset">Raw row where the prompt should appear.</param>
        public static void PositionPromptCursor_NoOffset(
            TextWriter restoreWriter,
            int globalIndent,
            int contentLeftNoOffset,
            int promptRowNoOffset)
        {
            try
            {
                Console.SetOut(restoreWriter);

                int col = Math.Max(0, contentLeftNoOffset + Math.Max(0, globalIndent));

                int targetRow;
                try
                {
                    targetRow = Math.Max(promptRowNoOffset, 0);

                    // Clamp to buffer height when possible.
                    if (targetRow >= Console.BufferHeight) targetRow = Console.BufferHeight - 1;
                    if (targetRow < 0) targetRow = 0;
                }
                catch
                {
                    targetRow = Math.Max(promptRowNoOffset, 0);
                }

                // Some consoles disallow SetCursorPosition to rows ahead of current cursor.
                // Try SetCursorPosition, otherwise write newlines until we reach the target row.
                try
                {
                    Console.SetCursorPosition(col, targetRow);
                }
                catch
                {
                    try
                    {
                        Console.SetCursorPosition(col, Console.CursorTop);
                    }
                    catch { /* ignore */ }

                    int current;
                    try { current = Console.CursorTop; }
                    catch { current = 0; }

                    while (current < targetRow)
                    {
                        Console.WriteLine();
                        current++;
                    }

                    try { Console.SetCursorPosition(col, Math.Min(targetRow, Console.CursorTop)); }
                    catch { /* ignore */ }
                }
            }
            catch
            {
                try { Console.SetOut(restoreWriter); } catch { }
            }
        }

        /// <summary>
        /// TextWriter that indents at the start of each line.
        /// Similar to ConsoleUI's internal IndentTextWriter, but reusable from UI code.
        /// </summary>
        private sealed class FrameIndentWriter : TextWriter
        {
            private readonly TextWriter _inner;
            private readonly string _indent;
            private bool _beginLine = true;
            private readonly object _lock = new();

            public FrameIndentWriter(TextWriter inner, int indentSpaces)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _indent = new string(' ', Math.Max(0, indentSpaces));
            }

            public override System.Text.Encoding Encoding => _inner.Encoding;

            private bool ShouldIndent()
            {
                if (!_beginLine) return false;
                try { return Console.CursorLeft == 0; }
                catch { return true; }
            }

            public override void Write(char value)
            {
                lock (_lock)
                {
                    if (_beginLine && ShouldIndent())
                    {
                        _inner.Write(_indent);
                        _beginLine = false;
                    }

                    _inner.Write(value);
                    _beginLine = value == '\n';
                }
            }

            public override void Write(string? value)
            {
                if (value == null) return;

                lock (_lock)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        char c = value[i];

                        if (_beginLine && ShouldIndent())
                        {
                            _inner.Write(_indent);
                            _beginLine = false;
                        }

                        _inner.Write(c);

                        if (c == '\n')
                            _beginLine = true;
                    }
                }
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (index < 0 || count < 0 || index + count > buffer.Length) throw new ArgumentOutOfRangeException();

                lock (_lock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        char c = buffer[index + i];

                        if (_beginLine && ShouldIndent())
                        {
                            _inner.Write(_indent);
                            _beginLine = false;
                        }

                        _inner.Write(c);

                        if (c == '\n')
                            _beginLine = true;
                    }
                }
            }

            public override void WriteLine()
            {
                Write('\n');
            }

            public override void WriteLine(string? value)
            {
                Write(value);
                Write('\n');
            }
        }
    }
}
