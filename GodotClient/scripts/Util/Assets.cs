using System;
using Godot;

namespace WarriorsOfEverdawn.Util;

public static class Assets
{
    public static T Load<T>(string path) where T : Resource =>
        GD.Load<T>(path) ?? throw new InvalidOperationException($"Missing asset {path} (expected {typeof(T).Name})");

    public static Node3D Instantiate(string scenePath) => Load<PackedScene>(scenePath).Instantiate<Node3D>();
}
