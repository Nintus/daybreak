using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Daymark.Windows;

internal sealed class ThemeDialog : Window
{
    private readonly ThemeSettings original = NativeTheme.Settings;
    private bool saved, updating;
    private readonly ComboBox preset = new() { MinWidth = 180 };
    private readonly ComboBox basis = new() { ItemsSource = Enum.GetValues<ThemeBase>(), MinWidth = 120 };
    private readonly TextBox hex = new() { MinWidth = 110, MaxLength = 7 };
    private readonly TextBlock error = new() { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel custom = new();
    public ThemeDialog()
    {
        Title = "Theme studio"; Width = 620; Height = Math.Min(740, SystemParameters.WorkArea.Height - 32); MinWidth = 500; MinHeight = 440;
        Background = NativeTheme.Background; Foreground = NativeTheme.Ink; WindowStartupLocation = WindowStartupLocation.CenterOwner; NativeTheme.Attach(this);
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) }; footer.Children.Add(actions);
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(4), Padding = new Thickness(14, 8, 14, 8) }; actions.Children.Add(cancel);
        var apply = new Button { Content = "Save theme", Style = NativeTheme.Style("PrimaryButton"), Margin = new Thickness(4), Padding = new Thickness(14, 8, 14, 8) }; actions.Children.Add(apply);
        apply.Click += (_, _) => { if (!UpdatePreview()) return; try { NativeTheme.Save(Draft()); saved = true; DialogResult = true; } catch (Exception) { error.Text = "The theme could not be saved. Check access to the app data folder."; } };
        Closing += (_, _) => { if (!saved) NativeTheme.Preview(original); };
        var form = new StackPanel(); root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        form.Children.Add(new TextBlock { Text = "Make Daymark yours", FontSize = 26, FontWeight = FontWeights.SemiBold });
        form.Children.Add(new TextBlock { Text = "Choose a palette, or start with a favorite color.", Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 20) });
        if (SystemParameters.HighContrast) form.Children.Add(new TextBlock { Text = "Windows high contrast is active. Your theme will be saved, but Windows colors take priority.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        Label(form, "THEME");
        preset.ItemsSource = Enum.GetValues<AppThemeMode>().Select(m => new Choice(m, NativeTheme.Name(m))).ToArray(); preset.DisplayMemberPath = "Name"; preset.SelectedValuePath = "Mode"; preset.SelectedValue = original.Mode; form.Children.Add(preset);
        System.Windows.Automation.AutomationProperties.SetName(preset, "Theme preset");
        form.Children.Add(custom); custom.Margin = new Thickness(0, 18, 0, 0);
        Label(custom, "BASE"); basis.SelectedItem = original.Base; custom.Children.Add(basis); System.Windows.Automation.AutomationProperties.SetName(basis, "Custom theme base");
        Label(custom, "ACCENT COLOR");
        var colorRow = new StackPanel { Orientation = Orientation.Horizontal }; custom.Children.Add(colorRow);
        hex.Text = original.Accent; colorRow.Children.Add(hex); System.Windows.Automation.AutomationProperties.SetName(hex, "Accent color hex");
        var pick = new Button { Content = "Choose color…", Margin = new Thickness(10, 0, 0, 0) }; colorRow.Children.Add(pick);
        pick.Click += (_, _) =>
        {
            var current = NativeTheme.Settings.Accent;
            if (ColorPicker.Choose(this, ThemePalette.Parse(current)) is { } color) hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        };
        var swatches = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) }; custom.Children.Add(swatches);
        foreach (var color in new[] { "#7C83FF", "#48B5EC", "#51BCA0", "#D6A65C", "#E48DAF", "#BA91ED", "#EA7A67", "#8996A8" })
        {
            var button = new Button { Width = 36, Height = 30, Margin = new Thickness(0, 0, 8, 8), ToolTip = color, Background = new SolidColorBrush(ThemePalette.Parse(color)) };
            // A swatch uses its own template so theme hover colors never obscure its color.
            button.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='2' CornerRadius='7'/></ControlTemplate>");
            button.BorderBrush = NativeTheme.Line; System.Windows.Automation.AutomationProperties.SetName(button, "Use accent " + color);
            button.GotKeyboardFocus += (_, _) => button.BorderBrush = NativeTheme.Ink; button.LostKeyboardFocus += (_, _) => button.BorderBrush = NativeTheme.Line;
            button.Click += (_, _) => hex.Text = color; swatches.Children.Add(button);
        }
        form.Children.Add(PreviewCard());
        form.Children.Add(new TextBlock { Text = "Changes preview across the app. Cancel restores your previous theme. OLED black uses #000000 for the canvas and calendar surfaces; event colors and outlines stay visible.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 16, 0, 0) });
        preset.SelectionChanged += (_, _) => UpdatePreview(); basis.SelectionChanged += (_, _) => UpdatePreview(); hex.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }
    private sealed record Choice(AppThemeMode Mode, string Name);
    private ThemeSettings Draft()
    {
        var mode = (AppThemeMode)preset.SelectedValue;
        var accent = hex.Text.Trim().ToUpperInvariant();
        if (mode != AppThemeMode.Custom && !System.Text.RegularExpressions.Regex.IsMatch(accent, "^#[0-9A-F]{6}$")) accent = NativeTheme.Settings.Accent;
        return new(mode, accent, (ThemeBase)basis.SelectedItem);
    }
    private bool UpdatePreview()
    {
        if (updating) return false;
        updating = true;
        try
        {
            var draft = Draft(); custom.Visibility = draft.Mode == AppThemeMode.Custom ? Visibility.Visible : Visibility.Collapsed;
            // Incomplete custom text never changes the last valid preview or saved preference.
            ThemePalette.Parse(draft.Accent); NativeTheme.Preview(draft); error.Text = ""; return true;
        }
        catch (ArgumentException e) { error.Text = e.Message; return false; }
        finally { updating = false; }
    }
    private static void Label(Panel parent, string text) => parent.Children.Add(new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 0, 0, 8) });
    private static Border PreviewCard()
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "YOUR DAY, AT A GLANCE", Foreground = NativeTheme.Muted, FontSize = 11 });
        panel.Children.Add(new TextBlock { Text = "A little room to focus", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 14) });
        panel.Children.Add(new Border { Background = NativeTheme.Soft, BorderBrush = NativeTheme.Accent, BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(14),
            Child = new TextBlock { Text = "Design review\n10:00 – 11:00 · Studio", Foreground = NativeTheme.AccentInk, LineHeight = 24 } });
        panel.Children.Add(new TextBlock { Text = "Preview only · no event will be created", Foreground = NativeTheme.Muted, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) });
        return new Border { Child = panel, Background = NativeTheme.Surface, BorderBrush = NativeTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 22, 0, 0) };
    }
}

internal static class ColorPicker
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ChooseColorData
    {
        public int Size; public IntPtr Owner, Instance; public uint Result; public IntPtr CustomColors; public uint Flags; public IntPtr Data, Hook, Template;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChooseColorW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColor(ref ChooseColorData data);
    public static Color? Choose(Window owner, Color color)
    {
        var memory = Marshal.AllocHGlobal(16 * sizeof(int));
        try
        {
            Marshal.Copy(Enumerable.Repeat(0x00FFFFFF, 16).ToArray(), 0, memory, 16);
            var data = new ChooseColorData { Size = Marshal.SizeOf<ChooseColorData>(), Owner = new System.Windows.Interop.WindowInteropHelper(owner).Handle,
                Result = (uint)(color.R | color.G << 8 | color.B << 16), CustomColors = memory, Flags = 3 };
            return ChooseColor(ref data) ? Color.FromRgb((byte)data.Result, (byte)(data.Result >> 8), (byte)(data.Result >> 16)) : null;
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
}
