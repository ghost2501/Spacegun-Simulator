using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Enemies
{
    public static class EnemyArchetypeCatalog
    {
        private static readonly List<EnemyArchetype> _all = new();

        public static IReadOnlyList<EnemyArchetype> All => _all;

        public static IReadOnlyList<EnemyArchetype> CampaignArchetypes
            => _all.Where(a => a is not null && !a.IsTutorialOnly).ToArray();

        public static EnemyArchetype? TryGetById(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return _all.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static EnemyArchetype GetById(string id)
        {
            var a = TryGetById(id);
            if (a is not null)
                return a;

            // Safe fallback to keep the game runnable if config is missing or malformed.
            return new EnemyArchetype
            {
                Id = id,
                Name = id,
                Description = "(Missing archetype config)",
                VelocityMultiplier = 1.0,
                MassRange = new DevelopmentTuning.Range(0.0, 0.0),
                FractureEnergyRange = new DevelopmentTuning.Range(0.0, 0.0),
                BaseDifficultyRating = 1,
                IsTutorialOnly = false,
            };
        }

        public static void ReplaceAll(IEnumerable<EnemyArchetype> archetypes)
        {
            _all.Clear();

            if (archetypes is null)
                return;

            foreach (var a in archetypes)
            {
                if (a is null)
                    continue;

                if (string.IsNullOrWhiteSpace(a.Id))
                    continue;

                // De-dup by id (first wins).
                if (_all.Any(x => string.Equals(x.Id, a.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _all.Add(a);
            }
        }

        public static EnemyArchetype SelectRandom(Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            var pool = CampaignArchetypes;
            if (pool.Count == 0)
                pool = All;

            if (pool.Count == 0)
                return GetById("unknown");

            return pool[rng.Next(pool.Count)];
        }
    }
}
