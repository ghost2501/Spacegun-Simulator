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
        public double RangeMultiplierFromBarrelLength => Math.Clamp(
            BarrelLength / Math.Max(1e-6, WeaponsTuning.Gun.RangeReferenceBarrelLength),
            WeaponsTuning.Gun.RangeMultiplierMin,
            WeaponsTuning.Gun.RangeMultiplierMax);

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
        public double IntegrityFailureThreshold { get; set; } = WeaponsTuning.Gun.IntegrityFailureThreshold;

        public double MaxSafePressure => CalculateMaxPressure();
        public double HeatPerShot => CalculateHeatGeneration();
        public double ReloadTime => CalculateReloadTime();

        public GunConfiguration()
        {
            BarrelLength = WeaponsTuning.Gun.DefaultBarrelLength;
            BoreDiameter = WeaponsTuning.Gun.DefaultBoreDiameter;
            BarrelMaterial = WeaponsTuning.Gun.DefaultBarrelMaterial;
            BarrelIntegrity = WeaponsTuning.Gun.DefaultBarrelIntegrity;
            FireControlQuality = WeaponsTuning.Gun.DefaultFireControlQuality;
            PropulsionSystem = WeaponsTuning.Gun.DefaultPropulsionSystem;
            PropellantMass = WeaponsTuning.Gun.DefaultPropellantMass;
            PropellantEnergyDensity = WeaponsTuning.Gun.DefaultPropellantEnergyDensity;
            PowerCapacity = WeaponsTuning.Gun.DefaultPowerCapacity;
            CapacitorEfficiency = WeaponsTuning.Gun.DefaultCapacitorEfficiency;
            CoolingSystem = WeaponsTuning.Gun.DefaultCoolingSystem;
            CoolingCapacity = WeaponsTuning.Gun.DefaultCoolingCapacity;
            AmmunitionCount = WeaponsTuning.Gun.DefaultAmmunitionCount;
            DefaultProjectile = new ProjectileConfiguration();

            IntegrityFailureThreshold = WeaponsTuning.Gun.IntegrityFailureThreshold;

            // Default base velocity for Weapons Tech Level 1 (canonical source)
            BaseMuzzleVelocityMs = GetBaseMuzzleVelocityForTechLevel(1);

            // Initialize wear tunable from canonical source
            BaseWearPerShot = GameConstants.DefaultBarrelWearPerShot;

            ShotsFired = 0;
            CumulativeWear = 0.0;
        }

        /// <summary>
        /// Get the base muzzle velocity for a given Weapons Tech level.
        /// Values are derived from GameConstants.BaseMuzzleVelocityMs and tech multipliers.
        /// </summary>
        public static double GetBaseMuzzleVelocityForTechLevel(int weaponsTechLevel)
        {
            return WeaponsTuning.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel)
                   * Math.Clamp(GameConstants.MuzzleVelocityMultiplier, 0.25, 3.0);
        }

        public static PropulsionType GetPropulsionSystemForWeaponsTechLevel(int weaponsTechLevel)
        {
            return WeaponsTuning.GetPropulsionSystemForTechLevel(weaponsTechLevel);
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
            double heatFactor = HeatPerShot / Math.Max(WeaponsTuning.Gun.WearHeatCoolingCapacityMin, CoolingCapacity); // >1 => overheated
            double pressureFactor = MaxSafePressure > 0 ? (HeatPerShot / MaxSafePressure) : 1.0;

            // Upgrade modifiers
            double upgradeModifier = GetUpgradeWearModifier();

            // Compose final wear for this shot
            double perShotWear = BaseWearPerShot
                * Math.Max(WeaponsTuning.Gun.WearHeatFactorMin, heatFactor)
                * Math.Max(WeaponsTuning.Gun.WearPressureFactorMin, pressureFactor)
                * upgradeModifier;

            // Bound wear so it's not absurd for a single shot
            perShotWear = Math.Clamp(perShotWear, WeaponsTuning.Gun.WearPerShotClampMin, WeaponsTuning.Gun.WearPerShotClampMax);

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
            double heatFactor = HeatPerShot / Math.Max(WeaponsTuning.Gun.WearHeatCoolingCapacityMin, CoolingCapacity);
            double pressureFactor = MaxSafePressure > 0 ? (HeatPerShot / MaxSafePressure) : 1.0;
            double perShotWear = BaseWearPerShot
                * Math.Max(WeaponsTuning.Gun.WearHeatFactorMin, heatFactor)
                * Math.Max(WeaponsTuning.Gun.WearPressureFactorMin, pressureFactor)
                * GetUpgradeWearModifier();
            perShotWear = Math.Clamp(perShotWear, WeaponsTuning.Gun.WearPerShotClampMin, WeaponsTuning.Gun.WearPerShotClampMax);

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

            var map = WeaponsTuning.Gun.WearModifiersByUpgradeId;

            foreach (var id in InstalledUpgrades)
            {
                if (map is not null && map.TryGetValue(id, out double mult))
                    modifier *= mult;
            }

            return Math.Max(WeaponsTuning.Gun.UpgradeWearModifierMin, modifier);
        }

        // ====================================================================
        // Existing internal calculation helpers (unchanged)
        // ====================================================================

        private double CalculateMaxPressure()
        {
            var map = WeaponsTuning.Gun.MaxPressureByBarrelMaterial;
            double basePressure = (map is not null && map.TryGetValue(BarrelMaterial, out double v))
                ? v
                : 500.0;
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

            // Baseline ranges are chosen so the default 0.5m bore supports baseline 5t
            // player projectiles and allows up to 10t.
            double minKg = 0.3 * areaScale;
            double maxKg = 10_000.0 * areaScale;

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
            double steelSafeCap = WeaponsTuning.Gun.SteelSafePropellantEnergyDensityCap;

            var map = WeaponsTuning.Gun.PropellantEnergyDensityCapMultiplierByBarrelMaterial;
            double materialMultiplier = (map is not null && map.TryGetValue(BarrelMaterial, out double v))
                ? v
                : 1.0;

            return steelSafeCap * materialMultiplier;
        }

        public double GetEffectivePropellantEnergyDensity()
        {
            double cap = GetMaxUsablePropellantEnergyDensity();
            return Math.Min(PropellantEnergyDensity, cap);
        }

        private double CalculateHeatGeneration()
        {
            string key = PropulsionSystem.ToString();
            var massCoeffMap = WeaponsTuning.Gun.HeatGenerationCoefficientByPropulsion;
            var powerCoeffMap = WeaponsTuning.Gun.HeatGenerationPowerCoefficientByPropulsion;
            double massCoeff = (massCoeffMap is not null && massCoeffMap.TryGetValue(key, out double mc)) ? mc : 0.0;
            double powerCoeff = (powerCoeffMap is not null && powerCoeffMap.TryGetValue(key, out double pc)) ? pc : 0.0;
            double baseHeat = PropellantMass * massCoeff + PowerCapacity * powerCoeff;
            return baseHeat;
        }

        private double CalculateReloadTime()
        {
            double baseTime = WeaponsTuning.Gun.ReloadBaseTimeSeconds;
            var map = WeaponsTuning.Gun.ReloadCoolingModifierByCoolingSystem;
            double coolingModifier = (map is not null && map.TryGetValue(CoolingSystem.ToString(), out double v))
                ? v
                : 1.0;

            double heatRatio = HeatPerShot / CoolingCapacity;
            if (heatRatio > WeaponsTuning.Gun.ReloadHeatRatioThreshold)
            {
                baseTime *= heatRatio;
            }

            return baseTime * coolingModifier;
        }
    }
}
