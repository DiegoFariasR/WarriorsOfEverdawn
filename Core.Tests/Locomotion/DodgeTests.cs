using System;
using System.Numerics;
using WarriorsOfEverdawn.Core.Locomotion;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Locomotion;

public class DodgeTests
{
    private const float Precision = 1e-4f;
    private const float Now = 10f;
    private const float Recharge = DodgeRules.RechargeTime;

    [Fact]
    public void Starts_with_every_charge()
    {
        Assert.Equal(DodgeRules.Charges, new ChargeCounter(DodgeRules.Charges, Recharge).Available(Now));
    }

    [Fact]
    public void Charges_run_out_and_come_back_one_at_a_time()
    {
        var charges = new ChargeCounter(2, Recharge);

        Assert.True(charges.TryUse(Now));
        Assert.True(charges.TryUse(Now));
        Assert.False(charges.TryUse(Now));
        Assert.Equal(0, charges.Available(Now));
        Assert.Equal(Recharge, charges.NextChargeIn(Now), Precision);

        Assert.Equal(0, charges.Available(Now + Recharge * 0.99f));
        Assert.Equal(1, charges.Available(Now + Recharge));
        Assert.Equal(Recharge, charges.NextChargeIn(Now + Recharge), Precision);
        Assert.Equal(2, charges.Available(Now + Recharge * 2f));
        Assert.Equal(0f, charges.NextChargeIn(Now + Recharge * 2f));
    }

    [Fact]
    public void A_charge_spent_while_recharging_queues_behind_the_one_already_coming()
    {
        var charges = new ChargeCounter(2, Recharge);
        charges.TryUse(Now);

        Assert.True(charges.TryUse(Now + Recharge * 0.5f));
        Assert.Equal(Recharge * 0.5f, charges.NextChargeIn(Now + Recharge * 0.5f), Precision);
        Assert.Equal(1, charges.Available(Now + Recharge));
        Assert.Equal(2, charges.Available(Now + Recharge * 2f));
    }

    [Fact]
    public void Goes_where_the_input_points_or_else_where_the_character_faces()
    {
        const float Facing = 0.7f;
        var input = new Vector2(3f, 4f);

        Assert.Equal(Vector2.Normalize(input), DodgeRules.Direction(input, Facing));
        var still = DodgeRules.Direction(Vector2.Zero, Facing);
        Assert.Equal(Ground.Forward(Facing).X, still.X, Precision);
        Assert.Equal(Ground.Forward(Facing).Y, still.Y, Precision);
    }

    [Fact]
    public void Covers_its_fixed_distance_at_its_speed()
    {
        Assert.Equal(DodgeRules.Distance, DodgeRules.Speed * DodgeRules.Duration, Precision);
    }

    [Fact]
    public void Rejects_a_counter_with_no_charges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeCounter(0, Recharge));
    }
}
