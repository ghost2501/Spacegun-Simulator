using Spacegun_Simulator.Economy;
using Spacegun_Simulator.Development.Shared;

namespace Spacegun_Simulator.Development.Weapons
{
    // ====================================================================
    // UPGRADE SYSTEM
    // ====================================================================
    public class UpgradeSystem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ResourceCost Cost { get; set; } = ResourceCost.None;
        public List<string> Prerequisites { get; } = new();
        public Dictionary<string, double> StatModifiers { get; } = new();
        // ... rest of the code unchanged ...

        public UpgradeSystem()
        {
            // Cost already initialized above; constructor kept for future expansion.
        }

        public bool CanApply(GunConfiguration gun, ResourcePool resources)
        {
            if (gun is null) throw new ArgumentNullException(nameof(gun));
            if (resources is null) throw new ArgumentNullException(nameof(resources));

            // Already applied?
            if (gun.InstalledUpgrades.Contains(Id)) return false;

            foreach (var prereq in Prerequisites)
            {
                if (!gun.InstalledUpgrades.Contains(prereq))
                    return false;
            }

            // Guard against null Cost
            return resources.CanAfford(Cost ?? ResourceCost.None);
        }

        public void Apply(GunConfiguration gun, ResourcePool resources)
        {
            if (!CanApply(gun, resources))
                throw new InvalidOperationException("Cannot apply upgrade: prerequisites or resources missing.");

            // Prevent double-apply if something spoiled checks
            if (gun.InstalledUpgrades.Contains(Id))
                throw new InvalidOperationException("Upgrade already applied.");

            // Guard against null Cost
            resources.Spend(Cost ?? ResourceCost.None);
            gun.InstalledUpgrades.Add(Id);

            // Apply stat modifications
            foreach (var kv in StatModifiers)
            {
                var key = kv.Key;
                var value = kv.Value;

                switch (key)
                {
                    case nameof(GunConfiguration.BarrelLength):
                        gun.BarrelLength += value;
                        break;
                    case nameof(GunConfiguration.BarrelIntegrity):
                        gun.BarrelIntegrity = Math.Min(1.0, gun.BarrelIntegrity + value);
                        break;
                    case nameof(GunConfiguration.PowerCapacity):
                        gun.PowerCapacity += value;
                        break;
                    case nameof(GunConfiguration.CapacitorEfficiency):
                        gun.CapacitorEfficiency = Math.Clamp(gun.CapacitorEfficiency + value, 0, 1);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}