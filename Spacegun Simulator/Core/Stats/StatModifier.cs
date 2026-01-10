namespace Spacegun_Simulator.Core.Stats;

public enum StatModifierOp
{
    Add,
    Mul,
    Set,
    ClampMin,
    ClampMax,
}

public sealed record StatModifier(
    string Key,
    StatModifierOp Op,
    double Value);
