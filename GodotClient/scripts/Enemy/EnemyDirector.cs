using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Enemy;

// Host only. Sends a wave when the previous one is cleared, and raises the crypt's guards once, as the session starts:
// they are no part of any wave, and the waves go on whether they stand or not. When the last guard falls, the crypt's
// treasure is left. Wave size does not scale with player count yet (open question in Docs/Design/multiplayer.md).
public partial class EnemyDirector : Node
{
    private const float FirstWaveDelay = 1f;
    private const float WaveGap = 3f;
    // More skeletons than the fortress has spots share them, a little apart.
    private const float SharedSpotJitter = 0.5f;

    // What the crypt's guards are named by, before their number.
    public const string GuardPrefix = "guard";

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
    private int _guardsRisen = -1;
    private bool _treasureLeft;

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
        if (_guardsRisen < 0)
        {
            RaiseGuards();
        }

        var standing = _enemies.GetChildren().OfType<EnemyCharacter>().Where(e => !e.IsDead).ToList();
        if (Crypt.Cleared(_guardsRisen, standing.Count(e => e.IsGuard), _treasureLeft))
        {
            _treasureLeft = true;
            GD.Print("[combat] the crypt is cleared: its treasure is left");
            Loot.In(GetTree()).LeaveTreasure(ArenaMap.In(GetTree()).CryptTreasure, Crypt.Treasure);
        }

        if (_untilNextWave > 0f)
        {
            _untilNextWave -= (float)delta;
            if (_untilNextWave <= 0f)
            {
                SendWave();
            }

            return;
        }

        if (!standing.Any(e => !e.IsGuard))
        {
            _untilNextWave = WaveGap;
        }
    }

    // One guard to each of the crypt's guard spots, facing into the crypt.
    private void RaiseGuards()
    {
        var map = ArenaMap.In(GetTree());
        _guardsRisen = map.CryptGuards.Count;
        GD.Print($"[combat] the crypt's guards rise: {_guardsRisen}");
        for (int i = 0; i < map.CryptGuards.Count; i++)
        {
            var spot = map.CryptGuards[i];
            _spawner.Spawn(new Godot.Collections.Array { $"{GuardPrefix}{i}", Crypt.GuardFor(i).Id, spot, Yaw.Of(map.Crypt - spot) });
        }
    }

    private void SendWave()
    {
        _wavesSent++;
        GD.Print($"[combat] wave {_wavesSent}: {Wave.Length * _waveScale} skeletons");
        // They rise inside the enemy fortress, on its spawn spots in a random order, facing its gate.
        var map = ArenaMap.In(GetTree());
        var spots = map.EnemySpawns.OrderBy(_ => _random.Randi()).ToList();
        var toGate = map.FortressGate - new Vector3(map.FortressCourtyard.Centre.X, 0f, map.FortressCourtyard.Centre.Y);
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
