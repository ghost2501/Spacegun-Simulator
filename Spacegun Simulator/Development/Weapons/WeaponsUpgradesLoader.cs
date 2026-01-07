using System.Text.Json;
using Spacegun_Simulator.Development.Shared;

namespace Spacegun_Simulator.Core
{
    public static class WeaponsUpgradesLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static void LoadIfExists(string relativePath = "Config/WeaponsUpgrades.json")
        {
            try
            {
                if (!File.Exists(relativePath))
                    return;

                var json = File.ReadAllText(relativePath);
                var cfg = JsonSerializer.Deserialize<WeaponsUpgradesConfig>(json, JsonOptions);
                if (cfg?.Upgrades is null)
                    return;

                var defs = new List<WeaponsUpgrades.UpgradeDefinition>(cfg.Upgrades.Length);

                foreach (var u in cfg.Upgrades)
                {
                    var cost = u.Cost?.ToResourceCost() ?? ResourceCost.None;
                    defs.Add(new WeaponsUpgrades.UpgradeDefinition(
                        Id: u.Id,
                        Name: u.Name,
                        Description: u.Description,
                        Cost: cost,
                        Prerequisites: u.Prerequisites ?? Array.Empty<string>(),
                        StatModifiers: u.StatModifiers ?? new Dictionary<string, double>(),
                        MinWeaponsTechLevel: u.MinWeaponsTechLevel,
                        MinProjectilesTechLevel: u.MinProjectilesTechLevel,
                        RequiresGuidanceMod: u.RequiresGuidanceMod ?? false,
                        RequiresPropulsion: u.RequiresPropulsion,
                        Parameters: u.Parameters ?? new Dictionary<string, double>()
                    ));
                }

                WeaponsUpgrades.Apply(defs);

                // Optional: treat upgrade definitions as the single source of truth for wear modifiers.
                // This is safe because it only affects guns that actually have InstalledUpgrades set.
                ApplyWearModifiersFromUpgrades(defs);
            }
            catch
            {
                // Keep game runnable if config is malformed.
            }
        }

        private static void ApplyWearModifiersFromUpgrades(IReadOnlyList<WeaponsUpgrades.UpgradeDefinition> defs)
        {
            if (defs is null || defs.Count == 0)
                return;

            var wearMap = new Dictionary<string, double>();
            foreach (var def in defs)
            {
                if (def.Parameters is null) continue;
                if (def.Parameters.TryGetValue("WearMultiplier", out double mult))
                    wearMap[def.Id] = mult;
            }

            if (wearMap.Count == 0)
                return;

            // Respect explicit overrides from WeaponsTuning.json.
            // If the current map differs from defaults, assume the user customized it.
            var current = WeaponsTuning.Gun.WearModifiersByUpgradeId;
            var defaults = WeaponsTuning.GunTuningValues.CreateDefaults().WearModifiersByUpgradeId;
            if (!IsSameMap(current, defaults))
                return;

            WeaponsTuning.Apply(new WeaponsTuningConfig
            {
                GunTuning = new GunTuningConfig
                {
                    WearModifiersByUpgradeId = wearMap
                }
            });
        }

        private static bool IsSameMap(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
            {
                if (!b.TryGetValue(k, out double other)) return false;
                if (v != other) return false;
            }
            return true;
        }
    }
}
