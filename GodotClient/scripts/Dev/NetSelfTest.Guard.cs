using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [guard-check]: players raise their guards and every machine sees it; a guard stays up while held, until a dash or
// going down drops it; guarded players move no faster than their guard allows; and on the host, where hits are
// decided, guards block, parry, and parried skeletons reel.
public partial class NetSelfTest
{
    private int _guardsRaisedHere;
    private int _guardsSeenRemote;
    private int _blocks;
    private int _parries;
    private int _enemiesParried;
    private int _guardFrames;
    private int _guardDrops;
    private float _guardSpeedShareMax;
    private bool _wasGuarding;

    private void TrackGuard(PlayerCharacter player) =>
        player.GuardRaised += () =>
        {
            if (player.IsMultiplayerAuthority())
            {
                _guardsRaisedHere++;
            }
            else
            {
                _guardsSeenRemote++;
            }
        };

    private void OnGuarded(PlayerCharacter player, GuardOutcome outcome)
    {
        _blocks += outcome == GuardOutcome.Blocked ? 1 : 0;
        _parries += outcome == GuardOutcome.Parried ? 1 : 0;
    }

    private void OnEnemyParried(EnemyCharacter enemy) => _enemiesParried++;

    private void PrintGuardCheck(long me) =>
        GD.Print($"[guard-check] me={me} raised_here={_guardsRaisedHere} seen_remote={_guardsSeenRemote} blocks={_blocks} "
            + $"parries={_parries} enemies_parried={_enemiesParried} guard_frames={_guardFrames} dropped_while_held={_guardDrops} "
            + $"guard_speed_share_max={_guardSpeedShareMax:F3}");

    // The frame the guard goes up is skipped: that frame's movement was decided before it.
    private void MeasureGuardSpeed()
    {
        var local = LocalPlayer();
        if (local is not { IsGuarding: true } || local.IsDashing)
        {
            if (_wasGuarding && local is { Controls.GuardHeld: true, IsDashing: false, IsDowned: false })
            {
                _guardDrops++;
            }

            _wasGuarding = false;
            return;
        }

        if (_wasGuarding && local.Weapon is { } weapon)
        {
            _guardFrames++;
            float limit = MoveSpeed.Run * weapon.Guard.MoveSpeedFactor;
            _guardSpeedShareMax = Mathf.Max(_guardSpeedShareMax, Yaw.Flat(local.NetVelocity).Length() / limit);
        }

        _wasGuarding = true;
    }
}
