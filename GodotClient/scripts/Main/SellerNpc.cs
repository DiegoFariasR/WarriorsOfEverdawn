using System;
using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Trade;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// A seller standing on its spot: the figure, its name over its head, and the prompt to trade while this machine's
// player is in reach and free to. Looks only: the body that blocks is ArenaMap's, and the trade is Market's.
public partial class SellerNpc : Node3D
{
    public const string PromptText = "E  -  trade";

    private const float NameHeight = 2.55f;
    private const float PromptHeight = 2.95f;
    private const float LabelPixel = 0.008f;

    private static readonly Dictionary<string, string> Models = new()
    {
        [Sellers.Weaponsmith.Id] = "res://assets/characters/Barbarian.glb",
        [Sellers.Blacksmith.Id] = "res://assets/characters/Engineer.glb",
        [Sellers.Merchant.Id] = "res://assets/characters/Rogue_Hooded.glb",
        [Sellers.Enchanter.Id] = "res://assets/characters/Mage.glb",
    };

    private Label3D _prompt = null!;

    public SellerDefinition Seller { get; private set; } = null!;

    public bool PromptShown => _prompt.Visible;

    public static SellerNpc Create(SellerSpot spot)
    {
        if (!Models.TryGetValue(spot.Seller.Id, out string? model))
        {
            throw new InvalidOperationException($"No figure for the seller '{spot.Seller.Id}'");
        }

        var npc = new SellerNpc { Name = spot.Seller.Id, Seller = spot.Seller, Position = spot.Position };
        var body = Assets.Instantiate(model);
        ToonLook.Apply(body);

        // KayKit models face +Z, which is the way a layout's yaw points.
        body.Rotation = new Vector3(0f, spot.Yaw, 0f);
        npc.AddChild(body);
        CharacterRig.ShrinkHead(body);

        // Libraries go in before the figure enters the tree; playing first would crash (Everdawn godot-pitfalls.md).
        var animation = new AnimationPlayer { Name = "AnimationPlayer" };
        RigAnimations.AddTo(animation);
        body.AddChild(animation);
        animation.Autoplay = RigAnimations.UnarmedIdle;

        npc.AddChild(Sign(spot.Seller.Name, UiTheme.GoldHi, NameHeight, 44));
        npc._prompt = Sign(PromptText, Colors.White, PromptHeight, 36);
        npc._prompt.Visible = false;
        npc.AddChild(npc._prompt);
        return npc;
    }

    public float DistanceTo(Vector3 position)
    {
        var offset = position - GlobalPosition;
        return new Vector2(offset.X, offset.Z).Length();
    }

    public override void _Process(double delta)
    {
        var local = PlayerCharacter.Find(GetTree(), Multiplayer.GetUniqueId());
        _prompt.Visible = local is { IsDowned: false, IsTrading: false } && DistanceTo(local.GlobalPosition) <= TradeRules.Reach;
    }

    private static Label3D Sign(string text, Color color, float height, int size) => new()
    {
        Text = text,
        Font = UiTheme.Words,
        FontSize = size,
        OutlineSize = 10,
        Modulate = color,
        OutlineModulate = Colors.Black,
        PixelSize = LabelPixel,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
        Position = Vector3.Up * height,
    };
}
