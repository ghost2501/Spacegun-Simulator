# Content Authoring Guide: Shot Performance Stats
**Spacegun Simulator - Modding & Content Expansion Reference**

This guide documents every stat/value that can modify shot performance, with recommended ranges, multiplier vs. absolute semantics, and JSON-safety notes for content authors.

---

## 1. Projectile Components (`Config/ProjectilesCatalog.json`)

### 1.1 Cores (`Cores[]`)
**What it does:** Determines projectile mass. Higher mass = more KE at same velocity, but worse Δv mass-efficiency.

| Field | Type | Typical Range | Notes |
|-------|------|---------------|-------|
| `Id` | string | `"light"`, `"standard"`, etc. | Unique identifier |
| `Name` | string | `"Light Core"` | Display name |
| `Description` | string | Short flavor text | Player-facing |
| `MassKg` | **absolute** | 5,000–10,000 kg baseline; 12,000+ kg heavy | Must fit gun bore (bore 0.5m supports ~0.3–10,000 kg by default) |
| `RequiredTechLevel` | int | 1–3 | Projectiles tech tree level |
| `Cost.Budget` | absolute | 50–400 | Economy currency |
| `Cost.Steel` | absolute | 30–300 tons | Steel resource |
| `Cost.Exotic` | absolute | 0–80 units | Exotic materials |

**Recommended progression:**
- Light cores (5–6.5t): cheaper, better Δv efficiency, lower raw KE.
- Standard (6.5–8t): balanced.
- Heavy (8–10t): expensive, high raw KE, poor Δv efficiency.

**Safe to add to JSON?** ✅ Yes. Loader handles arbitrary arrays.

---

### 1.2 Propulsion (`PropulsionSystems[]`)
**What it does:** Provides delta-v boost during flight, increasing impact velocity and enabling Control/Dodge shot allocations.

| Field | Type | Typical Range | Notes |
|-------|------|---------------|-------|
| `Id` | string | `"none"`, `"solid_rocket"`, etc. | Unique identifier; `"none"` must exist |
| `Name` | string | `"Solid Rocket Booster"` | Display name |
| `Description` | string | Short flavor text | Player-facing |
| `DeltaVCapacityMs` | **absolute** | 0–120,000 m/s | Total velocity change available |
| `BurnDurationSeconds` | **absolute** | 1–20 seconds | Longer burn = gradual accel over distance |
| `ReferenceMassKg` | **absolute** | 5,000 kg (baseline) | Mass-efficiency pivot: eff = refMass/(refMass+projMass) |
| `RequiredTechLevel` | int | 1–3 | Propulsion unlocks at tech ≥2 typically |
| `Cost.*` | absolute | Similar to cores | Economy cost |

**Effective Δv formula:**
```
burnRateMsPerSecond = DeltaVCapacityMs / BurnDurationSeconds
burnLimitedDeltaV = min(flightTime × burnRate, DeltaVCapacityMs)
massEfficiency = ReferenceMassKg / (ReferenceMassKg + ProjectileMassKg)
effectiveDeltaV = burnLimitedDeltaV × massEfficiency
```

**Design notes:**
- Short burn + high capacity = burst systems (good for close intercepts).
- Long burn + moderate capacity = sustainer systems (flexible across ranges).
- Higher reference mass favors heavy cores.
- **Current JSON:** 20k–120k m/s Δv, 2–20 sec burn, 5,000 kg ref mass.

**Safe to add to JSON?** ✅ Yes.

---

### 1.3 Enhancements (`Enhancements[]`)
**What it does:** Optional bonuses to hit tolerance, penetration, impact coupling, and defense.

| Field | Type | Typical Range | Notes |
|-------|------|---------------|-------|
| `Id` | string | `"none"`, `"guidance"`, etc. | `"none"` must exist |
| `Name` | string | `"Guidance Package"` | Display name |
| `Description` | string | Short flavor text | Player-facing |
| `HitToleranceBonus` | **multiplier** | 0.9–2.0 | Multiplies solver hit tolerance; 2.0 = double hitbox radius |
| `Penetration` | **multiplier** | 0.9–1.25 | Divides required energy; >1.0 = easier kills |
| `ImpactCoupling` | **multiplier** | 0.96–1.0 | **Supported but not in current JSON**; also divides required energy |
| `DefenseBonus` | **absolute (0–1)** | 0.0–0.5 | Additive defense rating vs enemy intercept; 0.25 = decent, 0.5 = strong |
| `RequiredTechLevel` | int | 1–3 | Tech unlock level |
| `Cost.*` | absolute | 0–400 | Economy cost |

**Interaction notes:**
- `Penetration` and `ImpactCoupling` **multiply together** to reduce `EffectiveFractureEnergyMJ`:
  ```
  effectiveEnergy = armoredEnergy / (Penetration × ImpactCoupling)
  ```
- `HitToleranceBonus` is a **straight multiplier** on final hit tolerance (already includes difficulty + mode tuning).
- `DefenseBonus` is **additive** to projectile defense rating (0–1 scale).

**Guidance special case:**
- Enhancement `Id == "guidance"` gates whether gun's `Guidance` stat applies to evasion countering.
- Without guidance mod installed, gun guidance defaults to 1.0 regardless of upgrades.

**Current JSON examples:**
- `"guidance"`: HitToleranceBonus=2.0, no damage mods.
- `"shaped"`: Penetration=1.25 (25% damage boost).
- `"countermeasures"`: DefenseBonus=0.25, slight pen penalty.
- `"hardened"`: DefenseBonus=0.5, moderate pen penalty.

**Safe to add to JSON?** ✅ Yes. Loader supports all fields (including `ImpactCoupling` even though current JSON doesn't use it).

---

## 2. Gun Configuration & Upgrades

### 2.1 Gun Stats (runtime state, modified by upgrades)
These are stored in `GameState.Gun` and affect muzzle velocity, range, and combat outcomes.

| Stat | Type | Typical Range | Where/How It's Used |
|------|------|---------------|---------------------|
| `BarrelLength` | **absolute** | 50–200 meters | Range multiplier (ref=100m → 1.0x); also barrel efficiency for muzzle velocity |
| `BarrelIntegrity` | **fractional (0–1)** | 0.05–1.0 | Multiplies max muzzle velocity; <0.05 = barrel failed |
| `PropellantMass` (chemical) | **absolute** | 50–200 kg | Chemical energy path: `mass × energyDensity × efficiency` |
| `PropellantEnergyDensity` (chemical) | **absolute** | 5.0–10.0 GJ/kg | Capped by barrel material (Steel=5.0, Exotic=10.0) |
| `PowerCapacity` (EM) | **absolute** | 100–500 MW | Railgun/coil/hybrid energy path |
| `CapacitorEfficiency` (EM) | **fractional (0–1)** | 0.7–0.9 | Multiplies usable EM energy |
| `Guidance` / `FireControlQuality` | **multiplier** | 1.0–3.0 | Counters enemy maneuverability; only matters if guidance mod present |
| `BoreDiameter` | **absolute** | 0.3–1.0 meters | Hard constraint on projectile mass range |

**Formula snippets:**
- **Range multiplier:** `BarrelLength / 100.0` (clamped 0.5–2.0x)
- **Barrel efficiency:** `0.5 + 0.5 × min(1.0, BarrelLength/200.0)` (muzzle velocity scaling)
- **Chemical muzzle energy:** `PropellantMass × GetEffectivePropellantEnergyDensity() × 1e9 J × 0.3 efficiency`
- **EM muzzle energy:** `PowerCapacity × 1e9 J × CapacitorEfficiency × [0.5–1.0 propulsion factor]`

---

### 2.2 Gun Upgrades (`Config/WeaponsUpgrades.json`)
**What it does:** JSON-driven one-shot purchases that modify gun stats or add installed upgrades.

| Upgrade Type | Parameter Keys | Value Semantics | Notes |
|--------------|----------------|-----------------|-------|
| `BarrelRepair` | `SetIntegrityTo` | **absolute (0–1)** | Sets integrity (typically 1.0) |
| `PropellantOptimization` | `Multiplier` | **multiplier** | Scales `PropellantEnergyDensity` (still material-capped) |
| `PowerCapacitorUpgrade` | `Multiplier` | **multiplier** | Scales `PowerCapacity` |
| `BarrelExtension` | `Multiplier`, `Max` | **multiplier + cap** | Scales length; hard-capped by `Max` |
| `GuidanceCalibration` | `Multiplier`, `MinInput`, `Max` | **multiplier + bounds** | Scales `Guidance`; requires guidance mod |
| Wear upgrades | `WearMultiplier` | **multiplier** | Modifies per-shot barrel wear (0.6 = 40% reduction) |

**Upgrade gating:**
- `MinWeaponsTechLevel`, `MinProjectilesTechLevel`: tech unlock gates.
- `RequiresPropulsion`: `"Chemical"` or `"NonChemical"` (filters by gun propulsion type).
- `RequiresGuidanceMod`: requires projectile guidance enhancement installed.
- `Prerequisites`: array of upgrade IDs that must be in `Gun.InstalledUpgrades` (for chained unlocks).

**Wear modifiers (tuning-side, referenced by upgrade ID):**
Current defaults in `WeaponsTuning.Gun.WearModifiersByUpgradeId`:
- `"ReinforcedBarrel"`: 0.6 (reduces wear)
- `"HighTempCoating"`: 0.75
- `"RapidFire"`: 1.5 (increases wear)
- `"CeramicLiner"`: 0.8

**Safe to add to JSON?** ✅ Yes. Unknown `Id` = no-op (future-proof). Wear multipliers work if ID exists in tuning map.

---

## 3. Runtime Shot Resolution (`ResolvedShotStats`)

### 3.1 Fields and Their Sources
These are computed per-shot in `GameState.ResolveShotStats()` and fed to the solver.

| Field | Source(s) | Effect |
|-------|-----------|--------|
| `ProjectileMassKg` | Core mass | KE input; Δv mass-efficiency |
| `MaxLaunchVelocityMs` | Gun energy + tech base + barrel | Upper bound on player launch velocity |
| `EffectiveFractureEnergyMJ` | Enemy armor + Defense / (Penetration × ImpactCoupling) | Required destructive KE |
| `Penetration` | Enhancement field | Divides energy required (carried through for reference) |
| `AdditionalHitToleranceMultiplier` | Enhancement `HitToleranceBonus` | Multiplies hit tolerance |
| `PropulsionDeltaVCapacityMs` | Propulsion Δv capacity | Max Δv boost available |
| `PropulsionBurnDurationSeconds` | Propulsion burn time | Δv accumulation rate |
| `PropulsionReferenceMassKg` | Propulsion ref mass | Mass-efficiency pivot |
| `ProjectileDefenseRating` | Enhancement `DefenseBonus` | Reduces intercept kill chance |

---

### 3.2 Impact Coupling (tuning-side, not currently in JSON)
**Where:** `DevelopmentTuning.ProjectileDefaults` ([DevelopmentTuning.cs](Spacegun%20Simulator/Development/Shared/DevelopmentTuning.cs#L179-L217))

| Field | Default | Notes |
|-------|---------|-------|
| `ImpactCoupling` | 0.002 | Base fraction of KE that couples into destructive work |
| `ImpactCouplingReferenceMassKg` | 5000.0 | Mass scaling pivot |
| `ImpactCouplingMassExponent` | 1.0 | Mass exponent: `scale = (refMass/projMass)^exp` |
| `ImpactCouplingTechMultiplierPerWeaponsLevel` | 1.0 | Tech scaling: `scale = mult^(level-1)` |

**Formula:**
```csharp
couplingMassScale = pow(couplingReferenceMassKg / projectileMassKg, couplingMassExponent)
couplingTechScale = pow(couplingTechPerLevel, weaponsTechLevel - 1)
impactCoupling = baseImpactCoupling × couplingMassScale × couplingTechScale × enhancement.ImpactCoupling
effectiveFractureEnergyMJ = armoredFractureEnergyMJ / (penetration × impactCoupling)
```

**Enhancement ImpactCoupling multiplier:**
- Loader supports it ([ProjectilesCatalogLoader.cs](Spacegun%20Simulator/Core/ProjectilesCatalogLoader.cs#L103)).
- Current JSON doesn't use it (all default to 1.0).
- **Safe to add:** ✅ Yes, just add `"ImpactCoupling": 1.1` to an enhancement definition.

---

## 4. Per-Shot Allocation (not mods, but performance levers)

**Where:** Player-entered percentages in `EnterFiringParametersPage`, applied in `CommitFiringSolutionFlow`.

| Allocation | Effect | Formula |
|------------|--------|---------|
| **Impulse %** | Adds Δv to impact velocity (more KE) | `impactVelocity = launchVelocity + effectiveDeltaV × (impulse%/100)` |
| **Control %** | Increases hit tolerance + guidance | `hitTolMult × (1 + controlBonus)` where bonus = `0.75×(1-exp(-dvControl/2000))` |
| **Dodge %** | Increases projectile defense | `defenseRating + dodgeBonus` where bonus = `0.25×(1-exp(-dvDodge/2500))` |

**Design note:** These are runtime choices, not content-authoring hooks. But they're worth knowing because they define the "gameplay value" of high Δv propulsion systems.

---

## 5. Combat Curves (not moddable, but affect outcomes)

### 5.1 Evasion (Maneuverability vs. Guidance)
**Formula:** `CombatCurves.ComputeEvasionChance(maneuverability, guidance)`
- `maneuver^1.25 / (maneuver^1.25 + guidance^1.10 + 0.35)` (clamped 0–0.65)
- **Reference points:**
  - maneuver=1.0, guidance=1.0 → 23% evade
  - maneuver=1.0, guidance=2.5 → 12% evade
  - maneuver=0.3, guidance=1.0 → 6% evade

### 5.2 Intercept (Offense vs. Defense)
**Formula:** `CombatCurves.ComputeInterceptKillChance(offense, defense)`
- `offense^1.30 / (offense^1.30 + (defense+0.15)^1.50 + 0.35)` (clamped 0–0.65)
- **Reference points:**
  - offense=0.7, defense=0.0 → 30% kill
  - offense=0.7, defense=0.5 → 19% kill
  - offense=0.2, defense=0.0 → 11% kill

**Content design implication:** Projectile `DefenseBonus` of 0.25–0.5 is meaningful; enemy Offense/Maneuverability in 0.3–0.7 range creates interesting trade-offs.

---

## 6. Difficulty & Mode Tuning (global multipliers)

### 6.1 Difficulty Config (`GameDifficulty.cs`)
| Field | Type | Typical | Effect |
|-------|------|---------|--------|
| `HitToleranceMultiplier` | **multiplier** | 1.0–100.0 | Final hit tolerance scale (NuclearOption=100x) |
| `TargetRcsMultiplier` | **multiplier** | 1.0–10.0 | Scales RCS (CometsAndAsteroids=10x) → larger hitbox |
| `TierHitToleranceMultipliers` | **multiplier array** | per-tier | Optional monotonic difficulty enforcement |

### 6.2 Mode Tuning (`GameModeTuning.cs`)
| Field | Type | Typical | Effect |
|-------|------|---------|--------|
| `FractureEnergyDefenseScale` | **multiplier** | 0.5–2.0 | How strongly enemy Defense scales energy required |
| `HitToleranceMultiplierPure` / `Full` | **multiplier** | 0.8–1.2 | Additional mode-specific hit tolerance scaling |

**Not moddable via content JSON**, but worth knowing for balance testing.

---

## 7. Quick Reference: Multiplier vs. Absolute

### Multipliers (scale existing values)
- Enhancement `HitToleranceBonus`
- Enhancement `Penetration`
- Enhancement `ImpactCoupling`
- Upgrade `Parameters.Multiplier` (for PropellantOptimization, PowerCapacitorUpgrade, BarrelExtension, GuidanceCalibration)
- Upgrade `WearMultiplier`
- Gun `Guidance` / `FireControlQuality`
- Difficulty `HitToleranceMultiplier`, `TargetRcsMultiplier`

### Absolutes (set or add to values)
- Core `MassKg`
- Propulsion `DeltaVCapacityMs`, `BurnDurationSeconds`, `ReferenceMassKg`
- Enhancement `DefenseBonus` (0–1 scale, additive)
- Gun `BarrelLength`, `PropellantMass`, `PowerCapacity`, etc.
- Upgrade `SetIntegrityTo` (BarrelRepair)

---

## 8. Recommended Ranges for Balanced Content

### Projectile Cores
- **Light:** 5–6.5t, cheap, favors Δv systems.
- **Medium:** 6.5–8t, balanced.
- **Heavy:** 8–10t, expensive, high raw KE.
- **Ultra:** 10t+, exotic, niche (requires larger bore).

### Propulsion Systems
- **None:** 0 Δv (baseline).
- **Entry:** 10k–30k m/s, 2–5 sec burn, 10–15 kg ref.
- **Mid-tier:** 30k–60k m/s, 5–10 sec burn, 15–20 kg ref.
- **Advanced:** 80k–120k m/s, 10–20 sec burn, 20–25 kg ref.

### Enhancements
- **Hit tolerance:** 1.0 (none) → 1.5–1.75 (warhead/frag) → 2.0 (guidance).
- **Penetration:** 0.9 (frag trade-off) → 1.0 (baseline) → 1.15–1.25 (AP/shaped).
- **Defense:** 0.0 (none) → 0.15–0.25 (countermeasures) → 0.4–0.5 (hardened).

### Gun Upgrades
- **Barrel length:** 1.05–1.15x multiplier (max 200m hard cap).
- **Energy density / power:** 1.1–1.3x multiplier.
- **Guidance:** 1.1–1.2x multiplier (gated by guidance mod).
- **Wear reduction:** 0.5–0.8x (lower is better).

---

## 9. Safe JSON Expansion Checklist

✅ **Always safe to add:**
- New cores, propulsion systems, enhancements to `ProjectilesCatalog.json` (loaders handle arbitrary arrays).
- New upgrades to `WeaponsUpgrades.json` (unknown IDs = no-op; wear multipliers work if ID in tuning map).
- New cost fields (`Budget`, `Steel`, `Exotic`) — economy system handles them.

✅ **Safe to add (supported but not in current JSON):**
- Enhancement `"ImpactCoupling": 1.0` field (loader maps it, just not currently used).

⚠️ **Requires code changes:**
- New upgrade parameter keys beyond the supported cases (`BarrelRepair`, `PropellantOptimization`, etc.) — need a new `case` in `GunDevelopmentPage.ApplyUpgradeDefinition()`.
- New enhancement effects beyond `HitToleranceBonus`, `Penetration`, `ImpactCoupling`, `DefenseBonus` — need plumbing in `GameState.ResolveShotStats()`.
- Drag, guidance accuracy, or other projectile config fields that aren't consumed by the current solver.

---

## 10. Example: Designing a New Enhancement

**Goal:** "Plasma Warhead" — high damage, poor accuracy trade-off.

```json
{
  "Id": "plasma_warhead",
  "Name": "Plasma Warhead",
  "Description": "Superheated plasma core - devastating but unstable",
  "HitToleranceBonus": 0.75,
  "Penetration": 1.5,
  "ImpactCoupling": 1.2,
  "DefenseBonus": 0.0,
  "RequiredTechLevel": 3,
  "Cost": { "Budget": 350, "Steel": 150, "Exotic": 100 }
}
```

**Analysis:**
- 25% **smaller** hitbox (0.75 hit tolerance) = much harder to hit.
- 50% **more** penetration (1.5) + 20% impact coupling (1.2) = 1.8× effective damage.
- Trade-off: high skill, high reward.
- Tech 3 gate + expensive cost = late-game unlock.

---

**End of Content Authoring Guide**

For questions or edge cases, reference the source files linked throughout this document.
