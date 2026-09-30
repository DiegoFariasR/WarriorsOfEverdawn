using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Main;

// An HP bar over every living skeleton and every other player who is up, in Everdawn's overhead-bar style
// (UnitBarsHud): a small dark panel, the red bar and its value. The border tells them apart: gold for skeletons,
// blue for co-op allies, red for PvP opponents. Screen-space, placed each frame from the character's position.
public partial class OverheadBars : Control
{
    private const float EnemyHeight = 2.5f;
    private const float PlayerHeight = 2.9f;
    private static readonly Vector2 BarSize = new(96f, 11f);

    // Everdawn's TeamPlayer / TeamEnemy.
    private static readonly Color Ally = new(0.200f, 0.400f, 0.900f);
    private static readonly Color Opponent = new(0.800f, 0.200f, 0.200f);

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
            _bars[gone].Panel.QueueFree();
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

            entry.SetBorder(target.Border);
            entry.Bar.Value = target.Hp;
            entry.Text.Text = $"{target.Hp}/{target.MaxHp}";

            var overhead = target.Node.GlobalPosition + Vector3.Up * target.Height;
            entry.Panel.Visible = !Camera.IsPositionBehind(overhead);
            entry.Panel.Position = Camera.UnprojectPosition(overhead) - entry.Panel.Size / 2f;
        }
    }

    // What should carry a bar right now: living skeletons, and every player but the local one while they are up.
    private IEnumerable<(Node3D Node, int Hp, int MaxHp, float Height, Color Border)> Targets()
    {
        foreach (var enemy in GetTree().GetNodesInGroup(EnemyCharacter.Group).OfType<EnemyCharacter>().Where(e => !e.IsDead))
        {
            yield return (enemy, enemy.Hp, enemy.Definition.MaxHp, EnemyHeight, UiTheme.Gold);
        }

        foreach (var player in GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>())
        {
            if (!player.IsMultiplayerAuthority() && !player.IsDowned)
            {
                yield return (player, player.Vitals.Hp, PlayerRules.MaxHp, PlayerHeight, SessionRules.Pvp ? Opponent : Ally);
            }
        }
    }

    private Entry Create(int maxHp)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var (root, bar, text) = UiTheme.MakeValueBar(UiTheme.BarHp, BarSize, 9);
        bar.MaxValue = maxHp;
        panel.AddChild(root);
        AddChild(panel);
        return new Entry(panel, bar, text);
    }

    private sealed class Entry
    {
        private Color? _border;

        public Entry(PanelContainer panel, ProgressBar bar, Label text)
        {
            Panel = panel;
            Bar = bar;
            Text = text;
        }

        public PanelContainer Panel { get; }

        public ProgressBar Bar { get; }

        public Label Text { get; }

        // PvP can be decided after a player's bar exists (the rules reach a client as it joins), so the border
        // follows the current relation.
        public void SetBorder(Color border)
        {
            if (_border == border)
            {
                return;
            }

            _border = border;
            Panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(UiTheme.WoodDk, 0.55f), border, radius: 3, margin: 2f));
        }
    }
}
