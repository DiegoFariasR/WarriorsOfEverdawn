using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [combat-check] / [combat-host]: hits, kills and damage. [pvp-check] / [pvp-host]: hits and damage between
// players. [flash-check]: hit blinks on skeletons and players, gone after their quarter second.
public partial class NetSelfTest
{
    private readonly Dictionary<string, int> _hitsBySkill = new();
    private readonly Dictionary<long, int> _enemyDamageByPeer = new();

    // On the host: hits on skeletons by damage type, and how many met a weakness, a resistance or neither.
    private readonly Dictionary<DamageType, int> _hitsByType = new();
    private int _weakHits;
    private int _resistedHits;
    private int _plainHits;
    private readonly Dictionary<long, int> _playerDamageByAttacker = new();
    private int _hitsSent;

    // Host: the hardest blow each skill landed. A blow is cut short by what its victim had left, so the hardest is
    // what tells an improved weapon from a plain one.
    private readonly SortedDictionary<string, int> _biggestHitBySkill = new();
    private int _enemyDeathsSeen;
    private int _damageTaken;
    private int _hitsTaken;
    private int _playerHitsSent;
    private int _enemyFlashes;
    private int _playerFlashes;
    private int _flashLingering;

    private void TrackCombat(PlayerCharacter player)
    {
        player.HitsSent += (skill, hits) =>
        {
            _hitsSent += hits;
            _hitsBySkill[skill.Id] = _hitsBySkill.GetValueOrDefault(skill.Id) + hits;
        };
        player.PlayerHit += _ => _playerHitsSent++;
        player.Vitals.Hit += amount =>
        {
            _damageTaken += amount;
            _hitsTaken++;
        };
    }

    private void PrintCombatChecks(long me)
    {
        GD.Print($"[combat-check] me={me} hits_sent={_hitsSent} enemy_deaths_seen={_enemyDeathsSeen} damage_taken={_damageTaken} hits_taken={_hitsTaken} "
            + $"hits_by_skill={string.Join(",", _hitsBySkill.OrderBy(h => h.Key).Select(h => $"{h.Key}:{h.Value}"))}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[combat-host] damage_by_peer={string.Join(",", _enemyDamageByPeer.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"))} "
                + $"biggest_hit_by_skill={string.Join(",", _biggestHitBySkill.Select(h => $"{h.Key}:{h.Value}"))} "
                + $"hits_by_type={string.Join(",", _hitsByType.OrderBy(h => h.Key).Select(h => $"{DamageTypes.NameOf(h.Key)}:{h.Value}"))} "
                + $"weak_hits={_weakHits} resisted_hits={_resistedHits} plain_hits={_plainHits}");
        }

        GD.Print($"[pvp-check] me={me} pvp={SessionRules.Pvp} hits_on_players={_playerHitsSent}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[pvp-host] damage_by_attacker={string.Join(",", _playerDamageByAttacker.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"))}");
        }

        GD.Print($"[flash-check] me={me} enemy_flashes={_enemyFlashes} player_flashes={_playerFlashes} lingering_frames={_flashLingering}");
    }

    private void OnEnemyDied(EnemyCharacter enemy)
    {
        _enemyDeathsSeen++;
        NoteCorpse(enemy);
    }

    private void OnEnemyDamaged(long attacker, SkillDefinition skill, int amount, int leaning)
    {
        foreach (var type in skill.Types)
        {
            _hitsByType[type] = _hitsByType.GetValueOrDefault(type) + 1;
        }

        _weakHits += leaning > 0 ? 1 : 0;
        _resistedHits += leaning < 0 ? 1 : 0;
        _plainHits += leaning == 0 ? 1 : 0;
        _enemyDamageByPeer[attacker] = _enemyDamageByPeer.GetValueOrDefault(attacker) + amount;
        _biggestHitBySkill[skill.Id] = Mathf.Max(_biggestHitBySkill.GetValueOrDefault(skill.Id), amount);
        CountMagicHit(attacker, skill);
    }

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

    // A flash must be gone shortly after its quarter second.
    private void MeasureFlashes()
    {
        var flashes = GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Select(e => e.Flash)
            .Concat(_players.GetChildren().OfType<PlayerCharacter>().Select(p => p.Flash));
        _flashLingering += flashes.Count(f => f.Showing && f.Age > HitFlash.Duration + 0.1f);
    }
}
