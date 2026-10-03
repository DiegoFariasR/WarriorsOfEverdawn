using System.Collections.Generic;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// A seller standing on its spot: the figure, and whether this machine's player is in reach and free to trade
// with it. Its name and the prompt are the HUD's to write (SellerLabels). Looks only: the body that blocks is
// ArenaMap's, and the trade is Market's.
public partial class SellerNpc : Node3D
{
    public const string PromptText = "E  -  trade";

    // The name stands this high over the seller's feet, clear of a body this wide as the camera sees it
    // (Overhead).
    public const float NameHeight = 2.55f;
    public const float Girth = 0.7f;

    private static readonly Dictionary<string, CharacterLook> Looks = new()
    {
        [Sellers.Weaponsmith.Id] = CharacterLook.Of("Barbarian", "BearHat"),
        [Sellers.Blacksmith.Id] = CharacterLook.Of("Engineer", "Backpack", "Goggles"),
        [Sellers.Merchant.Id] = CharacterLook.Of("RogueHooded", "Cape", "Mask"),
        [Sellers.Enchanter.Id] = CharacterLook.Of("Mage", "Cape", "Hat"),
    };

    public SellerDefinition Seller { get; private set; } = null!;

    // This machine's player is in reach and free to trade: the prompt is up.
    public bool PromptShown { get; private set; }

    public static CharacterLook LookOf(SellerDefinition seller) =>
        Looks.TryGetValue(seller.Id, out var look) ? look : throw new KeyNotFoundException($"No figure for the seller '{seller.Id}'");

    public static SellerNpc Create(SellerSpot spot)
    {
        var look = LookOf(spot.Seller);
        var npc = new SellerNpc { Name = spot.Seller.Id, Seller = spot.Seller, Position = spot.Position };
        npc.AddToGroup(ArenaMap.OnAFloor);
        var body = CharacterBody.Build(look);

        // KayKit models face +Z, which is the way a layout's yaw points.
        body.Rotation = new Vector3(0f, spot.Yaw, 0f);
        npc.AddChild(body);

        RigAnimations.AddPlayerTo(body).Autoplay = RigAnimations.UnarmedIdle;

        return npc;
    }

    public float DistanceTo(Vector3 position) => Yaw.AcrossFloor(position, GlobalPosition);

    public override void _Process(double delta)
    {
        var local = PlayerCharacter.Local(GetTree());
        PromptShown = local is { IsDowned: false, IsTrading: false } && DistanceTo(local.GlobalPosition) <= TradeRules.Reach;
    }
}
