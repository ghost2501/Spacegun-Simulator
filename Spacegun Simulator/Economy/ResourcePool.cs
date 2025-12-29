namespace Spacegun_Simulator.Economy
{
    // Central resource container for the player / base.
    // Units:
    //  - Budget: currency units
    //  - Steel: metric tons
    //  - ExoticMaterials: abstract units
    //  - PowerCapacity: MW
    //  - ResearchPoints: abstract points
    public class ResourcePool
    {
        public double Budget { get; set; }
        public double Steel { get; set; }
        public double ExoticMaterials { get; set; }
        public double PowerCapacity { get; set; }
        public double ResearchPoints { get; set; }

        public ResourcePool()
        {
            Budget = 1000;
            Steel = 500;
            ExoticMaterials = 50;
            PowerCapacity = 100;
            ResearchPoints = 10;
        }

        public bool CanAfford(global::Spacegun_Simulator.Development.Shared.ResourceCost cost)
        {
            if (cost is null) return true;
            if (cost.Budget > Budget) return false;
            if (cost.Steel > Steel) return false;
            if (cost.ExoticMaterials > ExoticMaterials) return false;
            if (cost.PowerCapacity > PowerCapacity) return false;
            if (cost.ResearchPoints > ResearchPoints) return false;
            return true;
        }

        public void Spend(global::Spacegun_Simulator.Development.Shared.ResourceCost cost)
        {
            if (cost is null) return;
            if (!CanAfford(cost)) throw new InvalidOperationException("Insufficient resources to spend cost.");

            Budget = Math.Max(0, Budget - cost.Budget);
            Steel = Math.Max(0, Steel - cost.Steel);
            ExoticMaterials = Math.Max(0, ExoticMaterials - cost.ExoticMaterials);
            PowerCapacity = Math.Max(0, PowerCapacity - cost.PowerCapacity);
            ResearchPoints = Math.Max(0, ResearchPoints - cost.ResearchPoints);
        }

        // Optional helper to grant resources
        public void Grant(global::Spacegun_Simulator.Development.Shared.ResourceCost grant)
        {
            if (grant is null) return;
            Budget += grant.Budget;
            Steel += grant.Steel;
            ExoticMaterials += grant.ExoticMaterials;
            PowerCapacity += grant.PowerCapacity;
            ResearchPoints += grant.ResearchPoints;
        }
    }
}
