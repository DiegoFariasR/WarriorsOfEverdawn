using System;
using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Characters;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// --armour-lineup, --weapon-lineup, --magic-lineup, --look-lineup: figures stood in a row in the open field, each
// named overhead, with a camera of its own in front of them. For looking at what a figure is, wears, holds and
// casts, which the game's own cameras see from too far above: the player's in every tier of armour (or the outfits
// named, to try others); with one weapon in its stance, its guard, each skill at the moment it lands, on the back,
// and lying on the ground; with the staffs, each holding its spell on an area with what it throws beside it, or
// each inside its barrier; or other figures altogether: the game's cast, a character of the catalogue as it was
// made, figures drawn at random from a pool. --gold-lineup stands no figure: it lays every pile gold falls in
// in a row on the ground. Used with --screenshot.
public partial class Lineup : Node3D
{
    private const float EyeHeight = 1.25f;
    private const float FieldOfView = 40f;
    private const float LabelHeight = 2.45f;

    // Room for a figure standing, and for one with a weapon held out.
    private const float ArmourApart = 1.7f;
    private const float WeaponApart = 2.2f;

    // What --look-lineup shows for each thing it is given: the game's own figures, or this many drawn from a pool.
    private const string Cast = "cast";
    private const string Bare = ":bare";
    private const int RolledPerPool = 8;

    // How far in front of the row the weapon lies.
    private const float GroundInFront = 3.5f;

    // Staffs casting: room between them for each one's area, shown smaller than it is so several fit a frame, and
    // a camera high enough to see the ground the spells land on.
    private const float SpellApart = 4.4f;
    private const float SpellEyeHeight = 5f;

    // Piles of gold: near enough to each other for all ten to be seen at a size coins can be counted at.
    private const float GoldApart = 0.7f;
    private const float GoldLabelHeight = 0.45f;
    private const float SpellCycle = 0.3f;
    private static readonly AreaDefinition ShownArea = new(Distance: 2.6f, Radius: 1.7f);

    // The middle of the field between the two fortresses, clear of both.
    private static readonly Vector3 Middle = Vector3.Zero;

    private readonly IReadOnlyList<Figure> _figures;
    private readonly float _apart;
    private readonly WeaponDefinition? _onGround;
    private readonly float _eyeHeight;
    private readonly float _labelHeight;
    private readonly float _lookHeight;
    private Camera3D _camera = null!;

    private Lineup(
        IReadOnlyList<Figure> figures,
        float apart,
        WeaponDefinition? onGround = null,
        float eyeHeight = EyeHeight,
        float labelHeight = LabelHeight,
        float lookHeight = EyeHeight)
    {
        _labelHeight = labelHeight;
        _lookHeight = lookHeight;
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
            ? outfits.Select(outfit => new Figure(outfit, _ => { }, RigAnimations.UnarmedIdle) { Look = ArmourLook.DressedAs(PlayerCharacter.Look, outfit) }).ToList()
            : Armours.All.Select(a => new Figure($"{a.Tier}  {a.Name}", _ => { }, RigAnimations.UnarmedIdle) { Look = ArmourLook.Dressed(PlayerCharacter.Look, a.Tier) }).ToList(),
        ArmourApart)
    {
        Name = "ArmourLineup",
    };

    // The pile each amount of gold lies in, from one coin to the most a pile shows, each under its amount, seen
    // from that high over the ground: low for how they look from behind a player, high for how they look from
    // above.
    public static Lineup OfGold(float eyeHeight) => new(
        Enumerable.Range(1, LootRules.MostCoinsShown)
            .Select(gold => new Figure(gold.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => { }, "") { Thing = () => Loot.Pile(gold) })
            .ToList(),
        GoldApart,
        eyeHeight: eyeHeight,
        labelHeight: GoldLabelHeight,
        lookHeight: 0f)
    {
        Name = "GoldLineup",
    };

    // Figures by what they are, each thing named giving one or more: "cast" the player, the enemies and the sellers
    // as the game has them; a pool's name ("townsfolk", or "townsfolk@40" to start at that seed) eight figures
    // drawn from it, each named by its seed; a character of the catalogue ("Druid") that character as it was made,
    // with everything it can wear, or with nothing on ("Druid:bare").
    public static Lineup OfLooks(IReadOnlyList<string> shown) => new(shown.SelectMany(Figures).ToList(), ArmourApart)
    {
        Name = "LookLineup",
    };

    private static IEnumerable<Figure> Figures(string shown)
    {
        static Figure Standing(string label, CharacterLook look) => new(label, _ => { }, RigAnimations.UnarmedIdle) { Look = look };

        if (shown == Cast)
        {
            return new[] { Standing("player", ArmourLook.Dressed(PlayerCharacter.Look, 0)) }
                .Concat(Enemies.All.Select(enemy => Standing(enemy.Id, CombatVisuals.LookFor(enemy).Figure)))
                .Concat(Sellers.All.Select(seller => Standing(seller.Name, SellerNpc.LookOf(seller)
                    ?? throw new KeyNotFoundException($"No figure for the seller '{seller.Id}'"))));
        }

        string[] pool = shown.Split('@');
        if (LookPools.All.Any(p => p.Name == pool[0]))
        {
            int first = pool.Length > 1 ? int.Parse(pool[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
            return Enumerable.Range(first, RolledPerPool)
                .Select(seed => Standing($"{pool[0]} {seed}", LookRandomizer.Roll(CharacterBody.Catalog, LookPools.ByName(pool[0]), seed)));
        }

        var catalog = CharacterBody.Catalog;
        bool bare = shown.EndsWith(Bare, StringComparison.Ordinal);
        string origin = bare ? shown[..^Bare.Length] : shown;
        if (!catalog.Origins.Contains(origin))
        {
            throw new ArgumentException($"'{shown}' is not '{Cast}', a pool ({string.Join(", ", LookPools.All.Select(p => p.Name))}) or a character of the catalogue");
        }

        // As it was made: its faceless head with its face where it has one, and all it can wear.
        var whole = CharacterLook.Of(origin);
        var head = catalog.Parts.FirstOrDefault(p => p.Origin == origin && PartsCatalog.IsShell(p)) ?? catalog.Get(whole.Head);
        var made = whole with
        {
            Head = head.Stem,
            Face = catalog.FaceFor(head),
            Accessories = catalog.In(CharacterSlot.Accessory).Where(p => !bare && p.Origin == origin && !IsFacePiece(p))
                .Select(p => p.Stem).OrderBy(s => s, StringComparer.Ordinal).ToList(),
        };
        return new[] { Standing(shown, made) };
    }

    private static bool IsFacePiece(PartEntry part) => part.Stem.EndsWith("_Eyes", StringComparison.Ordinal) || part.Stem.EndsWith("_Jaw", StringComparison.Ordinal);

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

    // The staffs of the elements named (all with none named; more than six run wider than the field can frame). Casting: each holds its spell on an area in front
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
                var thrown = Bolts.Thrown(staff.Primary, Vector3.Right);
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
            AddChild(Sign(_figures[i].Label, at + Vector3.Up * _labelHeight));
        }

        if (_onGround != null)
        {
            AddChild(GroundWeapons.Lying(_onGround, Middle + Vector3.Back * GroundInFront, Mathf.Pi / 2f));
        }

        // Far enough back for the whole row to fit across the frame, with a figure's width to spare each side.
        float halfAcross = width / 2f + _apart;
        float aspect = GetViewport().GetVisibleRect().Size.Aspect();
        float back = halfAcross / (Mathf.Tan(Mathf.DegToRad(FieldOfView) / 2f) * aspect);

        // Looking down at the ground, the camera keeps that distance from the row however high it is, so what
        // lies there is the same size on screen from low and from high.
        if (_lookHeight == 0f)
        {
            back = Mathf.Sqrt(Mathf.Max(back * back - _eyeHeight * _eyeHeight, 0.01f));
        }

        _camera = new Camera3D { Name = "LineupCamera", Fov = FieldOfView, Position = Middle + new Vector3(0f, _eyeHeight, back) };
        AddChild(_camera);
        _camera.LookAt(Middle + Vector3.Up * _lookHeight);
    }

    // The game's camera takes the view back as it follows the player, so this one takes it again each frame.
    public override void _Process(double delta) => _camera.MakeCurrent();

    private void Stand(Figure figure, Vector3 at)
    {
        if (figure.Thing is { } made)
        {
            var thing = made();
            thing.Position = at;
            AddChild(thing);
            return;
        }

        // KayKit models face +Z, toward the camera.
        var body = CharacterBody.Build(figure.Look ?? ArmourLook.Dressed(PlayerCharacter.Look, 0));
        body.Position = at;
        body.Rotation = new Vector3(0f, figure.Turned ? Mathf.Pi : 0f, 0f);
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
        Position = at,
    };

    // One figure of the row: what it is called, how it is armed, the clip it plays, and for a pose the moment of
    // the clip it is held at. Turned shows its back. Look is what it is; without one it is the player's figure in
    // what everyone starts in. Staged sets up what stands about it, once it is in the row.
    private sealed record Figure(string Label, Action<Node3D> Dress, string Clip, double? HeldAt = null, bool Turned = false)
    {
        public CharacterLook? Look { get; init; }

        // What lies or stands in the figure's place, when it is no figure at all.
        public Func<Node3D>? Thing { get; init; }

        public Action<Node3D>? Staged { get; init; }
    }
}
