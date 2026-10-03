using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// For --ring-lineup: across the view, left to right, over and over: a thrust's row of rings at chest height, a
// player's arrow flying above it and a skeleton's below, each leaving its rings, in the colours the game draws them in.
public partial class RingShow : Node3D
{
    private const float Every = 0.3f;
    private const float Across = 4f;
    private const float ThrustLength = 2.4f;
    private const float ChestHeight = 1.2f;
    private const float PlayerArrowHeight = 2f;
    private const float SkeletonArrowHeight = 0.5f;

    // The warm white a player's weapon draws its trail and rings in, plain.
    private static readonly Color Plain = new(1f, 0.92f, 0.75f);

    private readonly List<Node3D> _arrows = new();
    private float _untilNext;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        foreach (var arrow in _arrows.ToArray())
        {
            arrow.Position += Vector3.Right * Projectiles.Arrow.Speed * dt;
            if (arrow.Position.X > Across)
            {
                _arrows.Remove(arrow);
                arrow.QueueFree();
            }
        }

        _untilNext -= dt;
        if (_untilNext > 0f)
        {
            return;
        }

        _untilNext = Every;
        var chest = GlobalPosition + Vector3.Up * ChestHeight;
        PierceRings.In(GetTree()).Row(chest + Vector3.Left * (ThrustLength / 2f), chest + Vector3.Right * (ThrustLength / 2f), Plain);
        Loose(PlayerArrowHeight, Plain);
        Loose(SkeletonArrowHeight, EnemyCharacter.TrailTint);
    }

    private void Loose(float height, Color colour)
    {
        var arrow = Flight.Arrow(Vector3.Right, toonLook: true);
        arrow.Position = new Vector3(-Across, height, 0f);
        arrow.AddChild(new RingWake { Colour = colour });
        AddChild(arrow);
        _arrows.Add(arrow);
    }
}
