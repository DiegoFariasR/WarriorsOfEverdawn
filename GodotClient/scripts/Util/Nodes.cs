using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WarriorsOfEverdawn.Util;

public static class Nodes
{
    // Taken out at once, so what is put in their place this frame is all the parent has; freed at the frame's end.
    public static void FreeChildren(this Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    // Every mesh under the node, those made in code included (they have no owner); not the node itself.
    public static IEnumerable<MeshInstance3D> Meshes(this Node root) =>
        root.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>();
}
