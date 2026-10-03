using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class StatusBarsTests
{
    private const int Power = 20;
    private const int Hp = 80;

    private static readonly Resistances None = Resistances.None;

    [Fact]
    public void A_body_nothing_has_touched_has_no_status_and_is_as_it_was()
    {
        var bars = new StatusBars();
        var own = Resistances.None.With(DamageType.Void, 15);

        Assert.Equal(Statuses.None, bars.Active);
        Assert.False(bars.Lost);
        Assert.Equal(1f, bars.Speed);
        Assert.All(DamageTypes.All, type => Assert.Equal(1f, bars.Dealt(type)));
        Assert.All(DamageTypes.All, type => Assert.Equal(own.Against(type), bars.Adjusted(own).Against(type)));
        Assert.Empty(bars.Advance(StatusRules.Turn, Hp, None));
    }

    [Fact]
    public void A_slash_opens_a_wound_that_takes_a_share_of_the_hp_left_each_turn_until_it_closes()
    {
        var bars = new StatusBars();
        bars.Build(DamageType.Slash, Power, damage: 10, None);

        Assert.Equal(Power, bars.Bleed);
        Assert.Equal(Statuses.Bleeding, bars.Active);
        Assert.Empty(bars.Advance(StatusRules.Turn / 2f, Hp, None));
        var tick = Assert.Single(bars.Advance(StatusRules.Turn / 2f, Hp, None));
        Assert.Equal(new StatusTick(DamageType.Pierce, (int)MathF.Round(Hp * StatusRules.BleedOfHp)), tick);
        Assert.Equal(Power - StatusRules.BleedDecay, bars.Bleed);

        int turns = (int)MathF.Ceiling((float)bars.Bleed / StatusRules.BleedDecay);
        for (int i = 0; i < turns; i++)
        {
            bars.Advance(StatusRules.Turn, Hp, None);
        }

        Assert.Equal(Statuses.None, bars.Active);
        Assert.Empty(bars.Advance(StatusRules.Turn, Hp, None));
    }

    [Fact]
    public void A_wound_always_takes_something_and_a_body_weak_to_piercing_bleeds_more()
    {
        var weak = Resistances.None.With(DamageType.Pierce, -50);
        var bars = new StatusBars();
        bars.Build(DamageType.Slash, Power, damage: 10, None);

        Assert.Equal(1, Assert.Single(bars.Advance(StatusRules.Turn, hp: 1, None)).Damage);
        Assert.Equal((int)MathF.Round(Hp * StatusRules.BleedOfHp * weak.Taken(DamageType.Pierce)), Assert.Single(bars.Advance(StatusRules.Turn, Hp, weak)).Damage);
    }

    [Fact]
    public void Fire_burns_by_the_damage_it_did_and_the_burn_bites_for_what_is_on_the_bar()
    {
        const int Damage = 250;
        var bars = new StatusBars();
        bars.Build(DamageType.Fire, Power, Damage, None);
        int burn = Power * Damage / StatusRules.BurnPerDamage;

        Assert.Equal(burn, bars.Burn);
        Assert.Equal(Statuses.Burning, bars.Active);
        Assert.Equal(new StatusTick(DamageType.Fire, burn), Assert.Single(bars.Advance(StatusRules.Turn, Hp, None)));
        Assert.Equal(burn - Math.Max(burn / 2, StatusRules.BurnDecayLeast), bars.Burn);
    }

    [Fact]
    public void A_small_burn_is_gone_in_a_turn_and_a_body_that_resists_fire_burns_less()
    {
        var fireproof = Resistances.None.With(DamageType.Fire, 50);
        var bars = new StatusBars();
        var resisting = new StatusBars();
        bars.Build(DamageType.Fire, Power, damage: StatusRules.BurnPerDamage / 2, None);
        resisting.Build(DamageType.Fire, Power, damage: StatusRules.BurnPerDamage / 2, fireproof);

        Assert.True(bars.Burn <= StatusRules.BurnDecayLeast);
        Assert.Equal((int)(bars.Burn * fireproof.Taken(DamageType.Fire)), resisting.Burn);
        bars.Advance(StatusRules.Turn, Hp, None);
        Assert.Equal(0, bars.Burn);
    }

    [Theory]
    [InlineData(DamageType.Water)]
    [InlineData(DamageType.Ice)]
    public void Cold_puts_the_burn_out_first_and_fire_takes_the_cold_off_first(DamageType cold)
    {
        var burning = new StatusBars();
        burning.Build(DamageType.Fire, power: StatusRules.BurnPerDamage, damage: 30, None);
        int burn = burning.Burn;
        burning.Build(cold, burn - 10, damage: 1, None);

        Assert.Equal(10, burning.Burn);
        Assert.Equal(0, burning.Cold);
        burning.Build(cold, 10 + 5, damage: 1, None);
        Assert.Equal(0, burning.Burn);
        Assert.Equal(5, burning.Cold);

        var chilled = new StatusBars();
        chilled.Build(cold, StatusRules.ChilledAt, damage: 1, None);
        chilled.Build(DamageType.Fire, power: StatusRules.BurnPerDamage, damage: StatusRules.ChilledAt + 8, None);

        Assert.Equal(0, chilled.Cold);
        Assert.Equal(8, chilled.Burn);
    }

    [Fact]
    public void Water_chills_and_never_freezes_and_leaves_a_colder_bar_where_it_is()
    {
        var bars = new StatusBars();
        for (int i = 0; i < 10; i++)
        {
            bars.Build(DamageType.Water, StatusRules.BarMost, damage: 1, None);
        }

        Assert.Equal(StatusRules.ChilledAt, bars.Cold);
        Assert.Equal(Statuses.Chilled, bars.Active);
        Assert.Equal(StatusRules.ChilledSpeed, bars.Speed);
        Assert.False(bars.Lost);

        int colder = StatusRules.ChilledAt + 25;
        bars.Build(DamageType.Ice, 25, damage: 1, None);
        bars.Build(DamageType.Water, StatusRules.BarMost, damage: 1, None);
        Assert.Equal(colder, bars.Cold);
    }

    [Fact]
    public void Ice_freezes_at_a_full_bar_for_a_lost_turn_and_the_body_thaws_with_little_cold_left()
    {
        var bars = new StatusBars();
        bars.Build(DamageType.Ice, StatusRules.FrozenAt, damage: 1, None);

        Assert.True(bars.Lost);
        Assert.Equal(Statuses.Frozen, bars.Active);
        Assert.Equal(StatusRules.FrozenKeeps, bars.Cold);
        Assert.Equal(1f, bars.Speed);

        // A frozen body takes no more cold.
        bars.Build(DamageType.Ice, StatusRules.FrozenAt, damage: 1, None);
        Assert.Equal(StatusRules.FrozenKeeps, bars.Cold);

        bars.Advance(StatusRules.LostTurn / 2f, Hp, None);
        Assert.True(bars.Lost);
        bars.Advance(StatusRules.LostTurn / 2f, Hp, None);
        Assert.False(bars.Lost);
        Assert.True(bars.Cold <= StatusRules.ThawedKeepsAtMost);
        Assert.Equal(Statuses.None, bars.Active);
    }

    [Theory]
    [InlineData(DamageType.Blunt)]
    [InlineData(DamageType.Earth)]
    [InlineData(DamageType.Lightning)]
    public void A_blunt_blow_earth_and_lightning_shake_then_stun(DamageType type)
    {
        var bars = new StatusBars();
        bars.Build(type, StatusRules.DizzyAt, damage: 1, None);

        Assert.Equal(Statuses.Dizzy, bars.Active);
        Assert.All(DamageTypes.All, dealt => Assert.Equal(StatusRules.DizzyDamage, bars.Dealt(dealt)));

        bars.Build(type, StatusRules.StunnedAt - StatusRules.DizzyAt, damage: 1, None);
        Assert.True(bars.Lost);
        Assert.Equal(Statuses.Stunned, bars.Active);
        Assert.Equal(StatusRules.StunnedKeeps, bars.Stun);

        bars.Advance(StatusRules.LostTurn, Hp, None);
        Assert.False(bars.Lost);
        Assert.True(bars.Stun <= StatusRules.RecoveredKeepsAtMost);
    }

    [Fact]
    public void The_statuses_alone_tell_being_held_and_slowed_as_the_bars_do()
    {
        var bars = new StatusBars();
        void ToldAsTheBarsTellIt()
        {
            Assert.Equal(bars.Lost, bars.Active.IsLost());
            Assert.Equal(bars.Speed, bars.Active.SpeedShare());
        }

        bars.Build(DamageType.Water, StatusRules.ChilledAt, damage: 1, None);
        ToldAsTheBarsTellIt();
        Assert.Equal(StatusRules.ChilledSpeed, bars.Active.SpeedShare());

        bars.Build(DamageType.Ice, StatusRules.FrozenAt, damage: 1, None);
        ToldAsTheBarsTellIt();
        Assert.True(bars.Active.IsLost());

        bars.Advance(StatusRules.LostTurn, Hp, None);
        ToldAsTheBarsTellIt();
        Assert.False(bars.Active.IsLost());

        bars.Build(DamageType.Blunt, StatusRules.StunnedAt, damage: 1, None);
        ToldAsTheBarsTellIt();
        Assert.True(bars.Active.IsLost());

        bars.Advance(StatusRules.LostTurn, Hp, None);
        ToldAsTheBarsTellIt();
        Assert.False(bars.Active.IsLost());
    }

    [Fact]
    public void Wind_shakes_and_never_stuns()
    {
        var bars = new StatusBars();
        for (int i = 0; i < 10; i++)
        {
            bars.Build(DamageType.Wind, StatusRules.BarMost, damage: 1, None);
        }

        Assert.Equal(StatusRules.DizzyAt, bars.Stun);
        Assert.Equal(Statuses.Dizzy, bars.Active);
        Assert.False(bars.Lost);
    }

    [Fact]
    public void Every_bar_wears_down_a_turn_at_a_time()
    {
        var bars = new StatusBars();
        bars.Build(DamageType.Ice, 50, damage: 1, None);
        bars.Build(DamageType.Blunt, 50, damage: 1, None);
        bars.Build(DamageType.Void, 50, damage: 1, None);
        bars.Advance(StatusRules.Turn, Hp, None);

        Assert.Equal(50 - StatusRules.ColdDecay, bars.Cold);
        Assert.Equal(50 - StatusRules.StunDecay, bars.Stun);
        Assert.Equal(50 - StatusRules.AstralDecay, bars.Corruption);
    }

    [Fact]
    public void A_foes_divine_blow_takes_corruption_off_first_and_lights_a_body_only_so_far()
    {
        var bars = new StatusBars();
        bars.Build(DamageType.Void, 50, damage: 1, None);
        bars.Build(DamageType.Divine, 60, damage: 1, None);

        Assert.Equal(0, bars.Corruption);
        Assert.Equal(10, bars.Illumination);
        Assert.Equal(Statuses.Illuminated, bars.Active);

        bars.Build(DamageType.Divine, StatusRules.BarMost, damage: 1, None);
        Assert.Equal(StatusRules.LitByFoeAtMost, bars.Illumination);

        bars.Build(DamageType.Void, StatusRules.LitByFoeAtMost + 7, damage: 1, None);
        Assert.Equal(0, bars.Illumination);
        Assert.Equal(7, bars.Corruption);
        Assert.Equal(Statuses.Tainted, bars.Active);
    }

    [Fact]
    public void Casting_the_divine_blesses_then_exalts_the_caster()
    {
        var bars = new StatusBars();
        var own = Resistances.None;
        bars.BuildFromCasting(DamageType.Divine, StatusRules.BlessedAt);

        Assert.Equal(Statuses.Blessed, bars.Active);
        Assert.Equal(StatusRules.BlessedDivineDealt, bars.Dealt(DamageType.Divine));
        Assert.Equal(1f, bars.Dealt(DamageType.Fire));
        Assert.Equal(StatusRules.BlessedVoidResistance, bars.Adjusted(own).Against(DamageType.Void));

        bars.BuildFromCasting(DamageType.Divine, StatusRules.ExaltedAt - StatusRules.BlessedAt);
        Assert.Equal(Statuses.Exalted, bars.Active);
        Assert.Equal(StatusRules.ExaltedDivineDealt, bars.Dealt(DamageType.Divine));
        Assert.Equal(StatusRules.ExaltedVoidResistance, bars.Adjusted(own).Against(DamageType.Void));

        // A foe's blow lights no further than its cap, and takes nothing off what the caster built.
        bars.Build(DamageType.Divine, StatusRules.BarMost, damage: 1, None);
        Assert.Equal(StatusRules.ExaltedAt, bars.Illumination);
    }

    [Fact]
    public void Corruption_defiles_then_forsakes_and_opens_a_body_to_the_void()
    {
        var bars = new StatusBars();
        var own = Resistances.None;
        bars.BuildFromCasting(DamageType.Void, StatusRules.DefiledAt);

        Assert.Equal(Statuses.Defiled, bars.Active);
        Assert.Equal(StatusRules.DefiledVoidDealt, bars.Dealt(DamageType.Void));
        Assert.Equal(StatusRules.CorruptedVoidResistance, bars.Adjusted(own).Against(DamageType.Void));
        Assert.Equal(StatusRules.DefiledDivineResistance, bars.Adjusted(own).Against(DamageType.Divine));

        bars.Build(DamageType.Void, StatusRules.ForsakenAt - StatusRules.DefiledAt, damage: 1, None);
        Assert.Equal(Statuses.Forsaken, bars.Active);
        Assert.Equal(StatusRules.ForsakenVoidDealt, bars.Dealt(DamageType.Void));
        Assert.Equal(StatusRules.CorruptedVoidResistance, bars.Adjusted(own).Against(DamageType.Void));
        Assert.Equal(StatusRules.ForsakenDivineResistance, bars.Adjusted(own).Against(DamageType.Divine));
    }

    [Fact]
    public void Starting_a_skill_builds_its_divine_and_void_parts_on_the_caster_by_their_share()
    {
        StatusBars CastBy(SkillDefinition skill)
        {
            var caster = new StatusBars();
            caster.BuildFromCasting(skill);
            return caster;
        }

        var divine = CastBy(Weapons.Staff(Element.Divine).Primary);
        var voided = CastBy(Weapons.Staff(Element.Void).Primary);
        var enchanted = CastBy(Weapons.Enchanted(Weapons.Greatsword, Element.Void).Primary);

        Assert.Equal(StatusRules.CasterAstral, divine.Illumination);
        Assert.Equal(0, divine.Corruption);
        Assert.Equal(StatusRules.CasterAstral, voided.Corruption);
        Assert.Equal(0, voided.Illumination);
        Assert.Equal((int)MathF.Round(StatusRules.CasterAstral * Weapons.EnchantedShare), enchanted.Corruption);

        // Fire, steel, and the thrust a divine staff's dash carries, which is a blow and no magic.
        Assert.Equal(Statuses.None, CastBy(Weapons.Staff(Element.Fire).Primary).Active);
        Assert.Equal(Statuses.None, CastBy(Weapons.Greatsword.Primary).Active);
        Assert.Equal(Statuses.None, CastBy(Weapons.Staff(Element.Divine).Lunge).Active);
    }

    [Fact]
    public void A_weakness_builds_a_bar_faster_and_a_resistance_slower()
    {
        var weak = Resistances.None.With(DamageType.Slash, -25);
        var tough = Resistances.None.With(DamageType.Slash, 25);
        var soft = new StatusBars();
        var hard = new StatusBars();
        soft.Build(DamageType.Slash, Power, damage: 1, weak);
        hard.Build(DamageType.Slash, Power, damage: 1, tough);

        Assert.Equal((int)(Power * weak.Taken(DamageType.Slash)), soft.Bleed);
        Assert.Equal((int)(Power * tough.Taken(DamageType.Slash)), hard.Bleed);
        Assert.True(soft.Bleed > Power && hard.Bleed < Power);
    }

    [Theory]
    [InlineData(DamageType.Pierce)]
    [InlineData(DamageType.Arcane)]
    public void Piercing_and_the_arcane_build_nothing(DamageType type)
    {
        var bars = new StatusBars();
        bars.Build(type, StatusRules.BarMost, damage: 100, None);

        Assert.Equal(Statuses.None, bars.Active);
    }

    [Fact]
    public void Nothing_is_left_on_a_body_that_is_cleared()
    {
        var bars = new StatusBars();
        bars.Build(DamageType.Ice, StatusRules.FrozenAt, damage: 1, None);
        bars.Build(DamageType.Slash, Power, damage: 1, None);
        bars.Build(DamageType.Fire, Power, damage: 400, None);
        bars.Clear();

        Assert.Equal(Statuses.None, bars.Active);
        Assert.False(bars.Lost);
        Assert.Empty(bars.Advance(StatusRules.Turn, Hp, None));
    }

    [Fact]
    public void What_resists_piercing_resists_it_half_as_well_and_a_weakness_to_it_is_whole()
    {
        var armoured = Resistances.None.With(DamageType.Pierce, 40).With(DamageType.Slash, 40);
        var weak = Resistances.None.With(DamageType.Pierce, -40);

        Assert.Equal(1f - 40 / 2 / 100f, armoured.Taken(DamageType.Pierce), precision: 5);
        Assert.Equal(1f - 40 / 100f, armoured.Taken(DamageType.Slash), precision: 5);
        Assert.Equal(1f + 40 / 100f, weak.Taken(DamageType.Pierce), precision: 5);
    }

    [Fact]
    public void A_dizzy_hand_hits_softer_and_a_blessed_one_harder_with_the_divine()
    {
        var hands = new CharacterStats(Str: 10, Wis: 10, Agi: 0);
        var slice = Weapons.Greatsword.Primary;
        var bolt = Weapons.Staff(Element.Divine).Primary;
        var dizzy = new StatusBars();
        dizzy.Build(DamageType.Blunt, StatusRules.DizzyAt, damage: 1, None);
        var blessed = new StatusBars();
        blessed.BuildFromCasting(DamageType.Divine, StatusRules.BlessedAt);

        Assert.Equal(StatRules.Damage(slice, hands), StatRules.Damage(slice, hands, None, new StatusBars()));
        Assert.Equal((int)MathF.Round(slice.Damage * (1f + hands.Str * StatRules.DamagePerStr) * StatusRules.DizzyDamage), StatRules.Damage(slice, hands, None, dizzy));
        Assert.Equal((int)MathF.Round(bolt.Damage * (1f + hands.Wis * StatRules.DamagePerWis) * StatusRules.BlessedDivineDealt), StatRules.Damage(bolt, hands, None, blessed));
        Assert.Equal(StatRules.Damage(slice, hands), StatRules.Damage(slice, hands, None, blessed));
    }

    [Fact]
    public void The_parts_of_a_skills_damage_add_up_to_what_it_deals()
    {
        var hands = new CharacterStats(Str: 12, Wis: 9, Agi: 0);
        foreach (var weapon in Weapons.All.Concat(Weapons.Arms.Select(w => Weapons.Enchanted(w, Element.Void))))
        {
            // To the half point a whole number is rounded from: the two sums are worked out in different orders.
            Assert.All(weapon.Skills, skill =>
                Assert.InRange(StatRules.DamageByType(skill, hands).Sum(part => part.Amount), StatRules.Damage(skill, hands) - 0.51f, StatRules.Damage(skill, hands) + 0.51f));
        }
    }

    [Fact]
    public void A_blow_builds_the_bar_of_each_of_its_parts_by_that_parts_share_of_the_buildup()
    {
        // WIS enough for the fire part of a slice to leave a burn.
        var hands = new CharacterStats(Str: 10, Wis: 30, Agi: 0);
        var plain = Weapons.Greatsword.Primary;
        var enchanted = Weapons.Enchanted(Weapons.Greatsword, Element.Fire).Primary;
        float share = Weapons.EnchantedShare;
        var cut = new StatusBars();
        var burnt = new StatusBars();
        StatRules.Afflict(cut, plain, hands, None);
        StatRules.Afflict(burnt, enchanted, hands, None);

        Assert.Equal(DamageType.Slash, plain.Type);
        Assert.Equal(plain.Buildup, cut.Bleed);
        Assert.Equal(Statuses.Bleeding, cut.Active);

        int fireDealt = (int)MathF.Round(StatRules.DamageByType(enchanted, hands).Single(part => part.Type == DamageType.Fire).Amount);
        Assert.Equal((int)MathF.Round(enchanted.Buildup * (1f - share)), burnt.Bleed);
        Assert.Equal((int)MathF.Round(enchanted.Buildup * share) * fireDealt / StatusRules.BurnPerDamage, burnt.Burn);
        Assert.Equal(Statuses.Bleeding | Statuses.Burning, burnt.Active);
    }

    [Fact]
    public void Every_skill_of_a_weapon_builds_something_and_a_monsters_blow_nothing_yet()
    {
        Assert.All(Weapons.All.SelectMany(w => w.Skills), skill => Assert.True(skill.Buildup > 0, skill.Id));
        Assert.All(Enemies.All, enemy => Assert.Equal(0, enemy.Attack.Buildup));
    }
}
