using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Level;

public class BreakablesTests
{
    [Fact]
    public void A_crate_takes_two_blows_of_the_knights_greatsword_or_one_of_its_fire_bolts()
    {
        int slice = StatRules.Damage(Skills.Slice, PlayerRules.KnightStats);
        int bolt = StatRules.Damage(Weapons.Staff(Element.Fire).Primary, PlayerRules.KnightStats);

        Assert.True(slice < Breakables.Hp);
        Assert.True(2 * slice >= Breakables.Hp);
        Assert.True(bolt >= Breakables.Hp);
    }
}
