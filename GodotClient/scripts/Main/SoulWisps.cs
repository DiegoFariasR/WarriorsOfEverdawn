using System;
using System.Collections.Generic;
using EverdawnKit.Magic;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// A monster's souls, seen: as it dies a wisp rises out of it, hangs a moment, and flies to this machine's player,
// leaving a trail, and is taken in with a spark. Every player gets every soul (Loot), so on every machine the wisp
// flies to that machine's own player. Looks only: the host has counted the souls as the monster died.
// Design: Docs/Design/combat.md, "Gold, souls and orbs".
public partial class SoulWisps : Node3D
{
    public const string NodeName = "SoulWisps";

    // It rises out of the body from about its chest.
    private const float RisesFrom = 1f;

    private readonly List<SoulWisp> _flying = new();

    // On this machine, as a wisp reaches its player.
    public event Action? Absorbed;

    public int Risen { get; private set; }

    public int Taken { get; private set; }

    public int Flying => _flying.Count;

    public static SoulWisps In(SceneTree tree) => tree.CurrentScene.GetNode<SoulWisps>(NodeName);

    public override void _EnterTree() => EnemyCharacter.Died += OnEnemyDied;

    public override void _ExitTree() => EnemyCharacter.Died -= OnEnemyDied;

    private void OnEnemyDied(EnemyCharacter enemy)
    {
        if (PlayerCharacter.Local(GetTree()) is not { } player)
        {
            return;
        }

        for (int i = 0; i < enemy.Definition.Souls; i++)
        {
            var wisp = new SoulWisp(player, this, enemy.GlobalPosition + Vector3.Up * RisesFrom, Risen) { Name = $"Wisp{Risen}" };
            wisp.Arrived += () => OnArrived(wisp);
            _flying.Add(wisp);
            AddChild(wisp);
            Risen++;
        }
    }

    private void OnArrived(SoulWisp wisp)
    {
        _flying.Remove(wisp);
        Taken++;
        Absorbed?.Invoke();
    }
}

// One soul on its way: it rises in a slow turn, hangs, then flies to the player's chest along a curve, faster as it
// goes, the curve bending to one side and over. Its trail is left in the node that made it, which stays.
public partial class SoulWisp : Node3D
{
    private const float RiseTime = 0.7f;
    private const float RiseHeight = 1f;
    private const float RiseTurns = 1.25f;
    private const float RiseSwirl = 0.25f;
    private const float HangTime = 0.15f;

    // It flies this fast on the whole, but takes no less and no more than these, however near or far the player.
    private const float FlySpeed = 14f;
    private const float FlyShortest = 0.6f;
    private const float FlyLongest = 1.8f;

    // The curve's bend: up, and to one side by up to this much.
    private const float BendUp = 1.5f;
    private const float BendAside = 2f;

    private const float CoreRadius = 0.11f;
    private const float HaloRadius = 0.3f;
    private const float TrailRadius = 0.09f;

    private readonly PlayerCharacter _player;
    private readonly Node _trailInto;
    private readonly Vector3 _from;
    private readonly float _side;
    private readonly float _turnFrom;
    private Vector3 _flightStart;
    private Vector3 _bend;
    private float _flightTime;
    private float _age;

    public SoulWisp(PlayerCharacter player, Node trailInto, Vector3 from, int number)
    {
        _player = player;
        _trailInto = trailInto;
        _from = from;

        // Wisps of one death, and of deaths together, do not all bend the same way.
        var random = new RandomNumberGenerator { Seed = (ulong)(number * 7919 + 17) };
        _side = random.RandfRange(-1f, 1f);
        _turnFrom = random.Randf() * Mathf.Tau;
    }

    public event Action? Arrived;

    public override void _Ready()
    {
        Position = _from;
        AddChild(new MeshInstance3D
        {
            Name = "Core",
            Mesh = new SphereMesh { Radius = CoreRadius, Height = CoreRadius * 2f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = MagicMaterials.Flare(UiTheme.SoulText, alpha: 1f, energy: 4f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        AddChild(new MeshInstance3D
        {
            Name = "Halo",
            Mesh = new SphereMesh { Radius = HaloRadius, Height = HaloRadius * 2f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = MagicMaterials.Flare(UiTheme.SoulText, alpha: 0.3f, energy: 1.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        AddChild(new OmniLight3D { Name = "Light", LightColor = UiTheme.SoulText, LightEnergy = 1.2f, OmniRange = 3f, ShadowEnabled = false });
        AddChild(new SpellTrail { Name = "Trail", Colour = UiTheme.SoulText, Radius = TrailRadius, Into = _trailInto });
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player))
        {
            // The player it was going to has left: it goes out where it is.
            Arrived?.Invoke();
            QueueFree();
            return;
        }

        _age += (float)delta;
        if (_age < RiseTime)
        {
            float t = _age / RiseTime;
            float angle = _turnFrom + t * RiseTurns * Mathf.Tau;
            float risen = 1f - (1f - t) * (1f - t) * (1f - t);
            Position = _from + Vector3.Up * (RiseHeight * risen) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (RiseSwirl * (1f - t));
            return;
        }

        if (_age < RiseTime + HangTime)
        {
            return;
        }

        var target = _player.GlobalPosition + Vector3.Up * Bolts.Height;
        if (_flightTime <= 0f)
        {
            _flightStart = GlobalPosition;
            _flightTime = Mathf.Clamp(_flightStart.DistanceTo(target) / FlySpeed, FlyShortest, FlyLongest);
            var flat = Yaw.Flat(target - _flightStart);
            var across = flat.LengthSquared() > 1e-4f ? flat.Cross(Vector3.Up).Normalized() : Vector3.Right;
            _bend = Vector3.Up * BendUp + across * (BendAside * _side);
        }

        float s = Mathf.Min(1f, (_age - RiseTime - HangTime) / _flightTime);
        float eased = s * s;
        var control = (_flightStart + target) / 2f + _bend;
        GlobalPosition = _flightStart.Lerp(control, eased).Lerp(control.Lerp(target, eased), eased);
        if (s >= 1f)
        {
            ParticleBurst.Sparks(_player, Vector3.Up * Bolts.Height, UiTheme.SoulText, count: 14, lifetime: 0.4f, speed: 2.5f);
            Arrived?.Invoke();
            QueueFree();
        }
    }
}
