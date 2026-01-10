using Spacegun_Simulator.Core;
using Range = Spacegun_Simulator.Core.DevelopmentTuning.Range;

namespace Spacegun_Simulator.Enemies
{
    public static class EnemyConfigLoader
    {
        public static void LoadOrThrow()
        {
            LoadDesignationsOrThrow();
            LoadArchetypesOrThrow();
            LoadDoctrinesOrThrow();
        }

        private static void LoadDesignationsOrThrow(string relativePath = "Config/Enemies/EnemyDesignations.json")
        {
            if (!ConfigJson.TryDeserializeFile<EnemyDesignationsConfig>(relativePath, out var cfg) || cfg is null)
                throw new InvalidOperationException($"Required config file missing or invalid: '{relativePath}'.");

            if (cfg.TargetCountBase is null)
                throw new InvalidOperationException($"Missing required property 'TargetCountBase' in '{relativePath}'.");
            if (cfg.TargetCountTierBonus is null)
                throw new InvalidOperationException($"Missing required property 'TargetCountTierBonus' in '{relativePath}'.");
            if (cfg.TargetCountRandomMaxExclusive is null)
                throw new InvalidOperationException($"Missing required property 'TargetCountRandomMaxExclusive' in '{relativePath}'.");
            if (cfg.StealthChanceForLateTiers is null)
                throw new InvalidOperationException($"Missing required property 'StealthChanceForLateTiers' in '{relativePath}'.");

            if (cfg.EarlyTypes is null || cfg.EarlyTypes.Length == 0)
                throw new InvalidOperationException($"Missing or empty required property 'EarlyTypes' in '{relativePath}'.");
            if (cfg.MidTypes is null || cfg.MidTypes.Length == 0)
                throw new InvalidOperationException($"Missing or empty required property 'MidTypes' in '{relativePath}'.");
            if (cfg.LateTypes is null || cfg.LateTypes.Length == 0)
                throw new InvalidOperationException($"Missing or empty required property 'LateTypes' in '{relativePath}'.");

            static bool HasAnyNonWhitespace(string[] values)
            {
                foreach (var s in values)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        return true;
                }
                return false;
            }

            if (!HasAnyNonWhitespace(cfg.EarlyTypes))
                throw new InvalidOperationException($"Property 'EarlyTypes' in '{relativePath}' must contain at least one non-whitespace string.");
            if (!HasAnyNonWhitespace(cfg.MidTypes))
                throw new InvalidOperationException($"Property 'MidTypes' in '{relativePath}' must contain at least one non-whitespace string.");
            if (!HasAnyNonWhitespace(cfg.LateTypes))
                throw new InvalidOperationException($"Property 'LateTypes' in '{relativePath}' must contain at least one non-whitespace string.");

            if (cfg.CampaignNamePrefixes is null || cfg.CampaignNamePrefixes.Length == 0)
                throw new InvalidOperationException($"Missing or empty required property 'CampaignNamePrefixes' in '{relativePath}'.");
            if (cfg.CampaignNameSuffixes is null || cfg.CampaignNameSuffixes.Length == 0)
                throw new InvalidOperationException($"Missing or empty required property 'CampaignNameSuffixes' in '{relativePath}'.");

            if (!HasAnyNonWhitespace(cfg.CampaignNamePrefixes))
                throw new InvalidOperationException($"Property 'CampaignNamePrefixes' in '{relativePath}' must contain at least one non-whitespace string.");
            if (!HasAnyNonWhitespace(cfg.CampaignNameSuffixes))
                throw new InvalidOperationException($"Property 'CampaignNameSuffixes' in '{relativePath}' must contain at least one non-whitespace string.");

            if (cfg.CrossSectionRanges is null || cfg.CrossSectionRanges.Count == 0)
                throw new InvalidOperationException($"Missing or empty required property 'CrossSectionRanges' in '{relativePath}'.");

            EnemyNaming.ReplaceAll(cfg.CampaignNamePrefixes, cfg.CampaignNameSuffixes);

            var cross = new Dictionary<string, (double Min, double Max)>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in cfg.CrossSectionRanges)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;
                cross[kvp.Key] = (kvp.Value.Min, kvp.Value.Max);
            }

            if (cross.Count == 0)
                throw new InvalidOperationException($"Property 'CrossSectionRanges' in '{relativePath}' must contain at least one non-empty key.");

            EnemyDesignations.ReplaceAll(new EnemyDesignations.EnemyDesignationsValues(
                TargetCountBase: cfg.TargetCountBase.Value,
                TargetCountTierBonus: cfg.TargetCountTierBonus.Value,
                TargetCountRandomMaxExclusive: cfg.TargetCountRandomMaxExclusive.Value,
                EarlyTypes: cfg.EarlyTypes,
                MidTypes: cfg.MidTypes,
                LateTypes: cfg.LateTypes,
                CrossSectionRanges: cross,
                StealthChanceForLateTiers: cfg.StealthChanceForLateTiers.Value
            ));
        }

        private static void LoadArchetypesOrThrow(string relativePath = "Config/Enemies/EnemyArchetypes.json")
        {
            if (!ConfigJson.TryDeserializeFile<EnemyArchetypesConfig>(relativePath, out var cfg) || cfg?.Archetypes is null)
                throw new InvalidOperationException($"Required config file missing or invalid: '{relativePath}'.");

            EnemyArchetypeCatalog.ReplaceAll(cfg.Archetypes);
        }

        private static void LoadDoctrinesOrThrow(string relativePath = "Config/Enemies/EnemyDoctrines.json")
        {
            if (!ConfigJson.TryDeserializeFile<EnemyDoctrinesConfig>(relativePath, out var cfg) || cfg is null)
                throw new InvalidOperationException($"Required config file missing or invalid: '{relativePath}'.");

            var profiles = new List<EnemyDoctrineProfile>();
            foreach (var p in cfg.Profiles ?? Array.Empty<EnemyDoctrineProfileConfig>())
            {
                if (!Enum.TryParse<EnemyDoctrine>(p.Doctrine ?? string.Empty, ignoreCase: true, out var doctrine))
                    continue;

                profiles.Add(new EnemyDoctrineProfile(
                    Doctrine: doctrine,
                    Name: p.Name ?? doctrine.ToString(),
                    Description: p.Description ?? string.Empty,
                    VelocityMultiplier: p.VelocityMultiplier,
                    AccelerationMultiplier: p.AccelerationMultiplier,
                    ManeuverabilityMultiplier: p.ManeuverabilityMultiplier,
                    DefenseMultiplier: p.DefenseMultiplier,
                    OffenseMultiplier: p.OffenseMultiplier,
                    StealthChanceMultiplier: p.StealthChanceMultiplier,
                    RadarCrossSectionMultiplier: p.RadarCrossSectionMultiplier
                ));
            }

            var primaryPools = ParsePools(cfg.PrimaryDoctrinePoolsByArchetypeId);
            var guestPools = ParsePools(cfg.GuestDoctrinePoolsByArchetypeId);

            EnemyDoctrineCatalog.ReplaceAll(profiles, primaryPools, guestPools);
        }

        private static Dictionary<string, EnemyDoctrine[]> ParsePools(Dictionary<string, string[]>? raw)
        {
            var result = new Dictionary<string, EnemyDoctrine[]>(StringComparer.OrdinalIgnoreCase);
            if (raw is null)
                return result;

            foreach (var kvp in raw)
            {
                var list = new List<EnemyDoctrine>();
                foreach (var s in kvp.Value ?? Array.Empty<string>())
                {
                    if (Enum.TryParse<EnemyDoctrine>(s ?? string.Empty, ignoreCase: true, out var d))
                        list.Add(d);
                }

                result[kvp.Key] = list.Distinct().ToArray();
            }

            return result;
        }

        private sealed class EnemyDesignationsConfig
        {
            public int? TargetCountBase { get; set; }
            public int? TargetCountTierBonus { get; set; }
            public int? TargetCountRandomMaxExclusive { get; set; }

            public string[]? EarlyTypes { get; set; }
            public string[]? MidTypes { get; set; }
            public string[]? LateTypes { get; set; }

            public Dictionary<string, Range>? CrossSectionRanges { get; set; }
            public double? StealthChanceForLateTiers { get; set; }

            public string[]? CampaignNamePrefixes { get; set; }
            public string[]? CampaignNameSuffixes { get; set; }
        }

        private sealed class EnemyArchetypesConfig
        {
            public List<EnemyArchetype>? Archetypes { get; set; }
        }

        private sealed class EnemyDoctrinesConfig
        {
            public EnemyDoctrineProfileConfig[]? Profiles { get; set; }
            public Dictionary<string, string[]>? PrimaryDoctrinePoolsByArchetypeId { get; set; }
            public Dictionary<string, string[]>? GuestDoctrinePoolsByArchetypeId { get; set; }
        }

        private sealed class EnemyDoctrineProfileConfig
        {
            public string? Doctrine { get; set; }
            public string? Name { get; set; }
            public string? Description { get; set; }

            public double VelocityMultiplier { get; set; }
            public double AccelerationMultiplier { get; set; }
            public double ManeuverabilityMultiplier { get; set; }
            public double DefenseMultiplier { get; set; }
            public double OffenseMultiplier { get; set; }
            public double StealthChanceMultiplier { get; set; }
            public double RadarCrossSectionMultiplier { get; set; }
        }
    }

    public static class EnemyNaming
    {
        private static string[] _prefixes = Array.Empty<string>();
        private static string[] _suffixes = Array.Empty<string>();

        public static void ReplaceAll(string[]? prefixes, string[]? suffixes)
        {
            _prefixes = (prefixes ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            _suffixes = (suffixes ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        }

        public static string GenerateCampaignName(string archetypeName, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            if (_prefixes.Length == 0 || _suffixes.Length == 0)
                throw new InvalidOperationException("Enemy naming pools not loaded. Ensure EnemyConfigLoader.LoadOrThrow() is called during startup.");

            return $"{archetypeName}-Class {_prefixes[rng.Next(_prefixes.Length)]} {_suffixes[rng.Next(_suffixes.Length)]}";
        }
    }
}
