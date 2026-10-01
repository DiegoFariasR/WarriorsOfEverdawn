using System;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class PickupsTests
{
    private const float Facing = 0.6f;

    private static readonly Vector2 Here = new(3f, -2f);

    private static Vector2 Ahead(float distance) => Here + Ground.Forward(Facing) * distance;

    private static Vector2 Behind(float distance) => Here - Ground.Forward(Facing) * distance;

    [Fact]
    public void Of_two_weapons_in_reach_the_one_faced_is_chosen_even_if_the_other_is_nearer()
    {
        var places = new[] { Behind(Pickups.Reach * 0.2f), Ahead(Pickups.Reach * 0.7f) };

        Assert.Equal(1, Pickups.Focused(places, Here, Facing, Pickups.Reach));
        Assert.Equal(0, Pickups.Focused(places, Here, Facing + MathF.PI, Pickups.Reach));
    }

    [Fact]
    public void Two_weapons_lying_together_are_told_apart_by_standing_in_front_of_one()
    {
        var mine = Ahead(Pickups.InFront);
        var theirs = mine + new Vector2(0.1f, 0.05f);

        Assert.Equal(0, Pickups.Focused(new[] { mine, theirs }, Here, Facing, Pickups.Reach));
        Assert.Equal(1, Pickups.Focused(new[] { theirs, mine }, Here, Facing, Pickups.Reach));
    }

    [Fact]
    public void A_weapon_out_of_reach_is_not_chosen_however_squarely_it_is_faced()
    {
        var places = new[] { Ahead(Pickups.Reach * 1.01f), Behind(Pickups.Reach * 2f) };

        Assert.Equal(-1, Pickups.Focused(places, Here, Facing, Pickups.Reach));
        Assert.Equal(-1, Pickups.Focused(Array.Empty<Vector2>(), Here, Facing, Pickups.Reach));
    }

    [Fact]
    public void A_weapon_in_reach_behind_is_still_chosen_when_it_is_the_only_one()
    {
        Assert.Equal(0, Pickups.Focused(new[] { Behind(Pickups.Reach * 0.9f) }, Here, Facing, Pickups.Reach));
    }

    [Fact]
    public void A_dropped_weapon_lands_on_the_spot_it_is_picked_up_from_within_reach_and_label_range()
    {
        Assert.Equal(Ahead(Pickups.InFront), Pickups.SpotInFrontOf(Here, Facing));
        Assert.True(Pickups.InFront < Pickups.Reach);
        Assert.True(Pickups.Reach < Pickups.LabelRange);
    }
}
