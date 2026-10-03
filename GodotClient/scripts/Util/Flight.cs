using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Util;

// Something thrown or loosed along a straight line from a chest: a player's bolt, dart or arrow (Main/Bolts), or a
// skeleton's arrow (Enemy/Arrows). Every machine flies its own copy from the same start, and the world stops each
// copy at the same wall.
public abstract class Flight
{
    private Vector3? _leaving;

    protected Flight(ProjectileDefinition projectile, Vector3 chest, Vector3 from, Vector3 direction, Node3D node)
    {
        Projectile = projectile;
        _leaving = chest;
        From = from;
        Direction = direction;
        Node = node;
    }

    public ProjectileDefinition Projectile { get; }

    public Vector3 From { get; }

    public Vector3 Direction { get; }

    public Node3D Node { get; }

    public float Travelled { get; private set; }

    public float Age { get; private set; }

    public Vector3 Position => From + Direction * Travelled;

    public bool AtFullDistance => Travelled >= Projectile.MaxDistance;

    public static float Oldest(IEnumerable<Flight> flights) => flights.Select(f => f.Age).DefaultIfEmpty(0f).Max();

    public static Node3D Arrow(Vector3 direction, bool toonLook)
    {
        var arrow = Assets.InstantiateAtOrigin(CombatVisuals.ArrowModel);
        if (toonLook)
        {
            ToonLook.ApplyToWeapon(arrow);
        }

        // The model's point is its -Y end.
        var along = -direction;
        arrow.Basis = new Basis(along.Cross(Vector3.Up), along, Vector3.Up);
        return arrow;
    }

    // One step on: where it was and is, and where the world stopped it on the way, if it did. Its first step is
    // looked at from the thrower's chest, not from the hand: a thrower against a wall has its hand in the wall, and a
    // line that starts inside a wall meets nothing.
    public FlightStep Advance(PhysicsDirectSpaceState3D space, float delta)
    {
        var before = Position;
        Age += delta;
        Travelled = Mathf.Min(Travelled + Projectile.Speed * delta, Projectile.MaxDistance);
        var wall = Walls.Hit(space, _leaving ?? before, Position);
        _leaving = null;
        return new FlightStep(before, Position, wall);
    }

    public void StopAt(Vector3 wall) => Travelled = Mathf.Max(0f, (wall - From).Dot(Direction));
}

public readonly record struct FlightStep(Vector3 Before, Vector3 After, Vector3? Wall);
