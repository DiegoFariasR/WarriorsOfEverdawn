using System;
using System.Collections.Generic;

namespace WarriorsOfEverdawn.Core.Combat;

// The kinds of magic a staff can be made for: Everdawn's four elements and its two astral types.
public enum Element
{
    Fire,
    Water,
    Wind,
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
