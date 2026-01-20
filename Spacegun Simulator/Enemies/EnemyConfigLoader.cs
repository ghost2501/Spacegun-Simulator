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

            EnemyNaming.ReplaceAll(
                cfg.CampaignNamePrefixes,
                cfg.CampaignNameSuffixes,
                targetNameIncludeSerial: cfg.TargetNameIncludeSerial ?? true,
                cfg.TargetNamePrefixesByDoctrine,
                cfg.TargetNameSuffixesByArchetypeId);

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

            public bool? TargetNameIncludeSerial { get; set; }

            // Optional: more semantic target naming.
            // Keys are EnemyDoctrine enum names and EnemyArchetype ids.
            public Dictionary<string, string[]>? TargetNamePrefixesByDoctrine { get; set; }
            public Dictionary<string, string[]>? TargetNameSuffixesByArchetypeId { get; set; }
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

        private static Dictionary<string, string[]> _targetPrefixesByDoctrine = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string[]> _targetSuffixesByArchetypeId = new(StringComparer.OrdinalIgnoreCase);

        private static bool _targetNameIncludeSerial = true;

        public static void ReplaceAll(
            string[]? prefixes,
            string[]? suffixes,
            bool targetNameIncludeSerial,
            Dictionary<string, string[]>? targetPrefixesByDoctrine,
            Dictionary<string, string[]>? targetSuffixesByArchetypeId)
        {
            _prefixes = (prefixes ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            _suffixes = (suffixes ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            _targetNameIncludeSerial = targetNameIncludeSerial;

            _targetPrefixesByDoctrine = NormalizePoolMap(targetPrefixesByDoctrine);
            _targetSuffixesByArchetypeId = NormalizePoolMap(targetSuffixesByArchetypeId);
        }

        public static void ReplaceAll(string[]? prefixes, string[]? suffixes)
        {
            ReplaceAll(prefixes, suffixes, targetNameIncludeSerial: true, targetPrefixesByDoctrine: null, targetSuffixesByArchetypeId: null);
        }

        private static Dictionary<string, string[]> NormalizePoolMap(Dictionary<string, string[]>? map)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (map is null)
                return result;

            foreach (var kvp in map)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                var cleaned = (kvp.Value ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToArray();

                if (cleaned.Length == 0)
                    continue;

                result[kvp.Key.Trim()] = cleaned;
            }

            return result;
        }

        public static string GenerateCampaignName(string archetypeName, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            if (_prefixes.Length == 0 || _suffixes.Length == 0)
                throw new InvalidOperationException("Enemy naming pools not loaded. Ensure EnemyConfigLoader.LoadOrThrow() is called during startup.");

            return $"{archetypeName}-Class {_prefixes[rng.Next(_prefixes.Length)]} {_suffixes[rng.Next(_suffixes.Length)]}";
        }

        public static string GenerateCampaignName(EnemyArchetype archetype, EnemyDoctrine doctrine, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            if (_prefixes.Length == 0 || _suffixes.Length == 0)
                throw new InvalidOperationException("Enemy naming pools not loaded. Ensure EnemyConfigLoader.LoadOrThrow() is called during startup.");

            static string Norm(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

            string PickNonMatching(string[] pool, string? not1, string? not2)
            {
                if (pool.Length == 0)
                    return string.Empty;

                for (int tries = 0; tries < 12; tries++)
                {
                    string candidate = pool[rng.Next(pool.Length)];
                    string c = Norm(candidate);
                    if (!string.IsNullOrWhiteSpace(c)
                        && c != not1
                        && c != not2)
                        return candidate.Trim();
                }

                return (pool[rng.Next(pool.Length)] ?? string.Empty).Trim();
            }

            // Prefer semantic pools if configured; otherwise fall back to the legacy campaign pools.
            string doctrineKey = doctrine.ToString();
            string[] prefixPool = _targetPrefixesByDoctrine.TryGetValue(doctrineKey, out var dp)
                ? dp
                : _prefixes;

            string[] suffixPool = _targetSuffixesByArchetypeId.TryGetValue(archetype.Id, out var ap)
                ? ap
                : _suffixes;

            string prefix = PickNonMatching(prefixPool, not1: null, not2: null);
            string p = Norm(prefix);
            string suffix = PickNonMatching(suffixPool, not1: null, not2: p);
            string s = Norm(suffix);

            string callSign = string.IsNullOrWhiteSpace(prefix)
                ? suffix
                : (string.IsNullOrWhiteSpace(suffix) || s == p)
                    ? prefix
                    : $"{prefix} {suffix}";

            if (string.IsNullOrWhiteSpace(callSign))
                callSign = $"{_prefixes[rng.Next(_prefixes.Length)]} {_suffixes[rng.Next(_suffixes.Length)]}";

            return $"{archetype.Name}-Class {callSign}";
        }

        public static string GenerateTargetName(EnemyArchetype archetype, EnemyDoctrine doctrine, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            // Best-effort: if naming pools aren't loaded (tests/partial harness), fall back safely.
            if (_prefixes.Length == 0 || _suffixes.Length == 0)
            {
                int serialFallback = rng.Next(100, 999);
                return _targetNameIncludeSerial
                    ? $"{archetype.Name} #{serialFallback}"
                    : archetype.Name;
            }

            static string Norm(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

            string PickNonMatching(string[] pool, string? not1, string? not2)
            {
                if (pool.Length == 0)
                    return string.Empty;

                // Try a few times to avoid collisions while staying deterministic.
                // (If the pool is small, we may still end up with collisions.)
                for (int tries = 0; tries < 12; tries++)
                {
                    string candidate = pool[rng.Next(pool.Length)];
                    string c = Norm(candidate);
                    if (!string.IsNullOrWhiteSpace(c)
                        && c != not1
                        && c != not2)
                        return candidate.Trim();
                }

                // Fallback: accept whatever the RNG gives us.
                return (pool[rng.Next(pool.Length)] ?? string.Empty).Trim();
            }

            string doctrineKey = doctrine.ToString();
            string[] prefixPool = _targetPrefixesByDoctrine.TryGetValue(doctrineKey, out var dp)
                ? dp
                : _prefixes;

            string[] suffixPool = _targetSuffixesByArchetypeId.TryGetValue(archetype.Id, out var ap)
                ? ap
                : _suffixes;

            // Avoid nonsensical collisions if pools overlap.
            // (We don't have a designation anymore, so we only avoid prefix==suffix.)

            string prefix = PickNonMatching(prefixPool, not1: null, not2: null);
            string p = Norm(prefix);
            string suffix = PickNonMatching(suffixPool, not1: null, not2: p);
            string s = Norm(suffix);

            // If the pools are very small, we may still collide; de-dup gracefully.
            string callSign = string.IsNullOrWhiteSpace(prefix)
                ? suffix
                : (string.IsNullOrWhiteSpace(suffix) || s == p)
                    ? prefix
                    : $"{prefix} {suffix}";

            // The name itself should be indicative of the challenge:
            // prefix implies doctrine, suffix implies archetype.
            // Example: "Stealth Needle #517" or "Siege Boulder #517"
            // Always consume the serial RNG draw to preserve determinism for any later uses of this RNG.
            int serial = rng.Next(100, 999);

            if (_targetNameIncludeSerial)
            {
                return string.IsNullOrWhiteSpace(callSign)
                    ? $"{archetype.Name} #{serial}"
                    : $"{callSign} #{serial}";
            }

            return string.IsNullOrWhiteSpace(callSign)
                ? archetype.Name
                : callSign;
        }
    }
}
