using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [skill-check]: the weapon's Spin is used and lands, moves no faster than its share of run speed, and turns the whole
// body.
public partial class NetSelfTest
{
    private const float FullWeight = 0.999f;

    private int _spins;
    private int _spinFrames;
    private int _spinMovingFrames;
    private float _spinMaxSpeed;
    private float _spinSpeedLimit = float.NaN;
    private bool _wasSpinning;
    private float _spinTurn;
    private float _spinTurnExpected;
    private float _spinRevolutions;
    private float _lastSpinWeight;
    private float? _lastRootYaw;

    private void TrackSpin(PlayerCharacter player) =>
        player.AttackStarted += skill => _spins += skill.Channeled ? 1 : 0;

    private void PrintSpinCheck(long me)
    {
        float turnRatio = _spinTurnExpected > 0f ? Mathf.Abs(_spinTurn) / _spinTurnExpected : float.NaN;
        var weapon = LocalPlayer()?.Weapon ?? Weapons.Default;
        GD.Print($"[skill-check] me={me} weapon={weapon.Id} spins={_spins} spin_revolutions={_spinRevolutions:F1} spin_hits={_hitsBySkill.GetValueOrDefault(weapon.Secondary.Id)} "
            + $"primary_hits={_hitsBySkill.GetValueOrDefault(weapon.Primary.Id)} spin_turn_ratio={turnRatio:F2} "
            + $"spin_frames={_spinFrames} spin_moving_frames={_spinMovingFrames} spin_max_speed={_spinMaxSpeed:F2} spin_speed_limit={_spinSpeedLimit:F2}");
    }

    // While a slowing swing (Spin) plays, the local player may move, but never faster than its share of run speed.
    // The frame a swing starts is skipped: the movement for that frame was decided before the swing began.
    private void MeasureSpinMovement()
    {
        // A dash carries a Spin along at the dash's own speed.
        var slowing = LocalPlayer() is { ActiveSkill: { MoveSpeedFactor: < 1f } skill, IsDashing: false } local ? (local, skill) : default;
        if (slowing.local == null)
        {
            _wasSpinning = false;
            return;
        }

        if (_wasSpinning)
        {
            float speed = new Vector2(slowing.local.NetVelocity.X, slowing.local.NetVelocity.Z).Length();
            _spinFrames++;
            _spinMaxSpeed = Mathf.Max(_spinMaxSpeed, speed);
            _spinSpeedLimit = MoveSpeed.Run * slowing.skill.MoveSpeedFactor;
            if (speed > 0.1f)
            {
                _spinMovingFrames++;
            }
        }

        _wasSpinning = true;
    }

    // Spin's loop turns the whole body one revolution per cycle through the root bone. While it plays at full
    // strength, the root's measured turn over one revolution per cycle is near 1 when the loop plays full-body at the
    // attack speed; on the upper body only, the root would not turn at all.
    private void MeasureSpinTurn(float delta)
    {
        var local = LocalPlayer();
        if (local == null || local.ActiveSkill is not { Channeled: true } spin)
        {
            _lastRootYaw = null;
            return;
        }

        var skeleton = local.Skeleton;
        var facing = skeleton.GetBoneGlobalPose(skeleton.FindBone("root")).Basis * Vector3.Back;
        float yaw = Mathf.Atan2(facing.X, facing.Z);
        float weight = local.Animator.AttackWeight;
        if (_lastRootYaw is { } last)
        {
            float revolutions = delta * CombatTiming.SwingSpeed(spin, local.AttackSpeed) / local.Animator.AttackClipLength;
            _spinRevolutions += revolutions;

            // Only full-strength frames: fading in or out, the root blends back toward the plain pose and turns
            // against the spin, by up to half a turn per spin, so short spins would read low for reasons that have
            // nothing to do with whether the loop plays full-body.
            if (weight >= FullWeight && _lastSpinWeight >= FullWeight)
            {
                _spinTurn += Angles.Wrap(yaw - last);
                _spinTurnExpected += revolutions * Mathf.Tau;
            }
        }

        _lastRootYaw = yaw;
        _lastSpinWeight = weight;
    }
}
