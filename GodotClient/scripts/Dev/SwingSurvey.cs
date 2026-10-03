using System;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// --swing-survey (./dev.sh swing-survey): for every weapon skill, follows the held weapon through the skill's clip,
// read straight from the clip's tracks, and prints where Core's numbers come from: when the striking point moves
// fastest, when the weapon reaches furthest, and how far it reaches (its farthest point, CharacterRig.WeaponReach). A
// swing lands when it moves fastest, a thrust at full extension; a spin's range is its steady reach.
// Reach is given standing (the whole clip) and moving (hips and legs left to the legs clip, as CharacterAnimator layers
// a swing over a moving body); swings that lunge through the hips lose a lot while moving. In play the two mix, so
// net-test's reach-check (the live weapon reach at every hit test) has the last word on ranges.
// For a weapon whose head sticks out to one side of its shaft (the scythe's blade), head_leads says whether that side
// faces the way the weapon travels at the hit (near 1) or trails behind it (near -1), and head_in_stance where it
// points in the idle stance, in the body's space (KayKit: +Z is the character's front, +X its left, +Y up).
public partial class SwingSurvey : Node
{
    private const int Samples = 400;

    // Further than this from the shaft, a part of the weapon counts as a head sticking out to one side.
    private const float OneSidedBeyond = 0.7f;

    private readonly Action<int> _quit;

    public SwingSurvey(Action<int> quit)
    {
        _quit = quit;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public SwingSurvey()
        : this(null!)
    {
    }

    public override void _Ready()
    {
        try
        {
            SurveyAll();
            SurveyReleases();
        }
        catch (Exception e)
        {
            GD.PushError($"[swing-survey] {e.Message}");
            _quit(1);
            return;
        }

        _quit(0);
    }

    private void SurveyAll()
    {
        foreach (var weapon in Weapons.All)
        {
            var body = CharacterBody.Build(PlayerCharacter.Look);
            body.Visible = false;
            AddChild(body);
            var look = CombatVisuals.LookFor(weapon);
            var hand = CharacterRig.AttachToHand(body, look);
            var skeleton = CharacterBody.SkeletonOf(body);
            var toBody = body.GlobalTransform.AffineInverse() * skeleton.GlobalTransform;
            var points = CharacterRig.WeaponPoints(hand);
            foreach (var skill in weapon.Skills)
            {
                Survey(weapon, skill, skeleton, toBody, points);
            }

            body.QueueFree();
        }
    }

    // Enemy attacks played as a clip and a follow-up (the archer's draw, then release): the shot leaves as the string
    // hand snaps back, its fastest moment in the follow-up. The hit time counts across both clips.
    private void SurveyReleases()
    {
        foreach (var enemy in Enemies.All)
        {
            if (CombatVisuals.FollowUpFor(enemy.Attack) is not { } followUp)
            {
                continue;
            }

            var body = CharacterBody.Build(CombatVisuals.LookFor(enemy).Figure);
            body.Visible = false;
            AddChild(body);
            var skeleton = CharacterBody.SkeletonOf(body);
            var first = RigAnimations.Load(CombatVisuals.ClipFor(enemy.Attack));
            var release = RigAnimations.Load(followUp);
            double step = release.Length / Samples;
            var hand = Enumerable.Range(0, Samples + 1)
                .Select(i => ClipMotion.BonePose(skeleton, release, CharacterRig.HandBone, i * step).Origin)
                .ToArray();
            int fastest = Enumerable.Range(1, Samples).MaxBy(i => hand[i].DistanceTo(hand[i - 1]));
            GD.Print($"[swing-survey] enemy={enemy.Id} skill={enemy.Attack.Id} clips={CombatVisuals.ClipFor(enemy.Attack)}+{followUp} "
                + $"first_length={first.Length:F3} release_at={first.Length + fastest * step:F3} core_hit_time={enemy.Attack.HitTime:F3}");
            body.QueueFree();
        }
    }

    // For a weapon with a head out to one side: which way that side faces against the weapon's travel at sample `at`,
    // and where it points in the idle stance. Empty for weapons that are the same all round their shaft.
    private static string HeadOf(Vector3[] points, Transform3D[] hands, Vector3 tip, int at, Transform3D toBody, Skeleton3D skeleton)
    {
        var head = points.MaxBy(p => new Vector2(p.X, p.Z).Length());
        var side = new Vector3(head.X, 0f, head.Z);
        if (side.Length() < OneSidedBeyond)
        {
            return "";
        }

        side = side.Normalized();
        var travel = (hands[at] * tip - hands[at - 1] * tip).Normalized();
        var stance = toBody * ClipMotion.BonePose(skeleton, RigAnimations.Load(RigAnimations.Idle), CharacterRig.HandBone, 0.0);
        var inStance = stance.Basis * side;
        return $" head_leads={(hands[at].Basis * side).Dot(travel):F2} head_in_stance=({inStance.X:F2},{inStance.Y:F2},{inStance.Z:F2})";
    }

    private static void Survey(WeaponDefinition weapon, SkillDefinition skill, Skeleton3D skeleton, Transform3D toBody, Vector3[] points)
    {
        string clip = CombatVisuals.ClipFor(skill);
        var animation = RigAnimations.Load(clip);
        double step = animation.Length / Samples;
        var tip = CharacterRig.WeaponTip(points);
        var hands = Enumerable.Range(0, Samples + 1)
            .Select(i => toBody * ClipMotion.BonePose(skeleton, animation, CharacterRig.HandBone, i * step))
            .ToArray();
        var reaches = hands.Select(hand => CharacterRig.WeaponReach(points, hand, Vector3.Zero)).ToArray();
        var lowerBody = CharacterAnimator.LowerBones.ToHashSet();
        var movingReaches = Enumerable.Range(0, Samples + 1)
            .Select(i => CharacterRig.WeaponReach(points, toBody * ClipMotion.BonePose(skeleton, animation, CharacterRig.HandBone, i * step, lowerBody), Vector3.Zero))
            .ToArray();
        float Reach(int i) => reaches[i];
        float Speed(int i) => i == 0 ? 0f : (float)((hands[i] * tip).DistanceTo(hands[i - 1] * tip) / step);

        string common = $"[swing-survey] weapon={weapon.Id} skill={skill.Id} clip={clip} clip_length={animation.Length:F3} "
            + $"tip_length={tip.Length():F2}";
        if (skill.Channeled)
        {
            GD.Print($"{common} reach_mean={reaches.Average():F2} reach_min={reaches.Min():F2} reach_max={reaches.Max():F2} "
                + $"core_range={skill.Range:F2}");
            return;
        }

        int fastest = Enumerable.Range(1, Samples).MaxBy(Speed);
        int farthest = Enumerable.Range(0, Samples + 1).MaxBy(Reach);
        GD.Print($"{common} peak_speed_at={fastest * step:F3} reach_at_peak={Reach(fastest):F2} moving_reach_at_peak={movingReaches[fastest]:F2} "
            + $"max_reach_at={farthest * step:F3} max_reach={Reach(farthest):F2} moving_reach_at_max={movingReaches[farthest]:F2} "
            + $"core_hit_time={skill.HitTime:F3} core_range={skill.Range:F2}{HeadOf(points, hands, tip, fastest, toBody, skeleton)}");
    }
}
