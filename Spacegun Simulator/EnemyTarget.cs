namespace Spacegun_Simulator
{
    // ============================================================================
    // ENEMY TARGET
    // ============================================================================
    
    public class EnemyTarget
    {
        public string Name { get; set; } = string.Empty;  // Add default value
        public double Altitude { get; set; }
        public double Velocity { get; set; }
        public double CrossSection { get; set; }
        public double Evasiveness { get; set; }
        public double ArmorThickness { get; set; }
        public double ArmorQuality { get; set; }
        public double HitPoints { get; set; }
        public double MaxHitPoints { get; set; }
        
        public bool IsDestroyed => HitPoints <= 0;
        
        public void TakeDamage(double damage)
        {
            HitPoints -= damage;
            if (HitPoints < 0) HitPoints = 0;
        }
    }
}
