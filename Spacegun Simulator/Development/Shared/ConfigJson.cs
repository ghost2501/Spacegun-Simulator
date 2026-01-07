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

        public static bool TryDeserializeFile<T>(string relativePath, out T? result, JsonSerializerOptions? options = null)
        {
            result = default;

            try
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return false;

                if (!File.Exists(relativePath))
                    return false;

                var json = File.ReadAllText(relativePath);
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
