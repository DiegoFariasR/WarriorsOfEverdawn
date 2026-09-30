namespace WarriorsOfEverdawn.Main;

// Rules the host chooses for the whole session and sends to each client as it joins.
public static class SessionRules
{
    // Players' swings hit other players too (Docs/Design/multiplayer.md).
    public static bool Pvp { get; set; }
}
