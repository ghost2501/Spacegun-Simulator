using System.Text.Json;

namespace Spacegun_Simulator.Core
{
    public static class WeaponsTuningLoader
    {
        public static void LoadIfExists(string relativePath = "Config/WeaponsTuning.json")
        {
            if (!ConfigJson.TryDeserializeFile<WeaponsTuningConfig>(relativePath, out var cfg))
                return;

            WeaponsTuning.Apply(cfg!);
        }
    }
}
