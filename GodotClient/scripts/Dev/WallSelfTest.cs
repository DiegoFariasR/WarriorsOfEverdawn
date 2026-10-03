using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// Attached with --wall-check. Everything that flies is loosed at the back wall of the enemy fortress's courtyard,
// from five away and from as close as a body can stand, where it leaves the hand already inside the wall: a
// staff's bolt and a volley's dart, a wand's ball, a bow's arrow and a skeleton's arrow. Each must end at the
// wall's face, not past it. Then the line of sight magic goes by is read across the wall and along it. Prints
// [wall-check] lines and quits (exit 1 on failure).
public partial class WallSelfTest : Node
{
    private const float Afar = 5f;

    // A body's centre this far from the wall's face: touching it.
    private const float Touching = BodySize.Radius + 0.05f;

    // How near the wall's face a flight must end.
    private const float AtTheWall = 0.3f;

    // Bodies and shapes reach the physics space a step after they are made.
    private const int SettleSteps = 5;
    private const float GivenUpAfter = 3f;

    private readonly Queue<(string Name, Action<Vector3> Loose, float From)> _cases = new();
    private Bolts _bolts = null!;
    private Arrows _arrows = null!;
    private Vector3 _lane;
    private float _face;
    private int _steps;
    private bool _started;
    private bool _passed = true;
    private string? _flying;
    private float _flyingFor;
    private Vector3? _ended;
    private int _nextId = 9000;

    public override void _Ready()
    {
        Bolts.Ended += OnBoltEnded;
        Arrows.Ended += OnArrowEnded;
    }

    public override void _ExitTree()
    {
        Bolts.Ended -= OnBoltEnded;
        Arrows.Ended -= OnArrowEnded;
    }

    public override void _PhysicsProcess(double delta)
    {
        var player = PlayerCharacter.Local(GetTree());
        if (player == null || ++_steps < SettleSteps)
        {
            return;
        }

        if (!_started)
        {
            _started = true;
            Begin(player);
            return;
        }

        if (_flying != null)
        {
            _flyingFor += (float)delta;
            if (_ended is { } at)
            {
                // Past the wall is further towards -Z than its face.
                bool ok = Mathf.Abs(at.Z - _face) <= AtTheWall;
                Report(_flying, $"ended_at_z={at.Z:F2} wall_face_z={_face:F2} past_the_face={_face - at.Z:F2}", ok);
                _flying = null;
            }
            else if (_flyingFor > GivenUpAfter)
            {
                Report(_flying, $"never ended in {GivenUpAfter:F0} s", ok: false);
                _flying = null;
            }

            return;
        }

        if (_cases.Count > 0)
        {
            var (name, loose, from) = _cases.Dequeue();
            _flying = name;
            _flyingFor = 0f;
            _ended = null;
            loose(new Vector3(_lane.X, 0f, _face + from));
            return;
        }

        CheckSight();
        GD.Print($"[wall-check] {(_passed ? "passed" : "FAILED")}");
        GetTree().Quit(_passed ? 0 : 1);
        SetPhysicsProcess(false);
    }

    private void Begin(PlayerCharacter player)
    {
        _bolts = Bolts.In(GetTree());
        _arrows = Arrows.In(GetTree());
        var court = ArenaMap.In(GetTree()).FortressCourtyard;
        _face = court.Min.Y;
        if (FindLane(court) is not { } lane)
        {
            Report("lane", $"no stretch of the back wall at z={_face:F2} with a clear run of {Afar} to it", ok: false);
            GetTree().Quit(1);
            SetPhysicsProcess(false);
            return;
        }

        _lane = lane;
        GD.Print($"[wall-check] lane_x={lane.X:F1} wall_face_z={_face:F2}");
        float towardWall = Yaw.Of(Vector3.Forward);
        foreach (var (label, skill) in new[]
        {
            ("bolt", Weapons.Staff(Element.Fire).Primary),
            ("dart", Weapons.Staff(Element.Water).Primary),
            ("ball", Weapons.Wand(Element.Fire).Secondary),
            ("bow-arrow", Weapons.Bow.Primary),
        })
        {
            foreach (var (where, from) in new[] { ("afar", Afar), ("touching", Touching) })
            {
                _cases.Enqueue(($"{label}-{where}", feet => _bolts.Fly(player, _nextId++, skill, feet + Vector3.Up * Bolts.Height, towardWall), from));
            }
        }

        var shot = Enemies.SkeletonArcher.Attack;
        foreach (var (where, from) in new[] { ("afar", Afar), ("touching", Touching) })
        {
            _cases.Enqueue(($"skeleton-arrow-{where}", feet => _arrows.Loose(shot, shot.Damage, feet + Vector3.Up * Arrows.Height, Vector3.Forward), from));
        }
    }

    // A stretch of the back wall with nothing standing between it and a spot Afar from it, at the heights things
    // fly at. Found with the test's own rays; what is asserted afterwards is where the flights end.
    private Vector3? FindLane(Core.Level.LayoutArea court)
    {
        var space = GetViewport().World3D.DirectSpaceState;
        for (float x = court.Min.X + 2f; x <= court.Max.X - 2f; x += 0.5f)
        {
            bool clear = new[] { Bolts.Height, Arrows.Height, Walls.SightHeight }.All(height =>
            {
                var from = new Vector3(x, height, _face + Afar + 1f);
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + Vector3.Forward * (Afar + 3f), CollisionLayers.World));
                return hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Z - _face) < 0.05f;
            });
            if (clear)
            {
                return new Vector3(x, 0f, _face);
            }
        }

        return null;
    }

    // Magic goes by line of sight (Walls.Between): none across the wall, a clear one along the lane.
    private void CheckSight()
    {
        var space = GetViewport().World3D.DirectSpaceState;
        var inside = new Vector3(_lane.X, 0f, _face + 2f);
        var beyond = new Vector3(_lane.X, 0f, _face - 3f);
        var along = new Vector3(_lane.X, 0f, _face + Afar);
        bool across = Walls.Between(space, inside, beyond);
        bool within = Walls.Between(space, inside, along);
        Report("sight", $"wall_between_across={across} wall_between_within={within}", across && !within);
    }

    private void OnBoltEnded(SkillDefinition skill, Vector3 at) => _ended ??= at;

    private void OnArrowEnded(Vector3 at) => _ended ??= at;

    private void Report(string name, string what, bool ok)
    {
        _passed &= ok;
        GD.Print($"[wall-check] case={name} {what} {(ok ? "ok" : "FAIL")}");
    }
}
