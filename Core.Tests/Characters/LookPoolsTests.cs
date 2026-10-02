using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EverdawnKit.Characters;
using WarriorsOfEverdawn.Core.Characters;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Characters;

// The pools against the kit's files as they are mounted in the game (GodotClient/kit). The randomizer's own rules
// are tested in the kit.
public class LookPoolsTests
{
    private const int Rolled = 100;

    private static readonly string Kit = Path.Combine(RepoRoot(), "GodotClient", "kit");

    private static readonly PartsCatalog Catalog = PartsCatalog.Parse(File.ReadAllText(Path.Combine(Kit, "config", "parts_catalog.json")), "parts_catalog.json");

    [Fact]
    public void EveryPoolNamesCharactersOfTheCatalogueAndPalettesThatAreThere()
    {
        Assert.All(LookPools.All, pool =>
        {
            Assert.All(pool.Origins, origin => Assert.Contains(origin, Catalog.Origins));
            Assert.All(pool.Palettes, palette => Assert.True(File.Exists(Path.Combine(Kit, "textures", "characters", palette + ".png")), palette));
        });
        Assert.Equal(LookPools.All.Count, LookPools.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void EveryPoolGivesFiguresOfItsOwnCharactersAndManyDifferentOnes()
    {
        Assert.All(LookPools.All, pool =>
        {
            var looks = Enumerable.Range(0, Rolled).Select(seed => LookRandomizer.Roll(Catalog, pool, seed)).ToList();

            Assert.All(looks, look => Assert.All(look.Parts().SelectMany(p => Catalog.Pieces(p.Part)), part => Assert.Contains(part.Origin, pool.Origins)));
            Assert.True(looks.Select(l => l.Key).Distinct().Count() > Rolled / 2, pool.Name);
        });
    }

    [Fact]
    public void APoolIsFoundByItsNameAndAnUnknownOneRefused()
    {
        Assert.All(LookPools.All, pool => Assert.Same(pool, LookPools.ByName(pool.Name)));
        Assert.Throws<KeyNotFoundException>(() => LookPools.ByName("nobody"));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WarriorsOfEverdawn.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No WarriorsOfEverdawn.slnx above {AppContext.BaseDirectory}");
    }
}
