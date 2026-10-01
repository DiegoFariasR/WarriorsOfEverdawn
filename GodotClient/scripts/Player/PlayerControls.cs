using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Player;

public interface IPlayerControls
{
    // Ground-plane world direction, length 0..1.
    Vector3 Move { get; }

    // Where the player wants to face. Null when there is no aim input this frame; the character then turns to
    // where it moves.
    float? AimYaw { get; }

    // The button (WeaponDefinition.PrimaryButton or SecondaryButton) the player is holding down, if any.
    int? SkillHeld { get; }

    // Pressed this frame; the dash goes the way Move points, or where the character faces.
    bool DashPressed { get; }

    // Pressed this frame: change the weapon in hand to the next one (skipping the one on the back).
    bool WeaponNextPressed { get; }

    // Pressed this frame: swap the weapon in hand with the one on the back.
    bool SwapSetsPressed { get; }

    // Held: the weapon's guard is up for as long as it is held (and the character is free to guard).
    bool GuardHeld { get; }

    // Pressed this frame: let go of the weapon in hand.
    bool DropPressed { get; }

    // Pressed this frame: take the nearest weapon in reach off the ground, if a slot is free.
    bool PickUpPressed { get; }

    void Update(PlayerCharacter player, double delta);
}

// World modes (Angled, TopDown): WASD moves across the screen, the mouse aims until a stick is touched, the
// controller aims until the mouse moves again. Facing modes (Behind, TopDownTurning): the mouse or right stick
// turns the view, the character aims along it, and WASD moves relative to it. In every mode the aim here is where
// the player wants to face; PlayerCharacter turns toward it at a limited rate. Design: Docs/Design/camera.md.
public sealed class HumanControls : IPlayerControls
{
    private const float StickDeadzone = 0.35f;
    private const float StickTurnRate = 3f;
    private const int Pad = 0;

    private readonly ArenaCamera _camera;
    private bool _usingPad;
    private Vector2 _lastMouse;

    public HumanControls(ArenaCamera camera)
    {
        _camera = camera;
    }

    public Vector3 Move { get; private set; }

    public float? AimYaw { get; private set; }

    public int? SkillHeld { get; private set; }

    public bool DashPressed { get; private set; }

    public bool WeaponNextPressed { get; private set; }

    public bool SwapSetsPressed { get; private set; }

    public bool GuardHeld { get; private set; }

    public bool DropPressed { get; private set; }

    public bool PickUpPressed { get; private set; }

    // Input as Input.GetVector gives it: x right, y down (so W is -y).
    public static Vector3 MoveFor(CameraMode mode, Vector2 input, float facing) =>
        mode.FollowsFacing() ? Yaw.FromFacing(-input.Y, input.X, facing) : new Vector3(input.X, 0f, input.Y);

    public void Update(PlayerCharacter player, double delta)
    {
        var move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        DashPressed = Input.IsActionJustPressed("dash");
        WeaponNextPressed = Input.IsActionJustPressed("weapon_next");
        SwapSetsPressed = Input.IsActionJustPressed("weapon_swap");
        GuardHeld = Input.IsActionPressed("guard");
        DropPressed = Input.IsActionJustPressed("weapon_drop");
        PickUpPressed = Input.IsActionJustPressed("weapon_pickup");
        // The secondary wins when both are held, so holding the primary never blocks a Spin.
        SkillHeld = Input.IsActionPressed("attack_secondary") ? PlayerCharacter.Secondary
            : Input.IsActionPressed("attack") ? PlayerCharacter.Primary
            : null;
        if (_camera.Mode.FollowsFacing())
        {
            _camera.ViewYaw -= Input.GetAxis("aim_left", "aim_right") * StickTurnRate * (float)delta;
            AimYaw = _camera.ViewYaw;
            Move = MoveFor(_camera.Mode, move, _camera.ViewYaw);
            return;
        }

        Move = MoveFor(_camera.Mode, move, 0f);

        var aimStick = Input.GetVector("aim_left", "aim_right", "aim_up", "aim_down");
        var moveStick = new Vector2(Input.GetJoyAxis(Pad, JoyAxis.LeftX), Input.GetJoyAxis(Pad, JoyAxis.LeftY));
        var mouse = player.GetViewport().GetMousePosition();
        if (aimStick.Length() > StickDeadzone || moveStick.Length() > StickDeadzone)
        {
            _usingPad = true;
        }
        else if (mouse != _lastMouse)
        {
            _usingPad = false;
        }

        _lastMouse = mouse;

        if (_usingPad)
        {
            AimYaw = aimStick.Length() > StickDeadzone ? Yaw.Of(new Vector3(aimStick.X, 0f, aimStick.Y)) : null;
            return;
        }

        var target = _camera.GroundPointUnderMouse(player.GlobalPosition.Y);
        var toTarget = target - player.GlobalPosition;
        AimYaw = toTarget is { } offset && offset.LengthSquared() > 0.01f ? Yaw.Of(offset) : null;
    }
}

// Scripted input for headless multiplayer tests. It runs toward the nearest skeleton, or nearest hostile player in
// PvP (or in a circle when there is none) while the aim turns at a different rate, so every leg direction comes up. Close to a skeleton it circles
// it slowly enough to be caught, aims at it and swings when in reach, holding Spin instead while anything is in its
// reach and mana lasts.
// Regular stops exercise the full-body attack.
public sealed class BotControls : IPlayerControls
{
    private const float MoveTurnRate = 0.6f;
    private const float AimTurnRate = -0.9f;
    private const float CycleLength = 5f;
    private const float StandStillFrom = 3.8f;
    private const float AttackInterval = 1.25f;
    private const float EngageRadius = 7f;
    private const float OrbitDistance = 1.6f;
    private const float OrbitSpeed = 0.4f;
    private const float DashCloserThan = 3f;
    private const float DashEvery = 2.2f;
    private const float BurstGap = 0.35f;
    private const int BurstPresses = 3;

    // One burst in this many throws its dash through the hostile with the attack button, which makes it a lunge;
    // the first burst does. On every burst the bots killed with little else, and the other checks went hungry.
    private const int LungeEveryBursts = 3;

    // Swaps start this long into the bot's run, late enough that every player in a test session has joined, and
    // repeat every SwapEvery; each lasts SwapFor before swapping back. None start after swapUntil, so a bot ends a
    // test session holding the weapons it started with, which are the ones the session's checks read.
    private const float FirstSwapAt = 9f;
    private const float SwapEvery = 20f;
    private const float SwapFor = 1.5f;

    // DropEvery from FirstDropAt the bot lets go of its weapon, stands by it unarmed for UnarmedFor and takes it back,
    // so the self-tests see weapons put down and taken up on every machine. None start within DropMargin of swapUntil:
    // an unarmed bot is easily taken down, and it has to get back up, walk back and take its weapon before the
    // session's checks read what it holds.
    private const float FirstDropAt = 16f;
    private const float DropEvery = 40f;
    private const float UnarmedFor = 1.5f;
    private const float DropMargin = 12f;

    // How far off an arm's length from its weapon the bot is content to stand before taking it back.
    private const float StandOff = 0.15f;

    // Further than this from its weapon (having got back up in the town, say) the bot takes the way round the walls.
    private const float FarFromWeapon = 2.5f;

    // Every DefendEvery seconds with a skeleton close, the bot spends DefendFor on defence: it stops attacking (a guard
    // only goes up between swings) and keeps its guard up, facing a skeleton, for the rest of the phase. Parries are
    // the harder catch and skeletons are only close for a few seconds a wave, so the first phases go to them: the bot
    // waits for a swing that would reach it and raises its guard GuardReactAt into it, so the blow lands early enough
    // to be parried (the chop lands 0.3 s in); only a swing caught while the guard can go up that frame counts, since
    // raised any later it would meet the blow past the parry window. Every BraceEvery-th phase is braced instead: the
    // guard goes up at once, so blows meet it late and are blocked. A bot that has met no skeleton for two phases'
    // wait braces anyway, so every bot shows its guard.
    private const float DefendEvery = 4.5f;
    private const float DefendFor = 2f;
    private const int BraceEvery = 3;
    private const float GuardReactAt = 0.14f;
    private const float GuardReach = 1f;

    // A spin, once started, is held at least this long even if its targets die or step away, so every weapon's spin
    // plays whole revolutions at full strength for the self-tests to measure.
    private const float MinSpinHold = 0.8f;

    // The bot starts its Spin this far before anything is in its reach, as a player wading in would. Waiting for
    // reach, the shortest Spin (the staff's) hardly ever started: its targets died or moved on first. Not for archers:
    // they back away nearly as fast as a spinning bot glides, so it would chase them spinning at the air.
    private const float SpinLead = 1.2f;

    private const float CrowdPenalty = 3f;

    // Every SpinDrillEvery the bot spins for SpinDrillFor with nothing in reach, as it swings at the air: no bot then
    // goes a session without a Spin to measure, however the fight falls out.
    private const float SpinDrillEvery = 7f;
    private const float SpinDrillFor = 1f;

    // And every LungeDrillEvery it lunges at the air along its aim, for the same reason: the lunges of its bursts
    // depend on how many bursts the fight gives it, and one thrown sideways off a swing in progress reads no reach.
    private const float FirstLungeDrillAt = 4f;
    private const float LungeDrillEvery = 9f;

    // The way to a hostile and the straight line to it agree this closely (cosine) when nothing stands between.
    private const float InSightAlignment = 0.94f;

    private readonly float _phase;
    private float _time;
    private float _burstStart = float.NegativeInfinity;
    private int _burstPressed = BurstPresses;
    private int _dashesTried;
    private int _bursts;
    private float _spinUntil;
    private float _defendUntil = float.NegativeInfinity;
    private float _lastDefence = float.NegativeInfinity;
    private EnemyCharacter? _guardingAgainst;
    private bool _guarding;
    private int _defences;
    private bool _bracing;
    private readonly float _swapUntil;
    private WeaponDefinition? _home;
    private float _nextDropAt = FirstDropAt;
    private float _nextLungeDrill = FirstLungeDrillAt;
    private float _unarmedSince = float.NaN;

    public BotControls(long peerId, float swapUntil)
    {
        _phase = peerId % 8 * Mathf.Tau / 8f;
        _swapUntil = swapUntil;
    }

    public Vector3 Move { get; private set; }

    public float? AimYaw { get; private set; }

    public int? SkillHeld { get; private set; }

    public bool DashPressed { get; private set; }

    // A bot keeps the weapons it started with (--weapon, --back-weapon)...
    public bool WeaponNextPressed => false;

    // ...but swaps them for a moment every so often, so the self-tests see swaps reach every machine.
    public bool SwapSetsPressed { get; private set; }

    public bool GuardHeld { get; private set; }

    public bool DropPressed { get; private set; }

    public bool PickUpPressed { get; private set; }

    // What the bot is about, for the self-tests' [bot-check].
    public string Activity { get; private set; } = "starting";

    public void Update(PlayerCharacter player, double delta)
    {
        _time += (float)delta;
        _home ??= player.Weapon;
        DropPressed = false;
        PickUpPressed = false;
        if (player.Weapon is not { } weapon)
        {
            Retrieve(player);
            return;
        }

        _unarmedSince = float.NaN;

        // Pressed until it takes: a swap waits for any swing in progress to end.
        float intoSwap = (_time - FirstSwapAt) % SwapEvery;
        bool wantSwapped = _time >= FirstSwapAt && intoSwap < SwapFor && _time - intoSwap + SwapFor < _swapUntil;
        SwapSetsPressed = wantSwapped == (weapon == _home);

        // A Spin just started is held for its minimum whatever else is due, so each one is long enough to measure.
        bool spinHeld = player.IsChanneling && _time < _spinUntil;

        // Pressed until it takes, like a swap, and with the weapon it started with in hand.
        DropPressed = _time >= _nextDropAt && _time < _swapUntil - DropMargin && weapon == _home && !SwapSetsPressed && !spinHeld;
        Decide(player, weapon);

        if (spinHeld && !DashPressed)
        {
            SkillHeld = PlayerCharacter.Secondary;
        }

        // A weapon is only let go or swapped between swings, and a Spin held on in a crowd lasts as long as its mana:
        // without letting go, a bot whose swap back was due would fight on with the weapon from its back.
        if (DropPressed || (SwapSetsPressed && !spinHeld))
        {
            SkillHeld = null;
        }

        bool skeletonNear = MeleeSkeletons(player).Any();
        bool overdue = _time - Mathf.Max(_lastDefence, 0f) > DefendEvery * 2f;
        if (_time >= _defendUntil && _time - _lastDefence > DefendEvery && (skeletonNear || overdue) && !SwapSetsPressed && !DropPressed && !spinHeld)
        {
            _lastDefence = _time;
            _defendUntil = _time + DefendFor;
            _bracing = !skeletonNear || _defences % BraceEvery == BraceEvery - 1;
            _defences++;
            _guardingAgainst = null;
            _guarding = false;
        }

        // A raised guard holds off a swap or a drop, and the swap's window is short: one that is due ends the phase.
        if (SwapSetsPressed || DropPressed)
        {
            _defendUntil = Mathf.Min(_defendUntil, _time);
        }

        if (_time >= _defendUntil)
        {
            GuardHeld = false;
            return;
        }

        // A dash keeps its attack button, so lunges still come up while defending.
        if (!DashPressed)
        {
            SkillHeld = null;
        }

        if (!_guarding && player.CanRaiseGuard)
        {
            _guardingAgainst = _bracing ? NearestMeleeSkeleton(player) : MeleeSkeletons(player).FirstOrDefault(e => SwingReaches(e, player, GuardReactAt));
            _guarding = _bracing || _guardingAgainst != null;
        }

        // Waiting to parry, it turns to a skeleton from the moment its swing starts, or else to the nearest: turning
        // only once the guard is up takes longer than the swing gives.
        var facing = _guardingAgainst
            ?? MeleeSkeletons(player).FirstOrDefault(e => SwingReaches(e, player, 0f))
            ?? NearestMeleeSkeleton(player);
        if (GodotObject.IsInstanceValid(facing) && !facing!.IsDead)
        {
            AimYaw = Yaw.Of(facing.GlobalPosition - player.GlobalPosition);
        }

        GuardHeld = _guarding;
    }

    // Unarmed: the bot stays by the weapon it let go of, then takes it back.
    private void Retrieve(PlayerCharacter player)
    {
        if (float.IsNaN(_unarmedSince))
        {
            _unarmedSince = _time;
            _nextDropAt = _time + DropEvery;
        }

        Activity = "retrieving";
        SkillHeld = null;
        DashPressed = false;
        GuardHeld = false;
        SwapSetsPressed = false;

        // Not on the ground yet while the host is still putting it down.
        var mine = GroundWeapons.In(player.GetTree()).Items
            .Where(i => i.Weapon == _home)
            .OrderBy(i => i.Position.DistanceSquaredTo(player.GlobalPosition))
            .FirstOrDefault();
        if (mine == null)
        {
            Move = Vector3.Zero;
            AimYaw = null;
            return;
        }

        // Picking up takes the weapon in front, and bots fighting side by side let go of theirs within a step of each
        // other: the bot stands facing its own, an arm's length off, and asks only while that is the one it would get.
        var toWeapon = mine.Position - player.GlobalPosition;
        toWeapon.Y = 0f;
        float distance = toWeapon.Length();
        var toward = distance > 0.01f ? toWeapon / distance : Yaw.Forward(player.AimYaw);
        AimYaw = Yaw.Of(toward);
        Move = distance > FarFromWeapon ? ArenaMap.In(player.GetTree()).StepToward(player, mine.Position)
            : distance > Pickups.InFront + StandOff ? toward
            : distance < Pickups.InFront - StandOff ? -toward
            : Vector3.Zero;
        bool wouldTakeMine = GroundWeapons.In(player.GetTree()).InReachOf(player.GlobalPosition, player.AimYaw)?.Id == mine.Id;
        PickUpPressed = wouldTakeMine && _time - _unarmedSince >= UnarmedFor;
    }

    private void Decide(PlayerCharacter player, WeaponDefinition weapon)
    {
        DashPressed = false;
        bool standing = _time % CycleLength >= StandStillFrom;
        var enemy = TargetFor(player);
        var toEnemy = enemy == null ? Vector3.Zero : enemy.GlobalPosition - player.GlobalPosition;
        toEnemy.Y = 0f;
        float distance = toEnemy.Length();
        if (enemy == null || distance > EngageRadius)
        {
            // With nothing in range to fight the bot goes for the nearest gold, so little is left lying; else for the
            // nearest hostile, by the way round the walls, into the enemy fortress if that is where it is.
            var toGold = TowardGold(player);
            var heading = toGold
                ?? (enemy == null ? Yaw.Forward(_phase + _time * MoveTurnRate) : ArenaMap.In(player.GetTree()).StepToward(player, enemy.GlobalPosition));
            Activity = toGold != null ? "to-gold" : enemy == null ? "wandering" : $"to-{Kind(enemy)}";
            Move = standing ? Vector3.Zero : heading;
            AimYaw = _phase + _time * AimTurnRate;
            SkillHeld = SpinDrill(player) ? PlayerCharacter.Secondary : _time % AttackInterval < 0.1f ? PlayerCharacter.Primary : null;

            // A burst that has started is finished even with its hostile gone, so its last press still meets the
            // charge limit; a weapon that reaches far often kills the hostile mid-burst.
            if (BurstPressDue())
            {
                PressDash(player, sideways: new Vector3(-heading.Z, 0f, heading.X));
            }
            else if (_time >= _nextLungeDrill && SkillHeld != PlayerCharacter.Secondary && !player.IsChanneling)
            {
                _nextLungeDrill = _time + LungeDrillEvery;
                DashPressed = true;
                Move = Vector3.Zero;
                SkillHeld = PlayerCharacter.Primary;
            }

            return;
        }

        var inward = distance > 0.01f ? toEnemy / distance : Yaw.Forward(_phase);
        var around = new Vector3(-inward.Z, 0f, inward.X);
        var keepDistance = inward * Mathf.Clamp(distance - OrbitDistance, -1f, 1f);

        // The bot runs at the hostile by the way round the walls, and circles it slowly only once it is close: an
        // archer backs away faster than the circling closes in, and held the bot off for the whole of its volley.
        var way = ArenaMap.In(player.GetTree()).StepToward(player, enemy.GlobalPosition);
        bool close = distance <= OrbitDistance + 1f;
        bool inSight = close || way.Dot(inward) > InSightAlignment;
        Move = !close ? way : standing ? Vector3.Zero : (around + keepDistance).Normalized() * OrbitSpeed;
        Activity = $"{(inSight ? "fighting" : "to")}-{Kind(enemy)}";
        AimYaw = Yaw.Of(toEnemy);

        // Every so often when a hostile is close, three quick presses: more than the charges, so the last is refused.
        // They alternate between a sideways dash and one with no direction, which goes where the bot faces, through
        // the hostile.
        // Not while defending: mid-dash, the guard cannot go up into a swing.
        if (distance < DashCloserThan && _time - _burstStart > DashEvery && _time >= _defendUntil)
        {
            _burstStart = _time;
            _burstPressed = 0;
            _bursts++;
        }

        if (BurstPressDue())
        {
            PressDash(player, around);
            return;
        }

        var spin = weapon.Secondary;
        int inSpinReach = Hostiles(player).Count(h =>
            h.GlobalPosition.DistanceTo(player.GlobalPosition) - BodySize.Radius <= spin.Range + (KeepsAway(h) ? 0f : SpinLead));
        if (inSpinReach >= 1 && (player.IsChanneling || player.CanUse(PlayerCharacter.Secondary)))
        {
            if (!player.IsChanneling)
            {
                _spinUntil = _time + MinSpinHold;
            }

            SkillHeld = PlayerCharacter.Secondary;
        }
        else
        {
            // A primary that outreaches the Spin (the spear's thrust) would kill before anything came into the Spin's
            // reach, so while it can pay for a Spin the bot saves its swing for one. With nothing in reach it swings at
            // the air, as it does with no hostile about: the reach checks read every swing, hit or not, and a bot
            // that prefers its Spin would otherwise swing a handful of times a session.
            bool inPrimaryRange = distance - BodySize.Radius <= weapon.Primary.Range;
            bool savesForSpin = weapon.Primary.Range > spin.Range && player.CanUse(PlayerCharacter.Secondary);
            bool swings = inPrimaryRange ? !savesForSpin : _time % AttackInterval < 0.1f;
            SkillHeld = !inPrimaryRange && SpinDrill(player) ? PlayerCharacter.Secondary : swings ? PlayerCharacter.Primary : null;
        }
    }

    private static Vector3? TowardGold(PlayerCharacter player)
    {
        var nearest = Loot.In(player.GetTree()).Piles
            .OrderBy(p => p.Position.DistanceSquaredTo(player.GlobalPosition))
            .FirstOrDefault();
        if (nearest == null)
        {
            return null;
        }

        var way = ArenaMap.In(player.GetTree()).StepToward(player, nearest.Position);
        return way == Vector3.Zero ? null : way;
    }

    private bool SpinDrill(PlayerCharacter player) =>
        _time % SpinDrillEvery < SpinDrillFor && (player.IsChanneling || player.CanUse(PlayerCharacter.Secondary));

    private bool BurstPressDue() => _burstPressed < BurstPresses && _time - _burstStart >= _burstPressed * BurstGap;

    // One press of a burst: sideways and where the bot faces by turns.
    private void PressDash(PlayerCharacter player, Vector3 sideways)
    {
        DashPressed = true;
        bool goesSideways = _dashesTried++ % 2 == 0;
        Move = goesSideways ? sideways : Vector3.Zero;
        _burstPressed++;

        // A Spin in progress stays held, so it carries on through the dash.
        bool lunges = !goesSideways && _bursts % LungeEveryBursts == 1;
        SkillHeld = lunges ? PlayerCharacter.Primary : player.IsChanneling ? PlayerCharacter.Secondary : null;
    }

    private static string Kind(Node3D hostile) => hostile is EnemyCharacter skeleton ? skeleton.Definition.Id : "player";

    private static bool KeepsAway(Node3D hostile) => hostile is EnemyCharacter { Definition.KeepAway: > 0f };

    private static IEnumerable<EnemyCharacter> MeleeSkeletons(PlayerCharacter player) =>
        player.GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e =>
            !e.IsDead && e.Definition.Attack.Projectile == null
            && e.GlobalPosition.DistanceTo(player.GlobalPosition) - BodySize.Radius <= e.Definition.Attack.Range + GuardReach);

    private static EnemyCharacter? NearestMeleeSkeleton(PlayerCharacter player) =>
        MeleeSkeletons(player).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(player.GlobalPosition)).FirstOrDefault();

    // At least `from` into a swing, and still early enough to parry it, whose arc would reach the player, with
    // GuardReach to spare for either moving.
    private static bool SwingReaches(EnemyCharacter skeleton, PlayerCharacter player, float from) =>
        skeleton.AttackShownTime >= from && skeleton.AttackShownTime < GuardReactAt + 0.1f
        && MeleeArc.Hits(Yaw.ToGround(skeleton.GlobalPosition), skeleton.NetYaw, skeleton.Definition.Attack,
            Yaw.ToGround(player.GlobalPosition), BodySize.Radius + GuardReach);

    // The nearest hostile, counting one as CrowdPenalty further off for every other player already closer to it. All
    // going for the plain nearest, the bots piled onto one skeleton and the weapons that reach furthest killed it
    // before the shortest got a swing in.
    private static Node3D? TargetFor(PlayerCharacter player)
    {
        var others = player.GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>()
            .Where(p => p != player && !p.IsDowned)
            .ToList();
        return Hostiles(player)
            .OrderBy(h =>
            {
                float mine = h.GlobalPosition.DistanceTo(player.GlobalPosition);
                return mine + CrowdPenalty * others.Count(o => o != h && o.GlobalPosition.DistanceTo(h.GlobalPosition) < mine);
            })
            .FirstOrDefault();
    }

    // Living skeletons, and in PvP every other player who is up.
    private static IEnumerable<Node3D> Hostiles(PlayerCharacter player)
    {
        var tree = player.GetTree();
        IEnumerable<Node3D> skeletons = tree.GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => !e.IsDead);
        if (!SessionRules.Pvp)
        {
            return skeletons;
        }

        return skeletons.Concat(tree.GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>()
            .Where(p => p != player && !p.IsDowned));
    }
}
