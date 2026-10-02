using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// --armour-lineup: the player's figure stood in a row in the open field, once for each tier of armour (or for each
// outfit named, to try others), each named overhead, with a camera of its own in front of them at chest height. For
// looking at armour, which the game's own cameras see from too far above. Used with --screenshot.
public partial class ArmourLineup : Node3D
{
    private const float Apart = 1.7f;
    private const float EyeHeight = 1.25f;
    private const float FieldOfView = 40f;
    private const float LabelHeight = 2.45f;

    // The middle of the field between the two fortresses, clear of both.
    private static readonly Vector3 Middle = Vector3.Zero;

    private readonly IReadOnlyList<string> _outfits;
    private Camera3D _camera = null!;

    public ArmourLineup(IReadOnlyList<string> outfits)
    {
        _outfits = outfits;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public ArmourLineup()
        : this(Array.Empty<string>())
    {
    }

    public override void _Ready()
    {
        var shown = _outfits.Count > 0
            ? _outfits.Select(outfit => (Label: outfit, Dress: (Action<Skeleton3D>)(s => ArmourLook.WearOutfit(s, outfit, Array.Empty<string>())))).ToList()
            : Armours.All.Select(a => (Label: $"{a.Tier}  {a.Name}", Dress: (Action<Skeleton3D>)(s => ArmourLook.Wear(s, a.Tier)))).ToList();

        float width = (shown.Count - 1) * Apart;
        for (int i = 0; i < shown.Count; i++)
        {
            var at = Middle + Vector3.Right * (i * Apart - width / 2f);
            AddChild(Figure(at, shown[i].Dress));
            AddChild(Sign(shown[i].Label, at));
        }

        // Far enough back for the whole row to fit across the frame, with a figure's width to spare each side.
        float halfAcross = width / 2f + Apart;
        float aspect = GetViewport().GetVisibleRect().Size.Aspect();
        float back = halfAcross / (Mathf.Tan(Mathf.DegToRad(FieldOfView) / 2f) * aspect);
        _camera = new Camera3D { Name = "LineupCamera", Fov = FieldOfView, Position = Middle + new Vector3(0f, EyeHeight, back) };
        AddChild(_camera);
        _camera.LookAt(Middle + Vector3.Up * EyeHeight);
    }

    // The game's camera takes the view back as it follows the player, so this one takes it again each frame.
    public override void _Process(double delta) => _camera.MakeCurrent();

    private static Node3D Figure(Vector3 at, Action<Skeleton3D> dress)
    {
        // KayKit models face +Z, toward the camera.
        var body = Assets.Instantiate(PlayerCharacter.ModelPath);
        body.Position = at;
        CharacterRig.ShrinkHead(body);
        dress(body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath));

        // Libraries go in before the figure enters the tree; playing first would crash (Everdawn godot-pitfalls.md).
        var animation = new AnimationPlayer { Name = "AnimationPlayer" };
        RigAnimations.AddTo(animation);
        body.AddChild(animation);
        animation.Autoplay = RigAnimations.UnarmedIdle;
        return body;
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
}
