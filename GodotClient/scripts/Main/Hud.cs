using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Local player's frame (top left: name, HP, mana, stats), skill bar (bottom centre), mode notices (top centre) and
// the overhead bars on skeletons and other players, in Everdawn's style. Layout is first-pass. Design: Docs/Design/ui.md.
public partial class Hud : CanvasLayer
{
    private const float NoticeTime = 2.5f;
    private const float NoticeFade = 0.5f;
    private const float SoulPulseScale = 1.45f;
    private const float SoulPulseLight = 1.8f;
    private const float SoulPulseTime = 0.35f;
    private const float Margin = 16f;
    private const float BarWidth = 260f;
    private const float SlotHeight = 62f;

    // The player frame's border and margins either side of its bars.
    private const float FramePadding = 28f;

    private static readonly (int Skill, string Key)[] SkillSlots =
    {
        (PlayerCharacter.Primary, "LMB"),
        (PlayerCharacter.Secondary, "RMB"),
    };

    private readonly Label _notice = UiTheme.MakeLabel("", UiTheme.Words, 20, UiTheme.GoldHi, outline: 6);
    // White, and tinted each frame: red while down, else the colour of the first status it names.
    private readonly Label _status = UiTheme.MakeLabel("", UiTheme.Words, 13, Colors.White, outline: 3);
    private readonly Label[] _statValues = new Label[4];
    private readonly Label _gold = UiTheme.MakeLabel("0", UiTheme.Numbers, 16, UiTheme.GoldHi);
    private readonly Label _souls = UiTheme.MakeLabel("0", UiTheme.Numbers, 16, UiTheme.SoulText);
    private Tween? _soulsPulse;

    // White, and tinted each frame with the colour the orbs themselves are passing through.
    private readonly Label _orbs = UiTheme.MakeLabel("0", UiTheme.Numbers, 16, Colors.White);
    private readonly (Label Name, HBoxContainer Types, Label Cost, Label Cooldown, ColorRect Dim)[] _slots = new (Label, HBoxContainer, Label, Label, ColorRect)[SkillSlots.Length];

    private const string NoSkill = "-";

    // A potion's charge, as its pip on the skill bar.
    private static readonly Color PotionPip = new(0.9f, 0.25f, 0.25f);

    // Everdawn's MP colour, for mana costs on the skill bar.
    private static readonly Color ManaText = new(0.55f, 0.78f, 1f);
    private Label _name = null!;
    private readonly ColorRect[] _dashPips = new ColorRect[DashRules.Charges];
    private Label _dashRecharge = null!;
    private readonly ColorRect[] _potionPips = new ColorRect[HealthPotion.MaxCharges];
    private Label _potionCooldown = null!;
    private ColorRect _potionDim = null!;
    private ProgressBar _health = null!;
    private Label _healthText = null!;
    private ProgressBar _mana = null!;
    private Label _manaText = null!;
    private float _noticeLeft;
    private WeaponSets? _shownSets;
    private Label _guardName = null!;
    private ColorRect _guardDim = null!;
    private Label _backWeapon = null!;

    public PlayerCharacter? Player { get; set; }

    public OverheadBars Bars { get; } = new() { Name = "OverheadBars" };

    public GroundWeaponLabels GroundLabels { get; } = new() { Name = "GroundWeaponLabels" };

    public SellerLabels SellerLabels { get; } = new() { Name = "SellerLabels" };

    public SeatPrompt SeatPrompt { get; } = new() { Name = "SeatPrompt" };

    public ShopPanel Shop { get; } = new() { Name = "Shop" };

    public Control PlayerFrame { get; private set; } = null!;

    public Control SkillBar { get; private set; } = null!;

    public int ShownHp => (int)_health.Value;

    public int ShownMana => (int)_mana.Value;

    public string ShownGold => _gold.Text;

    public string ShownSouls => _souls.Text;

    // A soul reaching this machine's player (SoulWisps): its count swells and settles, lit white.
    public void PulseSouls()
    {
        _soulsPulse?.Kill();
        _souls.PivotOffset = _souls.Size / 2f;
        _souls.Scale = Vector2.One * SoulPulseScale;
        _souls.Modulate = Colors.White * SoulPulseLight;
        _soulsPulse = _souls.CreateTween().SetParallel();
        _soulsPulse.TweenProperty(_souls, "scale", Vector2.One, SoulPulseTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _soulsPulse.TweenProperty(_souls, "modulate", Colors.White, SoulPulseTime);
    }

    public string ShownOrbs => _orbs.Text;

    public string ShownArmour => _statValues[3].Text;

    public override void _Ready()
    {
        AddChild(Bars);
        AddChild(GroundLabels);
        AddChild(SellerLabels);
        AddChild(SeatPrompt);
        PlayerFrame = BuildPlayerFrame();
        AddChild(PlayerFrame);
        SkillBar = BuildSkillBar();
        AddChild(SkillBar);

        _notice.HorizontalAlignment = HorizontalAlignment.Center;
        _notice.AnchorRight = 1f;
        _notice.OffsetTop = 24f;

        // Centred in what the player frame leaves of the top of the screen, not across it.
        _notice.OffsetLeft = Margin + BarWidth + FramePadding;
        AddChild(_notice);

        // Last, so it opens over everything else.
        AddChild(Shop);
    }

    public void ShowNotice(string text)
    {
        _notice.Text = text;
        _noticeLeft = NoticeTime;
    }

    public override void _Process(double delta)
    {
        _noticeLeft = Mathf.Max(0f, _noticeLeft - (float)delta);
        _notice.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, _noticeLeft / NoticeFade));

        if (Player == null || !IsInstanceValid(Player))
        {
            return;
        }

        if (_shownSets == null || Player.Weapon != _shownSets.Active || Player.StowedWeapon != _shownSets.Stowed)
        {
            // The first weapons are simply shown; a change is announced.
            if (_shownSets != null)
            {
                ShowNotice($"{NameOf(Player.Weapon, "Nothing")} in hand, {NameOf(Player.StowedWeapon, "nothing")} on your back  -  Tab swaps, G drops");
            }

            ShowSkills(Player.Weapon);
            _backWeapon.Text = NameOf(Player.StowedWeapon, "Empty");
            _shownSets = Player.Sets;
        }

        _name.Text = $"Knight  -  {NameOf(Player.Weapon, "Unarmed")}" + (SessionRules.Pvp ? "  -  PvP" : "");
        int hp = Player.Vitals.Hp;
        _health.Value = hp;
        _healthText.Text = $"{hp} / {PlayerRules.MaxHp}";
        _mana.MaxValue = Player.MaxMana;
        _mana.Value = Player.Mana;
        _manaText.Text = $"{Player.Mana} / {Player.MaxMana}";
        _statValues[0].Text = Player.Stats.Str.ToString();
        _statValues[1].Text = Player.Stats.Wis.ToString();
        _statValues[2].Text = Player.Stats.Agi.ToString();
        _statValues[3].Text = Player.Vitals.Armour.ToString();
        _gold.Text = Player.Vitals.Gold.ToString();
        _souls.Text = Player.Vitals.Souls.ToString();
        _orbs.Text = Player.Vitals.Orbs.ToString();
        _orbs.Modulate = MagicOrb.ColourNow();
        var statuses = Player.IsDowned ? new List<(string Name, Color Colour)>() : StatusLooks.Of(Player.Vitals.Statuses).ToList();
        _status.Text = Player.IsDowned ? "Down - back up in a moment" : string.Join("   ", statuses.Select(s => s.Name));
        _status.Modulate = Player.IsDowned || statuses.Count == 0 ? UiTheme.StatusFallen : statuses[0].Colour;

        int charges = Player.DashCharges;
        for (int i = 0; i < _dashPips.Length; i++)
        {
            _dashPips[i].Color = i < charges ? UiTheme.GoldHi : new Color(UiTheme.WoodDk, 0.9f);
        }

        // Lit while the guard is up, dimmed while it recovers.
        // A barrier shows what is left of it.
        _guardName.Text = Player.Weapon?.Guard is not { } guard ? NoSkill
            : guard.Barrier != null ? $"{guard.Name}  {Player.Vitals.Barrier}"
            : guard.Name;
        _guardName.Modulate = Player.IsGuarding ? UiTheme.GoldHi : Colors.White;
        _guardDim.Visible = Player.GuardRecoveryLeft > 0f;

        _dashRecharge.Text = charges < DashRules.Charges ? $"{Player.NextDashIn:F1}" : "";

        int potion = Player.Vitals.PotionCharges;
        for (int i = 0; i < _potionPips.Length; i++)
        {
            _potionPips[i].Color = i < potion ? PotionPip : new Color(UiTheme.WoodDk, 0.9f);
        }

        float drink = Player.DrinkCooldownLeft;
        _potionCooldown.Text = drink > 0f ? $"{drink:F1}" : "";
        _potionDim.Visible = potion == 0;

        for (int i = 0; i < SkillSlots.Length; i++)
        {
            float cooldown = Player.CooldownRemaining(SkillSlots[i].Skill);
            _slots[i].Cooldown.Text = cooldown > 0f ? $"{cooldown:F1}" : "";
            _slots[i].Dim.Visible = !Player.CanUse(SkillSlots[i].Skill);
        }
    }

    private Control BuildPlayerFrame()
    {
        var frame = new PanelContainer { Position = new Vector2(Margin, Margin), MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Wood, UiTheme.Gold, radius: 4, margin: 10f, borderWidth: 2));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        frame.AddChild(column);
        // No wider than the bars under it: an enchanted weapon's name would stretch the frame across the notice.
        _name = UiTheme.MakeLabel("Knight", UiTheme.Words, 18, UiTheme.GoldHi, outline: 2);
        _name.CustomMinimumSize = new Vector2(BarWidth, 0f);
        _name.ClipText = true;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(_name);

        var (healthRoot, health, healthText) = UiTheme.MakeValueBar(UiTheme.BarHp, new Vector2(BarWidth, 20f), 14);
        health.MaxValue = PlayerRules.MaxHp;
        (_health, _healthText) = (health, healthText);
        column.AddChild(healthRoot);

        var (manaRoot, mana, manaText) = UiTheme.MakeValueBar(UiTheme.BarMp, new Vector2(BarWidth, 14f), 11);
        (_mana, _manaText) = (mana, manaText);
        column.AddChild(manaRoot);

        var stats = new HBoxContainer();
        stats.AddThemeConstantOverride("separation", 14);

        // ARM is the tier of armour worn, from 0.
        string[] names = { "STR", "WIS", "AGI", "ARM" };
        for (int i = 0; i < names.Length; i++)
        {
            var stat = new HBoxContainer();
            stat.AddThemeConstantOverride("separation", 5);
            stat.AddChild(UiTheme.MakeLabel(names[i], UiTheme.Words, 13, UiTheme.TextMain));
            _statValues[i] = UiTheme.MakeLabel("", UiTheme.Numbers, 16, UiTheme.GoldHi);
            stat.AddChild(_statValues[i]);
            stats.AddChild(stat);
        }

        column.AddChild(stats);

        // Earned, with nothing to spend them on yet.
        var purse = new HBoxContainer();
        purse.AddThemeConstantOverride("separation", 18);
        foreach (var (name, value) in new[] { ("Gold", _gold), ("Souls", _souls), ("Orbs", _orbs) })
        {
            var earned = new HBoxContainer();
            earned.AddThemeConstantOverride("separation", 5);
            earned.AddChild(UiTheme.MakeLabel(name, UiTheme.Words, 13, UiTheme.TextMain));
            earned.AddChild(value);
            purse.AddChild(earned);
        }

        column.AddChild(purse);
        column.AddChild(_status);
        return frame;
    }

    private Control BuildSkillBar()
    {
        var bar = new HBoxContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetBottom = -Margin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bar.AddThemeConstantOverride("separation", 8);

        for (int i = 0; i < SkillSlots.Length; i++)
        {
            var (slot, column, name) = BarSlot(140f, SkillSlots[i].Key);
            var types = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            types.AddThemeConstantOverride("separation", 4);
            var cost = UiTheme.MakeLabel("", UiTheme.Numbers, 12, ManaText);
            cost.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(types);
            column.AddChild(cost);

            var dim = Dim();
            slot.AddChild(dim);
            var cooldown = UiTheme.MakeLabel("", UiTheme.Numbers, 22, Colors.White, outline: 4);
            cooldown.HorizontalAlignment = HorizontalAlignment.Center;
            cooldown.VerticalAlignment = VerticalAlignment.Center;
            slot.AddChild(cooldown);

            _slots[i] = (name, types, cost, cooldown, dim);
            bar.AddChild(slot);
        }

        bar.AddChild(BuildGuardSlot());
        bar.AddChild(BuildDashSlot());
        bar.AddChild(BuildPotionSlot());
        bar.AddChild(BuildSwapSlot());
        return bar;
    }

    // A pip per charge, lit while available, the seconds until the next one comes back, and the lunge it turns into.
    private Control BuildDashSlot()
    {
        var (slot, column, _) = BarSlot(110f, "SPACE", "Dash");
        var pips = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pips.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < _dashPips.Length; i++)
        {
            _dashPips[i] = new ColorRect { CustomMinimumSize = new Vector2(16f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore };
            pips.AddChild(_dashPips[i]);
        }

        column.AddChild(pips);
        column.AddChild(Hint("+ LMB: lunge"));

        _dashRecharge = UiTheme.MakeLabel("", UiTheme.Numbers, 14, Colors.White, outline: 3);
        _dashRecharge.HorizontalAlignment = HorizontalAlignment.Right;
        _dashRecharge.VerticalAlignment = VerticalAlignment.Top;
        slot.AddChild(_dashRecharge);
        return slot;
    }

    // The health potion: a pip per charge, lit while it is there, dimmed with none left, and the seconds until it can
    // be drunk again.
    private Control BuildPotionSlot()
    {
        var (slot, column, _) = BarSlot(100f, "R", "Potion");
        var pips = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pips.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < _potionPips.Length; i++)
        {
            _potionPips[i] = new ColorRect { CustomMinimumSize = new Vector2(12f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore };
            pips.AddChild(_potionPips[i]);
        }

        column.AddChild(pips);
        column.AddChild(Hint($"+{HealthPotion.Heal} HP"));
        _potionDim = Dim();
        slot.AddChild(_potionDim);
        _potionCooldown = UiTheme.MakeLabel("", UiTheme.Numbers, 14, Colors.White, outline: 3);
        _potionCooldown.HorizontalAlignment = HorizontalAlignment.Right;
        _potionCooldown.VerticalAlignment = VerticalAlignment.Top;
        slot.AddChild(_potionCooldown);
        return slot;
    }

    private Control BuildGuardSlot()
    {
        var (slot, column, name) = BarSlot(110f, "SHIFT");
        _guardName = name;
        column.AddChild(Hint("hold"));
        _guardDim = Dim();
        slot.AddChild(_guardDim);
        return slot;
    }

    // The other weapon set, on the back: what Tab swaps to.
    private Control BuildSwapSlot()
    {
        var (slot, column, name) = BarSlot(120f, "TAB");
        _backWeapon = name;
        column.AddChild(Hint("on your back"));
        return slot;
    }

    // A framed slot of the skill bar with its key over its name; what else it shows goes under them in the column.
    private static (PanelContainer Slot, VBoxContainer Column, Label Name) BarSlot(float width, string key, string name = "")
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(width, SlotHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        slot.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.WoodDk, UiTheme.Gold, radius: 4, margin: 6f, borderWidth: 2));
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var keyLabel = UiTheme.MakeLabel(key, UiTheme.Numbers, 12, UiTheme.GoldDk);
        keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var nameLabel = UiTheme.MakeLabel(name, UiTheme.Words, 15, UiTheme.TextMain);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(keyLabel);
        column.AddChild(nameLabel);
        slot.AddChild(column);
        return (slot, column, nameLabel);
    }

    private static Label Hint(string text)
    {
        var hint = UiTheme.MakeLabel(text, UiTheme.Words, 11, UiTheme.GoldDk);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        return hint;
    }

    private static ColorRect Dim() => new() { Color = new Color(0f, 0f, 0f, 0.55f), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };

    // With an empty hand the slots have no skill to name; they stay dimmed, since nothing can be used.
    private void ShowSkills(WeaponDefinition? weapon)
    {
        for (int i = 0; i < SkillSlots.Length; i++)
        {
            var skill = weapon?.Skill(SkillSlots[i].Skill);
            _slots[i].Name.Text = skill?.Name ?? NoSkill;
            ShowTypes(_slots[i].Types, skill);
            _slots[i].Cost.Text = skill is { ManaCost: > 0 } ? $"{skill.ManaCost} MP{(skill.Channeled ? " / turn" : "")}" : "";
            _slots[i].Cost.Visible = skill is { ManaCost: > 0 };
        }
    }

    // The damage types a skill is of, each in its own colour: one, or two for a blow with an enchanted weapon.
    private static void ShowTypes(HBoxContainer row, SkillDefinition? skill)
    {
        row.FreeChildren();
        foreach (var type in skill?.Types ?? System.Array.Empty<DamageType>())
        {
            if (row.GetChildCount() > 0)
            {
                row.AddChild(UiTheme.MakeLabel("+", UiTheme.Words, 11, UiTheme.GoldDk));
            }

            row.AddChild(UiTheme.MakeLabel(type.ToString(), UiTheme.Words, 11, DamageTypeColours.Name(type)));
        }
    }

    private static string NameOf(WeaponDefinition? weapon, string empty) => weapon?.Name ?? empty;
}
