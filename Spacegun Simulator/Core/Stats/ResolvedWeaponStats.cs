using Spacegun_Simulator.Ballistics;

namespace Spacegun_Simulator.Core.Stats
{
    public readonly record struct ResolvedWeaponStats(
        ResolvedGunStats Gun,
        ResolvedProjectileStats Projectile,
        ResolvedShotStats Shot
    );
}
