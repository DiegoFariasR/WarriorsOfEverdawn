using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Level;

public enum SeatKind
{
    Sit,
    Lie,
}

// A place to sit or lie down: half a bench, a chair, a stool, a bed. Spot is the middle of what is sat or lain on, at
// its height; Facings are the ways someone on it can face, as yaws (Ground).
public sealed record Seat(int Id, SeatKind Kind, Vector3 Spot, IReadOnlyList<float> Facings);

// Where a body goes to sit or lie on a seat: its origin, and the way it faces.
public readonly record struct SeatPose(Vector3 Origin, float Yaw);

// The seats of the level's furniture, read from its pieces where they stand, and where a body goes on one. Which
// pieces are seats, where on them and which ways they are faced from, is here; a layout says only where the pieces
// stand. Design: Docs/Design/level-layouts.md, "Seats".
public static class Seats
{
    // Within this of a seat's spot, across the floor, a player can take it.
    public const float Reach = 1.6f;

    // KayKit's Sit_Chair clips sit a body down onto a seat this far behind its origin (Sit_Chair_Idle's hips, 0.40
    // back) and this high: the furniture of the dungeon kit is a little higher.
    public const float SitBack = 0.4f;
    public const float ClipSeatHeight = 0.42f;

    // KayKit's Lie clips lay a body down on its back along its own facing, head first: a body facing a bed's foot
    // lies with its head on the pillow.
    public const float LieBack = 0f;

    private sealed record Shape(SeatKind Kind, float Height, Vector2[] Spots, Vector2[] Facings);

    private static readonly Vector2 AlongX = new(1f, 0f);
    private static readonly Vector2 AlongZ = new(0f, 1f);

    // By piece, in its own space (x, z), its seats' spots and the ways a body on them faces. A bench seats two side by
    // side and is sat on from either side; a chair from its front, away from its back (on its +x side); a stool from
    // any side; a bed is lain on along its length, the pillow at its -z end.
    private static readonly Dictionary<string, Shape> Shapes = new()
    {
        ["bench"] = new(SeatKind.Sit, 0.5f, new[] { new Vector2(-0.45f, 0f), new Vector2(0.45f, 0f) }, new[] { AlongZ, -AlongZ }),
        ["chair"] = new(SeatKind.Sit, 0.5f, new[] { Vector2.Zero }, new[] { -AlongX }),
        ["stool"] = new(SeatKind.Sit, 0.5f, new[] { Vector2.Zero }, new[] { AlongZ, AlongX, -AlongZ, -AlongX }),
        ["bed_A_single"] = new(SeatKind.Lie, 0.55f, new[] { Vector2.Zero }, new[] { AlongZ }),
    };

    public static bool IsSeat(string asset) => Shapes.ContainsKey(asset);

    // Every seat of the layouts, in their order, numbered from 0 across them all: every machine numbers them alike.
    public static IReadOnlyList<Seat> In(IEnumerable<LevelLayout> layouts)
    {
        var seats = new List<Seat>();
        foreach (var placement in layouts.SelectMany(l => l.Placements))
        {
            if (!Shapes.TryGetValue(placement.Asset, out var shape))
            {
                continue;
            }

            float yaw = YawOf(placement.Rotation);
            foreach (var spot in shape.Spots)
            {
                var turned = Turned(spot, yaw);
                seats.Add(new Seat(seats.Count, shape.Kind, placement.Position + new Vector3(turned.X, shape.Height, turned.Y),
                    shape.Facings.Select(f => Ground.YawOf(Turned(f, yaw))).ToList()));
            }
        }

        return seats;
    }

    // Where a body that comes to the seat from `from` goes on it: sitting, its back to the seat on the side it came
    // from (of the sides the seat is sat on from, the one most toward it); lying, along the seat.
    public static SeatPose PoseOn(Seat seat, Vector3 from)
    {
        var toward = new Vector2(from.X - seat.Spot.X, from.Z - seat.Spot.Z);
        float facing = seat.Facings.MaxBy(f => Vector2.Dot(Ground.Forward(f), toward));
        var forward = Ground.Forward(facing);
        return seat.Kind == SeatKind.Sit
            ? new SeatPose(new Vector3(seat.Spot.X + forward.X * SitBack, seat.Spot.Y - ClipSeatHeight, seat.Spot.Z + forward.Y * SitBack), facing)
            : new SeatPose(new Vector3(seat.Spot.X + forward.X * LieBack, seat.Spot.Y, seat.Spot.Z + forward.Y * LieBack), facing);
    }

    // Whether a player standing there is near enough the seat, on its floor, to take it.
    public static bool InReach(Seat seat, Vector3 position) =>
        Floors.SameLevel(position.Y, seat.Spot.Y) && Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(seat.Spot.X, seat.Spot.Z)) <= Reach;

    // A piece is only ever turned about +Y.
    private static float YawOf(Quaternion q) => 2f * MathF.Atan2(q.Y, q.W);

    // A piece's own (x, z) where it stands turned by `yaw`, as Godot turns it.
    private static Vector2 Turned(Vector2 local, float yaw) =>
        new(local.X * MathF.Cos(yaw) + local.Y * MathF.Sin(yaw), -local.X * MathF.Sin(yaw) + local.Y * MathF.Cos(yaw));
}

// Who sits where, as the host keeps it: a seat holds one player, and a player is on one seat at most.
public sealed class SeatOccupancy
{
    private readonly Dictionary<int, long> _bySeat = new();
    private readonly Dictionary<long, int> _byPlayer = new();

    public IEnumerable<(int Seat, long Player)> Taken => _bySeat.Select(t => (t.Key, t.Value));

    public bool IsTaken(int seat) => _bySeat.ContainsKey(seat);

    public int? SeatOf(long player) => _byPlayer.TryGetValue(player, out int seat) ? seat : null;

    // Whether the player now has the seat: not when it is taken, or the player is on another.
    public bool TrySit(long player, int seat)
    {
        if (_bySeat.ContainsKey(seat) || _byPlayer.ContainsKey(player))
        {
            return false;
        }

        _bySeat[seat] = player;
        _byPlayer[player] = seat;
        return true;
    }

    // The seat the player got up from, if it was on one.
    public int? Stand(long player)
    {
        if (!_byPlayer.Remove(player, out int seat))
        {
            return null;
        }

        _bySeat.Remove(seat);
        return seat;
    }
}
