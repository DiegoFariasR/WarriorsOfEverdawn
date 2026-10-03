using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Main;

// The window for trading with a seller, the same for every seller: who they are and what they trade, what the player
// carries to pay with, slots in rows (TradeRules.Slots, filled or empty), and beside them what the chosen one is,
// what it costs, what having it would do, and the button that gets it. Mouse, keys or controller: click a slot or walk
// to it with the arrows; E, Enter or the button buys (or sells, at a seller
// who pays); Esc closes. What a seller offers can depend on what the player carries (the blacksmith works on the
// player's own weapons, the merchant pays for them), so the slots are read afresh as that changes. It only asks:
// Market carries the request to the host. Design: Docs/Design/trade.md.
public partial class ShopPanel : Control
{
    private const float SlotWidth = 150f;
    private const float SlotHeight = 100f;
    private const float DetailWidth = 290f;
    private const string EmptySlot = "-";

    private static readonly Color Dim = new(1f, 1f, 1f, 0.35f);

    private readonly Slot[] _slots = new Slot[TradeRules.Slots];
    private readonly bool?[] _shownChosen = new bool?[TradeRules.Slots];
    private readonly Label[] _purse = new Label[3];
    private IReadOnlyList<TradeItem> _items = Array.Empty<TradeItem>();
    private Control _window = null!;
    private Label _name = null!;
    private Label _line = null!;
    private Label _itemName = null!;
    private Label _details = null!;
    private Label _price = null!;
    private Label _effect = null!;
    private Label _status = null!;
    private Button _buy = null!;
    private PlayerCharacter? _player;
    private Input.MouseModeEnum _mouseBefore;

    // Asked and not yet answered. The host judges by what it sees the player carry, which trails what the player
    // has here by a moment, so a second press before the answer would trade the same thing twice.
    private bool _waiting;

    // The chosen thing is asked for; Market takes it from there.
    public event Action<SellerDefinition, TradeItem>? BuyAsked;

    public event Action? Opened;

    public event Action? Closed;

    public bool IsOpen => Visible;

    public SellerDefinition? Seller { get; private set; }

    public int Chosen { get; private set; }

    public int SlotCount => _slots.Length;

    // What the seller offers this player right now, in slot order.
    public IReadOnlyList<TradeItem> Items => _items;

    public string Status => _status.Text;

    // The window's rectangle on screen, for checking that it fits.
    public Rect2 WindowRect => _window.GetGlobalRect();

    private TradeItem? ChosenItem => Chosen < _items.Count ? _items[Chosen] : null;

    private static StyleBoxFlat SlotBox { get; } = UiTheme.Panel(UiTheme.WoodDk, UiTheme.GoldDk, radius: 4, margin: 6f, borderWidth: 1);

    private static StyleBoxFlat HoverBox { get; } = UiTheme.Panel(UiTheme.WoodDk, UiTheme.Gold, radius: 4, margin: 6f, borderWidth: 2);

    private static StyleBoxFlat ChosenBox { get; } = UiTheme.Panel(UiTheme.Wood, UiTheme.GoldHi, radius: 4, margin: 6f, borderWidth: 3);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;

        // Dims the fight behind and takes the clicks that miss the window.
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centre);
        _window = BuildWindow();
        centre.AddChild(_window);
    }

    public void Open(SellerDefinition seller, PlayerCharacter player)
    {
        Seller = seller;
        _player = player;
        _name.Text = seller.Name;
        _line.Text = seller.Line;
        _status.Text = "";
        _waiting = false;
        Chosen = 0;
        _mouseBefore = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Visible = true;
        Refresh();
        ChooseWhatCanBeHad();
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Input.MouseMode = _mouseBefore;
        Closed?.Invoke();
    }

    public void Choose(int slot)
    {
        if (slot >= 0 && slot < _items.Count)
        {
            Chosen = slot;
            _status.Text = "";
            Refresh();
        }
    }

    // Asks for the chosen thing when it can be had and paid for; says why not otherwise.
    public bool Buy()
    {
        if (!Visible || _player == null || _waiting || ChosenItem is not { } item)
        {
            return false;
        }

        if (item.Unavailable != null)
        {
            Say(item.Unavailable, good: false);
            return false;
        }

        var shortOf = ShortOf(item.Cost);
        if (!shortOf.IsNothing)
        {
            Say($"Not enough: {CostText(shortOf, ", ")} short", good: false);
            return false;
        }

        _waiting = true;
        BuyAsked?.Invoke(Seller!, item);
        return true;
    }

    public void ShowBought(TradeItem item)
    {
        // A tier of armour once had, or a weapon sold, is no longer on offer: the choice moves on to the next.
        _waiting = false;
        Refresh();
        ChooseWhatCanBeHad();
        Say(item.Kind switch
        {
            TradeKind.Sale => $"Sold: {item.Name} for {CostText(item.Pays)}",
            TradeKind.Enchantment => $"Enchanted: {item.Name}",
            TradeKind.Attunement => $"Now: {item.Name}",
            TradeKind.Purchase => $"Bought: {item.Name}",
            TradeKind.Rest => "Rested: your health, mana and potion are full",
            _ => item.Armour != null ? $"Now wearing: {item.Name}" : $"Made better: {item.Name}",
        }, good: true);
    }

    // What the button does to the chosen thing.
    private static string VerbOf(TradeItem item) => item.Kind switch
    {
        TradeKind.Sale => "Sell",
        TradeKind.Improvement => "Improve",
        TradeKind.Enchantment => "Enchant",
        TradeKind.Attunement => "Attune",
        TradeKind.Rest => "Rest",
        _ => "Buy",
    };

    // Leaves the choice where it is while that can be had; otherwise moves it to the first offer that can, so the
    // window opens on, and after a purchase moves on to, something the button is good for.
    private void ChooseWhatCanBeHad()
    {
        if (ChosenItem is { Unavailable: null })
        {
            return;
        }

        int first = _items.ToList().FindIndex(i => i.Unavailable == null);
        if (first >= 0)
        {
            Chosen = first;
            Refresh();
        }
    }

    public void ShowRefused(BuyOutcome outcome)
    {
        _waiting = false;
        Say(outcome switch
        {
            BuyOutcome.CannotAfford => "Not enough to pay for it",
            BuyOutcome.TooFar => "Too far from the seller",
            BuyOutcome.Down => "Not while down",
            BuyOutcome.Unavailable => "Not to be had just now",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a refusal"),
        }, good: false);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        if (_player == null || !IsInstanceValid(_player) || _player.IsDowned)
        {
            Close();
            return;
        }

        Refresh();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event.IsEcho())
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
        }
        else if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("interact"))
        {
            Buy();
        }
        else if (StepOf(@event) is { } step)
        {
            Choose(Stepped(Chosen, step.X, step.Y));
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    // The slot one step across or down from another, staying on the grid.
    public static int Stepped(int from, int across, int down)
    {
        int column = Mathf.Clamp(from % TradeRules.SlotColumns + across, 0, TradeRules.SlotColumns - 1);
        int row = Mathf.Clamp(from / TradeRules.SlotColumns + down, 0, TradeRules.Slots / TradeRules.SlotColumns - 1);
        return row * TradeRules.SlotColumns + column;
    }

    // A cost in words: "40 gold + 1 orb".
    public static string CostText(Cost cost, string between = " + ") =>
        string.Join(between, cost.Parts.Select(p => $"{p.Amount} {NameOf(p.Currency, p.Amount)}"));

    // What having the thing would do to what the buyer carries and wears, in words.
    public static string EffectOf(TradeItem item, Buyer buyer)
    {
        var sets = buyer.Weapons;
        if (item.IsSale)
        {
            return item.Slot is { } sold ? $"Leaves your {(sold == WeaponSlot.Hand ? "hand" : "back")} empty" : $"For {CostText(item.Cost)}";
        }

        if (item.Armour != null)
        {
            return $"Worn at once, in place of the {Armours.AtTier(buyer.ArmourTier).Name}";
        }

        if (item.Kind == TradeKind.Rest)
        {
            return "All of it at once, here and now";
        }

        if (item.Weapon == null)
        {
            return "";
        }

        if (item.Kind == TradeKind.Attunement)
        {
            return $"The {sets.Active?.Name} in your hand becomes this staff: other spells, the same level";
        }

        if (item.Kind == TradeKind.Enchantment)
        {
            string replaces = sets.Active?.Enchantment is { } old ? $", in place of its {old.ToString().ToLowerInvariant()}" : "";
            return $"Laid on the {Weapons.Plain(item.Weapon).Name} in your hand{replaces}. Magic grows with WIS, the rest with STR";
        }

        if (item.Slot is { } slot)
        {
            return $"Every blow of the {sets.In(slot)?.Name} {(slot == WeaponSlot.Hand ? "in your hand" : "on your back")} lands harder";
        }

        var (_, putDown) = sets.WithBought(item.Weapon);
        return putDown != null ? $"Takes the place of the {putDown.Name} in your hand, which is put down at your feet"
            : sets.Active == null ? "Goes into your hand"
            : "Goes onto your back";
    }

    // What the thing is, to this buyer: a weapon's three lines; for an improvement what each skill deals now and
    // what it would deal; for armour how much of a blow it stops against how much is stopped now; for a weapon to
    // sell, the lines of the weapon that would go; for an enchantment what each skill deals now and would, and how
    // much of it would be magic; for a staff of another element, that staff's lines.
    public static string DetailsOf(TradeItem item, Buyer buyer, CharacterStats stats)
    {
        var sets = buyer.Weapons;
        if (item.Armour is { } armour)
        {
            var lines = new List<string>
            {
                $"Stops {armour.PercentStopped}% of every blow",
                $"Now: {Armours.AtTier(buyer.ArmourTier).PercentStopped}%",
                $"Tier {armour.Tier} of {Armours.MaxTier}",
            };
            if (item.Unavailable != null)
            {
                lines.Add(item.Unavailable);
            }

            return string.Join("\n", lines);
        }

        if (item.IsSale)
        {
            return item.Slot is { } sold && sets.In(sold) is { } carried
                ? GroundWeaponLabels.DetailsOf(carried, stats)
                : "The blacksmith asks for these to make weapons and armour better";
        }

        if (item.Kind == TradeKind.Rest)
        {
            return $"HP to full ({PlayerRules.MaxHp})\nMana to full\nPotion to all {HealthPotion.MaxCharges} charges";
        }

        if (item.Weapon == null)
        {
            return item.Unavailable ?? "";
        }

        // A staff of another element is another weapon: its own lines, not this one's against them.
        if (item.Slot is not { } slot || sets.In(slot) is not { } now || item.Kind == TradeKind.Attunement)
        {
            return GroundWeaponLabels.DetailsOf(item.Weapon, stats);
        }

        return string.Join("\n", now.Skills.Zip(item.Weapon.Skills, (before, after) =>
                $"{after.Name} {GroundWeaponLabels.DamageOf(before, stats)} to {GroundWeaponLabels.DamageOf(after, stats)}"))
            + (item.Kind == TradeKind.Enchantment && item.Weapon.Enchantment is { } element
                ? $"\n{GroundWeaponLabels.EnchantedOf(element)}"
                : $"\nLevel {item.Weapon.Level} of {WeaponDefinition.MaxLevel}");
    }

    private static Vector2I? StepOf(InputEvent @event) =>
        @event.IsActionPressed("ui_left") || @event.IsActionPressed("move_left") ? Vector2I.Left
        : @event.IsActionPressed("ui_right") || @event.IsActionPressed("move_right") ? Vector2I.Right
        : @event.IsActionPressed("ui_up") || @event.IsActionPressed("move_forward") ? Vector2I.Up
        : @event.IsActionPressed("ui_down") || @event.IsActionPressed("move_back") ? Vector2I.Down
        : null;

    private static string NameOf(Currency currency, int amount) => currency switch
    {
        Currency.Gold => "gold",
        Currency.Souls => amount == 1 ? "soul" : "souls",
        Currency.Orbs => amount == 1 ? "orb" : "orbs",
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, "Not a currency"),
    };

    // One currency wears its own colour; a cost in several is plain gold lettering.
    private static Color ColourOf(Cost cost)
    {
        var parts = cost.Parts.ToList();
        return parts.Count != 1 ? UiTheme.GoldHi : parts[0].Currency switch
        {
            Currency.Gold => UiTheme.GoldHi,
            Currency.Souls => UiTheme.SoulText,
            _ => MagicOrb.ColourNow(),
        };
    }

    private void Say(string text, bool good)
    {
        _status.Text = text;
        _status.Modulate = good ? UiTheme.GoldHi : UiTheme.StatusFallen;
    }

    private Cost ShortOf(Cost cost) => cost.ShortWith(_player!.Vitals.Gold, _player.Vitals.Souls, _player.Vitals.Orbs);

    // What changes hands in coin: what a sale pays, or what a purchase costs.
    private static Cost Coin(TradeItem item) => item.IsSale ? item.Pays : item.Cost;

    // Brings the window in line with what the player carries and what is chosen; cheap enough for every frame.
    private void Refresh()
    {
        if (Seller == null || _player == null)
        {
            return;
        }

        var buyer = _player.AsBuyer();
        _items = Seller.ItemsFor(buyer);
        if (_items.Count > _slots.Length)
        {
            throw new InvalidOperationException($"The {Seller.Name} offers {_items.Count} things and the window has {_slots.Length} slots");
        }

        _purse[0].Text = _player.Vitals.Gold.ToString();
        _purse[1].Text = _player.Vitals.Souls.ToString();
        _purse[2].Text = _player.Vitals.Orbs.ToString();
        _purse[2].Modulate = MagicOrb.ColourNow();

        for (int i = 0; i < _slots.Length; i++)
        {
            var item = i < _items.Count ? _items[i] : null;
            bool chosen = i == Chosen && item != null;
            bool had = item is { Unavailable: null };
            _slots[i].Button.Disabled = item == null;
            ShowChosen(i, chosen);
            _slots[i].Content.Modulate = had ? Colors.White : Dim;
            _slots[i].Name.Text = item?.Label ?? item?.Name ?? EmptySlot;
            _slots[i].Price.Text = !had ? "" : item!.IsSale ? $"+{CostText(item.Pays)}" : CostText(item.Cost, "\n+ ");
            _slots[i].Price.Modulate = had && ShortOf(item!.Cost).IsNothing ? ColourOf(Coin(item)) : UiTheme.StatusFallen;
        }

        Chosen = Mathf.Clamp(Chosen, 0, Math.Max(0, _items.Count - 1));
        if (ChosenItem is not { } shown)
        {
            return;
        }

        var shortOf = ShortOf(shown.Cost);
        bool canBeHad = shown.Unavailable == null;
        _itemName.Text = shown.Name;
        _details.Text = DetailsOf(shown, buyer, _player.Stats);
        string price = shown.IsSale ? $"Pays {CostText(shown.Pays)}" : CostText(shown.Cost);
        _price.Text = !canBeHad ? "" : shortOf.IsNothing ? price : $"{price}\n{CostText(shortOf, ", ")} short";
        _price.Modulate = shortOf.IsNothing ? ColourOf(Coin(shown)) : UiTheme.StatusFallen;
        _effect.Text = canBeHad ? EffectOf(shown, buyer) : "";
        _buy.Text = $"{VerbOf(shown)}  -  E";
        _buy.Disabled = !canBeHad || !shortOf.IsNothing;
    }

    // Set only as the choice moves: setting a box, even the one it has, lays the button out again.
    private void ShowChosen(int slot, bool chosen)
    {
        if (_shownChosen[slot] == chosen)
        {
            return;
        }

        _shownChosen[slot] = chosen;
        _slots[slot].Button.AddThemeStyleboxOverride("normal", chosen ? ChosenBox : SlotBox);
        _slots[slot].Button.AddThemeStyleboxOverride("hover", chosen ? ChosenBox : HoverBox);
    }

    private Control BuildWindow()
    {
        var window = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        window.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Wood, UiTheme.Gold, radius: 6, margin: 16f, borderWidth: 2));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        window.AddChild(column);

        column.AddChild(BuildHeader());

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        column.AddChild(body);
        body.AddChild(BuildSlots());
        body.AddChild(BuildDetail());

        var hint = UiTheme.MakeLabel("Arrows: choose      E / Enter: trade      Esc: close", UiTheme.Words, 12, UiTheme.GoldDk);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(hint);
        return window;
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer();
        var who = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        who.AddThemeConstantOverride("separation", 0);
        _name = UiTheme.MakeLabel("", UiTheme.Words, 24, UiTheme.GoldHi, outline: 2);
        _line = UiTheme.MakeLabel("", UiTheme.Words, 13, UiTheme.TextMain);
        who.AddChild(_name);
        who.AddChild(_line);
        header.AddChild(who);

        // What the player has to pay with, as the player frame shows it.
        var purse = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        purse.AddThemeConstantOverride("separation", 18);
        var colours = new[] { UiTheme.GoldHi, UiTheme.SoulText, Colors.White };
        string[] names = { "Gold", "Souls", "Orbs" };
        for (int i = 0; i < names.Length; i++)
        {
            var carried = new HBoxContainer();
            carried.AddThemeConstantOverride("separation", 5);
            carried.AddChild(UiTheme.MakeLabel(names[i], UiTheme.Words, 13, UiTheme.TextMain));
            _purse[i] = UiTheme.MakeLabel("0", UiTheme.Numbers, 18, colours[i]);
            carried.AddChild(_purse[i]);
            purse.AddChild(carried);
        }

        header.AddChild(purse);
        return header;
    }

    private Control BuildSlots()
    {
        var grid = new GridContainer { Columns = TradeRules.SlotColumns };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        for (int i = 0; i < _slots.Length; i++)
        {
            int slot = i;

            // Keys are handled by the window as a whole, so no slot keeps the keyboard's focus to itself.
            var button = new Button { CustomMinimumSize = new Vector2(SlotWidth, SlotHeight), FocusMode = FocusModeEnum.None, Disabled = true };
            button.AddThemeStyleboxOverride("disabled", SlotBox);
            button.AddThemeStyleboxOverride("pressed", ChosenBox);
            button.Pressed += () => Choose(slot);

            var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
            content.AddThemeConstantOverride("separation", 0);
            var name = Wrapped(UiTheme.MakeLabel(EmptySlot, UiTheme.Words, 13, UiTheme.TextMain));
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.SizeFlagsVertical = SizeFlags.ExpandFill;
            var price = UiTheme.MakeLabel("", UiTheme.Numbers, 15, Colors.White);
            price.HorizontalAlignment = HorizontalAlignment.Center;
            content.AddChild(name);
            content.AddChild(price);
            button.AddChild(content);
            grid.AddChild(button);
            _slots[i] = new Slot(button, content, name, price);
        }

        return grid;
    }

    private Control BuildDetail()
    {
        var detail = new PanelContainer { CustomMinimumSize = new Vector2(DetailWidth, 0f) };
        detail.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.WoodDk, UiTheme.GoldDk, radius: 4, margin: 12f));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        detail.AddChild(column);

        _itemName = UiTheme.MakeLabel("", UiTheme.Words, 20, UiTheme.GoldHi, outline: 2);
        _details = Wrapped(UiTheme.MakeLabel("", UiTheme.Words, 13, UiTheme.TextMain));
        _price = UiTheme.MakeLabel("", UiTheme.Numbers, 18, Colors.White);
        _effect = Wrapped(UiTheme.MakeLabel("", UiTheme.Words, 12, UiTheme.TextMain));
        _status = Wrapped(UiTheme.MakeLabel("", UiTheme.Words, 13, Colors.White));
        var space = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };

        _buy = new Button { Text = "Buy  -  E", CustomMinimumSize = new Vector2(0f, 40f), FocusMode = FocusModeEnum.None };
        _buy.AddThemeFontOverride("font", UiTheme.Words);
        _buy.AddThemeFontSizeOverride("font_size", 16);
        _buy.AddThemeColorOverride("font_color", UiTheme.GoldHi);
        _buy.AddThemeStyleboxOverride("normal", HoverBox);
        _buy.AddThemeStyleboxOverride("hover", ChosenBox);
        _buy.AddThemeStyleboxOverride("pressed", ChosenBox);
        _buy.AddThemeStyleboxOverride("disabled", SlotBox);
        _buy.Pressed += () => Buy();

        foreach (var part in new Control[] { _itemName, _details, _price, _effect, space, _status, _buy })
        {
            column.AddChild(part);
        }

        return detail;
    }

    private static Label Wrapped(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private sealed record Slot(Button Button, Control Content, Label Name, Label Price);
}
