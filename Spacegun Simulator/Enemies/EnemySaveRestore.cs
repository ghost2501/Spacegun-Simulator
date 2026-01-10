namespace Spacegun_Simulator.Enemies
{
    public static class EnemySaveRestore
    {
        public sealed record EnemyWaveRestoreSnapshot(
            int WaveNumber,
            string ArchetypeId,
            string ArchetypeName,
            string ArchetypeDescription,
            double ArchetypeVelocityMultiplier,
            string Doctrine,
            string DoctrineSource,
            string TargetName,
            double TargetAltitude,
            double TargetVelocity,
            double TargetCrossSection,
            double TargetMass,
            double TargetFractureEnergy,
            double InitialDistance,
            double CurrentDistance,
            double AverageVelocity,
            double AverageRadarCrossSection,
            bool HasStealthCoating,
            float ApproachElevation,
            float ApproachAzimuth,
            bool HasCachedVectors,
            double CachedEnemyPositionX,
            double CachedEnemyPositionY,
            double CachedEnemyPositionZ,
            double CachedEnemyVelocityX,
            double CachedEnemyVelocityY,
            double CachedEnemyVelocityZ,
            float CachedCorrectLaunchDelayTime,
            float CachedCorrectElevation,
            float CachedCorrectAzimuth,
            float CachedCorrectVelocity,
            // Full-mode extensions
            int ThreatCount = 1,
            double TargetAcceleration = 0.0,
            double TargetManeuverability = 0.0,
            double TargetDefense = 0.0,
            double TargetOffense = 0.0
        );

        public sealed record CampaignEnemyTypeSnapshot(
            string Id,
            string ArchetypeId,
            string CustomName,
            string Description,
            string PrimaryDoctrine
        );

        public static EnemyWave CreateWaveForRestore(EnemyWaveRestoreSnapshot snapshot, EnemyArchetype? campaignEnemyArchetype)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var archetype = campaignEnemyArchetype;

            if (archetype == null)
            {
                archetype = EnemyArchetypeCatalog.TryGetById(snapshot.ArchetypeId);
            }

            if (archetype == null)
            {
                archetype = new EnemyArchetype
                {
                    Id = snapshot.ArchetypeId,
                    Name = snapshot.ArchetypeName,
                    Description = snapshot.ArchetypeDescription,
                    VelocityMultiplier = snapshot.ArchetypeVelocityMultiplier,
                    MassRange = new Spacegun_Simulator.Core.DevelopmentTuning.Range(0.0, 50_000.0),
                    FractureEnergyRange = new Spacegun_Simulator.Core.DevelopmentTuning.Range(0.0, 100_000.0),
                    BaseDifficultyRating = 1,
                    IsTutorialOnly = false,
                };
            }

            var target = new EnemyTarget
            {
                Name = snapshot.TargetName,
                Altitude = snapshot.TargetAltitude,
                Velocity = snapshot.TargetVelocity,
                CrossSection = snapshot.TargetCrossSection,
                Acceleration = snapshot.TargetAcceleration,
                Maneuverability = snapshot.TargetManeuverability,
                Defense = snapshot.TargetDefense,
                Offense = snapshot.TargetOffense,
                Mass = snapshot.TargetMass,
                FractureEnergy = snapshot.TargetFractureEnergy
            };

            var restoredWave = new EnemyWave(snapshot.WaveNumber)
            {
                WaveNumber = snapshot.WaveNumber,
                InitialDistance = snapshot.InitialDistance,
                CurrentDistance = snapshot.CurrentDistance,
                AverageVelocity = snapshot.AverageVelocity,
                AverageRadarCrossSection = snapshot.AverageRadarCrossSection,
                HasStealthCoating = snapshot.HasStealthCoating,
                ThreatCount = snapshot.ThreatCount,
                Archetype = archetype,
                Doctrine = (!string.IsNullOrWhiteSpace(snapshot.Doctrine)
                    && Enum.TryParse<EnemyDoctrine>(snapshot.Doctrine, ignoreCase: true, out var d)) ? d : EnemyDoctrine.None,
                DoctrineSource = (!string.IsNullOrWhiteSpace(snapshot.DoctrineSource)
                    && Enum.TryParse<EnemyDoctrineSource>(snapshot.DoctrineSource, ignoreCase: true, out var s)) ? s : EnemyDoctrineSource.None,
                ApproachElevation = snapshot.ApproachElevation,
                ApproachAzimuth = snapshot.ApproachAzimuth,
                CachedEnemyPosition = snapshot.HasCachedVectors
                    ? new global::Spacegun_Simulator.Ballistics.Vector3(snapshot.CachedEnemyPositionX, snapshot.CachedEnemyPositionY, snapshot.CachedEnemyPositionZ)
                    : null,
                CachedEnemyVelocity = snapshot.HasCachedVectors
                    ? new global::Spacegun_Simulator.Ballistics.Vector3(snapshot.CachedEnemyVelocityX, snapshot.CachedEnemyVelocityY, snapshot.CachedEnemyVelocityZ)
                    : null,
                CachedCorrectLaunchDelayTime = snapshot.CachedCorrectLaunchDelayTime,
                CachedCorrectElevation = snapshot.CachedCorrectElevation,
                CachedCorrectAzimuth = snapshot.CachedCorrectAzimuth,
                CachedCorrectVelocity = snapshot.CachedCorrectVelocity,
                IsRestoredFromSave = snapshot.HasCachedVectors,
                Targets = new List<EnemyTarget> { target }
            };

            if (!Enum.IsDefined(typeof(EnemyDoctrine), restoredWave.Doctrine))
                restoredWave.Doctrine = EnemyDoctrine.None;

            if (!Enum.IsDefined(typeof(EnemyDoctrineSource), restoredWave.DoctrineSource))
                restoredWave.DoctrineSource = EnemyDoctrineSource.None;

            // Enforce a single source of truth for enemy speed.
            // Detection uses restoredWave.AverageVelocity; engagement uses CachedEnemyVelocity magnitude.
            if (snapshot.HasCachedVectors && restoredWave.CachedEnemyVelocity.HasValue)
            {
                double speed = restoredWave.CachedEnemyVelocity.Value.Magnitude;
                restoredWave.AverageVelocity = speed;
                target.Velocity = speed;
            }

            return restoredWave;
        }

        public static EnemyType? TryCreateCampaignEnemyType(CampaignEnemyTypeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshot.Id)) return null;

            var archetype = EnemyArchetypeCatalog.TryGetById(snapshot.ArchetypeId);
            if (archetype == null) return null;

            var type = new EnemyType(
                snapshot.Id,
                archetype,
                snapshot.CustomName,
                snapshot.Description
            );

            if (!string.IsNullOrWhiteSpace(snapshot.PrimaryDoctrine)
                && Enum.TryParse<EnemyDoctrine>(snapshot.PrimaryDoctrine, ignoreCase: true, out var doctrine))
                type.PrimaryDoctrine = doctrine;

            // Clamp invalid/unknown values for safety.
            if (!Enum.IsDefined(typeof(EnemyDoctrine), type.PrimaryDoctrine))
                type.PrimaryDoctrine = EnemyDoctrine.None;

            return type;
        }
    }
}
