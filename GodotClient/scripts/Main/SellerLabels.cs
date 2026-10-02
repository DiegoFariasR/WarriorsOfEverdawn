using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Each seller's name over its head, and over the one this machine's player can trade with, the prompt above the
// name. Text on the screen, placed each frame from where the seller stands (Overhead), so it is as large and as
// clear of the seller from above as from behind: as text in the world it shrank to a few pixels under the
// orthographic cameras, and by height alone the two lines lay on each other.
public partial class SellerLabels : Control
{
    private readonly Dictionary<SellerNpc, Entry> _labels = new();

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null || GetTree().CurrentScene.GetNodeOrNull<Market>(Market.NodeName) is not { } market)
        {
            return;
        }

        foreach (var seller in market.Sellers)
        {
            if (!_labels.TryGetValue(seller, out var entry))
            {
                entry = _labels[seller] = Create(seller.Seller.Name);
            }

            entry.Prompt.Visible = seller.PromptShown;

            // A box outside a container keeps its size when its content shrinks.
            entry.Box.ResetSize();
            var over = Overhead.Point(camera, seller.GlobalPosition, SellerNpc.NameHeight, SellerNpc.Girth);
            entry.Box.Visible = !camera.IsPositionBehind(over);
            var at = camera.UnprojectPosition(over);
            entry.Box.Position = new Vector2(at.X - entry.Box.Size.X / 2f, at.Y - entry.Box.Size.Y);
        }
    }

    private Entry Create(string sellerName)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        var prompt = UiTheme.MakeLabel(SellerNpc.PromptText, UiTheme.Words, 13, Colors.White, outline: 4);
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        var name = UiTheme.MakeLabel(sellerName, UiTheme.Words, 17, UiTheme.GoldHi, outline: 5);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(prompt);
        box.AddChild(name);
        AddChild(box);
        return new Entry(box, prompt);
    }

    private sealed record Entry(VBoxContainer Box, Label Prompt);
}
