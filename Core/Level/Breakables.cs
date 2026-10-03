namespace WarriorsOfEverdawn.Core.Level;

// Pieces of the level that blows break: crates and barrels, marked in the layout (LayoutPlacement.Breakable). Each
// takes a hit as a body with no resistances does, and with nothing left it breaks for good. Design:
// Docs/Design/level-layouts.md, "Breakables".
public static class Breakables
{
    // Two blows of a plain weapon in the Knight's hands, or one bolt.
    public const int Hp = 30;
}
