using CommandLine;

namespace Town
{
    public class Options
    {
        [Option('o', DefaultValue = false, Required = false)]
        public bool Overlay { get; set; }

        [Option('w', DefaultValue = false, Required = false)]
        public bool Walls { get; set; }

        [Option('p', DefaultValue = 35, Required = false)]
        public int Patches { get; set; }

        [Option('s', DefaultValue = null, Required = false)]
        public int? Seed { get; set; }

        [Option('t', DefaultValue = false, Required = false)]
        public bool Water { get; set; }

    }
}