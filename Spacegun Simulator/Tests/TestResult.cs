namespace Spacegun_Simulator.Tests
{
    /// <summary>
    /// Records the outcome of a single firing solution test.
    /// Used for batch validation and precision challenge tracking.
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Test scenario identifier.
        /// </summary>
        public string ScenarioName { get; set; } = string.Empty;

        /// <summary>
        /// Input parameters tested.
        /// </summary>
        public TestParameters InputParameters { get; set; } = new();

        /// <summary>
        /// Expected outcome (for validation tests).
        /// </summary>
        public TestExpectation ExpectedOutcome { get; set; } = new();

        /// <summary>
        /// Actual outcome from firing solution.
        /// </summary>
        public TestOutcome ActualOutcome { get; set; } = new();

        /// <summary>
        /// Did this test pass all criteria?
        /// </summary>
        public bool Passed
        {
            get
            {
                // Check deviation is within tolerance
                bool deviationOk = ActualOutcome.Deviation <= ExpectedOutcome.MaxDeviation;

                // Check energy requirement (if required, must have it; if not required, always ok)
                bool energyOk = !ExpectedOutcome.RequiresSufficientEnergy || ActualOutcome.HasSufficientEnergy;

                return deviationOk && energyOk;
            }
        }

        /// <summary>
        /// Number of iterations to reach solution (for precision challenges).
        /// </summary>
        public int? IterationsToSolution { get; set; }

        /// <summary>
        /// Timestamp of test run.
        /// </summary>
        public DateTime TestRunTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Detailed error message if test failed.
        /// </summary>
        public string? FailureReason { get; set; }

        public override string ToString()
        {
            string status = Passed ? "✓ PASS" : "✗ FAIL";
            string iteration = IterationsToSolution.HasValue ? $" ({IterationsToSolution} iter)" : "";
            return $"{status} | {ScenarioName:,-20} | Deviation: {ActualOutcome.Deviation:F1}m{iteration}";
        }
    }

    /// <summary>
    /// Input parameters for a firing solution test.
    /// </summary>
    public class TestParameters
    {
        public float LaunchDelaySeconds { get; set; }
        public float ElevationDegrees { get; set; }
        public float AzimuthDegrees { get; set; }
        public float VelocityMs { get; set; }
        public Vector3 TargetPosition { get; set; }
        public Vector3 TargetVelocity { get; set; }
        public float ProjectileMassKg { get; set; }
        public float TargetFractureEnergyMJ { get; set; }
        public double TargetMassTons { get; set; }

        public override string ToString()
        {
            return $"Delay:{LaunchDelaySeconds:F2}s | Elev:{ElevationDegrees:F1}° | Azim:{AzimuthDegrees:F1}° | Vel:{VelocityMs:F0}m/s";
        }
    }

    /// <summary>
    /// Expected outcome criteria for validation.
    /// </summary>
    public class TestExpectation
    {
        /// <summary>
        /// Maximum deviation in meters for a "hit".
        /// </summary>
        public float MaxDeviation { get; set; } = 14.65f;  // 0.5 × RCS for standard target

        /// <summary>
        /// Must solution have sufficient energy to destroy?
        /// </summary>
        public bool RequiresSufficientEnergy { get; set; } = true;

        /// <summary>
        /// Optional: minimum iterations expected for difficulty estimate.
        /// </summary>
        public int? MinExpectedIterations { get; set; }

        /// <summary>
        /// Optional: maximum iterations before test times out.
        /// </summary>
        public int? MaxExpectedIterations { get; set; }
    }

    /// <summary>
    /// Actual outcome from firing solution calculation.
    /// </summary>
    public class TestOutcome
    {
        /// <summary>
        /// Deviation from target at closest approach (meters).
        /// </summary>
        public float Deviation { get; set; }

        /// <summary>
        /// Does projectile have sufficient kinetic energy?
        /// </summary>
        public bool HasSufficientEnergy { get; set; }

        /// <summary>
        /// Intercept point where closest approach occurs.
        /// </summary>
        public Vector3? InterceptPoint { get; set; }

        /// <summary>
        /// Time of closest approach.
        /// </summary>
        public float? TimeOfClosestApproach { get; set; }

        /// <summary>
        /// Raw calculation data for debugging.
        /// </summary>
        public string? DebugInfo { get; set; }
    }
}