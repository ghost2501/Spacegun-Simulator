using System;

namespace Spacegun_Simulator
{
    public static class TupleExtensions
    {
        // For integer ranges
        public static int MinValue(this (int Min, int Max) t) => Math.Min(t.Min, t.Max);
        public static int MaxValue(this (int Min, int Max) t) => Math.Max(t.Min, t.Max);

        // For double ranges
        public static double MinValue(this (double Min, double Max) t) => Math.Min(t.Min, t.Max);
        public static double MaxValue(this (double Min, double Max) t) => Math.Max(t.Min, t.Max);
    }
}