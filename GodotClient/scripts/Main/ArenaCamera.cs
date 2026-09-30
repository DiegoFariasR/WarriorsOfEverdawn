using System;
using Godot;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Follows the local player in one of four modes (CameraMode). In the facing modes the mouse is captured and turns
// the view instantly; the character then turns toward the view at its limited rate (Core Turning.MaxRate). In Behind
// vertical mouse motion tilts the camera. The mouse wheel scales the camera distance in every mode.
public partial class ArenaCamera : Camera3D
{
    private const float FollowRate = 12f;
    private const float AngledLookHeight = 1f;
    private const float TopDownHeight = 20f;
    private const float BehindDistance = 7.5f;
    private const float BehindLookHeight = 1.8f;
    private const float DefaultBehindPitch = 22f * Mathf.Pi / 180f;
    private const float MinBehindPitch = 5f * Mathf.Pi / 180f;
    private const float MaxBehindPitch = 60f * Mathf.Pi / 180f;
    private const float MouseTurnPerPixel = 0.005f;
    private const float MousePitchPerPixel = 0.004f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 2f;
    private const float ZoomStep = 1.12f;

    private static readonly Vector3 AngledOffset = new(0f, 16f, 10f);

    private PlayerCharacter? _target;
    private float _behindPitch = DefaultBehindPitch;
    private float _viewYaw;
    private bool _snapNext = true;
    private float _zoom = 1f;

    public event Action<CameraMode>? ModeChanged;

    public CameraMode Mode { get; private set; } = CameraModes.Default;

    // Multiplies every mode's distance; clamped to MinZoom..MaxZoom.
    public float Zoom
    {
        get => _zoom;
        set => _zoom = Mathf.Clamp(value, MinZoom, MaxZoom);
    }

    // Where the view looks in the facing modes: the facing the player asks for. Counter-clockwise positive.
    public float ViewYaw
    {
        get => _viewYaw;
        set => _viewYaw = Angles.Wrap(value);
    }

    public PlayerCharacter? Target
    {
        get => _target;
        set
        {
            _target = value;
            _snapNext = true;
            if (value != null)
            {
                ViewYaw = value.AimYaw;
            }
        }
    }

    public void SetMode(CameraMode mode)
    {
        // Picks up where the character already faces, so entering a facing mode does not spin it round.
        if (mode.FollowsFacing() && !Mode.FollowsFacing() && _target != null)
        {
            ViewYaw = _target.AimYaw;
        }

        Mode = mode;
        Input.MouseMode = mode.FollowsFacing() ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        ModeChanged?.Invoke(mode);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("camera_next"))
        {
            SetMode(Mode.Next());
            GetViewport().SetInputAsHandled();
            return;
        }

        // Checked before the capture click below: a wheel notch is a mouse button press too.
        if (@event.IsActionPressed("zoom_in") || @event.IsActionPressed("zoom_out"))
        {
            Zoom = @event.IsActionPressed("zoom_in") ? Zoom / ZoomStep : Zoom * ZoomStep;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!Mode.FollowsFacing())
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else if (@event is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            ViewYaw -= motion.Relative.X * MouseTurnPerPixel;
            if (Mode == CameraMode.Behind)
            {
                _behindPitch = Mathf.Clamp(_behindPitch + motion.Relative.Y * MousePitchPerPixel, MinBehindPitch, MaxBehindPitch);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_target == null || !IsInstanceValid(_target))
        {
            return;
        }

        var goal = GoalTransform(_target);
        if (_snapNext)
        {
            GlobalTransform = goal;
            _snapNext = false;
            return;
        }

        float blend = 1f - Mathf.Exp(-FollowRate * (float)delta);
        var rotation = GlobalBasis.GetRotationQuaternion().Slerp(goal.Basis.GetRotationQuaternion(), blend);
        GlobalTransform = new Transform3D(new Basis(rotation), GlobalPosition.Lerp(goal.Origin, blend));
    }

    public Transform3D GoalTransform(PlayerCharacter target)
    {
        var at = target.GlobalPosition;
        var facing = Yaw.Forward(_viewYaw);
        return Mode switch
        {
            CameraMode.Angled => LookFrom(at + AngledOffset * _zoom, at + Vector3.Up * AngledLookHeight, Vector3.Up),
            CameraMode.TopDown => LookFrom(at + Vector3.Up * (TopDownHeight * _zoom), at, Vector3.Forward),
            CameraMode.Behind => LookFrom(
                at - facing * (BehindDistance * _zoom * Mathf.Cos(_behindPitch))
                    + Vector3.Up * (BehindLookHeight + BehindDistance * _zoom * Mathf.Sin(_behindPitch)),
                at + Vector3.Up * BehindLookHeight,
                Vector3.Up),
            CameraMode.TopDownTurning => LookFrom(at + Vector3.Up * (TopDownHeight * _zoom), at, facing),
            _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null),
        };
    }

    public Vector3? GroundPointUnderMouse(float groundHeight)
    {
        var mouse = GetViewport().GetMousePosition();
        return new Plane(Vector3.Up, groundHeight).IntersectsRay(ProjectRayOrigin(mouse), ProjectRayNormal(mouse));
    }

    private static Transform3D LookFrom(Vector3 eye, Vector3 target, Vector3 up) =>
        new Transform3D(Basis.Identity, eye).LookingAt(target, up);
}
