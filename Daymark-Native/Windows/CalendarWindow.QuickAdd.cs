using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private void QuickAdd()
    {
        var dialog = new Window
        {
            Title = "Quick Add", Owner = this, Width = 520, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = NativeTheme.Surface, Foreground = NativeTheme.Ink, FontSize = 14
        };
        NativeTheme.Attach(dialog);
        var panel = new StackPanel { Margin = new Thickness(24) };
        dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Quick Add", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock
        {
            Text = "Enter a title and date, then add a time, repeat or reminder.\nLunch tomorrow at noon for 45m\nFocus next Monday at 9am every week remind 15m before",
            TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 14)
        });
        var input = new TextBox { MaxLength = 600, Padding = new Thickness(8) };
        System.Windows.Automation.AutomationProperties.SetName(input, "Event phrase");
        panel.Children.Add(input);
        var error = new TextBlock { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var review = new Button { Content = "_Review event…", IsDefault = true, Style = NativeTheme.Style("PrimaryButton") };
        buttons.Children.Add(review);
        panel.Children.Add(buttons);

        CalendarEvent? draft = null;
        review.Click += (_, _) =>
        {
            try
            {
                var duration = new EventDefaults(Path.GetDirectoryName(store.Path)!).Minutes;
                draft = QuickEventParser.Parse(input.Text, CalendarTime.Today(displayZone), duration, DefaultCalendarId());
                dialog.DialogResult = true;
            }
            catch (InvalidDataException exception) { error.Text = exception.Message; }
        };
        dialog.Loaded += (_, _) => input.Focus();
        if (dialog.ShowDialog() == true && draft is not null) ShowEditor(null, preset: draft);
    }
}
