using Spacegun_Simulator.Development.Projectiles;

namespace Spacegun_Simulator.Core
{
    public sealed class ProjectilesCatalogConfig
    {
        public int Version { get; set; } = 1;

        public ProjectileCore[]? Cores { get; set; }
        public PropulsionSystem[]? PropulsionSystems { get; set; }
        public ProjectileEnhancement[]? Enhancements { get; set; }
    }
}
