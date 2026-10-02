using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// The dash's afterimages: posed copies of the character left behind every Interval while a dash lasts, each
// fading out over FadeTime. They use Everdawn's spirit look (translucent cyan body with a glowing rim, and a depth
// prepass so the body never shows through itself), applied the way its CharacterAssembler.ApplySpiritRecursive does.
// Copies come from a pool built once per character.
public partial class GhostTrail : Node
{
    public const float FadeTime = 0.3f;

    private const float Interval = 0.06f;
    private const int PoolSize = 8;
    private const float StartOpacity = 0.6f;
    private const string SpiritShaderPath = "res://assets/shaders/spirit.gdshader";
    private const string DepthShaderPath = "res://assets/shaders/spirit_depth.gdshader";

    // Everdawn's SpiritDefaultTint.
    private static readonly Color Tint = new(0.55f, 0.95f, 1.0f, 1.0f);

    private readonly Node3D _source;
    private readonly string _modelPath;
    private WeaponLook? _inHand;
    private WeaponLook? _onBack;
    private int _armour;
    private readonly List<Ghost> _pool = new();
    private Skeleton3D _sourceSkeleton = null!;
    private float _emitLeft;
    private float _sinceEmit;

    public GhostTrail(Node3D sourceBody, string modelPath, WeaponLook? inHand, WeaponLook? onBack)
    {
        _source = sourceBody;
        _modelPath = modelPath;
        _inHand = inHand;
        _onBack = onBack;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public GhostTrail()
        : this(null!, "", null, null)
    {
    }

    public int Emitted { get; private set; }

    public int Showing => _pool.Count(g => g.Root.Visible);

    // Oldest visible ghost's age; 0 when none shows.
    public float OldestAge => _pool.Where(g => g.Root.Visible).Select(g => g.Age).DefaultIfEmpty(0f).Max();

    public override void _Ready()
    {
        _sourceSkeleton = _source.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        BuildPool();
    }

    // Ghosts carry the character's weapons too, in hand and on the back, so a change means a new pool.
    public void SetWeapons(WeaponLook? inHand, WeaponLook? onBack)
    {
        if (inHand == _inHand && onBack == _onBack)
        {
            return;
        }

        _inHand = inHand;
        _onBack = onBack;
        Rebuild();
    }

    // Ghosts wear the character's armour too.
    public void SetArmour(int tier)
    {
        if (tier != _armour)
        {
            _armour = tier;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (!IsNodeReady())
        {
            return;
        }

        foreach (var ghost in _pool)
        {
            ghost.Root.QueueFree();
        }

        _pool.Clear();
        BuildPool();
    }

    // Leaves ghosts behind for the next duration seconds, the first one now.
    public void Emit(float duration)
    {
        _emitLeft = duration;
        _sinceEmit = Interval;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        foreach (var ghost in _pool.Where(g => g.Root.Visible))
        {
            ghost.Age += dt;
            if (ghost.Age >= FadeTime)
            {
                ghost.Root.Visible = false;
            }
            else
            {
                ghost.SetFade(StartOpacity * (1f - ghost.Age / FadeTime));
            }
        }

        if (_emitLeft <= 0f)
        {
            return;
        }

        _emitLeft -= dt;
        _sinceEmit += dt;
        if (_sinceEmit >= Interval)
        {
            _sinceEmit = 0f;
            Spawn();
        }
    }

    private void Spawn()
    {
        var ghost = _pool.FirstOrDefault(g => !g.Root.Visible) ?? _pool.OrderByDescending(g => g.Age).First();
        ghost.Root.GlobalTransform = _source.GlobalTransform;
        for (int bone = 0; bone < _sourceSkeleton.GetBoneCount(); bone++)
        {
            ghost.Skeleton.SetBonePosePosition(bone, _sourceSkeleton.GetBonePosePosition(bone));
            ghost.Skeleton.SetBonePoseRotation(bone, _sourceSkeleton.GetBonePoseRotation(bone));
            ghost.Skeleton.SetBonePoseScale(bone, _sourceSkeleton.GetBonePoseScale(bone));
        }

        ghost.Age = 0f;
        ghost.SetFade(StartOpacity);
        ghost.Root.Visible = true;
        Emitted++;
    }

    private void BuildPool()
    {
        var spirit = Assets.Load<Shader>(SpiritShaderPath);
        var depth = Assets.Load<Shader>(DepthShaderPath);
        for (int i = 0; i < PoolSize; i++)
        {
            _pool.Add(BuildGhost(spirit, depth));
        }
    }

    private Ghost BuildGhost(Shader spirit, Shader depth)
    {
        // TopLevel: a ghost stays where it was left while the character moves on.
        var root = new Node3D { Name = "Ghost", TopLevel = true, Visible = false };
        var body = Assets.Instantiate(_modelPath);
        if (_inHand != null)
        {
            CharacterRig.AttachToHand(body, _inHand);
            CharacterRig.AttachOffHand(body, _inHand);
        }

        if (_onBack != null)
        {
            CharacterRig.AttachToBack(body, _onBack);
        }

        CharacterRig.ShrinkHead(body);
        ArmourLook.Wear(body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath), _armour);
        root.AddChild(body);
        AddChild(root);

        var materials = new List<ShaderMaterial>();
        foreach (var mesh in body.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>())
        {
            var source = mesh.GetActiveMaterial(0) as StandardMaterial3D;
            var color = new ShaderMaterial { Shader = spirit, RenderPriority = 0 };
            color.SetShaderParameter("tint", Tint);
            color.SetShaderParameter("albedo_color", source?.AlbedoColor ?? Colors.White);
            if (source?.AlbedoTexture is { } texture)
            {
                color.SetShaderParameter("albedo_tex", texture);
            }

            // The depth-only prepass draws first (lower priority) so the translucent pass keeps only the front-most
            // surface.
            var depthPass = new ShaderMaterial { Shader = depth, RenderPriority = -10, NextPass = color };
            mesh.MaterialOverride = depthPass;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            materials.Add(color);
            materials.Add(depthPass);
        }

        return new Ghost(root, body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath), materials);
    }

    private sealed class Ghost
    {
        private readonly List<ShaderMaterial> _materials;

        public Ghost(Node3D root, Skeleton3D skeleton, List<ShaderMaterial> materials)
        {
            Root = root;
            Skeleton = skeleton;
            _materials = materials;
        }

        public Node3D Root { get; }

        public Skeleton3D Skeleton { get; }

        public float Age { get; set; }

        public void SetFade(float fade)
        {
            foreach (var material in _materials)
            {
                material.SetShaderParameter("fade", fade);
            }
        }
    }
}
