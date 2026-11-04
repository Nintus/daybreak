using System.Windows;
using System.Windows.Controls;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private void GoToDate()
    {
        var dialog = new Window { Title = "Go to date", Owner = this, Width = 340, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = NativeTheme.Surface, Foreground = NativeTheme.Ink, FontSize = 14 };
        NativeTheme.Attach(dialog);
        var panel = new StackPanel { Margin = new Thickness(24) }; dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Go to date", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) });
        var date = new DatePicker { SelectedDate = selected.ToDateTime(TimeOnly.MinValue), DisplayDateStart = new DateTime(2, 1, 1), DisplayDateEnd = new DateTime(9998, 12, 31) };
        System.Windows.Automation.AutomationProperties.SetName(date, "Go to date"); panel.Children.Add(date);
        var error = new TextBlock { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap }; panel.Children.Add(error);
        date.DateValidationError += (_, args) => { args.ThrowException = false; date.SelectedDate = null; error.Text = "Choose a valid date."; };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        actions.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var go = new Button { Content = "_Go", IsDefault = true, Style = NativeTheme.Style("PrimaryButton") }; actions.Children.Add(go); panel.Children.Add(actions);
        go.Click += (_, _) =>
        {
            if (date.SelectedDate is not { Year: >= 2 and <= 9998 }) { error.Text = "Choose a valid date between years 2 and 9998."; return; }
            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true) return;
        selected = DateOnly.FromDateTime(date.SelectedDate!.Value); month = new(selected.Year, selected.Month, 1); search.Clear(); Refresh();
    }
}
