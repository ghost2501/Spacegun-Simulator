namespace Spacegun_Simulator.Development.Weapons
{
    using Spacegun_Simulator.Core;
    using Spacegun_Simulator.Development;
    using Spacegun_Simulator.Development.Projectiles;

    // ====================================================================
    // GUN CONFIGURATION
    // ====================================================================
    public class GunConfiguration
    {
        public double BarrelLength { get; set; }
        public double BoreDiameter { get; set; }
        public string BarrelMaterial { get; set; }
        public double BarrelIntegrity { get; set; } // 0.0 (destroyed) .. 1.0 (new)

        /// <summary>
        /// Fire-control quality multiplier (>= 0). Higher values help counter enemy maneuverability.
        /// </summary>
        public double FireControlQuality { get; set; }

        /// <summary>
        /// Player-facing name for <see cref="FireControlQuality"/>.
        /// Kept as an alias so existing saves/configs that reference FireControlQuality remain compatible.
        /// </summary>
        public double Guidance
        {
            get => FireControlQuality;
            set => FireControlQuality = value;
        }

        /// <summary>
        /// Range multiplier derived from barrel length.
        /// Baseline is 100.0 (default BarrelLength) => 1.0x.
        /// </summary>
        public double RangeMultiplierFromBarrelLength => Math.Clamp(BarrelLength / 100.0, 0.5, 2.0);

        public PropulsionType PropulsionSystem { get; set; }
        public double PropellantMass { get; set; }
        public double PropellantEnergyDensity { get; set; }

        public double PowerCapacity { get; set; }
        public double CapacitorEfficiency { get; set; }
        public CoolingSystem CoolingSystem { get; set; }
        public double CoolingCapacity { get; set; }

        public int AmmunitionCount { get; set; }
        public ProjectileConfiguration DefaultProjectile { get; set; }

        public List<string> InstalledUpgrades { get; set; } = new();

        // ===== NEW: Base Muzzle Velocity System =====
        /// <summary>
        /// The gun's base muzzle velocity in m/s.
        /// This is the velocity imparted to projectiles at launch.
        /// Scales with Weapons Tech level.
        /// </summary>
        public double BaseMuzzleVelocityMs { get; set; }

        // ====================================================================
        // Barrel degradation state (new)
        // ====================================================================
        public long ShotsFired { get; private set; }
        public double CumulativeWear { get; private set; }

        // Initialize from canonical source in GameConstants (single source of truth)
        public double BaseWearPerShot { get; set; }
        public double IntegrityFailureThreshold { get; set; } = 0.05;

        public double MaxSafePressure => CalculateMaxPressure();
        public double HeatPerShot => CalculateHeatGeneration();
        public double ReloadTime => CalculateReloadTime();

        public GunConfiguration()
        {
            BarrelLength = 100.0;
            BoreDiameter = 0.5;
            BarrelMaterial = "Steel";
            BarrelIntegrity = 1.0;
            FireControlQuality = 1.0;
            PropulsionSystem = PropulsionType.Chemical;
            PropellantMass = 50.0;
            PropellantEnergyDensity = 5.0;
            PowerCapacity = 100.0;
            CapacitorEfficiency = 0.7;
            CoolingSystem = CoolingSystem.Passive;
            CoolingCapacity = 10.0;
            AmmunitionCount = 10;
            DefaultProjectile = new ProjectileConfiguration();

            // Default base velocity for Weapons Tech Level 1 (canonical source)
            BaseMuzzleVelocityMs = GameConstants.WeaponsTechBaseVelocity.Length > 0
                ? GameConstants.WeaponsTechBaseVelocity[0]
                : 80_000;

            // Initialize wear tunable from canonical source
            BaseWearPerShot = GameConstants.DefaultBarrelWearPerShot;

            ShotsFired = 0;
            CumulativeWear = 0.0;
        }

        /// <summary>
        /// Get the base muzzle velocity for a given Weapons Tech level.
        /// Values are read from GameConstants.WeaponsTechBaseVelocity to centralize tuning.
        /// </summary>
        public static double GetBaseMuzzleVelocityForTechLevel(int weaponsTechLevel)
        {
            if (weaponsTechLevel <= 0) weaponsTechLevel = 1;

            int index = weaponsTechLevel - 1;
            if (index >= 0 && index < GameConstants.WeaponsTechBaseVelocity.Length)
                return GameConstants.WeaponsTechBaseVelocity[index];

            // Fallback to first entry if out of range
            return GameConstants.WeaponsTechBaseVelocity.Length > 0
                ? GameConstants.WeaponsTechBaseVelocity[0]
                : 80_000;
        }

        public static PropulsionType GetPropulsionSystemForWeaponsTechLevel(int weaponsTechLevel)
        {
            if (weaponsTechLevel <= 1) return PropulsionType.Chemical;
            if (weaponsTechLevel == 2) return PropulsionType.Railgun;
            return PropulsionType.Hybrid;
        }

        public void UpdateBaseMuzzleVelocity(int weaponsTechLevel)
        {
            BaseMuzzleVelocityMs = GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel);
            PropulsionSystem = GetPropulsionSystemForWeaponsTechLevel(weaponsTechLevel);
        }

        // ====================================================================
        // Degradation API
        // ====================================================================

        /// <summary>
        /// Register a single shot being fired. Calculates wear based on current gun state,
        /// installed upgrades and returns whether the barrel remains operational after the shot.
        /// This method is safe for test harness reuse and mimics in-game behaviour.
        /// </summary>
        public bool RegisterShot()
        {
            // Compute wear multiplier based on heat and pressure stresses
            double heatFactor = HeatPerShot / Math.Max(1.0, CoolingCapacity); // >1 => overheated
            double pressureFactor = MaxSafePressure > 0 ? (HeatPerShot / MaxSafePressure) : 1.0;

            // Upgrade modifiers
            double upgradeModifier = GetUpgradeWearModifier();

            // Compose final wear for this shot
            double perShotWear = BaseWearPerShot * Math.Max(0.1, heatFactor) * Math.Max(0.5, pressureFactor) * upgradeModifier;

            // Bound wear so it's not absurd for a single shot
            perShotWear = Math.Clamp(perShotWear, 1e-6, 0.2); // min tiny wear, max 20% integrity loss per shot

            ApplyWear(perShotWear);

            ShotsFired++;
            CumulativeWear += perShotWear;

            // If integrity falls below failure threshold it's unusable
            return BarrelIntegrity > IntegrityFailureThreshold;
        }

        /// <summary>
        /// Apply a fractional wear to the barrel integrity (0..1). Internal helper.
        /// </summary>
        private void ApplyWear(double wearFraction)
        {
            // Subtractive model: integrity reduces by wearFraction, clamped to [0,1]
            BarrelIntegrity = Math.Max(0.0, BarrelIntegrity - wearFraction);

            // Keep CumulativeWear consistent (may be slightly redundant)
            CumulativeWear = Math.Min(1.0, CumulativeWear + wearFraction);
        }

        /// <summary>
        /// Repair the barrel by restoring integrity (fractional amount 0..1).
        /// Returns the new integrity value.
        /// </summary>
        public double RepairBarrel(double repairFraction)
        {
            if (repairFraction <= 0) return BarrelIntegrity;
            BarrelIntegrity = Math.Min(1.0, BarrelIntegrity + repairFraction);
            // adjust cumulative wear (optional: recalculate)
            CumulativeWear = Math.Max(0.0, 1.0 - BarrelIntegrity);
            return BarrelIntegrity;
        }

        /// <summary>
        /// Fully replace the barrel (set integrity to 1, reset wear counters).
        /// </summary>
        public void ReplaceBarrel()
        {
            BarrelIntegrity = 1.0;
            ShotsFired = 0;
            CumulativeWear = 0.0;
        }

        /// <summary>
        /// Estimated remaining shots until failure based on current per-shot wear estimate.
        /// Uses current state to estimate typical per-shot wear (not a guarantee).
        /// </summary>
        public int EstimatedShotsRemaining()
        {
            // estimate current per-shot wear with current state
            double heatFactor = HeatPerShot / Math.Max(1.0, CoolingCapacity);
            double pressureFactor = MaxSafePressure > 0 ? (HeatPerShot / MaxSafePressure) : 1.0;
            double perShotWear = BaseWearPerShot * Math.Max(0.1, heatFactor) * Math.Max(0.5, pressureFactor) * GetUpgradeWearModifier();
            perShotWear = Math.Clamp(perShotWear, 1e-6, 0.2);

            if (perShotWear <= 0) return int.MaxValue;
            double workableIntegrity = Math.Max(0.0, BarrelIntegrity - IntegrityFailureThreshold);
            return (int)Math.Floor(workableIntegrity / perShotWear);
        }

        /// <summary>
        /// Returns true if barrel is currently considered unusable.
        /// </summary>
        public bool IsBarrelFailed() => BarrelIntegrity <= IntegrityFailureThreshold;

        /// <summary>
        /// Inspect wear modifiers provided by installed upgrades.
        /// Recognizes upgrade IDs (string-based) so existing inventory system works.
        /// - "ReinforcedBarrel" reduces wear
        /// - "HighTempCoating" reduces heat-driven wear
        /// - "RapidFire" increases wear
        /// </summary>
        private double GetUpgradeWearModifier()
        {
            double modifier = 1.0;

            if (InstalledUpgrades == null || InstalledUpgrades.Count == 0) return modifier;

            foreach (var id in InstalledUpgrades)
            {
                switch (id)
                {
                    case "ReinforcedBarrel":
                        modifier *= 0.6; // 40% less wear
                        break;
                    case "HighTempCoating":
                        modifier *= 0.75; // 25% less heat wear
                        break;
                    case "RapidFire":
                        modifier *= 1.5; // 50% more wear per shot for high ROF
                        break;
                    case "CeramicLiner":
                        modifier *= 0.8;
                        break;
                    default:
                        // Unknown upgrades are ignored for wear (future expansion)
                        break;
                }
            }

            return Math.Max(0.1, modifier); // never reduce wear below 10% of base
        }

        // ====================================================================
        // Existing internal calculation helpers (unchanged)
        // ====================================================================

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
            return basePressure * BarrelIntegrity;
        }

        /// <summary>
        /// Bore diameter defines the supported projectile mass range.
        /// This is used as a hard compatibility constraint.
        /// </summary>
        public (double MinKg, double MaxKg) GetSupportedProjectileMassRangeKg()
        {
            const double referenceBoreMeters = 0.5;
            double bore = Math.Max(0.01, BoreDiameter);
            double areaScale = Math.Pow(bore / referenceBoreMeters, 2.0);

            // Baseline ranges are chosen so legacy presets (incl. 100kg) and crafted cores (10-60kg)
            // are supported by the default 0.5m bore.
            double minKg = 0.3 * areaScale;
            double maxKg = 200.0 * areaScale;

            minKg = Math.Max(0.01, minKg);
            maxKg = Math.Max(minKg, maxKg);
            return (minKg, maxKg);
        }

        /// <summary>
        /// Barrel material limits the maximum usable propellant energy density.
        /// Values are in the same units as PropellantEnergyDensity.
        /// </summary>
        public double GetMaxUsablePropellantEnergyDensity()
        {
            // "Steel" is treated as the baseline safe cap.
            const double steelSafeCap = 5.0;

            double materialMultiplier = BarrelMaterial switch
            {
                "Steel" => 1.0,
                "Titanium" => 1.2,
                "Composite" => 1.5,
                "Exotic" => 2.0,
                _ => 1.0
            };

            return steelSafeCap * materialMultiplier;
        }

        public double GetEffectivePropellantEnergyDensity()
        {
            double cap = GetMaxUsablePropellantEnergyDensity();
            return Math.Min(PropellantEnergyDensity, cap);
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
