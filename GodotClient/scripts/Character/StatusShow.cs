using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// What a body's statuses look like on the body itself, on every machine: a block of ice round it while it is
// frozen, and a word over it as it is frozen or stunned. The statuses themselves are named over its HP bar
// (OverheadBars) and on the player's own frame (Hud).
public partial class StatusShow : Node3D
{
    private const float BlockHeight = 2.1f;

    // Clear enough to see who is in it.
    private static readonly Color Ice = new(0.6f, 0.85f, 1f, 0.4f);

    private MeshInstance3D? _block;
    private Statuses _shown;

    // The block of ice is up.
    public bool Frozen => _block != null;

    // Call-outs made since the node was made: a body frozen or stunned.
    public int CallOuts { get; private set; }

    public void Reflect(Statuses now)
    {
        if (now == _shown)
        {
            return;
        }

        var gained = now & ~_shown;
        foreach (var lost in new[] { Statuses.Frozen, Statuses.Stunned })
        {
            if (gained.HasFlag(lost))
            {
                CallOuts++;
                var body = GetParent<Node3D>();
                // Drawn through floors, so only over a body on the camera's subject's floor.
                if (ArenaMap.In(GetTree()).OnSubjectsFloor(body.GlobalPosition))
                {
                    FloatingText.Spawn(body, $"{StatusLooks.NameOf(lost)}!", StatusLooks.ColourOf(lost));
                }
            }
        }

        bool frozen = now.HasFlag(Statuses.Frozen);
        if (frozen && _block == null)
        {
            _block = new MeshInstance3D
            {
                Name = "Ice",
                Mesh = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.65f, Height = BlockHeight, RadialSegments = 6 },
                Position = Vector3.Up * BlockHeight / 2f,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = Ice,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
                MaterialOverlay = ElementLooks.Overlay(Element.Ice),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_block);
        }
        else if (!frozen && _block != null)
        {
            _block.QueueFree();
            _block = null;
        }

        _shown = now;
    }
}
