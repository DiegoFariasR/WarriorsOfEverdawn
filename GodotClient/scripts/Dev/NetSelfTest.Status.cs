using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [status-check]: what this machine sees of statuses: which ones skeletons and players have had, the most named
// over HP bars at once, skeletons frozen with their block of ice up, and how far a frozen or stunned skeleton
// moved while it was held; and of the local player, the frames it was held itself, how far it moved in them and the
// swings it started in them. On the host, [status-host]: the bites of burns and wounds, by type, and with
// --status-drill what the host froze and stunned on cue: skeletons, or with none about, players.
public partial class NetSelfTest
{
    private const float DrillEvery = 2.5f;

    private readonly Dictionary<DamageType, int> _enemyBites = new();
    private readonly Dictionary<ulong, Vector3> _heldAt = new();
    private Statuses _enemyStatusesSeen;
    private Statuses _playerStatusesSeen;
    private int _statusesNamedMost;
    private int _iceBlockFrames;
    private int _frozenWithoutIceFrames;
    private int _heldFrames;
    private float _heldMovedMost;
    private int _enemyBiteDamage;
    private int _playerBites;
    private int _localHeldFrames;
    private float _localHeldMovedMost;
    private Vector3? _localHeldAt;
    private int _swingsWhileHeld;
    private float _untilDrill = DrillEvery;
    private int _drills;

    public bool StatusDrill { get; init; }

    private void TrackStatus()
    {
        EnemyCharacter.StatusBit += OnEnemyBit;
        PlayerVitals.StatusBit += OnPlayerBit;
    }

    private void UntrackStatus()
    {
        EnemyCharacter.StatusBit -= OnEnemyBit;
        PlayerVitals.StatusBit -= OnPlayerBit;
    }

    private void OnEnemyBit(EnemyCharacter enemy, DamageType type, int damage)
    {
        _enemyBites[type] = _enemyBites.GetValueOrDefault(type) + 1;
        _enemyBiteDamage += damage;
    }

    private void OnPlayerBit(PlayerCharacter player, DamageType type, int damage) => _playerBites++;

    private void TrackStatusOf(PlayerCharacter player) =>
        player.AttackStarted += _ =>
        {
            if (player.IsMultiplayerAuthority() && player.Vitals.IsLost)
            {
                _swingsWhileHeld++;
            }
        };

    // Host only: the next skeleton that is up and not held is frozen, and the one after it stunned. In a PvP
    // session with no skeletons about, the players in turn: each frozen, then each stunned.
    private void DrillStatuses(float delta)
    {
        _untilDrill -= delta;
        if (_untilDrill > 0f)
        {
            return;
        }

        var skeletons = EnemyCharacter.Standing(GetTree()).ToList();
        if (skeletons.Count > 0)
        {
            var free = skeletons.FirstOrDefault(e => !e.Statuses.IsLost());
            if (free == null)
            {
                return;
            }

            free.BuildStatus(_drills % 2 == 0 ? DamageType.Ice : DamageType.Lightning, StatusRules.BarMost);
        }
        else if (SessionRules.Pvp)
        {
            var players = PlayerCharacter.All(GetTree()).OrderBy(p => p.PeerId).ToList();
            if (players.Count == 0)
            {
                return;
            }

            var player = players[_drills % players.Count];
            if (player.IsDowned || player.Vitals.IsLost)
            {
                return;
            }

            player.Vitals.Status.Build(_drills / players.Count % 2 == 0 ? DamageType.Ice : DamageType.Lightning, StatusRules.BarMost, damage: 0, Resistances.None);
        }
        else
        {
            return;
        }

        _untilDrill = DrillEvery;
        _drills++;
    }

    private void MeasureStatus(float delta)
    {
        if (StatusDrill && Multiplayer.IsServer())
        {
            DrillStatuses(delta);
        }

        foreach (var enemy in EnemyCharacter.Standing(GetTree()))
        {
            _enemyStatusesSeen |= enemy.Statuses;
            ulong id = enemy.GetInstanceId();
            if (!enemy.Statuses.IsLost())
            {
                _heldAt.Remove(id);
                continue;
            }

            _heldFrames++;
            if (enemy.Statuses.HasFlag(Statuses.Frozen))
            {
                _iceBlockFrames += enemy.StatusShow.Frozen ? 1 : 0;
                _frozenWithoutIceFrames += enemy.StatusShow.Frozen ? 0 : 1;
            }

            // From where it stood a frame after it was caught: the frame it is caught in, it may still be easing
            // to where the host has it.
            if (_heldAt.TryGetValue(id, out var at))
            {
                _heldMovedMost = Mathf.Max(_heldMovedMost, at.DistanceTo(enemy.NetPosition));
            }
            else
            {
                _heldAt[id] = enemy.NetPosition;
            }
        }

        foreach (var player in PlayerCharacter.All(GetTree()))
        {
            _playerStatusesSeen |= player.Vitals.Statuses;
        }

        // The local player obeys the host's word that it is held: from the frame after the word reached it.
        if (LocalPlayer() is { Vitals.IsLost: true, IsDowned: false } local)
        {
            _localHeldFrames++;
            if (_localHeldAt is { } at)
            {
                _localHeldMovedMost = Mathf.Max(_localHeldMovedMost, at.DistanceTo(local.GlobalPosition));
            }
            else
            {
                _localHeldAt = local.GlobalPosition;
            }
        }
        else
        {
            _localHeldAt = null;
        }

        _statusesNamedMost = Mathf.Max(_statusesNamedMost, _hud.Bars.StatusesNamed);
    }

    private void PrintStatusCheck(long me)
    {
        GD.Print($"[status-check] me={me} enemy_statuses={Named(_enemyStatusesSeen)} player_statuses={Named(_playerStatusesSeen)} "
            + $"named_most={_statusesNamedMost} held_frames={_heldFrames} ice_block_frames={_iceBlockFrames} frozen_without_ice_frames={_frozenWithoutIceFrames} "
            + $"held_moved_most={_heldMovedMost:F3} local_held_frames={_localHeldFrames} local_held_moved_most={_localHeldMovedMost:F3} swings_while_held={_swingsWhileHeld}");
        if (Multiplayer.IsServer())
        {
            string bites = _enemyBites.Count == 0 ? "none" : string.Join(",", _enemyBites.OrderBy(b => b.Key).Select(b => $"{DamageTypes.NameOf(b.Key)}:{b.Value}"));
            GD.Print($"[status-host] enemy_bites={bites} "
                + $"enemy_bite_damage={_enemyBiteDamage} player_bites={_playerBites} drills={_drills}");
        }
    }

    // "burning,chilled", or "none": a field of the line is never empty.
    private static string Named(Statuses statuses) =>
        statuses == Statuses.None ? "none" : string.Join(",", statuses.ToString().Split(", ").Select(name => name.ToLowerInvariant()));
}
