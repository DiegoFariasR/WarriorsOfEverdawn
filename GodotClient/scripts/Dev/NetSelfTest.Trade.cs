using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [trade-check], with --trade-drill: the local bot, started in reach of a seller with gold and orbs to spend, sees
// the prompt, opens the window (nine slots in rows of three, no bigger than the game's window), gets through it
// what the seller offers, slot after slot (weapons from the weaponsmith; from the blacksmith the weapon in its hand
// a level better, the one on its back, then its armour a tier better; to the merchant it sells the weapon in its
// hand, the one on its back and an orb) for as long as it can pay, is then told by the window that it cannot, asks
// the host anyway and is refused, and closes it. The weapons in its hand and on its back and the armour it wears are
// what those trades should leave it with, the gold and orbs it has left are what it started with less what it paid
// and plus what it was paid, and it stood still throughout. Every player's figure on this machine is dressed in the look of the armour that
// player wears (ArmourLook) for as long as it is here, which for the buyer is another than it came in. On the host,
// [trade-host]: who was sold how much, and what the host sees in every player's hand.
public partial class NetSelfTest
{
    // One step of the drill waits this long for the last to take effect (a purchase crosses the network twice).
    private const float TradeStepTime = 0.4f;

    private enum TradeStep
    {
        Approach,
        Buying,
        AskingAnyway,
        Closing,
        Done,
    }

    private readonly Dictionary<long, int> _soldTo = new();

    // Host: the weapon in hand, the gold, the orbs and the armour of every player it has seen; players who leave keep theirs.
    private readonly SortedDictionary<long, (string Hand, int Gold, int Orbs, int Armour)> _tradeSeen = new();
    private Rect2 _tradeWindow;
    private Market? _market;
    private TradeStep _tradeStep = TradeStep.Approach;
    private float _tradeWait;
    private int _promptFrames;
    private int _tradeOpened;
    private int _tradeClosed;
    private int _tradeAsked;
    private int _tradeBought;
    private int _tradeSpent;
    private int _tradeEarned;
    private int _tradeOrbsSpent;
    private int _tradeOrbsStart = -1;
    private int _tradeRefusedByWindow;
    private int _tradeRefusedByHost;
    private int _tradeGoldStart = -1;
    private int _tradeSlots = -1;
    private int _tradeItems = -1;
    private int _tradeWindowFits = -1;
    private WeaponSets _tradeExpected = new(null, null);
    private int _tradeExpectedArmour;
    private string _tradeOutfitStart = "none";
    private int _figuresMost;
    private int _undressedFrames;
    private int _undressedFramesMost;
    private Vector3 _tradeStoodAt;
    private float _tradeMoved;

    public bool TradeDrill { get; init; }

    private void TrackTrade()
    {
        _market = Market.In(GetTree());
        _market.Bought += (_, item) =>
        {
            _tradeBought++;
            _tradeSpent += item.Cost.Gold;
            _tradeEarned += item.Pays.Gold;
            _tradeOrbsSpent += item.Cost.Orbs;
            _tradeExpected = TradeRules.After(_tradeExpected, item).Sets;

            _tradeExpectedArmour = item.Armour?.Tier ?? _tradeExpectedArmour;
        };
        _market.Refused += _ => _tradeRefusedByHost++;
        _market.Sold += (peer, _, _) => _soldTo[peer] = _soldTo.GetValueOrDefault(peer) + 1;
        _hud.Shop.Opened += () =>
        {
            _tradeOpened++;
            _tradeStoodAt = LocalPlayer()?.GlobalPosition ?? Vector3.Zero;

            // What it has before it gets anything, which is what it should still have if nothing it gets changes it.
            _tradeExpected = new WeaponSets(LocalPlayer()?.Weapon, LocalPlayer()?.StowedWeapon);
            _tradeExpectedArmour = LocalPlayer()?.Vitals.Armour ?? 0;
        };
        _hud.Shop.Closed += () => _tradeClosed++;
    }

    private void MeasureTrade(float delta)
    {
        if (_market == null || LocalPlayer() is not { } local)
        {
            return;
        }

        _promptFrames += _market.Sellers.Count(s => s.PromptShown);

        // A figure is dressed in the frame after its armour changes, so a frame out of step is not a fault; frames on
        // end are.
        var figures = _players.GetChildren().OfType<PlayerCharacter>().ToList();
        _figuresMost = Mathf.Max(_figuresMost, figures.Count);
        _undressedFrames = figures.Any(p => !ArmourLook.IsDressedFor(p.Skeleton, p.Vitals.Armour)) ? _undressedFrames + 1 : 0;
        _undressedFramesMost = Mathf.Max(_undressedFramesMost, _undressedFrames);
        if (Multiplayer.IsServer())
        {
            foreach (var player in _players.GetChildren().OfType<PlayerCharacter>())
            {
                _tradeSeen[player.PeerId] = (player.Weapon?.Id ?? NoWeapon, player.Vitals.Gold, player.Vitals.Orbs, player.Vitals.Armour);
            }
        }

        if (!TradeDrill || local.Controls is not BotControls bot)
        {
            return;
        }

        var shop = _hud.Shop;
        if (shop.IsOpen)
        {
            var moved = local.GlobalPosition - _tradeStoodAt;
            _tradeMoved = Mathf.Max(_tradeMoved, new Vector2(moved.X, moved.Z).Length());
        }

        _tradeWait -= delta;
        if (_tradeWait > 0f)
        {
            return;
        }

        _tradeWait = TradeStepTime;
        switch (_tradeStep)
        {
            // Pressed until it takes: the bot's opening swing has to end first, and the gold it starts with to arrive.
            case TradeStep.Approach when shop.IsOpen:
                _tradeGoldStart = local.Vitals.Gold;
                _tradeOrbsStart = local.Vitals.Orbs;
                _tradeOutfitStart = ArmourLook.Worn(local.Skeleton);
                _tradeSlots = shop.SlotCount;
                _tradeItems = shop.Items.Count;
                _tradeStep = TradeStep.Buying;
                break;
            // Held where it starts, beside the seller, so it does not wander out of reach before the window opens.
            case TradeStep.Approach:
                _tradeWait = 0f;
                bot.Suspended = true;
                if (local.Vitals.Gold > 0 && _market.InReachOf(local.GlobalPosition) != null)
                {
                    bot.Interact();
                }

                break;

            // The first thing on offer that it does not carry, so each one got shows as a change of the weapon in hand.
            case TradeStep.Buying when _tradeAsked == _tradeBought:
                // By now the window has been laid out, which it is not in the frame it opens.
                _tradeWindow = shop.WindowRect;
                _tradeWindowFits = _tradeWindow.Size.X <= DesignSize().X && _tradeWindow.Size.Y <= DesignSize().Y ? 1 : 0;

                // The offers in slot order, one after another and round again, passing over what cannot be had.
                int offers = shop.Items.Count;
                shop.Choose(Enumerable.Range(0, offers).Select(i => (_tradeAsked + i) % offers).FirstOrDefault(i => shop.Items[i].Unavailable == null));

                // It gets what it can pay for: what the session starts it with is what decides how much that is.
                if (shop.Buy())
                {
                    _tradeAsked++;
                }
                else
                {
                    _tradeRefusedByWindow += shop.Status.Length > 0 ? 1 : 0;
                    _market.Buy(shop.Seller!, shop.Items[shop.Chosen]);
                    _tradeStep = TradeStep.AskingAnyway;
                }

                break;
            case TradeStep.AskingAnyway:
                _tradeStep = TradeStep.Closing;
                break;
            case TradeStep.Closing:
                // The weapon it bought is the one it keeps from here on; none, when it sold what it had.
                bot.Rehome(local.Weapon);
                shop.Close();
                _tradeStep = TradeStep.Done;
                break;
        }
    }

    // The size the game's window opens at: a headless run has no screen of its own to measure against.
    private static Vector2 DesignSize() => new(
        ProjectSettings.GetSetting("display/window/size/viewport_width").AsSingle(),
        ProjectSettings.GetSetting("display/window/size/viewport_height").AsSingle());

    private void PrintTradeCheck(long me)
    {
        var local = LocalPlayer();
        GD.Print($"[trade-check] me={me} sellers={_market?.Sellers.Count ?? -1} prompt_frames={_promptFrames} opened={_tradeOpened} closed={_tradeClosed} "
            + $"slots={_tradeSlots} slots_wanted={TradeRules.Slots} items={_tradeItems} window_fits={_tradeWindowFits} "
            + $"window={_tradeWindow.Size.X:F0}x{_tradeWindow.Size.Y:F0} game_window={DesignSize().X:F0}x{DesignSize().Y:F0} "
            + $"seller={_hud.Shop.Seller?.Id ?? "none"} bought={_tradeBought} spent={_tradeSpent} earned={_tradeEarned} gold_start={_tradeGoldStart} gold_here={local?.Vitals.Gold} "
            + $"orbs_spent={_tradeOrbsSpent} orbs_start={_tradeOrbsStart} orbs_here={local?.Vitals.Orbs} "
            + $"refused_by_window={_tradeRefusedByWindow} refused_by_host={_tradeRefusedByHost} "
            + $"in_hand={local?.Weapon?.Id ?? NoWeapon} expected_in_hand={_tradeExpected.Active?.Id ?? NoWeapon} "
            + $"on_back={local?.StowedWeapon?.Id ?? NoWeapon} expected_on_back={_tradeExpected.Stowed?.Id ?? NoWeapon} "
            + $"armour_here={local?.Vitals.Armour} expected_armour={_tradeExpectedArmour} armour_shown={_hud.ShownArmour} moved_while_trading={_tradeMoved:F2} "
            + $"outfit_start={_tradeOutfitStart} outfit_here={(local == null ? "none" : ArmourLook.Worn(local.Skeleton))} "
            + $"outfit_wanted={(local == null ? "none" : ArmourLook.OutfitFor(local.Vitals.Armour))} "
            + $"figures={_figuresMost} undressed_frames_on_end={_undressedFramesMost} "
            + $"drill_done={(_tradeStep == TradeStep.Done ? 1 : 0)}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[trade-host] sold={string.Join(",", _soldTo.OrderBy(s => s.Key).Select(s => $"{s.Key}:{s.Value}"))} "
                + $"hands={string.Join(",", _tradeSeen.Select(s => $"{s.Key}:{s.Value.Hand}"))} "
                + $"gold={string.Join(",", _tradeSeen.Select(s => $"{s.Key}:{s.Value.Gold}"))} "
                + $"orbs={string.Join(",", _tradeSeen.Select(s => $"{s.Key}:{s.Value.Orbs}"))} "
                + $"armour={string.Join(",", _tradeSeen.Select(s => $"{s.Key}:{s.Value.Armour}"))}");
        }
    }
}
