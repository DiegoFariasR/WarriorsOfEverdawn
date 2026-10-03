using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// Attached with --floors-check, skeletons on. No polygon of the ways may go through the air. The player walks the
// map's ways, as a player would: up into each of the town's storeys and back down, then down into the crypt under the
// enemy fortress and back up. Each walk must find its way and reach its floor. A storey must be hidden while the
// player is in the room under it and nothing hidden upstairs; the ground must be hidden from the crypt and the sky
// dark there, and nothing hidden from the ground. The crypt's guards must keep to the crypt until the player comes
// down. All but one that fights hand to hand are cut down before the player goes down, and that one must come for the
// player on the way and is cut down as it does: it is the floors on trial here, not the fight. Once it falls the
// treasure must lie at its spot and go to the player who walks to it. The waves are cut down as they rise. Prints
// [floors-check] lines and quits (exit 1 on failure).
public partial class FloorsSelfTest : Node
{
    // The navigation mesh and the bodies reach their servers some steps after they are made.
    private const int SettleSteps = 10;

    // A walk ends this close across the ground, on the floor it was for.
    private const float Arrived = 0.5f;
    private const float WalkLimit = 60f;

    // A guard has come for the player once it is this far from its spot. A warrior is in reach of a player at the
    // foot of the stairs a step and a half from its own.
    private const float LeftItsSpot = 1f;
    private const float TreasureWithin = 3f;

    // The treasure's orbs lie this far from its spot at most (Loot.LeaveTreasure), and a little more.
    private const float TreasureSpread = 1f;

    // Blows dealt to a skeleton before giving up on it: far more than any takes.
    private const int MostBlows = 200;

    private readonly Node3D _players;
    private readonly Queue<Stage> _stages = new();
    private readonly Steered _steered = new();
    private ArenaMap _map = null!;
    private PlayerCharacter _player = null!;
    private Stage? _stage;
    private float _stageFor;
    private int _steps;
    private bool _started;
    private bool _passed = true;
    private bool _hiddenOnGroundFloor;
    private string? _guardCame;
    private List<int> _treasure = new();
    private int _goldBefore;
    private int _orbsBefore;

    public FloorsSelfTest(Node3D players)
    {
        _players = players;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public FloorsSelfTest()
        : this(null!)
    {
    }

    public override void _PhysicsProcess(double delta)
    {
        var player = _players.GetChildren().OfType<PlayerCharacter>().FirstOrDefault(p => p.IsMultiplayerAuthority());
        if (player == null || ++_steps < SettleSteps)
        {
            return;
        }

        if (!_started)
        {
            _started = true;
            Begin(player);
        }

        CutDownWaves();
        if (_player.IsDowned)
        {
            Report(_stage?.Name ?? "walk", $"the player went down at {_player.GlobalPosition}", ok: false);
            End();
            return;
        }

        if (_stage == null)
        {
            if (_stages.Count == 0)
            {
                End();
                return;
            }

            _stage = _stages.Dequeue();
            _stageFor = 0f;
            _stage.Start?.Invoke();
        }

        _stageFor += (float)delta;
        _steered.Move = _stage.WalkTo is { } to ? _map.StepToward(_player, to()) : Vector3.Zero;
        if (_map.SubjectLevel == 0 && _map.HiddenPieces > 0)
        {
            _hiddenOnGroundFloor = true;
        }

        if (_guardCame == null && Floors.LevelOf(_player.GlobalPosition.Y) == -1
            && Guards().FirstOrDefault(g => !g.IsDead && OffItsSpot(g) >= LeftItsSpot) is { } coming)
        {
            _guardCame = coming.Name;
            CutDown(coming);
        }

        if (_stage.Done())
        {
            _steered.Move = Vector3.Zero;
            _stage.Finish?.Invoke();
            _stage = null;
        }
        else if (_stageFor > _stage.Limit)
        {
            Report(_stage.Name, $"not done in {_stage.Limit:F0} s: the player at {Rounded(_player.GlobalPosition)} on level {Floors.LevelOf(_player.GlobalPosition.Y)}{WaysFrom(_stage)}", ok: false);
            End();
        }
    }

    private void Begin(PlayerCharacter player)
    {
        _player = player;
        _player.Controls = _steered;
        _map = ArenaMap.In(GetTree());
        var home = _map.PlayerSpawnFor(0);
        GD.Print($"[floors-check] storeys={_map.Upstairs.Count} guard_spots={_map.CryptGuards.Count}");
        if (_map.Upstairs.Count == 0 || _map.CryptGuards.Count == 0)
        {
            Report("map", "the town has no storey or the crypt no guard spot", ok: false);
        }

        Check("ways-on-the-floors", () =>
        {
            var faulty = WaysDump.PolygonsOf(_map.Ways).Where(p => WaysDump.ThroughTheAir(p.Corners)).Select(p => p.Index).ToList();
            return ($"polygons_through_the_air={faulty.Count}{(faulty.Count > 0 ? $" ({string.Join(",", faulty)}; ./dev.sh ways-dump shows them)" : "")}", faulty.Count == 0);
        });

        foreach (var (upstairs, i) in _map.Upstairs.Select((u, i) => (u, i)))
        {
            Walk($"up-{i}", upstairs, 1, () =>
            {
                Report($"up-{i}-hiding", $"storey_hidden_from_the_room={_hiddenOnGroundFloor} hidden_upstairs={_map.HiddenPieces}",
                    _hiddenOnGroundFloor && _map.HiddenPieces == 0);
            });
            Walk($"down-{i}", home, 0, () => Report($"down-{i}-hiding", $"hidden_in_the_town={_map.HiddenPieces}", _map.HiddenPieces == 0));
        }

        Check("guards-keep-to-the-crypt", () =>
        {
            var guards = Guards();
            bool ok = guards.Count == _map.CryptGuards.Count && guards.All(g => !g.IsDead && Floors.LevelOf(g.GlobalPosition.Y) == -1 && OffItsSpot(g) < LeftItsSpot);
            return ($"guards={guards.Count} {string.Join(" ", guards.Select(g => $"{g.Name}:level={Floors.LevelOf(g.GlobalPosition.Y)},off_spot={OffItsSpot(g):F1}"))}", ok);
        });
        Check("guards-but-one-fall", () =>
        {
            var guards = Guards();
            var kept = guards.Where(g => g.Definition.Attack.Projectile == null).MinBy(g => g.GlobalPosition.DistanceTo(_map.Crypt));
            foreach (var guard in guards.Where(g => g != kept))
            {
                CutDown(guard);
            }

            return ($"kept={kept?.Name} standing={guards.Count(g => !g.IsDead)}", kept != null && guards.Count(g => !g.IsDead) == 1);
        });
        Walk("crypt-down", _map.Crypt, -1, () =>
        {
            var guards = Guards();
            Report("crypt-hiding", $"hidden_from_the_crypt={_map.HiddenPieces} guards_shown={guards.Count(g => g.Visible)}/{guards.Count} dark={_map.Underground}",
                _map.HiddenPieces > 0 && guards.All(g => g.Visible) && _map.Underground);
        });
        Check("guard-came", () => ($"came={_guardCame ?? "none"} standing={Guards().Count(g => !g.IsDead)}",
            _guardCame != null && Guards().All(g => g.IsDead)));
        _stages.Enqueue(new Stage("treasure-left", () => TreasureLying().Any(), TreasureWithin,
            Finish: () =>
            {
                var lying = TreasureLying().ToList();
                _treasure = lying.Select(l => l.Id).ToList();
                _goldBefore = _player.Vitals.Gold;
                _orbsBefore = _player.Vitals.Orbs;
                int gold = lying.Where(l => l.Kind == LootKind.Gold).Sum(l => l.Amount);
                int orbs = lying.Where(l => l.Kind == LootKind.Orb).Sum(l => l.Amount);
                Report("treasure-left", $"gold={gold} orbs={orbs}", gold >= Crypt.Treasure.Gold && orbs >= Crypt.Treasure.Orbs);
            }));
        Walk("treasure", _map.CryptTreasure, -1);
        _stages.Enqueue(new Stage("treasure-taken", () => !Loot.In(GetTree()).OnGround.Any(l => _treasure.Contains(l.Id)), TreasureWithin,
            Finish: () => Report("treasure-taken", $"gold+{_player.Vitals.Gold - _goldBefore} orbs+{_player.Vitals.Orbs - _orbsBefore}",
                _player.Vitals.Gold - _goldBefore >= Crypt.Treasure.Gold && _player.Vitals.Orbs - _orbsBefore >= Crypt.Treasure.Orbs)));
        Walk("crypt-up", _map.FortressGate, 0, () =>
            Report("crypt-up-hiding", $"hidden_on_the_ground={_map.HiddenPieces} dark={_map.Underground}", _map.HiddenPieces == 0 && !_map.Underground));
    }

    // A walk to `to` round the walls, by the way the skeletons and the bots take; it must find one, and end on `level`.
    private void Walk(string name, Vector3 to, int level, Action? arrived = null)
    {
        _stages.Enqueue(new Stage(name,
            () => Flat(_player.GlobalPosition - to).Length() < Arrived && Floors.LevelOf(_player.GlobalPosition.Y) == level,
            WalkLimit,
            Start: () =>
            {
                _hiddenOnGroundFloor = false;
                _map.Forget(_player);
                bool way = _map.HasWay(_player.GlobalPosition, to);
                Report($"{name}-way", $"from={Rounded(_player.GlobalPosition)} to={Rounded(to)} way={way}", way);
            },
            Finish: () =>
            {
                Report(name, $"level={Floors.LevelOf(_player.GlobalPosition.Y)} y={_player.GlobalPosition.Y:F2} in {_stageFor:F1} s", ok: true);
                arrived?.Invoke();
            },
            WalkTo: () => to));
    }

    private void Check(string name, Func<(string What, bool Ok)> check) =>
        _stages.Enqueue(new Stage(name, () => true, 0f, Finish: () =>
        {
            var (what, ok) = check();
            Report(name, what, ok);
        }));

    private List<EnemyCharacter> Guards() =>
        GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => e.IsGuard).ToList();

    private float OffItsSpot(EnemyCharacter guard)
    {
        int spot = int.Parse(guard.Name.ToString()[EnemyDirector.GuardPrefix.Length..], System.Globalization.CultureInfo.InvariantCulture);
        return Flat(guard.GlobalPosition - _map.CryptGuards[spot]).Length();
    }

    private IEnumerable<GroundLoot> TreasureLying() =>
        Loot.In(GetTree()).OnGround.Where(l => Floors.SameLevel(l.Position.Y, _map.CryptTreasure.Y) && Flat(l.Position - _map.CryptTreasure).Length() <= TreasureSpread);

    private void CutDownWaves()
    {
        foreach (var enemy in GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => !e.IsDead && !e.IsGuard))
        {
            CutDown(enemy);
        }
    }

    // Blows of the player's own weapon, through the path a hit takes on the host.
    private void CutDown(EnemyCharacter enemy)
    {
        string skill = (_player.Weapon ?? throw new InvalidOperationException("The player has no weapon in hand to cut a skeleton down with")).Primary.Id;
        for (int i = 0; i < MostBlows && !enemy.IsDead; i++)
        {
            enemy.Rpc(EnemyCharacter.MethodName.RequestDamage, skill);
        }
    }

    private void End()
    {
        GD.Print($"[floors-check] {(_passed ? "passed" : "FAILED")}");
        GetTree().Quit(_passed ? 0 : 1);
        SetPhysicsProcess(false);
    }

    private void Report(string name, string what, bool ok)
    {
        _passed &= ok;
        GD.Print($"[floors-check] case={name} {what} {(ok ? "ok" : "FAIL")}");
    }

    // Where a walk that stopped short stands on the ways, and the way they give it from there.
    private string WaysFrom(Stage stage)
    {
        if (stage.WalkTo is not { } to)
        {
            return "";
        }

        var map = _player.GetWorld3D().NavigationMap;
        var at = _player.GlobalPosition;
        var path = NavigationServer3D.MapGetPath(map, at, to(), optimize: true);
        var touching = Enumerable.Range(0, _player.GetSlideCollisionCount()).Select(_player.GetSlideCollision)
            .Select(c => $"{(c.GetCollider() as Node)?.Name}:{Rounded(c.GetNormal())}");
        var under = NavigationServer3D.MapGetClosestPointToSegment(map, at + Vector3.Up, at + Vector3.Down);
        return $" on_the_ways_under={Rounded(under)} heading={Rounded(_steered.Move)}"
            + $" way=[{string.Join(" ", path.Select(Rounded))}]"
            + $" on_floor={_player.IsOnFloor()} on_wall={_player.IsOnWall()} touching=[{string.Join(" ", touching)}]";
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    private static string Rounded(Vector3 v) => $"({v.X:F1},{v.Y:F1},{v.Z:F1})";

    private sealed record Stage(string Name, Func<bool> Done, float Limit, Action? Start = null, Action? Finish = null, Func<Vector3>? WalkTo = null);

    // The walk's legs, and nothing else asked for.
    private sealed class Steered : IPlayerControls
    {
        public Vector3 Move { get; set; }

        public float? AimYaw => null;

        public int? SkillHeld => null;

        public bool DashPressed => false;

        public bool WeaponNextPressed => false;

        public bool SwapSetsPressed => false;

        public bool GuardHeld => false;

        public bool DropPressed => false;

        public bool PickUpPressed => false;

        public bool InteractPressed => false;

        public bool Suspended { get; set; }

        public void Update(PlayerCharacter player, double delta)
        {
        }
    }
}
