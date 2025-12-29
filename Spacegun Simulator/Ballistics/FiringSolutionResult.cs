namespace Spacegun_Simulator.Ballistics
{
    public class FiringSolutionResult
    {
        public bool CanDestroy { get; set; }
        public bool CanHit { get; set; }
        public bool SolutionValid { get; set; }
        public Vector3? EnemyInterceptPoint { get; set; }
        public float LaunchDelayTime { get; set; }
        public float TargetElevation { get; set; }
        public float TargetAzimuth { get; set; }
        public float MinVelocityRequired { get; set; }
        public float MaxVelocityAvailable { get; set; }
        public float ProjectileVelocity { get; set; }
        public double KineticEnergyMJ { get; set; }
        public double FractureEnergyRequired { get; set; }
        public float InterceptDeviation { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
