using System;
using System.IO;
using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Level;
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
         "markers": [{"name": "gate", "position": [0, 0, 14]}, {"name": "spawn", "position": [1, 0, 19]}, {"name": "spawn", "position": [-1, 0, 19]}],
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
    public void Players_start_inside_the_towns_safe_area_and_waves_rise_outside_it()
    {
        var town = Load("allied-town");
        var fortress = Load("enemy-fortress");
        var safe = Assert.Single(town.AreasNamed("safe"));

        Assert.NotEmpty(town.MarkersNamed("player-spawn"));
        Assert.All(town.MarkersNamed("player-spawn"), p => Assert.True(safe.Contains(new Vector2(p.X, p.Z))));
        Assert.NotEmpty(fortress.MarkersNamed("enemy-spawn"));
        Assert.All(fortress.MarkersNamed("enemy-spawn"), p => Assert.False(safe.Contains(new Vector2(p.X, p.Z))));
        Assert.Empty(fortress.AreasNamed("safe"));

        // The gate is the way in, so it is not part of the safe ground itself.
        var gate = town.MarkersNamed("gate").Single();
        Assert.False(safe.Contains(new Vector2(gate.X, gate.Z)));
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
