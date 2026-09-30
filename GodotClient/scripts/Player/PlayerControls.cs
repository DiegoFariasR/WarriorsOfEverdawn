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

    // Index into PlayerCharacter.SkillSet of the skill the player is holding down, if any.
    int? SkillHeld { get; }

    // Pressed this frame; the dodge goes the way Move points, or where the character faces.
    bool DodgePressed { get; }

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

    public bool DodgePressed { get; private set; }

    // Input as Input.GetVector gives it: x right, y down (so W is -y).
    public static Vector3 MoveFor(CameraMode mode, Vector2 input, float facing) =>
        mode.FollowsFacing() ? Yaw.FromFacing(-input.Y, input.X, facing) : new Vector3(input.X, 0f, input.Y);

    public void Update(PlayerCharacter player, double delta)
    {
        var move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        DodgePressed = Input.IsActionJustPressed("dodge");
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
    private const float DodgeCloserThan = 3f;
    private const float DodgeEvery = 2.2f;
    private const float BurstGap = 0.35f;
    private const int BurstPresses = 3;

    private readonly float _phase;
    private float _time;
    private float _burstStart = float.NegativeInfinity;
    private int _burstPressed = BurstPresses;
    private int _dodgesTried;

    public BotControls(long peerId)
    {
        _phase = peerId % 8 * Mathf.Tau / 8f;
    }

    public Vector3 Move { get; private set; }

    public float? AimYaw { get; private set; }

    public int? SkillHeld { get; private set; }

    public bool DodgePressed { get; private set; }

    public void Update(PlayerCharacter player, double delta)
    {
        _time += (float)delta;
        DodgePressed = false;
        bool standing = _time % CycleLength >= StandStillFrom;
        var enemy = NearestHostile(player);
        var toEnemy = enemy == null ? Vector3.Zero : enemy.GlobalPosition - player.GlobalPosition;
        toEnemy.Y = 0f;
        float distance = toEnemy.Length();
        if (enemy == null || distance > EngageRadius)
        {
            var heading = enemy == null ? Yaw.Forward(_phase + _time * MoveTurnRate) : toEnemy / distance;
            Move = standing ? Vector3.Zero : heading;
            AimYaw = _phase + _time * AimTurnRate;
            SkillHeld = _time % AttackInterval < 0.1f ? PlayerCharacter.Primary : null;
            return;
        }

        var inward = distance > 0.01f ? toEnemy / distance : Yaw.Forward(_phase);
        var around = new Vector3(-inward.Z, 0f, inward.X);
        var keepDistance = inward * Mathf.Clamp(distance - OrbitDistance, -1f, 1f);
        Move = standing ? Vector3.Zero : (around + keepDistance).Normalized() * OrbitSpeed;
        AimYaw = Yaw.Of(toEnemy);

        // Every so often when a hostile is close, three quick presses: more than the charges, so the last is refused.
        // They alternate between a sideways dodge and one with no direction, which goes where the bot faces, through
        // the hostile.
        if (distance < DodgeCloserThan && _time - _burstStart > DodgeEvery)
        {
            _burstStart = _time;
            _burstPressed = 0;
        }

        if (_burstPressed < BurstPresses && _time - _burstStart >= _burstPressed * BurstGap)
        {
            DodgePressed = true;
            Move = _dodgesTried++ % 2 == 0 ? around : Vector3.Zero;
            _burstPressed++;
            SkillHeld = null;
            return;
        }
        var spin = PlayerCharacter.SkillSet[PlayerCharacter.Secondary];
        int inSpinReach = Hostiles(player).Count(h => h.GlobalPosition.DistanceTo(player.GlobalPosition) - BodySize.Radius <= spin.Range);
        if (inSpinReach >= 1 && (player.IsChanneling || player.CanUse(PlayerCharacter.Secondary)))
        {
            SkillHeld = PlayerCharacter.Secondary;
        }
        else
        {
            var slice = PlayerCharacter.SkillSet[PlayerCharacter.Primary];
            SkillHeld = distance - BodySize.Radius <= slice.Range ? PlayerCharacter.Primary : null;
        }
    }

    private static Node3D? NearestHostile(PlayerCharacter player) =>
        Hostiles(player).OrderBy(h => h.GlobalPosition.DistanceSquaredTo(player.GlobalPosition)).FirstOrDefault();

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
