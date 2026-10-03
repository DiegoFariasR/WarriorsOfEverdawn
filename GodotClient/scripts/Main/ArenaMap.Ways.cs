using System;
using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// The ways round the walls: a navigation mesh baked from the solids, for skeletons and bots.
public partial class ArenaMap
{
    // A walker's path is worked out again this often, or when it reaches the corner it was heading for.
    private const float PathLifetime = 0.25f;
    private const float CornerReached = 0.4f;

    private readonly Dictionary<ulong, (Vector3 Next, float Until)> _steps = new();

    // Walkers that asked for a way and got none (the mesh not ready, or no way through): they went straight.
    public int StraightSteps { get; private set; }

    public NavigationMesh Ways { get; private set; } = null!;

    // Whether the ways lead from one spot to the other, round the walls, through the gates and doorways and up or down
    // the stairs: a way that ends under or over the spot, on another floor, does not.
    public bool HasWay(Vector3 from, Vector3 to)
    {
        var path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, from, to, optimize: true);
        return path.Length > 0 && Yaw.Flat(path[^1] - to).Length() < CornerReached && Floors.SameLevel(path[^1].Y, to.Y);
    }

    // The direction to walk from where `walker` stands toward `to`, round the walls. Straight there when the way is
    // clear, when the navigation mesh has no path, or before it is ready (counted in StraightSteps).
    public Vector3 StepToward(Node3D walker, Vector3 to)
    {
        var from = walker.GlobalPosition;
        ulong id = walker.GetInstanceId();
        if (!_steps.TryGetValue(id, out var step) || _clock >= step.Until || Yaw.Flat(step.Next - from).Length() < CornerReached)
        {
            step = (NextCorner(from, to), _clock + PathLifetime);
            _steps[id] = step;
        }

        var direction = Yaw.Flat(step.Next - from);
        return direction.LengthSquared() > 1e-4f ? direction.Normalized() : Vector3.Zero;
    }

    public void Forget(Node3D walker) => _steps.Remove(walker.GetInstanceId());

    private Vector3 NextCorner(Vector3 from, Vector3 to)
    {
        var path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, from, to, optimize: true);
        foreach (var corner in path)
        {
            if (Yaw.Flat(corner - from).Length() >= CornerReached)
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
            GeometryCollisionMask = CollisionLayers.Solid,
            // Partitioned by watershed (the default), a floor over a flight of stairs and the floor under it can come
            // out as one region, with polygons from one to the other through the air; layers never overlap.
            SamplePartitionType = NavigationMesh.SamplePartitionTypeEnum.Layers,
        };
        var source = new NavigationMeshSourceGeometryData3D();
        NavigationServer3D.ParseSourceGeometryData(mesh, source, GetParent());
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);
        if (mesh.GetPolygonCount() == 0)
        {
            throw new InvalidOperationException("The arena's navigation mesh came out empty; nothing could find its way round the walls");
        }

        Ways = mesh;
        AddChild(new NavigationRegion3D { Name = "Ways", NavigationMesh = mesh });
        GD.Print($"[level] navigation: {mesh.GetPolygonCount()} polygons");
    }
}
