namespace Spacegun_Simulator.Core;

/// <summary>
/// High-level scenario selection that can change which systems run (economy/dev loop vs pure detection+fire).
/// This intentionally composes the existing <see cref="GameDifficulty"/> config (precision/tolerance) to keep
/// changes minimal and save-compatible.
/// </summary>
public enum GameModeId
{
    Tutorial_PotatoCannonsAndBeachballs = 0,

    Economy_NuclearTorpedosVsSpaceships = 1,
    Economy_KineticDronesVsRobotAsteroids = 2,
    Economy_SmartBulletsVsLivingProjectiles = 3,

    Pure_NuclearMissile = 4,
    Pure_ShootingAsteroidsWithSpaceBullets = 5,
    Pure_SpaceBulletsVsSpaceBullets = 6,
}

public sealed record GameModeDefinition(
    GameModeId Id,
    string DisplayName,
    string NarrativeDescription,
    GameDifficulty Difficulty,
    bool UsesEconomyAndDevelopment,
    bool IsTutorial
);

public static class GameModeCatalog
{
    private static readonly IReadOnlyList<GameModeDefinition> s_modes = new List<GameModeDefinition>
    {
        new(
            GameModeId.Tutorial_PotatoCannonsAndBeachballs,
            "Tutorial — Potato Cannons and Beachballs",
            "• Learn the interface and tools\n• Skips economy/development",
            GameDifficulty.PotatoCannonsAndBeachballs,
            UsesEconomyAndDevelopment: false,
            IsTutorial: true),

        // Economy + development modes (future deep systems; currently shares the same underlying difficulty tuning)
        new(
            GameModeId.Economy_NuclearTorpedosVsSpaceships,
            "Nuclear Torpedos Vs Spaceships (easy)",
            "• Economy + development enabled (future)\n• Forgiving tolerance",
            GameDifficulty.NuclearOption,
            UsesEconomyAndDevelopment: true,
            IsTutorial: false),

        new(
            GameModeId.Economy_KineticDronesVsRobotAsteroids,
            "Kinetic Drones Vs Robot Asteroids (hard)",
            "• Economy + development enabled (future)\n• Moderate tolerance",
            GameDifficulty.CometsAndAsteroids,
            UsesEconomyAndDevelopment: true,
            IsTutorial: false),

        new(
            GameModeId.Economy_SmartBulletsVsLivingProjectiles,
            "Smart Bullets Vs Living Projectiles (extreme)",
            "• Economy + development enabled (future)\n• Tight tolerance",
            GameDifficulty.RealSpacegunSimulator,
            UsesEconomyAndDevelopment: true,
            IsTutorial: false),

        // Pure deterministic modes (no economy/dev loop)
        new(
            GameModeId.Pure_NuclearMissile,
            "Nuclear Missile (easy — pure)",
            "• Pure detection + fire\n• No resources or upgrades",
            GameDifficulty.NuclearOption,
            UsesEconomyAndDevelopment: false,
            IsTutorial: false),

        new(
            GameModeId.Pure_ShootingAsteroidsWithSpaceBullets,
            "Shooting Asteroids with space bullets (hard — pure)",
            "• Pure detection + fire\n• No resources or upgrades",
            GameDifficulty.CometsAndAsteroids,
            UsesEconomyAndDevelopment: false,
            IsTutorial: false),

        new(
            GameModeId.Pure_SpaceBulletsVsSpaceBullets,
            "Space bullets Vs Space bullets (extreme — pure)",
            "• Pure detection + fire\n• No resources or upgrades",
            GameDifficulty.RealSpacegunSimulator,
            UsesEconomyAndDevelopment: false,
            IsTutorial: false),
    };

    public static IReadOnlyList<GameModeDefinition> GetAll() => s_modes;

    public static GameModeDefinition Get(GameModeId id)
        => s_modes.First(m => m.Id == id);

    /// <summary>
    /// Returns a compact literal label for dev-facing outputs (CSV, diagnostics), e.g.
    /// "Full-Easy", "Pure-Hard", "Full-Extreme". Tutorial modes return "Tutorial".
    /// </summary>
    public static string GetDifficultyLabel(GameModeDefinition mode)
    {
        if (mode.IsTutorial)
            return "Tutorial";

        string category = mode.UsesEconomyAndDevelopment ? "Full" : "Pure";
        string level = mode.Difficulty switch
        {
            GameDifficulty.NuclearOption => "Easy",
            GameDifficulty.CometsAndAsteroids => "Hard",
            GameDifficulty.RealSpacegunSimulator => "Extreme",
            _ => mode.Difficulty.ToString()
        };

        return $"{category}-{level}";
    }

    public static GameModeId GetDefaultForDifficulty(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.PotatoCannonsAndBeachballs => GameModeId.Tutorial_PotatoCannonsAndBeachballs,
        GameDifficulty.NuclearOption => GameModeId.Economy_NuclearTorpedosVsSpaceships,
        GameDifficulty.CometsAndAsteroids => GameModeId.Economy_KineticDronesVsRobotAsteroids,
        GameDifficulty.RealSpacegunSimulator => GameModeId.Economy_SmartBulletsVsLivingProjectiles,
        _ => GameModeId.Economy_KineticDronesVsRobotAsteroids
    };
}
