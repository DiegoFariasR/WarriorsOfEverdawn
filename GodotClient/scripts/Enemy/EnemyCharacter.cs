using System;
using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
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

    // A hit on a weakness is written larger and one that is resisted smaller, so what a weapon does to this kind
    // of body shows without a word.
    private const float WeakHitScale = 1.2f;
    private const float ResistedHitScale = 0.85f;
    private const float TickScale = 0.7f;

    // Red, so a skeleton's incoming swing reads as a threat.
    public static readonly Color TrailTint = new(1f, 0.35f, 0.25f);

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

    // Host only: where it rose, which it goes back to once it has lost every player, and whether it is after one.
    private Vector3 _post;
    private bool _engaged;

    // Host only: the bars its hits have built (Docs/Design/damage-types.md).
    private readonly StatusBars _status = new();
    private StatusShow _statusShow = null!;
    private bool _shownLost;
    private float _clock;
    private float _attackElapsed = -1f;
    private bool _attackResolved;
    private float _shownYaw;
    private float _attackShownTime = -1f;

    // Host only: how it was going, which on ice it keeps more of than it wants to.
    private Vector3 _going;

    public static event Action<EnemyCharacter>? Died;

    // Host only: who hit, with what, how much it took, and whether the hit met a weakness (1), a resistance (-1)
    // or neither.
    public static event Action<long, SkillDefinition, int, int>? DamageTaken;

    // Host only: a burn or a wound bit this skeleton for so much.
    public static event Action<EnemyCharacter, DamageType, int>? StatusBit;

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

    // Its statuses (Core's Statuses, as a number): the host's to work out, every machine's to show.
    [Export]
    public int StatusMask { get; set; }

    public Statuses Statuses => (Statuses)StatusMask;

    public StatusShow StatusShow => _statusShow;

    public EnemyDefinition Definition { get; private set; } = null!;

    public bool IsDead { get; private set; }

    public WeaponTrail Trail { get; private set; } = null!;

    public Skeleton3D Skeleton { get; private set; } = null!;

    public HitFlash Flash { get; private set; } = null!;

    // Seconds since the attack shown here began; it keeps counting after the attack ends.
    public float AttackShownTime => _attackShownTime;

    // One of the crypt's guards, not of a wave (EnemyDirector).
    public bool IsGuard => Name.ToString().StartsWith(EnemyDirector.GuardPrefix, StringComparison.Ordinal);

    // The dead among them too, for the CorpseTime they lie before they are freed.
    public static IEnumerable<EnemyCharacter> All(SceneTree tree) => tree.GetNodesInGroup(Group).OfType<EnemyCharacter>();

    public static IEnumerable<EnemyCharacter> Standing(SceneTree tree) => All(tree).Where(e => !e.IsDead);

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
            CollisionMask = CollisionLayers.Solid | CollisionLayers.Players | CollisionLayers.Enemies | CollisionLayers.Ward,
            FloorSnapLength = Gravity.FloorSnap,
            FloorMaxAngle = Gravity.SteepestFloor,
            _health = new Health(definition.MaxHp),
            _post = position,
            _shownYaw = yaw,
            _attackClip = CombatVisuals.ClipFor(definition.Attack),
            _attackFollowUp = followUp == null ? null : new StringName(followUp),
        };
        enemy.AddChild(CharacterRig.CreateCapsule());

        enemy._model = new Node3D { Name = "Model" };
        enemy.AddChild(enemy._model);
        var body = CharacterBody.Build(look.Figure);
        enemy._model.AddChild(body);
        var hand = look.LeftHand
            ? CharacterRig.AttachToLeftHand(body, look.Weapon, look.WeaponRotation)
            : CharacterRig.AttachToHand(body, look.Weapon);
        enemy.Skeleton = CharacterBody.SkeletonOf(body);
        enemy.Trail = new WeaponTrail(hand, TrailTint) { Name = "Trail" };
        enemy.AddChild(enemy.Trail);
        enemy.Flash = new HitFlash(body) { Name = "HitFlash" };
        enemy.AddChild(enemy.Flash);
        enemy._statusShow = new StatusShow { Name = "StatusShow" };
        enemy.AddChild(enemy._statusShow);

        enemy._animation = RigAnimations.AddPlayerTo(body);
        var walk = enemy._animation.GetAnimation(RigAnimations.SkeletonWalk);
        enemy._attackLength = (float)enemy._animation.GetAnimation(enemy._attackClip).Length
            + (followUp == null ? 0f : (float)enemy._animation.GetAnimation(followUp).Length);
        enemy._walkPlayback = definition.MoveSpeed / ClipMotion.GroundSpeed(enemy.Skeleton, RigAnimations.SkeletonWalk, walk, Vector3.Back);

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
            GlobalPosition = GlobalPosition.Lerp(NetPosition, Easing.Share(FollowRate, dt));
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
        AdvanceStatuses(delta);
        if (IsDead)
        {
            return;
        }

        // The chilled are slow to strike again as they are slow to walk.
        _cooldown -= delta * _status.Speed;
        var velocity = Vector3.Zero;
        if (_spawnLeft > 0f)
        {
            _spawnLeft -= delta;
        }
        else if (_status.Lost)
        {
            // Frozen or stunned: whatever it was doing is over.
            _attackElapsed = -1f;
        }
        else if (_attackElapsed >= 0f)
        {
            AdvanceAttack(delta);
        }
        else if (!_stagger.IsStaggered(_clock))
        {
            velocity = Decide(delta);
        }

        // On ice it slides: it gains and loses speed slowly, so it goes on past where it meant to stop.
        if (GroundSurfaces.In(GetTree()).IcyUnder(GlobalPosition))
        {
            var slid = Surfaces.Slide(Yaw.ToGround(_going), Yaw.ToGround(velocity), delta);
            velocity = new Vector3(slid.X, 0f, slid.Y);
        }

        _going = velocity;

        // A body frozen or stunned is not moved at all: left to the physics, the crowd walking into it would
        // shove it along.
        if (!_status.Lost)
        {
            Velocity = Gravity.With(velocity, this, delta);
            MoveAndSlide();
        }

        NetPosition = GlobalPosition;
        NetMoving = velocity.LengthSquared() > MovingThreshold * MovingThreshold;
    }

    // Host only: builds its bars as a hit of this type and power would, with no damage done: a turn on ice
    // (GroundSurfaces), and the self-tests, which freeze and stun on cue what a fight seldom leaves standing long enough.
    public void BuildStatus(DamageType type, int power)
    {
        _status.Build(type, power, damage: 0, _status.Adjusted(Definition.Resistances));
        StatusMask = (int)_status.Active;
    }

    // Host only: its bars wear down, and its burns and wounds bite.
    private void AdvanceStatuses(float delta)
    {
        foreach (var tick in _status.Advance(delta, _health.Current, _status.Adjusted(Definition.Resistances)))
        {
            int taken = _health.TakeDamage(tick.Damage);
            Hp = _health.Current;
            StatusBit?.Invoke(this, tick.Type, taken);
            Rpc(MethodName.ShowTick, taken, (int)tick.Type);
            if (_health.IsDead)
            {
                Rpc(MethodName.Die);
                return;
            }
        }

        StatusMask = (int)_status.Active;
    }

    private Vector3 Decide(float delta)
    {
        var decision = EnemyBrain.Decide(Definition, Yaw.ToGround(GlobalPosition), Yaw.ToGround(_post), LivingPlayers().ToList(), _cooldown <= 0f, _engaged);
        _engaged = decision.Action is not (EnemyAction.Idle or EnemyAction.Return);
        if (decision.Action == EnemyAction.Idle)
        {
            return Vector3.Zero;
        }

        // Its targets are on its own floor, so its way there is sought on that floor.
        var toTarget = new Vector3(decision.Target.Position.X, GlobalPosition.Y, decision.Target.Position.Y) - GlobalPosition;
        float targetYaw = Yaw.Of(toTarget);
        switch (decision.Action)
        {
            case EnemyAction.Chase:
                return WalkTo(GlobalPosition + toTarget, delta);
            case EnemyAction.Return:
                return WalkTo(_post, delta);
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
                return Yaw.Forward(awayYaw) * Definition.MoveSpeed * _status.Speed;
            default:
                NetYaw = Yaw.Approach(NetYaw, targetYaw, TurnRate, delta);
                return Vector3.Zero;
        }
    }

    // Round the walls, not through them: it faces the way it walks.
    private Vector3 WalkTo(Vector3 to, float delta)
    {
        var way = ArenaMap.In(GetTree()).StepToward(this, to);
        if (way == Vector3.Zero)
        {
            return Vector3.Zero;
        }

        NetYaw = Yaw.Approach(NetYaw, Yaw.Of(way), TurnRate, delta);
        return way * Definition.MoveSpeed * _status.Speed;
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
                var aim = target != null ? target.NetPosition - GlobalPosition : Yaw.Forward(NetYaw);
                Arrows.In(GetTree()).Loose(Definition.Attack, AttackDamage(), GlobalPosition + Vector3.Up * Arrows.Height, aim);
                return;
            }

            var me = Yaw.ToGround(GlobalPosition);
            var map = ArenaMap.In(GetTree());
            bool parried = false;
            foreach (var player in PlayerCharacter.All(GetTree()))
            {
                if (MayStrike(player, player.NetPosition, GlobalPosition.Y, map)
                    && MeleeArc.Hits(me, NetYaw, Definition.Attack, Yaw.ToGround(player.NetPosition), BodySize.Radius))
                {
                    parried |= player.Vitals.TakeAttack(AttackDamage(), GlobalPosition, DamageTypes.MaskOf(Definition.Attack.Types)) == GuardOutcome.Parried;
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

    // What its blow deals as it is now: less while it is dizzy.
    private int AttackDamage() => (int)MathF.Round(Definition.Attack.Damage * _status.Dealt(Definition.Attack.Type));

    // The players a skeleton may go for. The crypt's dead keep to the crypt, and the waves to the ground.
    private IEnumerable<EnemyTarget> LivingPlayers()
    {
        var map = ArenaMap.In(GetTree());
        return PlayerCharacter.All(GetTree())
            .Where(p => MayStrike(p, p.GlobalPosition, GlobalPosition.Y, map))
            .Select(p => new EnemyTarget(p.PeerId, Yaw.ToGround(p.GlobalPosition)));
    }

    // A player a skeleton's blow or arrow may strike: up, not inside the allied town, and on the floor that `y` is
    // on. `at` is where the caller takes the player to be: its latest reported position, or where it stands here.
    public static bool MayStrike(PlayerCharacter player, Vector3 at, float y, ArenaMap map) =>
        !player.IsDowned && !map.IsSafe(at) && Floors.SameLevel(y, at.Y);

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

        // Frozen or stunned, it holds the pose it was caught in; as it comes out of it, whatever it was doing is
        // over here too.
        _statusShow.Reflect(Statuses);
        bool lost = Statuses.IsLost();
        _animation.SpeedScale = lost ? 0f : 1f;
        if (lost)
        {
            _shownLost = true;
            _attackShownTime = -1f;
            return;
        }

        if (_shownLost)
        {
            _shownLost = false;
            _oneShot = "";
            _oneShotLeft = 0f;
            _pendingFollowUp = null;
            _loop = "";
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
    private void ShowHit(int amount, bool staggered, int types, int leaning)
    {
        if (SeenFloating)
        {
            FloatingText.Spawn(this, amount.ToString(), DamageTypeColours.Number(types),
                scale: leaning > 0 ? WeakHitScale : leaning < 0 ? ResistedHitScale : 1f);
        }

        Flash.Flash();
        if (staggered && _oneShot != _attackClip && _oneShot != _attackFollowUp && _oneShot != RigAnimations.SkeletonSpawn)
        {
            PlayOneShot(RigAnimations.HitReact, RigAnimations.PlaybackSpeed);
        }
    }

    // A burn's or a wound's bite: a number, smaller than a hit's, and no blink or flinch.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowTick(int amount, int type)
    {
        if (SeenFloating)
        {
            FloatingText.Spawn(this, amount.ToString(), DamageTypeColours.Number((DamageType)type), scale: TickScale);
        }
    }

    // What floats over this skeleton is drawn through floors, so only while it is on the camera's subject's floor,
    // and only near the subject (ArenaMap.ShowsOver).
    private bool SeenFloating => ArenaMap.In(GetTree()).ShowsOver(this);

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
        _animation.SpeedScale = 1f;
        _statusShow.Reflect(Statuses.None);
        GetNode<CollisionShape3D>("Shape").SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _animation.Play(RigAnimations.SkeletonDeath, AnimationBlend, RigAnimations.PlaybackSpeed);
        Died?.Invoke(this);
        if (IsMultiplayerAuthority())
        {
            // Dead, it asks the map its way no more: kept, the map would hold a step for every skeleton that ever walked.
            ArenaMap.In(GetTree()).Forget(this);
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

        long attackerId = Multiplayer.Sender();
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

        TakeHit(attackerId, attacker.Stats, attacker.Vitals.Status, skill, staggers: true);
    }

    // Host only: a turn on burning ground laid by that player, as a hit of its spell's, or with none by the level (a
    // hearth's fire) at the hit's own strength: it does not stagger, or a skeleton in the flames would stagger every
    // turn.
    public void TakeSurfaceHit(PlayerCharacter? caster, SkillDefinition hit)
    {
        if (!IsDead)
        {
            TakeHit(caster?.PeerId ?? SurfaceField.Level, caster?.Stats ?? default, caster?.Vitals.Status, hit, staggers: false);
        }
    }

    private void TakeHit(long attackerId, CharacterStats stats, StatusBars? dealer, SkillDefinition skill, bool staggers)
    {
        // By what this kind of body makes of each type the hit is of, as its bars leave it, and by what the
        // attacker's own bars make of its blows. Then the hit builds this body's bars.
        var resistances = _status.Adjusted(Definition.Resistances);
        int dealt = StatRules.Damage(skill, stats, resistances, dealer);
        int leaning = Math.Sign(dealt - StatRules.Damage(skill, stats, Resistances.None, dealer));
        int taken = _health.TakeDamage(dealt);
        Hp = _health.Current;
        StatRules.Afflict(_status, skill, stats, resistances, dealer);
        StatusMask = (int)_status.Active;
        DamageTaken?.Invoke(attackerId, skill, taken, leaning);
        bool staggered = staggers && !_health.IsDead && _stagger.TryApply(_clock);
        Rpc(MethodName.ShowHit, taken, staggered, DamageTypes.MaskOf(skill.Types), leaning);
        if (_health.IsDead)
        {
            Rpc(MethodName.Die);
        }
    }

    private static MultiplayerSynchronizer CreateSynchronizer()
    {
        var config = new SceneReplicationConfig().Sending(
            SceneReplicationConfig.ReplicationMode.Always,
            PropertyName.NetPosition, PropertyName.NetYaw, PropertyName.NetMoving, PropertyName.Hp, PropertyName.StatusMask);
        return new MultiplayerSynchronizer { Name = "Sync", ReplicationConfig = config, ReplicationInterval = SyncInterval };
    }
}
