namespace Spacegun_Simulator.Development.Shared
{
    /// <summary>
    /// Shared development cost container used across weapon, projectile, and technology systems.
    /// Kept in Development/Shared for discoverability.
    /// </summary>
    public sealed class ResourceCost
    {
        public double Budget { get; init; }
        public double Steel { get; init; }
        public double ExoticMaterials { get; init; }
        public double PowerCapacity { get; init; }
        public double ResearchPoints { get; init; }

        public ResourceCost(double budget = 0, double steel = 0, double exotic = 0, double power = 0, double research = 0)
        {
            Budget = budget;
            Steel = steel;
            ExoticMaterials = exotic;
            PowerCapacity = power;
            ResearchPoints = research;
        }

        public static readonly ResourceCost None = new();
    }
}
