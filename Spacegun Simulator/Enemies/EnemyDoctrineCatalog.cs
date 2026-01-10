namespace Spacegun_Simulator.Enemies
{
    public static class EnemyDoctrineCatalog
    {
        private static readonly Dictionary<EnemyDoctrine, EnemyDoctrineProfile> _profiles = new();

        private static readonly Dictionary<string, EnemyDoctrine[]> _primaryPoolsByArchetypeId = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, EnemyDoctrine[]> _guestPoolsByArchetypeId = new(StringComparer.OrdinalIgnoreCase);

        public static void ReplaceAll(
            IEnumerable<EnemyDoctrineProfile> profiles,
            Dictionary<string, EnemyDoctrine[]> primaryPoolsByArchetypeId,
            Dictionary<string, EnemyDoctrine[]> guestPoolsByArchetypeId)
        {
            _profiles.Clear();
            foreach (var p in profiles)
            {
                if (p is null) continue;
                _profiles[p.Doctrine] = p;
            }

            _primaryPoolsByArchetypeId.Clear();
            foreach (var kvp in primaryPoolsByArchetypeId)
                _primaryPoolsByArchetypeId[kvp.Key] = kvp.Value;

            _guestPoolsByArchetypeId.Clear();
            foreach (var kvp in guestPoolsByArchetypeId)
                _guestPoolsByArchetypeId[kvp.Key] = kvp.Value;
        }

        public static EnemyDoctrineProfile Get(EnemyDoctrine doctrine)
        {
            if (_profiles.TryGetValue(doctrine, out var profile))
                return profile;

            if (_profiles.TryGetValue(EnemyDoctrine.None, out var none))
                return none;

            return new EnemyDoctrineProfile(
                Doctrine: EnemyDoctrine.None,
                Name: "None",
                Description: "No special doctrine applied.",
                VelocityMultiplier: 1.0,
                AccelerationMultiplier: 1.0,
                ManeuverabilityMultiplier: 1.0,
                DefenseMultiplier: 1.0,
                OffenseMultiplier: 1.0,
                StealthChanceMultiplier: 1.0,
                RadarCrossSectionMultiplier: 1.0
            );
        }

        public static EnemyDoctrine SelectPrimaryDoctrine(EnemyArchetype archetype, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) return EnemyDoctrine.None;

            var pool = GetPool(_primaryPoolsByArchetypeId, archetype.Id);
            return pool.Length == 0 ? EnemyDoctrine.None : pool[rng.Next(pool.Length)];
        }

        public static EnemyDoctrine SelectGuestDoctrine(EnemyDoctrine campaignDoctrine, EnemyArchetype archetype, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) return EnemyDoctrine.None;

            var pool = GetPool(_guestPoolsByArchetypeId, archetype.Id);

            // Prefer a doctrine different from the campaign doctrine.
            var options = pool.Where(d => d != EnemyDoctrine.None && d != campaignDoctrine).Distinct().ToArray();
            if (options.Length == 0)
                options = pool.Where(d => d != EnemyDoctrine.None).Distinct().ToArray();

            return options.Length == 0 ? EnemyDoctrine.None : options[rng.Next(options.Length)];
        }

        private static EnemyDoctrine[] GetPool(Dictionary<string, EnemyDoctrine[]> pools, string archetypeId)
        {
            if (pools.TryGetValue(archetypeId, out var pool) && pool is not null)
                return pool;

            if (pools.TryGetValue("*", out var fallback) && fallback is not null)
                return fallback;

            return Array.Empty<EnemyDoctrine>();
        }
    }
}
