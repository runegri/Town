namespace Town
{
    public class TownOptions
    {
        public bool RenderOverlay { get; set; }
        public bool RenderWalls { get; set; }
        public int NumberOfPatches { get; set; }
        public int? Seed { get; set; }

        public static TownOptions Default => new TownOptions { NumberOfPatches = 35 };
    }
}