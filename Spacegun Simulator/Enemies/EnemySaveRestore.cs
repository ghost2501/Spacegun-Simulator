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
            string TargetName,
            double TargetAltitude,
            double TargetVelocity,
            double TargetCrossSection,
            double TargetEvasiveness,
            double TargetMass,
            double TargetFractureEnergy,
            double InitialDistance,
            double CurrentDistance,
            double AverageVelocity,
            double AverageRadarCrossSection,
            double AverageEvasiveness,
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
            float CachedCorrectVelocity
        );

        public sealed record CampaignEnemyTypeSnapshot(
            string Id,
            string ArchetypeId,
            string CustomName,
            string Description
        );

        public static EnemyWave CreateWaveForRestore(EnemyWaveRestoreSnapshot snapshot, EnemyArchetype? campaignEnemyArchetype)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var archetype = campaignEnemyArchetype;

            if (archetype == null)
            {
                archetype = EnemyArchetype.All.FirstOrDefault(a => a.Id == snapshot.ArchetypeId);
            }

            if (archetype == null)
            {
                archetype = new EnemyArchetype(
                    snapshot.ArchetypeId,
                    snapshot.ArchetypeName,
                    snapshot.ArchetypeDescription,
                    snapshot.ArchetypeVelocityMultiplier,
                    (0, 50_000),
                    (0, 100_000),
                    1
                );
            }

            var target = new EnemyTarget
            {
                Name = snapshot.TargetName,
                Altitude = snapshot.TargetAltitude,
                Velocity = snapshot.TargetVelocity,
                CrossSection = snapshot.TargetCrossSection,
                Evasiveness = snapshot.TargetEvasiveness,
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
                AverageEvasiveness = snapshot.AverageEvasiveness,
                HasStealthCoating = snapshot.HasStealthCoating,
                Archetype = archetype,
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

            return restoredWave;
        }

        public static EnemyType? TryCreateCampaignEnemyType(CampaignEnemyTypeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshot.Id)) return null;

            var archetype = EnemyArchetype.All.FirstOrDefault(a => a.Id == snapshot.ArchetypeId);
            if (archetype == null) return null;

            return new EnemyType(
                snapshot.Id,
                archetype,
                snapshot.CustomName,
                snapshot.Description
            );
        }
    }
}
