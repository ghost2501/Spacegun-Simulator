using Spacegun_Simulator.Development.Weapons;

namespace Spacegun_Simulator.Core.Stats;

public static class StatModifierApplier
{
    public static void ApplyToGameState(GameState game, IReadOnlyList<StatModifier> modifiers)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (modifiers is null || modifiers.Count == 0) return;

        foreach (var modifier in modifiers)
        {
            if (modifier is null) continue;
            if (string.IsNullOrWhiteSpace(modifier.Key)) continue;
            if (game.Gun is null) throw new InvalidOperationException("GameState.Gun is null.");

            if (TryApplyToGun(game.Gun, modifier))
                continue;

            // Projectile/propulsion/shot keys often can't be represented as direct mutations
            // on the (immutable) crafted projectile components. Persist and apply them during
            // ResolveWeaponStats instead.
            if (IsResolvableKey(modifier.Key))
                game.Gun.InstalledStatModifiers.Add(modifier);
        }
    }

    private static bool TryApplyToGun(GunConfiguration gun, StatModifier modifier)
    {
        // Supported keys are intentionally explicit so content authors can't
        // accidentally mutate arbitrary state.
        double current;
        switch (modifier.Key)
        {
            case "Gun.BarrelIntegrity":
            case "Gun.BarrelIntegrity01":
                current = gun.BarrelIntegrity;
                gun.BarrelIntegrity = Math.Clamp(ApplyOp(modifier.Op, current, modifier.Value), 0.0, 1.0);
                return true;

            case "Gun.BarrelLength":
            case "Gun.BarrelLengthM":
                current = gun.BarrelLength;
                gun.BarrelLength = Math.Max(0.0, ApplyOp(modifier.Op, current, modifier.Value));
                return true;

            case "Gun.PowerCapacity":
            case "Gun.PowerCapacityMW":
                current = gun.PowerCapacity;
                gun.PowerCapacity = Math.Max(0.0, ApplyOp(modifier.Op, current, modifier.Value));
                return true;

            case "Gun.PropellantEnergyDensity":
            case "Gun.PropellantEnergyDensityGJPerKg":
                current = gun.PropellantEnergyDensity;
                gun.PropellantEnergyDensity = Math.Max(0.0, ApplyOp(modifier.Op, current, modifier.Value));
                return true;

            case "Gun.FireControlQuality":
            case "Gun.Guidance":
                current = gun.FireControlQuality;
                gun.FireControlQuality = Math.Max(0.0, ApplyOp(modifier.Op, current, modifier.Value));
                return true;

            default:
                // Unknown keys are no-ops to allow forward-compatible content.
                return false;
        }
    }

    public static double ApplyOp(StatModifierOp op, double current, double value)
    {
        return op switch
        {
            StatModifierOp.Add => current + value,
            StatModifierOp.Mul => current * value,
            StatModifierOp.Set => value,
            StatModifierOp.ClampMin => Math.Max(value, current),
            StatModifierOp.ClampMax => Math.Min(value, current),
            _ => current,
        };
    }

    private static bool IsResolvableKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        return key.StartsWith("Projectile.", StringComparison.Ordinal)
            || key.StartsWith("Propulsion.", StringComparison.Ordinal)
            || key.StartsWith("Shot.", StringComparison.Ordinal);
    }
}
