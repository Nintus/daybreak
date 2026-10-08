using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

public sealed class EventEditor : Window
{
    public CalendarEvent? Result { get; private set; }
    private readonly TextBox title = new() { MaxLength = 300 };
    private readonly DatePicker startDate = new();
    private readonly DatePicker endDate = new();
    private readonly TextBox startTime = new();
    private readonly TextBox endTime = new();
    private readonly CheckBox allDay = new() { Content = "_All day" };
    private readonly TextBox location = new() { MaxLength = 1000 };
    private readonly TextBox notes = new() { MaxLength = 10000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public EventEditor(DateOnly selected, CalendarEvent? original)
    {
        Title = original is null ? "New event" : "Edit event";
        Width = 490; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 14;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Field(string label, Control input)
        {
            panel.Children.Add(new Label { Content = label, Target = input, Padding = new Thickness(0), Margin = new Thickness(0, 10, 0, 5) });
            input.Padding = new Thickness(7); panel.Children.Add(input);
        }
        Field("_Title", title); Field("Start _date", startDate); Field("Start ti_me (24-hour HH:mm)", startTime);
        Field("End da_te (exclusive for all-day events)", endDate); Field("End tim_e (24-hour HH:mm)", endTime);
        allDay.Margin = new Thickness(0, 14, 0, 0); panel.Children.Add(allDay);
        allDay.Checked += (_, _) => { startTime.IsEnabled = endTime.IsEnabled = false; if (endDate.SelectedDate <= startDate.SelectedDate) endDate.SelectedDate = startDate.SelectedDate?.AddDays(1); };
        allDay.Unchecked += (_, _) => startTime.IsEnabled = endTime.IsEnabled = true;
        Field("_Location", location); Field("_Notes", notes); panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(6, 0, 0, 0) };
        var save = new Button { Content = "_Save event", IsDefault = true, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(6, 0, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        var start = original?.Start ?? selected.ToDateTime(new TimeOnly(9, 0));
        var end = original?.End ?? start.AddHours(1);
        title.Text = original?.Title ?? ""; location.Text = original?.Location ?? ""; notes.Text = original?.Notes ?? "";
        startDate.SelectedDate = start.Date; endDate.SelectedDate = end.Date;
        startTime.Text = start.ToString("HH:mm"); endTime.Text = end.ToString("HH:mm"); allDay.IsChecked = original?.AllDay ?? false;
        Loaded += (_, _) => title.Focus();
        save.Click += (_, _) =>
        {
            try
            {
                if (startDate.SelectedDate is not DateTime from || endDate.SelectedDate is not DateTime to)
                    throw new InvalidOperationException("Choose valid start and end dates.");
                TimeOnly fromTime = TimeOnly.MinValue, toTime = TimeOnly.MinValue;
                if (allDay.IsChecked != true && (!TimeOnly.TryParseExact(startTime.Text.Trim(), "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out fromTime)
                    || !TimeOnly.TryParseExact(endTime.Text.Trim(), "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out toTime)))
                    throw new InvalidOperationException("Use 24-hour times such as 09:00 or 14:30.");
                var item = new CalendarEvent(original?.Id ?? Guid.NewGuid(), title.Text.Trim(), DateOnly.FromDateTime(from).ToDateTime(fromTime),
                    DateOnly.FromDateTime(to).ToDateTime(toTime), allDay.IsChecked == true, location.Text.Trim(), notes.Text);
                item.Validate(); Result = item; DialogResult = true;
            }
            catch (Exception problem) { error.Text = problem.Message; }
        };
    }
}
