using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// The training dummy in the town: whatever a player's blows, bolts and bursts land on, they land on it too. The host
// works out what a hit deals, as on a skeleton with no resistances, and every machine shows it: the number, a flash
// and a shake. It keeps nothing, no HP and no statuses, so it takes every blow and never falls.
public partial class TrainingDummy : StaticBody3D, IStruck
{
    public const string Group = "training_dummy";

    private const string ModelPath = "res://assets/props/Trainingdummy.glb";

    // The model's box: arms out, as tall as a door.
    private static readonly Vector3 Box = new(1.86f, 2.5f, 0.88f);

    // A hit rocks it back this far, and it swings to rest over ShakeTime.
    private const float ShakeAngle = 0.14f;
    private const float ShakeTime = 0.5f;

    // It stands taller than a body: its numbers start this much higher up the screen than a body's.
    private const float NumbersAbove = 0.4f;

    private Node3D _model = null!;
    private Tween? _shake;

    public HitFlash Flash { get; private set; } = null!;

    // Hits shown on this machine since it stood up.
    public int HitsShown { get; private set; }

    public static IEnumerable<TrainingDummy> All(SceneTree tree) => tree.GetNodesInGroup(Group).OfType<TrainingDummy>();

    public static TrainingDummy Create(string name, Vector3 position, float yaw)
    {
        var dummy = new TrainingDummy
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0f, yaw, 0f),
            CollisionLayer = CollisionLayers.Struck,
            CollisionMask = 0,
        };
        dummy._model = Assets.Instantiate(ModelPath);
        dummy._model.Name = "Model";
        dummy.AddChild(dummy._model);
        dummy.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Box }, Position = Vector3.Up * Box.Y / 2f });
        dummy.Flash = new HitFlash(dummy._model) { Name = "HitFlash" };
        dummy.AddChild(dummy.Flash);
        return dummy;
    }

    public float Radius => BodySize.Radius;

    public override void _Ready()
    {
        AddToGroup(Group);
        AddToGroup(Struck.Group);
    }

    public void AskToStrike(string skillId) => RpcId(1, MethodName.RequestDamage, skillId);

    // From the attacker's machine: one of its hits landed here.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDamage(string skillId)
    {
        if (!Multiplayer.IsServer())
        {
            GD.PushError($"[Dummy {Name}] damage request reached peer {Multiplayer.GetUniqueId()}; only the host works out damage");
            return;
        }

        long attackerId = Multiplayer.Sender();
        if (PlayerCharacter.Find(GetTree(), attackerId) is not { } attacker)
        {
            GD.PushWarning($"[Dummy {Name}] hit from peer {attackerId}, who is no longer in the game; ignored");
            return;
        }

        SkillDefinition skill;
        try
        {
            skill = attacker.SkillById(skillId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Dummy {Name}] {e.Message} (from peer {attackerId})");
            return;
        }

        int dealt = StatRules.Damage(skill, attacker.Stats, Resistances.None, attacker.Vitals.Status);
        Rpc(MethodName.ShowHit, dealt, DamageTypes.MaskOf(skill.Types));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowHit(int amount, int types)
    {
        HitsShown++;
        if (ArenaMap.In(GetTree()).OnSubjectsFloor(GlobalPosition))
        {
            FloatingText.Spawn(this, amount.ToString(), DamageTypeColours.Number(types), above: NumbersAbove);
        }

        Flash.Flash();
        _shake?.Kill();
        _model.Rotation = new Vector3(-ShakeAngle, 0f, 0f);
        _shake = CreateTween();
        _shake.TweenProperty(_model, "rotation:x", 0f, ShakeTime).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }
}
