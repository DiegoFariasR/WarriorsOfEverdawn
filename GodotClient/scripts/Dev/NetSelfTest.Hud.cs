using System.Linq;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// [ui-check]: the player frame and every overhead bar show the real values, and a bar is drawn over a skeleton only
// within ArenaMap.SkeletonOverheadReach of the player.
public partial class NetSelfTest
{
    private int _hudHpMismatches;
    private int _enemyBarMismatches;
    private int _manaMismatches;
    private int _maxEnemyBars;
    private int _maxPlayerBars;
    private int _skeletonBarFrames;
    private int _farSkeletonBarFrames;

    private void PrintHudCheck(long me)
    {
        var stats = LocalPlayer()?.Stats;
        GD.Print($"[ui-check] me={me} hp_mismatch_frames={_hudHpMismatches} bar_mismatch_frames={_enemyBarMismatches} "
            + $"mana_mismatch_frames={_manaMismatches} max_enemy_bars={_maxEnemyBars} max_player_bars={_maxPlayerBars} "
            + $"skeleton_bar_frames={_skeletonBarFrames} far_skeleton_bar_frames={_farSkeletonBarFrames} "
            + $"mana_shown={_hud.ShownMana}/{LocalPlayer()?.MaxMana} stats=STR{stats?.Str},WIS{stats?.Wis},AGI{stats?.Agi}");
    }

    // Runs after the HUD in the frame (it sits earlier under the arena), so both read the same synced values. The
    // player frame must show the local HP and mana; every living skeleton and every other player who is up must
    // have a bar showing their HP; no bar may outlive its owner.
    private void MeasureHud()
    {
        var local = LocalPlayer();
        if (local != null && _hud.ShownHp != local.Vitals.Hp)
        {
            _hudHpMismatches++;
        }

        if (local != null && _hud.ShownMana != local.Mana)
        {
            _manaMismatches++;
        }

        var skeletons = EnemyCharacter.Standing(GetTree()).ToList();
        var others = PlayerCharacter.All(GetTree()).Where(p => !p.IsMultiplayerAuthority() && !p.IsDowned).ToList();
        var shown = _hud.Bars.Shown.ToList();
        _maxEnemyBars = Mathf.Max(_maxEnemyBars, shown.Count(s => s.Target is EnemyCharacter));
        _maxPlayerBars = Mathf.Max(_maxPlayerBars, shown.Count(s => s.Target is PlayerCharacter));
        if (local != null)
        {
            var drawnAt = shown.Where(s => s.Drawn && s.Target is EnemyCharacter).Select(s => Yaw.Flat(s.Target.GlobalPosition - local.GlobalPosition).Length()).ToList();
            _skeletonBarFrames += drawnAt.Count > 0 ? 1 : 0;
            _farSkeletonBarFrames += drawnAt.Any(distance => distance > ArenaMap.SkeletonOverheadReach) ? 1 : 0;
        }
        bool matches = shown.Count == skeletons.Count + others.Count
            && skeletons.All(e => shown.Any(s => s.Target == e && s.Shown == e.Hp))
            && others.All(p => shown.Any(s => s.Target == p && s.Shown == p.Vitals.Hp));
        if (!matches)
        {
            _enemyBarMismatches++;
        }
    }
}
