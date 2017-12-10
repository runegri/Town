using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Town
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var options = new TownOptions
            {
                RenderOverlay = false,
                RenderWalls = true,
                NumberOfPatches = 35
            };

            if (args.Any() && int.TryParse(args[0], out var seed))
            {
                options.Seed = seed;
            }
            else
            {
                options.Seed = new Random().Next();
            }

            Rnd.Seed = options.Seed.Value;

            var town = new Town(options);

            var img = new TownRenderer(town, options).DrawTown();

            File.WriteAllText(@"C:\temp\town.svg", img);
        }

  
    }
}
