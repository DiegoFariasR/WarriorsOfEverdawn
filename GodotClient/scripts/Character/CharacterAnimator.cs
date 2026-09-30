using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Locomotion;

namespace WarriorsOfEverdawn.Character;

// Legs play a directional locomotion clip at the rate that matches the ground speed; attacks layer on top at the
// character's attack speed, a dodge over those, and death over everything. The attack drives the upper body always and the lower body only
// while standing still, since some swings (Slice, Stab) rotate through the hips.
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
    private const float DodgeFadeIn = 0.03f;
    private const float DodgeFadeOut = 0.06f;

    private const string IdleState = "idle";

    private static readonly string[] LowerBones =
    {
        "root", "hips",
        "upperleg.l", "lowerleg.l", "foot.l", "toes.l",
        "upperleg.r", "lowerleg.r", "foot.r", "toes.r",
    };

    private static readonly string[] UpperBones =
    {
        "spine", "chest", "head",
        "upperarm.l", "lowerarm.l", "wrist.l", "hand.l", "handslot.l",
        "upperarm.r", "lowerarm.r", "wrist.r", "hand.r", "handslot.r",
    };

    // Travel is the clip's direction of motion in skeleton space: KayKit faces +Z, and +X is its left.
    private static readonly (string State, string Clip, Vector3 Travel)[] LegClips =
    {
        (IdleState, RigAnimations.Idle, Vector3.Zero),
        (nameof(LegDirection.Forward), RigAnimations.Run, Vector3.Back),
        (nameof(LegDirection.Left), RigAnimations.StrafeLeft, Vector3.Right),
        (nameof(LegDirection.Right), RigAnimations.StrafeRight, Vector3.Left),
        (nameof(LegDirection.Backward), RigAnimations.Backpedal, Vector3.Forward),
    };

    private static readonly StringName LegsRequest = "parameters/legs/transition_request";
    private static readonly StringName LegsScale = "parameters/legs_speed/scale";
    private static readonly StringName LowerAmount = "parameters/lower_mix/blend_amount";
    private static readonly StringName UpperAmount = "parameters/upper_mix/blend_amount";
    private static readonly StringName LowerSeek = "parameters/attack_lower_seek/seek_request";
    private static readonly StringName UpperSeek = "parameters/attack_upper_seek/seek_request";
    private static readonly StringName DeathAmount = "parameters/death_mix/blend_amount";
    private static readonly StringName DeathSeek = "parameters/death_seek/seek_request";
    private static readonly StringName AttackProgress = "parameters/attack_upper/current_position";
    private static readonly StringName DodgeAmount = "parameters/dodge_mix/blend_amount";
    private static readonly StringName DodgeSeek = "parameters/dodge_seek/seek_request";
    private static readonly StringName DodgeSpeed = "parameters/dodge_speed/scale";
    private static readonly StringName LowerAttackSpeed = "parameters/attack_lower_speed/scale";
    private static readonly StringName UpperAttackSpeed = "parameters/attack_upper_speed/scale";

    private readonly AnimationTree _tree;
    // Parked on a valid clip until the first attack; the layer weight is zero until then.
    private readonly AnimationNodeAnimation _attackLower = new() { Animation = RigAnimations.Idle };
    private readonly AnimationNodeAnimation _attackUpper = new() { Animation = RigAnimations.Idle };
    private string _legState = IdleState;
    private float _stillness = 1f;
    private float _attackTime;
    private float _attackLength;
    private bool _fullBodyAttack;
    private readonly AnimationNodeAnimation _dodge = new() { Animation = RigAnimations.Idle };
    private float _dodgeTime;
    private float _dodgeLength;
    private float _chestBias;
    private bool _dying;
    private float _deathWeight;
    private readonly Dictionary<string, float> _chestYawByState = new();
    private readonly Dictionary<string, float> _groundSpeedByState = new();

    public CharacterAnimator(Node3D model)
    {
        var skeleton = model.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        Twist = new TorsoTwistModifier { Name = "TorsoTwist" };
        skeleton.AddChild(Twist);

        _tree = new AnimationTree { Name = "AnimationTree", TreeRoot = BuildTree() };
        RigAnimations.AddTo(_tree);
        model.AddChild(_tree);
        _tree.Set("parameters/death_speed/scale", RigAnimations.PlaybackSpeed);

        foreach (var (state, clip, travel) in LegClips)
        {
            if (state != IdleState)
            {
                var animation = _tree.GetAnimation(clip);
                _chestYawByState[state] = ClipMotion.MeanChestYaw(skeleton, clip, animation);
                _groundSpeedByState[state] = ClipMotion.GroundSpeed(skeleton, clip, animation, travel);
            }
        }
    }

    public TorsoTwistModifier Twist { get; }

    public bool IsAttacking => _attackTime < _attackLength;

    public bool IsDodging => _dodgeTime < _dodgeLength;

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

    // Drops whatever swing is playing, quickly, so something else (a dodge) can take over.
    public void CancelAttack() => _attackLength = Mathf.Min(_attackLength, _attackTime + CancelFade);

    // Plays a dodge clip squeezed or stretched to last exactly duration.
    public void PlayDodge(StringName clip, float duration)
    {
        _dodge.Animation = clip;
        _tree.Set(DodgeSeek, 0.0);
        _tree.Set(DodgeSpeed, _tree.GetAnimation(clip).Length / duration);
        _dodgeTime = 0f;
        _dodgeLength = duration;
    }

    public void Revive() => _dying = false;

    public void Update(float delta, LegDirection? legs, float speed, float twist)
    {
        string state = legs?.ToString() ?? IdleState;
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

        AttackWeight = attackWeight;
        _tree.Set(UpperAmount, attackWeight);
        _tree.Set(LowerAmount, attackWeight * (_fullBodyAttack ? 1f : _stillness));

        // Leg clips turn the chest on their own (Walking_Backwards by about 32 deg), so that bias comes off the twist
        // to keep the chest on the aim. Swings are authored relative to the body, so an attack takes the bias away.
        // The idle stance keeps its authored chest angle.
        float bias = legs == null ? 0f : _chestYawByState[state];
        _chestBias = Mathf.Lerp(_chestBias, bias, 1f - Mathf.Exp(-delta / LegCrossfade));
        float dodgeWeight = 0f;
        if (IsDodging)
        {
            _dodgeTime += delta;
            dodgeWeight = Mathf.Min(1f, _dodgeTime / DodgeFadeIn) * Mathf.Clamp((_dodgeLength - _dodgeTime) / DodgeFadeOut, 0f, 1f);
        }

        _tree.Set(DodgeAmount, dodgeWeight);
        _deathWeight = Mathf.MoveToward(_deathWeight, _dying ? 1f : 0f, delta / DeathBlend);
        _tree.Set(DeathAmount, _deathWeight);
        // A full-body swing (Spin) turns the body itself, so the torso twist toward the aim gives way to it.
        float swingOwnsTorso = _fullBodyAttack ? attackWeight : 0f;
        Twist.Twist = (twist - _chestBias * (1f - attackWeight)) * (1f - _deathWeight) * (1f - dodgeWeight) * (1f - swingOwnsTorso);
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

        AddAttackLayer(root, "lower", _attackLower, LowerBones, below: "legs_speed");
        AddAttackLayer(root, "upper", _attackUpper, UpperBones, below: "lower_mix");

        root.AddNode("death", new AnimationNodeAnimation { Animation = RigAnimations.PlayerDeath });
        root.AddNode("death_seek", new AnimationNodeTimeSeek());
        root.ConnectNode("death_seek", 0, "death");
        root.AddNode("death_speed", new AnimationNodeTimeScale());
        root.ConnectNode("death_speed", 0, "death_seek");
        // Everything but the root bone: the dodge clips carry their own root travel (0.25-0.65), and the code already
        // moves the character the full dodge distance.
        var dodgeMix = new AnimationNodeBlend2 { FilterEnabled = true };
        foreach (var bone in LowerBones.Where(b => b != "root").Concat(UpperBones))
        {
            dodgeMix.SetFilterPath($"{RigAnimations.SkeletonPath}:{bone}", true);
        }

        root.AddNode("dodge", _dodge);
        root.AddNode("dodge_seek", new AnimationNodeTimeSeek());
        root.ConnectNode("dodge_seek", 0, "dodge");
        root.AddNode("dodge_speed", new AnimationNodeTimeScale());
        root.ConnectNode("dodge_speed", 0, "dodge_seek");
        root.AddNode("dodge_mix", dodgeMix);
        root.ConnectNode("dodge_mix", 0, "upper_mix");
        root.ConnectNode("dodge_mix", 1, "dodge_speed");

        root.AddNode("death_mix", new AnimationNodeBlend2());
        root.ConnectNode("death_mix", 0, "dodge_mix");
        root.ConnectNode("death_mix", 1, "death_speed");
        root.ConnectNode("output", 0, "death_mix");
        return root;
    }

    private static void AddAttackLayer(AnimationNodeBlendTree root, string layer, AnimationNodeAnimation clip, string[] bones, string below)
    {
        var mix = new AnimationNodeBlend2 { FilterEnabled = true };
        foreach (var bone in bones)
        {
            mix.SetFilterPath($"{RigAnimations.SkeletonPath}:{bone}", true);
        }

        root.AddNode($"attack_{layer}", clip);
        root.AddNode($"attack_{layer}_seek", new AnimationNodeTimeSeek());
        root.ConnectNode($"attack_{layer}_seek", 0, $"attack_{layer}");
        root.AddNode($"attack_{layer}_speed", new AnimationNodeTimeScale());
        root.ConnectNode($"attack_{layer}_speed", 0, $"attack_{layer}_seek");
        root.AddNode($"{layer}_mix", mix);
        root.ConnectNode($"{layer}_mix", 0, below);
        root.ConnectNode($"{layer}_mix", 1, $"attack_{layer}_speed");
    }
}
