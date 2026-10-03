using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [level-check]: every player earns XP as monsters die and goes up a level at each level's XP, which every machine
// sees; the local bot puts each point it gets into STR, WIS and AGI in turn, with the HUD's buttons, and on every
// machine each player's stats are what it started with and a point a level, less the points it has yet to put in; its
// mana pool is as big as its WIS makes it, catching up in the physics step after a point goes in; the HUD shows its
// level and XP and its points while it has any. On the host, where it is decided, its own player's XP adds up to the
// monsters' it saw die.
public partial class NetSelfTest
{
    // A point at most this often, so the host has answered one click before the next.
    private const float RaiseEvery = 0.5f;

    private int _xpSinceHere;
    private int _levelUpsSeen;
    private int _levelUpsHere;
    private int _raisesClicked;
    private float _sinceRaise;
    private int _levelHudMismatchFrames;
    private int _progressMismatchFrames;
    private int _manaMaxLateFrames;
    private ulong? _manaBehindSince;

    private void OnLeveledUp(PlayerCharacter player, int level)
    {
        _levelUpsSeen++;
        _levelUpsHere += player.IsMultiplayerAuthority() ? 1 : 0;
    }

    private void MeasureLevel(float delta)
    {
        foreach (var player in PlayerCharacter.All(GetTree()))
        {
            var vitals = player.Vitals;
            if (vitals.Stats.Total != PlayerRules.KnightStats.Total + (vitals.Level - LevelRules.FirstLevel) * LevelRules.PointsPerLevel - vitals.Points)
            {
                _progressMismatchFrames++;
            }
        }

        if (LocalPlayer() is not { } local)
        {
            return;
        }

        var own = local.Vitals;
        MeasureManaPool(local);
        if (_hud.ShownLevel != own.Level || _hud.ShownXp != own.Xp || _hud.PointsShown != own.Points > 0)
        {
            _levelHudMismatchFrames++;
        }

        _sinceRaise += delta;
        if (local.Controls is BotControls && _hud.PointsShown && _sinceRaise >= RaiseEvery)
        {
            _hud.RaiseButton((Stat)(_raisesClicked % 3)).EmitSignal(BaseButton.SignalName.Pressed);
            _raisesClicked++;
            _sinceRaise = 0f;
        }
    }

    // The pool catches up in the player's next physics step; the frames drawn before it see it behind, more of them
    // the faster frames are drawn.
    private void MeasureManaPool(PlayerCharacter local)
    {
        if (local.MaxMana == StatRules.MaxMana(local.Stats))
        {
            _manaBehindSince = null;
            return;
        }

        _manaBehindSince ??= Engine.GetPhysicsFrames();
        _manaMaxLateFrames += Engine.GetPhysicsFrames() > _manaBehindSince + 1 ? 1 : 0;
    }

    private void PrintLevelCheck(long me)
    {
        var local = LocalPlayer();
        var vitals = local?.Vitals;
        var start = PlayerRules.KnightStats;
        GD.Print($"[level-check] me={me} level={vitals?.Level} xp={vitals?.Xp} points={vitals?.Points} "
            + $"xp_total={(vitals == null ? -1 : LevelRules.Reaching(vitals.Level) + vitals.Xp)} xp_died_here={_xpSinceHere} "
            + $"level_ups_here={_levelUpsHere} level_ups_seen={_levelUpsSeen} players={PlayerCharacter.All(GetTree()).Count()} "
            + $"raises_clicked={_raisesClicked} str={vitals?.Str} wis={vitals?.Wis} agi={vitals?.Agi} "
            + $"stats_gained={(vitals == null ? -1 : vitals.Stats.Total - start.Total)} "
            + $"mana_max={local?.MaxMana} mana_per_wis={StatRules.ManaPerWis} mana_max_late_frames={_manaMaxLateFrames} "
            + $"hud_mismatch_frames={_levelHudMismatchFrames} "
            + $"progress_mismatch_frames={_progressMismatchFrames}");
    }
}
