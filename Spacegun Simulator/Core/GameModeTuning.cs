namespace Spacegun_Simulator.Core;

/// <summary>
/// Config-driven knobs for game-mode behavior.
/// Intentionally small surface: foundation for determinism + balancing.
/// </summary>
public sealed class GameModeTuning
{
    /// <summary>
    /// Pure modes use a fixed seed by default so runs are identical.
    /// </summary>
    public int PureDeterministicSeed { get; set; } = 0;

    /// <summary>
    /// When true, disables random events for pure modes.
    /// </summary>
    public bool DisableRandomEventsInPure { get; set; } = true;

    /// <summary>
    /// Applied to radar cross-section values before detection calculations.
    /// Useful for tuning detectability per category.
    /// </summary>
    public double DetectionRcsMultiplierPure { get; set; } = 1.0;
    public double DetectionRcsMultiplierFull { get; set; } = 1.0;

    /// <summary>
    /// Applied to computed time-to-gun-range (seconds) before converting to years.
    /// </summary>
    public double TimeBudgetMultiplierPure { get; set; } = 1.0;
    public double TimeBudgetMultiplierFull { get; set; } = 1.0;

    /// <summary>
    /// Applied to hit tolerance (in addition to difficulty + projectile bonuses).
    /// </summary>
    public double HitToleranceMultiplierPure { get; set; } = 1.0;
    public double HitToleranceMultiplierFull { get; set; } = 1.0;

    public static GameModeTuning Current { get; private set; } = new();

    internal static void ApplyFromConfig(GameModeTuning? cfg)
    {
        if (cfg is null)
            return;

        Current = cfg;
    }

    public static bool IsPureMode(GameModeDefinition mode) => !mode.IsTutorial && !mode.UsesEconomyAndDevelopment;
    public static bool IsFullMode(GameModeDefinition mode) => !mode.IsTutorial && mode.UsesEconomyAndDevelopment;

    public double GetDetectionRcsMultiplier(GameModeDefinition mode)
        => IsPureMode(mode) ? DetectionRcsMultiplierPure : IsFullMode(mode) ? DetectionRcsMultiplierFull : 1.0;

    public double GetTimeBudgetMultiplier(GameModeDefinition mode)
        => IsPureMode(mode) ? TimeBudgetMultiplierPure : IsFullMode(mode) ? TimeBudgetMultiplierFull : 1.0;

    public double GetHitToleranceMultiplier(GameModeDefinition mode)
        => IsPureMode(mode) ? HitToleranceMultiplierPure : IsFullMode(mode) ? HitToleranceMultiplierFull : 1.0;
}
