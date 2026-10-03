using System;
using System.IO;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Tests;

// The checkout the tests run from, for the tests that read the game's own files.
internal static class Repo
{
    // A path from the checkout's root: Repo.PathTo("GodotClient", "kit").
    public static string PathTo(params string[] parts) => Path.Combine(parts.Prepend(Root()).ToArray());

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WarriorsOfEverdawn.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No WarriorsOfEverdawn.slnx above {AppContext.BaseDirectory}");
    }
}
