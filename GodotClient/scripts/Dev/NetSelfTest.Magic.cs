using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [magic-check]: this machine's player throws its wand's or staff's bolts, as many to a full cast as the skill has and never
// more, and every
// machine sees the others'; each bolt is gone by the time it could have flown its distance, and the burst it ends in
// by a moment after; a wand's spell held on an area is drawn, here and for the others, a round of strikes each cycle;
// barriers show on every machine, and this machine's is seen to take blows (it falls below full) and to come back
// while it is down; a staff's ball bursts where it ends, here and on the others' machines; every weapon of an
// element shows it, a staff or wand at its head and an enchanted weapon all over it,
// and no plain weapon does. On the host, [magic-host]: the hits each player's bolts and area spells landed, the
// blows of enchanted weapons that came as part magic, what
// each player's barrier took, and with --barrier-drill the blows the host dealt each barrier as it went up (whether
// a skeleton finds a barrier up is the fight's business, so the drill deals one through the same path, TakeAttack)
// with the HP those blows cost, which is none while the barrier holds. Fire and ice leave their ground: every machine
// sees burning ground and ice laid, and never more patches at once than the casters may keep; on the host turns on
// them burned and chilled skeletons, and skeletons stood on ice.
public partial class NetSelfTest
{
    // A bolt outlives the time its distance takes at its speed by no more than this.
    private const float BoltMargin = 0.25f;

    // The drill's blow, and how long a barrier has been up when it lands: well under what a barrier takes, so one
    // drilled a few times in a row still holds.
    private const int DrillBlow = 6;
    private const float DrillAfter = 0.3f;

    private readonly Dictionary<long, int> _boltHitsByPeer = new();
    private readonly Dictionary<long, int> _arrowHitsByPeer = new();
    private readonly Dictionary<long, int> _areaHitsByPeer = new();
    private readonly Dictionary<long, int> _enchantedHitsByPeer = new();
    private readonly Dictionary<long, int> _blastHitsByPeer = new();
    private int _blastsHere;
    private int _blastsSeenRemote;
    private int _blastCaughtMost;
    private readonly Dictionary<long, int> _barrierTookByPeer = new();
    private readonly Dictionary<long, int> _drillsByPeer = new();
    private readonly Dictionary<long, float> _barrierUpFor = new();
    private readonly HashSet<long> _drilledThisRaise = new();
    private int _drillHpLost;
    private int _barrierBefore = -1;
    private int _alightFrames;
    private int _darkFrames;
    private int _plainAlightFrames;
    private int _barrierRises;
    private int _boltsHere;
    private int _boltsSeenRemote;
    private int _boltsLanded;
    private int _castsHere;
    private int _boltsThisCast;
    private readonly Dictionary<string, int> _boltsPerCastMost = new();
    private int _castsOverThrown;
    private int _boltLingering;
    private int _burstsMost;
    private int _areaRoundsHereStart = -1;
    private int _areaFramesHere;
    private int _areaFramesRemote;
    private int _strikesMost;
    private int _burningLaid;
    private int _icyLaid;
    private int _surfacesShownMost;
    private int _burningActed;
    private int _icyActed;
    private int _onIceFrames;
    private int _playersActedOn;
    private int _barrierFramesHere;
    private int _barrierFramesRemote;
    private int _barrierLeast = int.MaxValue;

    public bool BarrierDrill { get; init; }

    private void TrackMagic()
    {
        Bolts.Loosed += OnBoltLoosed;
        Bolts.Landed += OnBoltLanded;
        Bolts.Burst += OnBurst;
        PlayerVitals.BarrierTook += OnBarrierTook;
        GroundSurfaces.Laid += OnGroundLaid;
        GroundSurfaces.Acted += OnGroundActed;
    }

    private void OnGroundLaid(SurfaceKind kind)
    {
        _burningLaid += kind == SurfaceKind.Burning ? 1 : 0;
        _icyLaid += kind == SurfaceKind.Icy ? 1 : 0;
    }

    private void OnGroundActed(SurfaceKind kind, Node3D body)
    {
        if (body is PlayerCharacter)
        {
            _playersActedOn++;
            return;
        }

        _burningActed += kind == SurfaceKind.Burning ? 1 : 0;
        _icyActed += kind == SurfaceKind.Icy ? 1 : 0;
    }

    private void UntrackMagic()
    {
        Bolts.Loosed -= OnBoltLoosed;
        Bolts.Landed -= OnBoltLanded;
        Bolts.Burst -= OnBurst;
        PlayerVitals.BarrierTook -= OnBarrierTook;
        GroundSurfaces.Laid -= OnGroundLaid;
        GroundSurfaces.Acted -= OnGroundActed;
    }

    private void TrackCasts(PlayerCharacter player)
    {
        player.AttackStarted += skill =>
        {
            if (skill.Projectile != null)
            {
                _castsHere++;
                _boltsThisCast = 0;
            }
        };
        player.BlastCaught += (_, caught) => _blastCaughtMost = Mathf.Max(_blastCaughtMost, caught);
    }

    private void OnBurst(PlayerCharacter caster, SkillDefinition skill)
    {
        if (GodotObject.IsInstanceValid(caster) && caster.IsMultiplayerAuthority())
        {
            _blastsHere++;
        }
        else
        {
            _blastsSeenRemote++;
        }
    }

    private void OnBoltLoosed(PlayerCharacter caster, SkillDefinition skill)
    {
        if (!caster.IsMultiplayerAuthority())
        {
            _boltsSeenRemote++;
            return;
        }

        // By skill: the weapon on the bot's back is cast for a moment at a time, and a volley cut short by the swap
        // back says nothing about the weapon it holds for the rest of the session.
        _boltsHere++;
        _boltsThisCast++;
        _boltsPerCastMost[skill.Id] = Mathf.Max(_boltsPerCastMost.GetValueOrDefault(skill.Id), _boltsThisCast);
        _castsOverThrown += _boltsThisCast == skill.Projectiles + 1 ? 1 : 0;
    }

    private void OnBoltLanded(SkillDefinition skill) => _boltsLanded++;

    // Host only.
    private void OnBarrierTook(PlayerCharacter player, int taken) =>
        _barrierTookByPeer[player.PeerId] = _barrierTookByPeer.GetValueOrDefault(player.PeerId) + taken;

    // Host only (the event fires where damage is applied).
    private void CountMagicHit(long attacker, SkillDefinition skill)
    {
        // A blow or an arrow of an enchanted weapon: part magic, whatever else it is counted as.
        if (skill is { Element: not null, MagicShare: > 0f and < 1f })
        {
            _enchantedHitsByPeer[attacker] = _enchantedHitsByPeer.GetValueOrDefault(attacker) + 1;
        }

        if (skill.BlastRadius > 0f)
        {
            _blastHitsByPeer[attacker] = _blastHitsByPeer.GetValueOrDefault(attacker) + 1;
        }
        else if (skill.Projectile != null && Bolts.IsArrow(skill))
        {
            _arrowHitsByPeer[attacker] = _arrowHitsByPeer.GetValueOrDefault(attacker) + 1;
        }
        else if (skill.Projectile != null)
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
        foreach (var player in PlayerCharacter.All(GetTree()))
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
                player.Vitals.TakeAttack(DrillBlow, player.GlobalPosition + Yaw.Forward(player.AimYaw), DamageTypes.NoTypes);
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

        var ground = GroundSurfaces.In(GetTree());
        _surfacesShownMost = Mathf.Max(_surfacesShownMost, ground.Shown);
        if (Multiplayer.IsServer() && Enemy.EnemyCharacter.Standing(GetTree()).Any(e => ground.IcyUnder(e.GlobalPosition)))
        {
            _onIceFrames++;
        }

        float longest = Weapons.All.SelectMany(w => w.Skills).Where(s => s.Projectile != null).Max(s => s.Projectile!.MaxDistance / s.Projectile.Speed);
        _boltLingering += bolts.OldestFlight > longest + BoltMargin ? 1 : 0;
        _burstsMost = Mathf.Max(_burstsMost, bolts.BurstsShowing);

        foreach (var player in PlayerCharacter.All(GetTree()))
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

            // A flash plays over everything the player holds for its quarter second, the element included.
            if (!player.Flash.Showing && !player.IsDowned)
            {
                bool ofAnElement = player.Weapon is { } held && (held.Element != null || held.Enchantment != null);
                _alightFrames += ofAnElement && player.WeaponAlight ? 1 : 0;
                _darkFrames += ofAnElement && !player.WeaponAlight ? 1 : 0;
                _plainAlightFrames += !ofAnElement && player.Weapon != null && player.WeaponAlight ? 1 : 0;
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
        GD.Print($"[magic-check] me={me} weapon={local?.Weapon?.Id ?? NoWeapon} casts={_castsHere} bolts_here={_boltsHere} "
            + $"bolts_per_cast_most={(local?.Weapon?.Primary is { } thrown ? _boltsPerCastMost.GetValueOrDefault(thrown.Id) : 0)} "
            + $"bolts_per_cast_wanted={local?.Weapon?.Primary.Projectiles ?? 0} casts_over_thrown={_castsOverThrown} bolts_landed={_boltsLanded} bolts_seen_remote={_boltsSeenRemote} bolt_lingering_frames={_boltLingering} "
            + $"blasts_here={_blastsHere} blasts_seen_remote={_blastsSeenRemote} blast_caught_most={_blastCaughtMost} "
            + $"bursts_most={_burstsMost} area_rounds_here={(local == null ? 0 : local.SpellArea.Rounds - Mathf.Max(_areaRoundsHereStart, 0))} "
            + $"area_frames_here={_areaFramesHere} area_frames_remote={_areaFramesRemote} strikes_most={_strikesMost} "
            + $"barrier_frames_here={_barrierFramesHere} barrier_frames_remote={_barrierFramesRemote} "
            + $"barrier_least={(_barrierLeast == int.MaxValue ? -1 : _barrierLeast)} barrier_now={local?.Vitals.Barrier ?? -1} barrier_rises={_barrierRises} alight_frames={_alightFrames} dark_frames={_darkFrames} plain_alight_frames={_plainAlightFrames} barrier_full={Weapons.Barrier.Barrier!.Strength} "
            + $"burning_laid={_burningLaid} icy_laid={_icyLaid} surfaces_shown_most={_surfacesShownMost} surfaces_kept_most={Surfaces.MostPerCaster * _seen.Count}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[magic-host] bolt_hits={PerPeer(_boltHitsByPeer)} arrow_hits={PerPeer(_arrowHitsByPeer)} area_hits={PerPeer(_areaHitsByPeer)} blast_hits={PerPeer(_blastHitsByPeer)} enchanted_hits={PerPeer(_enchantedHitsByPeer)} barrier_took={PerPeer(_barrierTookByPeer)} "
                + $"barrier_drills={PerPeer(_drillsByPeer)} drill_blow={DrillBlow} drill_hp_lost={_drillHpLost} "
                + $"burning_acted={_burningActed} icy_acted={_icyActed} on_ice_frames={_onIceFrames} players_acted_on={_playersActedOn}");
        }
    }
}
