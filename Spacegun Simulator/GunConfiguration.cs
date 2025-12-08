namespace Spacegun_Simulator
{
    // ============================================================================
    // GUN CONFIGURATION
    // ============================================================================

    public class GunConfiguration
    {
        public double BarrelLength { get; set; }
        public double BoreDiameter { get; set; }
        public string BarrelMaterial { get; set; }
        public double BarrelIntegrity { get; set; }

        public PropulsionType PropulsionSystem { get; set; }
        public double PropellantMass { get; set; }
        public double PropellantEnergyDensity { get; set; }

        public double PowerCapacity { get; set; }
        public double CapacitorEfficiency { get; set; }
        public CoolingSystem CoolingSystem { get; set; }
        public double CoolingCapacity { get; set; }
        public double StructuralReinforcement { get; set; }

        public int AmmunitionCount { get; set; }
        public ProjectileConfiguration DefaultProjectile { get; set; }

        public List<string> InstalledUpgrades { get; set; } = new();

        public double MaxSafePressure => CalculateMaxPressure();
        public double HeatPerShot => CalculateHeatGeneration();
        public double ReloadTime => CalculateReloadTime();

        public GunConfiguration()
        {
            BarrelLength = 100.0;
            BoreDiameter = 0.5;
            BarrelMaterial = "Steel";
            BarrelIntegrity = 1.0;
            PropulsionSystem = PropulsionType.Chemical;
            PropellantMass = 50.0;
            PropellantEnergyDensity = 5.0;
            PowerCapacity = 100.0;
            CapacitorEfficiency = 0.7;
            CoolingSystem = CoolingSystem.Passive;
            CoolingCapacity = 10.0;
            StructuralReinforcement = 1.0;
            AmmunitionCount = 10;
            DefaultProjectile = new ProjectileConfiguration();
        }

        private double CalculateMaxPressure()
        {
            double basePressure = BarrelMaterial switch
            {
                "Steel" => 500.0,
                "Titanium" => 700.0,
                "Composite" => 900.0,
                "Exotic" => 1200.0,
                _ => 500.0
            };
            return basePressure * StructuralReinforcement * BarrelIntegrity;
        }

        private double CalculateHeatGeneration()
        {
            double baseHeat = PropulsionSystem switch
            {
                PropulsionType.Chemical => PropellantMass * 0.3,
                PropulsionType.Railgun => PowerCapacity * 0.5,
                PropulsionType.Coilgun => PowerCapacity * 0.4,
                PropulsionType.Hybrid => PropellantMass * 0.2 + PowerCapacity * 0.3,
                _ => 0.0
            };
            return baseHeat;
        }

        private double CalculateReloadTime()
        {
            double baseTime = 30.0;
            double coolingModifier = CoolingSystem switch
            {
                CoolingSystem.Passive => 1.0,
                CoolingSystem.ActiveAir => 0.8,
                CoolingSystem.Liquid => 0.6,
                CoolingSystem.Cryogenic => 0.4,
                _ => 1.0
            };

            double heatRatio = HeatPerShot / CoolingCapacity;
            if (heatRatio > 1.0)
            {
                baseTime *= heatRatio;
            }

            return baseTime * coolingModifier;
        }
    }
}
