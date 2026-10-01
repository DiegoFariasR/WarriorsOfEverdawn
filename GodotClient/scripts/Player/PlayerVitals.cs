using System;
using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Player;

// A player's HP, owned by the host even though the player's movement is owned by its own peer.
public partial class PlayerVitals : Node
{
    private static readonly Color DamageColor = new(1f, 0.35f, 0.3f);
    private static readonly Color ParryColor = new(1f, 0.85f, 0.35f);
    private static readonly Color BlockColor = new(0.8f, 0.88f, 1f);

    private readonly Health _health = new(PlayerRules.MaxHp);

    [Export]
    public int Hp { get; set; } = PlayerRules.MaxHp;

    public event Action<int>? Hit;

    // Host only, PvP: attacker peer, victim peer, damage taken.
    public static event Action<long, long, int>? DamagedByPlayer;

    // Host only: an attack met this player's guard.
    public static event Action<PlayerCharacter, GuardOutcome>? Guarded;

    private PlayerCharacter Player => GetParent<PlayerCharacter>();

    public static PlayerVitals Create()
    {
        var vitals = new PlayerVitals { Name = "Vitals" };
        var config = new SceneReplicationConfig();
        var hp = new NodePath($".:{PropertyName.Hp}");
        config.AddProperty(hp);
        config.PropertySetSpawn(hp, true);
        config.PropertySetReplicationMode(hp, SceneReplicationConfig.ReplicationMode.OnChange);
        vitals.AddChild(new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config });
        return vitals;
    }

    // Host only: an attack from `from`, which the player's guard may block (a share of the damage gets through) or
    // parry (none does). Whoever calls it reacts to a parry.
    public GuardOutcome TakeAttack(int damage, Vector3 from)
    {
        var outcome = Player.ResolveGuard(from);
        if (outcome != GuardOutcome.Unguarded)
        {
            Guarded?.Invoke(Player, outcome);
            Rpc(MethodName.ShowGuarded, (int)outcome);
        }

        int through = Guard.DamageThrough(Player.Weapon.Guard, outcome, damage);
        if (through > 0)
        {
            TakeHit(through);
        }

        return outcome;
    }

    // Damage no guard can stop.
    public void TakeHit(int damage)
    {
        if (!Multiplayer.IsServer())
        {
            GD.PushError($"[Vitals {Player.Name}] TakeHit called on peer {Multiplayer.GetUniqueId()}; only the host applies damage");
            return;
        }

        int taken = _health.TakeDamage(damage);
        if (taken == 0)
        {
            return;
        }

        Hp = _health.Current;
        Rpc(MethodName.ShowHit, taken);
        if (_health.IsDead)
        {
            Rpc(MethodName.Downed);
            GetTree().CreateTimer(PlayerRules.RespawnDelay).Timeout += Respawn;
        }
    }

    // PvP: another player's machine reports its swing hit this player; the host applies the damage.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDamage(string skillId)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        long attackerId = sender == 0 ? Multiplayer.GetUniqueId() : sender;
        if (!Multiplayer.IsServer() || !SessionRules.Pvp)
        {
            GD.PushError($"[Vitals {Player.Name}] player damage request from peer {attackerId} outside a PvP host");
            return;
        }

        SkillDefinition skill;
        try
        {
            skill = Weapons.SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Vitals {Player.Name}] {e.Message} (from peer {attackerId})");
            return;
        }

        var attacker = PlayerCharacter.Find(GetTree(), attackerId);
        if (attacker == null || attacker == Player)
        {
            GD.PushWarning($"[Vitals {Player.Name}] hit from peer {attackerId}, who is gone or is this player; ignored");
            return;
        }

        int before = _health.Current;
        TakeAttack(StatRules.Damage(skill.Damage, attacker.Stats), attacker.NetPosition);
        if (before > _health.Current)
        {
            DamagedByPlayer?.Invoke(attackerId, Player.PeerId, before - _health.Current);
        }
    }

    private void Respawn()
    {
        // The player may have left while down.
        if (!IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        _health.RestoreFull();
        Hp = _health.Current;
        Rpc(MethodName.Revived, Vector3.Zero);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowHit(int amount)
    {
        FloatingText.Spawn(Player, amount.ToString(), DamageColor);
        Player.Flash.Flash();
        Hit?.Invoke(amount);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowGuarded(int outcome)
    {
        bool parried = (GuardOutcome)outcome == GuardOutcome.Parried;
        FloatingText.Spawn(Player, parried ? "Parry!" : "Block", parried ? ParryColor : BlockColor);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Downed() => Player.OnDowned();

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Revived(Vector3 at) => Player.OnRevived(at);
}
