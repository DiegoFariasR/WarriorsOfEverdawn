using System;
using System.Globalization;

namespace WarriorsOfEverdawn.Main;

public enum SessionMode
{
    Solo,
    Host,
    Join,
}

// User arguments, passed after "--" on the Godot command line.
public sealed record LaunchOptions(
    SessionMode Mode, string Address, int Port, bool Bot, float QuitAfter, bool CameraCheck, bool Pvp, bool NoEnemies)
{
    public const int DefaultPort = 7777;

    public static LaunchOptions Parse(string[] args)
    {
        var options = new LaunchOptions(
            SessionMode.Solo, "127.0.0.1", DefaultPort, Bot: false, QuitAfter: 0f, CameraCheck: false, Pvp: false, NoEnemies: false);
        for (int i = 0; i < args.Length; i++)
        {
            options = args[i] switch
            {
                "--host" => options with { Mode = SessionMode.Host },
                "--join" => options with { Mode = SessionMode.Join, Address = ValueAfter(args, ref i) },
                "--port" => options with { Port = int.Parse(ValueAfter(args, ref i), CultureInfo.InvariantCulture) },
                "--bot" => options with { Bot = true },
                "--camera-check" => options with { CameraCheck = true },
                "--pvp" => options with { Pvp = true },
                "--no-enemies" => options with { NoEnemies = true },
                "--quit-after" => options with { QuitAfter = float.Parse(ValueAfter(args, ref i), CultureInfo.InvariantCulture) },
                _ => throw new ArgumentException($"Unknown launch argument '{args[i]}'"),
            };
        }

        return options;
    }

    private static string ValueAfter(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"{args[i]} needs a value");
        }

        return args[++i];
    }
}
