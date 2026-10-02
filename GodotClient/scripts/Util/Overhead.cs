using Godot;

namespace WarriorsOfEverdawn.Util;

// Where a text or a bar goes so that it reads as over a thing from whatever camera is looking. Height alone puts it
// over the thing only while the camera looks across: from straight above, every height over a spot is the same
// spot on screen, so a name, a prompt and a bar stacked by height all land on one another and on the thing itself.
public static class Overhead
{
    // Up the screen, in the world.
    public static Vector3 Up(Camera3D? camera) => camera?.GlobalBasis.Y ?? Vector3.Up;

    // The point `height` over the feet, moved up the screen by as much of `girth` (the thing's half width, and what
    // room the text wants) as the camera looks down: none of it looking across, all of it from straight above.
    // That is where the far rim of a body that wide and that tall shows on screen. With no camera, the height.
    public static Vector3 Point(Camera3D? camera, Vector3 feet, float height, float girth)
    {
        var over = feet + Vector3.Up * height;
        return camera == null ? over : over + camera.GlobalBasis.Y * (girth * Mathf.Abs(camera.GlobalBasis.Z.Dot(Vector3.Up)));
    }
}
