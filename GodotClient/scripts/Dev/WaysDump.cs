using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Main;

namespace WarriorsOfEverdawn.Dev;

// Attached with --ways-dump. Once the arena's navigation mesh is baked, prints its polygons with a corner in the area
// asked for ([ways] lines; with no area, only the faulty ones) and quits: 1 when any of them goes through the air.
public partial class WaysDump : Node
{
    // The mesh joins its map a physics frame after it is baked.
    private const int SettleSteps = 3;

    // An edge rising more steeply than this, by more than this, joins two floors: no flight of stairs is that steep,
    // and a floor's own polygons are flat. Watershed partitioning once made such polygons over the town's stairs.
    private const float SteepestEdge = 60f;
    private const float RiseThroughTheAir = 1f;

    private readonly Rect2? _area;
    private int _steps;

    public WaysDump(Rect2? area)
    {
        _area = area;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public WaysDump()
        : this(null)
    {
    }

    public override void _PhysicsProcess(double delta)
    {
        if (++_steps < SettleSteps)
        {
            return;
        }

        SetPhysicsProcess(false);
        var mesh = ArenaMap.In(GetTree()).Ways;
        var polygons = PolygonsOf(mesh).ToList();
        var shown = polygons.Where(p => _area is not { } area || p.Corners.Any(c => area.HasPoint(new Vector2(c.X, c.Z)))).ToList();
        var faulty = shown.Where(p => ThroughTheAir(p.Corners)).ToList();
        foreach (var (index, corners) in _area == null ? faulty : shown)
        {
            GD.Print($"[ways] polygon {index}: {string.Join(" ", corners.Select(c => $"({c.X:F2},{c.Y:F2},{c.Z:F2})"))}"
                + (ThroughTheAir(corners) ? " THROUGH THE AIR" : ""));
        }

        GD.Print($"[ways] {polygons.Count} polygons, {shown.Count} in {(_area is { } a ? $"x {a.Position.X}..{a.End.X}, z {a.Position.Y}..{a.End.Y}" : "the whole map")}, "
            + $"{faulty.Count} through the air");
        GetTree().Quit(faulty.Count == 0 ? 0 : 1);
    }

    public static IEnumerable<(int Index, IReadOnlyList<Vector3> Corners)> PolygonsOf(NavigationMesh mesh)
    {
        var vertices = mesh.GetVertices();
        for (int i = 0; i < mesh.GetPolygonCount(); i++)
        {
            yield return (i, mesh.GetPolygon(i).Select(v => vertices[v]).ToList());
        }
    }

    public static bool ThroughTheAir(IReadOnlyList<Vector3> corners) =>
        corners.Select((c, i) => (c, Next: corners[(i + 1) % corners.Count])).Any(edge =>
        {
            float rise = Mathf.Abs(edge.Next.Y - edge.c.Y);
            float run = new Vector2(edge.Next.X - edge.c.X, edge.Next.Z - edge.c.Z).Length();
            return rise > RiseThroughTheAir && Mathf.RadToDeg(Mathf.Atan2(rise, run)) > SteepestEdge;
        });
}
