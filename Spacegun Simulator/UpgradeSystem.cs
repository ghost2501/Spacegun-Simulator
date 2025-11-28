
namespace Spacegun_Simulator
{
    // ============================================================================
    // UPGRADE SYSTEM
    // ============================================================================

    public class UpgradeSystem
    {
        public string Id { get; set; } = string.Empty;  // Add default value
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ResourceCost Cost { get; set; } 
        public List<string> Prerequisites { get; set; } = new List<string>();

        // Stat modifications
        public Dictionary<string, double> StatModifiers { get; set; } = new Dictionary<string, double>();

        public bool CanApply(GunConfiguration gun, ResourcePool resources)
        {
            // Check prerequisites
            foreach (var prereq in Prerequisites)
            {
                if (!gun.InstalledUpgrades.Contains(prereq))
                    return false;
            }

            // Check resources
            return resources.CanAfford(Cost);
        }

        public void Apply(GunConfiguration gun, ResourcePool resources)
        {
            resources.Spend(Cost); 
            gun.InstalledUpgrades.Add(Id);

            // Apply stat modifications
            foreach (var modifier in StatModifiers)
            {
                // This would use reflection or a more sophisticated system in practice
                // For now, simplified
            }
        }
    }

    public class ResourcePool
    {
        internal bool CanAfford(ResourceCost cost)
        {
            throw new NotImplementedException();
        }

        internal void Spend(ResourceCost cost)
        {
            throw new NotImplementedException();
        }
    }

    public class ResourceCost
    {
    }
}
