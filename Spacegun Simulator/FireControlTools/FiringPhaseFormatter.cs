using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.FireControlTools
{
    /// <summary>
    /// FIRING PHASE OUTPUT FORMATTER
    /// 
    /// Thin wrapper that delegates to DifficultyConfig for all precision decisions.
    /// DifficultyConfig is the SINGLE SOURCE OF TRUTH for precision.
    /// 
    /// This class provides static convenience methods for legacy code compatibility.
    /// New code should use DifficultyConfig directly when possible.
    /// </summary>
    public static class FiringPhaseFormatter
    {
        /// <summary>
        /// Get the DifficultyConfig for formatting operations.
        /// </summary>
        private static DifficultyConfig GetConfig(GameDifficulty difficulty) =>
            DifficultyConfig.GetConfig(difficulty);

        /// <summary>
        /// Get formatting precision for a given difficulty (legacy compatibility).
        /// Returns the distance precision decimal places as a general default.
        /// </summary>
        public static int GetPrecisionForDifficulty(GameDifficulty difficulty) =>
            GetConfig(difficulty).DistancePrecision.DecimalPlaces;

        /// <summary>
        /// Format a coordinate value with appropriate precision.
        /// </summary>
        public static string FormatCoordinate(double value, GameDifficulty difficulty) =>
            GetConfig(difficulty).DistancePrecision.Format(value);

        /// <summary>
        /// Format a velocity value with appropriate precision (m/s).
        /// </summary>
        public static string FormatVelocity(double value, GameDifficulty difficulty) =>
            GetConfig(difficulty).VelocityPrecision.Format(value);

        /// <summary>
        /// Format a mass value with appropriate precision (kg).
        /// </summary>
        public static string FormatMass(double value, GameDifficulty difficulty) =>
            GetConfig(difficulty).MassPrecision.Format(value);

        /// <summary>
        /// Format an energy value with appropriate precision (MJ).
        /// </summary>
        public static string FormatEnergy(double value, GameDifficulty difficulty)
        {
            var config = GetConfig(difficulty);
            if (value >= 1_000_000)
                return $"{config.EnergyPrecision.Format(value / 1_000_000)} PJ";
            return $"{config.EnergyPrecision.Format(value)} MJ";
        }

        /// <summary>
        /// Format a Cartesian vector with appropriate precision.
        /// </summary>
        public static string FormatVector3(Vector3 vector, GameDifficulty difficulty) =>
            GetConfig(difficulty).FormatVector3(vector);

        /// <summary>
        /// Format a distance value in square meters (RCS).
        /// </summary>
        public static string FormatRadarCrossSection(double value, GameDifficulty difficulty) =>
            GetConfig(difficulty).DistancePrecision.Format(value);

        /// <summary>
        /// Format an elevation angle in degrees.
        /// </summary>
        public static string FormatAngle(double degrees, GameDifficulty difficulty) =>
            GetConfig(difficulty).ElevationPrecision.Format(degrees);

        /// <summary>
        /// Format a launch delay time in seconds.
        /// </summary>
        public static string FormatLaunchDelay(double seconds, GameDifficulty difficulty) =>
            GetConfig(difficulty).LaunchDelayPrecision.Format(seconds);

        /// <summary>
        /// Format an azimuth bearing in degrees.
        /// </summary>
        public static string FormatAzimuth(double degrees, GameDifficulty difficulty) =>
            GetConfig(difficulty).AzimuthPrecision.Format(degrees);
    }
}