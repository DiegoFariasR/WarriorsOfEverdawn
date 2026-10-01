using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Enemy;

// Host only. Sends a wave when the previous one is cleared. Wave size does not scale with player count yet
// (open question in Docs/Design/multiplayer.md).
public partial class EnemyDirector : Node
{
    private const float FirstWaveDelay = 1f;
    private const float WaveGap = 3f;
    // More skeletons than the fortress has spots share them, a little apart.
    private const float SharedSpotJitter = 0.5f;

    private static readonly EnemyDefinition[] Wave =
    {
        Enemies.SkeletonMinion, Enemies.SkeletonMinion, Enemies.SkeletonMinion,
        Enemies.SkeletonWarrior, Enemies.SkeletonWarrior,
        Enemies.SkeletonArcher, Enemies.SkeletonArcher,
    };

    private readonly MultiplayerSpawner _spawner;
    private readonly Node3D _enemies;
    private readonly RandomNumberGenerator _random = new();
    private readonly int _waveScale;
    private float _untilNextWave = FirstWaveDelay;
    private int _wavesSent;
    private int _nextId;

    public EnemyDirector(MultiplayerSpawner spawner, Node3D enemies, int waveScale)
    {
        _spawner = spawner;
        _enemies = enemies;
        _waveScale = waveScale;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public EnemyDirector()
        : this(null!, null!, 1)
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
        GD.Print($"[combat] wave {_wavesSent}: {Wave.Length * _waveScale} skeletons");
        // They rise inside the enemy fortress, on its spawn spots in a random order, facing its gate.
        var map = ArenaMap.In(GetTree());
        var spots = map.EnemySpawns.OrderBy(_ => _random.Randi()).ToList();
        var toGate = map.FortressGate - new Vector3(map.Fortress.Centre.X, 0f, map.Fortress.Centre.Y);
        int placed = 0;
        foreach (var enemy in Enumerable.Repeat(Wave, _waveScale).SelectMany(wave => wave))
        {
            var position = spots[placed % spots.Count];
            if (placed >= spots.Count)
            {
                position += new Vector3(_random.RandfRange(-SharedSpotJitter, SharedSpotJitter), 0f, _random.RandfRange(-SharedSpotJitter, SharedSpotJitter));
            }

            placed++;
            _spawner.Spawn(new Godot.Collections.Array { $"enemy{_nextId++}", enemy.Id, position, Yaw.Of(toGate) });
        }
    }
}
