using System.Text.Json;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Core.Stats;

namespace Spacegun_Simulator.Core
{
    public static class WeaponsUpgradesLoader
    {
        public static void LoadIfExists(string relativePath = "Config/WeaponsUpgrades.json")
        {
            if (!ConfigJson.TryDeserializeFile<WeaponsUpgradesConfig>(relativePath, out var cfg))
                return;

            if (cfg?.Upgrades is null)
                return;

            var defs = new List<WeaponsUpgrades.UpgradeDefinition>(cfg.Upgrades.Length);

            foreach (var u in cfg.Upgrades)
            {
                var cost = u.Cost?.ToResourceCost() ?? ResourceCost.None;

                var modifiers = ParseModifiers(u.Modifiers);

                defs.Add(new WeaponsUpgrades.UpgradeDefinition(
                    Id: u.Id,
                    Name: u.Name,
                    Description: u.Description,
                    Cost: cost,
                    Prerequisites: u.Prerequisites ?? Array.Empty<string>(),
                    MinWeaponsTechLevel: u.MinWeaponsTechLevel,
                    MinProjectilesTechLevel: u.MinProjectilesTechLevel,
                    RequiresGuidanceMod: u.RequiresGuidanceMod ?? false,
                    RequiresPropulsion: u.RequiresPropulsion,
                    Parameters: u.Parameters ?? new Dictionary<string, double>(),
                    Modifiers: modifiers
                ));
            }

            WeaponsUpgrades.Apply(defs);

            // Optional: treat upgrade definitions as the single source of truth for wear modifiers.
            // This is safe because it only affects guns that actually have InstalledUpgrades set.
            ApplyWearModifiersFromUpgrades(defs);

            ValidatePrerequisites(defs);
        }

        private static IReadOnlyList<StatModifier> ParseModifiers(StatModifierConfig[]? configs)
        {
            if (configs is null || configs.Length == 0)
                return Array.Empty<StatModifier>();

            var list = new List<StatModifier>(configs.Length);
            foreach (var c in configs)
            {
                if (c is null) continue;
                if (string.IsNullOrWhiteSpace(c.Key)) continue;
                if (!TryParseOp(c.Op, out var op))
                {
                    TryWarn($"[WeaponsUpgrades] Unknown modifier op '{c.Op}' for key '{c.Key}'.");
                    continue;
                }

                list.Add(new StatModifier(c.Key.Trim(), op, c.Value));
            }

            return list;
        }

        private static bool TryParseOp(string? op, out StatModifierOp parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(op))
                return false;

            switch (op.Trim())
            {
                case "Add":
                case "add":
                    parsed = StatModifierOp.Add;
                    return true;
                case "Mul":
                case "mul":
                case "Multiply":
                case "multiply":
                    parsed = StatModifierOp.Mul;
                    return true;
                case "Set":
                case "set":
                    parsed = StatModifierOp.Set;
                    return true;
                case "ClampMin":
                case "clampMin":
                case "Min":
                case "min":
                    parsed = StatModifierOp.ClampMin;
                    return true;
                case "ClampMax":
                case "clampMax":
                case "Max":
                case "max":
                    parsed = StatModifierOp.ClampMax;
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidatePrerequisites(IReadOnlyList<WeaponsUpgrades.UpgradeDefinition> defs)
        {
            if (defs is null || defs.Count == 0)
                return;

            var byId = new Dictionary<string, WeaponsUpgrades.UpgradeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in defs)
            {
                if (!string.IsNullOrWhiteSpace(def.Id))
                    byId[def.Id] = def;
            }

            foreach (var def in defs)
            {
                if (def.Prerequisites is null || def.Prerequisites.Length == 0)
                    continue;

                foreach (var prereqId in def.Prerequisites)
                {
                    if (string.IsNullOrWhiteSpace(prereqId))
                        continue;

                    if (!byId.ContainsKey(prereqId))
                    {
                        TryWarn($"[WeaponsUpgrades] Upgrade '{def.Id}' has unknown prerequisite '{prereqId}'.");
                        continue;
                    }
                }
            }
        }

        private static void TryWarn(string message)
        {
            try { Console.Error.WriteLine(message); }
            catch { }
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
