# Tuning Guide (Common Issues + Levers)

This is a quick reference for diagnosing tuning problems using the headless tuning lab report and for choosing the safest levers to adjust.

## How to read the tuning lab energy report

The tuning lab report (generated via the headless CLI option(s) used in `Program.cs`) produces per-tier counts and averages. The important ideas:

- **Detected**: the wave was detected.
- **CanHit**: given the chosen firing baseline + solver, the shot can hit.
- **EnergySufficient**: the projectile has enough kinetic energy (including propulsion delta-v) to destroy the target *if it hits*.
- **BallisticsOk**: both `CanHit` and `CanDestroy` are true.
- **EnergyGated**: `CanHit` is true but `CanDestroy` is false (hit-only case).

Averages (computed over detected samples):

- **AvgEffectiveFractureEnergy_MJ**: average destroy threshold the projectile must exceed.
- **AvgKineticEnergy_MJ**: average available projectile kinetic energy at the chosen firing solution velocity (+ propulsion contribution).
- **AvgKeToFractureRatio**: rough “headroom” metric; > 1 tends toward destroy-on-hit.
- **AvgBaselineVelocity_ms**: average baseline muzzle velocity used by the solver.
- **AvgTargetCrossSection_M2** and **AvgTargetRadius_m**: average target size implied by the tier’s mass/density geometry.

### First triage: which failure mode do you have?

Use these patterns to decide where to tweak:

1) **Low Detected** (detection problem)
- Symptoms: `Detected` is low across tiers, while `CanHit` is hard to interpret because many samples never enter the solver.
- Common causes: detection range too low for enemy distance/stealth, radar level too weak, stealth penetration too low.
- Fix levers: radar tuning / detection multipliers, stealth mechanics, wave distance/stealth defaults.

2) **Detected is fine, but CanHit is low** (hitability problem)
- Symptoms: `Detected` high, `CanHit` low, and `MissedButDetected` (if enabled) is large.
- Common causes: enemy kinematics/evasion too strong, fire control/guidance too weak, effective gun range too low, or the target is too small.
- Use the new columns:
  - If **AvgTargetRadius_m is shrinking** more than expected at higher tiers, you have a geometry-driven hitbox issue (tier mass/density).
  - If target size is stable but `CanHit` drops, the issue is likely *motion/evasion/solver limits*.

3) **CanHit is fine, but EnergySufficient is low** (energy gate too hard)
- Symptoms: `CanHit` high, `EnergySufficient` low, `EnergyGated` high.
- Interpretation: upgrades and late-game should often look like this if you want “hit-only” to be a real state.
- Fix levers: projectile energy (mass/velocity/delta-v), penetration/coupling, target fracture energy inputs (bulk modulus, fracture strain, density at fixed mass).

4) **BallisticsOk collapses at some tier boundary** (tier tuning discontinuity)
- Symptoms: a sudden drop at tier boundaries rather than a smooth curve.
- Common causes: tier arrays or per-tier tuning ranges are discontinuous (mass/velocity/maneuverability jumps), or a solver constraint (range, max gun velocity) is being exceeded.

## Safe levers to tweak (and what they mostly affect)

### Hitability levers (mostly `CanHit`)

- **Enemy maneuverability / acceleration / evasion**: Strongest driver of `CanHit` when targets are already detected.
- **Enemy velocity**: Higher velocity generally reduces hitability if the gun/solver is near its limits.
- **Effective gun range**: If `EffectiveMaxGunVelocity_ms` and/or `EffectiveGunRange` constraints bind, `CanHit` can drop hard.
- **Fire control quality / guidance**: Improves solution quality; when underpowered, you’ll see more “missed but detected.”
- **Target size** (cross-section/radius): Derived from mass + density geometry. If you adjust tier mass or density ranges, you are also adjusting hitability unless you compensate.

### Energy / destroy levers (mostly `EnergySufficient` and `EnergyGated`)

- **Projectile KE**: $KE = \tfrac12 m v^2$. Velocity is a quadratic lever; projectile mass is linear.
- **Propulsion delta-v**: Adds to effective closing velocity in the tuning report model; useful for stretching kill capability without raising baseline muzzle velocity.
- **Penetration / coupling**: If modeled as scaling effective fracture energy, it can be a strong “damage multiplier” without affecting hitability.

### Target durability levers (mostly `AvgEffectiveFractureEnergy_MJ`)

The fracture model used in tuning is strain-energy based. In simplified terms:

- Fracture energy increases with **bulk modulus** and the chosen **fracture strain**.
- For a fixed mass, increasing **density** reduces volume, which tends to reduce stored strain energy (but also shrinks radius and can affect hitability).

Practical takeaway:
- If you increase **bulk modulus** a lot (ultra-hard materials), you may need to rebalance either **mass**, **fracture strain**, or projectile energy to keep “sometimes destroy” in the tiers you care about.

## Recommended workflow

1) Run the tuning lab report with your current config and record the CSV.
2) Look at tiers where gameplay feels wrong; find whether the issue is primarily detection, hitability, or energy gating.
3) Make one small change targeting that axis (e.g., maneuverability for `CanHit`, projectile velocity for `EnergySufficient`).
4) Re-run the report and compare:
- `CanHit` changes should correlate with kinematics/guidance/range.
- `EnergySufficient` changes should correlate with KE/fracture-energy levers.
- Target geometry changes should show up in **AvgTargetCrossSection_M2** and **AvgTargetRadius_m**.

## Common pitfalls

- **Changing density without checking radius**: Density changes alter radius through geometry; you may unintentionally make targets much harder to hit.
- **Over-relying on KE multipliers**: Velocity boosts can push you into solver/range constraints; sometimes delta-v or penetration/coupling is the cleaner lever.
- **Interpreting `EnergyOk` without `CanHit`**: If `CanHit` is low, improving KE won’t help until hitability is addressed.
