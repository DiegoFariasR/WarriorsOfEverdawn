using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Core.Trade;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Trade;

public class TradeTests
{
    private static readonly TradeItem Thing = new("thing", "Thing", new Cost(Gold: 30), Weapons.Scythe);

    private static readonly Buyer Anyone = new(WeaponSets.Default, ArmourTier: 0);

    private static Purse PurseWith(int gold = 0, int souls = 0, int orbs = 0)
    {
        var purse = new Purse();
        purse.EarnGold(gold);
        purse.EarnSouls(souls);
        purse.EarnOrbs(orbs);
        return purse;
    }

    [Fact]
    public void A_purchase_in_reach_with_the_gold_for_it_goes_through_and_takes_the_cost()
    {
        var purse = PurseWith(gold: Thing.Cost.Gold + 5);

        Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, Thing, TradeRules.Reach, down: false));
        Assert.Equal(5, purse.Gold);
    }

    [Fact]
    public void A_purse_one_coin_short_buys_nothing_and_keeps_its_gold()
    {
        var purse = PurseWith(gold: Thing.Cost.Gold - 1);

        Assert.False(purse.CanPay(Thing.Cost));
        Assert.Equal(new Cost(Gold: 1), purse.ShortOf(Thing.Cost));
        Assert.Equal(BuyOutcome.CannotAfford, TradeRules.Buy(purse, Thing, 0f, down: false));
        Assert.Equal(Thing.Cost.Gold - 1, purse.Gold);
    }

    [Fact]
    public void A_purse_with_exactly_the_cost_can_pay_and_is_short_of_nothing()
    {
        var purse = PurseWith(gold: Thing.Cost.Gold);

        Assert.True(purse.CanPay(Thing.Cost));
        Assert.True(purse.ShortOf(Thing.Cost).IsNothing);
    }

    [Fact]
    public void Nobody_buys_from_out_of_reach_or_while_down_whatever_they_carry()
    {
        var purse = PurseWith(gold: Thing.Cost.Gold * 3);

        Assert.Equal(BuyOutcome.TooFar, TradeRules.Buy(purse, Thing, TradeRules.Reach + 0.01f, down: false));
        Assert.Equal(BuyOutcome.Down, TradeRules.Buy(purse, Thing, 0f, down: true));
        Assert.Equal(Thing.Cost.Gold * 3, purse.Gold);
    }

    [Fact]
    public void A_cost_in_two_currencies_needs_both_and_takes_both()
    {
        var cost = new Cost(Gold: 30, Orbs: 2);
        var goldOnly = PurseWith(gold: cost.Gold * 2);
        var orbsOnly = PurseWith(orbs: cost.Orbs * 2);
        var both = PurseWith(gold: cost.Gold + 1, souls: 9, orbs: cost.Orbs + 1);

        Assert.Equal(new Cost(Orbs: cost.Orbs), goldOnly.ShortOf(cost));
        Assert.Equal(new Cost(Gold: cost.Gold), orbsOnly.ShortOf(cost));
        Assert.Equal(BuyOutcome.CannotAfford, TradeRules.Buy(goldOnly, Thing with { Cost = cost }, 0f, down: false));
        Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(both, Thing with { Cost = cost }, 0f, down: false));
        Assert.Equal((1, 9, 1), (both.Gold, both.Souls, both.Orbs));
    }

    [Fact]
    public void A_cost_names_only_the_currencies_it_asks_for()
    {
        Assert.Equal(new[] { (Currency.Gold, 30), (Currency.Orbs, 2) }, new Cost(Gold: 30, Orbs: 2).Parts);
        Assert.Empty(Cost.Nothing.Parts);
        Assert.True(Cost.Nothing.IsNothing);
    }

    [Fact]
    public void A_purse_refuses_to_pay_more_than_it_holds()
    {
        var purse = PurseWith(gold: Thing.Cost.Gold);

        Assert.Throws<InvalidOperationException>(() => purse.Pay(new Cost(Gold: Thing.Cost.Gold + 1)));
        Assert.Throws<InvalidOperationException>(() => purse.Pay(new Cost(Gold: -1)));
        Assert.Equal(Thing.Cost.Gold, purse.Gold);
    }

    [Fact]
    public void Something_that_cannot_be_had_is_not_sold_whatever_the_purse_holds()
    {
        var purse = PurseWith(gold: 1000, orbs: 100);
        var none = Thing with { Unavailable = "Not today" };

        Assert.Equal(BuyOutcome.Unavailable, TradeRules.Buy(purse, none, 0f, down: false));
        Assert.Equal(1000, purse.Gold);
    }

    [Fact]
    public void Every_sellers_goods_fit_its_window_and_can_be_told_apart()
    {
        Assert.NotEmpty(Sellers.All);
        Assert.Equal(Sellers.All.Count, Sellers.All.Select(s => s.Id).Distinct().Count());
        foreach (var seller in Sellers.All)
        {
            var items = seller.ItemsFor(Anyone);

            Assert.NotEmpty(items);
            Assert.True(items.Count <= TradeRules.Slots, seller.Id);
            Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
            Assert.All(items.Where(i => i.Unavailable == null), i => Assert.False(i.Cost.IsNothing && i.Pays.IsNothing, $"{seller.Id}: {i.Id}"));
            Assert.Same(seller, Sellers.ById(seller.Id));
            Assert.All(items, i => Assert.Equal(i, seller.Item(Anyone, i.Id)));
        }
    }

    [Fact]
    public void The_window_has_room_for_nine_goods_in_full_rows()
    {
        Assert.True(TradeRules.Slots >= 9);
        Assert.Equal(0, TradeRules.Slots % TradeRules.SlotColumns);
    }

    [Fact]
    public void The_weaponsmith_sells_every_weapon_of_steel_or_wood_plain_for_gold_alone()
    {
        var items = Sellers.Weaponsmith.ItemsFor(Anyone);

        Assert.Equal(Weapons.Arms.OrderBy(w => w.Id), items.Select(i => i.Weapon!).OrderBy(w => w.Id));
        Assert.All(items, i => Assert.Equal(new[] { Currency.Gold }, i.Cost.Parts.Select(p => p.Currency)));
        Assert.All(items, i => Assert.Null(i.Slot));
    }

    [Fact]
    public void The_arcanist_sells_a_staff_for_every_element_plain_for_gold_alone()
    {
        var items = Sellers.Arcanist.ItemsFor(Anyone);

        Assert.Equal(Weapons.Staffs, items.Select(i => i.Weapon!));
        Assert.Equal(Elements.All, items.Select(i => i.Weapon!.Element!.Value));
        Assert.All(items, i => Assert.Equal(new Cost(Gold: TradeRules.PlainWeaponPrice), i.Cost));
        Assert.All(items, i => Assert.Null(i.Slot));
    }

    [Fact]
    public void Between_them_the_weaponsmith_and_the_arcanist_sell_every_weapon_once()
    {
        var sold = Sellers.Weaponsmith.ItemsFor(Anyone).Concat(Sellers.Arcanist.ItemsFor(Anyone)).Select(i => i.Weapon!).ToList();

        Assert.Equal(Weapons.All.OrderBy(w => w.Id), sold.OrderBy(w => w.Id));
    }

    [Fact]
    public void Unknown_sellers_fail_loudly_and_unknown_goods_are_not_offered()
    {
        Assert.Throws<KeyNotFoundException>(() => Sellers.ById("nobody"));
        Assert.Null(Sellers.Weaponsmith.Item(Anyone, "nothing"));
    }

    [Fact]
    public void A_seller_is_reached_from_further_than_a_weapon_on_the_ground()
    {
        Assert.True(TradeRules.Reach > Pickups.Reach);
    }

    [Fact]
    public void The_blacksmith_offers_the_next_step_up_for_the_hand_the_back_and_the_armour_in_that_order()
    {
        var weapons = new WeaponSets(Weapons.Scythe, Weapons.AtLevel(Weapons.Spear, 4));

        var offers = Sellers.Blacksmith.ItemsFor(new Buyer(weapons, ArmourTier: 2));

        Assert.Equal(3, offers.Count);
        Assert.Equal(new WeaponSlot?[] { WeaponSlot.Hand, WeaponSlot.Back, null }, offers.Select(o => o.Slot));
        Assert.Equal(Weapons.AtLevel(Weapons.Scythe, 1), offers[0].Weapon);
        Assert.Equal(Weapons.AtLevel(Weapons.Spear, 5), offers[1].Weapon);
        Assert.Null(offers[2].Weapon);
        Assert.Equal(Armours.AtTier(3), offers[2].Armour);
        Assert.Equal(TradeRules.ImprovementCost(1), offers[0].Cost);
        Assert.Equal(TradeRules.ImprovementCost(5), offers[1].Cost);
        Assert.Equal(TradeRules.ArmourCost(3), offers[2].Cost);
        Assert.All(offers, o => Assert.Null(o.Unavailable));
    }

    [Fact]
    public void The_blacksmith_has_nothing_to_do_for_an_empty_slot_or_for_what_is_at_its_best()
    {
        var weapons = new WeaponSets(null, Weapons.AtLevel(Weapons.Spear, WeaponDefinition.MaxLevel));
        var purse = PurseWith(gold: 10000, orbs: 1000);

        var offers = Sellers.Blacksmith.ItemsFor(new Buyer(weapons, Armours.MaxTier));

        Assert.Equal(3, offers.Count);
        Assert.All(offers, o => Assert.NotNull(o.Unavailable));
        Assert.All(offers, o => Assert.Equal(BuyOutcome.Unavailable, TradeRules.Buy(purse, o, 0f, down: false)));
        Assert.Equal(10000, purse.Gold);
    }

    [Fact]
    public void Every_improvement_costs_gold_and_orbs_and_more_than_the_one_before()
    {
        var costs = Enumerable.Range(1, WeaponDefinition.MaxLevel).Select(TradeRules.ImprovementCost).ToList();

        Assert.All(costs, c => Assert.True(c.Gold > 0 && c.Orbs > 0 && c.Souls == 0));
        for (int i = 1; i < costs.Count; i++)
        {
            Assert.True(costs[i].Gold > costs[i - 1].Gold, $"to +{i + 1}");
            Assert.True(costs[i].Orbs >= costs[i - 1].Orbs, $"to +{i + 1}");
        }

        Assert.True(costs[^1].Orbs > costs[0].Orbs);
        Assert.Throws<ArgumentOutOfRangeException>(() => TradeRules.ImprovementCost(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TradeRules.ImprovementCost(WeaponDefinition.MaxLevel + 1));
    }

    [Fact]
    public void An_improvement_replaces_the_weapon_in_its_slot_and_puts_nothing_down()
    {
        var buyer = WeaponSets.Default;
        var better = Weapons.AtLevel(buyer.Stowed!, 1);

        var (sets, putDown) = TradeRules.Receive(buyer, better, WeaponSlot.Back);

        Assert.Equal(new WeaponSets(buyer.Active, better), sets);
        Assert.Null(putDown);
    }

    [Fact]
    public void A_weapon_bought_is_received_as_a_bought_weapon_is()
    {
        Assert.Equal(WeaponSets.Default.WithBought(Weapons.Scythe), TradeRules.Receive(WeaponSets.Default, Weapons.Scythe, slot: null));
    }

    [Fact]
    public void A_weapon_can_be_improved_step_by_step_to_its_best_and_no_further()
    {
        var buyer = new WeaponSets(Weapons.Greatsword, null);
        var purse = PurseWith(gold: 100000, orbs: 1000);

        for (int level = 1; level <= WeaponDefinition.MaxLevel; level++)
        {
            var offer = TradeRules.Improvement(buyer, WeaponSlot.Hand);
            Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, offer, 0f, down: false));
            buyer = TradeRules.Receive(buyer, offer.Weapon!, offer.Slot).Sets;
            Assert.Equal(level, buyer.Active!.Level);
        }

        Assert.NotNull(TradeRules.Improvement(buyer, WeaponSlot.Hand).Unavailable);
    }

    [Fact]
    public void The_blacksmith_offers_armour_one_tier_up_from_what_is_worn_whatever_that_is()
    {
        for (int worn = 0; worn < Armours.MaxTier; worn++)
        {
            var offer = TradeRules.BetterArmour(worn);

            Assert.Equal(Armours.AtTier(worn + 1), offer.Armour);
            Assert.Equal(TradeRules.ArmourCost(worn + 1), offer.Cost);
            Assert.Null(offer.Unavailable);
            Assert.Equal(TradeRules.BetterArmour(0).Id, offer.Id);
        }
    }

    [Fact]
    public void Armour_costs_gold_alone_for_its_first_tiers_then_gold_and_orbs_and_more_each_tier()
    {
        var costs = Enumerable.Range(1, Armours.MaxTier).Select(TradeRules.ArmourCost).ToList();

        Assert.Equal(2, TradeRules.ArmourTiersForGoldAlone);
        Assert.All(costs.Take(TradeRules.ArmourTiersForGoldAlone), c => Assert.True(c.Gold > 0 && c.Orbs == 0 && c.Souls == 0));
        Assert.All(costs.Skip(TradeRules.ArmourTiersForGoldAlone), c => Assert.True(c.Gold > 0 && c.Orbs > 0 && c.Souls == 0));
        for (int i = 1; i < costs.Count; i++)
        {
            Assert.True(costs[i].Gold > costs[i - 1].Gold, $"tier {i + 1}");
            Assert.True(costs[i].Orbs >= costs[i - 1].Orbs, $"tier {i + 1}");
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => TradeRules.ArmourCost(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TradeRules.ArmourCost(Armours.MaxTier + 1));
    }

    [Fact]
    public void Armour_is_made_better_tier_by_tier_to_the_last_and_no_further()
    {
        var purse = PurseWith(gold: 100000, orbs: 1000);
        int worn = 0;

        while (worn < Armours.MaxTier)
        {
            var offer = TradeRules.BetterArmour(worn);
            Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, offer, 0f, down: false));
            Assert.Equal(worn + 1, offer.Armour!.Tier);
            worn = offer.Armour.Tier;
        }

        var best = TradeRules.BetterArmour(worn);
        Assert.NotNull(best.Unavailable);
        Assert.Equal(Armours.AtTier(Armours.MaxTier), best.Armour);
        Assert.Equal(BuyOutcome.Unavailable, TradeRules.Buy(purse, best, 0f, down: false));
    }

    [Fact]
    public void The_merchant_offers_gold_for_the_hand_the_back_and_an_orb_in_that_order()
    {
        var weapons = new WeaponSets(Weapons.Scythe, Weapons.AtLevel(Weapons.Spear, 4));

        var offers = Sellers.Merchant.ItemsFor(new Buyer(weapons, ArmourTier: 0));

        Assert.Equal(3, offers.Count);
        Assert.Equal(new WeaponSlot?[] { WeaponSlot.Hand, WeaponSlot.Back, null }, offers.Select(o => o.Slot));
        Assert.Equal(new[] { weapons.Active!.Name, weapons.Stowed!.Name }, offers.Take(2).Select(o => o.Name));
        Assert.Equal(TradeRules.ResaleValue(weapons.Active), offers[0].Pays);
        Assert.Equal(TradeRules.ResaleValue(weapons.Stowed), offers[1].Pays);
        Assert.Same(TradeRules.OrbSale, offers[2]);
        Assert.All(offers, o => Assert.True(o.IsSale && o.Weapon == null && o.Armour == null && o.Unavailable == null));
        Assert.All(offers, o => Assert.Equal(new[] { Currency.Gold }, o.Pays.Parts.Select(p => p.Currency)));
    }

    [Fact]
    public void A_weapon_sold_pays_its_gold_and_leaves_its_slot_empty()
    {
        var carried = new WeaponSets(Weapons.Scythe, Weapons.Spear);
        var purse = PurseWith(gold: 7);

        foreach (var slot in new[] { WeaponSlot.Back, WeaponSlot.Hand })
        {
            int before = purse.Gold;
            var offer = TradeRules.Sale(carried, slot);

            Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, offer, 0f, down: false));
            Assert.Equal(before + TradeRules.ResaleValue(carried.In(slot)!).Gold, purse.Gold);
            var (after, putDown) = TradeRules.After(carried, offer);
            Assert.Equal(carried.Without(slot), after);
            Assert.Null(putDown);
            carried = after;
        }

        Assert.Equal(new WeaponSets(null, null), carried);
    }

    [Fact]
    public void The_merchant_has_nothing_to_pay_for_an_empty_slot()
    {
        var purse = PurseWith(gold: 7);

        var offers = Sellers.Merchant.ItemsFor(new Buyer(new WeaponSets(null, null), ArmourTier: 0));

        Assert.All(offers.Take(2), o => Assert.NotNull(o.Unavailable));
        Assert.All(offers.Take(2), o => Assert.Equal(BuyOutcome.Unavailable, TradeRules.Buy(purse, o, 0f, down: false)));
        Assert.Equal(7, purse.Gold);
    }

    [Fact]
    public void An_orb_sells_for_gold_and_only_from_a_purse_that_holds_one()
    {
        var purse = PurseWith(orbs: 1);

        Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, TradeRules.OrbSale, 0f, down: false));
        Assert.Equal((TradeRules.OrbPrice, 0), (purse.Gold, purse.Orbs));
        Assert.Equal(BuyOutcome.CannotAfford, TradeRules.Buy(purse, TradeRules.OrbSale, 0f, down: false));
        Assert.Equal((TradeRules.OrbPrice, 0), (purse.Gold, purse.Orbs));
    }

    [Fact]
    public void Nobody_sells_from_out_of_reach_or_while_down()
    {
        var purse = PurseWith(orbs: 1);
        var offer = TradeRules.Sale(WeaponSets.Default, WeaponSlot.Hand);

        Assert.Equal(BuyOutcome.TooFar, TradeRules.Buy(purse, offer, TradeRules.Reach + 0.01f, down: false));
        Assert.Equal(BuyOutcome.Down, TradeRules.Buy(purse, TradeRules.OrbSale, 0f, down: true));
        Assert.Equal((0, 1), (purse.Gold, purse.Orbs));
    }

    [Fact]
    public void A_better_weapon_sells_for_more_and_always_for_less_gold_than_it_took()
    {
        foreach (var plain in Weapons.All)
        {
            int last = 0;
            for (int level = 0; level <= WeaponDefinition.MaxLevel; level++)
            {
                var weapon = Weapons.AtLevel(plain, level);
                int pays = TradeRules.ResaleValue(weapon).Gold;

                Assert.True(pays > last, $"{weapon.Id}");
                Assert.True(pays < TradeRules.GoldPutInto(weapon), $"{weapon.Id}");
                last = pays;
            }
        }

        Assert.Equal(TradeRules.PlainWeaponPrice, TradeRules.GoldPutInto(Weapons.Scythe));
        Assert.Equal(TradeRules.PlainWeaponPrice + TradeRules.ImprovementCost(1).Gold + TradeRules.ImprovementCost(2).Gold,
            TradeRules.GoldPutInto(Weapons.AtLevel(Weapons.Scythe, 2)));
    }

    [Fact]
    public void Buying_a_weapon_and_selling_it_back_loses_gold()
    {
        var purse = PurseWith(gold: TradeRules.PlainWeaponPrice);
        var bought = Sellers.Weaponsmith.ItemsFor(Anyone)[0];

        Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, bought, 0f, down: false));
        var carried = TradeRules.After(new WeaponSets(null, null), bought).Sets;
        Assert.Equal(BuyOutcome.Bought, TradeRules.Buy(purse, TradeRules.Sale(carried, WeaponSlot.Hand), 0f, down: false));

        Assert.True(purse.Gold < TradeRules.PlainWeaponPrice);
        Assert.False(purse.CanPay(bought.Cost));
    }

    [Fact]
    public void A_trade_that_hands_nothing_over_leaves_what_is_carried_as_it_is()
    {
        Assert.Equal(WeaponSets.Default, TradeRules.After(WeaponSets.Default, TradeRules.OrbSale).Sets);
        Assert.Equal(WeaponSets.Default, TradeRules.After(WeaponSets.Default, TradeRules.BetterArmour(0)).Sets);
    }

    [Fact]
    public void An_offer_that_gives_nothing_is_not_sold()
    {
        var purse = PurseWith(gold: 1000);
        var empty = new TradeItem("empty", "Empty", new Cost(Gold: 1));

        Assert.False(empty.GivesSomething);
        Assert.Equal(BuyOutcome.Unavailable, TradeRules.Buy(purse, empty, 0f, down: false));
    }
}
