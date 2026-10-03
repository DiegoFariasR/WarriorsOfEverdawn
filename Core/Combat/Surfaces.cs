using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

public enum SurfaceKind
{
    Burning,
    Icy,
}

// What a spell leaves on the ground where it lands: a round patch of that kind, Radius wide, for Lasts seconds.
public sealed record SurfaceEffect(SurfaceKind Kind, float Radius, float Lasts);

// One patch on the ground: on a floor (Level, as Floors counts them), laid by a player (Caster, its peer), gone at Until.
public sealed record SurfacePatch(int Id, long Caster, SurfaceKind Kind, Vector2 Centre, int Level, float Radius, float Until);

// Burning ground and ice, first pass (Docs/Design/magic.md, "Burning ground and ice"). What stands on a patch is
// acted on once a turn (StatusRules.Turn): burning ground deals its hit, ice builds cold. Ice also takes a body's
// footing: it gains and loses speed slowly, so it slides on past where it meant to stop and is slow to turn about.
// Fire and ice undo each other: a patch laid over one of the other kind destroys it.
public static class Surfaces
{
    public const float BurningLasts = 4f;
    public const float IcyLasts = 6f;

    // A staff's ball leaves a patch this wide where it bursts, a little narrower than what the burst catches.
    public const float BurstPatchRadius = 2f;

    // A turn in the flames, by the caster's WIS as any spell of its: a fifth of a bolt, burning as fire does.
    public static readonly SkillDefinition BurningHit = new("burning-ground", Damage: 6, Range: 0f, HalfArc: 0f, HitTime: 0f)
    {
        Name = "Burning ground",
        Type = DamageType.Fire,
        Buildup = 30,
        Element = Element.Fire,
    };

    // What a turn on ice builds: more than a turn takes off (StatusRules.ColdDecay), so what stays on it is chilled
    // and in the end frozen.
    public const int IcyCold = 30;

    // On ice a body's speed changes by no more than this a second; on dry ground it changes at once.
    public const float IceGrip = 4f;

    // A held spell lays a patch under its area every cycle: one laid within this share of its radius of a patch of
    // the same kind, from the same caster, renews that patch instead of laying another.
    public const float RenewWithin = 0.5f;

    // A caster's patches at once: the next takes the place of the one nearest its end.
    public const int MostPerCaster = 8;

    // A hearth's fire (the town's camp fire): burning ground as wide as its logs, which never goes out.
    public const float HearthRadius = 0.6f;

    // Fire leaves burning ground and ice leaves ice; the other elements leave nothing yet.
    public static SurfaceEffect? Of(Element element, float radius) => element switch
    {
        Element.Fire => new SurfaceEffect(SurfaceKind.Burning, radius, BurningLasts),
        Element.Ice => new SurfaceEffect(SurfaceKind.Icy, radius, IcyLasts),
        _ => null,
    };

    // A body on the patch's floor touching it, as a body touching an area spell is in it. With no radius, whether
    // the body's middle, its footing, is on it.
    public static bool Touches(SurfacePatch patch, Vector2 body, int level, float bodyRadius) =>
        patch.Level == level && Vector2.Distance(patch.Centre, body) <= patch.Radius + bodyRadius;

    // How a body on ice is going after this step: from how it was going, toward how it wants to, by no more than
    // the grip allows.
    public static Vector2 Slide(Vector2 going, Vector2 wanted, float delta)
    {
        var change = wanted - going;
        float most = IceGrip * delta;
        return change.Length() <= most ? wanted : going + change / change.Length() * most;
    }
}

// The patches on the ground, as the host keeps them: laid, renewed, and gone when their time is up.
public sealed class SurfaceField
{
    // Who lays a hearth's fire: the level itself, no player (no peer has this id).
    public const long Level = 0;

    private readonly List<SurfacePatch> _patches = new();
    private int _nextId;

    public IReadOnlyList<SurfacePatch> Patches => _patches;

    // A patch of the level's own that lasts for good: no spell takes it off the ground, and it counts against no
    // caster's patches.
    public SurfacePatch Keep(SurfaceKind kind, Vector2 centre, int level, float radius)
    {
        var patch = new SurfacePatch(_nextId++, Level, kind, centre, level, radius, float.PositiveInfinity);
        _patches.Add(patch);
        return patch;
    }

    // Lays the effect at the spot, or renews the caster's patch of that kind there: the patch laid or renewed, and
    // those it took off the ground: every patch of the other kind it touches, whoever laid it (fire melts ice, ice
    // puts fire out), and the caster's own it took the place of, if any.
    public (SurfacePatch Patch, bool Renewed, IReadOnlyList<SurfacePatch> Gone) Lay(long caster, SurfaceEffect effect, Vector2 centre, int level, float now)
    {
        var gone = _patches.Where(p => p.Kind != effect.Kind && p.Level == level && p.Caster != Level
            && Vector2.Distance(p.Centre, centre) < p.Radius + effect.Radius).ToList();
        _patches.RemoveAll(gone.Contains);

        int near = _patches.FindIndex(p => p.Caster == caster && p.Kind == effect.Kind && p.Level == level
            && Vector2.Distance(p.Centre, centre) <= effect.Radius * Surfaces.RenewWithin);
        if (near >= 0)
        {
            _patches[near] = _patches[near] with { Until = now + effect.Lasts };
            return (_patches[near], true, gone);
        }

        var own = _patches.Where(p => p.Caster == caster).ToList();
        if (own.Count >= Surfaces.MostPerCaster)
        {
            var replaced = own.MinBy(p => p.Until)!;
            _patches.Remove(replaced);
            gone.Add(replaced);
        }

        var patch = new SurfacePatch(_nextId++, caster, effect.Kind, centre, level, effect.Radius, now + effect.Lasts);
        _patches.Add(patch);
        return (patch, false, gone);
    }

    // The patches whose time is up, taken off the ground.
    public IReadOnlyList<SurfacePatch> Expire(float now)
    {
        var gone = _patches.Where(p => p.Until <= now).ToList();
        _patches.RemoveAll(p => p.Until <= now);
        return gone;
    }

    // The first patch of the kind the body touches, if any.
    public SurfacePatch? Under(SurfaceKind kind, Vector2 body, int level, float bodyRadius) =>
        _patches.FirstOrDefault(p => p.Kind == kind && Surfaces.Touches(p, body, level, bodyRadius));
}
