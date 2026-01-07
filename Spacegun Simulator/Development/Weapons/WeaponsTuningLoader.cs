using System.Text.Json;

namespace Spacegun_Simulator.Core
{
    public static class WeaponsTuningLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static void LoadIfExists(string relativePath = "Config/WeaponsTuning.json")
        {
            try
            {
                if (!File.Exists(relativePath))
                    return;

                var json = File.ReadAllText(relativePath);
                var cfg = JsonSerializer.Deserialize<WeaponsTuningConfig>(json, JsonOptions);
                if (cfg is null)
                    return;

                WeaponsTuning.Apply(cfg);
            }
            catch
            {
                // Intentionally ignore config errors to keep the game runnable.
            }
        }
    }
}
