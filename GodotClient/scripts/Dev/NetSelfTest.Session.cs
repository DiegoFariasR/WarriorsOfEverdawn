using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [wave-check]: waves keep coming. [leak-check]: dead skeletons and damage numbers go on time, and the node count
// stays flat from wave to wave instead of creeping up over a long session.
public partial class NetSelfTest
{
    // Late enough into a wave that the last wave's final corpse is gone (it goes CorpseTime after its death, and the
    // next wave comes EnemyDirector's gap after that same death), early enough that the new wave is all still there.
    private const float WaveSampleDelay = 1.5f;

    // Removal crosses the network on clients, so allow a little past each lifetime.
    private const float RemovalMargin = 0.5f;

    private readonly Dictionary<ulong, float> _diedAt = new();
    private readonly Dictionary<ulong, float> _textSeenAt = new();
    private readonly List<int> _nodesByWave = new();
    private readonly List<int> _orphansByWave = new();
    private int _waves;
    private bool _skeletonsUp;
    private float _untilWaveSample = -1f;
    private int _corpseLingering;
    private int _textLingering;

    private void PrintSessionChecks(long me)
    {
        GD.Print($"[wave-check] me={me} waves={_waves}");
        GD.Print($"[leak-check] me={me} corpse_lingering_frames={_corpseLingering} text_lingering_frames={_textLingering} "
            + $"node_growth={Growth(_nodesByWave)} orphan_growth={Growth(_orphansByWave)} "
            + $"nodes_by_wave={string.Join(",", _nodesByWave)} orphans_by_wave={string.Join(",", _orphansByWave)}");
    }

    // From the second wave on: players are still joining during the first.
    private static string Growth(List<int> samples) =>
        samples.Count >= 3 ? (samples.Skip(2).Max() - samples[1]).ToString() : "NaN";

    private void NoteCorpse(EnemyCharacter enemy) => _diedAt[enemy.GetInstanceId()] = _time;

    private void MeasureSession(float delta)
    {
        var skeletons = GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().ToList();
        bool up = skeletons.Any(e => !e.IsDead);
        if (up && !_skeletonsUp)
        {
            _waves++;
            _untilWaveSample = WaveSampleDelay;
        }

        _skeletonsUp = up;
        if (_untilWaveSample > 0f)
        {
            _untilWaveSample -= delta;
            if (_untilWaveSample <= 0f)
            {
                _nodesByWave.Add((int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
                _orphansByWave.Add((int)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount));
            }
        }

        _corpseLingering += skeletons.Count(e => e.IsDead && _diedAt.TryGetValue(e.GetInstanceId(), out float at)
            && _time - at > EnemyCharacter.CorpseTime + RemovalMargin);

        // Damage numbers are the only Label3D nodes placed straight under the scene.
        foreach (var text in GetTree().CurrentScene.GetChildren().OfType<Label3D>())
        {
            ulong id = text.GetInstanceId();
            if (!_textSeenAt.TryGetValue(id, out float seen))
            {
                _textSeenAt[id] = _time;
            }
            else if (_time - seen > FloatingText.Duration + RemovalMargin)
            {
                _textLingering++;
            }
        }
    }
}
