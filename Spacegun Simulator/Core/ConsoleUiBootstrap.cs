using System.Text;
using System.Runtime.InteropServices;

namespace Spacegun_Simulator.Core
{
    internal static class ConsoleUiBootstrap
    {
        // Windows console UTF-8 code page.
        private const uint CP_UTF8 = 65001;

        public readonly record struct UiEncodingSetupResult(
            bool IsOutputRedirected,
            bool Utf8LikelyActive,
            bool AsciiFallbackEnabled);

        private static UiEncodingSetupResult? _lastSetupResult;

        public static UiEncodingSetupResult? LastSetupResult => _lastSetupResult;

        public static void WriteDiagnostics(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteLine("=== Console Diagnostics ===");
            writer.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

            try { writer.WriteLine($"IsOutputRedirected: {Console.IsOutputRedirected}"); } catch { }
            try { writer.WriteLine($"IsInputRedirected: {Console.IsInputRedirected}"); } catch { }
            try { writer.WriteLine($"IsErrorRedirected: {Console.IsErrorRedirected}"); } catch { }

            try { writer.WriteLine($"Console.OutputEncoding: {Console.OutputEncoding.WebName}"); } catch { }
            try { writer.WriteLine($"Console.InputEncoding:  {Console.InputEncoding.WebName}"); } catch { }

            try { writer.WriteLine($"ConsoleTextMode.AsciiOnly: {ConsoleTextMode.AsciiOnly}"); } catch { }
            try { writer.WriteLine($"ConsoleTextMode.AsciiOnlyForcedByUser: {ConsoleTextMode.AsciiOnlyForcedByUser}"); } catch { }

            var setup = LastSetupResult;
            if (setup.HasValue)
            {
                writer.WriteLine("--- UI Encoding Setup ---");
                writer.WriteLine($"IsOutputRedirected: {setup.Value.IsOutputRedirected}");
                writer.WriteLine($"Utf8LikelyActive:  {setup.Value.Utf8LikelyActive}");
                writer.WriteLine($"AsciiFallbackEnabled: {setup.Value.AsciiFallbackEnabled}");
            }

            if (OperatingSystem.IsWindows())
            {
                try { writer.WriteLine($"WinConsole OutputCP: {GetConsoleOutputCP()}"); } catch { }
                try { writer.WriteLine($"WinConsole InputCP:  {GetConsoleCP()}"); } catch { }
            }

            try { writer.WriteLine($"Console.Encoding (ScreenLayout buffers): {Encoding.UTF8.WebName}"); } catch { }
            writer.WriteLine("===========================");
        }

        public static UiEncodingSetupResult ConfigureForUi()
        {
            // If output is redirected (pipes, files), changing console code pages is pointless
            // and can sometimes cause surprises for downstream tools.
            if (Console.IsOutputRedirected)
            {
                var redirected = new UiEncodingSetupResult(
                    IsOutputRedirected: true,
                    Utf8LikelyActive: false,
                    AsciiFallbackEnabled: false);
                _lastSetupResult = redirected;
                return redirected;
            }

            bool ok = true;

            // Prefer UTF-8 so box drawing and symbols render consistently.
            try { Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); }
            catch { ok = false; }

            try { Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); }
            catch { ok = false; }

            // On Windows, the host also has a code page; update it to match.
            if (OperatingSystem.IsWindows())
            {
                bool outSet;
                bool inSet;

                try { outSet = SetConsoleOutputCP(CP_UTF8); }
                catch { outSet = false; }

                try { inSet = SetConsoleCP(CP_UTF8); }
                catch { inSet = false; }

                if (!outSet || !inSet)
                    ok = false;

                // Verify the host accepted the change.
                try
                {
                    if (GetConsoleOutputCP() != CP_UTF8)
                        ok = false;
                }
                catch { ok = false; }
            }

            // Verify encoding reports as UTF-8.
            try
            {
                if (!string.Equals(Console.OutputEncoding.WebName, "utf-8", StringComparison.OrdinalIgnoreCase))
                    ok = false;
            }
            catch { ok = false; }

            if (!ok)
                ConsoleTextMode.EnableAsciiOnly();

            var result = new UiEncodingSetupResult(
                IsOutputRedirected: false,
                Utf8LikelyActive: ok,
                AsciiFallbackEnabled: !ok);

            _lastSetupResult = result;
            return result;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleOutputCP();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleCP();
    }
}
