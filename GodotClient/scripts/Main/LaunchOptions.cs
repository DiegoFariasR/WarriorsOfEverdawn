using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;

namespace WarriorsOfEverdawn.Main;

public enum SessionMode
{
    Solo,
    Host,
    Join,
}

// When to capture the window and how many frames. At and Interval are seconds of play.
public sealed record ScreenshotOptions(float At, int Frames, float Interval)
{
    public static readonly ScreenshotOptions Default = new(At: 6f, Frames: 1, Interval: 0.5f);
}

// User arguments, passed after "--" on the Godot command line.
public sealed record LaunchOptions
{
    public const int DefaultPort = 7777;

    public SessionMode Mode { get; init; } = SessionMode.Solo;

    public string Address { get; init; } = "127.0.0.1";

    public int Port { get; init; } = DefaultPort;

    public bool Bot { get; init; }

    public float QuitAfter { get; init; }

    public bool CameraCheck { get; init; }

    // --wall-check: looses everything that flies at a wall and quits, saying whether each stopped there.
    public bool WallCheck { get; init; }

    public bool Pvp { get; init; }

    public bool NoEnemies { get; init; }

    // Keeps an AI-launched window minimized and unfocused, so it never covers the user's work.
    public bool AiPlaytest { get; init; }

    public ScreenshotOptions? Screenshot { get; init; }

    public bool NoUi { get; init; }

    public CameraMode? Camera { get; init; }

    // For captures: the camera starts this much nearer or further (1 as it is; the camera keeps it within its limits).
    public float? Zoom { get; init; }

    // Host only, for the playtest: seconds in, one player is taken down through the normal damage path.
    public float DownAt { get; init; }

    // Host only, for test sessions with several players: each wave brings this many times its skeletons. Waves are
    // not scaled by player count yet (Docs/Design/combat.md, open questions).
    public int WaveScale { get; init; } = 1;

    // The weapons this machine's player starts with, in hand and on the back.
    public WeaponDefinition? Weapon { get; init; }

    public WeaponDefinition? BackWeapon { get; init; }

    // From --weapon and --back-weapon; null when neither is given.
    public WeaponSets? Sets { get; init; }

    public bool SwingSurvey { get; init; }

    // For looking at armour: the player's figure in a row in the field, once per tier of armour, or once per outfit
    // named here (comma-separated). Null without the flag.
    public IReadOnlyList<string>? ArmourLineup { get; init; }

    // For looking at a weapon: the player's figure in a row in the field, holding it in its stance, its guard and
    // each of its skills, and carrying it on the back. Null without the flag.
    public WeaponDefinition? WeaponLineup { get; init; }

    // For looking at magic: the player's figure in a row in the field, once per staff of the elements named (all
    // with "all"), each casting; with MagicBarriers, every staff's figure inside its barrier instead. Null
    // without the flag.
    public IReadOnlyList<Element>? MagicLineup { get; init; }

    public bool MagicBarriers { get; init; }

    // For looking at figures: those named (comma-separated) in a row in the field. "cast" is the game's own, a
    // pool's name figures drawn from it at random, a character of the parts catalogue that character. Null without
    // the flag.
    public IReadOnlyList<string>? LookLineup { get; init; }

    // For looking at gold: every pile it falls in, in a row on the ground, seen from this high over it. Null
    // without the flag.
    public float? GoldLineup { get; init; }

    // --parts-check: puts every part of the catalogue on a figure and quits, saying whether each came out right.
    public bool PartsCheck { get; init; }

    // --floors-check: walks the player up into the town's storeys and down into the crypt, clears the crypt and
    // quits, saying whether each floor was reached and hidden or shown as it should be.
    public bool FloorsCheck { get; init; }

    // --ways-dump <x0,z0,x1,z1 or all>: prints the navigation mesh's polygons in that area (with all, only those that
    // go through the air) and quits, 1 when any goes through the air.
    public bool WaysDump { get; init; }

    public Godot.Rect2? WaysArea { get; init; }

    // Host only, for test sessions and for looking at an orb: every monster's chance of leaving a magic orb, in place
    // of its own, so a short session is sure to see some.
    public float? OrbChance { get; init; }

    // Host only, for trying the sellers: every player starts with this much gold in place of Purse.StartingGold, and
    // this many orbs.
    public int? StartGold { get; init; }

    public int GoldAtStart => StartGold ?? Purse.StartingGold;

    public int StartOrbs { get; init; }

    // Host only, for trying armour: every player starts wearing this tier.
    public int StartArmour { get; init; }

    // For the trade test: the bot trades with the seller it starts beside (NetSelfTest.Trade).
    public bool TradeDrill { get; init; }

    // Host only, for the self-tests: every barrier that goes up is dealt a blow, through the same path as a
    // skeleton's (NetSelfTest.Magic).
    public bool BarrierDrill { get; init; }

    // --status-drill, on a bot host: every few seconds a skeleton is frozen, and the next stunned, whatever the
    // fight has done to it. For the self-test: skeletons die too soon to be frozen by play alone.
    public bool StatusDrill { get; init; }

    // Host only, for looking at a place: players start on this spot of the ground instead of in the town, or on a
    // layout's marker: the index-th of that name across both layouts, the town's first (--start-at upstairs:1).
    public Godot.Vector3? StartAt { get; init; }

    public (string Name, int Index)? StartAtMarker { get; init; }

    public static LaunchOptions Parse(string[] args)
    {
        var options = new LaunchOptions();
        for (int i = 0; i < args.Length; i++)
        {
            options = args[i] switch
            {
                "--host" => options with { Mode = SessionMode.Host },
                "--join" => options with { Mode = SessionMode.Join, Address = ValueAfter(args, ref i) },
                "--port" => options with { Port = IntAfter(args, ref i) },
                "--bot" => options with { Bot = true },
                "--camera-check" => options with { CameraCheck = true },
                "--wall-check" => options with { WallCheck = true },
                "--parts-check" => options with { PartsCheck = true },
                "--floors-check" => options with { FloorsCheck = true },
                "--ways-dump" => options with { WaysDump = true, WaysArea = AreaAfter(args, ref i) },
                "--pvp" => options with { Pvp = true },
                "--no-enemies" => options with { NoEnemies = true },
                "--quit-after" => options with { QuitAfter = FloatAfter(args, ref i) },
                "--ai-playtest" => options with { AiPlaytest = true },
                "--screenshot" => options with { Screenshot = options.Screenshot ?? ScreenshotOptions.Default },
                "--shot-at" => options with { Screenshot = ShotOf(options) with { At = FloatAfter(args, ref i) } },
                "--shots" => options with { Screenshot = ShotOf(options) with { Frames = PositiveIntAfter(args, ref i) } },
                "--shot-interval" => options with { Screenshot = ShotOf(options) with { Interval = FloatAfter(args, ref i) } },
                "--no-ui" => options with { NoUi = true },
                "--camera" => options with { Camera = CameraAfter(args, ref i) },
                "--zoom" => options with { Zoom = FloatAfter(args, ref i) },
                "--down-at" => options with { DownAt = FloatAfter(args, ref i) },
                "--wave-scale" => options with { WaveScale = PositiveIntAfter(args, ref i) },
                "--weapon" => options with { Weapon = WeaponAfter(args, ref i) },
                "--back-weapon" => options with { BackWeapon = WeaponAfter(args, ref i) },
                "--swing-survey" => options with { SwingSurvey = true },
                "--armour-lineup" => options with { ArmourLineup = Array.Empty<string>() },
                "--outfits" => options with { ArmourLineup = ValueAfter(args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries) },
                "--weapon-lineup" => options with { WeaponLineup = WeaponAfter(args, ref i) },
                "--magic-lineup" => options with { MagicLineup = ElementsAfter(args, ref i) },
                "--magic-barriers" => options with { MagicLineup = Array.Empty<Element>(), MagicBarriers = true },
                "--gold-lineup" => options with { GoldLineup = FloatAfter(args, ref i) },
                "--look-lineup" => options with { LookLineup = ValueAfter(args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries) },
                "--start-at" => StartAtAfter(args, ref i, options),
                "--orb-chance" => options with { OrbChance = ChanceAfter(args, ref i) },
                "--start-gold" => options with { StartGold = PositiveIntAfter(args, ref i) },
                "--start-orbs" => options with { StartOrbs = PositiveIntAfter(args, ref i) },
                "--start-armour" => options with { StartArmour = ArmourTierAfter(args, ref i) },
                "--trade-drill" => options with { TradeDrill = true },
                "--barrier-drill" => options with { BarrierDrill = true },
                "--status-drill" => options with { StatusDrill = true },
                _ => throw new ArgumentException($"Unknown launch argument '{args[i]}'"),
            };
        }

        return options.Weapon == null && options.BackWeapon == null ? options : options with { Sets = SetsOf(options) };
    }

    private static WeaponSets SetsOf(LaunchOptions options)
    {
        if (options.BackWeapon is not { } back)
        {
            return WeaponSets.StartingWith(options.Weapon!);
        }

        var inHand = options.Weapon ?? (back == WeaponSets.Default.Active ? WeaponSets.Default.Stowed : WeaponSets.Default.Active);
        return inHand != back ? new WeaponSets(inHand, back) : throw new ArgumentException($"--weapon and --back-weapon are both '{back.Id}'; the two sets must differ");
    }

    private static ScreenshotOptions ShotOf(LaunchOptions options) => options.Screenshot ?? ScreenshotOptions.Default;

    private static float FloatAfter(string[] args, ref int i)
    {
        string flag = args[i];
        string value = ValueAfter(args, ref i);
        return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float number)
            ? number
            : throw new ArgumentException($"{flag} needs a number, got '{value}'");
    }

    private static int IntAfter(string[] args, ref int i)
    {
        string flag = args[i];
        string value = ValueAfter(args, ref i);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            ? number
            : throw new ArgumentException($"{flag} needs a whole number, got '{value}'");
    }

    private static int PositiveIntAfter(string[] args, ref int i)
    {
        string flag = args[i];
        int value = IntAfter(args, ref i);
        return value > 0 ? value : throw new ArgumentException($"{flag} needs a number above 0, got {value}");
    }

    // Numbered as the player sees them: 1 to 4, the order C cycles through.
    private static CameraMode CameraAfter(string[] args, ref int i)
    {
        int number = IntAfter(args, ref i);
        return number is >= 1 and <= CameraModes.Count
            ? (CameraMode)(number - 1)
            : throw new ArgumentException($"--camera needs 1 to {CameraModes.Count}, got {number}");
    }

    private static int ArmourTierAfter(string[] args, ref int i)
    {
        string flag = args[i];
        int tier = IntAfter(args, ref i);
        return tier >= 0 && tier <= Armours.MaxTier ? tier : throw new ArgumentException($"{flag} needs a tier from 0 to {Armours.MaxTier}, got {tier}");
    }

    private static float ChanceAfter(string[] args, ref int i)
    {
        string flag = args[i];
        float chance = FloatAfter(args, ref i);
        return chance is >= 0f and <= 1f ? chance : throw new ArgumentException($"{flag} needs a chance from 0 to 1, got {chance}");
    }

    // x0,z0,x1,z1 on the ground, or all of it (null).
    private static Godot.Rect2? AreaAfter(string[] args, ref int i)
    {
        string flag = args[i];
        string value = ValueAfter(args, ref i);
        if (value == "all")
        {
            return null;
        }

        var parts = value.Split(',').Select(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : (float?)null).ToList();
        if (parts.Count != 4 || parts.Any(p => p == null))
        {
            throw new ArgumentException($"{flag} needs an area as x0,z0,x1,z1 or 'all', got '{value}'");
        }

        var from = new Godot.Vector2(Math.Min(parts[0]!.Value, parts[2]!.Value), Math.Min(parts[1]!.Value, parts[3]!.Value));
        var to = new Godot.Vector2(Math.Max(parts[0]!.Value, parts[2]!.Value), Math.Max(parts[1]!.Value, parts[3]!.Value));
        return new Godot.Rect2(from, to - from);
    }

    // A spot as x,z on the ground or x,y,z on a floor, or a marker as name or name:index.
    private static LaunchOptions StartAtAfter(string[] args, ref int i, LaunchOptions options)
    {
        string flag = args[i];
        string value = ValueAfter(args, ref i);
        string wanted = $"{flag} needs a spot as x,z on the ground or x,y,z on a floor, or a marker as name or name:index, got '{value}'";
        if (value.Length > 0 && char.IsLetter(value[0]))
        {
            var named = value.Split(':');
            int index = 0;
            return named.Length == 1 || (named.Length == 2 && int.TryParse(named[1], NumberStyles.None, CultureInfo.InvariantCulture, out index))
                ? options with { StartAtMarker = (named[0], index) }
                : throw new ArgumentException(wanted);
        }

        var parts = value.Split(',').Select(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : (float?)null).ToList();
        return parts.All(p => p != null) && parts.Count is 2 or 3
            ? options with { StartAt = parts.Count == 2 ? new Godot.Vector3(parts[0]!.Value, 0f, parts[1]!.Value) : new Godot.Vector3(parts[0]!.Value, parts[1]!.Value, parts[2]!.Value) }
            : throw new ArgumentException(wanted);
    }

    private static IReadOnlyList<Element> ElementsAfter(string[] args, ref int i)
    {
        string flag = args[i];
        string value = ValueAfter(args, ref i);
        return value == "all" ? Array.Empty<Element>()
            : value.Split(',').Select(name => Enum.TryParse<Element>(name, ignoreCase: true, out var element)
                ? element
                : throw new ArgumentException($"{flag} needs elements ({string.Join(", ", Elements.All)}) or 'all', got '{name}'")).ToList();
    }

    private static WeaponDefinition WeaponAfter(string[] args, ref int i)
    {
        string flag = args[i];
        string id = ValueAfter(args, ref i);
        try
        {
            return Weapons.ById(id);
        }
        catch (KeyNotFoundException)
        {
            throw new ArgumentException($"{flag} '{id}' is not one of {string.Join(", ", Weapons.All.Select(w => w.Id))}");
        }
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
