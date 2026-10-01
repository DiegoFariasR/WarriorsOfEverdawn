using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace WarriorsOfEverdawn.Core.Level;

// Solid pieces block movement; the rest is walked through.
public sealed record LayoutPlacement(string Asset, Vector3 Position, Quaternion Rotation, Vector3 Scale, bool Solid);

// Faces are Blender polygons: vertex indices in counter-clockwise order.
public sealed record LayoutMesh(string Name, IReadOnlyList<Vector3> Vertices, IReadOnlyList<IReadOnlyList<int>> Faces, Vector3 LinearColor);

public sealed record LayoutLight(Vector3 Position, Vector3 LinearColor, float Energy, float Range, float? HaloRadius);

// A named point on the ground: where players start, where a wave rises, the middle of a gate.
public sealed record LayoutMarker(string Name, Vector3 Position);

// A named rectangle of ground, by its corners on the ground plane (x, z).
public sealed record LayoutArea(string Name, Vector2 Min, Vector2 Max)
{
    public Vector2 Centre => (Min + Max) / 2f;

    public bool Contains(Vector2 point) => point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
}

// A level layout written by Tools/level_fortresses.py (format: Docs/Design/level-layouts.md, after Everdawn's
// level-layout pipeline). Coordinates are Godot's (Y up, metres); mesh and light colours are scene-linear. Asset and
// texture values are engine paths. Textures maps an asset path prefix to the texture every piece under it wears (the
// longest matching prefix wins): the dungeon kit's pieces carry none of their own.
public sealed record LevelLayout(
    IReadOnlyDictionary<string, string> Assets,
    IReadOnlyDictionary<string, string> Textures,
    IReadOnlyList<LayoutPlacement> Placements,
    IReadOnlyList<LayoutMarker> Markers,
    IReadOnlyList<LayoutArea> Areas,
    IReadOnlyList<LayoutMesh> Meshes,
    IReadOnlyList<LayoutLight> Lights)
{
    public const int SupportedVersion = 1;

    public IEnumerable<Vector3> MarkersNamed(string name) => Markers.Where(m => m.Name == name).Select(m => m.Position);

    public IEnumerable<LayoutArea> AreasNamed(string name) => Areas.Where(a => a.Name == name);

    public static LevelLayout Parse(string json, string sourceName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int version = root.GetProperty("version").GetInt32();
            if (version != SupportedVersion)
            {
                throw new FormatException($"{sourceName}: layout version {version}, expected {SupportedVersion}");
            }

            var assets = root.GetProperty("assets").EnumerateObject().ToDictionary(a => a.Name, a => a.Value.GetString()!);
            var textures = root.TryGetProperty("textures", out var textureMap)
                ? textureMap.EnumerateObject().ToDictionary(t => t.Name, t => t.Value.GetString()!)
                : new Dictionary<string, string>();
            var placements = root.GetProperty("placements").EnumerateArray()
                .Select(p => new LayoutPlacement(
                    p.GetProperty("asset").GetString()!,
                    ReadVector3(p.GetProperty("position")),
                    ReadQuaternion(p.GetProperty("rotation")),
                    ReadVector3(p.GetProperty("scale")),
                    p.TryGetProperty("solid", out var solid) && solid.GetBoolean()))
                .ToList();
            foreach (var placement in placements.Where(p => !assets.ContainsKey(p.Asset)))
            {
                throw new FormatException($"{sourceName}: a placement names asset '{placement.Asset}', which the layout does not list");
            }

            return new LevelLayout(
                assets,
                textures,
                placements,
                ReadList(root, "markers", m => new LayoutMarker(m.GetProperty("name").GetString()!, ReadVector3(m.GetProperty("position")))),
                ReadList(root, "areas", a => new LayoutArea(a.GetProperty("name").GetString()!, ReadVector2(a.GetProperty("min")), ReadVector2(a.GetProperty("max")))),
                ReadList(root, "meshes", ReadMesh),
                ReadList(root, "lights", ReadLight));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new FormatException($"{sourceName}: invalid level layout: {ex.Message}", ex);
        }
    }

    private static List<T> ReadList<T>(JsonElement root, string name, Func<JsonElement, T> read) =>
        root.TryGetProperty(name, out var array) ? array.EnumerateArray().Select(read).ToList() : new List<T>();

    private static LayoutMesh ReadMesh(JsonElement m) => new(
        m.GetProperty("name").GetString() ?? "LayoutMesh",
        m.GetProperty("vertices").EnumerateArray().Select(ReadVector3).ToList(),
        m.GetProperty("faces").EnumerateArray()
            .Select(f => (IReadOnlyList<int>)f.EnumerateArray().Select(i => i.GetInt32()).ToList())
            .ToList(),
        ReadVector3(m.GetProperty("color")));

    private static LayoutLight ReadLight(JsonElement l) => new(
        ReadVector3(l.GetProperty("position")),
        ReadVector3(l.GetProperty("color")),
        l.GetProperty("energy").GetSingle(),
        l.GetProperty("range").GetSingle(),
        l.TryGetProperty("halo", out var halo) ? halo.GetSingle() : null);

    private static Vector2 ReadVector2(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle());

    private static Vector3 ReadVector3(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

    private static Quaternion ReadQuaternion(JsonElement r) =>
        new(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle());
}
