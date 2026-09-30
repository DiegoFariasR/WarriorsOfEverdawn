using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Enemy;

// Host only. Sends a wave when the previous one is cleared. Wave size does not scale with player count yet
// (open question in Docs/Design/multiplayer.md).
public partial class EnemyDirector : Node
{
    private const float FirstWaveDelay = 1f;
    private const float WaveGap = 3f;
    private const float SpawnRadiusMin = 11f;
    private const float SpawnRadiusMax = 15f;

    private static readonly EnemyDefinition[] Wave =
    {
        Enemies.SkeletonMinion, Enemies.SkeletonMinion, Enemies.SkeletonMinion,
        Enemies.SkeletonWarrior, Enemies.SkeletonWarrior,
    };

    private readonly MultiplayerSpawner _spawner;
    private readonly Node3D _enemies;
    private readonly RandomNumberGenerator _random = new();
    private float _untilNextWave = FirstWaveDelay;
    private int _wavesSent;
    private int _nextId;

    public EnemyDirector(MultiplayerSpawner spawner, Node3D enemies)
    {
        _spawner = spawner;
        _enemies = enemies;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public EnemyDirector()
        : this(null!, null!)
    {
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_untilNextWave > 0f)
        {
            _untilNextWave -= (float)delta;
            if (_untilNextWave <= 0f)
            {
                SendWave();
            }

            return;
        }

        if (!_enemies.GetChildren().OfType<EnemyCharacter>().Any(e => !e.IsDead))
        {
            _untilNextWave = WaveGap;
        }
    }

    private void SendWave()
    {
        _wavesSent++;
        GD.Print($"[combat] wave {_wavesSent}: {Wave.Length} skeletons");
        foreach (var enemy in Wave)
        {
            float angle = _random.RandfRange(0f, Mathf.Tau);
            float radius = _random.RandfRange(SpawnRadiusMin, SpawnRadiusMax);
            var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            _spawner.Spawn(new Godot.Collections.Array { $"enemy{_nextId++}", enemy.Id, position, Yaw.Of(-position) });
        }
    }
}
