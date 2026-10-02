using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Player;

// Owned by the peer that controls it: that peer moves it, replicates position, velocity and aim, and decides
// whether its own swings hit. HP lives on the host-owned Vitals child. Every other peer derives legs, body turn
// and torso twist from the replicated values. Design: Docs/Design/multiplayer.md.
public partial class PlayerCharacter : CharacterBody3D
{
    public const string Group = "players";

    public const int Primary = WeaponDefinition.PrimaryButton;
    public const int Secondary = WeaponDefinition.SecondaryButton;

    public const string ModelPath = "res://assets/characters/Knight.glb";
    private const float SyncInterval = 0.05f;
    private const float RemoteFollowRate = 15f;
    private const float BodyTurnRate = 12f;
    private const float MovingThreshold = 0.2f;

    private static readonly Color TrailTint = new(1f, 0.92f, 0.75f);

    private static readonly Dictionary<LegDirection, string> DashClips = new()
    {
        [LegDirection.Forward] = RigAnimations.DashForward,
        [LegDirection.Backward] = RigAnimations.DashBackward,
        [LegDirection.Left] = RigAnimations.DashLeft,
        [LegDirection.Right] = RigAnimations.DashRight,
    };

    private Node3D _model = null!;
    private CharacterAnimator _animator = null!;
    private LegDirection _legs = LegDirection.Forward;
    private float _bodyYaw;
    private float _shownAimYaw;
    private readonly SkillCooldowns _cooldowns = new(2);
    private readonly ManaPool _mana = new(StatRules.MaxMana(PlayerRules.KnightStats));
    private readonly HashSet<ulong> _hitThisSwing = new();
    private float _clock;
    private readonly Guard _guard = new();
    private WeaponDefinition? _weapon = WeaponSets.Default.Active;
    private WeaponDefinition? _stowed = WeaponSets.Default.Stowed;
    private bool _pickUpPending;
    private BoneAttachment3D _hand = null!;
    private BoneAttachment3D _back = null!;
    private bool _reportedUnknownWeapon;

    // The tier of armour the figure is dressed in; -1 before the first look is put on.
    private int _armourShown;
    private SkillDefinition? _active;
    private SkillDefinition? _swing;
    private int _swingButton = -1;
    private float _swingElapsed;
    private int _swingHits;
    private float _swingShownTime;
    private float _swingSpeed;
    private bool _lungeArmed;
    private bool _lunged;
    private bool _reportedMissingControls;
    private readonly ChargeCounter _dashes = new(DashRules.Charges, DashRules.RechargeTime);
    private float _dashLeft;
    private Vector3 _dashDirection;
    private Vector3 _dashFrom;

    [Export]
    public Vector3 NetPosition { get; set; }

    [Export]
    public Vector3 NetVelocity { get; set; }

    [Export]
    public float AimYaw { get; set; }

    // The weapon sets, chosen on the player's own machine and replicated; every machine shows the weapon in hand and
    // the one on the back that they name. Empty for an empty slot.
    [Export]
    public string WeaponId { get; set; } = WeaponSets.Default.Active!.Id;

    [Export]
    public string StowedWeaponId { get; set; } = WeaponSets.Default.Stowed!.Id;

    public long PeerId { get; private set; }

    public IPlayerControls? Controls { get; set; }

    public CharacterAnimator Animator => _animator;

    public PlayerVitals Vitals { get; private set; } = null!;

    public Skeleton3D Skeleton { get; private set; } = null!;

    public WeaponTrail Trail { get; private set; } = null!;

    public HitFlash Flash { get; private set; } = null!;

    public GhostTrail Ghosts { get; private set; } = null!;

    public bool IsDashing => _animator.IsDashing;

    public int DashCharges => _dashes.Available(_clock);

    public float NextDashIn => _dashes.NextChargeIn(_clock);

    // Dash presses turned down for want of a charge.
    public int DashesRefused { get; private set; }

    public bool IsDowned { get; private set; }

    public CharacterStats Stats { get; } = PlayerRules.KnightStats;

    public int MaxMana => StatRules.MaxMana(Stats);

    // Owned by the player's own machine, which is where skills start.
    public int Mana => (int)_mana.Current;

    public float AttackSpeed => StatRules.AttackSpeed(Stats);

    // The weapon this machine shows the player holding; null with an empty hand.
    public WeaponDefinition? Weapon => _weapon;

    // The weapon this machine shows on the player's back; null with nothing there.
    public WeaponDefinition? StowedWeapon => _stowed;

    public bool IsGuarding => _guard.IsUp;

    public float GuardRecoveryLeft => _guard.RecoveryLeft(GuardClock);

    public bool CanRaiseGuard => FreeToGuard && _guard.CanRaise(GuardClock);

    // Between swings only, and with a weapon to guard with.
    private bool FreeToGuard => !IsDowned && _weapon != null && _swing == null && ActiveSkill == null;

    // Every machine keeps its own guard timing, by its own clock: the host's decides blocks and parries.
    private static float GuardClock => Time.GetTicksMsec() / 1000f;

    // The skill whose swing is playing, on every peer.
    public SkillDefinition? ActiveSkill => _active != null && _animator.IsAttacking ? _active : null;

    public bool IsChanneling => ActiveSkill?.Channeled == true;

    public LegDirection? Legs => NetVelocity.Length() > MovingThreshold ? _legs : null;

    public event Action<SkillDefinition>? AttackStarted;

    // Skill and how many enemies the owner reported hitting with that swing.
    public event Action<SkillDefinition, int>? HitsSent;

    // PvP: the other player this machine reported hitting.
    public event Action<PlayerCharacter>? PlayerHit;

    // On every peer, as a dash starts: its direction.
    public event Action<Vector3>? DashStarted;

    // On the owner, as a dash ends: where it started and where it ended.
    public event Action<Vector3, Vector3>? DashFinished;

    // Each frame a swing tests for hits: the skill and how far the drawn weapon reaches from the body centre.
    public event Action<SkillDefinition, float>? HitTested;

    // On every machine, as the guard goes up.
    public event Action? GuardRaised;

    // On every machine, as the player's weapon in hand or on the back changes.
    public event Action<WeaponSets>? WeaponsChanged;

    // On the owner: the weapon it reached for was taken by someone else first.
    public event Action? PickUpRefused;

    // On the owner: the seller's window is open over the game.
    public bool IsTrading { get; set; }

    // On the owner: it asked the seller in reach to trade.
    public event Action<SellerDefinition>? TradeAsked;

    public float CooldownRemaining(int skill) => _cooldowns.Remaining(skill, _clock);

    public bool CanUse(int button) =>
        _weapon != null && _cooldowns.IsReady(button, _clock) && _mana.CanAfford(_weapon.Skill(button).ManaCost);

    public static PlayerCharacter? Find(SceneTree tree, long peerId) =>
        tree.GetNodesInGroup(Group).OfType<PlayerCharacter>().FirstOrDefault(p => p.PeerId == peerId);

    public static PlayerCharacter Create(long peerId, Vector3 spawnPosition)
    {
        var player = new PlayerCharacter
        {
            Name = peerId.ToString(),
            PeerId = peerId,
            Position = spawnPosition,
            NetPosition = spawnPosition,
            CollisionLayer = CollisionLayers.Players,
            CollisionMask = CollisionLayers.World | CollisionLayers.Enemies,
        };
        player.AddChild(CharacterRig.CreateCapsule());

        player._model = new Node3D { Name = "Model" };
        player.AddChild(player._model);
        var body = Assets.Instantiate(ModelPath);
        player._model.AddChild(body);
        var look = CombatVisuals.LookFor(WeaponSets.Default.Active!);
        var backLook = CombatVisuals.LookFor(WeaponSets.Default.Stowed!);
        player._hand = CharacterRig.AttachToHand(body, look);
        player._back = CharacterRig.AttachToBack(body, backLook);
        CharacterRig.ShrinkHead(body);
        player.Skeleton = body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        ArmourLook.Wear(player.Skeleton, player._armourShown);
        player.Trail = new WeaponTrail(player._hand, TrailTint) { Name = "Trail" };
        player.AddChild(player.Trail);
        player.Flash = new HitFlash(body) { Name = "HitFlash" };
        player.AddChild(player.Flash);
        player.Ghosts = new GhostTrail(body, ModelPath, look, backLook) { Name = "Ghosts" };
        player.AddChild(player.Ghosts);
        player._animator = new CharacterAnimator(body);

        player.Vitals = PlayerVitals.Create();
        player.AddChild(player.Vitals);
        player.AddChild(CreateSynchronizer());
        player.SetMultiplayerAuthority((int)peerId);
        player.Vitals.SetMultiplayerAuthority(1);
        return player;
    }

    public override void _Ready()
    {
        AddToGroup(Group);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsMultiplayerAuthority())
        {
            SimulateLocal(delta);
            AdvanceSwing(dt);
        }
        else
        {
            FollowNetwork(dt);
        }

        Present(dt);
    }

    internal void OnDowned()
    {
        IsDowned = true;
        _swing = null;
        _active = null;
        _guard.Lower(GuardClock);
        if (_dashLeft > 0f)
        {
            FinishDash();
        }

        // A body on the ground is not shoved about by the skeletons walking over it; they still bump into it.
        CollisionMask = CollisionLayers.World;

        _animator.PlayDeath();
    }

    internal void OnRevived(Vector3 at)
    {
        IsDowned = false;
        CollisionMask = CollisionLayers.World | CollisionLayers.Enemies;
        _animator.Revive();
        GlobalPosition = at;
        NetPosition = at;
    }

    private void SimulateLocal(double delta)
    {
        if (Controls == null)
        {
            if (!_reportedMissingControls)
            {
                GD.PushError($"[Player {Name}] locally owned but has no controls; it will not move");
                _reportedMissingControls = true;
            }

            return;
        }

        _clock += (float)delta;
        _mana.Regenerate(StatRules.ManaRegenPerSecond * (float)delta);
        Controls.Update(this, delta);

        if (_dashLeft <= 0f && !IsDowned && Controls.DashPressed)
        {
            if (_dashes.TryUse(_clock))
            {
                BeginDash();
            }
            else
            {
                DashesRefused++;
            }
        }

        if (_dashLeft > 0f)
        {
            _dashLeft -= (float)delta;

            // The attack button, held as the dash starts or pressed just after, makes it a lunge.
            if (!_lunged && _weapon != null && _swing == null && _dashLeft > 0f && DashRules.Duration - _dashLeft <= DashRules.LungeWithin
                && (_lungeArmed || Controls.SkillHeld == Primary))
            {
                _lunged = true;
                Rpc(MethodName.StartLunge, _weapon.Lunge.Id, _dashLeft);
            }

            Velocity = _dashDirection * DashRules.Speed;
            MoveAndSlide();
            NetPosition = GlobalPosition;
            NetVelocity = Velocity;
            _shownAimYaw = AimYaw;
            if (_dashLeft <= 0f)
            {
                FinishDash();
            }

            return;
        }

        // A downed body keeps the aim it fell with.
        bool held = IsDowned;
        var move = held ? Vector3.Zero : Controls.Move;
        float amount = Mathf.Min(move.Length(), 1f);
        float? wanted = !held && Controls.AimYaw is { } aim ? aim : amount > 0f ? Yaw.Of(move) : null;
        if (wanted is { } target)
        {
            AimYaw = Angles.RotateToward(AimYaw, target, Turning.MaxRate * (float)delta);
        }

        var velocity = Vector3.Zero;
        if (amount > 0f)
        {
            _legs = LegDirectionSelector.Select(Yaw.Of(move) - AimYaw, _legs).Direction;
            velocity = move / move.Length() * MoveSpeed.For(_legs, ActiveSkill, _guard.IsUp ? _weapon?.Guard : null) * amount;
        }

        Velocity = velocity;
        MoveAndSlide();
        NetPosition = GlobalPosition;
        NetVelocity = velocity;
        _shownAimYaw = AimYaw;

        // While the guard is up nothing else starts.
        bool wantsGuard = Controls.GuardHeld && FreeToGuard;
        if (wantsGuard && _guard.CanRaise(GuardClock))
        {
            Rpc(MethodName.RaiseGuard);
        }
        else if (!wantsGuard && _guard.IsUp)
        {
            Rpc(MethodName.LowerGuard);
        }

        if (_guard.IsUp)
        {
            return;
        }

        // Between swings only, so a swing always plays out with the weapon it started with. While the host is still
        // answering a pick-up the slots stay as they are, so the weapon has somewhere to go when it arrives.
        if (!IsDowned && _swing == null && !_animator.IsAttacking && !_pickUpPending)
        {
            var sets = new WeaponSets(_weapon, _stowed);
            if (Controls.SwapSetsPressed)
            {
                Carry(sets.Swapped());
            }
            else if (Controls.WeaponNextPressed)
            {
                Carry(sets.WithNextActive());
            }
            else if (Controls.DropPressed && _weapon != null)
            {
                GroundWeapons.In(GetTree()).Drop(_weapon, GlobalPosition + Yaw.Forward(AimYaw) * Pickups.InFront, AimYaw);
                Carry(sets.WithHandEmptied());
            }
            else if (Controls.PickUpPressed && sets.HasFreeSlot
                && GroundWeapons.In(GetTree()).InReachOf(GlobalPosition, AimYaw) is { } lying)
            {
                _pickUpPending = true;
                GroundWeapons.In(GetTree()).PickUp(lying.Id);
            }
            else if (Controls.InteractPressed && Market.In(GetTree()).InReachOf(GlobalPosition) is { } seller)
            {
                TradeAsked?.Invoke(seller.Seller);
            }
        }

        if (!IsDowned && Controls.SkillHeld is { } button && !_animator.IsAttacking && CanUse(button))
        {
            var skill = _weapon!.Skill(button);
            _mana.TrySpend(skill.ManaCost);
            _cooldowns.Start(button, skill, _clock);
            _swingButton = button;
            Rpc(MethodName.StartAttack, skill.Id);
        }
    }

    // On the owner: the weapon sets it carries from now on. The synchronizer takes them to the other machines.
    public void Carry(WeaponSets sets)
    {
        WeaponId = sets.Active?.Id ?? "";
        StowedWeaponId = sets.Stowed?.Id ?? "";
        ShowWeapons();
    }

    // The host handed this player the weapon it reached for.
    internal void OnPickedUp(WeaponDefinition weapon)
    {
        _pickUpPending = false;
        var sets = new WeaponSets(_weapon, _stowed);
        if (sets.HasFreeSlot)
        {
            Carry(sets.WithPickedUp(weapon));
            return;
        }

        GD.PushError($"[Player {Name}] no free slot for the {weapon.Name} it was handed; putting it back down");
        GroundWeapons.In(GetTree()).Drop(weapon, GlobalPosition, AimYaw);
    }

    // The host sold this player the weapon. One bought takes a free slot, or with none the hand, whose weapon is put
    // down where the player stands; an improved one takes the place of the weapon it was made from, in its slot.
    internal void OnBought(WeaponDefinition weapon, WeaponSlot? slot)
    {
        var (sets, putDown) = TradeRules.Receive(new WeaponSets(_weapon, _stowed), weapon, slot);
        if (putDown != null)
        {
            GroundWeapons.In(GetTree()).Drop(putDown, GlobalPosition + Yaw.Forward(AimYaw) * Pickups.InFront, AimYaw);
        }

        Carry(sets);
    }

    // Someone else took it first.
    internal void OnPickUpRefused()
    {
        _pickUpPending = false;
        PickUpRefused?.Invoke();
    }

    // Brings the armour shown in line with the tier the host says is worn.
    private void ShowArmour()
    {
        if (Vitals.Armour == _armourShown)
        {
            return;
        }

        _armourShown = Vitals.Armour;
        ArmourLook.Wear(Skeleton, _armourShown);
        Ghosts.SetArmour(_armourShown);
    }

    // Brings the weapons shown in hand and on the back in line with WeaponId and StowedWeaponId.
    private void ShowWeapons()
    {
        if (WeaponId == (_weapon?.Id ?? "") && StowedWeaponId == (_stowed?.Id ?? ""))
        {
            return;
        }

        WeaponDefinition? inHand, onBack;
        try
        {
            inHand = WeaponId.Length > 0 ? Weapons.ById(WeaponId) : null;
            onBack = StowedWeaponId.Length > 0 ? Weapons.ById(StowedWeaponId) : null;
        }
        catch (KeyNotFoundException e)
        {
            if (!_reportedUnknownWeapon)
            {
                GD.PushError($"[Player {Name}] {e.Message}; keeping what it carried");
                _reportedUnknownWeapon = true;
            }

            return;
        }

        var handLook = inHand == null ? null : CombatVisuals.LookFor(inHand);
        var backLook = onBack == null ? null : CombatVisuals.LookFor(onBack);
        if (inHand != _weapon)
        {
            CharacterRig.HoldWeapon(_hand, handLook);
            Trail.Retarget();
        }

        if (onBack != _stowed)
        {
            CharacterRig.HoldOnBack(_back, backLook);
        }

        Ghosts.SetWeapons(handLook, backLook);
        _animator.TwoHanded = inHand != null;
        _weapon = inHand;
        _stowed = onBack;
        WeaponsChanged?.Invoke(new WeaponSets(inHand, onBack));
    }

    // The owner decides its own hits, against enemies as it sees them. A single-moment swing tests once at its hit
    // time; a sweep keeps testing until its window closes, hitting each enemy at most once. A channel repeats its
    // window every cycle until its button is let go or the next cycle cannot be paid for.
    private void AdvanceSwing(float delta)
    {
        if (_swing is not { } skill)
        {
            return;
        }

        if (skill.Channeled && Controls?.SkillHeld != _swingButton)
        {
            EndChannelCycle(skill);
            return;
        }

        _swingElapsed += delta;
        if (_swingElapsed < CombatTiming.HitDelay(skill, _swingSpeed))
        {
            return;
        }

        var me = Yaw.ToGround(GlobalPosition);
        HitTested?.Invoke(skill, Trail.ReachFrom(GlobalPosition));
        foreach (var node in GetTree().GetNodesInGroup(EnemyCharacter.Group))
        {
            if (node is EnemyCharacter enemy && !enemy.IsDead && !_hitThisSwing.Contains(enemy.GetInstanceId())
                && MeleeArc.Hits(me, AimYaw, skill, Yaw.ToGround(enemy.GlobalPosition), BodySize.Radius))
            {
                enemy.RpcId(1, EnemyCharacter.MethodName.RequestDamage, skill.Id);
                _hitThisSwing.Add(enemy.GetInstanceId());
                _swingHits++;
            }
        }

        if (SessionRules.Pvp)
        {
            foreach (var other in GetTree().GetNodesInGroup(Group).OfType<PlayerCharacter>())
            {
                if (other != this && !other.IsDowned && !_hitThisSwing.Contains(other.GetInstanceId())
                    && MeleeArc.Hits(me, AimYaw, skill, Yaw.ToGround(other.GlobalPosition), BodySize.Radius))
                {
                    other.Vitals.RpcId(1, PlayerVitals.MethodName.RequestDamage, skill.Id);
                    _hitThisSwing.Add(other.GetInstanceId());
                    _swingHits++;
                    PlayerHit?.Invoke(other);
                }
            }
        }

        if (!skill.Channeled)
        {
            if (_swingElapsed >= CombatTiming.HitWindowEnd(skill, _swingSpeed))
            {
                _swing = null;
                HitsSent?.Invoke(skill, _swingHits);
            }

            return;
        }

        // Each loop of the clip is a cycle: paid for up front, with its own hit window.
        float cycle = _animator.AttackClipLength / _swingSpeed;
        if (_swingElapsed < cycle)
        {
            return;
        }

        if (IsDowned || !_mana.TrySpend(skill.ManaCost))
        {
            EndChannelCycle(skill);
            return;
        }

        HitsSent?.Invoke(skill, _swingHits);
        _swingElapsed -= cycle;
        _swingHits = 0;
        _hitThisSwing.Clear();
    }

    // The owner's side of a dash: charge already spent. Skeletons stop blocking so the dash can roll through them.
    // A Spin still held carries on through the dash, hitting and costing as it goes. Any other swing in progress is
    // abandoned, though a primary swing that had not landed yet is not lost: the dash carries it on as a lunge.
    private void BeginDash()
    {
        var direction = DashRules.Direction(Yaw.ToGround(Controls!.Move), AimYaw);
        bool spinsOn = _swing is { Channeled: true } && Controls.SkillHeld == _swingButton;
        _lungeArmed = _weapon != null && _swing == _weapon.Primary;
        _lunged = false;
        if (_swing != null && !spinsOn)
        {
            HitsSent?.Invoke(_swing, _swingHits);
            _swing = null;
        }

        _dashLeft = DashRules.Duration;
        _dashDirection = new Vector3(direction.X, 0f, direction.Y);
        _dashFrom = GlobalPosition;
        CollisionMask = CollisionLayers.World;
        Rpc(MethodName.StartDash, _dashDirection, spinsOn);
    }

    private void FinishDash()
    {
        _dashLeft = 0f;
        CollisionMask = CollisionLayers.World | CollisionLayers.Enemies;
        DashFinished?.Invoke(_dashFrom, GlobalPosition);
    }

    // Every peer: the clip for the dash's direction relative to the facing, and the ghosts. With spinsOn the Spin in
    // progress keeps the body and the dash only moves it.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void StartDash(Vector3 direction, bool spinsOn)
    {
        _guard.Lower(GuardClock);
        _animator.PlayDash(DashClips[LegDirectionSelector.Nearest(Yaw.Of(direction) - AimYaw)], DashRules.Duration);
        if (spinsOn)
        {
            _animator.LeaveBodyToAttack();
        }
        else
        {
            // The swing stops counting at once (no rooting, no trail); only its animation fades.
            _active = null;
            _animator.CancelAttack();
        }

        Ghosts.Emit(DashRules.Duration);
        DashStarted?.Invoke(direction);
    }

    private void EndChannelCycle(SkillDefinition skill)
    {
        HitsSent?.Invoke(skill, _swingHits);
        _swing = null;
        Rpc(MethodName.EndChannel);
    }

    private void FollowNetwork(float delta)
    {
        GlobalPosition = GlobalPosition.Lerp(NetPosition, 1f - Mathf.Exp(-RemoteFollowRate * delta));
        _shownAimYaw = Yaw.Approach(_shownAimYaw, AimYaw, RemoteFollowRate, delta);
    }

    private void Present(float delta)
    {
        ShowWeapons();
        ShowArmour();
        float speed = NetVelocity.Length();
        bool moving = speed > MovingThreshold;
        float targetBodyYaw = _shownAimYaw;
        if (moving)
        {
            var pose = LegDirectionSelector.Select(Yaw.Of(NetVelocity) - _shownAimYaw, _legs);
            _legs = pose.Direction;
            targetBodyYaw = _shownAimYaw + pose.BodyYawOffset;
        }

        _bodyYaw = Yaw.Approach(_bodyYaw, targetBodyYaw, BodyTurnRate, delta);
        _model.Rotation = new Vector3(0f, _bodyYaw + CharacterRig.ModelYawOffset, 0f);
        _animator.Update(delta, moving ? _legs : null, speed, Angles.Wrap(_shownAimYaw - _bodyYaw));

        // Driven by the swing's start on this peer, so every player's trail shows on every machine.
        _swingShownTime += delta;
        Trail.Recording = ActiveSkill is { } swing && (swing.Channeled || WeaponTrail.Shows(swing, _swingShownTime, _swingSpeed));
    }

    // By skill id, not button: the swing plays as started even if the weapon change that preceded it hasn't reached
    // this machine yet.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void StartAttack(string skillId)
    {
        if (FindSkill(skillId) is { } skill)
        {
            BeginSwing(skill, AttackSpeed);
        }
    }

    // The thrust a dash carries, played so that it reaches full extension as the dash ends, landsIn from now. The
    // dash keeps the legs; the thrust takes the upper body back from it.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void StartLunge(string skillId, float landsIn)
    {
        if (landsIn <= 0f)
        {
            GD.PushError($"[Player {Name}] lunge '{skillId}' with no dash left to carry it ({landsIn} s)");
            return;
        }

        if (FindSkill(skillId) is { } lunge)
        {
            BeginSwing(lunge, CombatTiming.LungeSpeed(lunge, landsIn));
            _animator.LeaveUpperBodyToAttack();
        }
    }

    // What a seller needs to know of this player: what it carries and wears.
    public Buyer AsBuyer() => new(new WeaponSets(_weapon, _stowed), Vitals.Armour);

    // The skill of that id as the weapons this player carries have it: an improved weapon's hits harder than the
    // plain skill the id names. Throws for an id no weapon has.
    public SkillDefinition SkillById(string skillId) => new WeaponSets(_weapon, _stowed).SkillById(skillId);

    private SkillDefinition? FindSkill(string skillId)
    {
        try
        {
            return SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Player {Name}] {e.Message}");
            return null;
        }
    }

    private void BeginSwing(SkillDefinition skill, float speed)
    {
        _animator.PlayAttack(CombatVisuals.ClipFor(skill), CombatVisuals.IsFullBody(skill), speed, skill.Channeled);
        _active = skill;
        _swing = skill;
        _swingSpeed = speed;
        _swingElapsed = 0f;
        _swingHits = 0;
        _swingShownTime = 0f;
        _hitThisSwing.Clear();
        AttackStarted?.Invoke(skill);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndChannel() => _animator.EndLoop();

    // The guard plays on the upper body at the attack speed, like a held swing, so the legs keep moving under it. It
    // takes the attack layer from the last swing, which stops counting as active: otherwise that swing would read as
    // still playing for as long as the guard is up, and drop the guard the next frame.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RaiseGuard()
    {
        _active = null;
        _guard.Raise(GuardClock);
        _animator.PlayAttack(RigAnimations.Guard, fullBody: false, AttackSpeed, loop: true);
        GuardRaised?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void LowerGuard()
    {
        _guard.Lower(GuardClock);
        _animator.EndLoop();
    }

    // Host: how this player's guard meets an attack coming from `from`, by where it last reported standing and aiming.
    public GuardOutcome ResolveGuard(Vector3 from) => _weapon == null
        ? GuardOutcome.Unguarded
        : _guard.Resolve(_weapon.Guard, GuardClock, Yaw.ToGround(NetPosition), AimYaw, Yaw.ToGround(from));

    private static MultiplayerSynchronizer CreateSynchronizer()
    {
        var config = new SceneReplicationConfig();
        foreach (var property in new[] { PropertyName.NetPosition, PropertyName.NetVelocity, PropertyName.AimYaw })
        {
            var path = new NodePath($".:{property}");
            config.AddProperty(path);
            config.PropertySetSpawn(path, true);
            config.PropertySetReplicationMode(path, SceneReplicationConfig.ReplicationMode.Always);
        }

        foreach (var property in new[] { PropertyName.WeaponId, PropertyName.StowedWeaponId })
        {
            var path = new NodePath($".:{property}");
            config.AddProperty(path);
            config.PropertySetSpawn(path, true);
            config.PropertySetReplicationMode(path, SceneReplicationConfig.ReplicationMode.OnChange);
        }

        return new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config, ReplicationInterval = SyncInterval };
    }
}
