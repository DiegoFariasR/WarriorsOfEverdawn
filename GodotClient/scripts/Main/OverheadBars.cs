using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Main;

// An HP bar over every living skeleton and every other player who is up: a thin bar with no frame and no number,
// small enough to leave the fight in view. The fill tells them apart: red for skeletons and PvP opponents, blue for
// co-op allies. Screen-space, placed each frame from the character's position.
public partial class OverheadBars : Control
{
    private const float EnemyHeight = 1.95f;
    private const float PlayerHeight = 2.1f;
    private static readonly Vector2 BarSize = new(48f, 5f);

    // Everdawn's TeamPlayer.
    private static readonly Color Ally = new(0.200f, 0.400f, 0.900f);

    private readonly Dictionary<Node3D, Entry> _bars = new();

    public Camera3D? Camera { get; set; }

    public IEnumerable<(Node3D Target, int Shown)> Shown =>
        _bars.Where(pair => IsInstanceValid(pair.Key)).Select(pair => (pair.Key, (int)pair.Value.Bar.Value));

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        var targets = Targets().ToDictionary(t => t.Node);
        foreach (var gone in _bars.Keys.Where(n => !targets.ContainsKey(n)).ToList())
        {
            _bars[gone].Bar.QueueFree();
            _bars.Remove(gone);
        }

        if (Camera == null)
        {
            return;
        }

        foreach (var target in targets.Values)
        {
            if (!_bars.TryGetValue(target.Node, out var entry))
            {
                entry = _bars[target.Node] = Create(target.MaxHp);
            }

            entry.SetFill(target.Fill);
            entry.Bar.Value = target.Hp;

            var overhead = target.Node.GlobalPosition + Vector3.Up * target.Height;
            entry.Bar.Visible = !Camera.IsPositionBehind(overhead);
            entry.Bar.Position = Camera.UnprojectPosition(overhead) - BarSize / 2f;
        }
    }

    // What should carry a bar right now: living skeletons, and every player but the local one while they are up.
    private IEnumerable<(Node3D Node, int Hp, int MaxHp, float Height, Color Fill)> Targets()
    {
        foreach (var enemy in GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => !e.IsDead))
        {
            yield return (enemy, enemy.Hp, enemy.Definition.MaxHp, EnemyHeight, UiTheme.BarHp);
        }

        foreach (var player in GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>())
        {
            if (!player.IsMultiplayerAuthority() && !player.IsDowned)
            {
                yield return (player, player.Vitals.Hp, PlayerRules.MaxHp, PlayerHeight, SessionRules.Pvp ? UiTheme.BarHp : Ally);
            }
        }
    }

    private Entry Create(int maxHp)
    {
        var bar = new ProgressBar { ShowPercentage = false, MaxValue = maxHp, Size = BarSize, MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", UiTheme.Panel(UiTheme.BarTrack, null, radius: 2, margin: 0f));
        AddChild(bar);
        return new Entry(bar);
    }

    private sealed class Entry
    {
        private Color? _fill;

        public Entry(ProgressBar bar)
        {
            Bar = bar;
        }

        public ProgressBar Bar { get; }

        // PvP can be decided after a player's bar exists (the rules reach a client as it joins), so the fill follows
        // the current relation.
        public void SetFill(Color fill)
        {
            if (_fill == fill)
            {
                return;
            }

            _fill = fill;
            Bar.AddThemeStyleboxOverride("fill", UiTheme.Panel(fill, null, radius: 2, margin: 0f));
        }
    }
}
