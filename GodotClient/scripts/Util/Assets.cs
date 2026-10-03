using System;
using System.Collections.Generic;
using Godot;

namespace WarriorsOfEverdawn.Util;

public static class Assets
{
    // Every resource loaded here is held for as long as the game runs. Godot caches a loaded resource with only a weak
    // hold on its C# side; once that side was collected, the next load of the resource could throw "Handle is not
    // initialized" (godotengine/godot#112067): a gold pile's model, once in a net-test run. Held here, it never is.
    private static readonly Dictionary<string, Resource> Held = new();

    public static T Load<T>(string path) where T : Resource
    {
        Resource resource;
        lock (Held)
        {
            if (!Held.TryGetValue(path, out resource!))
            {
                resource = GD.Load<T>(path) ?? throw new InvalidOperationException($"Missing asset {path} (expected {typeof(T).Name})");
                Held[path] = resource;
            }
        }

        return resource as T ?? throw new InvalidOperationException($"{path} is a {resource.GetType().Name}, not the {typeof(T).Name} asked for");
    }

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
