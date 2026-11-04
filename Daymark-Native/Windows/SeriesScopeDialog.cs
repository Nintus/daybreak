using System.Windows;
using System.Windows.Controls;

namespace Daymark.Windows;

internal sealed class SeriesScopeDialog : Window
{
    public bool EntireSeries { get; private set; }
    public SeriesScopeDialog(string verb)
    {
        Title = $"{verb} repeating event"; Width = 390; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = NativeTheme.Surface; Foreground = NativeTheme.Ink;
        FontSize = 14; NativeTheme.Attach(this);
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = $"{verb} repeating event", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(new TextBlock { Text = "Choose which events this change should affect.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 0, 0, 18) });
        void Choice(string text, bool all)
        {
            var button = new Button { Content = text, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(14, 10, 14, 10) };
            button.Click += (_, _) => { EntireSeries = all; DialogResult = true; }; panel.Children.Add(button);
        }
        Choice("This occurrence", false); Choice("Entire series", true);
        panel.Children.Add(new Button { Content = "Cancel", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right });
    }
}
