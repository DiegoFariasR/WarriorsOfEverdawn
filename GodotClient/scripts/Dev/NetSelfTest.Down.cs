using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [down-check]: a player who goes down stays put, can't attack and takes no hits, gets back up after the respawn delay
// in the allied town with full HP, and every machine sees both. The playtest takes one player down on cue (--down-at).
public partial class NetSelfTest
{
    // HP comes back through the synchronizer, which can land a little after the revive RPC. In a crowd a blow can
    // land in that time, so the damage taken since getting up is added back before comparing with full HP.
    private const float ReviveHpDelay = 0.5f;
    private const float DriftWorthPrinting = 0.005f;

    // Where a body lies is taken this many physics steps after it falls. Twice in some 100 downs a body moved about
    // a quarter of a metre in the step after falling, standing still, against a wall of the fortress: it stops
    // colliding with skeletons as it falls, and what they had pressed into the wall is pushed clear of it.
    private const int SettleSteps = 2;

    private readonly Dictionary<string, float> _wentDownAt = new();
    private readonly Dictionary<string, Vector3> _fellAt = new();
    private readonly Dictionary<string, Vector3> _liesAt = new();
    private readonly Dictionary<string, ulong> _settledBy = new();
    private readonly Dictionary<string, bool> _wasDown = new();
    private readonly List<float> _downSeconds = new();
    private readonly List<(PlayerCharacter Player, float At)> _hpChecks = new();
    private readonly Dictionary<string, int> _hitSinceRevive = new();
    private int _downs;
    private int _revives;
    private int _localDowns;
    private int _hpAfterReviveMin = int.MaxValue;
    private float _reviveDistanceMax;
    private float _downDriftMax;
    private float _downSettleMax;
    private int _attacksWhileDown;
    private int _hitsWhileDown;

    private void TrackDowns(PlayerCharacter player)
    {
        player.Vitals.Hit += amount =>
        {
            _hitsWhileDown += player.IsDowned ? 1 : 0;
            _hitSinceRevive[player.Name] = _hitSinceRevive.GetValueOrDefault(player.Name) + amount;
        };
        if (player.IsMultiplayerAuthority())
        {
            player.AttackStarted += _ => _attacksWhileDown += player.IsDowned ? 1 : 0;
        }
    }

    private void PrintDownCheck(long me)
    {
        float shortest = _downSeconds.Count > 0 ? _downSeconds.Min() : float.NaN;
        float longest = _downSeconds.Count > 0 ? _downSeconds.Max() : float.NaN;
        int hpAfterRevive = _hpAfterReviveMin == int.MaxValue ? -1 : _hpAfterReviveMin;
        GD.Print($"[down-check] me={me} downs={_downs} revives={_revives} local_downs={_localDowns} "
            + $"down_seconds_min={shortest:F2} down_seconds_max={longest:F2} expected_seconds={PlayerRules.RespawnDelay:F2} "
            + $"hp_after_revive_min={hpAfterRevive} hp_max={PlayerRules.MaxHp} revive_distance_max={_reviveDistanceMax:F2} "
            + $"down_drift_max={_downDriftMax:F3} down_settle_max={_downSettleMax:F3} settle_allowed={BodySize.Radius:F2} attacks_while_down={_attacksWhileDown} hits_while_down={_hitsWhileDown}");
    }

    private void MeasureDowns()
    {
        foreach (var player in _players.GetChildren().OfType<PlayerCharacter>())
        {
            string name = player.Name;
            bool down = player.IsDowned;
            bool wasDown = _wasDown.GetValueOrDefault(name);
            if (down && !wasDown)
            {
                _downs++;
                _localDowns += player.IsMultiplayerAuthority() ? 1 : 0;
                _wentDownAt[name] = _time;

                // Counted from going down, not from the revive being noticed here: a blow can land in the frame a
                // player gets up, and none lands while it is down.
                _hitSinceRevive[name] = 0;
                _fellAt[name] = player.GlobalPosition;
                _settledBy[name] = Engine.GetPhysicsFrames() + SettleSteps;
            }
            else if (!down && wasDown)
            {
                _revives++;
                _downSeconds.Add(_time - _wentDownAt[name]);
                _hpChecks.Add((player, _time + ReviveHpDelay));
                if (player.IsMultiplayerAuthority())
                {
                    var offset = player.GlobalPosition - ArenaMap.In(GetTree()).RevivePointFor(player.PeerId);
                    _reviveDistanceMax = Mathf.Max(_reviveDistanceMax, new Vector2(offset.X, offset.Z).Length());
                }
            }

            _wasDown[name] = down;
            // Position, not velocity: the velocity from the last physics step before the fall still reads for a frame or two
            // after it, since frames outrun physics steps in headless runs.
            if (down && player.IsMultiplayerAuthority())
            {
                if (Engine.GetPhysicsFrames() <= _settledBy[name])
                {
                    _liesAt[name] = player.GlobalPosition;
                    var settled = _liesAt[name] - _fellAt[name];
                    _downSettleMax = Mathf.Max(_downSettleMax, new Vector2(settled.X, settled.Z).Length());
                }

                var drift = player.GlobalPosition - _liesAt[name];
                float before = _downDriftMax;
                _downDriftMax = Mathf.Max(_downDriftMax, new Vector2(drift.X, drift.Z).Length());

                // Nothing should move a body once it lies: what does is printed as it happens.
                if (_downDriftMax > before + DriftWorthPrinting)
                {
                    var touching = Enumerable.Range(0, player.GetSlideCollisionCount())
                        .Select(i => player.GetSlideCollision(i))
                        .Select(c => $"{(c.GetCollider() as Node)?.Name}@{c.GetNormal()}");
                    GD.Print($"[down-drift] me={Multiplayer.GetUniqueId()} since_down={_time - _wentDownAt[name]:F3} drift={drift} at={player.GlobalPosition} "
                        + $"velocity={player.Velocity} dashing={player.IsDashing} mask={player.CollisionMask} touching=[{string.Join(";", touching)}]");
                }
            }
        }

        foreach (var check in _hpChecks.Where(c => _time >= c.At).ToList())
        {
            if (IsInstanceValid(check.Player))
            {
                _hpAfterReviveMin = Mathf.Min(_hpAfterReviveMin, check.Player.Vitals.Hp + _hitSinceRevive.GetValueOrDefault(check.Player.Name));
            }

            _hpChecks.Remove(check);
        }
    }
}
