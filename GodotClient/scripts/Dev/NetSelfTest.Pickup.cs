using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [pickup-check]: weapons let go of lie on the ground on every machine and leave it when taken up; the local player
// drops and takes its own; an unarmed player starts no attack; and the labels show for exactly the weapons in range,
// with the offer to take one while a slot is free.
public partial class NetSelfTest
{
    // Labels are drawn a frame after the ground changes, so a mismatch only counts once it lasts.
    private const int LabelLagFrames = 3;

    private GroundWeapons? _ground;
    private int _weaponsPlaced;
    private int _weaponsTaken;
    private int _dropsHere;
    private int _pickUpsHere;
    private int _attacksUnarmed;
    private int _labelFrames;
    private int _offerFrames;
    private int _labelMismatchFrames;
    private int _labelMismatchRun;

    private void TrackGround()
    {
        _ground = GroundWeapons.In(GetTree());
        _ground.Placed += _ => _weaponsPlaced++;
        _ground.Taken += (_, byPeer) =>
        {
            _weaponsTaken++;
            _pickUpsHere += byPeer == Multiplayer.GetUniqueId() ? 1 : 0;
        };
    }

    private void TrackPickups(PlayerCharacter player)
    {
        var carried = new WeaponSets(player.Weapon, player.StowedWeapon);
        player.WeaponsChanged += sets =>
        {
            // The hand emptied with the back untouched: let go of, not swapped.
            _dropsHere += carried.Active != null && sets == carried.WithHandEmptied() ? 1 : 0;
            carried = sets;
        };
        player.AttackStarted += _ => _attacksUnarmed += player.Weapon == null ? 1 : 0;
    }

    private void MeasurePickups()
    {
        if (_ground == null || LocalPlayer() is not { } local)
        {
            return;
        }

        var inRange = _ground.Items.Where(i => Flat(i.Position - local.GlobalPosition) <= Pickups.LabelRange).ToList();
        var labels = _hud.GroundLabels;
        bool matches = labels.Shown.OrderBy(id => id).SequenceEqual(inRange.Select(i => i.Id).OrderBy(id => id))
            && labels.Detailed == _ground.AttendedBy(local.GlobalPosition, local.AimYaw)?.Id;
        _labelMismatchRun = matches ? 0 : _labelMismatchRun + 1;
        _labelMismatchFrames += _labelMismatchRun > LabelLagFrames ? 1 : 0;
        _labelFrames += matches && inRange.Count > 0 ? 1 : 0;
        _offerFrames += labels.OffersPickUp == true ? 1 : 0;
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.X, offset.Z).Length();

    private void PrintPickupCheck(long me) =>
        GD.Print($"[pickup-check] me={me} placed_seen={_weaponsPlaced} taken_seen={_weaponsTaken} on_ground={_ground?.Items.Count() ?? -1} "
            + $"drops_here={_dropsHere} pickups_here={_pickUpsHere} attacks_unarmed={_attacksUnarmed} "
            + $"label_frames={_labelFrames} offer_frames={_offerFrames} label_mismatch_frames={_labelMismatchFrames}");
}
