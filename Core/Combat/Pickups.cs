using System.Collections.Generic;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

// Weapons lying on the ground: how close a player has to be to take one, and to read what it is.
public static class Pickups
{
    // Centre of the player to the weapon.
    public const float Reach = 1.6f;

    public const float LabelRange = 4f;

    // The spot a player's hands are over: where a weapon let go of lands, and the spot a weapon is picked up from.
    // Within Reach, so a weapon dropped can be taken straight back.
    public const float InFront = 0.7f;

    public static Vector2 SpotInFrontOf(Vector2 player, float facingYaw) => player + Ground.Forward(facingYaw) * InFront;

    // Of the places within `within` of the player, the one nearest the spot in front of it, or -1 when none is in
    // range. Facing a weapon chooses it, so two lying close together can be told apart; plain nearest-to-the-player
    // could not be steered once a player stood between them.
    public static int Focused(IReadOnlyList<Vector2> places, Vector2 player, float facingYaw, float within)
    {
        var spot = SpotInFrontOf(player, facingYaw);
        int focused = -1;
        float best = float.PositiveInfinity;
        for (int i = 0; i < places.Count; i++)
        {
            if (Vector2.DistanceSquared(places[i], player) > within * within)
            {
                continue;
            }

            float distance = Vector2.DistanceSquared(places[i], spot);
            if (distance < best)
            {
                best = distance;
                focused = i;
            }
        }

        return focused;
    }
}
