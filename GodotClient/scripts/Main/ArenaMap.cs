using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Level;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Where a seller stands in the town and which way they face.
public sealed record SellerSpot(SellerDefinition Seller, Vector3 Position, float Yaw);

// The arena's two fortresses and what they mean in play: the allied town, where players start, get back up and are
// safe, and the enemy fortress, where the waves rise. Each is a courtyard with a room off either side. Built on every
// machine from the same two layout files. The floors (the ground, and what is hidden above the camera's subject) are
// in ArenaMap.Floors.cs, the ways round the walls in ArenaMap.Ways.cs. Design: Docs/Design/level-layouts.md.
public partial class ArenaMap : Node3D
{
    public const string NodeName = "Map";

    private const string TownPath = "res://config/levels/allied-town.layout.json";
    private const string FortressPath = "res://config/levels/enemy-fortress.layout.json";

    // A layout's marker named this and a seller's id is where that seller stands.
    private const string SellerMarker = "seller-";

    // Where a training dummy stands (TrainingDummy), facing the marker's way.
    private const string DummyMarker = "training-dummy";

    // The areas that make up the ground inside a fortress's walls.
    private static readonly string[] InsideAreas = { "courtyard", "room", "door" };

    // The barrier across the town gate that only skeletons run into.
    private const float WardHeight = 4f;

    // A wall in the way of the camera fades to this, at this rate.
    private const float FadedTransparency = 0.8f;
    private const float FadeRate = 5f;
    private const float FadeMargin = 0.4f;
    private const float ChestHeight = 1.2f;

    private readonly List<Occluder> _occluders = new();
    private readonly List<Hanging> _hangings = new();
    private List<Vector3> _playerSpawns = new();
    private List<Vector3> _enemySpawns = new();
    private List<LayoutArea> _safe = new();
    private List<LayoutArea> _fortressGround = new();
    private List<Vector3> _rooms = new();
    private List<SellerSpot> _sellers = new();
    private List<LayoutMarker> _markers = new();
    private float _clock;

    // The camera whose view walls are faded out of, and who it is looking at.
    public Camera3D? Camera { get; set; }

    public Node3D? CameraSubject { get; set; }

    public IReadOnlyList<Vector3> EnemySpawns => _enemySpawns;

    // The enemy fortress's courtyard, and the middle of its gate.
    public LayoutArea FortressCourtyard { get; private set; } = null!;

    public Vector3 FortressGate { get; private set; }

    // A spot to stand on in each room of both fortresses.
    public IReadOnlyList<Vector3> Rooms => _rooms;

    public IReadOnlyList<SellerSpot> SellerSpots => _sellers;

    // The crypt's guard spots, and where its treasure is left.
    public IReadOnlyList<Vector3> CryptGuards { get; private set; } = Array.Empty<Vector3>();

    public Vector3 CryptTreasure { get; private set; }

    // A spot in the crypt, and one upstairs in each of the town's storeys.
    public Vector3 Crypt { get; private set; }

    public IReadOnlyList<Vector3> Upstairs { get; private set; } = Array.Empty<Vector3>();

    public int FadedWalls => _occluders.Count(o => o.Meshes.Count > 0 && o.Meshes[0].Transparency > 0.01f);

    public int Hangings => _hangings.Count;

    public int FadedHangings => _hangings.Count(h => h.Meshes[0].Transparency > 0.01f);

    public static ArenaMap In(SceneTree tree) => tree.CurrentScene.GetNode<ArenaMap>(NodeName);

    // The town's layout and the fortress's, in that order.
    public IReadOnlyList<LevelLayout> Layouts { get; private set; } = Array.Empty<LevelLayout>();

    // Called once the map is in the scene, not from _Ready: an exception thrown there is logged by the engine and
    // the game carries on without a map, where this one stops the arena.
    public void Build()
    {
        var town = LevelLayoutNode.Load(TownPath);
        var fortress = LevelLayoutNode.Load(FortressPath);
        AddChild(town);
        AddChild(fortress);
        Layouts = new[] { town.Layout, fortress.Layout };
        _occluders.AddRange(town.Occluders.Concat(fortress.Occluders));
        _hangings.AddRange(town.Hangings.Concat(fortress.Hangings));
        _markers = town.Layout.Markers.Concat(fortress.Layout.Markers).ToList();

        _playerSpawns = town.Layout.MarkersNamed("player-spawn").Select(ToGodot).ToList();
        _enemySpawns = fortress.Layout.MarkersNamed("enemy-spawn").Select(ToGodot).ToList();
        _safe = town.Layout.AreasNamed("safe").ToList();
        FortressGate = ToGodot(fortress.Layout.MarkersNamed("gate").Single());
        FortressCourtyard = fortress.Layout.AreasNamed("courtyard").Single();
        _fortressGround = InsideAreas.SelectMany(fortress.Layout.AreasNamed).ToList();
        _rooms = new[] { town, fortress }.SelectMany(l => l.Layout.MarkersNamed("room")).Select(ToGodot).ToList();
        CryptGuards = fortress.Layout.MarkersNamed("crypt-guard").Select(ToGodot).ToList();
        CryptTreasure = ToGodot(fortress.Layout.MarkersNamed("crypt-treasure").Single());
        Crypt = ToGodot(fortress.Layout.MarkersNamed("crypt").Single());
        Upstairs = town.Layout.MarkersNamed("upstairs").Select(ToGodot).ToList();
        BuildFloors(town, fortress);
        if (_playerSpawns.Count == 0 || _enemySpawns.Count == 0 || _safe.Count == 0)
        {
            throw new InvalidOperationException(
                $"The fortress layouts lack what the arena needs: {_playerSpawns.Count} player spawns, {_enemySpawns.Count} enemy spawns, "
                + $"{_safe.Count} safe areas");
        }

        foreach (var gate in town.Layout.AreasNamed("gate"))
        {
            AddChild(BuildWard(gate));
        }

        // A seller is walked round like any solid, so each is stood here before the ways are worked out.
        _sellers = town.Layout.MarkersStarting(SellerMarker)
            .Select(m => new SellerSpot(SellerNamed(m.Who), ToGodot(m.Marker.Position), m.Marker.Yaw))
            .ToList();
        foreach (var spot in _sellers)
        {
            var body = new StaticBody3D { Name = $"{spot.Seller.Id}Body", Position = spot.Position, CollisionLayer = CollisionLayers.World, CollisionMask = 0 };
            body.AddChild(CharacterRig.CreateCapsule());
            AddChild(body);
        }

        // So are the training dummies, the same on every machine by their order in the layout.
        int dummies = 0;
        foreach (var marker in town.Layout.Markers.Where(m => m.Name == DummyMarker))
        {
            AddChild(TrainingDummy.Create($"Dummy{dummies++}", ToGodot(marker.Position), marker.Yaw));
        }

        // And the crates and barrels that break, numbered alike on every machine by their order in the layouts.
        int breakables = 0;
        foreach (var layout in new[] { town, fortress })
        {
            foreach (var piece in layout.Breakables)
            {
                AddChild(Breakable.Create($"Breakable{breakables++}", piece with { Box = layout.Transform * piece.Box }));
            }
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

        Cover(CameraSubject.GlobalPosition);

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

    // The index-th marker of that name across both layouts, the town's first: where --start-at puts the players.
    public Vector3 MarkerSpot(string name, int index)
    {
        var spots = _markers.Where(m => m.Name == name).Select(m => ToGodot(m.Position)).ToList();
        return index < spots.Count ? spots[index]
            : throw new InvalidOperationException($"--start-at {name}:{index}: the layouts have {spots.Count} marker(s) named '{name}' "
                + $"(markers: {string.Join(", ", _markers.Select(m => m.Name).Distinct())})");
    }

    // Where a player gets back up.
    public Vector3 RevivePointFor(long peerId) => PlayerSpawnFor((int)(peerId % _playerSpawns.Count));

    // Inside the town's walls, its rooms included: skeletons neither come here nor aim at anyone here, and nothing of
    // theirs hurts here.
    public bool IsSafe(Vector3 position) => _safe.Any(a => a.Contains(Yaw.ToGround(position)));

    // Inside the enemy fortress's walls: its courtyard, its rooms and the doorways between.
    public bool InFortress(Vector3 position) => _fortressGround.Any(a => a.Contains(Yaw.ToGround(position)));

    private static SellerDefinition SellerNamed(string id)
    {
        try
        {
            return Sellers.ById(id);
        }
        catch (KeyNotFoundException e)
        {
            throw new InvalidOperationException($"The town's layout stands a seller nobody knows: {e.Message}", e);
        }
    }

    private static StaticBody3D BuildWard(LayoutArea gate)
    {
        var size = new Vector3(gate.Max.X - gate.Min.X, WardHeight, gate.Max.Y - gate.Min.Y);
        var ward = new StaticBody3D
        {
            Name = "Ward",
            Position = new Vector3(gate.Centre.X, WardHeight / 2f, gate.Centre.Y),
            CollisionLayer = CollisionLayers.Ward,
            CollisionMask = 0,
        };
        ward.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        return ward;
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
