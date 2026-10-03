using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Theme;

// Everdawn's HUD look: dark wood panels with gold borders, cream text, Cinzel for words and Impact for numbers.
// Colours are Everdawn's EverdawnTheme values.
public static class UiTheme
{
    public static readonly Color WoodDk = new(0.118f, 0.071f, 0.039f);
    public static readonly Color Wood = new(0.227f, 0.141f, 0.094f);
    public static readonly Color Gold = new(0.831f, 0.659f, 0.259f);
    public static readonly Color GoldHi = new(1.000f, 0.847f, 0.471f);
    public static readonly Color GoldDk = new(0.541f, 0.392f, 0.094f);
    public static readonly Color TextMain = new(0.784f, 0.722f, 0.596f);
    public static readonly Color BarHp = new(0.769f, 0.220f, 0.220f);
    public static readonly Color BarMp = new(0.247f, 0.561f, 0.812f);
    public static readonly Color BarTrack = new(WoodDk, 0.95f);
    public static readonly Color DamageHarm = new(1.000f, 0.333f, 0.133f);
    public static readonly Color StatusFallen = new(0.800f, 0.300f, 0.300f);

    // This game's own, for souls wherever they are counted.
    public static readonly Color SoulText = new(0.72f, 0.9f, 1f);

    // This game's own too: Everdawn has no XP bar.
    public static readonly Color BarXp = new(0.56f, 0.36f, 0.82f);

    private static FontVariation? _words;
    private static FontFile? _numbers;

    // Cinzel, emboldened the way Everdawn's BoldFont is.
    public static FontVariation Words
    {
        get
        {
            if (_words == null)
            {
                _words = new FontVariation { BaseFont = Assets.Load<FontFile>("res://assets/fonts/Cinzel-Regular.ttf") };
                _words.SetVariationEmbolden(0.8f);
            }

            return _words;
        }
    }

    // Licence note: Impact is a Microsoft font; fine in a prototype, swap for an open one before shipping (Docs/Design/ui.md).
    public static FontFile Numbers => _numbers ??= Assets.Load<FontFile>("res://assets/fonts/Impact.ttf");

    public static StyleBoxFlat Panel(Color background, Color? border, int radius = 3, float margin = 4f, int borderWidth = 1)
    {
        int width = border.HasValue ? borderWidth : 0;
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border ?? Colors.Transparent,
            BorderWidthLeft = width,
            BorderWidthRight = width,
            BorderWidthTop = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = margin,
            ContentMarginRight = margin,
            ContentMarginTop = margin,
            ContentMarginBottom = margin,
            AntiAliasing = true,
        };
    }

    public static Label MakeLabel(string text, Font font, int size, Color color, int outline = 0)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (outline > 0)
        {
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", outline);
        }

        return label;
    }

    // A bar with its value written across it ("85 / 100"), like Everdawn's unit bars.
    public static (Control Root, ProgressBar Bar, Label Text) MakeValueBar(Color fill, Vector2 size, int textSize)
    {
        var root = new Control { CustomMinimumSize = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        var bar = new ProgressBar { ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bar.AddThemeStyleboxOverride("fill", Panel(fill, null, radius: 2, margin: 0f));
        bar.AddThemeStyleboxOverride("background", Panel(BarTrack, null, radius: 2, margin: 0f));
        root.AddChild(bar);

        var text = MakeLabel("", Numbers, textSize, Colors.White, outline: 3);
        text.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        root.AddChild(text);
        return (root, bar, text);
    }
}
