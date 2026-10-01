using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Level;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// The arena's two fortresses and what they mean in play: the allied town, where players start, get back up and are
// safe, and the enemy fortress, where the waves rise. Built on every machine from the same two layout files. Also
// the way round the walls: a navigation mesh baked from the solids, for skeletons and bots.
// Design: Docs/Design/level-layouts.md.
public partial class ArenaMap : Node3D
{
    public const string NodeName = "Map";

    private const string TownPath = "res://config/levels/allied-town.layout.json";
    private const string FortressPath = "res://config/levels/enemy-fortress.layout.json";

    // A walker's path is worked out again this often, or when it reaches the corner it was heading for.
    private const float PathLifetime = 0.25f;
    private const float CornerReached = 0.4f;

    // The barrier across the town gate that only skeletons run into.
    private const float WardHeight = 4f;

    // A wall in the way of the camera fades to this, at this rate.
    private const float FadedTransparency = 0.8f;
    private const float FadeRate = 5f;
    private const float FadeMargin = 0.4f;
    private const float ChestHeight = 1.2f;

    private readonly Dictionary<ulong, (Vector3 Next, float Until)> _steps = new();
    private readonly List<Occluder> _occluders = new();
    private readonly List<Hanging> _hangings = new();
    private List<Vector3> _playerSpawns = new();
    private List<Vector3> _enemySpawns = new();
    private List<LayoutArea> _safe = new();
    private List<LayoutArea> _courtyards = new();
    private float _clock;

    // The camera whose view walls are faded out of, and who it is looking at.
    public Camera3D? Camera { get; set; }

    public Node3D? CameraSubject { get; set; }

    public IReadOnlyList<Vector3> EnemySpawns => _enemySpawns;

    public IReadOnlyList<LayoutArea> SafeAreas => _safe;

    // The ground inside the enemy fortress's walls, and the middle of its gate.
    public LayoutArea Fortress => _courtyards[^1];

    public Vector3 FortressGate { get; private set; }

    // Walkers that asked for a way and got none (the mesh not ready, or no way through): they went straight.
    public int StraightSteps { get; private set; }

    public int FadedWalls => _occluders.Count(o => o.Meshes.Count > 0 && o.Meshes[0].Transparency > 0.01f);

    public int Hangings => _hangings.Count;

    public int FadedHangings => _hangings.Count(h => h.Meshes[0].Transparency > 0.01f);

    public static ArenaMap In(SceneTree tree) => tree.CurrentScene.GetNode<ArenaMap>(NodeName);

    // Called once the map is in the scene, not from _Ready: an exception thrown there is logged by the engine and
    // the game carries on without a map, where this one stops the arena.
    public void Build()
    {
        var town = LevelLayoutNode.Load(TownPath);
        var fortress = LevelLayoutNode.Load(FortressPath);
        AddChild(town);
        AddChild(fortress);
        _occluders.AddRange(town.Occluders.Concat(fortress.Occluders));
        _hangings.AddRange(town.Hangings.Concat(fortress.Hangings));

        _playerSpawns = town.Layout.MarkersNamed("player-spawn").Select(ToGodot).ToList();
        _enemySpawns = fortress.Layout.MarkersNamed("enemy-spawn").Select(ToGodot).ToList();
        _safe = town.Layout.AreasNamed("safe").ToList();
        FortressGate = ToGodot(fortress.Layout.MarkersNamed("gate").Single());
        _courtyards = new[] { town, fortress }.SelectMany(l => l.Layout.AreasNamed("courtyard")).ToList();
        if (_playerSpawns.Count == 0 || _enemySpawns.Count == 0 || _safe.Count == 0 || _courtyards.Count != 2)
        {
            throw new InvalidOperationException(
                $"The fortress layouts lack what the arena needs: {_playerSpawns.Count} player spawns, {_enemySpawns.Count} enemy spawns, "
                + $"{_safe.Count} safe areas, {_courtyards.Count} courtyards");
        }

        foreach (var gate in town.Layout.AreasNamed("gate"))
        {
            AddChild(BuildWard(gate));
        }

        BakeWays();
    }

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        if (Camera == null || CameraSubject == null || !IsInstanceValid(CameraSubject))
        {
            return;
        }

        var eye = Camera.GlobalPosition;
        var chest = CameraSubject.GlobalPosition + Vector3.Up * ChestHeight;
        foreach (var occluder in _occluders)
        {
            Fade(occluder.Meshes, occluder.Blocks(eye, chest, FadeMargin), (float)delta);
        }

        foreach (var hanging in _hangings)
        {
            Fade(hanging.Meshes, hanging.On.Any(o => o.Blocks(eye, chest, FadeMargin)), (float)delta);
        }
    }

    private static void Fade(IReadOnlyList<MeshInstance3D> meshes, bool inTheWay, float delta)
    {
        float target = inTheWay ? FadedTransparency : 0f;
        foreach (var mesh in meshes)
        {
            mesh.Transparency = Mathf.MoveToward(mesh.Transparency, target, FadeRate * delta);
        }
    }

    // Where the n-th player to join starts: a spot of its own while there are enough.
    public Vector3 PlayerSpawnFor(int slot) => _playerSpawns[Mathf.PosMod(slot, _playerSpawns.Count)];

    // Where a player gets back up.
    public Vector3 RevivePointFor(long peerId) => PlayerSpawnFor((int)(peerId % _playerSpawns.Count));

    // Inside the town's walls: skeletons neither come here nor aim at anyone here, and nothing of theirs hurts here.
    public bool IsSafe(Vector3 position) => _safe.Any(a => a.Contains(Yaw.ToGround(position)));

    // The direction to walk from where `walker` stands toward `to`, round the walls. Straight there when the way is
    // clear, when the navigation mesh has no path, or before it is ready (counted in StraightSteps).
    public Vector3 StepToward(Node3D walker, Vector3 to)
    {
        var from = walker.GlobalPosition;
        ulong id = walker.GetInstanceId();
        if (!_steps.TryGetValue(id, out var step) || _clock >= step.Until || Flat(step.Next - from).Length() < CornerReached)
        {
            step = (NextCorner(from, to), _clock + PathLifetime);
            _steps[id] = step;
        }

        var direction = Flat(step.Next - from);
        return direction.LengthSquared() > 1e-4f ? direction.Normalized() : Vector3.Zero;
    }

    public void Forget(Node3D walker) => _steps.Remove(walker.GetInstanceId());

    private Vector3 NextCorner(Vector3 from, Vector3 to)
    {
        var path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, from, to, optimize: true);
        foreach (var corner in path)
        {
            if (Flat(corner - from).Length() >= CornerReached)
            {
                return corner;
            }
        }

        if (path.Length == 0)
        {
            StraightSteps++;
        }

        return to;
    }

    // Walkable ground is whatever the solids leave of the World layer; the ward is not on it, so players' ways go
    // through the town gate. Skeletons never aim at anyone inside, so theirs do not.
    private void BakeWays()
    {
        var mesh = new NavigationMesh
        {
            AgentRadius = 0.5f,
            AgentHeight = 2f,
            AgentMaxClimb = 0.25f,
            CellSize = 0.25f,
            CellHeight = 0.25f,
            GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
            GeometryCollisionMask = CollisionLayers.World,
        };
        var source = new NavigationMeshSourceGeometryData3D();
        NavigationServer3D.ParseSourceGeometryData(mesh, source, GetParent());
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);
        if (mesh.GetPolygonCount() == 0)
        {
            throw new InvalidOperationException("The arena's navigation mesh came out empty; nothing could find its way round the walls");
        }

        AddChild(new NavigationRegion3D { Name = "Ways", NavigationMesh = mesh });
        GD.Print($"[level] navigation: {mesh.GetPolygonCount()} polygons");
    }

    private static StaticBody3D BuildWard(LayoutArea gate)
    {
        var size = new Vector3(gate.Max.X - gate.Min.X, WardHeight, gate.Max.Y - gate.Min.Y);
        var ward = new StaticBody3D
        {
            Name = "Ward",
            Position = new Vector3((gate.Min.X + gate.Max.X) / 2f, WardHeight / 2f, (gate.Min.Y + gate.Max.Y) / 2f),
            CollisionLayer = CollisionLayers.Ward,
            CollisionMask = 0,
        };
        ward.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        return ward;
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
