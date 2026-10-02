using System;
using System.IO;
using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Core.Trade;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Level;

public class LevelLayoutTests
{
    private const string Minimal = """
        {
         "version": 1,
         "assets": {"wall": "res://assets/dungeon/wall.glb", "bone": "res://assets/props/bone_A.glb"},
         "textures": {"res://assets/dungeon/": "res://assets/textures/dungeon/dungeon_texture.png"},
         "placements": [
          {"asset": "wall", "position": [4, 0, -8], "rotation": [0, 0.707107, 0, 0.707107], "scale": [1, 1, 1], "solid": true},
          {"asset": "bone", "position": [1, 0, 2], "rotation": [0, 0, 0, 1], "scale": [1.5, 1.5, 1.5]}
         ],
         "markers": [{"name": "gate", "position": [0, 0, 14]}, {"name": "spawn", "position": [1, 0, 19]}, {"name": "spawn", "position": [-1, 0, 19]},
                     {"name": "seller-smith", "position": [3, 0, 20], "yaw": 1.5}],
         "areas": [{"name": "safe", "min": [-9.5, 14.5], "max": [9.5, 29.5]}]
        }
        """;

    private static readonly string[] Fortresses = { "allied-town", "enemy-fortress" };

    [Fact]
    public void A_layout_reads_its_pieces_markers_and_areas()
    {
        var layout = LevelLayout.Parse(Minimal, "minimal");

        Assert.Equal(2, layout.Placements.Count);
        Assert.True(layout.Placements[0].Solid);
        Assert.False(layout.Placements[1].Solid);
        Assert.Equal(new Vector3(4f, 0f, -8f), layout.Placements[0].Position);
        Assert.Equal(new Vector3(1.5f), layout.Placements[1].Scale);
        Assert.Equal(2, layout.MarkersNamed("spawn").Count());
        Assert.Equal(new Vector3(0f, 0f, 14f), layout.MarkersNamed("gate").Single());
        Assert.Single(layout.AreasNamed("safe"));
        Assert.Single(layout.Textures);
        Assert.Empty(layout.Meshes);
        Assert.Empty(layout.Lights);
    }

    [Fact]
    public void A_marker_someone_stands_on_says_who_and_which_way_they_face()
    {
        var layout = LevelLayout.Parse(Minimal, "minimal");

        var (who, marker) = Assert.Single(layout.MarkersStarting("seller-"));
        Assert.Equal("smith", who);
        Assert.Equal(1.5f, marker.Yaw);
        Assert.Equal(new Vector3(3f, 0f, 20f), marker.Position);
        Assert.All(layout.Markers.Where(m => m != marker), m => Assert.Equal(0f, m.Yaw));
        Assert.Empty(layout.MarkersStarting("gate"));
    }

    [Fact]
    public void Every_seller_has_a_place_to_stand_inside_the_towns_safe_ground()
    {
        var town = Load("allied-town");
        var safe = town.AreasNamed("safe").ToList();
        var standing = town.MarkersStarting("seller-").ToList();

        Assert.Equal(Sellers.All.Select(s => s.Id).OrderBy(id => id), standing.Select(s => s.Who).OrderBy(id => id));
        Assert.All(standing, s => Assert.Contains(safe, a => a.Contains(new Vector2(s.Marker.Position.X, s.Marker.Position.Z))));
        Assert.Empty(Load("enemy-fortress").MarkersStarting("seller-"));
    }

    [Fact]
    public void A_solid_blocks_as_its_box_unless_it_follows_its_mesh()
    {
        string doorway = Minimal.Replace("\"solid\": true", "\"solid\": true, \"shape\": \"mesh\"");

        Assert.False(LevelLayout.Parse(Minimal, "minimal").Placements[0].FollowsMesh);
        Assert.True(LevelLayout.Parse(doorway, "doorway").Placements[0].FollowsMesh);
    }

    [Fact]
    public void A_shape_on_a_piece_that_is_not_solid_and_an_unknown_shape_fail_loudly()
    {
        string notSolid = Minimal.Replace("\"scale\": [1.5, 1.5, 1.5]", "\"scale\": [1.5, 1.5, 1.5], \"shape\": \"mesh\"");
        string unknown = Minimal.Replace("\"solid\": true", "\"solid\": true, \"shape\": \"sphere\"");

        Assert.Throws<FormatException>(() => LevelLayout.Parse(notSolid, "not-solid"));
        Assert.Throws<FormatException>(() => LevelLayout.Parse(unknown, "unknown-shape"));
    }

    [Fact]
    public void An_area_holds_the_points_within_its_corners_and_on_its_edges()
    {
        var area = new LayoutArea("safe", new Vector2(-2f, 1f), new Vector2(3f, 5f));

        Assert.True(area.Contains(new Vector2(0f, 3f)));
        Assert.True(area.Contains(area.Min));
        Assert.True(area.Contains(area.Max));
        Assert.False(area.Contains(new Vector2(area.Max.X + 0.01f, 3f)));
        Assert.False(area.Contains(new Vector2(0f, area.Min.Y - 0.01f)));
    }

    [Fact]
    public void A_layout_of_another_version_fails_loudly()
    {
        string other = Minimal.Replace("\"version\": 1", $"\"version\": {LevelLayout.SupportedVersion + 1}");

        var error = Assert.Throws<FormatException>(() => LevelLayout.Parse(other, "other-version"));
        Assert.Contains("other-version", error.Message);
    }

    [Fact]
    public void A_placement_of_an_unlisted_asset_and_broken_json_fail_loudly()
    {
        Assert.Throws<FormatException>(() => LevelLayout.Parse(Minimal.Replace("\"asset\": \"bone\"", "\"asset\": \"ghost\""), "unlisted"));
        Assert.Throws<FormatException>(() => LevelLayout.Parse("{\"version\": 1}", "no-assets"));
        Assert.Throws<FormatException>(() => LevelLayout.Parse("not json", "garbage"));
    }

    [Fact]
    public void Both_fortresses_have_walls_a_gate_and_a_way_out()
    {
        foreach (string name in Fortresses)
        {
            var layout = Load(name);

            Assert.Contains(layout.Placements, p => p.Solid);
            Assert.Single(layout.MarkersNamed("gate"));
            Assert.Single(layout.MarkersNamed("outside-gate"));
            Assert.Single(layout.AreasNamed("gate"));
            Assert.All(layout.Placements, p => Assert.True(layout.Assets.ContainsKey(p.Asset), $"{name}: {p.Asset}"));
        }
    }

    [Fact]
    public void Players_start_inside_the_towns_safe_ground_and_waves_rise_outside_it()
    {
        var town = Load("allied-town");
        var fortress = Load("enemy-fortress");
        var safe = town.AreasNamed("safe").ToList();
        bool IsSafe(Vector3 p) => safe.Any(a => a.Contains(new Vector2(p.X, p.Z)));

        Assert.NotEmpty(town.MarkersNamed("player-spawn"));
        Assert.All(town.MarkersNamed("player-spawn"), p => Assert.True(IsSafe(p)));
        Assert.NotEmpty(fortress.MarkersNamed("enemy-spawn"));
        Assert.All(fortress.MarkersNamed("enemy-spawn"), p => Assert.False(IsSafe(p)));
        Assert.Empty(fortress.AreasNamed("safe"));

        // The gate is the way in, so it is not part of the safe ground itself.
        Assert.False(IsSafe(town.MarkersNamed("gate").Single()));
    }

    [Fact]
    public void Each_fortress_has_a_room_off_either_side_with_a_doorway_to_walk_through()
    {
        foreach (string name in Fortresses)
        {
            var layout = Load(name);
            var courtyard = Assert.Single(layout.AreasNamed("courtyard"));
            var rooms = layout.AreasNamed("room").ToList();
            var doors = layout.AreasNamed("door").ToList();

            Assert.Equal(2, rooms.Count);
            Assert.Contains(rooms, r => r.Max.X < courtyard.Min.X);
            Assert.Contains(rooms, r => r.Min.X > courtyard.Max.X);
            Assert.Equal(rooms.Count, doors.Count);
            Assert.Equal(rooms.Count, layout.Placements.Count(p => p.FollowsMesh));

            // A doorway's clear ground reaches from the courtyard into its room, and each room has a spot to stand on.
            Assert.All(rooms, r => Assert.Contains(doors, d => d.Max.X > r.Min.X && d.Min.X < r.Max.X && d.Max.X > courtyard.Min.X && d.Min.X < courtyard.Max.X));
            Assert.All(rooms, r => Assert.Single(layout.MarkersNamed("room"), m => r.Contains(new Vector2(m.X, m.Z))));
        }
    }

    [Fact]
    public void The_towns_rooms_and_doorways_are_safe_ground_too()
    {
        var town = Load("allied-town");
        var safe = town.AreasNamed("safe").ToList();

        Assert.All(town.AreasNamed("room").Concat(town.AreasNamed("door")), a => Assert.Contains(a with { Name = "safe" }, safe));
    }

    private static LevelLayout Load(string name)
    {
        string path = Path.Combine(RepoRoot(), "GodotClient", "config", "levels", $"{name}.layout.json");
        return LevelLayout.Parse(File.ReadAllText(path), name);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WarriorsOfEverdawn.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No WarriorsOfEverdawn.slnx above {AppContext.BaseDirectory}");
    }
}
