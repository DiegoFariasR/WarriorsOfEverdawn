using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Enemy;

// Host-owned: the host runs its AI, HP and attacks and replicates position, facing and HP. Players report their
// own hits on it; the host applies the damage. Design: Docs/Design/multiplayer.md.
public partial class EnemyCharacter : CharacterBody3D
{
    public const string Group = "enemies";

    private const float SyncInterval = 0.1f;
    private const float FollowRate = 12f;
    private const float TurnRate = 10f;
    public const float CorpseTime = 3f;
    private const float AnimationBlend = 0.2f;
    private const float MovingThreshold = 0.1f;

    // Where an arrow leaves the archer: about the bow's height, a little in front of the chest.
    private const float LooseHeight = 1.2f;
    private const float LooseForward = 0.5f;

    private static readonly Color DamageColor = new(1f, 0.9f, 0.4f);

    // Red, so a skeleton's incoming swing reads as a threat.
    private static readonly Color TrailTint = new(1f, 0.35f, 0.25f);

    private Health _health = null!;
    private Node3D _model = null!;
    private AnimationPlayer _animation = null!;
    private StringName _attackClip = null!;
    private StringName? _attackFollowUp;
    private StringName? _pendingFollowUp;
    private float _attackLength;
    private long _attackTarget;
    private float _walkPlayback;
    private StringName _oneShot = "";
    private StringName _loop = "";
    private float _oneShotLeft;
    private float _spawnLeft;
    private float _cooldown;
    private readonly Stagger _stagger = new();
    private float _clock;
    private float _attackElapsed = -1f;
    private bool _attackResolved;
    private float _shownYaw;
    private float _attackShownTime = -1f;

    public static event Action<EnemyCharacter>? Died;

    public static event Action<long, SkillDefinition, int>? DamageTaken;

    // Host only: a player's guard parried this skeleton's swing.
    public static event Action<EnemyCharacter>? Parried;

    [Export]
    public Vector3 NetPosition { get; set; }

    [Export]
    public float NetYaw { get; set; }

    [Export]
    public bool NetMoving { get; set; }

    [Export]
    public int Hp { get; set; }

    public EnemyDefinition Definition { get; private set; } = null!;

    public bool IsDead { get; private set; }

    public WeaponTrail Trail { get; private set; } = null!;

    public Skeleton3D Skeleton { get; private set; } = null!;

    public HitFlash Flash { get; private set; } = null!;

    // Seconds since the attack shown here began; it keeps counting after the attack ends.
    public float AttackShownTime => _attackShownTime;

    public static EnemyCharacter Create(string name, EnemyDefinition definition, Vector3 position, float yaw)
    {
        var look = CombatVisuals.LookFor(definition);
        string? followUp = CombatVisuals.FollowUpFor(definition.Attack);
        var enemy = new EnemyCharacter
        {
            Name = name,
            Definition = definition,
            Position = position,
            NetPosition = position,
            NetYaw = yaw,
            Hp = definition.MaxHp,
            CollisionLayer = CollisionLayers.Enemies,
            CollisionMask = CollisionLayers.World | CollisionLayers.Players | CollisionLayers.Enemies | CollisionLayers.Ward,
            _health = new Health(definition.MaxHp),
            _shownYaw = yaw,
            _attackClip = CombatVisuals.ClipFor(definition.Attack),
            _attackFollowUp = followUp == null ? null : new StringName(followUp),
        };
        enemy.AddChild(CharacterRig.CreateCapsule());

        enemy._model = new Node3D { Name = "Model" };
        enemy.AddChild(enemy._model);
        var body = Assets.Instantiate(look.Model);
        enemy._model.AddChild(body);
        var hand = look.LeftHand
            ? CharacterRig.AttachToLeftHand(body, look.Weapon, look.WeaponRotation)
            : CharacterRig.AttachToHand(body, look.Weapon);
        CharacterRig.ShrinkHead(body);
        enemy.Skeleton = body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        enemy.Trail = new WeaponTrail(hand, TrailTint) { Name = "Trail" };
        enemy.AddChild(enemy.Trail);
        enemy.Flash = new HitFlash(body) { Name = "HitFlash" };
        enemy.AddChild(enemy.Flash);

        // Libraries go in before the player enters the tree; playing first would crash (Everdawn godot-pitfalls.md).
        enemy._animation = new AnimationPlayer { Name = "AnimationPlayer" };
        RigAnimations.AddTo(enemy._animation);
        body.AddChild(enemy._animation);
        var skeleton = body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        var walk = enemy._animation.GetAnimation(RigAnimations.SkeletonWalk);
        enemy._attackLength = (float)enemy._animation.GetAnimation(enemy._attackClip).Length
            + (followUp == null ? 0f : (float)enemy._animation.GetAnimation(followUp).Length);
        enemy._walkPlayback = definition.MoveSpeed / ClipMotion.GroundSpeed(skeleton, RigAnimations.SkeletonWalk, walk, Vector3.Back);

        enemy.AddChild(CreateSynchronizer());
        return enemy;
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        PlayOneShot(RigAnimations.SkeletonSpawn, RigAnimations.PlaybackSpeed);
        _spawnLeft = _oneShotLeft;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsMultiplayerAuthority())
        {
            Think(dt);
            _shownYaw = NetYaw;
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(NetPosition, 1f - Mathf.Exp(-FollowRate * dt));
            _shownYaw = Yaw.Approach(_shownYaw, NetYaw, FollowRate, dt);
        }

        _model.Rotation = new Vector3(0f, _shownYaw + CharacterRig.ModelYawOffset, 0f);
        Animate(dt);
    }

    private void Think(float delta)
    {
        if (IsDead)
        {
            return;
        }

        _clock += delta;
        _cooldown -= delta;
        var velocity = Vector3.Zero;
        if (_spawnLeft > 0f)
        {
            _spawnLeft -= delta;
        }
        else if (_attackElapsed >= 0f)
        {
            AdvanceAttack(delta);
        }
        else if (!_stagger.IsStaggered(_clock))
        {
            velocity = Decide(delta);
        }

        Velocity = velocity;
        MoveAndSlide();
        NetPosition = GlobalPosition;
        NetMoving = velocity.LengthSquared() > MovingThreshold * MovingThreshold;
    }

    private Vector3 Decide(float delta)
    {
        var decision = EnemyBrain.Decide(Definition, Yaw.ToGround(GlobalPosition), LivingPlayers().ToList(), _cooldown <= 0f);
        if (decision.Action == EnemyAction.Idle)
        {
            return Vector3.Zero;
        }

        var toTarget = new Vector3(decision.Target.Position.X, 0f, decision.Target.Position.Y) - GlobalPosition;
        float targetYaw = Yaw.Of(toTarget);
        switch (decision.Action)
        {
            case EnemyAction.Chase:
                // Round the walls, not through them: it faces the way it walks.
                var way = ArenaMap.In(GetTree()).StepToward(this, GlobalPosition + toTarget);
                if (way == Vector3.Zero)
                {
                    return Vector3.Zero;
                }

                NetYaw = Yaw.Approach(NetYaw, Yaw.Of(way), TurnRate, delta);
                return way * Definition.MoveSpeed;
            case EnemyAction.Attack:
                NetYaw = targetYaw;
                _attackElapsed = 0f;
                _attackResolved = false;
                _attackTarget = decision.Target.Id;
                _cooldown = Definition.AttackCooldown;
                Rpc(MethodName.PlayAttack);
                return Vector3.Zero;
            case EnemyAction.Retreat:
                float awayYaw = Yaw.Of(-toTarget);
                NetYaw = Yaw.Approach(NetYaw, awayYaw, TurnRate, delta);
                return Yaw.Forward(awayYaw) * Definition.MoveSpeed;
            default:
                NetYaw = Yaw.Approach(NetYaw, targetYaw, TurnRate, delta);
                return Vector3.Zero;
        }
    }

    // Host view of player positions decides enemy hits (Docs/Design/multiplayer.md, "Who owns what"). NetPosition is
    // the latest a player reported, ahead of the smoothed position the host shows, so a dash counts as early as the
    // host can know about it.
    private void AdvanceAttack(float delta)
    {
        _attackElapsed += delta;
        var target = PlayerCharacter.Find(GetTree(), _attackTarget);
        if (Definition.Attack.Projectile != null && !_attackResolved && target is { IsDowned: false })
        {
            // An archer follows its target while it draws.
            NetYaw = Yaw.Approach(NetYaw, Yaw.Of(target.NetPosition - GlobalPosition), TurnRate, delta);
        }

        if (!_attackResolved && _attackElapsed >= CombatTiming.HitDelay(Definition.Attack, CombatTiming.BaseAttackSpeed))
        {
            _attackResolved = true;
            if (Definition.Attack.Projectile != null)
            {
                var from = GlobalPosition + Yaw.Forward(NetYaw) * LooseForward + Vector3.Up * LooseHeight;
                var aim = target != null ? target.NetPosition - GlobalPosition : Yaw.Forward(NetYaw);
                Arrows.In(GetTree()).Loose(Definition.Attack, from, aim);
                return;
            }

            var me = Yaw.ToGround(GlobalPosition);
            var map = ArenaMap.In(GetTree());
            bool parried = false;
            foreach (var player in GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>())
            {
                if (!player.IsDowned && !map.IsSafe(player.NetPosition)
                    && MeleeArc.Hits(me, NetYaw, Definition.Attack, Yaw.ToGround(player.NetPosition), BodySize.Radius))
                {
                    parried |= player.Vitals.TakeAttack(Definition.Attack.Damage, GlobalPosition) == GuardOutcome.Parried;
                }
            }

            // A parry throws the swing back: it ends here, and the skeleton reels long enough to be punished.
            if (parried)
            {
                _attackElapsed = -1f;
                _stagger.Force(_clock, Guard.ParryStagger);
                Parried?.Invoke(this);
                Rpc(MethodName.ShowParried);
                return;
            }
        }

        if (_attackElapsed >= _attackLength / CombatTiming.BaseAttackSpeed)
        {
            _attackElapsed = -1f;
        }
    }

    // The players a skeleton may go for: up, and not inside the allied town.
    private IEnumerable<EnemyTarget> LivingPlayers()
    {
        var map = ArenaMap.In(GetTree());
        return GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>()
            .Where(p => !p.IsDowned && !map.IsSafe(p.GlobalPosition))
            .Select(p => new EnemyTarget(p.PeerId, Yaw.ToGround(p.GlobalPosition)));
    }

    private void Animate(float delta)
    {
        if (_attackShownTime >= 0f)
        {
            _attackShownTime += delta;
        }

        // A bow draws no trail: the arrow is the attack.
        Trail.Recording = !IsDead && Definition.Attack.Projectile == null && _attackShownTime >= 0f
            && WeaponTrail.Shows(Definition.Attack, _attackShownTime, CombatTiming.BaseAttackSpeed);
        if (IsDead)
        {
            return;
        }

        if (_oneShotLeft > 0f)
        {
            _oneShotLeft -= delta;
            if (_oneShotLeft > 0f)
            {
                return;
            }

            _oneShot = "";
            if (_pendingFollowUp is { } followUp)
            {
                _pendingFollowUp = null;
                PlayOneShot(followUp, CombatTiming.BaseAttackSpeed);
                return;
            }
        }

        StringName loop = NetMoving ? RigAnimations.SkeletonWalk : RigAnimations.SkeletonIdle;
        if (loop != _loop)
        {
            _animation.Play(loop, AnimationBlend, NetMoving ? _walkPlayback : RigAnimations.PlaybackSpeed);
            _loop = loop;
        }
    }

    private void PlayOneShot(StringName clip, float speed)
    {
        _animation.Play(clip, AnimationBlend, speed);
        _oneShot = clip;
        _oneShotLeft = (float)_animation.GetAnimation(clip).Length / speed;
        _loop = "";
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PlayAttack()
    {
        PlayOneShot(_attackClip, CombatTiming.BaseAttackSpeed);
        _pendingFollowUp = _attackFollowUp;
        _attackShownTime = 0f;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowHit(int amount, bool staggered)
    {
        FloatingText.Spawn(this, amount.ToString(), DamageColor);
        Flash.Flash();
        if (staggered && _oneShot != _attackClip && _oneShot != _attackFollowUp && _oneShot != RigAnimations.SkeletonSpawn)
        {
            PlayOneShot(RigAnimations.HitReact, RigAnimations.PlaybackSpeed);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowParried()
    {
        _attackShownTime = -1f;
        _pendingFollowUp = null;
        PlayOneShot(RigAnimations.HitReact, RigAnimations.PlaybackSpeed);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Die()
    {
        IsDead = true;
        GetNode<CollisionShape3D>("Shape").SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _animation.Play(RigAnimations.SkeletonDeath, AnimationBlend, RigAnimations.PlaybackSpeed);
        Died?.Invoke(this);
        if (IsMultiplayerAuthority())
        {
            GetTree().CreateTimer(CorpseTime).Timeout += () =>
            {
                if (IsInstanceValid(this))
                {
                    QueueFree();
                }
            };
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDamage(string skillId)
    {
        if (!Multiplayer.IsServer())
        {
            GD.PushError($"[Enemy {Name}] damage request reached peer {Multiplayer.GetUniqueId()}; only the host applies damage");
            return;
        }

        if (IsDead)
        {
            return;
        }

        long sender = Multiplayer.GetRemoteSenderId();
        long attackerId = sender == 0 ? Multiplayer.GetUniqueId() : sender;
        var attacker = PlayerCharacter.Find(GetTree(), attackerId);
        if (attacker == null)
        {
            GD.PushWarning($"[Enemy {Name}] hit from peer {attackerId}, who is no longer in the game; ignored");
            return;
        }

        // As the weapon the attacker carries has the skill, by what the host sees it carrying: an improved weapon
        // hits harder.
        SkillDefinition skill;
        try
        {
            skill = attacker.SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Enemy {Name}] {e.Message} (from peer {attackerId})");
            return;
        }

        int taken = _health.TakeDamage(StatRules.Damage(skill, attacker.Stats));
        Hp = _health.Current;
        DamageTaken?.Invoke(attackerId, skill, taken);
        bool staggered = !_health.IsDead && _stagger.TryApply(_clock);
        Rpc(MethodName.ShowHit, taken, staggered);
        if (_health.IsDead)
        {
            Rpc(MethodName.Die);
        }
    }

    private static MultiplayerSynchronizer CreateSynchronizer()
    {
        var config = new SceneReplicationConfig();
        foreach (var property in new[] { PropertyName.NetPosition, PropertyName.NetYaw, PropertyName.NetMoving, PropertyName.Hp })
        {
            var path = new NodePath($".:{property}");
            config.AddProperty(path);
            config.PropertySetSpawn(path, true);
            config.PropertySetReplicationMode(path, SceneReplicationConfig.ReplicationMode.Always);
        }

        return new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config, ReplicationInterval = SyncInterval };
    }
}
