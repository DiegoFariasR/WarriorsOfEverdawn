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

    private static readonly string Kit = Repo.PathTo("GodotClient", "kit");

    private static readonly PartsCatalog Catalog = PartsCatalog.Parse(File.ReadAllText(Path.Combine(Kit, "config", "parts_catalog.json")), "parts_catalog.json");

    private static readonly PartTags Tags = PartTags.Parse(File.ReadAllText(Path.Combine(Kit, "config", "part_tags.json")), "part_tags.json", Catalog);

    [Fact]
    public void EveryPoolNamesTagsOfTheKitAndThePoolsHaveNamesOfTheirOwn()
    {
        Assert.Empty(Tags.Warnings);
        Assert.All(LookPools.All, pool => Assert.All(pool.Tags.Concat(pool.Without), tag => Tags.KindOf(tag)));
        Assert.Equal(LookPools.All.Count, LookPools.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void EveryPoolGivesFiguresOfItsOwnTagsInColoursOfThemAndManyDifferentOnes()
    {
        Assert.All(LookPools.All, pool =>
        {
            var looks = Enumerable.Range(0, Rolled).Select(seed => LookRandomizer.Roll(Catalog, Tags, pool, seed)).ToList();

            Assert.All(looks, look =>
            {
                Assert.All(look.Parts().SelectMany(p => Catalog.Pieces(p.Part)), part => Assert.True(Tags.Fits(part, pool), part.Stem));
                Assert.True(look.Palette != null && Tags.FitsPalette(look.Palette, pool), look.Palette);
            });
            Assert.True(looks.Select(l => l.Key).Distinct().Count() > Rolled / 2, pool.Name);
        });
    }

    [Fact]
    public void APoolIsFoundByItsNameAndAnUnknownOneRefused()
    {
        Assert.All(LookPools.All, pool => Assert.Same(pool, LookPools.ByName(pool.Name)));
        Assert.Throws<KeyNotFoundException>(() => LookPools.ByName("nobody"));
    }
}
