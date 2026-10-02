using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// An HP bar over every living skeleton and every other player who is up: a thin bar with no frame and no number,
// small enough to leave the fight in view. The fill tells them apart: red for skeletons and PvP opponents, blue for
// co-op allies. Screen-space, placed each frame from the character's position.
public partial class OverheadBars : Control
{
    private const float EnemyHeight = 1.95f;
    private const float PlayerHeight = 2.1f;
    private static readonly Vector2 BarSize = new(48f, 5f);
    private const float StatusesAbove = 1f;

    // Clear of a body this wide as the camera sees it (Overhead): from above, a bar by its height alone lies
    // across the head.
    private const float Girth = 0.6f;

    // Everdawn's TeamPlayer.
    private static readonly Color Ally = new(0.200f, 0.400f, 0.900f);

    private readonly Dictionary<Node3D, Entry> _bars = new();

    public Camera3D? Camera { get; set; }

    // Statuses named over bars right now, all bars together.
    public int StatusesNamed => _bars.Values.Sum(entry => entry.StatusesNamed);

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
            entry.SetStatuses(target.Statuses);
            entry.Bar.Value = target.Hp;

            var overhead = Overhead.Point(Camera, target.Node.GlobalPosition, target.Height, Girth);
            entry.Bar.Visible = !Camera.IsPositionBehind(overhead);
            entry.Bar.Position = Camera.UnprojectPosition(overhead) - BarSize / 2f;
        }
    }

    // What should carry a bar right now: living skeletons, and every player but the local one while they are up.
    private IEnumerable<(Node3D Node, int Hp, int MaxHp, float Height, Color Fill, Statuses Statuses)> Targets()
    {
        foreach (var enemy in GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => !e.IsDead))
        {
            yield return (enemy, enemy.Hp, enemy.Definition.MaxHp, EnemyHeight, UiTheme.BarHp, enemy.Statuses);
        }

        foreach (var player in GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>())
        {
            if (!player.IsMultiplayerAuthority() && !player.IsDowned)
            {
                yield return (player, player.Vitals.Hp, PlayerRules.MaxHp, PlayerHeight, SessionRules.Pvp ? UiTheme.BarHp : Ally, player.Vitals.Statuses);
            }
        }
    }

    private Entry Create(int maxHp)
    {
        var bar = new ProgressBar { ShowPercentage = false, MaxValue = maxHp, Size = BarSize, MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", UiTheme.Panel(UiTheme.BarTrack, null, radius: 2, margin: 0f));
        AddChild(bar);

        // The body's statuses, named in a row over the bar and centred on it.
        var statuses = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Begin,
            OffsetBottom = -StatusesAbove,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        statuses.AddThemeConstantOverride("separation", 4);
        bar.AddChild(statuses);
        return new Entry(bar, statuses);
    }

    private sealed class Entry
    {
        private readonly HBoxContainer _statuses;
        private Color? _fill;
        private Statuses? _named;

        public Entry(ProgressBar bar, HBoxContainer statuses)
        {
            Bar = bar;
            _statuses = statuses;
        }

        public ProgressBar Bar { get; }

        public int StatusesNamed => _statuses.GetChildCount();

        public void SetStatuses(Statuses statuses)
        {
            if (_named == statuses)
            {
                return;
            }

            _named = statuses;
            foreach (var named in _statuses.GetChildren())
            {
                _statuses.RemoveChild(named);
                named.QueueFree();
            }

            foreach (var (name, colour) in StatusLooks.Of(statuses))
            {
                _statuses.AddChild(UiTheme.MakeLabel(name, UiTheme.Words, 11, colour, outline: 3));
            }
        }

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
