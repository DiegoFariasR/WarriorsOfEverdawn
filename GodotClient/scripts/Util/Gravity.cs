using Godot;

namespace WarriorsOfEverdawn.Util;

// What pulls a body down onto whatever floor is under it: the ground, a storey's floor, a flight of stairs, the floor
// of a crypt. Bodies walk with level speeds; this is the only speed they have up or down, apart from what walking up
// a ramp lifts them by.
public static class Gravity
{
    public const float Pull = 20f;
    public const float Fastest = 30f;

    // Kept to a floor this far below the feet from one step to the next, so walking down a flight of stairs is
    // walking, not falling a little each step. A dash down one still leaves it now and then, and lands again.
    public const float FloorSnap = 0.25f;

    // Stairs up to a storey rise at 42 degrees, and a body touching them where their slope meets an edge is pushed
    // more steeply than that: at Godot's 45 a body now and then took them for a wall halfway up.
    public static readonly float SteepestFloor = Mathf.DegToRad(50f);

    // Its upward speed for this step: none on a floor, and off one, falling faster each step.
    public static float Fall(CharacterBody3D body, float delta) =>
        body.IsOnFloor() ? 0f : Mathf.Max(body.Velocity.Y - Pull * delta, -Fastest);

    // The level speed with the fall added.
    public static Vector3 With(Vector3 level, CharacterBody3D body, float delta) => new(level.X, Fall(body, delta), level.Z);
}
