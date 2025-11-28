namespace SpaceGunSimulator
{
    public class ResourcePool
    {
        public double Budget { get; set; }
        public double Steel { get; set; }
        public double ExoticMaterials { get; set; }
        public double PowerCapacity { get; set; }
        public double ResearchPoints { get; set; }

        public ResourcePool()
        {
            Budget = 1000;
            Steel = 500;
            ExoticMaterials = 50;
            PowerCapacity = 100;
            ResearchPoints = 10;
        }
    }
}