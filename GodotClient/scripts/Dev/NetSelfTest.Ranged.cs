using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [ranged-check]: archers loose arrows that every machine sees, each drawn where it flies, arrows hit players (counted
// on the host, where hits are decided), and none outlives its flight.
public partial class NetSelfTest
{
    // A little past the longest flight, for an arrow ended by the host's message to reach this machine.
    private const float FlightMargin = 0.2f;

    private int _arrowsSeen;
    private int _arrowHits;
    private int _arrowLingering;
    private float _arrowDrawnOff;

    private static float LongestFlight => Projectiles.Arrow.MaxDistance / Projectiles.Arrow.Speed;

    private void PrintRangedCheck(long me) =>
        GD.Print($"[ranged-check] me={me} arrows_seen={_arrowsSeen} arrow_hits={_arrowHits} lingering_frames={_arrowLingering} "
            + $"longest_flight={LongestFlight:F2} drawn_off_max={_arrowDrawnOff:F2}");

    private void OnArrowLoosed() => _arrowsSeen++;

    private void OnArrowHit(PlayerCharacter player, int damage) => _arrowHits++;

    private void MeasureArrows()
    {
        var arrows = GetTree().CurrentScene.GetNodeOrNull<Arrows>(Arrows.NodeName);
        if (arrows == null)
        {
            return;
        }

        if (arrows.OldestFlight > LongestFlight + FlightMargin)
        {
            _arrowLingering++;
        }

        // How far the middle of each arrow's mesh is drawn from where the arrow is.
        foreach (var arrow in arrows.GetChildren().OfType<Node3D>())
        {
            foreach (var mesh in arrow.Meshes())
            {
                var drawn = mesh.GlobalTransform * mesh.GetAabb().GetCenter();
                _arrowDrawnOff = Mathf.Max(_arrowDrawnOff, drawn.DistanceTo(arrow.GlobalPosition));
            }
        }
    }
}
