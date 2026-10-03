using System.IO;
using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Level;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Level;

public class SeatsTests
{
    // A bench along x at the origin, a chair at x 5 turned a quarter (its back, on its own +x side, toward -z), a
    // stool at x 10, a bed at x 15 upstairs, and a barrel.
    private const string Furnished = """
        {
         "version": 1,
         "assets": {"bench": "b", "chair": "c", "stool": "s", "bed_A_single": "d", "barrel_small": "e"},
         "placements": [
          {"asset": "bench", "position": [0, 0, 0], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1], "solid": true},
          {"asset": "chair", "position": [5, 0, 0], "rotation": [0, 0.707107, 0, 0.707107], "scale": [1, 1, 1], "solid": true},
          {"asset": "stool", "position": [10, 0, 0], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1], "solid": true},
          {"asset": "bed_A_single", "position": [15, 4.05, 0], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1], "solid": true},
          {"asset": "barrel_small", "position": [20, 0, 0], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1], "solid": true}
         ]
        }
        """;

    private static readonly LevelLayout Layout = LevelLayout.Parse(Furnished, "furnished");

    [Fact]
    public void A_bench_seats_two_and_a_chair_a_stool_and_a_bed_one_each_numbered_in_order()
    {
        var seats = Seats.In(new[] { Layout });

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, seats.Select(s => s.Id));
        Assert.Equal(new[] { SeatKind.Sit, SeatKind.Sit, SeatKind.Sit, SeatKind.Sit, SeatKind.Lie }, seats.Select(s => s.Kind));
        Assert.All(seats.Take(2), s => Assert.Equal(0f, s.Spot.Z, precision: 4));
        Assert.True(seats[0].Spot.X < 0f && seats[1].Spot.X > 0f);
        Assert.True(seats[4].Spot.Y > Layout.Placements[3].Position.Y);
        Assert.False(Seats.IsSeat("barrel_small"));
    }

    [Fact]
    public void A_sitter_has_its_back_to_the_seat_on_the_side_it_came_from_on_the_seats_height()
    {
        var bench = Seats.In(new[] { Layout })[0];

        var fromFront = Seats.PoseOn(bench, bench.Spot + new Vector3(0f, 0f, 1f));
        var fromBack = Seats.PoseOn(bench, bench.Spot + new Vector3(0.3f, 0f, -1f));

        AssertFacing(new Vector2(0f, 1f), fromFront.Yaw);
        AssertFacing(new Vector2(0f, -1f), fromBack.Yaw);
        Assert.Equal(bench.Spot.Z + Seats.SitBack, fromFront.Origin.Z, precision: 4);
        Assert.Equal(bench.Spot.Z - Seats.SitBack, fromBack.Origin.Z, precision: 4);
        Assert.Equal(bench.Spot.Y - Seats.ClipSeatHeight, fromFront.Origin.Y, precision: 4);
    }

    [Fact]
    public void A_chair_is_sat_on_from_its_front_whatever_side_one_comes_from_and_a_stool_from_any()
    {
        var seats = Seats.In(new[] { Layout });
        var chair = seats[2];
        var stool = seats[3];

        // Its back, on its own +x side, is turned to -z: it is sat on facing +z.
        foreach (var from in new[] { new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f) })
        {
            AssertFacing(new Vector2(0f, 1f), Seats.PoseOn(chair, chair.Spot + from).Yaw);
        }

        AssertFacing(new Vector2(-1f, 0f), Seats.PoseOn(stool, stool.Spot + new Vector3(-1f, 0f, 0.2f)).Yaw);
        AssertFacing(new Vector2(0f, -1f), Seats.PoseOn(stool, stool.Spot + new Vector3(0.2f, 0f, -1f)).Yaw);
    }

    [Fact]
    public void A_bed_is_lain_on_along_its_length_on_top_of_it()
    {
        var bed = Seats.In(new[] { Layout })[4];

        var pose = Seats.PoseOn(bed, bed.Spot + new Vector3(1f, 0f, 0f));

        AssertFacing(new Vector2(0f, 1f), pose.Yaw);
        Assert.Equal(bed.Spot.Y, pose.Origin.Y, precision: 4);
    }

    [Fact]
    public void A_seat_is_taken_from_within_reach_on_its_own_floor()
    {
        var seats = Seats.In(new[] { Layout });

        Assert.True(Seats.InReach(seats[0], new Vector3(-0.45f, 0f, Seats.Reach)));
        Assert.False(Seats.InReach(seats[0], new Vector3(-0.45f, 0f, Seats.Reach + 0.01f)));
        Assert.False(Seats.InReach(seats[4], new Vector3(15f, 0f, 1f)));
        Assert.True(Seats.InReach(seats[4], new Vector3(15f, Floors.Storey, 1f)));
    }

    [Fact]
    public void A_seat_holds_one_player_and_a_player_one_seat_until_it_gets_up()
    {
        var taken = new SeatOccupancy();

        Assert.True(taken.TrySit(player: 1, seat: 0));
        Assert.False(taken.TrySit(player: 2, seat: 0));
        Assert.False(taken.TrySit(player: 1, seat: 1));
        Assert.True(taken.TrySit(player: 2, seat: 1));
        Assert.Equal(0, taken.SeatOf(1));
        Assert.True(taken.IsTaken(1));

        Assert.Equal(0, taken.Stand(1));
        Assert.Null(taken.Stand(1));
        Assert.False(taken.IsTaken(0));
        Assert.True(taken.TrySit(player: 2 + 1, seat: 0));
        Assert.Equal(new[] { (0, 3L), (1, 2L) }, taken.Taken.OrderBy(t => t.Seat));
    }

    [Fact]
    public void The_town_has_somewhere_to_sit_and_to_lie_down_inside_its_safe_ground_and_the_fortress_nowhere()
    {
        var town = Load("allied-town");
        var seats = Seats.In(new[] { town });
        var safe = town.AreasNamed("safe").ToList();

        Assert.Contains(seats, s => s.Kind == SeatKind.Sit);
        Assert.Contains(seats, s => s.Kind == SeatKind.Lie);
        Assert.All(seats, s => Assert.Contains(safe, a => a.Contains(new Vector2(s.Spot.X, s.Spot.Z))));
        Assert.Empty(Seats.In(new[] { Load("enemy-fortress") }));
    }

    private static void AssertFacing(Vector2 expected, float yaw)
    {
        var forward = Ground.Forward(yaw);
        Assert.Equal(expected.X, forward.X, precision: 4);
        Assert.Equal(expected.Y, forward.Y, precision: 4);
    }

    private static LevelLayout Load(string name)
    {
        string path = Repo.PathTo("GodotClient", "config", "levels", $"{name}.layout.json");
        return LevelLayout.Parse(File.ReadAllText(path), name);
    }
}
