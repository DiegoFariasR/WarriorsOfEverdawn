using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Level;

// Builds a level layout in the scene: every placement, with the texture its layout gives its kit, and a body on the
// World layer for each solid (its box, its mesh for a piece that is walked through, like a doorway, or a wedge for a
// flight of stairs). Adapted from Everdawn's LevelLayoutNode; format in Docs/Design/level-layouts.md.
public partial class LevelLayoutNode : Node3D
{
    // A solid at least this tall can stand between the camera and a character; lower ones never hide one. So can a
    // loose piece that hangs on nothing and lies wholly higher than this: a storey's stone top.
    private const float OccludingHeight = 2.5f;

    // A piece that is not solid, sits this close to a tall solid and has its middle this far off the ground hangs on
    // it (a banner, a torch on a wall). The floor under a statue and the bones at a wall's foot do not.
    private const float HungWithin = 0.5f;
    private const float HungAbove = 1f;

    private readonly List<Occluder> _occluders = new();
    private readonly List<Hanging> _hangings = new();
    private readonly List<LevelPiece> _pieces = new();
    private readonly List<LevelLight> _lights = new();
    private readonly List<BreakablePiece> _breakables = new();

    public LevelLayout Layout { get; private set; } = null!;

    // The tall solids and the loose pieces overhead, each with its box in world space, for fading the ones in the
    // camera's way.
    public IReadOnlyList<Occluder> Occluders => _occluders;

    // What hangs on them and fades with them: left opaque, a banner on a faded wall still hides what the wall did.
    public IReadOnlyList<Hanging> Hangings => _hangings;

    // Every piece and every light, by its floor, for hiding what is above a player who is under it.
    public IReadOnlyList<LevelPiece> Pieces => _pieces;

    public IReadOnlyList<LevelLight> Lights => _lights;

    // The pieces blows break, in the layout's order, with no body yet: the game gives each its own (Main/Breakable).
    public IReadOnlyList<BreakablePiece> Breakables => _breakables;

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

        var loose = new List<(string Asset, Node3D Piece, Vector3 Centre, List<MeshInstance3D> Meshes)>();
        foreach (var placement in Layout.Placements)
        {
            var piece = scenes[placement.Asset].Instantiate<Node3D>();
            piece.Transform = new Transform3D(
                new Basis(ToGodot(placement.Rotation)) * Basis.FromScale(ToGodot(placement.Scale)), ToGodot(placement.Position));
            AddChild(piece);

            var meshes = piece.Meshes()
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

            if (meshes.Count > 0)
            {
                var middle = piece.Transform * BoundsOf(piece, meshes).GetCenter();
                _pieces.Add(new LevelPiece(placement.Level, new Vector2(middle.X, middle.Z), meshes, placement.View));
                if (!placement.Solid)
                {
                    loose.Add((placement.Asset, piece, middle, meshes));
                }
            }

            if (placement.Breakable)
            {
                var (box, size) = BoxOf(piece, BoundsOf(piece, meshes));
                _breakables.Add(new BreakablePiece(piece, meshes, box, size));
            }
            else if (placement.Solid)
            {
                AddSolid(piece, meshes, placement.FollowsMesh, placement.Ramp);
            }
        }

        var hung = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var overhead = new List<Occluder>();
        foreach (var (asset, piece, centre, meshes) in loose)
        {
            var on = centre.Y < HungAbove ? new List<Occluder>() : _occluders.Where(o => o.Holds(centre, HungWithin)).ToList();
            if (on.Count > 0)
            {
                _hangings.Add(new Hanging(on, meshes));
                hung[asset] = hung.GetValueOrDefault(asset) + 1;
            }
            else if (BoxOf(piece, BoundsOf(piece, meshes)) is var (box, size) && centre.Y - size.Y / 2f >= OccludingHeight)
            {
                overhead.Add(new Occluder(box, size, meshes));
            }
        }

        // Only once every hanging is found, so nothing hangs on a piece overhead.
        _occluders.AddRange(overhead);

        foreach (var mesh in Layout.Meshes)
        {
            AddChild(BuildMesh(mesh));
        }

        foreach (var light in Layout.Lights)
        {
            var color = new Color(light.LinearColor.X, light.LinearColor.Y, light.LinearColor.Z).LinearToSrgb();
            var omni = new OmniLight3D { Position = ToGodot(light.Position), LightColor = color, LightEnergy = light.Energy, OmniRange = light.Range };
            AddChild(omni);
            _lights.Add(new LevelLight(omni, Floors.FloorUnder(light.Position.Y), new Vector2(light.Position.X, light.Position.Z)));
        }

        GD.Print($"[level] {resPath}: {Layout.Placements.Count} placements ({Layout.Placements.Count(p => p.Solid)} solid, "
            + $"{_hangings.Count} hung on walls), {Layout.Markers.Count} markers, {Layout.Areas.Count} areas");
        GD.Print($"[level] {resPath}: hung on walls: {string.Join(", ", hung.Select(h => $"{h.Value} {h.Key}"))}");
    }

    // The body sits beside the piece, not under it: a piece may be scaled, and physics bodies must not be. Its shape is
    // the piece's own box, turned with it, the piece's faces, or a flight of stairs. Whichever, the box is what fades
    // when it is tall.
    private void AddSolid(Node3D piece, List<MeshInstance3D> meshes, bool followsMesh, bool ramp)
    {
        var bounds = BoundsOf(piece, meshes);
        var scale = piece.Scale;
        var (box, size) = BoxOf(piece, bounds);
        var turned = new Transform3D(piece.Basis.Orthonormalized(), piece.Position);
        var body = new StaticBody3D
        {
            Name = $"{piece.Name}Solid",
            Transform = followsMesh || ramp ? turned : box,
            CollisionLayer = CollisionLayers.World,
            CollisionMask = 0,
        };
        body.AddChild(new CollisionShape3D
        {
            Shape = ramp ? Wedge(bounds, scale) : followsMesh ? FacesOf(piece, meshes) : new BoxShape3D { Size = size },
        });
        AddChild(body);

        if (size.Y >= OccludingHeight)
        {
            _occluders.Add(new Occluder(box, size, meshes));
        }
    }

    // The piece's own box turned with it, at its scale, and its size.
    private static (Transform3D Box, Vector3 Size) BoxOf(Node3D piece, Aabb bounds) =>
        (new Transform3D(piece.Basis.Orthonormalized(), piece.Position) * new Transform3D(Basis.Identity, bounds.GetCenter() * piece.Scale),
            bounds.Size * piece.Scale);

    // A flight of stairs: a wedge the box's whole width, sloping from the top of its -Z end down to the bottom of its +Z
    // end (the kit's stairs rise toward their own -Z) and solid under the slope, so it is walked up and never under.
    private static ConvexPolygonShape3D Wedge(Aabb bounds, Vector3 scale)
    {
        var lo = bounds.Position * scale;
        var hi = bounds.End * scale;
        return new ConvexPolygonShape3D
        {
            Points = new[]
            {
                new Vector3(lo.X, lo.Y, lo.Z), new Vector3(hi.X, lo.Y, lo.Z),
                new Vector3(lo.X, hi.Y, lo.Z), new Vector3(hi.X, hi.Y, lo.Z),
                new Vector3(lo.X, lo.Y, hi.Z), new Vector3(hi.X, lo.Y, hi.Z),
            },
        };
    }

    // The piece's triangles in its own space, at its scale.
    private static ConcavePolygonShape3D FacesOf(Node3D piece, List<MeshInstance3D> meshes)
    {
        var scaled = new Transform3D(Basis.FromScale(piece.Scale), Vector3.Zero);
        var faces = new List<Vector3>();
        foreach (var mesh in meshes)
        {
            var toBody = scaled * RelativeTransform(mesh, piece);
            faces.AddRange(mesh.Mesh.GetFaces().Select(vertex => toBody * vertex));
        }

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces.ToArray());
        return shape;
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

// A piece by the floor it is on and where its middle is on the ground, with what draws it.
// The cameras that draw it (LayoutView) too.
public sealed record LevelPiece(int Level, Vector2 Ground, IReadOnlyList<MeshInstance3D> Meshes, LayoutView View = LayoutView.All);

// A piece blows break, its meshes, and its box (centred on Box, Size across), which its body is to take.
public sealed record BreakablePiece(Node3D Piece, IReadOnlyList<MeshInstance3D> Meshes, Transform3D Box, Vector3 Size);

// A light by the floor under it and where it is on the ground.
public sealed record LevelLight(OmniLight3D Light, int Level, Vector2 Ground);
