using System.Collections.Concurrent;

namespace Spacegun_Simulator.Audio.Backends;

public static class AudioBackendDiagnostics
{
    private static readonly ConcurrentDictionary<string, byte> _once = new(StringComparer.Ordinal);

    public static void LogOnce(string key, string message)
    {
        if (!_once.TryAdd(key, 0)) return;

        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Saves", "Logs");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, "audio.log");
            var line = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Never let diagnostics break gameplay/UI.
        }
    }
}
