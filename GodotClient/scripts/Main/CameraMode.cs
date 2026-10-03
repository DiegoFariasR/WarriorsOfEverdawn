using System;

namespace WarriorsOfEverdawn.Main;

// Cycled with C (controller: Back). Local to each player; nothing about it goes over the network.
// Design: Docs/Design/camera.md.
public enum CameraMode
{
    Angled,
    TopDown,
    Behind,
    TopDownTurning,
}

public static class CameraModes
{
    public const int Count = 4;

    public const CameraMode Default = CameraMode.Behind;

    // The facing modes turn the character with the mouse or right stick and read movement relative to it.
    public static bool FollowsFacing(this CameraMode mode) => mode is CameraMode.Behind or CameraMode.TopDownTurning;

    // The modes that look down on the field draw it without perspective: a thing is the same size on screen
    // wherever it stands, near the camera or far, as in an isometric game. Behind keeps its perspective.
    public static bool IsOrthographic(this CameraMode mode) => mode != CameraMode.Behind;

    public static bool LooksStraightDown(this CameraMode mode) => mode is CameraMode.TopDown or CameraMode.TopDownTurning;

    public static CameraMode Next(this CameraMode mode) => (CameraMode)(((int)mode + 1) % Count);

    public static string Label(this CameraMode mode) => mode switch
    {
        CameraMode.Angled => "1 Angled",
        CameraMode.TopDown => "2 Top-down",
        CameraMode.Behind => "3 Behind (mouse turns, Esc frees the cursor)",
        CameraMode.TopDownTurning => "4 Top-down, turns with you (mouse turns, Esc frees the cursor)",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
