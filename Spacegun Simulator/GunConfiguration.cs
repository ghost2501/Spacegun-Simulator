using System;
using System.Collections.Generic;

namespace Spacegun_Simulator
{
    // ====================================================================
    // GUN CONFIGURATION
    // ====================================================================
    // The gun provides the BASE muzzle velocity for all projectiles.
    // Velocity is determined by Weapons Tech level and gun upgrades.
    // Projectile propulsion (Delta-V) is an optional modifier unlocked at
    // Projectiles Tech Level 2+.
    //
    // NEW: Barrel degradation system
    // - Tracks shots fired and cumulative wear
    // - Applies per-shot wear based on heat, pressure and installed upgrades
    // - Exposes repair/replacement helpers and diagnostics
    public class GunConfiguration
    {
        public double BarrelLength { get; set; }
        public double BoreDiameter { get; set; }
        public string BarrelMaterial { get; set; }
        public double BarrelIntegrity { get; set; } // 0.0 (destroyed) .. 1.0 (new)

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
        /// <summary>
        /// Number of shots fired since last maintenance/replace.
        /// </summary>
        public long ShotsFired { get; private set; }

        /// <summary>
        /// Cumulative wear applied as a fraction (0..1). Mirrors integrity reduction.
        /// </summary>
        public double CumulativeWear { get; private set; }

        /// <summary>
        /// Base wear per shot (fraction of integrity lost) under nominal conditions.
        /// Designers can tune this value. Typical default produces slow wear across many shots.
        /// </summary>
        public double BaseWearPerShot { get; set; } = 0.0005; // 0.05% per nominal shot

        /// <summary>
        /// Minimum integrity threshold below which the barrel is considered unusable.
        /// </summary>
        public double IntegrityFailureThreshold { get; set; } = 0.05; // 5%

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

            // Default base velocity for Weapons Tech Level 1
            BaseMuzzleVelocityMs = 80_000;  // 80 km/s

            ShotsFired = 0;
            CumulativeWear = 0.0;
        }

        /// <summary>
        /// Get the base muzzle velocity for a given Weapons Tech level.
        /// Higher tech levels unlock faster gun systems.
        /// </summary>
        public static double GetBaseMuzzleVelocityForTechLevel(int weaponsTechLevel)
        {
            return weaponsTechLevel switch
            {
                1 => 80_000,    // 80 km/s - Chemical propellant
                2 => 160_000,   // 160 km/s - Electromagnetic railgun
                3 => 350_000,   // 350 km/s - Plasma rail system
                _ => 80_000
            };
        }

        /// <summary>
        /// Update the gun's base velocity based on current Weapons Tech level.
        /// Call this after researching Weapons tech upgrades.
        /// </summary>
        public void UpdateBaseMuzzleVelocity(int weaponsTechLevel)
        {
            BaseMuzzleVelocityMs = GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel);
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

            // Structural reinforcement reduces wear (higher reinforcement -> less wear)
            double reinforcementFactor = 1.0 / Math.Max(0.1, StructuralReinforcement);

            // Upgrade modifiers
            double upgradeModifier = GetUpgradeWearModifier();

            // Compose final wear for this shot
            double perShotWear = BaseWearPerShot * Math.Max(0.1, heatFactor) * Math.Max(0.5, pressureFactor) * reinforcementFactor * upgradeModifier;

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
            double reinforcementFactor = 1.0 / Math.Max(0.1, StructuralReinforcement);
            double perShotWear = BaseWearPerShot * Math.Max(0.1, heatFactor) * Math.Max(0.5, pressureFactor) * reinforcementFactor * GetUpgradeWearModifier();
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
