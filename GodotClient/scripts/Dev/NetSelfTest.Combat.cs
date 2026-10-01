using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
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
    private readonly Dictionary<long, int> _playerDamageByAttacker = new();
    private int _hitsSent;
    private int _enemyDeathsSeen;
    private int _damageTaken;
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
        player.Vitals.Hit += amount => _damageTaken += amount;
    }

    private void PrintCombatChecks(long me)
    {
        GD.Print($"[combat-check] me={me} hits_sent={_hitsSent} enemy_deaths_seen={_enemyDeathsSeen} damage_taken={_damageTaken} "
            + $"hits_by_skill={string.Join(",", _hitsBySkill.OrderBy(h => h.Key).Select(h => $"{h.Key}:{h.Value}"))}");
        if (Multiplayer.IsServer())
        {
            GD.Print($"[combat-host] damage_by_peer={string.Join(",", _enemyDamageByPeer.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"))}");
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

    private void OnEnemyDamaged(long attacker, int amount) =>
        _enemyDamageByPeer[attacker] = _enemyDamageByPeer.GetValueOrDefault(attacker) + amount;

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
