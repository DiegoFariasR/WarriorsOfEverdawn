using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [magic-check]: this machine's player throws its staff's bolts, as many to a cast as the skill has, and every
// machine sees the others'; each bolt is gone by the time it could have flown its distance, and the burst it ends in
// by a moment after; the spell held on an area is drawn, here and for the others, a round of strikes each cycle;
// barriers show on every machine, and this machine's is seen to take blows (it falls below full) and to come back
// while it is down. On the host, [magic-host]: the hits each player's bolts and area spells landed, what
// each player's barrier took, and with --barrier-drill the blows the host dealt each barrier as it went up (whether
// a skeleton finds a barrier up is the fight's business, so the drill deals one through the same path, TakeAttack)
// with the HP those blows cost, which is none while the barrier holds.
public partial class NetSelfTest
{
    // A bolt outlives the time its distance takes at its speed by no more than this.
    private const float BoltMargin = 0.25f;

    // The drill's blow, and how long a barrier has been up when it lands: well under what a barrier takes, so one
    // drilled a few times in a row still holds.
    private const int DrillBlow = 6;
    private const float DrillAfter = 0.3f;

    private readonly Dictionary<long, int> _boltHitsByPeer = new();
    private readonly Dictionary<long, int> _areaHitsByPeer = new();
    private readonly Dictionary<long, int> _barrierTookByPeer = new();
    private readonly Dictionary<long, int> _drillsByPeer = new();
    private readonly Dictionary<long, float> _barrierUpFor = new();
    private readonly HashSet<long> _drilledThisRaise = new();
    private int _drillHpLost;
    private int _barrierBefore = -1;
    private int _barrierRises;
    private int _boltsHere;
    private int _boltsSeenRemote;
    private int _boltsLanded;
    private int _castsHere;
    private int _boltsThisCast;
    private int _boltsPerCastMost;
    private int _boltsPerCastWanted;
    private int _boltLingering;
    private int _burstsMost;
    private int _areaRoundsHereStart = -1;
    private int _areaFramesHere;
    private int _areaFramesRemote;
    private int _strikesMost;
    private int _barrierFramesHere;
    private int _barrierFramesRemote;
    private int _barrierLeast = int.MaxValue;

    public bool BarrierDrill { get; init; }

    private void TrackMagic()
    {
        Bolts.Loosed += OnBoltLoosed;
        Bolts.Landed += OnBoltLanded;
        PlayerVitals.BarrierTook += OnBarrierTook;
    }

    private void UntrackMagic()
    {
        Bolts.Loosed -= OnBoltLoosed;
        Bolts.Landed -= OnBoltLanded;
        PlayerVitals.BarrierTook -= OnBarrierTook;
    }

    private void TrackCasts(PlayerCharacter player) =>
        player.AttackStarted += skill =>
        {
            if (skill.Projectile != null)
            {
                _castsHere++;
                _boltsThisCast = 0;
                _boltsPerCastWanted = Mathf.Max(_boltsPerCastWanted, skill.Projectiles);
            }
        };

    private void OnBoltLoosed(PlayerCharacter caster, SkillDefinition skill)
    {
        if (!caster.IsMultiplayerAuthority())
        {
            _boltsSeenRemote++;
            return;
        }

        _boltsHere++;
        _boltsThisCast++;
        _boltsPerCastMost = Mathf.Max(_boltsPerCastMost, _boltsThisCast);
    }

    private void OnBoltLanded(SkillDefinition skill) => _boltsLanded++;

    // Host only.
    private void OnBarrierTook(PlayerCharacter player, int taken) =>
        _barrierTookByPeer[player.PeerId] = _barrierTookByPeer.GetValueOrDefault(player.PeerId) + taken;

    // Host only (the event fires where damage is applied).
    private void CountMagicHit(long attacker, SkillDefinition skill)
    {
        if (skill.Projectile != null)
        {
            _boltHitsByPeer[attacker] = _boltHitsByPeer.GetValueOrDefault(attacker) + 1;
        }
        else if (skill.Area != null)
        {
            _areaHitsByPeer[attacker] = _areaHitsByPeer.GetValueOrDefault(attacker) + 1;
        }
    }

    // Host only. Once per raise: a barrier that has been up for DrillAfter and still holds is dealt DrillBlow from
    // in front, as a skeleton would deal it.
    private void DrillBarriers(float delta)
    {
        foreach (var player in _players.GetChildren().OfType<PlayerCharacter>())
        {
            long peer = player.PeerId;
            bool up = player.IsGuarding && !player.IsDowned && player.Weapon?.Guard.Barrier != null;
            _barrierUpFor[peer] = up ? _barrierUpFor.GetValueOrDefault(peer) + delta : 0f;
            if (!up)
            {
                _drilledThisRaise.Remove(peer);
                continue;
            }

            if (_barrierUpFor[peer] >= DrillAfter && player.Vitals.Barrier >= DrillBlow && _drilledThisRaise.Add(peer))
            {
                int hp = player.Vitals.Hp;
                player.Vitals.TakeAttack(DrillBlow, player.GlobalPosition + Yaw.Forward(player.AimYaw));
                _drillsByPeer[peer] = _drillsByPeer.GetValueOrDefault(peer) + 1;
                _drillHpLost += hp - player.Vitals.Hp;
            }
        }
    }

    private void MeasureMagic(float delta)
    {
        var bolts = GetTree().CurrentScene.GetNodeOrNull<Bolts>(Bolts.NodeName);
        if (bolts == null)
        {
            return;
        }

        if (BarrierDrill && Multiplayer.IsServer())
        {
            DrillBarriers(delta);
        }

        float longest = Weapons.Staffs.Max(s => s.Primary.Projectile!.MaxDistance / s.Primary.Projectile.Speed);
        _boltLingering += bolts.OldestFlight > longest + BoltMargin ? 1 : 0;
        _burstsMost = Mathf.Max(_burstsMost, bolts.BurstsShowing);

        foreach (var player in _players.GetChildren().OfType<PlayerCharacter>())
        {
            bool mine = player.IsMultiplayerAuthority();
            _strikesMost = Mathf.Max(_strikesMost, player.SpellArea.StrikesAlive);
            if (mine && _areaRoundsHereStart < 0)
            {
                _areaRoundsHereStart = player.SpellArea.Rounds;
            }

            if (player.SpellArea.Showing)
            {
                _areaFramesHere += mine ? 1 : 0;
                _areaFramesRemote += mine ? 0 : 1;
            }

            if (player.BarrierShown)
            {
                _barrierFramesHere += mine ? 1 : 0;
                _barrierFramesRemote += mine ? 0 : 1;
            }

            if (mine)
            {
                _barrierLeast = Mathf.Min(_barrierLeast, player.Vitals.Barrier);
                _barrierRises += _barrierBefore >= 0 && player.Vitals.Barrier > _barrierBefore ? 1 : 0;
                _barrierBefore = player.Vitals.Barrier;
            }
        }
    }

    private void PrintMagicCheck(long me)
    {
        var local = LocalPlayer();
        GD.Print($"[magic-check] me={me} weapon={local?.Weapon?.Id ?? NoWeapon} casts={_castsHere} bolts_here={_boltsHere} bolts_per_cast_most={_boltsPerCastMost} "
            + $"bolts_per_cast_wanted={_boltsPerCastWanted} bolts_landed={_boltsLanded} bolts_seen_remote={_boltsSeenRemote} bolt_lingering_frames={_boltLingering} "
            + $"bursts_most={_burstsMost} area_rounds_here={(local == null ? 0 : local.SpellArea.Rounds - Mathf.Max(_areaRoundsHereStart, 0))} "
            + $"area_frames_here={_areaFramesHere} area_frames_remote={_areaFramesRemote} strikes_most={_strikesMost} "
            + $"barrier_frames_here={_barrierFramesHere} barrier_frames_remote={_barrierFramesRemote} "
            + $"barrier_least={(_barrierLeast == int.MaxValue ? -1 : _barrierLeast)} barrier_now={local?.Vitals.Barrier ?? -1} barrier_rises={_barrierRises} barrier_full={Weapons.Barrier.Barrier!.Strength}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[magic-host] bolt_hits={PerPeer(_boltHitsByPeer)} area_hits={PerPeer(_areaHitsByPeer)} barrier_took={PerPeer(_barrierTookByPeer)} "
                + $"barrier_drills={PerPeer(_drillsByPeer)} drill_blow={DrillBlow} drill_hp_lost={_drillHpLost}");
        }
    }

    private static string PerPeer(Dictionary<long, int> counts) => string.Join(",", counts.OrderBy(c => c.Key).Select(c => $"{c.Key}:{c.Value}"));
}
