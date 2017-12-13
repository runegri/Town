using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLine;

namespace Town
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var options = new Options();
            var valid = Parser.Default.ParseArgumentsStrict(args, options);

            if (!valid)
            {
                throw new ArgumentException("Invalid application arguments");
            }

            var townOptions = new TownOptions
            {
                Overlay = options.Overlay,
                Patches = options.Patches,
                Walls = options.Walls,
                Water = options.Water,
                Seed = options.Seed ?? new Random().Next()
            };

            Rnd.Seed = townOptions.Seed.Value;

            var town = new Town(townOptions);

            var img = new TownRenderer(town, townOptions).DrawTown();

            File.WriteAllText(@"C:\temp\town.svg", img);
        }

  
    }
}
