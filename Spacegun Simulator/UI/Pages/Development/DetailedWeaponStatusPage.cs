using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Core.Stats;
using Spacegun_Simulator.Development.Projectiles;
using Spacegun_Simulator.Development.Technology;
using Spacegun_Simulator.Development.Weapons;
using Spacegun_Simulator.Enemies;

namespace Spacegun_Simulator.UI.Pages.Development;

public sealed class DetailedWeaponStatusPage : PageBase
{
	public override string Id => PageId.DetailedWeaponStatus;
	public override string Title => "DETAILED WEAPON STATUS";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "Press Any Key to Return. (M)enu (Q)uit"
	);

	private readonly List<string> _lines = new();
	private int _scroll;

	public override void OnEnter(UiContext ui)
	{
		_scroll = 0;
		BuildLines(ui);
	}


	private void BuildLines(UiContext ui)
	{
		_lines.Clear();
		try
		{
			var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DetailedWeaponStatusPage requires GameState). ");
			var projectileBaseline = game.ProjectileDefaultsBaseline;

			EnemyTarget? statusTarget = game.CurrentWave?.Targets?.FirstOrDefault();
			double modeHitTolMult = GameModeTuning.Current.GetHitToleranceMultiplier(game.Mode);

			int weaponsTechLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int weaponsTechLevelFromTree)
				? weaponsTechLevelFromTree
				: 1;
			int projectilesTechLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out int projectilesTechLevelFromTree)
				? projectilesTechLevelFromTree
				: 1;

			_lines.Add("=== TECHNOLOGY LEVELS ===");
			_lines.Add(Clamp60($"  Weapons:     Level {weaponsTechLevel}"));
			_lines.Add(Clamp60($"               {TechTree.GetTechDescription(TechTree.TechType.Weapons, weaponsTechLevel)}"));
			_lines.Add(Clamp60($"  Projectiles: Level {projectilesTechLevel}"));
			_lines.Add(Clamp60($"               {TechTree.GetTechDescription(TechTree.TechType.Projectiles, projectilesTechLevel)}"));
			_lines.Add("");

			// Verbose blocks hidden (not deleted) for gameplay.
			/*
			var weaponsBaseline = game.WeaponsTuningBaseline;
			var gunBaseline = weaponsBaseline.GunTuning;

			int weaponsTechLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int weaponsTechLevelFromTree)
				? weaponsTechLevelFromTree
				: 1;
			int projectilesTechLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out int projectilesTechLevelFromTree)
				? projectilesTechLevelFromTree
				: 1;

		_lines.Add("=== BASELINE TUNING (CANONICAL) ===");
		double baseMuzzleVelocityL1 = weaponsBaseline.BaseMuzzleVelocityMs ?? GameConstants.BaseMuzzleVelocityMs;
		if (weaponsBaseline.WeaponsTechLevels is { Length: > 0 })		
			baseMuzzleVelocityL1 *= Math.Max(0.0, weaponsBaseline.WeaponsTechLevels[0].MuzzleVelocityMultiplier);
		_lines.Add(Clamp60($"  Base Muzzle Velocity (L1): {baseMuzzleVelocityL1:N0} m/s"));
		_lines.Add(Clamp60($"  Muzzle Velocity Mult: {GameConstants.MuzzleVelocityMultiplier:F3}x"));
		double wearPerShot = weaponsBaseline.DefaultBarrelWearPerShot ?? GameConstants.DefaultBarrelWearPerShot;
		_lines.Add(Clamp60($"  Default Barrel Wear/Shot: {wearPerShot:E2}"));
		if (gunBaseline != null)
		{
			_lines.Add(Clamp60($"  Integrity Failure Threshold: {(gunBaseline.IntegrityFailureThreshold ?? WeaponsTuning.Gun.IntegrityFailureThreshold):P1}"));
			_lines.Add(Clamp60($"  Range Ref Barrel Length: {(gunBaseline.RangeReferenceBarrelLength ?? WeaponsTuning.Gun.RangeReferenceBarrelLength):F1} m"));
			_lines.Add(Clamp60($"  Range Mult Clamp: {(gunBaseline.RangeMultiplierMin ?? WeaponsTuning.Gun.RangeMultiplierMin):F2}x - {(gunBaseline.RangeMultiplierMax ?? WeaponsTuning.Gun.RangeMultiplierMax):F2}x"));
		}
		else
		{
			_lines.Add(Clamp60($"  Integrity Failure Threshold: {WeaponsTuning.Gun.IntegrityFailureThreshold:P1}"));
			_lines.Add(Clamp60($"  Range Ref Barrel Length: {WeaponsTuning.Gun.RangeReferenceBarrelLength:F1} m"));
			_lines.Add(Clamp60($"  Range Mult Clamp: {WeaponsTuning.Gun.RangeMultiplierMin:F2}x - {WeaponsTuning.Gun.RangeMultiplierMax:F2}x"));
		}
		_lines.Add("");

		_lines.Add("=== PROJECTILE DEFAULTS (BASELINE) ===");
		_lines.Add(Clamp60($"  Mass: {projectileBaseline.Mass:N0} kg"));
		_lines.Add(Clamp60($"  Length: {projectileBaseline.Length:F2} m"));
		_lines.Add(Clamp60($"  Guidance: {(projectileBaseline.HasGuidance ? "Yes" : "No")}"));
		_lines.Add(Clamp60($"  Guidance Accuracy: {projectileBaseline.GuidanceAccuracy:F3}"));
		_lines.Add(Clamp60($"  Impact Coupling: {projectileBaseline.ImpactCoupling:F6}x"));
		_lines.Add(Clamp60($"  Coupling Ref Mass: {projectileBaseline.ImpactCouplingReferenceMassKg:N0} kg"));
		_lines.Add(Clamp60($"  Coupling Mass Exp: {projectileBaseline.ImpactCouplingMassExponent:F3}"));
		_lines.Add(Clamp60($"  Coupling Tech/Level: {projectileBaseline.ImpactCouplingTechMultiplierPerWeaponsLevel:F3}x"));
		_lines.Add("");

		*/

			_lines.Add("=== CURRENT LIMITS (CANONICAL) ===");
		var diffCfg = DifficultyConfig.GetConfig(game.SelectedDifficulty);
		double effectiveGunRange = diffCfg.IsTutorialMode
			? DifficultyConfig.TutorialPotatoCannon.EffectiveRangeMeters
			: game.GetCurrentEffectiveGunRangeMeters();
		_lines.Add(Clamp60($"  Effective Gun Range: {GameConstants.FormatDistance(effectiveGunRange)}"));
		if (diffCfg.IsTutorialMode)
		{
			_lines.Add(Clamp60("    (Tutorial fixed envelope)"));
		}
		else
		{
			var tier = GameConstants.GetTierForWave(game.CurrentWaveNumber);
			double tierEnvelope = tier.MaxEffectiveGunRange;
			double barrelMult = game.Gun?.RangeMultiplierFromBarrelLength ?? 1.0;
			double stealthMult = (game.Detection != null && game.CurrentWave != null)
				? game.Detection.GetStealthRangeMultiplier(game.CurrentWave)
				: 1.0;
			double rebuilt = Math.Max(0.0, tierEnvelope * barrelMult * stealthMult);
			_lines.Add(Clamp60($"    Tier Envelope: {GameConstants.FormatDistance(tierEnvelope)}"));
			_lines.Add(Clamp60($"    Barrel Mult: {barrelMult:F3}x"));
			_lines.Add(Clamp60($"    Stealth Mult: {stealthMult:F3}x"));
			_lines.Add(Clamp60($"    Rebuilt: {GameConstants.FormatDistance(rebuilt)}"));
		}

		if (game.Gun != null)
		{
			var (minKg, maxKg) = game.Gun.GetSupportedProjectileMassRangeKg();
			double currentMassKg = game.CraftedProjectile?.MassKg
				?? game.Gun.DefaultProjectile?.Mass
				?? projectileBaseline.Mass;
			bool inRange = currentMassKg >= minKg && currentMassKg <= maxKg;
			_lines.Add(Clamp60($"  Supported Projectile Mass: {minKg:N2} - {maxKg:N0} kg"));
			_lines.Add(Clamp60($"    Current: {currentMassKg:N0} kg ({(inRange ? "OK" : "OUT OF RANGE")})"));
		}
		_lines.Add(Clamp60($"  Max Launch Velocity: {game.GetCurrentMaxLaunchVelocityMs():N0} m/s"));
		_lines.Add(Clamp60($"  Difficulty: {diffCfg.DisplayName}"));
		_lines.Add(Clamp60($"    Target RCS Mult: {diffCfg.TargetRcsMultiplier:F3}x"));
		_lines.Add(Clamp60($"    Hit Tolerance Mult: {diffCfg.HitToleranceMultiplier:F3}x"));
		_lines.Add("");

		/*
		_lines.Add("=== MODE TUNING (CANONICAL) ===");
		double modeHitTolMult2 = GameModeTuning.Current.GetHitToleranceMultiplier(game.Mode);
		_lines.Add(Clamp60($"  Hit Tolerance Mult (Mode): {modeHitTolMult2:F3}x"));
		_lines.Add(Clamp60($"  Fracture Energy Defense Scale: {GameModeTuning.Current.FractureEnergyDefenseScale:F3}x"));
		_lines.Add("");
		*/

		_lines.Add("=== INSTALLED UPGRADES (GUN) ===");
		if (game.Gun?.InstalledUpgrades is null || game.Gun.InstalledUpgrades.Count == 0)
		{
			_lines.Add(Clamp60("  (none)"));
			_lines.Add("");
		}
		else
		{
			var defs = WeaponsUpgrades.Definitions;
			var byId = new Dictionary<string, WeaponsUpgrades.UpgradeDefinition>(StringComparer.OrdinalIgnoreCase);
			foreach (var d in defs)
				if (!string.IsNullOrWhiteSpace(d.Id))
					byId[d.Id] = d;

			double combinedWearMult = 1.0;
			var wearMap = WeaponsTuning.Gun.WearModifiersByUpgradeId;
			foreach (var id in game.Gun.InstalledUpgrades)
			{
				if (!string.IsNullOrWhiteSpace(id) && wearMap is not null && wearMap.TryGetValue(id, out double m))
					combinedWearMult *= m;
			}
			combinedWearMult = Math.Max(WeaponsTuning.Gun.UpgradeWearModifierMin, combinedWearMult);
			_lines.Add(Clamp60($"  Combined Wear Mult (applied): {combinedWearMult:F3}x"));

			foreach (var id in game.Gun.InstalledUpgrades)
			{
				if (string.IsNullOrWhiteSpace(id))
					continue;

				if (byId.TryGetValue(id, out var def))
				{
					_lines.Add(Clamp60($"  - {def.Id}: {def.Name}"));
					if (def.Parameters is not null && def.Parameters.Count > 0)
					{
						foreach (var kvp in def.Parameters.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
							_lines.Add(Clamp60($"      {kvp.Key}: {kvp.Value}"));
					}
				}
				else
				{
					_lines.Add(Clamp60($"  - {id}: (unknown upgrade id)"));
				}
			}

			// Also show the declared stat modifiers for completeness (these may have already been applied
			// to Gun.* stats at purchase time, but are still useful for debugging/verification).
			var upgradeDeclaredModifiers = new List<(string UpgradeId, StatModifier Mod)>();
			foreach (var id in game.Gun.InstalledUpgrades)
			{
				if (string.IsNullOrWhiteSpace(id))
					continue;

				if (!byId.TryGetValue(id, out var def))
					continue;
				if (def.Modifiers is null || def.Modifiers.Count == 0)
					continue;

				foreach (var m in def.Modifiers)
				{
					if (m is null) continue;
					if (string.IsNullOrWhiteSpace(m.Key)) continue;
					upgradeDeclaredModifiers.Add((id, m));
				}
			}

			if (upgradeDeclaredModifiers.Count > 0)
			{
				static string Fmt(double v) => v.ToString("G6");

				_lines.Add(Clamp60(""));
				_lines.Add(Clamp60("  Declared Stat Modifiers (from installed upgrades):"));
				_lines.Add(Clamp60($"    Count: {upgradeDeclaredModifiers.Count}"));

				var byKey = new Dictionary<string, List<(string UpgradeId, StatModifier Mod)>>(StringComparer.OrdinalIgnoreCase);
				foreach (var pair in upgradeDeclaredModifiers)
				{
					if (!byKey.TryGetValue(pair.Mod.Key, out var list))
					{
						list = new List<(string UpgradeId, StatModifier Mod)>();
						byKey[pair.Mod.Key] = list;
					}
					list.Add(pair);
				}

				foreach (var key in byKey.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
				{
					var mods = byKey[key];
					_lines.Add(Clamp60($"    -- {key} --"));

					bool affine = mods.All(m => m.Mod.Op is StatModifierOp.Add or StatModifierOp.Mul);
					if (affine)
					{
						// Fold ordered ops into: x -> a*x + b
						double a = 1.0;
						double b = 0.0;
						foreach (var pair in mods)
						{
							var m = pair.Mod;
							if (m.Op == StatModifierOp.Add)
							{
								b += m.Value;
							}
							else // Mul
							{
								a *= m.Value;
								b *= m.Value;
							}
						}

						if (Math.Abs(b) < 1e-12)
						{
							// _lines.Add(Clamp60($"       Combined: x * {Fmt(a)}"));
						}
						else if (Math.Abs(a - 1.0) < 1e-12)
						{
							// _lines.Add(Clamp60($"       Combined: x + {Fmt(b)}"));
						}
						else
						{
							// _lines.Add(Clamp60($"       Combined: x * {Fmt(a)} + {Fmt(b)}"));
						}
					}
					else
					{
						// Render an order-preserving expression.
						string expr = "x";
						foreach (var pair in mods)
						{
							var m = pair.Mod;
							expr = m.Op switch
							{
								StatModifierOp.Add => $"({expr}+{Fmt(m.Value)})",
								StatModifierOp.Mul => $"({expr}*{Fmt(m.Value)})",
								StatModifierOp.Set => Fmt(m.Value),
								StatModifierOp.ClampMin => $"max({expr},{Fmt(m.Value)})",
								StatModifierOp.ClampMax => $"min({expr},{Fmt(m.Value)})",
								_ => expr,
							};
						}

						// _lines.Add(Clamp60($"       Combined: {expr}"));
					}

					foreach (var pair in mods)
						_lines.Add(Clamp60($"       - {pair.Mod.Op} {Fmt(pair.Mod.Value)} [{pair.UpgradeId}]"));
				}
			}

			_lines.Add("");
		}

		/*
		_lines.Add("=== INSTALLED STAT MODIFIERS (PERSISTENT) ===");
		_lines.Add(Clamp60("  (Applied during ResolveWeaponStats; affects Projectile/Propulsion/Shot keys)"));
		if (game.Gun?.InstalledStatModifiers is null || game.Gun.InstalledStatModifiers.Count == 0)
		{
			_lines.Add(Clamp60("  (none)"));
			_lines.Add("");
		}
		else
		{
			_lines.Add(Clamp60($"  Count: {game.Gun.InstalledStatModifiers.Count}"));
			// (Verbose listing hidden for gameplay.)
			_lines.Add("");
		}
		*/

		_lines.Add("=== CRAFTED PROJECTILE (RAW COMPONENTS) ===");
		if (game.CraftedProjectile is null)
		{
			_lines.Add(Clamp60("  (none configured)"));
			_lines.Add("");
		}
		else
		{
			var crafted = game.CraftedProjectile;
			_lines.Add(Clamp60($"  Name: {crafted.DisplayName}"));
			_lines.Add(Clamp60($"  Core: {crafted.Core.Id} | {crafted.Core.Name}"));
			_lines.Add(Clamp60($"    Mass: {crafted.Core.MassKg:N0} kg"));
			_lines.Add(Clamp60($"    Cost: {crafted.Core.Cost.Budget:F0} Budget, {crafted.Core.Cost.Steel:F0} Steel, {crafted.Core.Cost.ExoticMaterials:F0} Exotic"));
			_lines.Add(Clamp60($"  Propulsion: {crafted.Propulsion.Id} | {crafted.Propulsion.Name}"));
			_lines.Add(Clamp60($"    Δv Capacity: {crafted.Propulsion.DeltaVCapacityMs:N0} m/s"));
			_lines.Add(Clamp60($"    Burn: {crafted.Propulsion.BurnDurationSeconds:F1} s | Burn Rate: {crafted.Propulsion.BurnRateMsPerSecond:N0} m/s/s"));
			_lines.Add(Clamp60($"    Ref Mass: {crafted.Propulsion.ReferenceMassKg:N0} kg"));
			_lines.Add(Clamp60($"  Guidance: {crafted.GuidanceModule.Id} | {crafted.GuidanceModule.Name}"));
			_lines.Add(Clamp60($"    Hit Tol Bonus: {crafted.GuidanceModule.HitToleranceBonus:F3}x"));
			_lines.Add(Clamp60($"    Penetration: {crafted.GuidanceModule.Penetration:F3}x"));
			_lines.Add(Clamp60($"    Impact Coupling: {crafted.GuidanceModule.ImpactCoupling:F3}x"));
			_lines.Add(Clamp60($"    Defense Bonus: {crafted.GuidanceModule.DefenseBonus:P0}"));
			_lines.Add(Clamp60($"  Payload: {crafted.PayloadModule.Id} | {crafted.PayloadModule.Name}"));
			_lines.Add(Clamp60($"    Hit Tol Bonus: {crafted.PayloadModule.HitToleranceBonus:F3}x"));
			_lines.Add(Clamp60($"    Penetration: {crafted.PayloadModule.Penetration:F3}x"));
			_lines.Add(Clamp60($"    Impact Coupling: {crafted.PayloadModule.ImpactCoupling:F3}x"));
			_lines.Add(Clamp60($"    Defense Bonus: {crafted.PayloadModule.DefenseBonus:P0}"));
			_lines.Add(Clamp60($"  Armor: {crafted.ArmorModule.Id} | {crafted.ArmorModule.Name}"));
			_lines.Add(Clamp60($"    Hit Tol Bonus: {crafted.ArmorModule.HitToleranceBonus:F3}x"));
			_lines.Add(Clamp60($"    Penetration: {crafted.ArmorModule.Penetration:F3}x"));
			_lines.Add(Clamp60($"    Impact Coupling: {crafted.ArmorModule.ImpactCoupling:F3}x"));
			_lines.Add(Clamp60($"    Defense Bonus: {crafted.ArmorModule.DefenseBonus:P0}"));
			_lines.Add(Clamp60($"    Total Cost: {crafted.TotalCost.Budget:F0} Budget, {crafted.TotalCost.Steel:F0} Steel, {crafted.TotalCost.ExoticMaterials:F0} Exotic"));
			_lines.Add("");
		}

		/*
		_lines.Add("=== STAT BREAKDOWN (BY KEY) ===");
		_lines.Add(Clamp60("  Baseline -> Components -> Mods -> Final (Resolved)"));
		if (statusTarget is null)
		{
			_lines.Add(Clamp60("  (Start a wave to compute shot-derived breakdown.)"));
			_lines.Add("");
		}
		else
		{
			static string Fmt6(double v) => v.ToString("G6");
			static string Fmt3(double v) => v.ToString("F3");

			int weaponsTechLevelBreakdown = 1;
			if (game.TechTree?.CurrentLevel != null && game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int w))
				weaponsTechLevelBreakdown = Math.Max(1, w);

			// Helpers: persistent installed modifiers and declared upgrade modifiers
			double ApplyPersistent(string key, double value)
			{
				if (game.Gun?.InstalledStatModifiers is null || game.Gun.InstalledStatModifiers.Count == 0)
					return value;
				double cur = value;
				foreach (var m in game.Gun.InstalledStatModifiers)
				{
					if (m is null) continue;
					if (!string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
					cur = StatModifierApplier.ApplyOp(m.Op, cur, m.Value);
				}
				return cur;
			}

			var upgradeById = new Dictionary<string, WeaponsUpgrades.UpgradeDefinition>(StringComparer.OrdinalIgnoreCase);
			foreach (var d in WeaponsUpgrades.Definitions)
				if (!string.IsNullOrWhiteSpace(d.Id))
					upgradeById[d.Id] = d;

			List<(string UpgradeId, StatModifier Mod)> GetDeclaredUpgradeMods(string key)
			{
				var list = new List<(string UpgradeId, StatModifier Mod)>();
				if (game.Gun?.InstalledUpgrades is null || game.Gun.InstalledUpgrades.Count == 0)
					return list;

				foreach (var id in game.Gun.InstalledUpgrades)
				{
					if (string.IsNullOrWhiteSpace(id)) continue;
					if (!upgradeById.TryGetValue(id, out var def)) continue;
					if (def.Modifiers is null || def.Modifiers.Count == 0) continue;
					foreach (var m in def.Modifiers)
					{
						if (m is null) continue;
						if (!string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
						list.Add((id, m));
					}
				}

				return list;
			}

			void WriteModsSummary(string indent, string label, IReadOnlyList<StatModifier> mods)
			{
				_lines.Add(Clamp60($"{indent}{label}: {mods.Count}"));
				if (mods.Count == 0) return;

				bool affine = mods.All(m => m.Op is StatModifierOp.Add or StatModifierOp.Mul);
				if (affine)
				{
					double a = 1.0;
					double b = 0.0;
					foreach (var m in mods)
					{
						if (m.Op == StatModifierOp.Add)
							b += m.Value;
						else
						{
							a *= m.Value;
							b *= m.Value;
						}
					}

					string combined = Math.Abs(b) < 1e-12
						? $"x * {Fmt6(a)}"
						: (Math.Abs(a - 1.0) < 1e-12
							? $"x + {Fmt6(b)}"
							: $"x * {Fmt6(a)} + {Fmt6(b)}");
					_lines.Add(Clamp60($"{indent}  Combined: {combined}"));
				}
				else
				{
					string expr = "x";
					foreach (var m in mods)
					{
						expr = m.Op switch
						{
							StatModifierOp.Add => $"({expr}+{Fmt6(m.Value)})",
							StatModifierOp.Mul => $"({expr}*{Fmt6(m.Value)})",
							StatModifierOp.Set => Fmt6(m.Value),
							StatModifierOp.ClampMin => $"max({expr},{Fmt6(m.Value)})",
							StatModifierOp.ClampMax => $"min({expr},{Fmt6(m.Value)})",
							_ => expr,
						};
					}
					_lines.Add(Clamp60($"{indent}  Combined: {expr}"));
				}

				foreach (var m in mods)
					_lines.Add(Clamp60($"{indent}  - {m.Op} {Fmt6(m.Value)}"));
			}

			void WriteDeclaredModsSummary(string indent, string label, IReadOnlyList<(string UpgradeId, StatModifier Mod)> mods)
			{
				_lines.Add(Clamp60($"{indent}{label}: {mods.Count}"));
				if (mods.Count == 0) return;
				foreach (var pair in mods)
					_lines.Add(Clamp60($"{indent}  - {pair.Mod.Op} {Fmt6(pair.Mod.Value)} [{pair.UpgradeId}]"));
			}

			ResolvedWeaponStats resolved;
			try
			{
				resolved = game.ResolveWeaponStats(statusTarget);
			}
			catch (Exception ex)
			{
				_lines.Add(Clamp60("  (Unable to resolve weapon stats)"));
				_lines.Add(Clamp60($"  {ex.GetType().Name}: {ex.Message}"));
				_lines.Add("");
				goto AfterBreakdown;
			}

			// --- Gun keys ---
			_lines.Add(Clamp60("  -- Gun.* --"));
			_lines.Add(Clamp60("    Notes: Most Gun.* upgrades mutate the gun at install-time;"));
			_lines.Add(Clamp60("           this section shows current stored values + declared upgrade modifiers."));
				double baselineWear = weaponsBaseline.DefaultBarrelWearPerShot ?? GameConstants.DefaultBarrelWearPerShot;
				double baselineFail = gunBaseline?.IntegrityFailureThreshold ?? WeaponsTuning.Gun.IntegrityFailureThreshold;
				_lines.Add(Clamp60($"    Baseline (known constants): Wear/Shot {baselineWear:E2} | FailureThreshold {baselineFail:P1}"));
			_lines.Add(Clamp60($"    Baseline (tech): BaseMuzzleVelocity(L{weaponsTechLevelBreakdown}) {GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevelBreakdown):N0} m/s"));

			_lines.Add(Clamp60($"    Gun.BarrelLengthM: {resolved.Gun.BarrelLengthM:F2} m"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.BarrelLengthM"));
			_lines.Add(Clamp60($"    Gun.BoreDiameterM: {resolved.Gun.BoreDiameterM:F3} m"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.BoreDiameterM"));
			_lines.Add(Clamp60($"    Gun.BarrelIntegrity01: {resolved.Gun.BarrelIntegrity01:F3}"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.BarrelIntegrity01"));
			_lines.Add(Clamp60($"    Gun.FireControlQuality: {resolved.Gun.FireControlQuality:F3}"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.FireControlQuality"));
			_lines.Add(Clamp60($"    Gun.BaseMuzzleVelocityMs: {resolved.Gun.BaseMuzzleVelocityMs:N0} m/s"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.BaseMuzzleVelocityMs"));
			_lines.Add(Clamp60($"    Gun.RangeMultiplierFromBarrelLength: {resolved.Gun.RangeMultiplierFromBarrelLength:F3}x"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.RangeMultiplierFromBarrelLength"));
			_lines.Add(Clamp60($"    Gun.BaseWearPerShot01: {resolved.Gun.BaseWearPerShot01:E2}"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.BaseWearPerShot01"));
			_lines.Add(Clamp60($"    Gun.IntegrityFailureThreshold01: {resolved.Gun.IntegrityFailureThreshold01:P1}"));
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Gun.IntegrityFailureThreshold01"));
			_lines.Add("");

			// --- Projectile / Shot keys ---
			double baselineMass = projectileBaseline.Mass;
			double rawMass = game.CraftedProjectile?.MassKg
				?? game.Gun?.DefaultProjectile?.Mass
				?? baselineMass;
			double moddedMass = ApplyPersistent("Projectile.MassKg", rawMass);
			moddedMass = Math.Max(0.01, moddedMass);

			double rawPen = game.CraftedProjectile?.PenetrationMultiplier ?? 1.0;
			double moddedPen = ApplyPersistent("Projectile.PenetrationMult", rawPen);
			moddedPen = Math.Max(0.1, moddedPen);

			double baselineImpactCoupling = projectileBaseline.ImpactCoupling;
			double moduleImpactCoupling = game.CraftedProjectile?.ImpactCouplingMultiplier ?? 1.0;
			double couplingReferenceMassKg = Math.Max(0.01, projectileBaseline.ImpactCouplingReferenceMassKg);
			double couplingMassExponent = Math.Max(0.0, projectileBaseline.ImpactCouplingMassExponent);
			double couplingMassScale = couplingMassExponent > 0.0
				? Math.Pow(couplingReferenceMassKg / Math.Max(0.01, moddedMass), couplingMassExponent)
				: 1.0;
			double couplingTechPerLevel = Math.Max(0.0, projectileBaseline.ImpactCouplingTechMultiplierPerWeaponsLevel);
			double couplingTechScale = couplingTechPerLevel != 1.0
				? Math.Pow(couplingTechPerLevel, Math.Max(0, weaponsTechLevelBreakdown - 1))
				: 1.0;
			double impactCouplingPreModKey = Math.Clamp(
				baselineImpactCoupling * couplingMassScale * couplingTechScale * moduleImpactCoupling,
				0.0001,
				100.0);
			double impactCouplingFinal = ApplyPersistent("Projectile.ImpactCouplingMult", impactCouplingPreModKey);
			impactCouplingFinal = Math.Clamp(impactCouplingFinal, 0.0001, 100.0);

			double rawHitTol = game.CraftedProjectile?.HitToleranceMultiplier ?? 1.0;
			double moddedHitTol = ApplyPersistent("Projectile.HitToleranceMult", rawHitTol);
			moddedHitTol = ApplyPersistent("Shot.AdditionalHitToleranceMult", moddedHitTol);
			moddedHitTol = Math.Max(0.1, moddedHitTol);

			double rawDeltaV = game.CraftedProjectile?.Propulsion?.DeltaVCapacityMs ?? 0.0;
			double rawBurnS = game.CraftedProjectile?.Propulsion?.BurnDurationSeconds ?? 1.0;
			double rawRefMass = game.CraftedProjectile?.Propulsion?.ReferenceMassKg ?? 10.0;
			double moddedDeltaV = Math.Max(0.0, ApplyPersistent("Propulsion.DeltaVCapacityMs", rawDeltaV));
			double moddedBurnS = Math.Max(0.1, ApplyPersistent("Propulsion.BurnDurationS", rawBurnS));
			double moddedRefMass = Math.Max(0.01, ApplyPersistent("Propulsion.ReferenceMassKg", rawRefMass));

			double rawDefense = game.CraftedProjectile?.DefenseRating ?? 0.0;
			double moddedDefense = ApplyPersistent("Projectile.DefenseRating01", rawDefense);
			moddedDefense = Math.Clamp(moddedDefense, 0.0, 1.0);

			// Shot-derived breakdowns
			double defense01 = Math.Clamp(statusTarget.Defense, 0.0, 1.0);
			double defenseScale = Math.Max(0.0, GameModeTuning.Current.FractureEnergyDefenseScale);
			double armoredFractureEnergyMJ = Math.Max(0.0, statusTarget.FractureEnergy * (1.0 + defenseScale * defense01));
			double effEnergyPreModKey = Math.Max(0.0, armoredFractureEnergyMJ / (moddedPen * impactCouplingFinal));
			double effEnergyFinal = ApplyPersistent("Shot.EffectiveFractureEnergyMJ", effEnergyPreModKey);
			effEnergyFinal = Math.Max(0.0, effEnergyFinal);

			var projectileCfg = new ProjectileConfiguration { Mass = moddedMass };
			double energyBasedMax = BallisticsCalculator.CalculateMuzzleVelocity(game.Gun!, projectileCfg);
			double barrelEfficiency = Math.Min(1.0, game.Gun!.BarrelLength / 200.0);
			double barrelMultiplier = (0.5 + 0.5 * barrelEfficiency);
			double techBaseMax = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevelBreakdown) * barrelMultiplier * game.Gun!.BarrelIntegrity;
			double maxLaunchPreModKey = Math.Max(1.0, Math.Min(techBaseMax, energyBasedMax));
			double maxLaunchFinal = ApplyPersistent("Shot.MaxLaunchVelocityMs", maxLaunchPreModKey);
			maxLaunchFinal = Math.Max(1.0, maxLaunchFinal);

			_lines.Add(Clamp60("  -- Projectile.MassKg --"));
			_lines.Add(Clamp60($"    Baseline: {baselineMass:N0} kg"));
			_lines.Add(Clamp60($"    Components: {rawMass:N0} kg"));
			_lines.Add(Clamp60($"    After Mods: {moddedMass:N0} kg"));
			_lines.Add(Clamp60($"    Final: {resolved.Shot.ProjectileMassKg:N0} kg"));
			WriteModsSummary("    ", "Persistent Mods", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Projectile.MassKg", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Projectile.MassKg"));
			_lines.Add("");

			_lines.Add(Clamp60("  -- Projectile.PenetrationMult --"));
			_lines.Add(Clamp60("    Baseline: 1.0"));
			_lines.Add(Clamp60($"    Components: {Fmt3(rawPen)}x"));
			_lines.Add(Clamp60($"    After Mods: {Fmt3(moddedPen)}x"));
			_lines.Add(Clamp60($"    Final: {Fmt3(resolved.Shot.Penetration)}x"));
			WriteModsSummary("    ", "Persistent Mods", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Projectile.PenetrationMult", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Projectile.PenetrationMult"));
			_lines.Add("");

			_lines.Add(Clamp60("  -- Projectile.ImpactCouplingMult --"));
			_lines.Add(Clamp60($"    Baseline: {Fmt6(baselineImpactCoupling)}x"));
			_lines.Add(Clamp60($"    Components: Modules {Fmt3(moduleImpactCoupling)}x"));
			_lines.Add(Clamp60($"    Derived: MassScale {Fmt3(couplingMassScale)}x | TechScale {Fmt3(couplingTechScale)}x"));
			_lines.Add(Clamp60($"    Pre-Mod Key Value: {Fmt6(impactCouplingPreModKey)}x"));
			_lines.Add(Clamp60($"    After Mods: {Fmt6(impactCouplingFinal)}x"));
			_lines.Add(Clamp60($"    Final: {Fmt6(resolved.Projectile.ImpactCouplingMult)}x"));
			WriteModsSummary("    ", "Persistent Mods", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Projectile.ImpactCouplingMult", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Projectile.ImpactCouplingMult"));
			_lines.Add("");

			_lines.Add(Clamp60("  -- Projectile.HitToleranceMult / Shot.AdditionalHitToleranceMult --"));
			_lines.Add(Clamp60("    Baseline: 1.0"));
			_lines.Add(Clamp60($"    Components: {Fmt3(rawHitTol)}x"));
			_lines.Add(Clamp60($"    After Mods: {Fmt3(moddedHitTol)}x"));
			_lines.Add(Clamp60($"    Final: {Fmt3(resolved.Shot.AdditionalHitToleranceMultiplier)}x"));
			WriteModsSummary("    ", "Persistent Mods (Projectile.HitToleranceMult)", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Projectile.HitToleranceMult", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteModsSummary("    ", "Persistent Mods (Shot.AdditionalHitToleranceMult)", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Shot.AdditionalHitToleranceMult", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Projectile.HitToleranceMult").Concat(GetDeclaredUpgradeMods("Shot.AdditionalHitToleranceMult")).ToList());
			_lines.Add("");

			_lines.Add(Clamp60("  -- Propulsion.* --"));
			_lines.Add(Clamp60($"    Components: Δv {rawDeltaV:N0} m/s | Burn {rawBurnS:F1} s | RefMass {rawRefMass:N0} kg"));
			_lines.Add(Clamp60($"    After Mods: Δv {moddedDeltaV:N0} m/s | Burn {moddedBurnS:F1} s | RefMass {moddedRefMass:N0} kg"));
			_lines.Add(Clamp60($"    Final: Δv {resolved.Shot.PropulsionDeltaVCapacityMs:N0} m/s | Burn {resolved.Shot.PropulsionBurnDurationSeconds:F1} s | RefMass {resolved.Shot.PropulsionReferenceMassKg:N0} kg"));
			WriteModsSummary("    ", "Persistent Mods (Propulsion.DeltaVCapacityMs)", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Propulsion.DeltaVCapacityMs", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteModsSummary("    ", "Persistent Mods (Propulsion.BurnDurationS)", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Propulsion.BurnDurationS", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteModsSummary("    ", "Persistent Mods (Propulsion.ReferenceMassKg)", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Propulsion.ReferenceMassKg", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Propulsion.DeltaVCapacityMs").Concat(GetDeclaredUpgradeMods("Propulsion.BurnDurationS")).Concat(GetDeclaredUpgradeMods("Propulsion.ReferenceMassKg")).ToList());
			_lines.Add("");

			_lines.Add(Clamp60("  -- Projectile.DefenseRating01 --"));
			_lines.Add(Clamp60("    Baseline: 0.0"));
			_lines.Add(Clamp60($"    Components: {Fmt3(rawDefense)}"));
			_lines.Add(Clamp60($"    After Mods: {Fmt3(moddedDefense)}"));
			_lines.Add(Clamp60($"    Final: {Fmt3(resolved.Shot.ProjectileDefenseRating)}"));
			WriteModsSummary("    ", "Persistent Mods", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Projectile.DefenseRating01", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Projectile.DefenseRating01"));
			_lines.Add("");

			_lines.Add(Clamp60("  -- Shot.EffectiveFractureEnergyMJ --"));
			_lines.Add(Clamp60($"    Target: FractureEnergy {statusTarget.FractureEnergy:F2} MJ | Defense {defense01:P0}"));
			_lines.Add(Clamp60($"    Mode: FractureEnergyDefenseScale {Fmt3(defenseScale)}x"));
			_lines.Add(Clamp60($"    Armored FE: {armoredFractureEnergyMJ:F2} MJ"));
			_lines.Add(Clamp60($"    Inputs: Pen {Fmt3(moddedPen)}x | Coupling {Fmt6(impactCouplingFinal)}x"));
			_lines.Add(Clamp60($"    Pre-Mod Key Value: {effEnergyPreModKey:F2} MJ"));
			_lines.Add(Clamp60($"    After Mods: {effEnergyFinal:F2} MJ"));
			_lines.Add(Clamp60($"    Final: {resolved.Shot.EffectiveFractureEnergyMJ:F2} MJ"));
			WriteModsSummary("    ", "Persistent Mods", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Shot.EffectiveFractureEnergyMJ", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Shot.EffectiveFractureEnergyMJ"));
			_lines.Add("");

			_lines.Add(Clamp60("  -- Shot.MaxLaunchVelocityMs --"));
			_lines.Add(Clamp60($"    Inputs: Mass {moddedMass:N0} kg | WeaponsTL {weaponsTechLevelBreakdown}"));
			_lines.Add(Clamp60($"    Pre-Mod: EnergyBased {energyBasedMax:N0} m/s | TechCap {techBaseMax:N0} m/s"));
			_lines.Add(Clamp60($"    Pre-Mod Key Value: {maxLaunchPreModKey:N0} m/s"));
			_lines.Add(Clamp60($"    After Mods: {maxLaunchFinal:N0} m/s"));
			_lines.Add(Clamp60($"    Final: {resolved.Shot.MaxLaunchVelocityMs:N0} m/s"));
			WriteModsSummary("    ", "Persistent Mods", (game.Gun?.InstalledStatModifiers ?? new List<StatModifier>()).Where(m => m is not null && string.Equals(m.Key, "Shot.MaxLaunchVelocityMs", StringComparison.OrdinalIgnoreCase)).ToList());
			WriteDeclaredModsSummary("    ", "Declared Upgrade Mods", GetDeclaredUpgradeMods("Shot.MaxLaunchVelocityMs"));
			_lines.Add("");

		AfterBreakdown:
			;
		}
		*/

		_lines.Add("=== RESOLVED (CANONICAL) ===");
		if (statusTarget is null)
		{
			_lines.Add(Clamp60("  Shot-derived values: (no active wave target)"));
			_lines.Add(Clamp60("  Start a wave to see Effective Fracture Energy, etc."));
			_lines.Add("");
		}
		else
		{
			try
			{
				var resolved = game.ResolveWeaponStats(statusTarget);

				_lines.Add("  -- Gun --");
				_lines.Add(Clamp60($"  Weapons Tech Level: {resolved.Gun.WeaponsTechLevel}"));
				_lines.Add(Clamp60($"  Barrel Length: {resolved.Gun.BarrelLengthM:F1} m"));
				_lines.Add(Clamp60($"  Bore Diameter: {resolved.Gun.BoreDiameterM:F3} m"));
				_lines.Add(Clamp60($"  Barrel Material: {resolved.Gun.BarrelMaterial}"));
				_lines.Add(Clamp60($"  Barrel Integrity: {resolved.Gun.BarrelIntegrity01:P0}"));
				_lines.Add(Clamp60($"  Fire Control Quality: {resolved.Gun.FireControlQuality:F2}x"));
				_lines.Add(Clamp60($"  Base Muzzle Velocity: {resolved.Gun.BaseMuzzleVelocityMs:N0} m/s"));
				_lines.Add(Clamp60($"  Range Mult (Barrel): {resolved.Gun.RangeMultiplierFromBarrelLength:F2}x"));
				_lines.Add(Clamp60($"  Max Launch Velocity (Energy): {resolved.Gun.EnergyBasedMaxLaunchVelocityMs:N0} m/s"));
				_lines.Add(Clamp60($"  Max Launch Velocity (Tech): {resolved.Gun.TechBasedMaxLaunchVelocityMs:N0} m/s"));
				_lines.Add(Clamp60($"  Max Launch Velocity (Final): {resolved.Gun.MaxLaunchVelocityMs:N0} m/s"));
				_lines.Add(Clamp60($"  Base Wear/Shot: {resolved.Gun.BaseWearPerShot01:E2}"));
				_lines.Add(Clamp60($"  Failure Threshold: {resolved.Gun.IntegrityFailureThreshold01:P1}"));
				_lines.Add(Clamp60($"  Shots Fired: {resolved.Gun.ShotsFired:N0} | Cumulative Wear: {resolved.Gun.CumulativeWear01:P1}"));
				_lines.Add("");

				_lines.Add("  -- Projectile --");
				_lines.Add(Clamp60($"  Core: {resolved.Projectile.CoreId ?? "(none)"}"));
				_lines.Add(Clamp60($"  Propulsion: {resolved.Projectile.PropulsionId ?? "(none)"}"));
				_lines.Add(Clamp60($"  Guidance: {resolved.Projectile.GuidanceModuleId ?? "(none)"}"));
				_lines.Add(Clamp60($"  Payload: {resolved.Projectile.PayloadModuleId ?? "(none)"}"));
				_lines.Add(Clamp60($"  Armor: {resolved.Projectile.ArmorModuleId ?? "(none)"}"));
				_lines.Add(Clamp60($"  Mass: {resolved.Projectile.MassKg:N0} kg"));
				_lines.Add(Clamp60($"  Penetration: {resolved.Projectile.PenetrationMult:F3}x"));
				_lines.Add(Clamp60($"  Impact Coupling (Base): {resolved.Projectile.BaseImpactCouplingMult:F4}x"));
				_lines.Add(Clamp60($"  Impact Coupling (Mods): {resolved.Projectile.ModuleImpactCouplingMult:F3}x"));
				_lines.Add(Clamp60($"  Impact Coupling (Final): {resolved.Projectile.ImpactCouplingMult:F4}x"));
				_lines.Add(Clamp60($"  Hit Tolerance Mult: {resolved.Projectile.HitToleranceMult:F3}x"));
				_lines.Add(Clamp60($"  Projectile Defense: {resolved.Projectile.DefenseRating01:P0}"));
				_lines.Add(Clamp60($"  Prop Δv Capacity: {resolved.Projectile.PropulsionDeltaVCapacityMs:N0} m/s"));
				_lines.Add(Clamp60($"  Prop Burn: {resolved.Projectile.PropulsionBurnDurationSeconds:F1} s"));
				_lines.Add(Clamp60($"  Prop Ref Mass: {resolved.Projectile.PropulsionReferenceMassKg:N0} kg"));
				_lines.Add("");

				_lines.Add("  -- Shot/Solver Inputs --");
				_lines.Add(Clamp60($"  Target: {statusTarget.Name}"));
				_lines.Add(Clamp60($"  Effective Fracture Energy: {resolved.Shot.EffectiveFractureEnergyMJ:N0} MJ"));
				_lines.Add(Clamp60($"  Projectile Mass (Used): {resolved.Shot.ProjectileMassKg:N0} kg"));
				_lines.Add(Clamp60($"  Max Launch Velocity (Used): {resolved.Shot.MaxLaunchVelocityMs:N0} m/s"));
				_lines.Add(Clamp60($"  Additional Hit Tol Mult (Enh): {resolved.Shot.AdditionalHitToleranceMultiplier:F3}x"));
				_lines.Add(Clamp60($"  Additional Hit Tol Mult (Enh×Mode): {(resolved.Shot.AdditionalHitToleranceMultiplier * modeHitTolMult):F3}x"));
				_lines.Add("");
			}
			catch (Exception ex)
			{
				_lines.Add(Clamp60("  [ResolveWeaponStats failed]"));
				_lines.Add(Clamp60($"  {ex.GetType().Name}: {ex.Message}"));
				_lines.Add("");
			}
		}

		/*
		// Gun Base Velocity
		int weaponsTechLevelForBaseVelocity = 1;
		if (game.TechTree?.CurrentLevel != null && game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int wt))
			weaponsTechLevelForBaseVelocity = Math.Max(1, wt);
		double gunBaseVelocity = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevelForBaseVelocity);
		_lines.Add("=== GUN BASE VELOCITY ===");
		_lines.Add(Clamp60($"  Base Muzzle Velocity: {gunBaseVelocity:N0} m/s ({gunBaseVelocity / 1000:N0} km/s)"));
		_lines.Add("");

		// Unlocked Components
		_lines.Add("=== UNLOCKED COMPONENTS ===");
		var techTree = game.TechTree ?? new TechTree();
		var cores = CraftedProjectile.GetUnlockedCores(techTree);
		_lines.Add(Clamp60($"  Cores ({cores.Count} available):"));
		foreach (var core in cores)
			_lines.Add(Clamp60($"    - {core.Name} ({core.MassKg} kg)"));
		_lines.Add("");

		var propulsion = CraftedProjectile.GetUnlockedPropulsion(techTree);
		_lines.Add(Clamp60($"  Propulsion ({propulsion.Count} available):"));
		foreach (var prop in propulsion)
		{
			if (prop.Id == "none")
				_lines.Add(Clamp60($"    - {prop.Name} (no boost)"));
			else
				_lines.Add(Clamp60($"    - {prop.Name} (+{prop.DeltaVCapacityMs / 1000:N0} km/s Δv over {prop.BurnDurationSeconds:F1}s)"));
		}
		_lines.Add("");

		var guidance = CraftedProjectile.GetUnlockedModules(techTree, ProjectileEnhancementSlot.Guidance);
		_lines.Add(Clamp60($"  Guidance Modules ({guidance.Count} available):"));
		foreach (var m in guidance)
			_lines.Add(Clamp60($"    - {m.Name}"));
		_lines.Add("");

		var payload = CraftedProjectile.GetUnlockedModules(techTree, ProjectileEnhancementSlot.Payload);
		_lines.Add(Clamp60($"  Payload Modules ({payload.Count} available):"));
		foreach (var m in payload)
			_lines.Add(Clamp60($"    - {m.Name}"));
		_lines.Add("");

		var armor = CraftedProjectile.GetUnlockedModules(techTree, ProjectileEnhancementSlot.Armor);
		_lines.Add(Clamp60($"  Armor Modules ({armor.Count} available):"));
		foreach (var m in armor)
			_lines.Add(Clamp60($"    - {m.Name}"));
		_lines.Add("");
		*/

		// Gun Status
		int weaponsTechLevelForBaseVelocity = 1;
		if (game.TechTree?.CurrentLevel != null && game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int wt2))
			weaponsTechLevelForBaseVelocity = Math.Max(1, wt2);

		_lines.Add("=== GUN CONFIGURATION ===");
		var gun = game.Gun ?? throw new InvalidOperationException("GameState.Gun is null.");
		_lines.Add(Clamp60($"  Barrel Integrity: {gun.BarrelIntegrity:P0}"));
		gun.UpdateBaseMuzzleVelocity(weaponsTechLevelForBaseVelocity);
		_lines.Add(Clamp60($"  Propulsion: {gun.PropulsionSystem}"));
		if (gun.PropulsionSystem == Spacegun_Simulator.Development.PropulsionType.Chemical)
		{
			_lines.Add(Clamp60($"  Propellant Mass: {gun.PropellantMass:F0} kg"));
			_lines.Add(Clamp60($"  Propellant Energy Density: {gun.GetEffectivePropellantEnergyDensity():F2} GJ/kg"));
		}
		else
		{
			_lines.Add(Clamp60($"  Power Capacity: {gun.PowerCapacity:F0} MW"));
		}
		_lines.Add(Clamp60($"  Weapons Tech Level: {weaponsTechLevelForBaseVelocity}"));
		_lines.Add("");

		// Current Projectile
		_lines.Add("=== CURRENT PROJECTILE ===");
		if (game.CraftedProjectile != null)
		{
			var proj = game.CraftedProjectile;
			_lines.Add(Clamp60($"  Configuration: {proj.DisplayName}"));
			_lines.Add(Clamp60($"  Mass: {proj.MassKg} kg"));
			_lines.Add(Clamp60($"  Gun Base Velocity: {proj.GunBaseMuzzleVelocityMs:N0} m/s"));

			if (proj.Propulsion.Id != "none")
			{
				double maxDeltaV = proj.Propulsion.CalculateEffectiveDeltaV(proj.MassKg, proj.Propulsion.BurnDurationSeconds);
				_lines.Add(Clamp60($"  Propulsion Δv: +{maxDeltaV:N0} m/s"));
				_lines.Add(Clamp60($"  Max Velocity: {proj.MaxVelocityMs:N0} m/s"));
			}

			_lines.Add(Clamp60($"  Max KE: {proj.RawKineticEnergyMJ:N0} MJ"));
			double p = proj.PenetrationMultiplier;
			if (p != 1.0)
				_lines.Add(Clamp60($"  Penetration: {(p - 1) * 100:+0}%"));
			if (proj.HitToleranceMultiplier != 1.0)
				_lines.Add(Clamp60($"  Hit Tolerance: {(proj.HitToleranceMultiplier - 1) * 100:+0}%"));
			if (proj.ImpactCouplingMultiplier != 1.0)
				_lines.Add(Clamp60($"  Impact Coupling: {(proj.ImpactCouplingMultiplier - 1) * 100:+0}%"));
			if (proj.DefenseRating > 0.0)
				_lines.Add(Clamp60($"  Defense: {proj.DefenseRating:P0}"));
		}
		else
		{
			_lines.Add("  [NOT CONFIGURED]");
		}
		}
		catch (Exception ex)
		{
			_lines.Clear();
			_lines.Add("=== DETAILED WEAPON STATUS ===");
			_lines.Add(Clamp60("  (Unable to render page due to an internal error.)"));
			_lines.Add(Clamp60("  Check ui.log for details."));
			_lines.Add("");
			_lines.Add(Clamp60($"  Error: {ex.GetType().Name}"));
			_lines.Add(Clamp60($"  Message: {ex.Message}"));

			try
			{
				ui.DebugLog($"ERROR DetailedWeaponStatusPage.BuildLines: {ex}");
			}
			catch { }
		}
	}

	protected override void RenderBody(UiContext ui)
	{
		if (_lines.Count == 0)
			BuildLines(ui);

		int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
		int maxScroll = Math.Max(0, _lines.Count - viewport);
		if (_scroll < 0) _scroll = 0;
		if (_scroll > maxScroll) _scroll = maxScroll;

		int end = Math.Min(_lines.Count, _scroll + viewport);
		for (int i = _scroll; i < end; i++)
			ui.WriteLine(Clamp60(_lines[i]));
	}

	protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
	{
		const int lineStep = 1;
		const int pageStep = 6;

		switch (key.Key)
		{
			case ConsoleKey.UpArrow: _scroll -= lineStep; return PageResult.Stay;
			case ConsoleKey.DownArrow: _scroll += lineStep; return PageResult.Stay;
			case ConsoleKey.PageUp: _scroll -= pageStep; return PageResult.Stay;
			case ConsoleKey.PageDown: _scroll += pageStep; return PageResult.Stay;
		}

		// Any other key returns to the previous page.
		return PageResult.Back();
	}
}
