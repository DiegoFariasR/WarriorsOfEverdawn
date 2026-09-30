using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Main;

// Local player's frame (top left: name, HP, mana, stats), skill bar (bottom centre), mode notices (top centre) and
// the overhead bars on skeletons and other players, in Everdawn's style. Layout is first-pass. Design: Docs/Design/ui.md.
public partial class Hud : CanvasLayer
{
    private const float NoticeTime = 2.5f;
    private const float NoticeFade = 0.5f;
    private const float Margin = 16f;
    private const float BarWidth = 260f;

    private static readonly (int Skill, string Key)[] SkillSlots =
    {
        (PlayerCharacter.Primary, "LMB"),
        (PlayerCharacter.Secondary, "RMB"),
    };

    private readonly Label _notice = UiTheme.MakeLabel("", UiTheme.Words, 20, UiTheme.GoldHi, outline: 6);
    private readonly Label _status = UiTheme.MakeLabel("", UiTheme.Words, 13, UiTheme.StatusFallen, outline: 3);
    private readonly Label[] _statValues = new Label[3];
    private readonly (Label Cooldown, ColorRect Dim)[] _slots = new (Label, ColorRect)[SkillSlots.Length];

    // Everdawn's MP colour, for mana costs on the skill bar.
    private static readonly Color ManaText = new(0.55f, 0.78f, 1f);
    private Label _name = null!;
    private readonly ColorRect[] _dodgePips = new ColorRect[DodgeRules.Charges];
    private Label _dodgeRecharge = null!;
    private ProgressBar _health = null!;
    private Label _healthText = null!;
    private ProgressBar _mana = null!;
    private Label _manaText = null!;
    private float _noticeLeft;

    public PlayerCharacter? Player { get; set; }

    public OverheadBars Bars { get; } = new() { Name = "OverheadBars" };

    public Control PlayerFrame { get; private set; } = null!;

    public Control SkillBar { get; private set; } = null!;

    public int ShownHp => (int)_health.Value;

    public int ShownMana => (int)_mana.Value;

    public override void _Ready()
    {
        AddChild(Bars);
        PlayerFrame = BuildPlayerFrame();
        AddChild(PlayerFrame);
        SkillBar = BuildSkillBar();
        AddChild(SkillBar);

        _notice.HorizontalAlignment = HorizontalAlignment.Center;
        _notice.AnchorRight = 1f;
        _notice.OffsetTop = 24f;
        AddChild(_notice);
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

        _name.Text = SessionRules.Pvp ? "Knight  -  PvP" : "Knight";
        int hp = Player.Vitals.Hp;
        _health.Value = hp;
        _healthText.Text = $"{hp} / {PlayerRules.MaxHp}";
        _mana.MaxValue = Player.MaxMana;
        _mana.Value = Player.Mana;
        _manaText.Text = $"{Player.Mana} / {Player.MaxMana}";
        _statValues[0].Text = Player.Stats.Str.ToString();
        _statValues[1].Text = Player.Stats.Wis.ToString();
        _statValues[2].Text = Player.Stats.Agi.ToString();
        _status.Text = Player.IsDowned ? "Down - back up in a moment" : "";

        int charges = Player.DodgeCharges;
        for (int i = 0; i < _dodgePips.Length; i++)
        {
            _dodgePips[i].Color = i < charges ? UiTheme.GoldHi : new Color(UiTheme.WoodDk, 0.9f);
        }

        _dodgeRecharge.Text = charges < DodgeRules.Charges ? $"{Player.NextDodgeIn:F1}" : "";

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
        _name = UiTheme.MakeLabel("Knight", UiTheme.Words, 18, UiTheme.GoldHi, outline: 2);
        column.AddChild(_name);

        var (healthRoot, health, healthText) = UiTheme.MakeValueBar(UiTheme.BarHp, new Vector2(BarWidth, 20f), 14);
        health.MaxValue = PlayerRules.MaxHp;
        (_health, _healthText) = (health, healthText);
        column.AddChild(healthRoot);

        var (manaRoot, mana, manaText) = UiTheme.MakeValueBar(UiTheme.BarMp, new Vector2(BarWidth, 14f), 11);
        (_mana, _manaText) = (mana, manaText);
        column.AddChild(manaRoot);

        var stats = new HBoxContainer();
        stats.AddThemeConstantOverride("separation", 18);
        string[] names = { "STR", "WIS", "AGI" };
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
            var (skill, key) = SkillSlots[i];
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(140f, 62f), MouseFilter = Control.MouseFilterEnum.Ignore };
            slot.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.WoodDk, UiTheme.Gold, radius: 4, margin: 6f, borderWidth: 2));

            var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            var keyLabel = UiTheme.MakeLabel(key, UiTheme.Numbers, 12, UiTheme.GoldDk);
            keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            var name = UiTheme.MakeLabel(Capitalised(PlayerCharacter.SkillSet[skill].Id), UiTheme.Words, 15, UiTheme.TextMain);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(keyLabel);
            column.AddChild(name);
            int manaCost = PlayerCharacter.SkillSet[skill].ManaCost;
            if (manaCost > 0)
            {
                string per = PlayerCharacter.SkillSet[skill].Channeled ? " / turn" : "";
                var cost = UiTheme.MakeLabel($"{manaCost} MP{per}", UiTheme.Numbers, 12, ManaText);
                cost.HorizontalAlignment = HorizontalAlignment.Center;
                column.AddChild(cost);
            }

            slot.AddChild(column);

            var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            slot.AddChild(dim);
            var cooldown = UiTheme.MakeLabel("", UiTheme.Numbers, 22, Colors.White, outline: 4);
            cooldown.HorizontalAlignment = HorizontalAlignment.Center;
            cooldown.VerticalAlignment = VerticalAlignment.Center;
            slot.AddChild(cooldown);

            _slots[i] = (cooldown, dim);
            bar.AddChild(slot);
        }

        bar.AddChild(BuildDodgeSlot());
        return bar;
    }

    // A pip per charge, lit while available, and the seconds until the next one comes back.
    private Control BuildDodgeSlot()
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(110f, 62f), MouseFilter = Control.MouseFilterEnum.Ignore };
        slot.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.WoodDk, UiTheme.Gold, radius: 4, margin: 6f, borderWidth: 2));
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var key = UiTheme.MakeLabel("SPACE", UiTheme.Numbers, 12, UiTheme.GoldDk);
        key.HorizontalAlignment = HorizontalAlignment.Center;
        var name = UiTheme.MakeLabel("Dodge", UiTheme.Words, 15, UiTheme.TextMain);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        var pips = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pips.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < _dodgePips.Length; i++)
        {
            _dodgePips[i] = new ColorRect { CustomMinimumSize = new Vector2(16f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore };
            pips.AddChild(_dodgePips[i]);
        }

        column.AddChild(key);
        column.AddChild(name);
        column.AddChild(pips);
        slot.AddChild(column);

        _dodgeRecharge = UiTheme.MakeLabel("", UiTheme.Numbers, 14, Colors.White, outline: 3);
        _dodgeRecharge.HorizontalAlignment = HorizontalAlignment.Right;
        _dodgeRecharge.VerticalAlignment = VerticalAlignment.Top;
        slot.AddChild(_dodgeRecharge);
        return slot;
    }

    private static string Capitalised(string id) => char.ToUpperInvariant(id[0]) + id[1..];
}
