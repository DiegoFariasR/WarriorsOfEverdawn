using System;
using System.Collections.Generic;

namespace WarriorsOfEverdawn.Core.Combat;

// The kinds of magic a staff can be made for, in Everdawn's order: its four elements, the weaponised forms of two of
// them (ice of water, lightning of wind), and its two astral types.
public enum Element
{
    Fire,
    Water,
    Ice,
    Wind,
    Lightning,
    Earth,
    Divine,
    Void,
}

public static class Elements
{
    public static IReadOnlyList<Element> All { get; } = Enum.GetValues<Element>();

    public static bool IsAstral(Element element) => element is Element.Divine or Element.Void;

    // Lower case, as ids are made from it: "fire".
    public static string IdOf(Element element) => element.ToString().ToLowerInvariant();
}
