using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Core.Stats;
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

    public const int Primary = 0;
    public const int Secondary = 1;

    // The two-handed sword: Slice on the primary button, Spin on the secondary.
    public static readonly SkillDefinition[] SkillSet = { Skills.Slice, Skills.Spin };

    private const string ModelPath = "res://assets/characters/Knight.glb";
    private const string WeaponPath = "res://assets/weapons/sword_2handed.glb";
    private const float SyncInterval = 0.05f;
    private const float RemoteFollowRate = 15f;
    private const float BodyTurnRate = 12f;
    private const float MovingThreshold = 0.2f;

    private static readonly Color TrailTint = new(1f, 0.92f, 0.75f);

    private static readonly Dictionary<LegDirection, string> DodgeClips = new()
    {
        [LegDirection.Forward] = RigAnimations.DodgeForward,
        [LegDirection.Backward] = RigAnimations.DodgeBackward,
        [LegDirection.Left] = RigAnimations.DodgeLeft,
        [LegDirection.Right] = RigAnimations.DodgeRight,
    };

    private Node3D _model = null!;
    private CharacterAnimator _animator = null!;
    private LegDirection _legs = LegDirection.Forward;
    private float _bodyYaw;
    private float _shownAimYaw;
    private readonly SkillCooldowns _cooldowns = new(SkillSet.Length);
    private readonly ManaPool _mana = new(StatRules.MaxMana(PlayerRules.KnightStats));
    private readonly HashSet<ulong> _hitThisSwing = new();
    private float _clock;
    private int _activeSkill = -1;
    private int _swingSkill = -1;
    private float _swingElapsed;
    private int _swingHits;
    private float _swingShownTime;
    private bool _reportedMissingControls;
    private readonly ChargeCounter _dodges = new(DodgeRules.Charges, DodgeRules.RechargeTime);
    private float _dodgeLeft;
    private Vector3 _dodgeDirection;
    private Vector3 _dodgeFrom;

    [Export]
    public Vector3 NetPosition { get; set; }

    [Export]
    public Vector3 NetVelocity { get; set; }

    [Export]
    public float AimYaw { get; set; }

    public long PeerId { get; private set; }

    public IPlayerControls? Controls { get; set; }

    public CharacterAnimator Animator => _animator;

    public PlayerVitals Vitals { get; private set; } = null!;

    public Skeleton3D Skeleton { get; private set; } = null!;

    public WeaponTrail Trail { get; private set; } = null!;

    public HitFlash Flash { get; private set; } = null!;

    public GhostTrail Ghosts { get; private set; } = null!;

    public bool IsDodging => _animator.IsDodging;

    public int DodgeCharges => _dodges.Available(_clock);

    public float NextDodgeIn => _dodges.NextChargeIn(_clock);

    // Dodge presses turned down for want of a charge.
    public int DodgesRefused { get; private set; }

    public bool IsDowned { get; private set; }

    public CharacterStats Stats { get; } = PlayerRules.KnightStats;

    public int MaxMana => StatRules.MaxMana(Stats);

    // Owned by the player's own machine, which is where skills start.
    public int Mana => (int)_mana.Current;

    public float AttackSpeed => StatRules.AttackSpeed(Stats);

    // The skill whose swing is playing, on every peer.
    public SkillDefinition? ActiveSkill => _activeSkill >= 0 && _animator.IsAttacking ? SkillSet[_activeSkill] : null;

    public bool IsChanneling => ActiveSkill?.Channeled == true;

    public LegDirection? Legs => NetVelocity.Length() > MovingThreshold ? _legs : null;

    public event Action<SkillDefinition>? AttackStarted;

    // Skill and how many enemies the owner reported hitting with that swing.
    public event Action<SkillDefinition, int>? HitsSent;

    // PvP: the other player this machine reported hitting.
    public event Action<PlayerCharacter>? PlayerHit;

    // On every peer, as a dodge starts: its direction.
    public event Action<Vector3>? DodgeStarted;

    // On the owner, as a dodge ends: where it started and where it ended.
    public event Action<Vector3, Vector3>? DodgeFinished;

    // Each frame a swing tests for hits: the skill and how far the drawn blade tip is from the body centre.
    public event Action<SkillDefinition, float>? HitTested;

    public float CooldownRemaining(int skill) => _cooldowns.Remaining(skill, _clock);

    public bool CanUse(int skill) => _cooldowns.IsReady(skill, _clock) && _mana.CanAfford(SkillSet[skill].ManaCost);

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
        var hand = CharacterRig.AttachToHand(body, WeaponPath);
        CharacterRig.ShrinkHead(body);
        player.Skeleton = body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        player.Trail = new WeaponTrail(hand, TrailTint) { Name = "Trail" };
        player.AddChild(player.Trail);
        player.Flash = new HitFlash(body) { Name = "HitFlash" };
        player.AddChild(player.Flash);
        player.Ghosts = new GhostTrail(body, ModelPath, WeaponPath) { Name = "Ghosts" };
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
        _swingSkill = -1;
        _activeSkill = -1;
        if (_dodgeLeft > 0f)
        {
            FinishDodge();
        }

        _animator.PlayDeath();
    }

    internal void OnRevived(Vector3 at)
    {
        IsDowned = false;
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

        if (_dodgeLeft <= 0f && !IsDowned && Controls.DodgePressed)
        {
            if (_dodges.TryUse(_clock))
            {
                BeginDodge();
            }
            else
            {
                DodgesRefused++;
            }
        }

        if (_dodgeLeft > 0f)
        {
            _dodgeLeft -= (float)delta;
            Velocity = _dodgeDirection * DodgeRules.Speed;
            MoveAndSlide();
            NetPosition = GlobalPosition;
            NetVelocity = Velocity;
            _shownAimYaw = AimYaw;
            if (_dodgeLeft <= 0f)
            {
                FinishDodge();
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
            velocity = move / move.Length() * MoveSpeed.For(_legs, ActiveSkill) * amount;
        }

        Velocity = velocity;
        MoveAndSlide();
        NetPosition = GlobalPosition;
        NetVelocity = velocity;
        _shownAimYaw = AimYaw;

        if (!IsDowned && Controls.SkillHeld is { } skill && !_animator.IsAttacking && CanUse(skill))
        {
            _mana.TrySpend(SkillSet[skill].ManaCost);
            _cooldowns.Start(skill, SkillSet[skill], _clock);
            Rpc(MethodName.StartAttack, skill);
        }
    }

    // The owner decides its own hits, against enemies as it sees them. A single-moment swing tests once at its hit
    // time; a sweep keeps testing until its window closes, hitting each enemy at most once. A channel repeats its
    // window every cycle until its button is let go or the next cycle cannot be paid for.
    private void AdvanceSwing(float delta)
    {
        if (_swingSkill < 0)
        {
            return;
        }

        var skill = SkillSet[_swingSkill];
        if (skill.Channeled && Controls?.SkillHeld != _swingSkill)
        {
            EndChannelCycle(skill);
            return;
        }

        _swingElapsed += delta;
        if (_swingElapsed < CombatTiming.HitDelay(skill, AttackSpeed))
        {
            return;
        }

        var me = Yaw.ToGround(GlobalPosition);
        HitTested?.Invoke(skill, (Yaw.ToGround(Trail.CurrentTip) - me).Length());
        foreach (var node in GetTree().GetNodesInGroup(EnemyCharacter.Group))
        {
            if (node is EnemyCharacter enemy && !enemy.IsDead && !_hitThisSwing.Contains(enemy.GetInstanceId())
                && MeleeArc.Hits(me, AimYaw, skill, Yaw.ToGround(enemy.GlobalPosition), BodySize.Radius))
            {
                enemy.RpcId(1, EnemyCharacter.MethodName.RequestDamage, _swingSkill);
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
                    other.Vitals.RpcId(1, PlayerVitals.MethodName.RequestDamage, _swingSkill);
                    _hitThisSwing.Add(other.GetInstanceId());
                    _swingHits++;
                    PlayerHit?.Invoke(other);
                }
            }
        }

        if (!skill.Channeled)
        {
            if (_swingElapsed >= CombatTiming.HitWindowEnd(skill, AttackSpeed))
            {
                _swingSkill = -1;
                HitsSent?.Invoke(skill, _swingHits);
            }

            return;
        }

        // Each loop of the clip is a cycle: paid for up front, with its own hit window.
        float cycle = _animator.AttackClipLength / AttackSpeed;
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

    // The owner's side of a dodge: charge already spent. Any swing in progress is abandoned, and skeletons stop
    // blocking so the dodge can roll through them.
    private void BeginDodge()
    {
        var direction = DodgeRules.Direction(Yaw.ToGround(Controls!.Move), AimYaw);
        if (_swingSkill >= 0)
        {
            HitsSent?.Invoke(SkillSet[_swingSkill], _swingHits);
            _swingSkill = -1;
        }

        _dodgeLeft = DodgeRules.Duration;
        _dodgeDirection = new Vector3(direction.X, 0f, direction.Y);
        _dodgeFrom = GlobalPosition;
        CollisionMask = CollisionLayers.World;
        Rpc(MethodName.StartDodge, _dodgeDirection);
    }

    private void FinishDodge()
    {
        _dodgeLeft = 0f;
        CollisionMask = CollisionLayers.World | CollisionLayers.Enemies;
        DodgeFinished?.Invoke(_dodgeFrom, GlobalPosition);
    }

    // Every peer: the clip for the dodge's direction relative to the facing, and the ghosts.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void StartDodge(Vector3 direction)
    {
        // The swing stops counting at once (no rooting, no trail); only its animation fades.
        _activeSkill = -1;
        _animator.CancelAttack();
        _animator.PlayDodge(DodgeClips[LegDirectionSelector.Nearest(Yaw.Of(direction) - AimYaw)], DodgeRules.Duration);
        Ghosts.Emit(DodgeRules.Duration);
        DodgeStarted?.Invoke(direction);
    }

    private void EndChannelCycle(SkillDefinition skill)
    {
        HitsSent?.Invoke(skill, _swingHits);
        _swingSkill = -1;
        Rpc(MethodName.EndChannel);
    }

    private void FollowNetwork(float delta)
    {
        GlobalPosition = GlobalPosition.Lerp(NetPosition, 1f - Mathf.Exp(-RemoteFollowRate * delta));
        _shownAimYaw = Yaw.Approach(_shownAimYaw, AimYaw, RemoteFollowRate, delta);
    }

    private void Present(float delta)
    {
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
        Trail.Recording = ActiveSkill is { } swing && (swing.Channeled || WeaponTrail.Shows(swing, _swingShownTime, AttackSpeed));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void StartAttack(int skill)
    {
        if (skill < 0 || skill >= SkillSet.Length)
        {
            GD.PushError($"[Player {Name}] unknown skill index {skill}");
            return;
        }

        var definition = SkillSet[skill];
        _animator.PlayAttack(CombatVisuals.ClipFor(definition), CombatVisuals.IsFullBody(definition), AttackSpeed, definition.Channeled);
        _activeSkill = skill;
        _swingSkill = skill;
        _swingElapsed = 0f;
        _swingHits = 0;
        _swingShownTime = 0f;
        _hitThisSwing.Clear();
        AttackStarted?.Invoke(definition);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndChannel() => _animator.EndLoop();

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

        return new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config, ReplicationInterval = SyncInterval };
    }
}
