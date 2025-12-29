using System.Text;

namespace Spacegun_Simulator.UI.Screen
{
    /// <summary>
    /// TextWriter that indents at the start of each line.
    /// Used to preserve legacy "global indent" behavior outside page-buffered rendering.
    /// </summary>
    public sealed class IndentTextWriter : TextWriter
    {
        private readonly TextWriter _inner;
        private readonly string _indent;
        private bool _beginLine = true;
        private readonly object _lock = new();

        public IndentTextWriter(TextWriter inner, int indentSpaces)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _indent = new string(' ', Math.Max(0, indentSpaces));
        }

        public int IndentLength => _indent.Length;

        public override Encoding Encoding => _inner.Encoding;

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
                    Write(value[i]);
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (index < 0 || count < 0 || index + count > buffer.Length) throw new ArgumentOutOfRangeException();

            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                    Write(buffer[index + i]);
            }
        }

        public override void WriteLine() => Write('\n');

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        public override void Flush() => _inner.Flush();

        protected override void Dispose(bool disposing)
        {
            // Intentionally don't dispose the inner writer (Console.Out)
            base.Dispose(disposing);
        }
    }
}
