using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Town.Geom;

namespace Town
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            if (args.Any() && int.TryParse(args[0], out var seed))
            {
                Rnd.Seed = seed;
            }

            var nPatches = 35;

            var town = new Town(nPatches);

            var img = new TownRenderer(town, new TownRendererOptions{RenderOverlay = false}).DrawTown();

            File.WriteAllText(@"C:\temp\town.svg", img);
        }

  
    }
}
