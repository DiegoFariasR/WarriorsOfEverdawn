using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Main;

// A label over each weapon on the ground the local player is close to: its name, and for the one the player would
// take (GroundWeapons.AttendedBy: the one it faces) what it does in this player's hands and whether it can be taken. Screen-space, placed each frame from the weapon's position.
public partial class GroundWeaponLabels : Control
{
    private const float LabelHeight = 0.7f;

    private readonly Dictionary<int, Entry> _labels = new();

    public Camera3D? Camera { get; set; }

    public PlayerCharacter? Player { get; set; }

    // The weapons labelled right now, and the one showing its details.
    public IReadOnlyCollection<int> Shown => _labels.Keys;

    public int? Detailed { get; private set; }

    // Whether the prompt on show offers the weapon (a slot is free) or says the slots are full; null with no prompt.
    public bool? OffersPickUp { get; private set; }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        var near = Near().ToList();
        foreach (int gone in _labels.Keys.Where(id => near.All(n => n.Id != id)).ToList())
        {
            _labels[gone].Panel.QueueFree();
            _labels.Remove(gone);
        }

        var attended = near.Count > 0 ? Ground()!.AttendedBy(Player!.GlobalPosition, Player.AimYaw) : null;
        Detailed = attended?.Id;
        OffersPickUp = null;
        foreach (var item in near)
        {
            if (!_labels.TryGetValue(item.Id, out var entry))
            {
                entry = _labels[item.Id] = Create(item.Weapon);
            }

            bool detailed = item == attended;
            entry.Details.Visible = detailed;
            entry.Prompt.Visible = detailed && DistanceTo(item) <= Pickups.Reach;
            if (detailed)
            {
                entry.Details.Text = DetailsOf(item.Weapon, Player!.Stats);
                bool free = new WeaponSets(Player.Weapon, Player.StowedWeapon).HasFreeSlot;
                OffersPickUp = entry.Prompt.Visible ? free : null;
                entry.Prompt.Text = free ? "F  -  pick up" : "Hands and back are full  -  G drops";
                entry.Prompt.Modulate = free ? UiTheme.GoldHi : UiTheme.StatusFallen;
            }

            // A panel outside a container keeps its size when its content shrinks.
            entry.Panel.ResetSize();
            var above = item.Position + Vector3.Up * LabelHeight;
            entry.Panel.Visible = !Camera!.IsPositionBehind(above);
            var at = Camera.UnprojectPosition(above);
            entry.Panel.Position = new Vector2(at.X - entry.Panel.Size.X / 2f, at.Y - entry.Panel.Size.Y);
        }
    }

    // What a weapon does in the hands of a player with these stats.
    public static string DetailsOf(WeaponDefinition weapon, CharacterStats stats) =>
        $"{weapon.Primary.Name} {DamageOf(weapon.Primary, stats)}  -  {(weapon.Primary.Projectile == null ? "reach" : "flies")} {weapon.Primary.Range:F1}\n"
        + $"{weapon.Secondary.Name} {DamageOf(weapon.Secondary, stats)} a turn  -  {weapon.Secondary.ManaCost} MP\n"
        + $"{weapon.Lunge.Name} {DamageOf(weapon.Lunge, stats)}  -  "
        + (weapon.Guard.Barrier is { } barrier ? $"barrier takes {barrier.Strength}" : $"guard stops {(1f - weapon.Guard.DamageTaken) * 100f:F0}%");

    // What a skill deals in these hands; a volley as so many darts of so much: "3 x 12".
    public static string DamageOf(SkillDefinition skill, CharacterStats stats) =>
        skill.Projectiles > 1 ? $"{skill.Projectiles} x {StatRules.Damage(skill, stats)}" : StatRules.Damage(skill, stats).ToString();

    private IEnumerable<GroundWeapon> Near()
    {
        if (Camera == null || Player == null || !IsInstanceValid(Player))
        {
            return Enumerable.Empty<GroundWeapon>();
        }

        return Ground() is { } ground ? ground.Items.Where(i => DistanceTo(i) <= Pickups.LabelRange) : Enumerable.Empty<GroundWeapon>();
    }

    private GroundWeapons? Ground() => GetTree().CurrentScene.GetNodeOrNull<GroundWeapons>(GroundWeapons.NodeName);

    private float DistanceTo(GroundWeapon item)
    {
        var offset = item.Position - Player!.GlobalPosition;
        return new Vector2(offset.X, offset.Z).Length();
    }

    private Entry Create(WeaponDefinition weapon)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(UiTheme.WoodDk, 0.8f), UiTheme.GoldDk, radius: 3, margin: 5f));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 1);
        var name = UiTheme.MakeLabel(weapon.Name, UiTheme.Words, 14, UiTheme.GoldHi, outline: 2);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        var details = UiTheme.MakeLabel("", UiTheme.Words, 11, UiTheme.TextMain);
        details.HorizontalAlignment = HorizontalAlignment.Center;
        var prompt = UiTheme.MakeLabel("", UiTheme.Words, 12, Colors.White, outline: 2);
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(name);
        column.AddChild(details);
        column.AddChild(prompt);
        panel.AddChild(column);
        AddChild(panel);
        return new Entry(panel, details, prompt);
    }

    private sealed record Entry(PanelContainer Panel, Label Details, Label Prompt);
}
