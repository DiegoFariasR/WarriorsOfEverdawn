using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Level;

// Builds a level layout in the scene: every placement, with the texture its layout gives its kit, and a box on the
// World layer for each solid. Adapted from Everdawn's LevelLayoutNode; format in Docs/Design/level-layouts.md.
public partial class LevelLayoutNode : Node3D
{
    // A solid at least this tall can stand between the camera and a character; lower ones never hide one.
    private const float OccludingHeight = 2.5f;

    // A piece that is not solid, sits this close to a tall solid and has its middle this far off the ground hangs on
    // it (a banner, a torch on a wall). The floor under a statue and the bones at a wall's foot do not.
    private const float HungWithin = 0.5f;
    private const float HungAbove = 1f;

    private readonly List<Occluder> _occluders = new();
    private readonly List<Hanging> _hangings = new();

    public LevelLayout Layout { get; private set; } = null!;

    // The tall solids, each with its box in world space, for fading the ones in the camera's way.
    public IReadOnlyList<Occluder> Occluders => _occluders;

    // What hangs on them and fades with them: left opaque, a banner on a faded wall still hides what the wall did.
    public IReadOnlyList<Hanging> Hangings => _hangings;

    // Fails loudly: a level that cannot be read is no level to play.
    public static LevelLayoutNode Load(string resPath)
    {
        if (!FileAccess.FileExists(resPath))
        {
            throw new InvalidOperationException($"No level layout at {resPath}; generate it with ./dev.sh level-fortresses");
        }

        var layout = LevelLayout.Parse(FileAccess.GetFileAsString(resPath), resPath);
        var node = new LevelLayoutNode { Name = resPath.GetFile().GetBaseName().Replace(".layout", ""), Layout = layout };
        node.Build(resPath);
        return node;
    }

    private void Build(string resPath)
    {
        var scenes = Layout.Assets.ToDictionary(a => a.Key, a => Assets.Load<PackedScene>(a.Value));
        var materials = Layout.Textures.ToDictionary(t => t.Key, t => new StandardMaterial3D
        {
            AlbedoTexture = Assets.Load<Texture2D>(t.Value),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        });

        var loose = new List<(string Asset, Vector3 Centre, List<MeshInstance3D> Meshes)>();
        foreach (var placement in Layout.Placements)
        {
            var piece = scenes[placement.Asset].Instantiate<Node3D>();
            piece.Transform = new Transform3D(
                new Basis(ToGodot(placement.Rotation)) * Basis.FromScale(ToGodot(placement.Scale)), ToGodot(placement.Position));
            AddChild(piece);

            var meshes = piece.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>()
                .Concat(piece is MeshInstance3D own ? new[] { own } : Array.Empty<MeshInstance3D>())
                .ToList();
            string path = Layout.Assets[placement.Asset];
            // The longest prefix wins, so one kind of piece can wear another texture than the rest of its kit.
            var material = materials.Where(m => path.StartsWith(m.Key, StringComparison.Ordinal))
                .OrderByDescending(m => m.Key.Length)
                .Select(m => m.Value)
                .FirstOrDefault();
            if (material != null)
            {
                foreach (var mesh in meshes)
                {
                    mesh.MaterialOverride = material;
                }
            }

            if (placement.Solid)
            {
                AddSolid(piece, meshes);
            }
            else if (meshes.Count > 0)
            {
                loose.Add((placement.Asset, piece.Transform * BoundsOf(piece, meshes).GetCenter(), meshes));
            }
        }

        var hung = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (asset, centre, meshes) in loose)
        {
            var on = centre.Y < HungAbove ? new List<Occluder>() : _occluders.Where(o => o.Holds(centre, HungWithin)).ToList();
            if (on.Count > 0)
            {
                _hangings.Add(new Hanging(on, meshes));
                hung[asset] = hung.GetValueOrDefault(asset) + 1;
            }
        }

        foreach (var mesh in Layout.Meshes)
        {
            AddChild(BuildMesh(mesh));
        }

        foreach (var light in Layout.Lights)
        {
            var color = new Color(light.LinearColor.X, light.LinearColor.Y, light.LinearColor.Z).LinearToSrgb();
            AddChild(new OmniLight3D { Position = ToGodot(light.Position), LightColor = color, LightEnergy = light.Energy, OmniRange = light.Range });
        }

        GD.Print($"[level] {resPath}: {Layout.Placements.Count} placements ({Layout.Placements.Count(p => p.Solid)} solid, "
            + $"{_hangings.Count} hung on walls), {Layout.Markers.Count} markers, {Layout.Areas.Count} areas");
        GD.Print($"[level] {resPath}: hung on walls: {string.Join(", ", hung.Select(h => $"{h.Value} {h.Key}"))}");
    }

    // The body sits beside the piece, not under it: a piece may be scaled, and physics bodies must not be. Its box is
    // the piece's own, turned with it.
    private void AddSolid(Node3D piece, List<MeshInstance3D> meshes)
    {
        var bounds = BoundsOf(piece, meshes);
        var scale = piece.Scale;
        var size = bounds.Size * scale;
        var turned = new Transform3D(piece.Basis.Orthonormalized(), piece.Position);
        var body = new StaticBody3D
        {
            Name = $"{piece.Name}Solid",
            Transform = turned * new Transform3D(Basis.Identity, bounds.GetCenter() * scale),
            CollisionLayer = CollisionLayers.World,
            CollisionMask = 0,
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);

        if (size.Y >= OccludingHeight)
        {
            _occluders.Add(new Occluder(body.Transform, size, meshes));
        }
    }

    // The piece's meshes together, in the piece's own space (before its scale).
    private static Aabb BoundsOf(Node3D piece, List<MeshInstance3D> meshes)
    {
        Aabb? box = null;
        foreach (var mesh in meshes)
        {
            var inPiece = RelativeTransform(mesh, piece) * mesh.GetAabb();
            box = box?.Merge(inPiece) ?? inPiece;
        }

        return box ?? throw new InvalidOperationException($"Piece {piece.Name} has no mesh to take its size from");
    }

    private static Transform3D RelativeTransform(Node3D node, Node3D ancestor)
    {
        var transform = Transform3D.Identity;
        for (var current = node; current != ancestor; current = current.GetParent<Node3D>())
        {
            transform = current.Transform * transform;
        }

        return transform;
    }

    private static MeshInstance3D BuildMesh(LayoutMesh m)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var face in m.Faces)
        {
            // Layout polygons are counter-clockwise; Godot treats clockwise as front-facing.
            for (int k = 1; k + 1 < face.Count; k++)
            {
                st.AddVertex(ToGodot(m.Vertices[face[0]]));
                st.AddVertex(ToGodot(m.Vertices[face[k + 1]]));
                st.AddVertex(ToGodot(m.Vertices[face[k]]));
            }
        }

        st.GenerateNormals();
        var albedo = new Color(m.LinearColor.X, m.LinearColor.Y, m.LinearColor.Z).LinearToSrgb();
        return new MeshInstance3D
        {
            Name = m.Name,
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = albedo, Roughness = 0.9f },
        };
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private static Quaternion ToGodot(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
}

// A tall solid: its box (centred on Box, Size across) and the meshes to fade when it is in the camera's way.
public sealed record Occluder(Transform3D Box, Vector3 Size, IReadOnlyList<MeshInstance3D> Meshes)
{
    // Whether the straight line between two points passes through the box, grown by margin on every side.
    public bool Blocks(Vector3 from, Vector3 to, float margin)
    {
        var toBox = Box.AffineInverse();
        return Grown(margin).IntersectsSegment(toBox * from, toBox * to);
    }

    // Whether a point is inside the box, grown by margin on every side.
    public bool Holds(Vector3 point, float margin) => Grown(margin).HasPoint(Box.AffineInverse() * point);

    private Aabb Grown(float margin) => new(-Size / 2f - Vector3.One * margin, Size + Vector3.One * margin * 2f);
}

// A piece hung on tall solids (one wall, or two where it sits on their join): it fades when any of them does.
public sealed record Hanging(IReadOnlyList<Occluder> On, IReadOnlyList<MeshInstance3D> Meshes);
