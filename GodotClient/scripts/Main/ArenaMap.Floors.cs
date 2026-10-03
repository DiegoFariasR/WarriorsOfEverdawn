using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Level;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// The floors: the ground the layouts' holes are cut out of, and what is hidden above the camera's subject.
public partial class ArenaMap
{
    // The ground: the field, a box this thick under its top at height 0, with holes where the layouts have them.
    private const float GroundThickness = 0.2f;
    private static readonly Vector2 GroundHalf = new(35f, 40f);
    private static readonly Color GroundColour = new(0.36f, 0.42f, 0.3f);

    // A group for what stands or lies on a floor and is hidden with it, besides players and skeletons: sellers, gold,
    // orbs, weapons on the ground.
    public const string OnAFloor = "on_a_floor";

    private readonly List<LevelPiece> _pieces = new();
    private readonly List<LevelLight> _lights = new();
    private readonly HashSet<ulong> _hiddenBodies = new();
    private List<LayoutArea> _covers = new();
    private List<LayoutArea> _covering = new();
    private string _coveringKey = "";
    private bool _underground;

    // The sky and light the camera sees the arena in, darkened while its subject is under the ground.
    public Environment? Atmosphere { get; set; }

    // The pieces hidden now, as above the camera's subject, and the floor that subject is on.
    public int HiddenPieces => _pieces.Count(p => !Shown(p.Meshes));

    public int SubjectLevel => CameraSubject != null && IsInstanceValid(CameraSubject) ? Floors.LevelOf(CameraSubject.GlobalPosition.Y) : 0;

    // Whether the camera's subject is under the ground, and the world past the crypt's walls dark.
    public bool Underground => _underground;

    private void BuildFloors(LevelLayoutNode town, LevelLayoutNode fortress)
    {
        _pieces.AddRange(town.Pieces.Concat(fortress.Pieces));
        _lights.AddRange(town.Lights.Concat(fortress.Lights));
        _covers = town.Layout.AreasNamed("cover").Concat(fortress.Layout.AreasNamed("cover")).ToList();
        BuildGround(town.Layout.AreasNamed("hole").Concat(fortress.Layout.AreasNamed("hole")).ToList());
    }

    // What is above the camera's subject, over where it stands, is hidden: every floor from a cover's level up, over
    // the cover's ground, while the subject is below that level and on that ground. A storey over a room hides from
    // a player in the room; the whole ground hides from one in the crypt. Hidden pieces are drawn into the shadows
    // still, so a floor overhead still shades what is under it; lights and bodies up there are not drawn at all.
    private void Cover(Vector3 subject)
    {
        int level = Floors.LevelOf(subject.Y);
        if (level < 0 != _underground && Atmosphere != null)
        {
            _underground = level < 0;
            WorldLook.Underground(Atmosphere, _underground);
        }

        var ground = Yaw.ToGround(subject);
        _covering = _covers.Where(c => c.Level > level && c.Contains(ground)).ToList();
        string key = string.Join(",", _covering.Select(c => _covers.IndexOf(c)));
        if (key != _coveringKey)
        {
            _coveringKey = key;
            foreach (var piece in _pieces)
            {
                Show(piece.Meshes, !Hides(piece.Level, piece.Ground));
            }

            foreach (var light in _lights)
            {
                light.Light.Visible = !Hides(light.Level, light.Ground);
            }
        }

        // Only what this hid is shown again: anything else that hides a body keeps it hidden.
        var tree = GetTree();
        foreach (var body in tree.GetNodesInGroup(PlayerCharacter.Group).Concat(tree.GetNodesInGroup(EnemyCharacter.Group)).Concat(tree.GetNodesInGroup(OnAFloor)).OfType<Node3D>())
        {
            bool hidden = body != CameraSubject && Hides(body.GlobalPosition);
            ulong id = body.GetInstanceId();
            if (hidden && _hiddenBodies.Add(id))
            {
                body.Visible = false;
            }
            else if (!hidden && _hiddenBodies.Remove(id))
            {
                body.Visible = true;
            }
        }
    }

    // Whether a body there is hidden from the camera's subject, as above it.
    public bool Hides(Vector3 position) => Hides(Floors.LevelOf(position.Y), new Vector2(position.X, position.Z));

    private bool Hides(int level, Vector2 ground) =>
        _covering.Any(c => level >= c.Level && c.Contains(new System.Numerics.Vector2(ground.X, ground.Y)));

    // Whether something there is on the camera's subject's floor, or there is no subject. Bars and numbers are drawn
    // over everything, so one over a body on another floor would show through the floor between.
    public bool OnSubjectsFloor(Vector3 position) =>
        CameraSubject == null || !IsInstanceValid(CameraSubject) || Floors.SameLevel(CameraSubject.GlobalPosition.Y, position.Y);

    private static void Show(IReadOnlyList<MeshInstance3D> meshes, bool shown)
    {
        foreach (var mesh in meshes)
        {
            mesh.CastShadow = shown ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
        }
    }

    private static bool Shown(IReadOnlyList<MeshInstance3D> meshes) =>
        meshes.Count == 0 || meshes[0].CastShadow != GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;

    // The field with the holes cut out of it: the ground's rectangle cut along every hole's edges, and every piece of
    // it outside the holes laid as a slab of its own.
    private void BuildGround(IReadOnlyList<LayoutArea> holes)
    {
        var xs = holes.SelectMany(h => new[] { h.Min.X, h.Max.X }).Append(-GroundHalf.X).Append(GroundHalf.X).Distinct().Order().ToList();
        var zs = holes.SelectMany(h => new[] { h.Min.Y, h.Max.Y }).Append(-GroundHalf.Y).Append(GroundHalf.Y).Distinct().Order().ToList();
        var ground = new StaticBody3D { Name = "Ground", CollisionLayer = CollisionLayers.World, CollisionMask = 0 };
        var material = new StandardMaterial3D { AlbedoColor = GroundColour };
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            for (int j = 0; j + 1 < zs.Count; j++)
            {
                var middle = new Vector2((xs[i] + xs[i + 1]) / 2f, (zs[j] + zs[j + 1]) / 2f);
                if (holes.Any(h => h.Contains(new System.Numerics.Vector2(middle.X, middle.Y))))
                {
                    continue;
                }

                var size = new Vector2(xs[i + 1] - xs[i], zs[j + 1] - zs[j]);
                var mesh = new MeshInstance3D
                {
                    Mesh = new PlaneMesh { Size = size },
                    MaterialOverride = material,
                    Position = new Vector3(middle.X, 0f, middle.Y),
                };
                ground.AddChild(mesh);
                ground.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = new Vector3(size.X, GroundThickness, size.Y) },
                    Position = new Vector3(middle.X, -GroundThickness / 2f, middle.Y),
                });
                _pieces.Add(new LevelPiece(0, middle, new[] { mesh }));
            }
        }

        AddChild(ground);
    }
}
