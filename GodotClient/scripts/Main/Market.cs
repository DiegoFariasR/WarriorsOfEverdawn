using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Main;

// The sellers standing in the allied town, and the trade done with them. The host holds every purse, so it decides
// every purchase: the buyer's machine asks, the host works out what the seller offers that player from what it sees
// them carry and wear, takes the cost and answers. A weapon it names in the answer, and the buyer's machine puts it
// in its player's hands; armour the host puts on the player itself, since it is the host that reckons the blows. Present on every machine at the same path, for its RPCs. Design: Docs/Design/trade.md.
public partial class Market : Node3D
{
    public const string NodeName = "Market";

    // Deliver's slot for a weapon that goes wherever a bought weapon goes, its weapon when there is none to hand
    // over, and its armour when the thing was not armour.
    private const int NoSlot = -1;
    private const string NoWeapon = "";
    private const int NoArmour = -1;

    private readonly List<SellerNpc> _sellers = new();

    // On the buyer's machine: the host sold it this, or refused and why.
    public event Action<SellerDefinition, TradeItem>? Bought;

    public event Action<BuyOutcome>? Refused;

    // Host only: a sale went through, to this peer.
    public event Action<long, SellerDefinition, TradeItem>? Sold;

    public IReadOnlyList<SellerNpc> Sellers => _sellers;

    public static Market In(SceneTree tree) => tree.CurrentScene.GetNode<Market>(NodeName);

    // A seller for every spot the town's layout gives one.
    public void Build(ArenaMap map)
    {
        foreach (var spot in map.SellerSpots)
        {
            var seller = SellerNpc.Create(spot);
            _sellers.Add(seller);
            AddChild(seller);
        }
    }

    // The nearest seller close enough to trade with.
    public SellerNpc? InReachOf(Vector3 position) =>
        _sellers.Where(s => s.DistanceTo(position) <= TradeRules.Reach).MinBy(s => s.DistanceTo(position));

    // From the buyer's machine. The host answers that machine alone, with Deliver or Refuse.
    public void Buy(SellerDefinition seller, TradeItem item) => RpcId(1, MethodName.RequestBuy, seller.Id, item.Id);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestBuy(string sellerId, string itemId)
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

        var seller = _sellers.FirstOrDefault(s => s.Seller.Id == sellerId);
        var buyer = PlayerCharacter.Find(GetTree(), peer);
        var item = buyer == null ? null : seller?.Seller.Item(buyer.AsBuyer(), itemId);
        if (seller == null || item == null || buyer == null)
        {
            GD.PushError($"[Market] peer {peer} asked for '{itemId}' from '{sellerId}': no such seller, offer or player here");
            return;
        }

        // The position the buyer last reported, as for hits on players (Docs/Design/multiplayer.md).
        var outcome = buyer.Vitals.Buy(item, seller.DistanceTo(buyer.NetPosition));
        if (outcome == BuyOutcome.Bought)
        {
            Sold?.Invoke(peer, seller.Seller, item);
            RpcId(peer, MethodName.Deliver, sellerId, itemId, item.Weapon?.Id ?? NoWeapon, item.Slot is { } slot ? (int)slot : NoSlot, item.Armour?.Tier ?? NoArmour);
        }
        else
        {
            RpcId(peer, MethodName.Refuse, (int)outcome);
        }
    }

    // A weapon comes by id from the host, who took the cost for exactly that one; the slot is the one it replaces
    // the weapon of, for an improvement. Armour comes as its tier, and the host has already put it on.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Deliver(string sellerId, string itemId, string weaponId, int slot, int armourTier)
    {
        var seller = Core.Trade.Sellers.ById(sellerId);
        var weapon = weaponId == NoWeapon ? null : Weapons.ById(weaponId);
        WeaponSlot? into = slot == NoSlot ? null : (WeaponSlot)slot;
        if (PlayerCharacter.Find(GetTree(), Multiplayer.GetUniqueId()) is not { } buyer)
        {
            GD.PushError($"[Market] bought '{itemId}' from '{sellerId}', but this machine's player is gone");
            return;
        }

        // The offer as the host judged it, for what it was and cost: made to the buyer as it stood before, with the
        // weapons it still has here and the tier of armour below the one it was sold. The tier worn may already
        // have changed on this machine, and on the host it has.
        var before = new Buyer(buyer.AsBuyer().Weapons, armourTier == NoArmour ? buyer.Vitals.Armour : armourTier - 1);
        var item = seller.Item(before, itemId) ?? new TradeItem(itemId, weapon?.Name ?? itemId, Cost.Nothing, weapon, into);
        if (weapon != null)
        {
            buyer.OnBought(weapon, into);
        }

        Bought?.Invoke(seller, item);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refuse(int outcome) => Refused?.Invoke((BuyOutcome)outcome);
}
