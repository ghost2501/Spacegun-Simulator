using System.Text.Json;

namespace Spacegun_Simulator.Core
{
    internal static class ConfigJson
    {
        public static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static string? TryResolveConfigPath(string relativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return null;

                string rel = relativePath.Replace('/', Path.DirectorySeparatorChar);

                // Support running from:
                // - project directory ("Config/..." exists)
                // - repo root ("Spacegun Simulator/Config/..." exists)
                // - bin output (if configs are copied alongside output)
                string[] candidates = new[]
                {
                    Path.Combine(Environment.CurrentDirectory, rel),
                    Path.Combine(Environment.CurrentDirectory, "Spacegun Simulator", rel),
                    Path.Combine(AppContext.BaseDirectory, rel),
                };

                foreach (var candidate in candidates)
                {
                    try
                    {
                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch
                    {
                        // keep probing
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static bool TryDeserializeFile<T>(string relativePath, out T? result, JsonSerializerOptions? options = null)
        {
            result = default;

            try
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return false;

                string? resolved = TryResolveConfigPath(relativePath) ?? (File.Exists(relativePath) ? relativePath : null);
                if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                    return false;

                var json = File.ReadAllText(resolved);
                result = JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
                return result is not null;
            }
            catch
            {
                // Intentionally ignore config errors to keep the game runnable.
                return false;
            }
        }
    }
}
