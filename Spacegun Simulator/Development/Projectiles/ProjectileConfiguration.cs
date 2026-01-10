namespace Spacegun_Simulator.Development.Projectiles
{
    using Spacegun_Simulator.Development;
    using Spacegun_Simulator.Core;

    // ============================================================================
    // PROJECTILE CONFIGURATION
    // ============================================================================

    public class ProjectileConfiguration
    {
        public double Mass { get; set; }
        public double Length { get; set; }
        public ProjectileType Type { get; set; }
        public bool HasGuidance { get; set; }
        public double GuidanceAccuracy { get; set; }
        public ArmorPenetrationType PenetrationType { get; set; }

        public ProjectileConfiguration()
        {
            Mass = DevelopmentTuning.ProjectileDefaults.Mass;
            Length = DevelopmentTuning.ProjectileDefaults.Length;
            Type = ProjectileType.KineticPenetrator;
            HasGuidance = DevelopmentTuning.ProjectileDefaults.HasGuidance;
            GuidanceAccuracy = DevelopmentTuning.ProjectileDefaults.GuidanceAccuracy;
            PenetrationType = ArmorPenetrationType.KineticEnergy;
        }
    }
}
