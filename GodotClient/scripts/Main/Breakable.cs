using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Level;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// A crate or a barrel of the level that blows break (Core's Breakables). The host keeps what it has left and works out
// what each hit deals, as against a body with no resistances; every machine shows the hit, a flash and a shake, and
// at nothing left it breaks: its body goes, and it bursts into splinters and dust. It stays broken for the session,
// and a machine that joins later is told what is broken already. The ways are left as they were: they still go round
// where it stood. Design: Docs/Design/level-layouts.md, "Breakables".
public partial class Breakable : StaticBody3D, IStruck
{
    public const string Group = "breakables";

    // A hit knocks it this far aside, and it settles over ShakeTime.
    private const float ShakeDistance = 0.08f;
    private const float ShakeTime = 0.3f;

    private BreakablePiece _piece = null!;
    private Vector3 _rest;
    private Tween? _shake;
    private int _hp = Breakables.Hp;

    public HitFlash Flash { get; private set; } = null!;

    public bool IsBroken { get; private set; }

    // Hits shown on this machine before it broke.
    public int HitsShown { get; private set; }

    // A blow meets it this wide: half its narrower side across the ground.
    public float Radius { get; private set; }

    // On every machine, as one breaks (shown: not as a machine joining hears of one broken before it came).
    public static event Action<Breakable, bool>? Broke;

    public static IEnumerable<Breakable> All(SceneTree tree) => tree.GetNodesInGroup(Group).OfType<Breakable>();

    public static Breakable Create(string name, BreakablePiece piece)
    {
        var breakable = new Breakable
        {
            Name = name,
            Transform = piece.Box,
            CollisionLayer = CollisionLayers.Struck,
            CollisionMask = 0,
            _piece = piece,
            _rest = piece.Piece.Position,
            Radius = Mathf.Min(piece.Size.X, piece.Size.Z) / 2f,
        };
        breakable.AddChild(new CollisionShape3D { Name = "Shape", Shape = new BoxShape3D { Size = piece.Size } });
        breakable.Flash = new HitFlash(piece.Piece) { Name = "HitFlash" };
        breakable.AddChild(breakable.Flash);
        return breakable;
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        AddToGroup(Struck.Group);
    }

    public void AskToStrike(string skillId) => RpcId(1, MethodName.RequestDamage, skillId);

    // From the attacker's machine: one of its hits landed here.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDamage(string skillId)
    {
        if (!Multiplayer.IsServer())
        {
            GD.PushError($"[Breakable {Name}] damage request reached peer {Multiplayer.GetUniqueId()}; only the host works out damage");
            return;
        }

        // Struck by two machines at once: the second hit lands on what is already splinters.
        if (IsBroken)
        {
            return;
        }

        long attackerId = Multiplayer.Sender();
        if (PlayerCharacter.Find(GetTree(), attackerId) is not { } attacker)
        {
            GD.PushWarning($"[Breakable {Name}] hit from peer {attackerId}, who is no longer in the game; ignored");
            return;
        }

        SkillDefinition skill;
        try
        {
            skill = attacker.SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Breakable {Name}] {e.Message} (from peer {attackerId})");
            return;
        }

        _hp -= StatRules.Damage(skill, attacker.Stats, Resistances.None, attacker.Vitals.Status);
        if (_hp > 0)
        {
            Rpc(MethodName.ShowHit);
        }
        else
        {
            Rpc(MethodName.Break, true);
        }
    }

    // Host: a machine that has just joined learns what is broken already.
    public void SendBrokenTo(long peer)
    {
        if (IsBroken)
        {
            RpcId(peer, MethodName.Break, false);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowHit()
    {
        HitsShown++;
        Flash.Flash();
        _shake?.Kill();
        var aside = new Vector3(GD.Randf() - 0.5f, 0f, GD.Randf() - 0.5f).Normalized() * ShakeDistance;
        _piece.Piece.Position = _rest + aside;
        _shake = CreateTween();
        _shake.TweenProperty(_piece.Piece, "position", _rest, ShakeTime).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Break(bool shown)
    {
        if (IsBroken)
        {
            return;
        }

        IsBroken = true;
        _shake?.Kill();
        RemoveFromGroup(Struck.Group);
        GetNode<CollisionShape3D>("Shape").SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        foreach (var mesh in _piece.Meshes)
        {
            mesh.Visible = false;
        }

        if (shown)
        {
            var splinters = new Splinters(Transform, _piece.Size) { Name = $"{Name}Splinters" };
            GetParent().AddChild(splinters);
        }

        Broke?.Invoke(this, shown);
    }
}

// What a crate leaves as it breaks: chips of its wood thrown out and up, tumbling, bouncing once off the floor and
// shrinking away, and a puff of dust. Frees itself when it is over.
public partial class Splinters : Node3D
{
    private const int Chips = 14;
    private const float Lifetime = 1.4f;
    private const float ShrinkFrom = 1f;
    private const float Gravity = 12f;
    private const float Bounce = 0.35f;
    private const float DustTime = 0.6f;

    private static readonly Color DustColour = new(0.62f, 0.56f, 0.48f, 0.7f);

    // Chips are drawn in the crates' wood, a light plank and a dark one, and not in the kit's texture: that is a
    // sheet of colour swatches, and a chip's box laid over the whole of it came out in every colour at once.
    private static readonly StandardMaterial3D[] Wood =
    {
        new() { AlbedoColor = new Color(0.72f, 0.43f, 0.22f), Roughness = 0.9f },
        new() { AlbedoColor = new Color(0.52f, 0.3f, 0.15f), Roughness = 0.9f },
    };

    private readonly Transform3D _box;
    private readonly Vector3 _size;
    private readonly List<(MeshInstance3D Mesh, Vector3 Velocity, Vector3 Spin)> _chips = new();
    private float _age;

    // Splinters on this machine at this moment, for the self-tests: each frees itself when it is over.
    public static int Alive { get; private set; }

    public Splinters(Transform3D box, Vector3 size)
    {
        _box = box;
        _size = size;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public Splinters()
    {
    }

    private float Floor => _box.Origin.Y - _size.Y / 2f;

    public override void _EnterTree() => Alive++;

    public override void _ExitTree() => Alive--;

    public override void _Ready()
    {
        var random = new RandomNumberGenerator();
        for (int i = 0; i < Chips; i++)
        {
            var at = _box * new Vector3(random.RandfRange(-0.4f, 0.4f) * _size.X, random.RandfRange(-0.2f, 0.4f) * _size.Y, random.RandfRange(-0.4f, 0.4f) * _size.Z);
            var outward = Yaw.Flat(at - _box.Origin);
            var chip = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(random.RandfRange(0.15f, 0.4f), random.RandfRange(0.06f, 0.14f), random.RandfRange(0.3f, 0.7f)) },
                MaterialOverride = Wood[i % Wood.Length],
                Position = at,
                Rotation = new Vector3(random.Randf(), random.Randf(), random.Randf()) * Mathf.Tau,
            };
            AddChild(chip);
            var velocity = (outward.LengthSquared() > 1e-4f ? outward.Normalized() : Vector3.Right) * random.RandfRange(1.5f, 3.5f) + Vector3.Up * random.RandfRange(2f, 4.5f);
            _chips.Add((chip, velocity, new Vector3(random.RandfRange(-9f, 9f), random.RandfRange(-9f, 9f), random.RandfRange(-9f, 9f))));
        }

        var dust = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f },
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = DustColour,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = _box.Origin,
            Scale = new Vector3(_size.X, _size.Y * 0.6f, _size.Z) * 0.6f,
        };
        AddChild(dust);
        var puff = dust.CreateTween().SetParallel();
        puff.TweenProperty(dust, "scale", dust.Scale * 2.2f, DustTime).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        puff.TweenProperty(dust.MaterialOverride, "albedo_color:a", 0f, DustTime);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        float shrink = _age < ShrinkFrom ? 1f : 1f - (_age - ShrinkFrom) / (Lifetime - ShrinkFrom);
        for (int i = 0; i < _chips.Count; i++)
        {
            var (mesh, velocity, spin) = _chips[i];
            velocity += Vector3.Down * Gravity * dt;
            var at = mesh.Position + velocity * dt;
            if (at.Y < Floor && velocity.Y < 0f)
            {
                at.Y = Floor;
                velocity = new Vector3(velocity.X * Bounce, -velocity.Y * Bounce, velocity.Z * Bounce);
                spin *= Bounce;
            }

            mesh.Position = at;
            mesh.Rotation += spin * dt;
            mesh.Scale = Vector3.One * Mathf.Max(shrink, 0.01f);
            _chips[i] = (mesh, velocity, spin);
        }
    }
}
