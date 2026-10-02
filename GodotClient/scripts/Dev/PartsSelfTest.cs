using System;
using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Characters;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// Attached with --parts-check. Every part of the catalogue is put on a figure in its slot, in place of the
// player's own or over it, and must come out as a mesh under the skeleton whose skin resolves, sized as its slot
// and the bone that carries it say. Then figures are drawn at random from every pool and dressed, each in turn on
// the one skeleton. Prints [parts-check] lines and quits (exit 1 on failure).
public partial class PartsSelfTest : Node
{
    private const int RolledPerPool = 200;

    // A scale read back off a transform is this near the one it was given.
    private const float ScaleSlack = 0.001f;

    // Scenes are freed a frame after they are let go; the check starts once the arena has settled.
    private const int SettleFrames = 3;

    private int _frames;

    public override void _Process(double delta)
    {
        if (++_frames < SettleFrames)
        {
            return;
        }

        SetProcess(false);
        bool passed = false;
        try
        {
            passed = Run();
        }
        catch (Exception problem)
        {
            GD.PrintErr($"[parts-check] stopped by {problem.GetType().Name}: {problem.Message}");
        }

        GD.Print($"[parts-check] {(passed ? "passed" : "FAILED")}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private bool Run()
    {
        var catalog = CharacterBody.Catalog;
        var body = CharacterBody.Build(PlayerCharacter.Look);
        AddChild(body);
        var skeleton = CharacterBody.SkeletonOf(body);

        var failures = new List<string>();
        foreach (var part in catalog.Parts.OrderBy(p => p.Stem, StringComparer.Ordinal))
        {
            try
            {
                CharacterBody.Dress(skeleton, Wearing(PlayerCharacter.Look, part));
                if (Fault(skeleton, part) is { } fault)
                {
                    failures.Add($"{part.Stem}: {fault}");
                }
            }
            catch (Exception problem)
            {
                failures.Add($"{part.Stem}: {problem.Message}");
            }
        }

        foreach (string failure in failures)
        {
            GD.Print($"[parts-check] FAIL {failure}");
        }

        GD.Print($"[parts-check] parts={catalog.Parts.Count} origins={catalog.Origins.Count} failed={failures.Count}");

        bool pools = true;
        foreach (var pool in LookPools.All)
        {
            var keys = new HashSet<string>();
            string? fault = null;
            for (int seed = 0; seed < RolledPerPool && fault == null; seed++)
            {
                try
                {
                    var look = LookRandomizer.Roll(catalog, pool, seed);
                    CharacterBody.Dress(skeleton, look);
                    keys.Add(look.Key);
                    if (CharacterBody.Worn(skeleton) != look.Key)
                    {
                        fault = $"seed {seed}: the figure does not say it wears what it was dressed in";
                    }
                }
                catch (Exception problem)
                {
                    fault = $"seed {seed}: {problem.Message}";
                }
            }

            // A pool that rolls the same figure over and over is no pool.
            bool ok = fault == null && keys.Count > RolledPerPool / 2;
            pools &= ok;
            GD.Print($"[parts-check] pool={pool.Name} rolled={RolledPerPool} different={keys.Count} {(ok ? "ok" : $"FAIL {fault ?? "too few different figures"}")}");
        }

        return failures.Count == 0 && pools;
    }

    // The player's look with the part in its slot: in place of the look's own, or added to what it wears.
    private static CharacterLook Wearing(CharacterLook look, PartEntry part) => part.Slot switch
    {
        CharacterSlot.Head => look with { Head = part.Stem },
        CharacterSlot.Face => look with { Face = part.Stem },
        CharacterSlot.Body => look with { Body = part.Stem },
        CharacterSlot.ArmLeft => look with { ArmLeft = part.Stem },
        CharacterSlot.ArmRight => look with { ArmRight = part.Stem },
        CharacterSlot.LegLeft => look with { LegLeft = part.Stem },
        CharacterSlot.LegRight => look with { LegRight = part.Stem },
        _ => look.Adding(part.Stem),
    };

    private static string? Fault(Skeleton3D skeleton, PartEntry part)
    {
        var worn = CharacterBody.PartsOn(skeleton).Where(p => p.Part == part.Stem).ToList();
        if (worn.Count == 0)
        {
            return "not on the figure";
        }

        foreach (var (mesh, _, slot) in worn)
        {
            if (mesh.Mesh == null || mesh.Mesh.GetSurfaceCount() == 0)
            {
                return "a mesh with no surface";
            }

            if (mesh.Skin is { } skin)
            {
                for (int bind = 0; bind < skin.GetBindCount(); bind++)
                {
                    if (skeleton.FindBone(skin.GetBindName(bind)) < 0)
                    {
                        return $"its skin is bound to '{skin.GetBindName(bind)}', which the rig does not have";
                    }
                }
            }
            else if (mesh.GetParent() is not BoneAttachment3D { BoneIdx: >= 0 })
            {
                return "not skinned, and on no bone";
            }

            float expected = slot is CharacterSlot.Head or CharacterSlot.Face ? CharacterBody.HeadScale : part.OnHead ? CharacterBody.HeadgearScale : 1f;
            float scale = mesh.Transform.Basis.Scale.Y;
            if (Mathf.Abs(scale - expected) > ScaleSlack)
            {
                return $"drawn at {scale:F3} of its size, expected {expected:F3}";
            }
        }

        return null;
    }
}
