using System.Windows.Media;

namespace Daymark.Windows;

internal enum ThemeBase { Light, Dark, Oled }
internal sealed record ThemeSettings(AppThemeMode Mode = AppThemeMode.System, string Accent = "#7986FF", ThemeBase Base = ThemeBase.Dark);

internal static class ThemePalette
{
    public static Color Parse(string hex)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(hex ?? "", "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Use a six-digit color such as #7986FF.");
        return (Color)ColorConverter.ConvertFromString(hex!);
    }
    private static Color Mix(Color a, Color b, double ratio) => Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * ratio), (byte)Math.Round(a.G + (b.G - a.G) * ratio), (byte)Math.Round(a.B + (b.B - a.B) * ratio));
    private static double Luminance(Color c)
    {
        double Channel(byte value) { var n = value / 255d; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
        return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
    }
    public static double Contrast(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
    private static Color Readable(Color color, Color background, double minimum)
    {
        var target = Luminance(background) < .4 ? Colors.White : Colors.Black;
        for (var i = 0; Contrast(color, background) < minimum && i < 100; i++) color = Mix(color, target, .06);
        return color;
    }
    public static Dictionary<string, Color> Build(ThemeBase basis, string hex)
    {
        var seed = Parse(hex); var dark = basis != ThemeBase.Light; var oled = basis == ThemeBase.Oled;
        var background = oled ? Colors.Black : dark ? Mix(Color.FromRgb(18, 20, 25), seed, .035) : Mix(Color.FromRgb(247, 248, 250), seed, .025);
        var surface = oled ? Colors.Black : dark ? Mix(Color.FromRgb(28, 31, 38), seed, .045) : Colors.White;
        var soft = Mix(surface, seed, dark ? .18 : .1);
        var accent = Readable(seed, surface, 4.5);
        var onAccent = Contrast(Colors.Black, accent) >= Contrast(Colors.White, accent) ? Colors.Black : Colors.White;
        return new()
        {
            ["Background"] = background, ["Surface"] = surface,
            ["Ink"] = dark ? Color.FromRgb(239, 241, 246) : Color.FromRgb(27, 32, 43),
            ["Muted"] = Readable(Mix(surface, dark ? Colors.White : Colors.Black, .60), surface, 4.5),
            ["Line"] = Mix(surface, dark ? Colors.White : Colors.Black, oled ? .23 : .16),
            ["Accent"] = accent, ["OnAccent"] = onAccent,
            ["AccentInk"] = Readable(seed, soft, 4.5), ["Soft"] = soft,
            ["Subtle"] = oled ? Colors.Black : Mix(surface, seed, .035),
            ["Hover"] = Mix(surface, seed, dark ? .13 : .07), ["Pressed"] = Mix(surface, seed, dark ? .22 : .13),
            ["SelectedBorder"] = accent, ["Error"] = dark ? Color.FromRgb(255, 156, 150) : Color.FromRgb(180, 35, 24),
            ["Blue"] = dark ? Color.FromRgb(181, 208, 255) : Color.FromRgb(36, 84, 165),
            ["Teal"] = dark ? Color.FromRgb(131, 216, 200) : Color.FromRgb(22, 114, 105),
            ["TealSoft"] = Mix(surface, Color.FromRgb(22, 114, 105), dark ? .20 : .09),
            ["Violet"] = dark ? Color.FromRgb(202, 182, 255) : Color.FromRgb(114, 64, 200),
            ["Orange"] = dark ? Color.FromRgb(244, 183, 122) : Color.FromRgb(159, 80, 0),
            ["Rose"] = dark ? Color.FromRgb(255, 173, 199) : Color.FromRgb(172, 49, 93)
        };
    }
}
