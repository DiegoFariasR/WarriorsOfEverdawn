using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// --armour-lineup, --weapon-lineup, --magic-lineup: the player's figure stood in a row in the open field, each named
// overhead, with a camera of its own in front of them. For looking at what a figure wears, holds and casts, which the
// game's own cameras see from too far above: every tier of armour (or the outfits named, to try others); one weapon
// in its stance, its guard, each skill at the moment it lands, on the back, and lying on the ground; or the staffs,
// each holding its spell on an area with what it throws beside it, or each inside its barrier. Used with
// --screenshot.
public partial class Lineup : Node3D
{
    private const float EyeHeight = 1.25f;
    private const float FieldOfView = 40f;
    private const float LabelHeight = 2.45f;

    // Room for a figure standing, and for one with a weapon held out.
    private const float ArmourApart = 1.7f;
    private const float WeaponApart = 2.2f;

    // How far in front of the row the weapon lies.
    private const float GroundInFront = 3.5f;

    // Staffs casting: room between them for each one's area, shown smaller than it is so several fit a frame, and
    // a camera high enough to see the ground the spells land on.
    private const float SpellApart = 4.4f;
    private const float SpellEyeHeight = 5f;
    private const float SpellCycle = 0.3f;
    private static readonly AreaDefinition ShownArea = new(Distance: 2.6f, Radius: 1.7f);

    // The middle of the field between the two fortresses, clear of both.
    private static readonly Vector3 Middle = Vector3.Zero;

    private readonly IReadOnlyList<Figure> _figures;
    private readonly float _apart;
    private readonly WeaponDefinition? _onGround;
    private readonly float _eyeHeight;
    private Camera3D _camera = null!;

    private Lineup(IReadOnlyList<Figure> figures, float apart, WeaponDefinition? onGround = null, float eyeHeight = EyeHeight)
    {
        _figures = figures;
        _apart = apart;
        _onGround = onGround;
        _eyeHeight = eyeHeight;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public Lineup()
        : this(Array.Empty<Figure>(), ArmourApart)
    {
    }

    // The tiers of armour, or with outfits named, those.
    public static Lineup OfArmour(IReadOnlyList<string> outfits) => new(
        outfits.Count > 0
            ? outfits.Select(outfit => new Figure(outfit, body => ArmourLook.WearOutfit(SkeletonOf(body), outfit), RigAnimations.UnarmedIdle)).ToList()
            : Armours.All.Select(a => new Figure($"{a.Tier}  {a.Name}", body => ArmourLook.Wear(SkeletonOf(body), a.Tier), RigAnimations.UnarmedIdle)).ToList(),
        ArmourApart)
    {
        Name = "ArmourLineup",
    };

    // One weapon: how it is stood with and guarded with, each skill held at the moment it lands (a lunge at the end
    // of its thrust), how it is carried on the back, and how it lies on the ground, in front of the row.
    public static Lineup OfWeapon(WeaponDefinition weapon)
    {
        var look = CombatVisuals.LookFor(weapon);
        void Armed(Node3D body)
        {
            CharacterRig.AttachToHand(body, look);
            CharacterRig.AttachOffHand(body, look);
        }

        return new Lineup(
            new Figure[]
            {
                new("Stance", Armed, look.OneHanded ? RigAnimations.UnarmedIdle : RigAnimations.Idle),
                new(weapon.Guard.Name, Armed, RigAnimations.Guard),
                new(weapon.Primary.Name, Armed, CombatVisuals.ClipFor(weapon.Primary), weapon.Primary.HitTime),
                new(weapon.Secondary.Name, Armed, CombatVisuals.ClipFor(weapon.Secondary), weapon.Secondary.HitTime),
                new(weapon.Lunge.Name, Armed, CombatVisuals.ClipFor(weapon.Lunge), weapon.Lunge.SweepEnd ?? weapon.Lunge.HitTime),
                new("On the back", body => CharacterRig.AttachToBack(body, look), RigAnimations.UnarmedIdle, Turned: true),
            },
            WeaponApart,
            weapon)
        {
            Name = "WeaponLineup",
        };
    }

    // The staffs of the elements named (all six with none named). Casting: each holds its spell on an area in front
    // of it, with what it throws hanging at its side. With barrier, each stands inside its barrier instead.
    public static Lineup OfMagic(IReadOnlyList<Element> elements, bool barrier)
    {
        var shown = elements.Count > 0 ? elements : Elements.All;
        return new Lineup(
            shown.Select(element => barrier ? Shielded(element) : Casting(element)).ToList(),
            barrier ? WeaponApart : SpellApart,
            eyeHeight: barrier ? EyeHeight : SpellEyeHeight)
        {
            Name = "MagicLineup",
        };
    }

    private static Figure Casting(Element element)
    {
        var staff = Weapons.Staff(element);
        var look = CombatVisuals.LookFor(staff);
        return new Figure($"{staff.Primary.Name}\n{staff.Secondary.Name}", body => CharacterRig.AttachToHand(body, look), RigAnimations.MagicChannel)
        {
            Staged = body =>
            {
                var area = new SpellArea { Name = "SpellArea" };
                body.AddChild(area);
                area.Hold(element, ShownArea, SpellCycle);
                area.MoveTo(body.GlobalPosition + Vector3.Back * ShownArea.Distance);
                var thrown = Bolts.Thrown(staff.Primary, element, Vector3.Right);
                thrown.Position = new Vector3(-1.1f, 1.3f, 0.3f);
                body.AddChild(thrown);
            },
        };
    }

    private static Figure Shielded(Element element)
    {
        var staff = Weapons.Staff(element);
        var look = CombatVisuals.LookFor(staff);
        return new Figure(staff.Name, body => CharacterRig.AttachToHand(body, look), RigAnimations.Guard)
        {
            Staged = body =>
            {
                var bubble = new BarrierBubble();
                body.AddChild(bubble);
                bubble.Show(element, 1f);
            },
        };
    }

    public override void _Ready()
    {
        float width = (_figures.Count - 1) * _apart;
        for (int i = 0; i < _figures.Count; i++)
        {
            var at = Middle + Vector3.Right * (i * _apart - width / 2f);
            Stand(_figures[i], at);
            AddChild(Sign(_figures[i].Label, at));
        }

        if (_onGround != null)
        {
            AddChild(GroundWeapons.Lying(_onGround, Middle + Vector3.Back * GroundInFront, Mathf.Pi / 2f));
        }

        // Far enough back for the whole row to fit across the frame, with a figure's width to spare each side.
        float halfAcross = width / 2f + _apart;
        float aspect = GetViewport().GetVisibleRect().Size.Aspect();
        float back = halfAcross / (Mathf.Tan(Mathf.DegToRad(FieldOfView) / 2f) * aspect);
        _camera = new Camera3D { Name = "LineupCamera", Fov = FieldOfView, Position = Middle + new Vector3(0f, _eyeHeight, back) };
        AddChild(_camera);
        _camera.LookAt(Middle + Vector3.Up * EyeHeight);
    }

    // The game's camera takes the view back as it follows the player, so this one takes it again each frame.
    public override void _Process(double delta) => _camera.MakeCurrent();

    private static Skeleton3D SkeletonOf(Node3D body) => body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);

    private void Stand(Figure figure, Vector3 at)
    {
        // KayKit models face +Z, toward the camera.
        var body = Assets.Instantiate(PlayerCharacter.ModelPath);
        ToonLook.Apply(body);
        body.Position = at;
        body.Rotation = new Vector3(0f, figure.Turned ? Mathf.Pi : 0f, 0f);
        CharacterRig.ShrinkHead(body);
        ArmourLook.Wear(SkeletonOf(body), 0);
        figure.Dress(body);

        // Libraries go in before the figure enters the tree; playing first would crash (Everdawn godot-pitfalls.md).
        var animation = new AnimationPlayer { Name = "AnimationPlayer" };
        RigAnimations.AddTo(animation);
        body.AddChild(animation);
        AddChild(body);
        animation.Play(figure.Clip);
        if (figure.HeldAt is { } moment)
        {
            animation.Seek(moment, update: true);
            animation.Pause();
        }

        figure.Staged?.Invoke(body);
    }

    private static Label3D Sign(string text, Vector3 at) => new()
    {
        Text = text,
        Font = UiTheme.Words,
        FontSize = 40,
        OutlineSize = 10,
        Modulate = UiTheme.GoldHi,
        OutlineModulate = Colors.Black,
        PixelSize = 0.004f,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
        Position = at + Vector3.Up * LabelHeight,
    };

    // One figure of the row: what it is called, how it is dressed and armed, the clip it plays, and for a pose the
    // moment of the clip it is held at. Turned shows its back. Staged sets up what stands about it, once it is in
    // the row.
    private sealed record Figure(string Label, Action<Node3D> Dress, string Clip, double? HeldAt = null, bool Turned = false)
    {
        public Action<Node3D>? Staged { get; init; }
    }
}
