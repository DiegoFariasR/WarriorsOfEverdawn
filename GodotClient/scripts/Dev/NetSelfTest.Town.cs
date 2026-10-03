using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [town-check], with --town-drill: the local bot, started in the town, breaks the nearest crate or barrel with the
// weapon in hand, walks to the nearest bench and sits on it (E), gets up (E), walks up to the inn's beds and lies on a
// bed, and gets up. On every machine: its crate showed a hit that did not break it before the one that did, the
// crates it sees break burst into splinters that are gone soon after, every
// player is seen sitting and lying down, its body where the seat puts it, and getting up again; and the prompt showed
// while a seat was in reach. On the host, [town-host]: no seat is left taken.
public partial class NetSelfTest
{
    // Each step of the drill is given this long before it is called failed.
    private const float TownStepLimit = 20f;

    // Seated or lying this long before getting up; the body is measured once it has been there for SettledOnSeat.
    private const float RestHold = 1.5f;
    private const float SettledOnSeat = 1.2f;

    // A spot is reached within this, a crate is struck from this much past its edge, and a seat taken from this
    // share of its reach: well within it as the host sees the player too.
    private const float AtSpot = 0.4f;
    private const float StrikeFrom = 0.8f;
    private const float SeatFrom = 0.6f;

    // E asked again when no answer came in this long: the host refuses a seat without a word.
    private const float SitRetry = 2f;

    private enum TownStep
    {
        Settle,
        ToCrate,
        Strike,
        ToBench,
        Sitting,
        OnBench,
        OffBench,
        ToBed,
        Lying,
        OnBed,
        OffBed,
        Done,
    }

    private readonly Dictionary<PlayerCharacter, (SeatKind Kind, Vector3 Origin, float Since)> _onSeat = new();
    private readonly HashSet<string> _seenSitting = new();
    private readonly HashSet<string> _seenLying = new();
    private readonly HashSet<string> _seenGettingUp = new();
    private TownStep _townStep = TownStep.Settle;
    private float _townStepFor;
    private Breakable? _crate;
    private Vector3 _townSpot;
    private int _brokeMine;
    private int _crateHits = -1;
    private int _cratesBrokenShown;
    private int _cratesBrokenHeard;
    private int _splintersMost;
    private float _offSeatMost;
    private int _seatPromptFrames;
    private string _townFailed = "none";

    public bool TownDrill { get; init; }

    private void TrackTown()
    {
        Breakable.Broke += OnCrateBroke;
        Seating.Sat += OnSatDown;
        Seating.Stood += OnGotUp;
    }

    private void UntrackTown()
    {
        Breakable.Broke -= OnCrateBroke;
        Seating.Sat -= OnSatDown;
        Seating.Stood -= OnGotUp;
    }

    private void OnCrateBroke(Breakable crate, bool shown)
    {
        _cratesBrokenShown += shown ? 1 : 0;
        _cratesBrokenHeard += shown ? 0 : 1;
    }

    private void OnSatDown(PlayerCharacter player, Seat seat, Vector3 origin)
    {
        _onSeat[player] = (seat.Kind, origin, _time);
        (seat.Kind == SeatKind.Sit ? _seenSitting : _seenLying).Add(player.Name);
    }

    private void OnGotUp(PlayerCharacter player)
    {
        _onSeat.Remove(player);
        _seenGettingUp.Add(player.Name);
    }

    private void MeasureTown(float delta)
    {

        _seatPromptFrames += _hud.SeatPrompt.Shown != null ? 1 : 0;
        _splintersMost = Mathf.Max(_splintersMost, Splinters.Alive);
        foreach (var (player, (_, origin, since)) in _onSeat)
        {
            if (IsInstanceValid(player) && _time - since >= SettledOnSeat)
            {
                _offSeatMost = Mathf.Max(_offSeatMost, player.GlobalPosition.DistanceTo(origin));
            }
        }

        if (!TownDrill || LocalPlayer() is not { } local || local.Controls is not BotControls bot)
        {
            return;
        }

        bot.Suspended = true;
        _townStepFor += delta;
        if (_townStep != TownStep.Done && _townStepFor > TownStepLimit)
        {
            _townFailed = $"{_townStep}_at_{local.GlobalPosition.X:F1},{local.GlobalPosition.Y:F1},{local.GlobalPosition.Z:F1}";
            GoTo(TownStep.Done, bot);
            return;
        }

        var map = ArenaMap.In(GetTree());
        var seating = Seating.In(GetTree());
        switch (_townStep)
        {
            case TownStep.Settle when _townStepFor >= 1f:
                _crate = Breakable.All(GetTree()).Where(b => !b.IsBroken && Floors.SameLevel(b.GlobalPosition.Y, local.GlobalPosition.Y))
                    .MinBy(b => b.GlobalPosition.DistanceTo(local.GlobalPosition));
                if (_crate == null)
                {
                    _townFailed = "no_crate";
                    GoTo(TownStep.Done, bot);
                    break;
                }

                _townSpot = ReachableAround(map, local, Yaw.Flat(_crate.GlobalPosition) + Vector3.Up * local.GlobalPosition.Y, _crate.Radius + StrikeFrom);
                GoTo(TownStep.ToCrate, bot);
                break;
            case TownStep.ToCrate:
                if (Walked(map, local, bot, _townSpot))
                {
                    GoTo(TownStep.Strike, bot);
                }

                break;
            case TownStep.Strike:
                bot.DrillAim = Yaw.Of(_crate!.GlobalPosition - local.GlobalPosition);
                bot.DrillSkill = PlayerCharacter.Primary;
                if (_crate.IsBroken)
                {
                    _brokeMine = 1;
                    _crateHits = _crate.HitsShown;
                    var bench = seating.All.Where(s => s.Kind == SeatKind.Sit && !seating.IsTaken(s.Id)).MinBy(s => Distance(local, s));
                    _townSpot = bench == null ? local.GlobalPosition : InFrontOf(map, local, bench);
                    GoTo(TownStep.ToBench, bot);
                }

                break;
            case TownStep.ToBench:
                if (Walked(map, local, bot, _townSpot) || seating.InReachOf(local.GlobalPosition) is { Kind: SeatKind.Sit })
                {
                    bot.DrillMove = Vector3.Zero;
                    bot.Interact();
                    GoTo(TownStep.Sitting, bot);
                }

                break;
            case TownStep.Sitting when local.Seated == SeatKind.Sit:
                GoTo(TownStep.OnBench, bot);
                break;
            case TownStep.Sitting when _townStepFor >= SitRetry:
                bot.Interact();
                _townStepFor = 0f;
                break;
            case TownStep.OnBench when _townStepFor >= RestHold:
                bot.Interact();
                GoTo(TownStep.OffBench, bot);
                break;
            case TownStep.OffBench when !local.IsResting:
                var bed = seating.All.Where(s => s.Kind == SeatKind.Lie && !seating.IsTaken(s.Id)).MinBy(s => Distance(local, s));
                if (bed == null)
                {
                    _townFailed = "no_bed";
                    GoTo(TownStep.Done, bot);
                    break;
                }

                _townSpot = InFrontOf(map, local, bed);
                GoTo(TownStep.ToBed, bot);
                break;
            case TownStep.ToBed:
                if (Walked(map, local, bot, _townSpot) || seating.InReachOf(local.GlobalPosition) is { Kind: SeatKind.Lie })
                {
                    bot.DrillMove = Vector3.Zero;
                    bot.Interact();
                    GoTo(TownStep.Lying, bot);
                }

                break;
            case TownStep.Lying when local.Seated == SeatKind.Lie:
                GoTo(TownStep.OnBed, bot);
                break;
            case TownStep.Lying when _townStepFor >= SitRetry:
                bot.Interact();
                _townStepFor = 0f;
                break;
            case TownStep.OnBed when _townStepFor >= RestHold:
                bot.Interact();
                GoTo(TownStep.OffBed, bot);
                break;
            case TownStep.OffBed when !local.IsResting:
                GoTo(TownStep.Done, bot);
                break;
        }
    }

    private void GoTo(TownStep step, BotControls bot)
    {
        _townStep = step;
        _townStepFor = 0f;
        bot.DrillMove = Vector3.Zero;
        bot.DrillAim = null;
        bot.DrillSkill = null;
    }

    // Steps the bot along the ways toward the spot: whether it is there.
    private static bool Walked(ArenaMap map, PlayerCharacter local, BotControls bot, Vector3 spot)
    {
        bool there = Yaw.Flat(spot - local.GlobalPosition).Length() < AtSpot && Floors.SameLevel(spot.Y, local.GlobalPosition.Y);
        bot.DrillMove = there ? Vector3.Zero : map.StepToward(local, spot);
        return there;
    }

    // Of the eight spots this far round the middle, the nearest the ways reach.
    private static Vector3 ReachableAround(ArenaMap map, PlayerCharacter local, Vector3 middle, float distance) =>
        Enumerable.Range(0, 8).Select(i => middle + Yaw.Forward(i * Mathf.Tau / 8f) * distance)
            .Where(spot => map.HasWay(local.GlobalPosition, spot))
            .DefaultIfEmpty(middle)
            .MinBy(spot => spot.DistanceTo(local.GlobalPosition));

    // A step in front of the seat, on a side it is taken from, where the ways reach, on its floor.
    private static Vector3 InFrontOf(ArenaMap map, PlayerCharacter local, Seat seat)
    {
        float floor = Floors.Storey * Floors.LevelOf(seat.Spot.Y);
        var spot = new Vector3(seat.Spot.X, floor, seat.Spot.Z);
        float away = Core.Level.Seats.Reach * SeatFrom;
        var sides = seat.Facings.Select(f => spot + Yaw.Forward(f) * away)
            .Concat(seat.Facings.Select(f => spot + Yaw.Forward(f + Mathf.Pi / 2f) * away));
        return sides.Where(side => map.HasWay(local.GlobalPosition, side)).DefaultIfEmpty(spot).MinBy(side => side.DistanceTo(local.GlobalPosition));
    }

    private static float Distance(PlayerCharacter local, Seat seat) =>
        local.GlobalPosition.DistanceTo(new Vector3(seat.Spot.X, seat.Spot.Y, seat.Spot.Z));

    private void PrintTownCheck(long me)
    {
        int players = _seen.Count;
        GD.Print($"[town-check] me={me} players={players} broke_mine={_brokeMine} crate_hits={_crateHits} crates_broken_shown={_cratesBrokenShown} "
            + $"crates_broken_heard={_cratesBrokenHeard} crates_broken_here={Breakable.All(GetTree()).Count(b => b.IsBroken)} "
            + $"splinters_most={_splintersMost} splinters_left={Splinters.Alive} "
            + $"seen_sitting={_seenSitting.Count} seen_lying={_seenLying.Count} seen_getting_up={_seenGettingUp.Count} "
            + $"off_seat_most={_offSeatMost:F2} resting_at_end={PlayerCharacter.All(GetTree()).Count(p => p.IsResting)} "
            + $"prompt_frames={_seatPromptFrames} drill_done={(_townStep == TownStep.Done && _townFailed == "none" ? 1 : 0)} failed={_townFailed}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[town-host] seats={Seating.In(GetTree()).All.Count} seats_taken={Seating.In(GetTree()).Taken}");
        }
    }
}
