namespace Spacegun_Simulator.UI
{
    public static class DifficultyText
    {
        public static string DescribeStars(int stars) => stars switch
        {
            1 => "★☆☆☆☆ Very Easy",
            2 => "★★☆☆☆ Easy",
            3 => "★★★☆☆ Moderate",
            4 => "★★★★☆ Hard",
            5 => "★★★★★ Extreme",
            _ => "Unknown"
        };
    }
}

