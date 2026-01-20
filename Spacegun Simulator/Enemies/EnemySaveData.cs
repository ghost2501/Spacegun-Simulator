namespace Spacegun_Simulator.Enemies
{
    public sealed class EnemySaveData
    {
        public CurrentWaveSaveData CurrentWave { get; set; } = new();
        public CampaignEnemyTypeSaveData CampaignEnemyType { get; set; } = new();

        public sealed class CampaignEnemyTypeSaveData
        {
            public string Id { get; set; } = string.Empty;
            public string ArchetypeId { get; set; } = string.Empty;
            public string SecondaryArchetypeId { get; set; } = string.Empty;
            public string CustomName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;

            // Doctrine layer (optional for backward compatibility)
            public string PrimaryDoctrine { get; set; } = string.Empty;
        }

        public sealed class CurrentWaveSaveData
        {
            public int WaveNumber_Wave { get; set; }
            public double InitialDistance { get; set; }
            public double CurrentDistance { get; set; }
            public double AverageVelocity { get; set; }
            public double AverageRadarCrossSection { get; set; }
            public bool HasStealthCoating { get; set; }

            // Full-mode wave variables
            public int ThreatCount { get; set; } = 1;

            public string ArchetypeId { get; set; } = string.Empty;
            public string ArchetypeName { get; set; } = string.Empty;
            public string ArchetypeDescription { get; set; } = string.Empty;
            public double ArchetypeVelocityMultiplier { get; set; }

            // Doctrine layer
            public string Doctrine { get; set; } = string.Empty;
            public string DoctrineSource { get; set; } = string.Empty;

            // Legacy fields
            public double ArchetypeMass { get; set; }
            public double ArchetypeFractureEnergy { get; set; }
            public int ArchetypeDifficultyRating { get; set; }

            public string TargetName { get; set; } = string.Empty;
            public double TargetAltitude { get; set; }
            public double TargetVelocity { get; set; }
            public double TargetCrossSection { get; set; }
            public double TargetAcceleration { get; set; }
            public double TargetManeuverability { get; set; }
            public double TargetDefense { get; set; }
            public double TargetOffense { get; set; }
            public double TargetMass { get; set; }
            public double TargetFractureEnergy { get; set; }

            public float ApproachElevation { get; set; }
            public float ApproachAzimuth { get; set; }

            public double CachedEnemyPositionX { get; set; }
            public double CachedEnemyPositionY { get; set; }
            public double CachedEnemyPositionZ { get; set; }
            public double CachedEnemyVelocityX { get; set; }
            public double CachedEnemyVelocityY { get; set; }
            public double CachedEnemyVelocityZ { get; set; }
            public bool HasCachedVectors { get; set; }

            public float CachedCorrectLaunchDelayTime { get; set; }
            public float CachedCorrectElevation { get; set; }
            public float CachedCorrectAzimuth { get; set; }
            public float CachedCorrectVelocity { get; set; }
        }
    }
}
