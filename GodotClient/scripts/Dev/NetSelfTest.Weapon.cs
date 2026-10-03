using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [trail-check]: weapon trails build during swings and clear after them. [reach-check]: the live blade tip at each
// hit test matches the skill's range.
public partial class NetSelfTest
{
    private readonly Dictionary<string, List<float>> _tipReachAtHit = new();
    private int _trailMaxEdges;
    private float _trailTipReach;
    private float _sinceTrailStopped;
    private int _trailLingering;
    private bool _enemyTrailSeen;

    private void TrackWeapon(PlayerCharacter player) =>
        player.HitTested += (skill, reach) =>
        {
            if (!_tipReachAtHit.TryGetValue(skill.Id, out var reaches))
            {
                _tipReachAtHit[skill.Id] = reaches = new List<float>();
            }

            reaches.Add(reach);
        };

    private void PrintWeaponChecks(long me)
    {
        var trail = LocalPlayer()?.Trail;
        GD.Print($"[trail-check] me={me} tip_length={(trail?.TipLength ?? float.NaN):F2} max_edges={_trailMaxEdges} "
            + $"tip_reach_max={_trailTipReach:F2} "
            + $"lingering_frames={_trailLingering} enemy_trail_seen={(_enemyTrailSeen ? 1 : 0)}");
        var weapon = LocalPlayer()?.Weapon ?? Weapons.Default;
        var reachBySkill = new[] { weapon.Primary, weapon.Secondary }.Select(s => $"{s.Id}={Median(_tipReachAtHit.GetValueOrDefault(s.Id)):F2}/{s.Range:F2}");
        GD.Print($"[reach-check] me={me} weapon={weapon.Id} {string.Join(" ", reachBySkill)}");
    }

    // The local trail must build during swings and be gone shortly after it stops recording. Tip reach is how far
    // the drawn blade tip gets from the body centre, to compare with the hit range.
    private void MeasureTrails(float delta)
    {
        var local = LocalPlayer();
        if (local != null)
        {
            var trail = local.Trail;
            _trailMaxEdges = Mathf.Max(_trailMaxEdges, trail.EdgeCount);
            if (trail.EdgeCount > 0)
            {
                _trailTipReach = Mathf.Max(_trailTipReach, Yaw.Flat(trail.LatestTip - local.GlobalPosition).Length());
            }

            _sinceTrailStopped = trail.Recording ? 0f : _sinceTrailStopped + delta;
            if (_sinceTrailStopped > WeaponTrail.FadeTime + 0.05f && trail.EdgeCount > 0)
            {
                _trailLingering++;
            }
        }

        _enemyTrailSeen |= EnemyCharacter.All(GetTree()).Any(e => e.Trail.EdgeCount >= 2);
    }
}
