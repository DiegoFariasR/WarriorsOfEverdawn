using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [wave-check]: waves keep coming. [leak-check]: dead skeletons and damage numbers go on time, and what does not
// come and go with the fight stays flat from wave to wave instead of creeping up over a long session.
public partial class NetSelfTest
{
    // What comes and goes with the fight is checked on its own and left out here: skeletons and damage numbers (the two
    // lifetimes above), gold and orbs on the ground ([loot-check]; what nobody walks over lies there for good), the HP bars
    // over the living ([ui-check]) and arrows in flight ([ranged-check]). What must stay flat is the rest, at its
    // fewest in each wave: a leak is there at every moment. A player with an empty hand or back is fewer nodes than
    // one carrying both weapons (each is on the model and on every dash ghost), so a sample is taken only while every
    // player carries both, which can leave a wave only its fighting to sample.
    private const float NodeSampleEvery = 0.25f;

    // Removal crosses the network on clients, so allow a little past each lifetime.
    private const float RemovalMargin = 0.5f;

    private sealed record NodeSample(int Standing, Dictionary<string, int> Kinds);

    private readonly Dictionary<ulong, float> _diedAt = new();
    private readonly Dictionary<ulong, float> _textSeenAt = new();
    private readonly List<NodeSample> _fewestByWave = new();
    private readonly List<int> _fewestOrphansByWave = new();
    private NodeSample? _fewest;

    // Orphans at their fewest in each wave too: a weapon changing hands leaves its old models out of the tree until
    // the frame ends, and a leak is there in every frame.
    private int _fewestOrphans;
    private float _untilNodeSample;
    private int _waves;
    private bool _skeletonsUp;
    private int _corpseLingering;
    private int _textLingering;

    private void PrintSessionChecks(long me)
    {
        var standing = _fewestByWave.Select(s => s.Standing).ToList();
        var orphans = _fewestOrphansByWave;
        GD.Print($"[wave-check] me={me} waves={_waves}");
        GD.Print($"[leak-check] me={me} corpse_lingering_frames={_corpseLingering} text_lingering_frames={_textLingering} "
            + $"node_growth={Growth(standing)} orphan_growth={Growth(orphans)} "
            + $"standing_nodes_by_wave={string.Join(",", standing)} orphans_by_wave={string.Join(",", orphans)} "
            + $"grew_at_peak={GrewAtPeak()}");
    }

    // From the second wave on: players are still joining during the first.
    private static string Growth(List<int> samples) =>
        samples.Count >= 3 ? (samples.Skip(2).Max() - samples[1]).ToString() : "NaN";

    // What the fullest wave had more or less of than the second, by kind of thing under the scene: a failing
    // node_growth then says what piled up.
    private string GrewAtPeak()
    {
        if (_fewestByWave.Count < 3)
        {
            return "NaN";
        }

        int peak = Enumerable.Range(2, _fewestByWave.Count - 2).MaxBy(i => _fewestByWave[i].Standing);
        var from = _fewestByWave[1].Kinds;
        var to = _fewestByWave[peak].Kinds;
        var changes = from.Keys.Union(to.Keys)
            .Select(kind => (kind, change: to.GetValueOrDefault(kind) - from.GetValueOrDefault(kind)))
            .Where(c => c.change != 0)
            .OrderByDescending(c => c.change)
            .Select(c => $"{c.kind}:{c.change:+0;-0}");
        return $"wave{peak + 1}/" + string.Join("/", changes);
    }

    private void NoteCorpse(EnemyCharacter enemy) => _diedAt[enemy.GetInstanceId()] = _time;

    private void MeasureSession(float delta)
    {
        var skeletons = GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().ToList();
        bool up = skeletons.Any(e => !e.IsDead);
        if (up && !_skeletonsUp)
        {
            _waves++;
            if (_fewest != null)
            {
                _fewestByWave.Add(_fewest);
                _fewestOrphansByWave.Add(_fewestOrphans);
            }

            _fewest = null;
        }

        _skeletonsUp = up;
        _untilNodeSample -= delta;
        if (_waves > 0 && _untilNodeSample <= 0f && EveryPlayerCarriesBoth())
        {
            _untilNodeSample = NodeSampleEvery;
            var sample = SampleNodes();
            int orphans = (int)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            _fewestOrphans = _fewest == null ? orphans : Mathf.Min(_fewestOrphans, orphans);
            if (_fewest == null || sample.Standing < _fewest.Standing)
            {
                _fewest = sample;
            }
        }

        _corpseLingering += skeletons.Count(e => e.IsDead && _diedAt.TryGetValue(e.GetInstanceId(), out float at)
            && _time - at > EnemyCharacter.CorpseTime + RemovalMargin);

        foreach (var text in DamageNumbers())
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

    private bool EveryPlayerCarriesBoth() =>
        _players.GetChildren().OfType<PlayerCharacter>().All(p => p.Weapon != null && p.StowedWeapon != null);

    // Damage numbers are the only Label3D nodes placed straight under the scene.
    private IEnumerable<Label3D> DamageNumbers() => GetTree().CurrentScene.GetChildren().OfType<Label3D>();

    private NodeSample SampleNodes()
    {
        var scene = GetTree().CurrentScene;
        int total = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var kinds = new Dictionary<string, int>();
        int standingInScene = 1;
        foreach (var child in scene.GetChildren(true).Where(c => c is not Label3D))
        {
            int nodes = CountStanding(child);
            string kind = child.GetType().Assembly == typeof(Node).Assembly ? child.Name.ToString() : child.GetType().Name;
            kinds[kind] = kinds.GetValueOrDefault(kind) + nodes;
            standingInScene += nodes;
        }

        int inScene = CountNodes(scene);
        kinds["OutsideTheScene"] = total - inScene;
        return new NodeSample(total - inScene + standingInScene, kinds);
    }

    private static int CountNodes(Node node) => 1 + node.GetChildren(true).Sum(CountNodes);

    private static int CountStanding(Node node) =>
        node is EnemyCharacter ? 0
        : node is Loot or OverheadBars or Arrows ? 1
        : 1 + node.GetChildren(true).Sum(CountStanding);
}
