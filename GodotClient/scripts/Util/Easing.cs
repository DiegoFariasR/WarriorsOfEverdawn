using Godot;

namespace WarriorsOfEverdawn.Util;

public static class Easing
{
    // The share of what is left between a value and its goal that a step of `delta` seconds closes at `rate`: the
    // value comes in the same way at any frame rate.
    public static float Share(float rate, float delta) => 1f - Mathf.Exp(-rate * delta);
}
