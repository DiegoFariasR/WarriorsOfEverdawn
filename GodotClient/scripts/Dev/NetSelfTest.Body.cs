using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [anim-check]: the chest on the aim, with and without the torso twist. [speed-check]: ground speed per leg clip and
// swing playback at the attack speed. [turn-check]: turning within its limit. [head-check]: head sizes.
// [carry-check]: both hands stay on a weapon for two while running, a sword and shield are held up in their stance
// instead of swinging with the run, and the hands come apart standing with an empty hand.
public partial class NetSelfTest
{
    // Only frames where the twist does real work count toward the with/without comparison; below this the
    // clips' own chest sway dominates both numbers.
    private const float SignificantTwist = 20f * Mathf.Pi / 180f;

    private readonly Dictionary<LegDirection, (float Sum, int Count)> _torsoSignedByLegs = new();
    private readonly List<float> _swingShares = new();
    private float _torsoErrorSum;
    private float _torsoErrorMax;
    private int _torsoSamples;
    private float _twistedErrorSum;
    private float _untwistedErrorSum;
    private int _twistedSamples;
    private float? _lastSwingPosition;
    private string _groundSpeeds = "";
    private float? _lastAim;
    private float _maxTurnRate;
    private int _turnLimitedFrames;
    private float _playerHead = float.NaN;
    private float _enemyHead = float.NaN;
    private float _enemyHeadgear = float.NaN;
    private readonly List<float> _handsApartRunning = new();
    private readonly List<float> _handsApartRunningOneHanded = new();
    private readonly List<float> _handsApartStanding = new();
    private readonly List<float> _handsApartUnarmed = new();

    private void TrackBody(PlayerCharacter player)
    {
        var twist = player.Animator.Twist;
        twist.ModificationProcessed += () => MeasureTorso(player, twist);
        twist.ModificationProcessed += () => MeasureCarry(player);
        MeasureHeads(player.Skeleton, out _playerHead, out _);
    }

    private void PrintBodyChecks(long me)
    {
        float mean = _torsoSamples > 0 ? _torsoErrorSum / _torsoSamples : float.NaN;
        var byLegs = string.Join(",", _torsoSignedByLegs.OrderBy(p => p.Key).Select(p => $"{p.Key}:{Mathf.RadToDeg(p.Value.Sum / p.Value.Count):+0.0;-0.0}"));
        float withTwist = _twistedSamples > 0 ? _twistedErrorSum / _twistedSamples : float.NaN;
        float withoutTwist = _twistedSamples > 0 ? _untwistedErrorSum / _twistedSamples : float.NaN;
        GD.Print($"[anim-check] me={me} torso_vs_aim_mean_deg={Mathf.RadToDeg(mean):F1} torso_vs_aim_max_deg={Mathf.RadToDeg(_torsoErrorMax):F1} samples={_torsoSamples} "
            + $"twist_samples={_twistedSamples} with_twist_deg={Mathf.RadToDeg(withTwist):F1} without_twist_deg={Mathf.RadToDeg(withoutTwist):F1} signed_by_legs={byLegs}");
        GD.Print($"[speed-check] me={me} ground_speed={_groundSpeeds} swing_playback_share={Median(_swingShares):F3} samples={_swingShares.Count}");
        GD.Print($"[head-check] me={me} head_expected={CharacterBody.HeadScale:F3} headgear_expected={CharacterBody.HeadgearScale:F3} "
            + $"player_head={_playerHead:F3} enemy_head={_enemyHead:F3} enemy_headgear={_enemyHeadgear:F3}");
        GD.Print($"[carry-check] me={me} hands_apart_running={Median(_handsApartRunning):F2} samples={_handsApartRunning.Count} "
            + $"hands_apart_running_one_handed={Median(_handsApartRunningOneHanded):F2} one_handed_samples={_handsApartRunningOneHanded.Count} "
            + $"hands_apart_standing={Median(_handsApartStanding):F2} hands_apart_standing_unarmed={Median(_handsApartUnarmed):F2} unarmed_samples={_handsApartUnarmed.Count}");
        GD.Print($"[turn-check] me={me} max_turn_deg_s={Mathf.RadToDeg(_maxTurnRate):F0} limit_deg_s={Mathf.RadToDeg(Turning.MaxRate):F0} frames_at_limit={_turnLimitedFrames}");
    }

    // Only while running without an attack: swings, idle turns and lying down legitimately point the chest off the aim.
    private void MeasureTorso(PlayerCharacter player, TorsoTwistModifier twist)
    {
        if (player.Legs == null || player.Animator.IsAttacking || player.IsDowned || twist.ChestBone < 0)
        {
            return;
        }

        // An aim flicked further round than the torso can twist leaves the chest behind until the body turns.
        if (Mathf.Abs(twist.Twist) > TorsoTwistModifier.MaxTwist)
        {
            return;
        }

        var skeleton = twist.GetSkeleton();

        // KayKit bones rest facing +Z in skeleton space.
        var chestForward = skeleton.GlobalBasis * skeleton.GetBoneGlobalPose(twist.ChestBone).Basis * Vector3.Back;
        float signed = Angles.Wrap(Yaw.Of(chestForward) - player.AimYaw);
        float error = Mathf.Abs(signed);
        var legs = player.Legs.Value;
        var (sum, count) = _torsoSignedByLegs.GetValueOrDefault(legs);
        _torsoSignedByLegs[legs] = (sum + signed, count + 1);
        _torsoErrorSum += error;
        _torsoErrorMax = Mathf.Max(_torsoErrorMax, error);
        _torsoSamples++;

        // Without the modifier the chest would sit AppliedTwist further round.
        if (Mathf.Abs(twist.AppliedTwist) >= SignificantTwist)
        {
            _twistedErrorSum += error;
            _untwistedErrorSum += Mathf.Abs(Angles.Wrap(signed - twist.AppliedTwist));
            _twistedSamples++;
        }
    }

    // How far apart the hands are while running with nothing else playing: a two-handed weapon keeps them together,
    // and a weapon in one hand with a shield on the other keeps them in its stance, nearer than the run clip swings
    // them. Running with empty hands is left out: the arms swing then, and a bot taken down unarmed runs a long way
    // back to its weapon from the town. Standing with a weapon in one hand is left out too: its stance is the one
    // empty hands stand in.
    private void MeasureCarry(PlayerCharacter player)
    {
        bool oneHanded = player.Animator.Stance == WeaponStance.OneHanded;
        if (player.Animator.IsAttacking || player.IsDashing || player.IsDowned || (player.Legs != null && player.Weapon == null)
            || (player.Legs == null && oneHanded))
        {
            return;
        }

        var skeleton = player.Skeleton;
        var left = skeleton.GetBoneGlobalPose(skeleton.FindBone("hand.l")).Origin;
        var right = skeleton.GetBoneGlobalPose(skeleton.FindBone("hand.r")).Origin;
        var samples = player.Legs != null ? (oneHanded ? _handsApartRunningOneHanded : _handsApartRunning)
            : player.Weapon != null ? _handsApartStanding
            : _handsApartUnarmed;
        samples.Add(left.DistanceTo(right));
    }

    // Clip seconds per real second while a swing plays. Frames where the clip restarts or sits clamped at its end
    // are skipped, so only steady playback counts.
    private void MeasureSwingRate(float delta)
    {
        var local = LocalPlayer();
        if (local == null)
        {
            return;
        }

        if (_groundSpeeds.Length == 0)
        {
            _groundSpeeds = string.Join(",", local.Animator.GroundSpeedByLegs.Select(p => $"{p.Key}:{p.Value:F2}"));
        }

        // A lunge plays at its dash's pace, not the attack speed.
        if (!local.Animator.IsAttacking || IsLunge(local.ActiveSkill))
        {
            _lastSwingPosition = null;
            return;
        }

        float position = local.Animator.AttackClipPosition;
        if (_lastSwingPosition is { } last && position > last && position < local.Animator.AttackClipLength - 0.001f)
        {
            // As a share of what the skill plays at of itself, so a quick skill reads as the attack speed too, and of
            // the attack speed of the moment: AGI rises as points go into it.
            _swingShares.Add((position - last) / delta / (local.ActiveSkill?.SwingSpeed ?? 1f) / local.AttackSpeed);
        }

        _lastSwingPosition = position;
    }

    // One aim update happens per physics frame, so consecutive readings are exactly one turn step apart.
    private void MeasureTurn(float delta)
    {
        var local = LocalPlayer();
        if (local == null)
        {
            return;
        }

        if (_lastAim is { } last)
        {
            float rate = Mathf.Abs(Angles.Wrap(local.AimYaw - last)) / delta;
            _maxTurnRate = Mathf.Max(_maxTurnRate, rate);
            if (rate >= Turning.MaxRate * 0.99f)
            {
                _turnLimitedFrames++;
            }
        }

        _lastAim = local.AimYaw;
    }

    // The scale actually on a live character's head and headgear meshes: the part in its head slot, and an
    // accessory the head carries, skinned beside the head or hung on the head bone (the skeleton warrior's helmet).
    // Players are bare-headed, so headgear is read off the skeletons.
    private static void MeasureHeads(Skeleton3D skeleton, out float head, out float headgear)
    {
        head = float.NaN;
        headgear = float.NaN;
        foreach (var (mesh, part, slot) in CharacterBody.PartsOn(skeleton))
        {
            if (slot == CharacterSlot.Head)
            {
                head = mesh.Transform.Basis.Scale.Y;
            }
            else if (slot == CharacterSlot.Accessory && CharacterBody.Catalog.Get(part).OnHead)
            {
                headgear = mesh.Transform.Basis.Scale.Y;
            }
        }
    }

    // Enemies come and go, so the latest one wearing headgear is measured.
    private void MeasureEnemyHelmet()
    {
        foreach (var enemy in EnemyCharacter.All(GetTree()))
        {
            MeasureHeads(enemy.Skeleton, out float head, out float headgear);
            _enemyHead = head;
            if (!float.IsNaN(headgear))
            {
                _enemyHeadgear = headgear;
            }
        }
    }
}
