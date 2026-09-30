namespace WarriorsOfEverdawn.Core.Locomotion;

// How fast a character's aim, and so its upper body and its attacks, can come round. Input sets where the player
// wants to face; the character gets there at this rate. Half a turn takes a third of a second.
public static class Turning
{
    public const float MaxRate = 540f * Angles.DegToRad;
}
