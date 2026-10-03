using System;
using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Level;
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

    // Every player's figure, before its armour: the Knight, bare-headed as Everdawn's heroes are. With its great
    // helm on, the widest thing on the figure, the head read as far too big from the cameras here.
    public static readonly CharacterLook Look = CharacterLook.Of("Knight", "Cape");
    private const float SyncInterval = 0.05f;
    private const float RemoteFollowRate = 15f;
    private const float BodyTurnRate = 12f;
    private const float MovingThreshold = 0.2f;

    // What a player on its feet runs into; dashing or down, the level alone.
    private const uint OnFootMask = CollisionLayers.World | CollisionLayers.Enemies;

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
    private BoneAttachment3D _offHand = null!;
    private SpellArea _spellArea = null!;
    private CastingCircles _circles = null!;
    private BarrierBubble _barrier = null!;
    private int _swingLoosed;
    private int _boltsThrown;
    private BoneAttachment3D _back = null!;
    private bool _reportedUnknownWeapon;

    // The tier of armour the figure is dressed in; it is built in the first, tier 0.
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

    // Slower while chilled, as its run is.
    public float AttackSpeed => StatRules.AttackSpeed(Stats) * Vitals.Speed;

    // The weapon this machine shows the player holding; null with an empty hand.
    public WeaponDefinition? Weapon => _weapon;

    // The weapon this machine shows on the player's back; null with nothing there.
    public WeaponDefinition? StowedWeapon => _stowed;

    public WeaponSets Sets => new(_weapon, _stowed);

    public bool IsGuarding => _guard.IsUp;

    public float GuardRecoveryLeft => _guard.RecoveryLeft(GuardClock);

    public bool CanRaiseGuard => FreeToGuard && _guard.CanRaise(GuardClock);

    // Between swings only, and with a weapon to guard with.
    private bool FreeToGuard => !IsDowned && !Vitals.IsLost && _weapon != null && _swing == null && ActiveSkill == null;

    // Every machine keeps its own guard timing, by its own clock: the host's decides blocks and parries.
    private static float GuardClock => Time.GetTicksMsec() / 1000f;

    // The skill whose swing is playing, on every peer.
    public SkillDefinition? ActiveSkill => _active != null && _animator.IsAttacking ? _active : null;

    // The spell this machine draws the player holding on an area, and the barrier it draws round it.
    public SpellArea SpellArea => _spellArea;

    public bool BarrierShown => _barrier.Visible;

    public StatusShow StatusShow { get; private set; } = null!;

    // Whether an element shows on what this machine draws in the player's hand: an enchanted weapon's all over it,
    // a staff's at its head.
    public bool WeaponAlight => _hand.Meshes().Any(m => m.MaterialOverlay != null);

    public bool IsChanneling => ActiveSkill?.Channeled == true;

    public LegDirection? Legs => NetVelocity.Length() > MovingThreshold ? _legs : null;

    public event Action<SkillDefinition>? AttackStarted;

    // Skill and how many enemies the owner reported hitting with that swing.
    public event Action<SkillDefinition, int>? HitsSent;

    // PvP: the other player this machine reported hitting.
    public event Action<PlayerCharacter>? PlayerHit;

    // On the owner, as one of its balls bursts: the skill and how many bodies the burst caught.
    public event Action<SkillDefinition, int>? BlastCaught;

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

    public static IEnumerable<PlayerCharacter> All(SceneTree tree) => tree.GetNodesInGroup(Group).OfType<PlayerCharacter>();

    public static PlayerCharacter? Find(SceneTree tree, long peerId) => All(tree).FirstOrDefault(p => p.PeerId == peerId);

    // The player this machine controls; null until it has spawned here.
    public static PlayerCharacter? Local(SceneTree tree) => Find(tree, tree.GetMultiplayer().GetUniqueId());

    public static PlayerCharacter Create(long peerId, Vector3 spawnPosition)
    {
        var player = new PlayerCharacter
        {
            Name = peerId.ToString(),
            PeerId = peerId,
            Position = spawnPosition,
            NetPosition = spawnPosition,
            CollisionLayer = CollisionLayers.Players,
            CollisionMask = OnFootMask,
            FloorSnapLength = Gravity.FloorSnap,
            FloorMaxAngle = Gravity.SteepestFloor,
        };
        player.AddChild(CharacterRig.CreateCapsule());

        player._model = new Node3D { Name = "Model" };
        player.AddChild(player._model);
        var body = ArmourLook.Build(Look, player._armourShown);
        player._model.AddChild(body);
        var look = CombatVisuals.LookFor(WeaponSets.Default.Active!);
        var backLook = CombatVisuals.LookFor(WeaponSets.Default.Stowed!);
        player._hand = CharacterRig.AttachToHand(body, look);
        player._offHand = CharacterRig.AttachOffHand(body, look);
        player._back = CharacterRig.AttachToBack(body, backLook);
        player.Skeleton = CharacterBody.SkeletonOf(body);
        player.Trail = new WeaponTrail(player._hand, TrailTint) { Name = "Trail" };
        player.AddChild(player.Trail);
        player.Flash = new HitFlash(body) { Name = "HitFlash" };
        player.AddChild(player.Flash);
        player.Ghosts = new GhostTrail(body, ArmourLook.Dressed(Look, player._armourShown), look, backLook) { Name = "Ghosts" };
        player.AddChild(player.Ghosts);
        player._spellArea = new SpellArea { Name = "SpellArea" };
        player.AddChild(player._spellArea);
        player._circles = new CastingCircles { Name = "CastingCircles" };
        player.AddChild(player._circles);
        player._barrier = new BarrierBubble();
        player.AddChild(player._barrier);
        player.StatusShow = new StatusShow { Name = "StatusShow" };
        player.AddChild(player.StatusShow);
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
        CollisionMask = OnFootMask;
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

        // Frozen or stunned, by the host's word: no step, no dash, no skill, and what it was swinging is over.
        bool lost = Vitals.IsLost;
        if (lost && _swing is { } cut)
        {
            if (cut.Channeled)
            {
                EndChannelCycle(cut);
            }

            _swing = null;
        }

        if (lost && _dashLeft > 0f)
        {
            FinishDash();
        }

        if (_dashLeft <= 0f && !IsDowned && !lost && Controls.DashPressed)
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

            Velocity = Gravity.With(_dashDirection * DashRules.Speed, this, (float)delta);
            MoveAndSlide();
            NetPosition = GlobalPosition;
            NetVelocity = Yaw.Flat(Velocity);
            _shownAimYaw = AimYaw;
            if (_dashLeft <= 0f)
            {
                FinishDash();
            }

            return;
        }

        // A downed body keeps the aim it fell with, and a frozen or stunned one the aim it was caught with.
        bool held = IsDowned || lost;
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
            velocity = move / move.Length() * MoveSpeed.For(_legs, ActiveSkill, _guard.IsUp ? _weapon?.Guard : null) * amount * Vitals.Speed;
        }

        // Frozen or stunned, it is not moved at all: left to the physics, skeletons walking into it would shove it.
        if (!lost)
        {
            Velocity = Gravity.With(velocity, this, (float)delta);
            MoveAndSlide();
        }

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
        if (!held && _swing == null && !_animator.IsAttacking && !_pickUpPending)
        {
            var sets = Sets;
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

        if (!held && Controls.SkillHeld is { } button && !_animator.IsAttacking && CanUse(button))
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
        var sets = Sets;
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
        var (sets, putDown) = TradeRules.Receive(Sets, weapon, slot);
        if (putDown != null)
        {
            GroundWeapons.In(GetTree()).Drop(putDown, GlobalPosition + Yaw.Forward(AimYaw) * Pickups.InFront, AimYaw);
        }

        Carry(sets);
    }

    // The host paid this player for the weapon in that slot: it is gone, and the slot free.
    internal void OnSold(WeaponSlot slot) => Carry(Sets.Without(slot));

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
        ArmourLook.Wear(Skeleton, Look, _armourShown);
        Ghosts.SetFigure(ArmourLook.Dressed(Look, _armourShown));
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
            CharacterRig.HoldOffHand(_offHand, handLook);
            Trail.Retarget();
        }

        if (onBack != _stowed)
        {
            CharacterRig.HoldOnBack(_back, backLook);
        }

        Ghosts.SetWeapons(handLook, backLook);
        _animator.Stance = handLook?.Stance ?? WeaponStance.Unarmed;
        _weapon = inHand;
        _stowed = onBack;
        WeaponsChanged?.Invoke(Sets);
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

        // A thrown spell hits nothing itself: its bolts do, each when it gets there (Bolts).
        while (skill.Projectile != null && _swingLoosed < skill.Projectiles
            && _swingElapsed >= (skill.HitTime + _swingLoosed * skill.VolleyInterval) / _swingSpeed)
        {
            _swingLoosed++;
            Rpc(MethodName.LooseBolt, _boltsThrown++, skill.Id, GlobalPosition + Vector3.Up * Bolts.Height, AimYaw);
        }

        var me = Yaw.ToGround(GlobalPosition);
        if (skill.Projectile == null && skill.Area == null)
        {
            HitTested?.Invoke(skill, Trail.ReachFrom(GlobalPosition));
        }

        // A blow lands on the floor it is struck on, and a spell held on an area on what its caster can see: not on
        // what stands behind a wall.
        var space = GetWorld3D().DirectSpaceState;
        bool Reaches(Node3D body) =>
            Floors.SameLevel(GlobalPosition.Y, body.GlobalPosition.Y) && (skill.Area == null || !Walls.Between(space, GlobalPosition, body.GlobalPosition));

        foreach (var foe in Foes())
        {
            if (!_hitThisSwing.Contains(foe.GetInstanceId())
                && SkillHits.Catches(me, AimYaw, skill, Yaw.ToGround(foe.GlobalPosition), BodySize.Radius) && Reaches(foe))
            {
                DealTo(foe, skill);
                _hitThisSwing.Add(foe.GetInstanceId());
                _swingHits++;
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

    // On the owner, for Bolts: the first body one of its bolts touches on its way from one point to the next takes the
    // skill's damage, as a body caught by a swing does; one that bursts does so there instead, on that body and
    // every other in the burst. False when it touches none.
    public bool StrikeWith(SkillDefinition skill, Vector3 from, Vector3 to)
    {
        var projectile = skill.Projectile!;
        float feet = from.Y - Bolts.Height;
        var touched = Foes()
            .Where(f => Floors.SameLevel(feet, f.GlobalPosition.Y)
                && Projectiles.Hits(Yaw.ToGround(from), Yaw.ToGround(to), Yaw.ToGround(f.GlobalPosition), BodySize.Radius, projectile))
            .ToList();

        // A skeleton it touches is struck before any player, whichever is nearer.
        var struck = touched.OfType<EnemyCharacter>().MinBy(e => e.GlobalPosition.DistanceSquaredTo(from)) as Node3D
            ?? touched.OfType<PlayerCharacter>().MinBy(p => p.GlobalPosition.DistanceSquaredTo(from));
        if (struck == null)
        {
            return false;
        }

        if (skill.BlastRadius > 0f)
        {
            BlastAt(skill, to);
        }
        else
        {
            DealTo(struck, skill);
        }

        return true;
    }

    // On the owner, for Bolts: a ball of the skill bursts at that spot, and every body the burst catches takes the
    // skill's damage. A wall shelters what stands behind it.
    public void BlastAt(SkillDefinition skill, Vector3 at)
    {
        var spot = Yaw.ToGround(at);
        var space = GetWorld3D().DirectSpaceState;
        var feet = at - Vector3.Up * Bolts.Height;
        int caught = 0;
        foreach (var foe in Foes()
            .Where(f => Floors.SameLevel(feet.Y, f.GlobalPosition.Y)
                && Projectiles.Blasts(spot, skill.BlastRadius, Yaw.ToGround(f.GlobalPosition), BodySize.Radius)
                && !Walls.Between(space, feet, f.GlobalPosition)))
        {
            DealTo(foe, skill);
            caught++;
        }

        BlastCaught?.Invoke(skill, caught);
    }

    // Who this player's blows may land on: the skeletons still standing, then in PvP the other players who are up.
    public IEnumerable<Node3D> Foes()
    {
        foreach (var enemy in EnemyCharacter.Standing(GetTree()))
        {
            yield return enemy;
        }

        if (!SessionRules.Pvp)
        {
            yield break;
        }

        foreach (var other in All(GetTree()))
        {
            if (other != this && !other.IsDowned)
            {
                yield return other;
            }
        }
    }

    // On the owner: the host is asked to deal the skill's damage to a foe one of its blows landed on.
    private void DealTo(Node3D foe, SkillDefinition skill)
    {
        if (foe is PlayerCharacter other)
        {
            other.Vitals.RpcId(1, PlayerVitals.MethodName.RequestDamage, skill.Id);
            PlayerHit?.Invoke(other);
        }
        else
        {
            ((EnemyCharacter)foe).RpcId(1, EnemyCharacter.MethodName.RequestDamage, skill.Id);
        }
    }

    public void EndBoltEverywhere(int id, Vector3 at) => Rpc(MethodName.EndBolt, id, at);

    // Every peer flies its own copy of the bolt, from where the caster's machine loosed it.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void LooseBolt(int id, string skillId, Vector3 chest, float yaw)
    {
        if (FindSkill(skillId) is { } skill)
        {
            Bolts.In(GetTree()).Fly(this, id, skill, chest, yaw);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndBolt(int id, Vector3 at) => Bolts.In(GetTree()).End(PeerId, id, at);

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
        CollisionMask = OnFootMask;
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
        GlobalPosition = GlobalPosition.Lerp(NetPosition, Easing.Share(RemoteFollowRate, delta));
        _shownAimYaw = Yaw.Approach(_shownAimYaw, AimYaw, RemoteFollowRate, delta);
    }

    private void Present(float delta)
    {
        ShowWeapons();
        ShowArmour();
        StatusShow.Reflect(IsDowned ? Statuses.None : Vitals.Statuses);
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
        // A spell is no swing of the weapon: it leaves no trail.
        Trail.Recording = ActiveSkill is { Projectile: null, Area: null } swing && (swing.Channeled || WeaponTrail.Shows(swing, _swingShownTime, _swingSpeed));
        ShowMagic();
    }

    // The spell held on an area and the barrier, as this machine sees them: the area where the player is drawn
    // aiming, a round of strikes each loop of the casting clip, and a magic circle under the caster.
    private void ShowMagic()
    {
        _circles.Hold(ActiveSkill is { Area: not null, Element: not null });
        if (ActiveSkill is { Area: { } area, Element: { } element })
        {
            _spellArea.Hold(element, area, _animator.AttackClipLength / _swingSpeed);
            _spellArea.MoveTo(GlobalPosition + Yaw.Forward(_shownAimYaw) * area.Distance);
        }
        else if (_spellArea.Showing)
        {
            _spellArea.Release();
        }

        var barrier = IsGuarding && !IsDowned ? _weapon?.Guard.Barrier : null;
        _barrier.Show(barrier == null ? null : _weapon!.Element, barrier == null ? 0f : (float)Vitals.Barrier / barrier.Strength);
    }

    // By skill id, not button: the swing plays as started even if the weapon change that preceded it hasn't reached
    // this machine yet.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void StartAttack(string skillId)
    {
        if (FindSkill(skillId) is { } skill)
        {
            BeginSwing(skill, CombatTiming.SwingSpeed(skill, AttackSpeed));
            if (Multiplayer.IsServer())
            {
                Vitals.Cast(skill);
            }
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
    public Buyer AsBuyer() => new(Sets, Vitals.Armour);

    // The skill of that id as the weapons this player carries have it: an improved weapon's hits harder than the
    // plain skill the id names. Throws for an id no weapon has.
    public SkillDefinition SkillById(string skillId) => Sets.SkillById(skillId);

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
        _swingLoosed = 0;
        _swingShownTime = 0f;
        _hitThisSwing.Clear();
        if (skill.Projectile != null && skill.Element != null && !Bolts.IsArrow(skill))
        {
            _circles.Throw(AimYaw, (skill.HitTime + (skill.Projectiles - 1) * skill.VolleyInterval) / speed);
        }

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
        _animator.PlayAttack(RigAnimations.GuardFor(_animator.Stance), fullBody: false, AttackSpeed, loop: true);
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
        var config = new SceneReplicationConfig()
            .Sending(SceneReplicationConfig.ReplicationMode.Always, PropertyName.NetPosition, PropertyName.NetVelocity, PropertyName.AimYaw)
            .Sending(SceneReplicationConfig.ReplicationMode.OnChange, PropertyName.WeaponId, PropertyName.StowedWeaponId);
        return new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config, ReplicationInterval = SyncInterval };
    }
}
