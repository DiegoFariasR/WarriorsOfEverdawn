using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WarriorsOfEverdawn.Main;

// What a player's blows, bolts and bursts land on that is no fighter: the training dummies and the crates and barrels
// that break. A machine whose player struck one asks the host to deal the hit; it is as wide as Radius to a blow.
public interface IStruck
{
    float Radius { get; }

    void AskToStrike(string skillId);
}

public static class Struck
{
    // Every IStruck that can still be struck is in this group, and leaves it when it can be no more.
    public const string Group = "struck";

    public static IEnumerable<Node3D> All(SceneTree tree) => tree.GetNodesInGroup(Group).OfType<Node3D>();
}
