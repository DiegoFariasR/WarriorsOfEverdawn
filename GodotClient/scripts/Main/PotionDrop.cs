using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// How a charge of the health potion looks where it lies: a red potion floating off the ground and turning, in a soft
// red light. Looks only: Loot decides when it falls and who gets it.
public partial class PotionDrop : Node3D
{
    private const string ModelPath = "res://assets/props/potion_medium_red.glb";

    // The bottle is half a metre tall as modelled: drawn this much bigger, it is seen among the coins.
    private const float DrawnLarger = 1.6f;

    private const float FloatHeight = 0.35f;
    private const float BobHeight = 0.08f;
    private const float BobRate = 2.4f;
    private const float SpinRate = 1.3f;

    private static readonly Color Glow = new(1f, 0.3f, 0.3f);
    private const float LightEnergy = 1.2f;
    private const float LightRange = 3f;

    private Node3D _bottle = null!;
    private float _age;

    public static PotionDrop Create()
    {
        var drop = new PotionDrop();
        drop._bottle = Assets.InstantiateAtOrigin(ModelPath);
        drop._bottle.Scale = Vector3.One * DrawnLarger;
        drop._bottle.Position = Vector3.Up * FloatHeight;
        drop.AddChild(drop._bottle);
        drop.AddChild(new OmniLight3D { LightColor = Glow, LightEnergy = LightEnergy, OmniRange = LightRange, Position = Vector3.Up * (FloatHeight + 0.4f) });
        return drop;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        _bottle.Position = Vector3.Up * (FloatHeight + BobHeight * Mathf.Sin(_age * BobRate));
        _bottle.Rotation = new Vector3(0f, _age * SpinRate, 0f);
    }
}
