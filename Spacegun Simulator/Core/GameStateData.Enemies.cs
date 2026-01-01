using System.Text.Json.Serialization;
using Spacegun_Simulator.Enemies;

namespace Spacegun_Simulator.Core
{
    public partial class GameStateData
    {
        private EnemySaveData _enemy = new();

        [JsonIgnore]
        public EnemySaveData Enemy
        {
            get => _enemy;
            set => _enemy = value ?? new EnemySaveData();
        }

        // ===== CURRENT WAVE STATE =====
        public int CurrentWaveNumber_Wave
        {
            get => Enemy.CurrentWave.WaveNumber_Wave;
            set => Enemy.CurrentWave.WaveNumber_Wave = value;
        }

        public double CurrentWaveInitialDistance
        {
            get => Enemy.CurrentWave.InitialDistance;
            set => Enemy.CurrentWave.InitialDistance = value;
        }

        public double CurrentWaveCurrentDistance
        {
            get => Enemy.CurrentWave.CurrentDistance;
            set => Enemy.CurrentWave.CurrentDistance = value;
        }

        public double CurrentWaveAverageVelocity
        {
            get => Enemy.CurrentWave.AverageVelocity;
            set => Enemy.CurrentWave.AverageVelocity = value;
        }

        public double CurrentWaveAverageRadarCrossSection
        {
            get => Enemy.CurrentWave.AverageRadarCrossSection;
            set => Enemy.CurrentWave.AverageRadarCrossSection = value;
        }

        public bool CurrentWaveHasStealthCoating
        {
            get => Enemy.CurrentWave.HasStealthCoating;
            set => Enemy.CurrentWave.HasStealthCoating = value;
        }

        public int CurrentWaveShipCount
        {
            get => Enemy.CurrentWave.ShipCount;
            set => Enemy.CurrentWave.ShipCount = value;
        }

        public string CurrentWaveArchetypeId
        {
            get => Enemy.CurrentWave.ArchetypeId;
            set => Enemy.CurrentWave.ArchetypeId = value ?? string.Empty;
        }

        public string CurrentWaveArchetypeName
        {
            get => Enemy.CurrentWave.ArchetypeName;
            set => Enemy.CurrentWave.ArchetypeName = value ?? string.Empty;
        }

        public string CurrentWaveArchetypeDescription
        {
            get => Enemy.CurrentWave.ArchetypeDescription;
            set => Enemy.CurrentWave.ArchetypeDescription = value ?? string.Empty;
        }

        public double CurrentWaveArchetypeVelocityMultiplier
        {
            get => Enemy.CurrentWave.ArchetypeVelocityMultiplier;
            set => Enemy.CurrentWave.ArchetypeVelocityMultiplier = value;
        }

        public double CurrentWaveArchetypeMass
        {
            get => Enemy.CurrentWave.ArchetypeMass;
            set => Enemy.CurrentWave.ArchetypeMass = value;
        }

        public double CurrentWaveArchetypeFractureEnergy
        {
            get => Enemy.CurrentWave.ArchetypeFractureEnergy;
            set => Enemy.CurrentWave.ArchetypeFractureEnergy = value;
        }

        public int CurrentWaveArchetypeDifficultyRating
        {
            get => Enemy.CurrentWave.ArchetypeDifficultyRating;
            set => Enemy.CurrentWave.ArchetypeDifficultyRating = value;
        }

        public string CurrentWaveTargetName
        {
            get => Enemy.CurrentWave.TargetName;
            set => Enemy.CurrentWave.TargetName = value ?? string.Empty;
        }

        public double CurrentWaveTargetAltitude
        {
            get => Enemy.CurrentWave.TargetAltitude;
            set => Enemy.CurrentWave.TargetAltitude = value;
        }

        public double CurrentWaveTargetVelocity
        {
            get => Enemy.CurrentWave.TargetVelocity;
            set => Enemy.CurrentWave.TargetVelocity = value;
        }

        public double CurrentWaveTargetCrossSection
        {
            get => Enemy.CurrentWave.TargetCrossSection;
            set => Enemy.CurrentWave.TargetCrossSection = value;
        }

        public double CurrentWaveTargetAcceleration
        {
            get => Enemy.CurrentWave.TargetAcceleration;
            set => Enemy.CurrentWave.TargetAcceleration = value;
        }

        public double CurrentWaveTargetManeuverability
        {
            get => Enemy.CurrentWave.TargetManeuverability;
            set => Enemy.CurrentWave.TargetManeuverability = value;
        }

        public double CurrentWaveTargetDefense
        {
            get => Enemy.CurrentWave.TargetDefense;
            set => Enemy.CurrentWave.TargetDefense = value;
        }

        public double CurrentWaveTargetOffense
        {
            get => Enemy.CurrentWave.TargetOffense;
            set => Enemy.CurrentWave.TargetOffense = value;
        }

        public double CurrentWaveTargetMass
        {
            get => Enemy.CurrentWave.TargetMass;
            set => Enemy.CurrentWave.TargetMass = value;
        }

        public double CurrentWaveTargetFractureEnergy
        {
            get => Enemy.CurrentWave.TargetFractureEnergy;
            set => Enemy.CurrentWave.TargetFractureEnergy = value;
        }

        // ===== CAMPAIGN ENEMY TYPE =====
        public string CampaignEnemyTypeId
        {
            get => Enemy.CampaignEnemyType.Id;
            set => Enemy.CampaignEnemyType.Id = value ?? string.Empty;
        }

        public string CampaignEnemyTypeArchetypeId
        {
            get => Enemy.CampaignEnemyType.ArchetypeId;
            set => Enemy.CampaignEnemyType.ArchetypeId = value ?? string.Empty;
        }

        public string CampaignEnemyTypeCustomName
        {
            get => Enemy.CampaignEnemyType.CustomName;
            set => Enemy.CampaignEnemyType.CustomName = value ?? string.Empty;
        }

        public string CampaignEnemyTypeDescription
        {
            get => Enemy.CampaignEnemyType.Description;
            set => Enemy.CampaignEnemyType.Description = value ?? string.Empty;
        }

        public float EnemyApproachElevation
        {
            get => Enemy.CurrentWave.ApproachElevation;
            set => Enemy.CurrentWave.ApproachElevation = value;
        }

        public float EnemyApproachAzimuth
        {
            get => Enemy.CurrentWave.ApproachAzimuth;
            set => Enemy.CurrentWave.ApproachAzimuth = value;
        }

        // ===== CACHED CARTESIAN VECTORS (From Wave) =====
        public double CachedEnemyPositionX
        {
            get => Enemy.CurrentWave.CachedEnemyPositionX;
            set => Enemy.CurrentWave.CachedEnemyPositionX = value;
        }

        public double CachedEnemyPositionY
        {
            get => Enemy.CurrentWave.CachedEnemyPositionY;
            set => Enemy.CurrentWave.CachedEnemyPositionY = value;
        }

        public double CachedEnemyPositionZ
        {
            get => Enemy.CurrentWave.CachedEnemyPositionZ;
            set => Enemy.CurrentWave.CachedEnemyPositionZ = value;
        }

        public double CachedEnemyVelocityX
        {
            get => Enemy.CurrentWave.CachedEnemyVelocityX;
            set => Enemy.CurrentWave.CachedEnemyVelocityX = value;
        }

        public double CachedEnemyVelocityY
        {
            get => Enemy.CurrentWave.CachedEnemyVelocityY;
            set => Enemy.CurrentWave.CachedEnemyVelocityY = value;
        }

        public double CachedEnemyVelocityZ
        {
            get => Enemy.CurrentWave.CachedEnemyVelocityZ;
            set => Enemy.CurrentWave.CachedEnemyVelocityZ = value;
        }

        public bool HasCachedVectors
        {
            get => Enemy.CurrentWave.HasCachedVectors;
            set => Enemy.CurrentWave.HasCachedVectors = value;
        }

        // ===== CACHED CORRECT FIRING SOLUTION (From Wave) =====
        public float CachedCorrectLaunchDelayTime
        {
            get => Enemy.CurrentWave.CachedCorrectLaunchDelayTime;
            set => Enemy.CurrentWave.CachedCorrectLaunchDelayTime = value;
        }

        public float CachedCorrectElevation
        {
            get => Enemy.CurrentWave.CachedCorrectElevation;
            set => Enemy.CurrentWave.CachedCorrectElevation = value;
        }

        public float CachedCorrectAzimuth
        {
            get => Enemy.CurrentWave.CachedCorrectAzimuth;
            set => Enemy.CurrentWave.CachedCorrectAzimuth = value;
        }

        public float CachedCorrectVelocity
        {
            get => Enemy.CurrentWave.CachedCorrectVelocity;
            set => Enemy.CurrentWave.CachedCorrectVelocity = value;
        }
    }
}
