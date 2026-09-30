using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Everdawn's damage blink: a red, noisy glow laid over the whole character for a quarter second on each hit. Its
// sword-hit (Slash) flash, carried over: the elemental_fire overlay at the outer layer's tint, noise and speed, the
// dissolve eased in and out over the same timings. MaterialOverlay covers every surface, skinned ones included.
public partial class HitFlash : Node
{
    public const float Duration = 0.25f;

    private const string ShaderPath = "res://assets/shaders/elemental_fire.gdshader";
    private const float PeakDissolve = 0.56f;
    private const float FadeIn = 0.055f;
    private const float FadeOut = 0.07f;

    private const string Dissolve = "shader_parameter/dissolve";

    private static readonly Color Tint = new(1f, 0.3f, 0.3f);

    private readonly ShaderMaterial _material;
    private readonly GeometryInstance3D[] _meshes;
    private Tween? _tween;
    private float _age = float.PositiveInfinity;

    public HitFlash(Node3D body)
    {
        _meshes = body.FindChildren("*", nameof(GeometryInstance3D), recursive: true, owned: false)
            .OfType<GeometryInstance3D>()
            .ToArray();
        _material = new ShaderMaterial { Shader = Assets.Load<Shader>(ShaderPath) };
        _material.SetShaderParameter("tint", Tint);
        _material.SetShaderParameter("noise_scale", 6f);
        _material.SetShaderParameter("speed", 10f);
        _material.SetShaderParameter("dissolve", 0f);
        _material.SetShaderParameter("time_offset", GD.Randf() * 97f);
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public HitFlash()
    {
        _meshes = Array.Empty<GeometryInstance3D>();
        _material = new ShaderMaterial();
    }

    public static event Action<HitFlash>? Started;

    public bool Showing => _meshes.Length > 0 && _meshes[0].MaterialOverlay != null;

    // Seconds since the latest flash began.
    public float Age => _age;

    public void Flash()
    {
        _tween?.Kill();
        foreach (var mesh in _meshes)
        {
            mesh.MaterialOverlay = _material;
        }

        _age = 0f;
        _material.SetShaderParameter("dissolve", 0f);
        _tween = CreateTween();
        _tween.TweenProperty(_material, Dissolve, PeakDissolve, FadeIn)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _tween.TweenInterval(Duration - FadeIn - FadeOut);
        _tween.TweenProperty(_material, Dissolve, 0f, FadeOut)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _tween.TweenCallback(Callable.From(Clear));
        Started?.Invoke(this);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
    }

    private void Clear()
    {
        foreach (var mesh in _meshes)
        {
            mesh.MaterialOverlay = null;
        }
    }
}
