using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [map-check]: the local player starts inside the allied town and takes no hit while it stands there; no skeleton is
// ever inside the town; every skeleton rises inside the enemy fortress and skeletons get out of it; the local player
// gets out of the town and as far as the fortress; walls between the camera and the player fade, and what hangs on
// them with them; and walkers find their way (after the first second, nobody had to be sent straight for want of a
// path).
public partial class NetSelfTest
{
    // The navigation mesh joins its map at the end of the first physics frame; ways asked before that go straight.
    private const float WaysReadyAfter = 1f;

    private readonly HashSet<ulong> _seenRising = new();
    private readonly HashSet<ulong> _leftFortress = new();
    private ArenaMap? _map;
    private int _startedSafe = -1;
    private int _hitsWhileSafe;
    private int _enemiesInTownFrames;
    private int _roseOutsideFortress;
    private int _straightStepsAtStart = -1;
    private float _nearestToFortress = float.PositiveInfinity;
    private bool _leftTown;
    private int _wallsFadedMax;
    private int _hangingsFadedMax;

    private void TrackMap() => _map = ArenaMap.In(GetTree());

    private void TrackSafety(PlayerCharacter player)
    {
        _startedSafe = _map!.IsSafe(player.GlobalPosition) ? 1 : 0;
        player.Vitals.Hit += _ => _hitsWhileSafe += _map.IsSafe(player.GlobalPosition) ? 1 : 0;
    }

    private void MeasureMap()
    {
        if (_map == null)
        {
            return;
        }

        if (_straightStepsAtStart < 0 && _time >= WaysReadyAfter)
        {
            _straightStepsAtStart = _map.StraightSteps;
        }

        var fortress = _map.Fortress;
        foreach (var enemy in GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>())
        {
            var at = Yaw.ToGround(enemy.GlobalPosition);
            if (_seenRising.Add(enemy.GetInstanceId()) && !fortress.Contains(at))
            {
                _roseOutsideFortress++;
            }

            if (!enemy.IsDead && !fortress.Contains(at))
            {
                _leftFortress.Add(enemy.GetInstanceId());
            }

            _enemiesInTownFrames += !enemy.IsDead && _map.IsSafe(enemy.GlobalPosition) ? 1 : 0;
        }

        if (LocalPlayer() is { } local)
        {
            _leftTown |= !_map.IsSafe(local.GlobalPosition);
            _wallsFadedMax = Mathf.Max(_wallsFadedMax, _map.FadedWalls);
            _hangingsFadedMax = Mathf.Max(_hangingsFadedMax, _map.FadedHangings);
            _nearestToFortress = Mathf.Min(_nearestToFortress, local.GlobalPosition.DistanceTo(_map.FortressGate));
        }
    }

    private void PrintMapCheck(long me) =>
        GD.Print($"[map-check] me={me} started_safe={_startedSafe} left_town={(_leftTown ? 1 : 0)} nearest_to_fortress_gate={_nearestToFortress:F1} "
            + $"hits_while_safe={_hitsWhileSafe} enemies_in_town_frames={_enemiesInTownFrames} rose_outside_fortress={_roseOutsideFortress} "
            + $"enemies_seen={_seenRising.Count} enemies_left_fortress={_leftFortress.Count} walls_faded_max={_wallsFadedMax} "
            + $"hangings={_map?.Hangings ?? -1} hangings_faded_max={_hangingsFadedMax} "
            + $"straight_steps_after_start={(_map == null || _straightStepsAtStart < 0 ? -1 : _map.StraightSteps - _straightStepsAtStart)}");
}
