using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// A weapon lying on the ground, where its middle rests.
public sealed record GroundWeapon(int Id, WeaponDefinition Weapon, Vector3 Position, float Yaw);

// Weapons lying on the ground. The host owns the list: a player's machine asks it to put a dropped weapon down or to
// hand one over, and the host tells every machine what lies where, so two players reaching for the same weapon cannot
// both get it. Present on every machine at the same path, for its RPCs. Design: Docs/Design/combat.md.
public partial class GroundWeapons : Node3D
{
    public const string NodeName = "GroundWeapons";

    // Just clear of the ground, so the model does not fight with it for the same pixels.
    private const float RestHeight = 0.06f;


    private readonly Dictionary<int, (GroundWeapon Item, Node3D Model)> _lying = new();
    private int _nextId;

    // On every machine, as a weapon is put down.
    public event Action<GroundWeapon>? Placed;

    // On every machine, as a weapon is taken up, with the peer who took it.
    public event Action<GroundWeapon, long>? Taken;

    public IEnumerable<GroundWeapon> Items => _lying.Values.Select(l => l.Item);

    public static GroundWeapons In(SceneTree tree) => tree.CurrentScene.GetNode<GroundWeapons>(NodeName);

    // The weapon a player standing here and facing this way would take: of those in reach, the one nearest the spot
    // in front of it.
    public GroundWeapon? InReachOf(Vector3 position, float facingYaw) => Focused(position, facingYaw, Pickups.Reach);

    // The weapon that player's label details go to: the one it would take, or with none in reach the one it faces
    // most nearly among those close enough to read.
    public GroundWeapon? AttendedBy(Vector3 position, float facingYaw) =>
        InReachOf(position, facingYaw) ?? Focused(position, facingYaw, Pickups.LabelRange);

    private GroundWeapon? Focused(Vector3 position, float facingYaw, float within)
    {
        var items = Items.ToList();
        int focused = Pickups.Focused(items.Select(i => Yaw.ToGround(i.Position)).ToList(), Yaw.ToGround(position), facingYaw, within);
        return focused >= 0 ? items[focused] : null;
    }

    // From the machine of the player letting go of the weapon.
    public void Drop(WeaponDefinition weapon, Vector3 at, float yaw) => RpcId(1, MethodName.RequestDrop, weapon.Id, at, yaw);

    // From the machine of the player reaching for it. The host answers with Take, to everyone, or with Refuse.
    public void PickUp(int id) => RpcId(1, MethodName.RequestPickUp, id);

    // Host: a machine that has just joined learns what already lies on the ground.
    public void SendAllTo(long peer)
    {
        foreach (var item in Items)
        {
            RpcId(peer, MethodName.Place, item.Id, item.Weapon.Id, item.Position, item.Yaw);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDrop(string weaponId, Vector3 at, float yaw)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        if (Find(weaponId) == null)
        {
            return;
        }

        Rpc(MethodName.Place, _nextId++, weaponId, new Vector3(at.X, 0f, at.Z), yaw);
    }

    // First come, first served: a weapon already taken is refused to whoever asks next.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestPickUp(int id)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        // 0 when the host's own player asks.
        long peer = Multiplayer.GetRemoteSenderId();
        if (peer == 0)
        {
            peer = Multiplayer.GetUniqueId();
        }

        if (_lying.ContainsKey(id))
        {
            Rpc(MethodName.Take, id, peer);
        }
        else
        {
            RpcId(peer, MethodName.Refuse);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Place(int id, string weaponId, Vector3 at, float yaw)
    {
        // A machine joining as a weapon is put down can hear of it twice: once with everyone, once in the catch-up.
        if (_lying.ContainsKey(id) || Find(weaponId) is not { } weapon)
        {
            return;
        }

        var pivot = Lying(weapon, at, yaw);
        pivot.Name = $"Weapon{id}";
        AddChild(pivot);

        var item = new GroundWeapon(id, weapon, at, yaw);
        _lying[id] = (item, pivot);
        Placed?.Invoke(item);
    }

    // The weapon as it lies on the ground at a spot, turned to a yaw.
    public static Node3D Lying(WeaponDefinition weapon, Vector3 at, float yaw)
    {
        var look = CombatVisuals.LookFor(weapon);
        var model = Assets.InstantiateAtOrigin(look.Model);
        ToonLook.ApplyToWeapon(model);
        CharacterRig.Adorned(model, look);
        var pivot = new Node3D { Position = at + Vector3.Up * RestHeight };

        // Weapon models stand along +Y from wherever their grip is; laid on its side, the weapon rests on its middle.
        pivot.Rotation = new Vector3(Mathf.Pi / 2f, yaw, 0f);
        model.Basis = Basis.FromEuler(look.GroundRotation).Scaled(Vector3.One * look.Scale);
        model.Position = -(model.Basis * MiddleOf(model));
        pivot.AddChild(model);

        // What goes in the other hand lies beside the weapon, as its look says it lies.
        if (look.OffHand is { } piece)
        {
            var beside = Assets.InstantiateAtOrigin(piece.Model);
            ToonLook.ApplyToWeapon(beside);
            CharacterRig.Adorned(beside, look with { Glow = null });
            beside.Basis = Basis.FromEuler(piece.GroundRotation).Scaled(Vector3.One * piece.Scale);
            beside.Position = Vector3.Right * piece.GroundBeside - beside.Basis * MiddleOf(beside);
            pivot.AddChild(beside);
        }

        return pivot;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Take(int id, long byPeer)
    {
        if (!_lying.Remove(id, out var lying))
        {
            GD.PushError($"[GroundWeapons] weapon {id}, taken by peer {byPeer}, is not on the ground on this machine");
            return;
        }

        lying.Model.QueueFree();
        Taken?.Invoke(lying.Item, byPeer);
        if (byPeer != Multiplayer.GetUniqueId())
        {
            return;
        }

        if (PlayerCharacter.Find(GetTree(), byPeer) is { } player)
        {
            player.OnPickedUp(lying.Item.Weapon);
        }
        else
        {
            GD.PushError($"[GroundWeapons] the {lying.Item.Weapon.Name} was handed to this machine, which has no player to take it; it is lost");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refuse() => PlayerCharacter.Find(GetTree(), Multiplayer.GetUniqueId())?.OnPickUpRefused();

    private static WeaponDefinition? Find(string weaponId)
    {
        try
        {
            return Weapons.ById(weaponId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[GroundWeapons] {e.Message}");
            return null;
        }
    }

    // The centre of the model's meshes, in the model's own space.
    private static Vector3 MiddleOf(Node3D model)
    {
        Aabb? bounds = null;
        foreach (var mesh in model.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>())
        {
            var box = mesh.Transform * mesh.GetAabb();
            bounds = bounds?.Merge(box) ?? box;
        }

        return bounds?.GetCenter() ?? throw new InvalidOperationException($"{model.Name} has no mesh to lay on the ground");
    }
}
