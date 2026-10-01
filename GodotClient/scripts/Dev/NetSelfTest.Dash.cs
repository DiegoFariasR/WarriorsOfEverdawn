using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [dash-check]: dashes keep their distance and charges, show on other machines, and their ghosts clear.
// [spin-dash-check]: a Spin held through a dash carries on: it keeps testing for hits all the way, is still going as
// the dash ends, and other machines see the player spin through the dash.
public partial class NetSelfTest
{
    private readonly List<float> _dashDistances = new();
    private readonly List<float> _dashStartTimes = new();
    private int _remoteDashesSeen;
    private int _ghostLingering;
    private int _spinDashes;
    private int _spinDashesKept;
    private int _spinDashesCut;
    private int _remoteSpinDashesSeen;
    private int _spinTestsWhileDashing;
    private bool _dashCarriesSpin;
    private bool _spinLetGo;
    private float _lastSpinTestAt = float.NegativeInfinity;

    private void TrackDashes(PlayerCharacter player)
    {
        if (!player.IsMultiplayerAuthority())
        {
            player.DashStarted += _ =>
            {
                _remoteDashesSeen++;
                _remoteSpinDashesSeen += player.IsChanneling ? 1 : 0;
            };
            return;
        }

        // A dash that cancels the swing has cleared it by the time it is announced, so a Spin still showing is one the
        // dash carries.
        player.DashStarted += _ =>
        {
            _dashStartTimes.Add(_time);
            _dashCarriesSpin = player.IsChanneling;
            _spinLetGo = false;
            _spinDashes += _dashCarriesSpin ? 1 : 0;
        };

        // A Spin's hit window is its whole revolution, so it tests every frame for as long as it goes on.
        player.HitTested += (skill, _) =>
        {
            if (skill.Channeled)
            {
                _lastSpinTestAt = _time;
                _spinTestsWhileDashing += player.IsDashing ? 1 : 0;
            }
        };
        player.DashFinished += (from, to) =>
        {
            _dashDistances.Add(new Vector2(to.X - from.X, to.Z - from.Z).Length());
            if (!_dashCarriesSpin)
            {
                return;
            }

            if (_time - _lastSpinTestAt <= SpinStillGoing)
            {
                _spinDashesKept++;
            }
            else if (!_spinLetGo && !EndsSpinFairly(player))
            {
                _spinDashesCut++;
            }

            _dashCarriesSpin = false;
        };
    }

    // A Spin that tested for hits this recently is still going.
    private const float SpinStillGoing = 0.05f;

    // What ends a Spin whatever the dash does: its button let go, no mana for the next revolution, or going down.
    private static bool EndsSpinFairly(PlayerCharacter player) =>
        player.IsDowned || player.Controls?.SkillHeld != PlayerCharacter.Secondary || !player.CanUse(PlayerCharacter.Secondary);

    // Watched on every frame of a dash that carries a Spin: a bot lets go for a frame as its target leaves reach.
    private void MeasureSpinDash()
    {
        if (_dashCarriesSpin && LocalPlayer() is { } local && EndsSpinFairly(local))
        {
            _spinLetGo = true;
        }
    }

    private void PrintDashCheck(long me)
    {
        GD.Print($"[spin-dash-check] me={me} spin_dashes={_spinDashes} kept={_spinDashesKept} cut_while_held={_spinDashesCut} "
            + $"tests_while_dashing={_spinTestsWhileDashing} remote_seen={_remoteSpinDashesSeen}");
        float dashMedian = _dashDistances.Count > 0 ? _dashDistances.OrderBy(d => d).ElementAt(_dashDistances.Count / 2) : float.NaN;
        float dashLongest = _dashDistances.Count > 0 ? _dashDistances.Max() : float.NaN;
        GD.Print($"[dash-check] me={me} dashes={_dashDistances.Count} distance_median={dashMedian:F2} distance_max={dashLongest:F2} expected={DashRules.Distance:F2} "
            + $"max_in_recharge_window={MaxDashesInRechargeWindow()} charges={DashRules.Charges} refused={LocalPlayer()?.DashesRefused} remote_dashes_seen={_remoteDashesSeen} "
            + $"ghosts_emitted={LocalPlayer()?.Ghosts.Emitted} ghost_lingering_frames={_ghostLingering}");
    }

    // Ghosts that outstay their fade.
    private void MeasureGhosts()
    {
        var local = LocalPlayer();
        if (local != null && local.Ghosts.Showing > 0 && local.Ghosts.OldestAge > GhostTrail.FadeTime + 0.1f)
        {
            _ghostLingering++;
        }
    }

    // Dashes started within any stretch just short of one recharge: never more than the charges.
    private int MaxDashesInRechargeWindow()
    {
        float window = DashRules.RechargeTime * 0.99f;
        int most = 0;
        for (int i = 0; i < _dashStartTimes.Count; i++)
        {
            most = Mathf.Max(most, _dashStartTimes.Count(t => t >= _dashStartTimes[i] && t < _dashStartTimes[i] + window));
        }

        return most;
    }
}
