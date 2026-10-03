using System;
using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Player;

// What the host keeps for a player, even though the player's movement is owned by its own peer: its HP, the armour it
// wears, and the gold, souls and magic orbs it has earned and not yet spent.
public partial class PlayerVitals : Node
{
    // A hit of no damage type: the game's own.
    private static readonly Color DamageColor = new(1f, 0.35f, 0.3f);
    private static readonly Color ParryColor = new(1f, 0.85f, 0.35f);
    private static readonly Color BlockColor = new(0.8f, 0.88f, 1f);

    private readonly Health _health = new(PlayerRules.MaxHp);
    private readonly Purse _purse = new();
    private readonly ArmourWear _wear = new();
    private readonly BarrierPool _barrier = new(Weapons.Barrier.Barrier!);
    private readonly StatusBars _status = new();

    [Export]
    public int Hp { get; set; } = PlayerRules.MaxHp;

    [Export]
    public int Gold { get; set; }

    [Export]
    public int Souls { get; set; }

    [Export]
    public int Orbs { get; set; }

    // The tier of armour worn (Armours.AtTier), from 0 for what everyone starts in.
    [Export]
    public int Armour { get; set; }

    // What is left of the barrier a staff raises: the host's to keep, like the HP it stands in front of.
    [Export]
    public int Barrier { get; set; } = Weapons.Barrier.Barrier!.Strength;

    // The player's statuses (Core's Statuses, as a number): the host's to work out, like the HP they wear down;
    // every machine's to show, and its own machine's to obey.
    [Export]
    public int StatusMask { get; set; }

    public Statuses Statuses => (Statuses)StatusMask;

    // Frozen or stunned: it neither moves nor acts.
    public bool IsLost => Statuses.IsLost();

    // The share of its run and attack speed it keeps: less while chilled.
    public float Speed => Statuses.SpeedShare();

    // Host only: the bars themselves, for what the player deals and takes.
    public StatusBars Status => _status;

    public event Action<int>? Hit;

    // Host only: a burn or a wound bit this player for so much.
    public static event Action<PlayerCharacter, DamageType, int>? StatusBit;

    // Host only, PvP: attacker peer, victim peer, damage taken.
    public static event Action<long, long, int>? DamagedByPlayer;

    // Host only: an attack met this player's guard.
    public static event Action<PlayerCharacter, GuardOutcome>? Guarded;

    // Host only: this player's barrier took that much of a blow.
    public static event Action<PlayerCharacter, int>? BarrierTook;

    private PlayerCharacter Player => GetParent<PlayerCharacter>();

    public static PlayerVitals Create()
    {
        var vitals = new PlayerVitals { Name = "Vitals" };
        var config = new SceneReplicationConfig().Sending(
            SceneReplicationConfig.ReplicationMode.OnChange,
            PropertyName.Hp, PropertyName.Gold, PropertyName.Souls, PropertyName.Orbs, PropertyName.Armour, PropertyName.Barrier, PropertyName.StatusMask);
        vitals.AddChild(new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config });
        return vitals;
    }

    // Host only: an attack from `from`, which the player's guard may block (a share of the damage gets through) or
    // parry (none does). Whoever calls it reacts to a parry. `types` is the mask of the damage types it is of
    // (DamageTypes.MaskOf), for the colour it shows in.
    public GuardOutcome TakeAttack(int damage, Vector3 from, int types)
    {
        // A barrier that is spent is no guard at all.
        var guard = Player.Weapon?.Guard;
        var outcome = guard?.Barrier != null && !_barrier.Holds ? GuardOutcome.Unguarded : Player.ResolveGuard(from);
        if (outcome != GuardOutcome.Unguarded)
        {
            Guarded?.Invoke(Player, outcome);
            Rpc(MethodName.ShowGuarded, (int)outcome);
        }

        // The guard takes its share first (a barrier, all it has left), then the armour takes its own of what is left.
        int past = guard == null ? damage
            : guard.Barrier != null && outcome != GuardOutcome.Unguarded ? AbsorbWithBarrier(damage)
            : Guard.DamageThrough(guard, outcome, damage);
        int through = _wear.Through(Armours.AtTier(Armour), past);
        if (through > 0)
        {
            TakeHit(through, types);
        }

        return outcome;
    }

    // Host only: the barrier comes back while it is down.
    public override void _Process(double delta)
    {
        if (Multiplayer.IsServer())
        {
            _barrier.Advance((float)delta, up: Player.IsGuarding && Player.Weapon?.Guard.Barrier != null);
            Barrier = _barrier.Left;

            // Burns and wounds bite past any guard; the armour takes its share of each as of any blow.
            foreach (var tick in _status.Advance((float)delta, _health.Current, _status.Adjusted(Resistances.None)))
            {
                int through = _wear.Through(Armours.AtTier(Armour), tick.Damage);
                if (through > 0)
                {
                    StatusBit?.Invoke(Player, tick.Type, through);
                    TakeHit(through, DamageTypes.MaskOf(new[] { tick.Type }));
                }
            }

            StatusMask = (int)_status.Active;
        }
    }

    private int AbsorbWithBarrier(int damage)
    {
        int through = _barrier.Absorb(damage);
        Barrier = _barrier.Left;
        BarrierTook?.Invoke(Player, damage - through);
        return through;
    }

    // Host only.
    public void EarnGold(int amount)
    {
        _purse.EarnGold(amount);
        Gold = _purse.Gold;
    }

    // Host only.
    public void EarnSouls(int amount)
    {
        _purse.EarnSouls(amount);
        Souls = _purse.Souls;
    }

    // Host only.
    public void EarnOrbs(int amount)
    {
        _purse.EarnOrbs(amount);
        Orbs = _purse.Orbs;
    }

    // Host only: the player, `distance` from a seller, asks for this. Its cost is taken when it goes through.
    public BuyOutcome Buy(TradeItem item, float distance)
    {
        var outcome = TradeRules.Buy(_purse, item, distance, Player.IsDowned);
        Gold = _purse.Gold;
        Souls = _purse.Souls;
        Orbs = _purse.Orbs;

        // Armour is the host's to hand over, like the purse it is paid from; a weapon goes to the buyer's own machine.
        if (outcome == BuyOutcome.Bought && item.Armour is { } armour)
        {
            Armour = armour.Tier;
        }

        return outcome;
    }

    // Host only.
    public void Wear(ArmourDefinition armour) => Armour = armour.Tier;

    // Damage no guard can stop.
    public void TakeHit(int damage, int types)
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
        Rpc(MethodName.ShowHit, taken, types);
        if (_health.IsDead)
        {
            _status.Clear();
            Rpc(MethodName.Downed);
            GetTree().CreateTimer(PlayerRules.RespawnDelay).Timeout += Respawn;
        }
    }

    // PvP: another player's machine reports its swing hit this player; the host applies the damage.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDamage(string skillId)
    {
        long attackerId = Multiplayer.Sender();
        if (!Multiplayer.IsServer() || !SessionRules.Pvp)
        {
            GD.PushError($"[Vitals {Player.Name}] player damage request from peer {attackerId} outside a PvP host");
            return;
        }

        var attacker = PlayerCharacter.Find(GetTree(), attackerId);
        if (attacker == null || attacker == Player)
        {
            GD.PushWarning($"[Vitals {Player.Name}] hit from peer {attackerId}, who is gone or is this player; ignored");
            return;
        }

        SkillDefinition skill;
        try
        {
            skill = attacker.SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Vitals {Player.Name}] {e.Message} (from peer {attackerId})");
            return;
        }

        // As with a blow on a skeleton: by what this player's bars and the attacker's make of it, and what gets
        // past the guard builds this player's bars.
        var dealer = attacker.Vitals.Status;
        var resistances = _status.Adjusted(Resistances.None);
        int before = _health.Current;
        TakeAttack(StatRules.Damage(skill, attacker.Stats, resistances, dealer), attacker.NetPosition, DamageTypes.MaskOf(skill.Types));
        if (before > _health.Current)
        {
            StatRules.Afflict(_status, skill, attacker.Stats, resistances, dealer);
            DamagedByPlayer?.Invoke(attackerId, Player.PeerId, before - _health.Current);
        }
    }

    // Host only: the player starts a skill. What of it is divine or void builds on the player itself.
    public void Cast(SkillDefinition skill) => _status.BuildFromCasting(skill);

    private void Respawn()
    {
        // The player may have left while down.
        if (!IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        _health.RestoreFull();
        _status.Clear();
        Hp = _health.Current;
        Rpc(MethodName.Revived, ArenaMap.In(GetTree()).RevivePointFor(Player.PeerId));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowHit(int amount, int types)
    {
        if (SeenFloating)
        {
            FloatingText.Spawn(Player, amount.ToString(), types == DamageTypes.NoTypes ? DamageColor : DamageTypeColours.Number(types));
        }

        Player.Flash.Flash();
        Hit?.Invoke(amount);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowGuarded(int outcome)
    {
        bool parried = (GuardOutcome)outcome == GuardOutcome.Parried;
        if (SeenFloating)
        {
            FloatingText.Spawn(Player, parried ? "Parry!" : "Block", parried ? ParryColor : BlockColor);
        }
    }

    // What floats over this player is drawn through floors, so only while it is on the camera's subject's floor.
    private bool SeenFloating => ArenaMap.In(GetTree()).OnSubjectsFloor(Player.GlobalPosition);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Downed() => Player.OnDowned();

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Revived(Vector3 at) => Player.OnRevived(at);
}
