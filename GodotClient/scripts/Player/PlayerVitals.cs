using System;
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

    private readonly Health _health = new(PlayerRules.MaxHp);

    [Export]
    public int Hp { get; set; } = PlayerRules.MaxHp;

    public event Action<int>? Hit;

    // Host only, PvP: attacker peer, victim peer, damage taken.
    public static event Action<long, long, int>? DamagedByPlayer;

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
    private void RequestDamage(int skill)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        long attackerId = sender == 0 ? Multiplayer.GetUniqueId() : sender;
        if (!Multiplayer.IsServer() || !SessionRules.Pvp)
        {
            GD.PushError($"[Vitals {Player.Name}] player damage request from peer {attackerId} outside a PvP host");
            return;
        }

        if (skill < 0 || skill >= PlayerCharacter.SkillSet.Length)
        {
            GD.PushError($"[Vitals {Player.Name}] unknown skill index {skill} from peer {attackerId}");
            return;
        }

        var attacker = PlayerCharacter.Find(GetTree(), attackerId);
        if (attacker == null || attacker == Player)
        {
            GD.PushWarning($"[Vitals {Player.Name}] hit from peer {attackerId}, who is gone or is this player; ignored");
            return;
        }

        int before = _health.Current;
        TakeHit(StatRules.Damage(PlayerCharacter.SkillSet[skill].Damage, attacker.Stats));
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
    private void Downed() => Player.OnDowned();

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Revived(Vector3 at) => Player.OnRevived(at);
}
