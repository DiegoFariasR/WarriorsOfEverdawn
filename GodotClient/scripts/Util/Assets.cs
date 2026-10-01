using System;
using Godot;

namespace WarriorsOfEverdawn.Util;

public static class Assets
{
    public static T Load<T>(string path) where T : Resource =>
        GD.Load<T>(path) ?? throw new InvalidOperationException($"Missing asset {path} (expected {typeof(T).Name})");

    public static Node3D Instantiate(string scenePath) => Load<PackedScene>(scenePath).Instantiate<Node3D>();

    // Prop and weapon GLBs can carry the position their node had on the sheet they were laid out on (the arrow's is
    // 9 across and 4 down). With those cleared the model sits on its own origin, so it is drawn where it is placed.
    public static Node3D InstantiateAtOrigin(string scenePath)
    {
        var node = Instantiate(scenePath);
        foreach (var child in node.FindChildren("*", nameof(Node3D), recursive: true, owned: false))
        {
            ((Node3D)child).Position = Vector3.Zero;
        }

        return node;
    }
}
