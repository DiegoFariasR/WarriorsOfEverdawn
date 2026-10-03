using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Legs play a directional locomotion clip at the rate that matches the ground speed, with the arms kept in the
// two-handed stance while a weapon for both hands is held; attacks layer on top at their own speed, a dash over those, and death over everything. The
// attack drives the upper body always and the lower body only while standing still, since some swings (Slice, Stab)
// rotate through the hips. A dash drives the whole body unless it leaves the upper body to a thrust, or all of it
// to a Spin that carries on through it.
// Design: Docs/Design/locomotion.md.
public sealed class CharacterAnimator
{
    private const float LegCrossfade = 0.15f;
    private const float AttackFadeIn = 0.08f;
    private const float AttackFadeOut = 0.2f;
    private const float StillnessRate = 8f;
    private const float MinLegSpeedScale = 0.3f;
    private const float DeathBlend = 0.15f;
    private const float CancelFade = 0.05f;
    private const float DashFadeIn = 0.03f;
    private const float DashFadeOut = 0.06f;

    private const string IdleState = "idle";
    private const string UnarmedIdleState = "idle_unarmed";

    // The bones the legs clip keeps while a swing plays over a moving body.
    public static readonly IReadOnlyList<string> LowerBones = new[]
    {
        "root", "hips",
        "upperleg.l", "lowerleg.l", "foot.l", "toes.l",
        "upperleg.r", "lowerleg.r", "foot.r", "toes.r",
    };

    // Both arms hang off the chest, so their poses from one clip keep the hands together over another clip's chest.
    private static readonly string[] ArmBones =
    {
        "upperarm.l", "lowerarm.l", "wrist.l", "hand.l", "handslot.l",
        "upperarm.r", "lowerarm.r", "wrist.r", "hand.r", "handslot.r",
    };

    private static readonly string[] UpperBones = new[] { "spine", "chest", "head" }.Concat(ArmBones).ToArray();

    // Travel is the clip's direction of motion in skeleton space: KayKit faces +Z, and +X is its left.
    private static readonly (string State, string Clip, Vector3 Travel)[] LegClips =
    {
        (IdleState, RigAnimations.Idle, Vector3.Zero),
        (UnarmedIdleState, RigAnimations.UnarmedIdle, Vector3.Zero),
        (nameof(LegDirection.Forward), RigAnimations.Run, Vector3.Back),
        (nameof(LegDirection.Left), RigAnimations.StrafeLeft, Vector3.Right),
        (nameof(LegDirection.Right), RigAnimations.StrafeRight, Vector3.Left),
        (nameof(LegDirection.Backward), RigAnimations.Backpedal, Vector3.Forward),
    };

    private static readonly StringName LegsRequest = "parameters/legs/transition_request";
    private static readonly StringName LegsScale = "parameters/legs_speed/scale";
    private static readonly StringName StanceAmount = "parameters/stance_mix/blend_amount";
    private static readonly StringName LowerAmount = "parameters/attack_lower_mix/blend_amount";
    private static readonly StringName UpperAmount = "parameters/attack_upper_mix/blend_amount";
    private static readonly StringName LowerSeek = "parameters/attack_lower_seek/seek_request";
    private static readonly StringName UpperSeek = "parameters/attack_upper_seek/seek_request";
    private static readonly StringName DeathAmount = "parameters/death_mix/blend_amount";
    private static readonly StringName DeathSeek = "parameters/death_seek/seek_request";
    private static readonly StringName AttackProgress = "parameters/attack_upper/current_position";
    private static readonly StringName DashLowerAmount = "parameters/dash_lower_mix/blend_amount";
    private static readonly StringName DashUpperAmount = "parameters/dash_upper_mix/blend_amount";
    private static readonly StringName DashLowerSeek = "parameters/dash_lower_seek/seek_request";
    private static readonly StringName DashUpperSeek = "parameters/dash_upper_seek/seek_request";
    private static readonly StringName DashLowerSpeed = "parameters/dash_lower_speed/scale";
    private static readonly StringName DashUpperSpeed = "parameters/dash_upper_speed/scale";
    private static readonly StringName LowerAttackSpeed = "parameters/attack_lower_speed/scale";
    private static readonly StringName UpperAttackSpeed = "parameters/attack_upper_speed/scale";

    private readonly AnimationTree _tree;
    // Parked on a valid clip until the first attack; the layer weight is zero until then.
    private readonly AnimationNodeAnimation _stanceArms = new() { Animation = RigAnimations.Idle };
    private readonly AnimationNodeAnimation _attackLower = new() { Animation = RigAnimations.Idle };
    private readonly AnimationNodeAnimation _attackUpper = new() { Animation = RigAnimations.Idle };
    private WeaponStance _stance = WeaponStance.TwoHanded;
    private string _legState = IdleState;
    private float _stillness = 1f;
    private float _attackTime;
    private float _attackLength;
    private bool _fullBodyAttack;
    private readonly AnimationNodeAnimation _dashLower = new() { Animation = RigAnimations.Idle };
    private readonly AnimationNodeAnimation _dashUpper = new() { Animation = RigAnimations.Idle };
    private float _dashTime;
    private float _dashLength;
    private bool _dashHoldsLegs = true;
    private bool _dashHoldsUpperBody = true;
    private float _chestBias;
    private bool _dying;
    private float _deathWeight;
    private readonly Dictionary<string, float> _chestYawByState = new();
    private readonly Dictionary<string, float> _groundSpeedByState = new();

    public CharacterAnimator(Node3D model)
    {
        var skeleton = CharacterBody.SkeletonOf(model);
        Twist = new TorsoTwistModifier { Name = "TorsoTwist" };
        skeleton.AddChild(Twist);

        _tree = new AnimationTree { Name = "AnimationTree", TreeRoot = BuildTree() };
        RigAnimations.AddTo(_tree);
        model.AddChild(_tree);
        _tree.Set("parameters/death_speed/scale", RigAnimations.PlaybackSpeed);

        foreach (var (state, clip, travel) in LegClips)
        {
            if (travel != Vector3.Zero)
            {
                var animation = _tree.GetAnimation(clip);
                _chestYawByState[state] = ClipMotion.MeanChestYaw(skeleton, clip, animation);
                _groundSpeedByState[state] = ClipMotion.GroundSpeed(skeleton, clip, animation, travel);
            }
        }
    }

    public TorsoTwistModifier Twist { get; }

    // How the arms are carried, standing and over the leg clips: both hands on a weapon for two; a weapon in one
    // and a shield on the other held up in the stance KayKit calls unarmed, fists raised; with empty hands that same
    // stance standing, and the arms swinging with the legs.
    public WeaponStance Stance
    {
        get => _stance;
        set
        {
            _stance = value;
            _stanceArms.Animation = value == WeaponStance.OneHanded ? RigAnimations.UnarmedIdle : RigAnimations.Idle;
        }
    }

    public bool IsAttacking => _attackTime < _attackLength;

    public bool IsDashing => _dashTime < _dashLength;

    public IReadOnlyDictionary<string, float> GroundSpeedByLegs => _groundSpeedByState;

    public float AttackClipLength { get; private set; }

    // How strongly the swing currently drives the upper body, 0..1, after its fade in and out.
    public float AttackWeight { get; private set; }

    // Seconds into the current swing clip, in clip time.
    public float AttackClipPosition => (float)_tree.Get(AttackProgress).AsDouble();

    // A looping swing plays until EndLoop; a one-shot ends with its clip.
    public void PlayAttack(StringName clip, bool fullBody, float attackSpeed, bool loop)
    {
        _tree.Set(LowerAttackSpeed, attackSpeed);
        _tree.Set(UpperAttackSpeed, attackSpeed);
        _fullBodyAttack = fullBody;
        _attackLower.Animation = clip;
        _attackUpper.Animation = clip;
        _tree.Set(LowerSeek, 0.0);
        _tree.Set(UpperSeek, 0.0);
        _attackTime = 0f;
        AttackClipLength = (float)_tree.GetAnimation(clip).Length;
        _attackLength = loop ? float.PositiveInfinity : AttackClipLength / attackSpeed;
    }

    public void PlayDeath()
    {
        _tree.Set(DeathSeek, 0.0);
        _dying = true;
        _attackLength = _attackTime;
    }

    // Fades a looping swing out from where it is now.
    public void EndLoop() => _attackLength = Mathf.Min(_attackLength, _attackTime + AttackFadeOut);

    // Drops whatever swing is playing, quickly, so something else (a dash) can take over.
    public void CancelAttack() => _attackLength = Mathf.Min(_attackLength, _attackTime + CancelFade);

    // Plays a dash clip squeezed or stretched to last exactly duration.
    public void PlayDash(StringName clip, float duration)
    {
        float speed = (float)_tree.GetAnimation(clip).Length / duration;
        _dashLower.Animation = clip;
        _dashUpper.Animation = clip;
        _tree.Set(DashLowerSeek, 0.0);
        _tree.Set(DashUpperSeek, 0.0);
        _tree.Set(DashLowerSpeed, speed);
        _tree.Set(DashUpperSpeed, speed);
        _dashTime = 0f;
        _dashLength = duration;
        _dashHoldsLegs = true;
        _dashHoldsUpperBody = true;
    }

    // For the rest of this dash the legs keep its clip and the upper body goes back to the attack layer: a thrust
    // thrown on the move.
    public void LeaveUpperBodyToAttack() => _dashHoldsUpperBody = false;

    // For the rest of this dash its clip does not show at all: the attack layer keeps the whole body (a Spin that
    // carries on through the dash).
    public void LeaveBodyToAttack()
    {
        _dashHoldsLegs = false;
        _dashHoldsUpperBody = false;
    }

    public void Revive() => _dying = false;

    public void Update(float delta, LegDirection? legs, float speed, float twist)
    {
        string state = legs?.ToString() ?? (Stance == WeaponStance.TwoHanded ? IdleState : UnarmedIdleState);
        if (state != _legState)
        {
            _tree.Set(LegsRequest, state);
            _legState = state;
        }

        _tree.Set(LegsScale, legs == null ? RigAnimations.PlaybackSpeed : Mathf.Max(MinLegSpeedScale, speed / _groundSpeedByState[state]));
        _stillness = Mathf.MoveToward(_stillness, legs == null ? 1f : 0f, StillnessRate * delta);

        float attackWeight = 0f;
        if (IsAttacking)
        {
            _attackTime += delta;
            attackWeight = Mathf.Min(1f, _attackTime / AttackFadeIn)
                * Mathf.Clamp((_attackLength - _attackTime) / AttackFadeOut, 0f, 1f);
        }

        // The legs clips swing the arms as if empty-handed; over them armed arms keep their stance. Standing, the
        // legs clip is the stance itself.
        _tree.Set(StanceAmount, Stance == WeaponStance.Unarmed ? 0f : 1f - _stillness);
        AttackWeight = attackWeight;
        _tree.Set(UpperAmount, attackWeight);
        _tree.Set(LowerAmount, attackWeight * (_fullBodyAttack ? 1f : _stillness));

        // Leg clips turn the chest on their own (Walking_Backwards by about 32 deg), so that bias comes off the twist
        // to keep the chest on the aim. Swings are authored relative to the body, so an attack takes the bias away.
        // The idle stance keeps its authored chest angle.
        float bias = legs == null ? 0f : _chestYawByState[state];
        _chestBias = Mathf.Lerp(_chestBias, bias, Easing.Share(1f / LegCrossfade, delta));
        float dashWeight = 0f;
        if (IsDashing)
        {
            _dashTime += delta;
            dashWeight = Mathf.Min(1f, _dashTime / DashFadeIn) * Mathf.Clamp((_dashLength - _dashTime) / DashFadeOut, 0f, 1f);
        }

        float dashUpperWeight = _dashHoldsUpperBody ? dashWeight : 0f;
        _tree.Set(DashLowerAmount, _dashHoldsLegs ? dashWeight : 0f);
        _tree.Set(DashUpperAmount, dashUpperWeight);
        _deathWeight = Mathf.MoveToward(_deathWeight, _dying ? 1f : 0f, delta / DeathBlend);
        _tree.Set(DeathAmount, _deathWeight);
        // A full-body swing (Spin) turns the body itself, so the torso twist toward the aim gives way to it.
        float swingOwnsTorso = _fullBodyAttack ? attackWeight : 0f;
        Twist.Twist = (twist - _chestBias * (1f - attackWeight)) * (1f - _deathWeight) * (1f - dashUpperWeight) * (1f - swingOwnsTorso);
    }

    private AnimationNodeBlendTree BuildTree()
    {
        var root = new AnimationNodeBlendTree();

        var legs = new AnimationNodeTransition { XfadeTime = LegCrossfade, Sync = true, InputCount = LegClips.Length };
        root.AddNode("legs", legs);
        for (int i = 0; i < LegClips.Length; i++)
        {
            var (state, clip, _) = LegClips[i];
            legs.SetInputName(i, state);
            root.AddNode($"leg_{state}", new AnimationNodeAnimation { Animation = clip });
            root.ConnectNode("legs", i, $"leg_{state}");
        }

        root.AddNode("legs_speed", new AnimationNodeTimeScale());
        root.ConnectNode("legs_speed", 0, "legs");

        AddLayer(root, "stance", _stanceArms, ArmBones, below: "legs_speed");
        AddLayer(root, "attack_lower", _attackLower, LowerBones, below: "stance_mix");
        AddLayer(root, "attack_upper", _attackUpper, UpperBones, below: "attack_lower_mix");

        // Not the root bone: the dash clips carry their own root travel (0.25-0.65), and the code already moves the
        // character the full dash distance.
        AddLayer(root, "dash_lower", _dashLower, LowerBones.Where(b => b != "root"), below: "attack_upper_mix");
        AddLayer(root, "dash_upper", _dashUpper, UpperBones, below: "dash_lower_mix");

        root.AddNode("death", new AnimationNodeAnimation { Animation = RigAnimations.PlayerDeath });
        root.AddNode("death_seek", new AnimationNodeTimeSeek());
        root.ConnectNode("death_seek", 0, "death");
        root.AddNode("death_speed", new AnimationNodeTimeScale());
        root.ConnectNode("death_speed", 0, "death_seek");
        root.AddNode("death_mix", new AnimationNodeBlend2());
        root.ConnectNode("death_mix", 0, "dash_upper_mix");
        root.ConnectNode("death_mix", 1, "death_speed");
        root.ConnectNode("output", 0, "death_mix");
        return root;
    }

    // A clip with its own seek and speed, mixed over what is below it on the given bones only.
    private static void AddLayer(AnimationNodeBlendTree root, string layer, AnimationNodeAnimation clip, IEnumerable<string> bones, string below)
    {
        var mix = new AnimationNodeBlend2 { FilterEnabled = true };
        foreach (var bone in bones)
        {
            mix.SetFilterPath($"{RigAnimations.SkeletonPath}:{bone}", true);
        }

        root.AddNode(layer, clip);
        root.AddNode($"{layer}_seek", new AnimationNodeTimeSeek());
        root.ConnectNode($"{layer}_seek", 0, layer);
        root.AddNode($"{layer}_speed", new AnimationNodeTimeScale());
        root.ConnectNode($"{layer}_speed", 0, $"{layer}_seek");
        root.AddNode($"{layer}_mix", mix);
        root.ConnectNode($"{layer}_mix", 0, below);
        root.ConnectNode($"{layer}_mix", 1, $"{layer}_speed");
    }
}

public enum WeaponStance
{
    Unarmed,
    TwoHanded,
    OneHanded,
}
