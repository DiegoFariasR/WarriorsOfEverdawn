using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// Attached in --bot runs. Records what this peer sees of every player, how far the local player's chest points
// from its aim, and the combat it takes part in, then prints the [*-check] lines ./dev.sh net-test asserts on.
public partial class NetSelfTest : Node
{
    private const float SampleInterval = 0.5f;

    private const float FullWeight = 0.999f;

    // Only frames where the twist does real work count toward the with/without comparison; below this the
    // clips' own chest sway dominates both numbers.
    private const float SignificantTwist = 20f * Mathf.Pi / 180f;

    private readonly Node3D _players;
    private readonly Hud _hud;
    private readonly Dictionary<string, Observation> _seen = new();
    private float _sinceSample;
    private float _torsoErrorSum;
    private float _torsoErrorMax;
    private int _torsoSamples;
    private float _twistedErrorSum;
    private float _untwistedErrorSum;
    private int _twistedSamples;
    private float? _lastAim;
    private float _maxTurnRate;
    private int _turnLimitedFrames;
    private float _playerHead = float.NaN;
    private float _playerHeadgear = float.NaN;
    private float _enemyHead = float.NaN;
    private float _enemyHeadgear = float.NaN;
    private readonly List<float> _swingRates = new();
    private readonly Dictionary<string, int> _hitsBySkill = new();
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
    private int _trailMaxEdges;
    private float _trailTipReach;
    private float _sinceTrailStopped;
    private int _trailLingering;
    private bool _enemyTrailSeen;
    private readonly Dictionary<string, List<float>> _tipReachAtHit = new();
    private int _enemyFlashes;
    private int _playerFlashes;
    private int _flashLingering;
    private int _hudHpMismatches;
    private int _enemyBarMismatches;
    private int _maxEnemyBars;
    private int _maxPlayerBars;
    private int _manaMismatches;
    private int _playerHitsSent;
    private readonly Dictionary<long, int> _playerDamageByAttacker = new();
    private readonly List<float> _dodgeDistances = new();
    private readonly List<float> _dodgeStartTimes = new();
    private int _remoteDodgesSeen;
    private int _ghostLingering;
    private float _time;
    private float? _lastSwingPosition;
    private string _groundSpeeds = "";
    private readonly Dictionary<LegDirection, (float Sum, int Count)> _torsoSignedByLegs = new();
    private readonly Dictionary<long, int> _enemyDamageByPeer = new();
    private int _hitsSent;
    private int _enemyDeathsSeen;
    private int _damageTaken;

    public NetSelfTest(Node3D players, Hud hud)
    {
        _players = players;
        _hud = hud;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public NetSelfTest()
        : this(null!, null!)
    {
    }

    public override void _EnterTree()
    {
        EnemyCharacter.Died += OnEnemyDied;
        EnemyCharacter.DamageTaken += OnEnemyDamaged;
        HitFlash.Started += OnFlash;
        PlayerVitals.DamagedByPlayer += OnPlayerDamagedByPlayer;
    }

    public override void _ExitTree()
    {
        EnemyCharacter.Died -= OnEnemyDied;
        EnemyCharacter.DamageTaken -= OnEnemyDamaged;
        HitFlash.Started -= OnFlash;
        PlayerVitals.DamagedByPlayer -= OnPlayerDamagedByPlayer;
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var player in _players.GetChildren().OfType<PlayerCharacter>())
        {
            if (!_seen.ContainsKey(player.Name))
            {
                Track(player);
            }
        }

        MeasureTurn((float)delta);
        MeasureEnemyHelmet();
        MeasureSpinMovement();

        _sinceSample += (float)delta;
        if (_sinceSample < SampleInterval)
        {
            return;
        }

        _sinceSample = 0f;
        foreach (var player in _players.GetChildren().OfType<PlayerCharacter>())
        {
            var seen = _seen[player.Name];
            var position = player.GlobalPosition;
            if (seen.Last is { } last)
            {
                seen.Travel += last.DistanceTo(position);
            }

            seen.Last = position;
            if (player.Legs is { } legs)
            {
                seen.Legs.Add(legs);
            }
        }
    }

    // Runs after the animation trees in the same frame (this node sits after Players), so each reading is the
    // swing clip's position after that frame's advance.
    public override void _Process(double delta)
    {
        _time += (float)delta;
        MeasureGhosts();
        MeasureSwingRate((float)delta);
        MeasureSpinTurn((float)delta);
        MeasureTrails((float)delta);
        MeasureFlashes();
        MeasureHud();
    }

    public void PrintSummary()
    {
        long me = Multiplayer.GetUniqueId();
        foreach (var (name, seen) in _seen.OrderBy(pair => pair.Key))
        {
            GD.Print($"[net-check] me={me} player={name} travel={seen.Travel:F1} attacks={seen.Attacks} legs={string.Join(",", seen.Legs.OrderBy(l => l))}");
        }

        float mean = _torsoSamples > 0 ? _torsoErrorSum / _torsoSamples : float.NaN;
        var byLegs = string.Join(",", _torsoSignedByLegs.OrderBy(p => p.Key).Select(p => $"{p.Key}:{Mathf.RadToDeg(p.Value.Sum / p.Value.Count):+0.0;-0.0}"));
        GD.Print($"[combat-check] me={me} hits_sent={_hitsSent} enemy_deaths_seen={_enemyDeathsSeen} damage_taken={_damageTaken}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[combat-host] damage_by_peer={string.Join(",", _enemyDamageByPeer.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"))}");
        }

        float withTwist = _twistedSamples > 0 ? _twistedErrorSum / _twistedSamples : float.NaN;
        float withoutTwist = _twistedSamples > 0 ? _untwistedErrorSum / _twistedSamples : float.NaN;
        GD.Print($"[anim-check] me={me} torso_vs_aim_mean_deg={Mathf.RadToDeg(mean):F1} torso_vs_aim_max_deg={Mathf.RadToDeg(_torsoErrorMax):F1} samples={_torsoSamples} "
            + $"twist_samples={_twistedSamples} with_twist_deg={Mathf.RadToDeg(withTwist):F1} without_twist_deg={Mathf.RadToDeg(withoutTwist):F1} signed_by_legs={byLegs}");
        float swingRate = _swingRates.Count > 0 ? _swingRates.OrderBy(r => r).ElementAt(_swingRates.Count / 2) : float.NaN;
        GD.Print($"[speed-check] me={me} ground_speed={_groundSpeeds} swing_playback_rate={swingRate:F2} expected={LocalPlayer()?.AttackSpeed:F2} samples={_swingRates.Count}");
        float turnRatio = _spinTurnExpected > 0f ? Mathf.Abs(_spinTurn) / _spinTurnExpected : float.NaN;
        GD.Print($"[skill-check] me={me} spins={_spins} spin_revolutions={_spinRevolutions:F1} spin_hits={_hitsBySkill.GetValueOrDefault(Skills.Spin.Id)} "
            + $"slice_hits={_hitsBySkill.GetValueOrDefault(Skills.Slice.Id)} spin_turn_ratio={turnRatio:F2} "
            + $"spin_frames={_spinFrames} spin_moving_frames={_spinMovingFrames} spin_max_speed={_spinMaxSpeed:F2} spin_speed_limit={_spinSpeedLimit:F2}");
        var trail = LocalPlayer()?.Trail;
        GD.Print($"[trail-check] me={me} tip_length={(trail?.TipLength ?? float.NaN):F2} max_edges={_trailMaxEdges} "
            + $"tip_reach_max={_trailTipReach:F2} "
            + $"lingering_frames={_trailLingering} enemy_trail_seen={(_enemyTrailSeen ? 1 : 0)}");
        var reachBySkill = PlayerCharacter.SkillSet.Select(s =>
        {
            var reaches = _tipReachAtHit.GetValueOrDefault(s.Id);
            float median = reaches is { Count: > 0 } ? reaches.OrderBy(r => r).ElementAt(reaches.Count / 2) : float.NaN;
            return $"{s.Id}={median:F2}/{s.Range:F2}";
        });
        GD.Print($"[reach-check] me={me} {string.Join(" ", reachBySkill)}");
        var stats = LocalPlayer()?.Stats;
        GD.Print($"[ui-check] me={me} hp_mismatch_frames={_hudHpMismatches} bar_mismatch_frames={_enemyBarMismatches} "
            + $"mana_mismatch_frames={_manaMismatches} max_enemy_bars={_maxEnemyBars} max_player_bars={_maxPlayerBars} "
            + $"mana_shown={_hud.ShownMana}/{LocalPlayer()?.MaxMana} stats=STR{stats?.Str},WIS{stats?.Wis},AGI{stats?.Agi}");
        GD.Print($"[pvp-check] me={me} pvp={SessionRules.Pvp} hits_on_players={_playerHitsSent}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[pvp-host] damage_by_attacker={string.Join(",", _playerDamageByAttacker.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"))}");
        }
        GD.Print($"[flash-check] me={me} enemy_flashes={_enemyFlashes} player_flashes={_playerFlashes} lingering_frames={_flashLingering}");
        float dodgeMedian = _dodgeDistances.Count > 0 ? _dodgeDistances.OrderBy(d => d).ElementAt(_dodgeDistances.Count / 2) : float.NaN;
        GD.Print($"[dodge-check] me={me} dodges={_dodgeDistances.Count} distance_median={dodgeMedian:F2} expected={DodgeRules.Distance:F2} "
            + $"max_in_recharge_window={MaxDodgesInRechargeWindow()} charges={DodgeRules.Charges} refused={LocalPlayer()?.DodgesRefused} remote_dodges_seen={_remoteDodgesSeen} "
            + $"ghosts_emitted={LocalPlayer()?.Ghosts.Emitted} ghost_lingering_frames={_ghostLingering}");
        GD.Print($"[head-check] me={me} head_expected={CharacterRig.HeadScale:F3} headgear_expected={CharacterRig.HeadgearScale:F3} "
            + $"player_head={_playerHead:F3} player_headgear={_playerHeadgear:F3} enemy_head={_enemyHead:F3} enemy_headgear={_enemyHeadgear:F3}");
        GD.Print($"[turn-check] me={me} max_turn_deg_s={Mathf.RadToDeg(_maxTurnRate):F0} limit_deg_s={Mathf.RadToDeg(Turning.MaxRate):F0} frames_at_limit={_turnLimitedFrames}");
    }

    private void Track(PlayerCharacter player)
    {
        var seen = new Observation();
        _seen[player.Name] = seen;
        player.AttackStarted += _ => seen.Attacks++;
        if (!player.IsMultiplayerAuthority())
        {
            player.DodgeStarted += _ => _remoteDodgesSeen++;
        }
        if (player.IsMultiplayerAuthority())
        {
            player.HitsSent += (skill, hits) =>
            {
                _hitsSent += hits;
                _hitsBySkill[skill.Id] = _hitsBySkill.GetValueOrDefault(skill.Id) + hits;
            };
            player.AttackStarted += skill => _spins += skill.Id == Skills.Spin.Id ? 1 : 0;
            player.PlayerHit += _ => _playerHitsSent++;
            player.DodgeStarted += _ => _dodgeStartTimes.Add(_time);
            player.DodgeFinished += (from, to) => _dodgeDistances.Add(new Vector2(to.X - from.X, to.Z - from.Z).Length());
            player.HitTested += (skill, reach) =>
            {
                if (!_tipReachAtHit.TryGetValue(skill.Id, out var reaches))
                {
                    _tipReachAtHit[skill.Id] = reaches = new List<float>();
                }

                reaches.Add(reach);
            };
            player.Vitals.Hit += amount => _damageTaken += amount;
            var twist = player.Animator.Twist;
            twist.ModificationProcessed += () => MeasureTorso(player, twist);
            MeasureHeads(player.Skeleton, out _playerHead, out _playerHeadgear);
        }
    }

    // Only while running without an attack: swings and idle turns legitimately point the chest off the aim.
    private void MeasureTorso(PlayerCharacter player, TorsoTwistModifier twist)
    {
        if (player.Legs == null || player.Animator.IsAttacking || twist.ChestBone < 0)
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

    // Clip seconds per real second while a swing plays. Frames where the clip restarts or sits clamped at its end
    // are skipped, so only steady playback counts.
    private void MeasureSwingRate(float delta)
    {
        var local = _players.GetChildren().OfType<PlayerCharacter>().FirstOrDefault(p => p.IsMultiplayerAuthority());
        if (local == null)
        {
            return;
        }

        if (_groundSpeeds.Length == 0)
        {
            _groundSpeeds = string.Join(",", local.Animator.GroundSpeedByLegs.Select(p => $"{p.Key}:{p.Value:F2}"));
        }

        if (!local.Animator.IsAttacking)
        {
            _lastSwingPosition = null;
            return;
        }

        float position = local.Animator.AttackClipPosition;
        if (_lastSwingPosition is { } last && position > last && position < local.Animator.AttackClipLength - 0.001f)
        {
            _swingRates.Add((position - last) / delta);
        }

        _lastSwingPosition = position;
    }

    // While a slowing swing (Spin) plays, the local player may move, but never faster than its share of run speed.
    // The frame a swing starts is skipped: the movement for that frame was decided before the swing began.
    private void MeasureSpinMovement()
    {
        var slowing = LocalPlayer() is { ActiveSkill: { MoveSpeedFactor: < 1f } skill } local ? (local, skill) : default;
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
        if (local == null || local.ActiveSkill?.Id != Skills.Spin.Id)
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
            float revolutions = delta * local.AttackSpeed / local.Animator.AttackClipLength;
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

    // The local trail must build during swings and be gone shortly after it stops recording. Tip reach is how far
    // the drawn blade tip gets from the body centre, to compare with the hit range.
    private void MeasureTrails(float delta)
    {
        var local = LocalPlayer();
        if (local != null)
        {
            var trail = local.Trail;
            _trailMaxEdges = Mathf.Max(_trailMaxEdges, trail.EdgeCount);
            if (trail.EdgeCount > 0)
            {
                var reach = trail.LatestTip - local.GlobalPosition;
                _trailTipReach = Mathf.Max(_trailTipReach, new Vector2(reach.X, reach.Z).Length());
            }

            _sinceTrailStopped = trail.Recording ? 0f : _sinceTrailStopped + delta;
            if (_sinceTrailStopped > WeaponTrail.FadeTime + 0.05f && trail.EdgeCount > 0)
            {
                _trailLingering++;
            }
        }

        _enemyTrailSeen |= GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Any(e => e.Trail.EdgeCount >= 2);
    }

    // Another player's dodge seen here, and ghosts that outstay their fade.
    private void MeasureGhosts()
    {
        var local = LocalPlayer();
        if (local != null && local.Ghosts.Showing > 0 && local.Ghosts.OldestAge > GhostTrail.FadeTime + 0.1f)
        {
            _ghostLingering++;
        }
    }

    // Dodges started within any stretch just short of one recharge: never more than the charges.
    private int MaxDodgesInRechargeWindow()
    {
        float window = DodgeRules.RechargeTime * 0.99f;
        int most = 0;
        for (int i = 0; i < _dodgeStartTimes.Count; i++)
        {
            most = Mathf.Max(most, _dodgeStartTimes.Count(t => t >= _dodgeStartTimes[i] && t < _dodgeStartTimes[i] + window));
        }

        return most;
    }

    private PlayerCharacter? LocalPlayer() =>
        _players.GetChildren().OfType<PlayerCharacter>().FirstOrDefault(p => p.IsMultiplayerAuthority());

    // The scale actually on a live character's head and headgear meshes. Headgear is either skinned beside the head
    // (the Knight's helmet) or hung on the head bone by the importer (the skeleton warrior's).
    private static void MeasureHeads(Skeleton3D skeleton, out float head, out float headgear)
    {
        head = float.NaN;
        headgear = float.NaN;
        var meshes = skeleton.GetChildren().OfType<MeshInstance3D>()
            .Concat(skeleton.GetChildren().OfType<BoneAttachment3D>().Where(a => a.BoneName == "head")
                .SelectMany(a => a.GetChildren().OfType<MeshInstance3D>()));
        foreach (var mesh in meshes)
        {
            float expected = CharacterRig.HeadScaleOf(mesh.Name);
            if (expected == CharacterRig.HeadScale)
            {
                head = mesh.Transform.Basis.Scale.Y;
            }
            else if (expected == CharacterRig.HeadgearScale)
            {
                headgear = mesh.Transform.Basis.Scale.Y;
            }
        }
    }

    // Enemies come and go, so the latest one wearing headgear is measured.
    private void MeasureEnemyHelmet()
    {
        foreach (var enemy in GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>())
        {
            MeasureHeads(enemy.Skeleton, out float head, out float headgear);
            _enemyHead = head;
            if (!float.IsNaN(headgear))
            {
                _enemyHeadgear = headgear;
            }
        }
    }

    // One aim update happens per physics frame, so consecutive readings are exactly one turn step apart.
    private void MeasureTurn(float delta)
    {
        var local = _players.GetChildren().OfType<PlayerCharacter>().FirstOrDefault(p => p.IsMultiplayerAuthority());
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

    private void OnEnemyDied(EnemyCharacter enemy) => _enemyDeathsSeen++;

    // Host only (the event fires where damage is applied).
    private void OnPlayerDamagedByPlayer(long attacker, long victim, int amount) =>
        _playerDamageByAttacker[attacker] = _playerDamageByAttacker.GetValueOrDefault(attacker) + amount;

    private void OnFlash(HitFlash flash)
    {
        if (flash.GetParent() is EnemyCharacter)
        {
            _enemyFlashes++;
        }
        else if (flash.GetParent() is PlayerCharacter)
        {
            _playerFlashes++;
        }
    }

    // Runs after the HUD in the frame (it sits earlier under the arena), so both read the same synced values. The
    // player frame must show the local HP and mana; every living skeleton and every other player who is up must
    // have a bar showing their HP; no bar may outlive its owner.
    private void MeasureHud()
    {
        var local = LocalPlayer();
        if (local != null && _hud.ShownHp != local.Vitals.Hp)
        {
            _hudHpMismatches++;
        }

        if (local != null && _hud.ShownMana != local.Mana)
        {
            _manaMismatches++;
        }

        var skeletons = GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => !e.IsDead).ToList();
        var others = _players.GetChildren().OfType<PlayerCharacter>().Where(p => !p.IsMultiplayerAuthority() && !p.IsDowned).ToList();
        var shown = _hud.Bars.Shown.ToList();
        _maxEnemyBars = Mathf.Max(_maxEnemyBars, shown.Count(s => s.Target is EnemyCharacter));
        _maxPlayerBars = Mathf.Max(_maxPlayerBars, shown.Count(s => s.Target is PlayerCharacter));
        bool matches = shown.Count == skeletons.Count + others.Count
            && skeletons.All(e => shown.Any(s => s.Target == e && s.Shown == e.Hp))
            && others.All(p => shown.Any(s => s.Target == p && s.Shown == p.Vitals.Hp));
        if (!matches)
        {
            _enemyBarMismatches++;
        }
    }

    // A flash must be gone shortly after its quarter second.
    private void MeasureFlashes()
    {
        var flashes = GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Select(e => e.Flash)
            .Concat(_players.GetChildren().OfType<PlayerCharacter>().Select(p => p.Flash));
        _flashLingering += flashes.Count(f => f.Showing && f.Age > HitFlash.Duration + 0.1f);
    }

    private void OnEnemyDamaged(long attacker, int amount) =>
        _enemyDamageByPeer[attacker] = _enemyDamageByPeer.GetValueOrDefault(attacker) + amount;

    private sealed class Observation
    {
        public Vector3? Last { get; set; }

        public float Travel { get; set; }

        public int Attacks { get; set; }

        public HashSet<LegDirection> Legs { get; } = new();
    }
}
