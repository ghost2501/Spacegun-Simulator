using System.Text.Json;

namespace Spacegun_Simulator.Core
{
    public static class DevelopmentTuningLoader
    {
        public static void LoadIfExists(string relativePath = "Config/DevelopmentTuning.json")
        {
            if (!ConfigJson.TryDeserializeFile<DevelopmentTuningConfig>(relativePath, out var cfg))
                return;

            DevelopmentTuning.Apply(cfg!);
        }
    }
}
