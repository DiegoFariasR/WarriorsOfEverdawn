using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [bot-check]: where the local bot spent the session and on what, in seconds. Nothing is asserted on it: it is what
// to read when another check comes up short of samples, to see whether the bot was kept from the fight and by what.
public partial class NetSelfTest
{
    // A hostile this close is one the bot can be fighting.
    private const float InContact = 4f;

    private readonly SortedDictionary<string, float> _secondsWhere = new();
    private readonly SortedDictionary<string, float> _secondsDoing = new();
    private float _secondsDowned;
    private float _secondsInContact;
    private float _secondsNoHostile;

    private void MeasureBot(float delta)
    {
        if (_map == null || LocalPlayer() is not { } local)
        {
            return;
        }

        string where = _map.IsSafe(local.GlobalPosition) ? "town"
            : _map.InFortress(local.GlobalPosition) ? "fortress"
            : "field";
        _secondsWhere[where] = _secondsWhere.GetValueOrDefault(where) + delta;
        if (local.IsDowned)
        {
            _secondsDowned += delta;
        }
        else if (local.Controls is BotControls bot)
        {
            _secondsDoing[bot.Activity] = _secondsDoing.GetValueOrDefault(bot.Activity) + delta;
        }

        var hostiles = EnemyCharacter.Standing(GetTree()).ToList();
        if (hostiles.Count == 0)
        {
            _secondsNoHostile += delta;
        }
        else if (hostiles.Min(e => e.GlobalPosition.DistanceTo(local.GlobalPosition)) <= InContact)
        {
            _secondsInContact += delta;
        }
    }

    private void PrintBotCheck(long me) =>
        GD.Print($"[bot-check] me={me} where={Seconds(_secondsWhere)} doing={Seconds(_secondsDoing)} downed={_secondsDowned:F1} "
            + $"in_contact={_secondsInContact:F1} no_hostile={_secondsNoHostile:F1}");

    private static string Seconds(SortedDictionary<string, float> seconds) =>
        seconds.Count == 0 ? "none" : string.Join(",", seconds.Select(s => $"{s.Key}:{s.Value:F1}"));
}
