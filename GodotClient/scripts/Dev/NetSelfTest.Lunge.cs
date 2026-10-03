using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [lunge-check]: a dash thrown with the attack button carries a thrust, every machine sees it, it lands hits, its
// hit window closes as the dash ends (counted in physics frames, which a slow frame does not stretch), and the weapon then reaches as far as the lunge's range (each lunge against
// its own weapon's, since bots swap sets). Reach is taken from lunges forward, along the aim: the dash clips to the
// side and back lean the hips less, and reach about 0.25 short.
public partial class NetSelfTest
{
    private int _lungesHere;
    private int _lungesSeenRemote;
    private int _lungeHits;
    private bool _lungePending;
    private ulong? _lungeClosedAt;
    private ulong? _lungeDashEndedAt;
    private ulong _lungeLandOffsetMax;
    private readonly List<float> _lungeReachOff = new();
    private float _lungeReach;
    private bool _dashingForward;

    // By id: an improved weapon's lunge is its plain one hitting harder.
    private static bool IsLunge(SkillDefinition? skill) => skill != null && Weapons.All.Any(w => w.Lunge.Id == skill.Id);

    private void TrackLunges(PlayerCharacter player)
    {
        if (!player.IsMultiplayerAuthority())
        {
            player.AttackStarted += skill => _lungesSeenRemote += IsLunge(skill) ? 1 : 0;
            return;
        }

        player.DashStarted += direction => _dashingForward = Mathf.Abs(Angles.Wrap(Yaw.Of(direction) - player.AimYaw)) < Mathf.Pi / 4f;
        player.AttackStarted += skill =>
        {
            if (IsLunge(skill))
            {
                _lungesHere++;
                _lungePending = true;
                _lungeClosedAt = null;
                _lungeDashEndedAt = null;
                _lungeReach = 0f;
            }
        };

        // The weapon is furthest out as the window closes, at full extension.
        player.HitTested += (skill, reach) => _lungeReach = IsLunge(skill) ? reach : _lungeReach;
        player.HitsSent += (skill, hits) =>
        {
            if (IsLunge(skill))
            {
                _lungeHits += hits;
                if (_dashingForward)
                {
                    _lungeReachOff.Add(_lungeReach - skill.Range);
                }

                _lungeClosedAt = Engine.GetPhysicsFrames();
                SettleLunge();
            }
        };
        player.DashFinished += (_, _) =>
        {
            _lungeDashEndedAt = Engine.GetPhysicsFrames();
            SettleLunge();
        };
    }

    // Once a lunge's window has closed and its dash has ended: how far apart the two were.
    private void SettleLunge()
    {
        if (!_lungePending || _lungeClosedAt is not { } closed || _lungeDashEndedAt is not { } ended)
        {
            return;
        }

        _lungeLandOffsetMax = Math.Max(_lungeLandOffsetMax, closed > ended ? closed - ended : ended - closed);
        _lungePending = false;
    }

    private void PrintLungeCheck(long me)
    {
        GD.Print($"[lunge-check] me={me} lunges_here={_lungesHere} seen_remote={_lungesSeenRemote} lunge_hits={_lungeHits} "
            + $"land_offset_frames_max={_lungeLandOffsetMax} forward_lunges={_lungeReachOff.Count} reach_off_range={Median(_lungeReachOff):F2}");
    }
}
