using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Burning ground and ice where fire and ice land (Core's Surfaces). The caster's machine says where its spell left
// one, as it decides what the spell hit; the host keeps the patches (SurfaceField), tells every machine where they
// are, and once a turn acts on whatever stands on them, skeletons and players alike, the caster and its friends too:
// burning ground hits them as the caster's spell would, ice chills them. A patch laid over one of the other kind
// destroys it, fire melting ice and ice putting fire out. The level has fires of its own, at its "campfire" markers:
// burning ground that never goes out, laid alike on every machine, which nothing puts out and which burns even on the
// town's safe ground, at the fire's own strength (no one's WIS). Nothing else hurts a player on the town's safe
// ground. On ice everything slides, everywhere: a skeleton as the host moves it, a player on its own machine, so
// every machine knows where the ice is (IcyUnder). A machine that joins later does not hear of the patches of the
// moment: they last a few seconds. Present on every machine at the same path, for its RPCs.
// Design: Docs/Design/magic.md, "Burning ground and ice".
public partial class GroundSurfaces : Node3D
{
    public const string NodeName = "GroundSurfaces";

    private const string HearthMarker = "campfire";

    // A spell is never said to land further from its caster than a ball flies and bursts, and a little.
    private const float FurthestFromCaster = 16f;

    private readonly SurfaceField _field = new();

    // Every patch as this machine has heard of it, until its end by this machine's clock.
    private readonly Dictionary<int, SurfacePatch> _known = new();
    private readonly Dictionary<int, SurfacePatchView> _shown = new();
    private float _clock;
    private float _untilTurn = StatusRules.Turn;

    // On every machine, as a patch is laid (not renewed), and on the host as a turn on one acts on a skeleton or a
    // player.
    public static event Action<SurfaceKind>? Laid;

    public static event Action<SurfaceKind, Node3D>? Acted;

    public int Shown => _shown.Count;

    public static GroundSurfaces In(SceneTree tree) => tree.CurrentScene.GetNode<GroundSurfaces>(NodeName);

    // From the caster's machine: its skill left its ground at that spot, on the floor there.
    public void Lay(SkillDefinition skill, Vector3 at) => RpcId(1, MethodName.RequestLay, skill.Id, at);

    // The level's own fires, from its layouts.
    public void Build(ArenaMap map)
    {
        int hearths = 0;
        foreach (var marker in map.Layouts.SelectMany(l => l.MarkersNamed(HearthMarker)))
        {
            var at = new Vector3(marker.X, marker.Y, marker.Z);
            _field.Keep(SurfaceKind.Burning, Yaw.ToGround(at), Floors.LevelOf(at.Y), Surfaces.HearthRadius);
            var view = new SurfacePatchView(SurfaceKind.Burning, Surfaces.HearthRadius, float.PositiveInfinity) { Name = $"Hearth{hearths++}", Position = at };
            view.AddToGroup(ArenaMap.OnAFloor);
            AddChild(view);
        }
    }

    // Whether a body there stands on ice.
    public bool IcyUnder(Vector3 position)
    {
        var ground = Yaw.ToGround(position);
        int level = Floors.LevelOf(position.Y);
        return _known.Values.Any(p => p.Kind == SurfaceKind.Icy && Surfaces.Touches(p, ground, level, bodyRadius: 0f));
    }

    // Host only: the effect laid at the spot, as the caster's: what a spell of its leaves, and the self-tests.
    public void LayFor(PlayerCharacter caster, SurfaceEffect effect, Vector3 at)
    {
        var (patch, renewed, gone) = _field.Lay(caster.PeerId, effect, Yaw.ToGround(at), Floors.LevelOf(at.Y), _clock);
        foreach (var taken in gone)
        {
            Rpc(MethodName.Remove, taken.Id);
        }

        if (renewed)
        {
            Rpc(MethodName.Renew, patch.Id, effect.Lasts);
        }
        else
        {
            Rpc(MethodName.Place, patch.Id, (int)patch.Kind, at, patch.Radius, effect.Lasts);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _clock += (float)delta;
        foreach (var (id, _) in _known.Where(k => k.Value.Until <= _clock).ToList())
        {
            _known.Remove(id);
        }

        if (!Multiplayer.IsServer())
        {
            return;
        }

        _field.Expire(_clock);
        _untilTurn -= (float)delta;
        if (_untilTurn > 0f)
        {
            return;
        }

        _untilTurn += StatusRules.Turn;
        foreach (var enemy in EnemyCharacter.Standing(GetTree()).ToList())
        {
            var ground = Yaw.ToGround(enemy.GlobalPosition);
            int level = Floors.LevelOf(enemy.GlobalPosition.Y);
            if (FireUnder(ground, level, safe: false) is { } fire && Lit(fire, out var caster))
            {
                enemy.TakeSurfaceHit(caster, Surfaces.BurningHit);
                Acted?.Invoke(SurfaceKind.Burning, enemy);
            }

            if (!enemy.IsDead && _field.Under(SurfaceKind.Icy, ground, level, BodySize.Radius) != null)
            {
                enemy.BuildStatus(DamageType.Ice, Surfaces.IcyCold);
                Acted?.Invoke(SurfaceKind.Icy, enemy);
            }
        }

        // Players where each last reported standing, as for every hit on a player.
        var map = ArenaMap.In(GetTree());
        foreach (var player in PlayerCharacter.All(GetTree()).ToList())
        {
            if (player.IsDowned)
            {
                continue;
            }

            bool safe = map.IsSafe(player.NetPosition);
            var ground = Yaw.ToGround(player.NetPosition);
            int level = Floors.LevelOf(player.NetPosition.Y);
            if (FireUnder(ground, level, safe) is { } fire && Lit(fire, out var caster))
            {
                player.Vitals.TakeSurfaceHit(caster, Surfaces.BurningHit);
                Acted?.Invoke(SurfaceKind.Burning, player);
            }

            if (!safe && !player.IsDowned && _field.Under(SurfaceKind.Icy, ground, level, BodySize.Radius) != null)
            {
                player.Vitals.BuildStatus(DamageType.Ice, Surfaces.IcyCold);
                Acted?.Invoke(SurfaceKind.Icy, player);
            }
        }
    }

    // The fire a body there stands in; on safe ground only the level's own burns.
    private SurfacePatch? FireUnder(System.Numerics.Vector2 ground, int level, bool safe) =>
        _field.Patches.FirstOrDefault(p => p.Kind == SurfaceKind.Burning && (!safe || p.Caster == SurfaceField.Level)
            && Surfaces.Touches(p, ground, level, BodySize.Radius));

    // Who lit it: the level's own has no one, and one whose caster has left burns no more.
    private bool Lit(SurfacePatch fire, out PlayerCharacter? caster)
    {
        caster = fire.Caster == SurfaceField.Level ? null : PlayerCharacter.Find(GetTree(), fire.Caster);
        return fire.Caster == SurfaceField.Level || caster != null;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestLay(string skillId, Vector3 at)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        long peer = Multiplayer.Sender();
        if (PlayerCharacter.Find(GetTree(), peer) is not { } caster)
        {
            GD.PushWarning($"[GroundSurfaces] ground laid by peer {peer}, who is no longer in the game; ignored");
            return;
        }

        SkillDefinition skill;
        try
        {
            skill = caster.SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[GroundSurfaces] {e.Message} (from peer {peer})");
            return;
        }

        if (skill.Surface is not { } effect)
        {
            GD.PushError($"[GroundSurfaces] peer {peer} said {skillId} left ground, and it leaves none");
            return;
        }

        if (Yaw.Flat(at - caster.NetPosition).Length() > FurthestFromCaster)
        {
            GD.PushError($"[GroundSurfaces] peer {peer} said {skillId} left ground {Yaw.Flat(at - caster.NetPosition).Length():F1} away; no spell lands that far");
            return;
        }

        LayFor(caster, effect, at);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Place(int id, int kind, Vector3 at, float radius, float lasts)
    {
        _known[id] = new SurfacePatch(id, 0, (SurfaceKind)kind, Yaw.ToGround(at), Floors.LevelOf(at.Y), radius, _clock + lasts);
        var view = new SurfacePatchView((SurfaceKind)kind, radius, lasts) { Name = $"Patch{id}", Position = at };
        view.Gone += () => _shown.Remove(id);
        view.AddToGroup(ArenaMap.OnAFloor);
        _shown[id] = view;
        AddChild(view);
        Laid?.Invoke((SurfaceKind)kind);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Renew(int id, float lasts)
    {
        if (_known.TryGetValue(id, out var known))
        {
            _known[id] = known with { Until = _clock + lasts };
        }

        if (_shown.TryGetValue(id, out var view))
        {
            view.Renew(lasts);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Remove(int id)
    {
        _known.Remove(id);
        if (_shown.TryGetValue(id, out var view))
        {
            view.Renew(0f);
        }
    }
}
