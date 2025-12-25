namespace Spacegun_Simulator
{
    // ============================================================================
    // PROJECTILE CONFIGURATION
    // ============================================================================

    public class ProjectileConfiguration
    {
        public double Mass { get; set; }
        public double Length { get; set; }
        public ProjectileType Type { get; set; }
        public double DragCoefficient { get; set; }
        public bool HasGuidance { get; set; }
        public double GuidanceAccuracy { get; set; }
        public ArmorPenetrationType PenetrationType { get; set; }

        public ProjectileConfiguration()
        {
            Mass = 10.0;
            Length = 0.5;
            Type = ProjectileType.KineticPenetrator;
            DragCoefficient = 0.3;
            HasGuidance = false;
            GuidanceAccuracy = 0.0;
            PenetrationType = ArmorPenetrationType.KineticEnergy;
        }
    }
}
