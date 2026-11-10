using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class EventMoveDialog : Window
{
    public DateOnly DestinationDate { get; private set; }
    public TimeOnly DestinationTime { get; private set; }

    public EventMoveDialog(CalendarEvent item, TimeZoneInfo displayZone)
    {
        Title = "Move event";
        Width = 460; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Surface; Foreground = NativeTheme.Ink; FontSize = 14;
        NativeTheme.Attach(this);
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = item.Title, FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        var detail = item.SeriesId is null ? "Choose a new start. The event keeps its duration." : "Only this occurrence moves. It keeps its duration.";
        if (!item.AllDay && item.TimeZoneId is not null) detail += $" Times use {displayZone.DisplayName}.";
        panel.Children.Add(new TextBlock { Text = detail, Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) });
        var date = new DatePicker { SelectedDate = item.Start.Date, DisplayDateStart = new DateTime(2, 1, 1), DisplayDateEnd = new DateTime(9998, 12, 31) };
        panel.Children.Add(new Label { Content = "_New date", Target = date });
        panel.Children.Add(date);
        var clock = new TextBox { Text = item.Start.ToString("HH:mm", CultureInfo.InvariantCulture), MaxLength = 5 };
        if (!item.AllDay)
        {
            panel.Children.Add(new Label { Content = "Start _time (24-hour)", Target = clock });
            panel.Children.Add(clock);
        }
        var error = new TextBlock { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(error);
        date.DateValidationError += (_, args) => { args.ThrowException = false; date.SelectedDate = null; error.Text = "Choose a valid date."; };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        actions.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var move = new Button { Content = "_Move event", IsDefault = true, Style = NativeTheme.Style("PrimaryButton") };
        actions.Children.Add(move); panel.Children.Add(actions);
        move.Click += (_, _) =>
        {
            if (date.SelectedDate is not { } day) { error.Text = "Choose a valid date."; return; }
            var time = TimeOnly.MinValue;
            if (!item.AllDay && !TimeOnly.TryParseExact(clock.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
            { error.Text = "Enter a 24-hour time such as 14:30."; return; }
            DestinationDate = DateOnly.FromDateTime(day);
            DestinationTime = item.AllDay ? time : TimeOnly.FromDateTime(CalendarTime.KeepSubminutePrecision(day.Date + time.ToTimeSpan(), item.Start));
            DialogResult = true;
        };
    }
}
