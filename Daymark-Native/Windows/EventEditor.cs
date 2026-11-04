using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly ComboBox zone = new() { ItemsSource = ZoneChoice.All, DisplayMemberPath = "Label", SelectedValuePath = "Id", IsTextSearchEnabled = true };
    private readonly ComboBox startOccurrence = OccurrencePicker();
    private readonly ComboBox endOccurrence = OccurrencePicker();
    private readonly TextBox location = new() { MaxLength = 1000 };
    private readonly TextBox notes = new() { MaxLength = 10000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock error = new() { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap };

    public EventEditor(DateOnly selected, CalendarEvent? original, TimeOnly? initialTime = null, TimeZoneInfo? defaultZone = null)
    {
        NativeTheme.Attach(this);
        Title = original is null ? "New event" : "Edit event";
        Width = 560; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 14;
        Background = NativeTheme.Surface; Foreground = NativeTheme.Ink;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = Title, FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        Label Field(string label, Control input, Panel? container = null)
        {
            container ??= panel;
            var caption = new Label { Content = label, Target = input, Padding = new Thickness(0), Margin = new Thickness(0, 10, 0, 5) };
            container.Children.Add(caption);
            input.Padding = new Thickness(7); container.Children.Add(input);
            return caption;
        }
        void DateAndTime(string dateLabel, DatePicker date, string timeLabel, TextBox time)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            var left = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; var right = new StackPanel();
            Grid.SetColumn(right, 1); row.Children.Add(left); row.Children.Add(right); panel.Children.Add(row);
            Field(dateLabel, date, left); Field(timeLabel, time, right);
            time.ToolTip = "24-hour time, for example 09:00 or 14:30";
        }
        foreach (var date in new[] { startDate, endDate }) date.Loaded += (_, _) =>
        {
            if (date.Template.FindName("PART_TextBox", date) is DatePickerTextBox text) text.Style = NativeTheme.Style("DateField");
        };
        Field("_Title", title);
        DateAndTime("Start _date", startDate, "Start ti_me · 24-hour", startTime);
        DateAndTime("End da_te", endDate, "End tim_e · 24-hour", endTime);
        allDay.Margin = new Thickness(0, 14, 0, 0); panel.Children.Add(allDay);
        allDay.Checked += (_, _) => { startTime.IsEnabled = endTime.IsEnabled = false; if (endDate.SelectedDate <= startDate.SelectedDate) endDate.SelectedDate = startDate.SelectedDate?.AddDays(1); };
        allDay.Unchecked += (_, _) => startTime.IsEnabled = endTime.IsEnabled = true;
        var allDayHelp = new TextBlock { Text = "End date is exclusive: choose the day after the last included day.", FontSize = 12,
            Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        panel.Children.Add(allDayHelp);
        Field("Time _zone", zone); zone.ToolTip = "Changing this keeps entered clock times and changes the event's instants.";
        var startOccurrenceLabel = Field("Start occurrence — this hour happens twice", startOccurrence);
        var endOccurrenceLabel = Field("End occurrence — this hour happens twice", endOccurrence);
        panel.Children.Add(new TextBlock { Text = "Floating events keep the entered clock time in every display zone.",
            FontSize = 12, Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
        Field("_Location", location); Field("_Notes", notes); panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(6, 0, 0, 0) };
        var save = new Button { Content = "_Save event", IsDefault = true, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(6, 0, 0, 0) };
        save.Style = NativeTheme.Style("PrimaryButton");
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        defaultZone ??= TimeZoneInfo.Local;
        var sourceZone = original?.TimeZoneId is string id ? CalendarTime.FindZone(id) : defaultZone;
        var sourceEvent = original is null ? null : CalendarTime.InZone(original, sourceZone);
        var start = sourceEvent?.Start ?? selected.ToDateTime(initialTime ?? new TimeOnly(9, 0));
        var end = sourceEvent?.End ?? start.AddHours(1);
        title.Text = original?.Title ?? ""; location.Text = original?.Location ?? ""; notes.Text = original?.Notes ?? "";
        startDate.SelectedDate = start.Date; endDate.SelectedDate = end.Date;
        startTime.Text = start.ToString("HH:mm"); endTime.Text = end.ToString("HH:mm"); allDay.IsChecked = original?.AllDay ?? false;
        zone.SelectedIndex = original is not null && !original.AllDay && original.TimeZoneId is null ? 0
            : ZoneChoice.All.ToList().FindIndex(option => option.Id == CalendarTime.ZoneId(sourceZone));
        if (zone.SelectedIndex < 0)
        {
            zone.ItemsSource = ZoneChoice.All.Append(new ZoneChoice(CalendarTime.ZoneId(sourceZone), sourceZone.DisplayName)).ToArray();
            zone.SelectedValue = CalendarTime.ZoneId(sourceZone);
        }
        startOccurrence.SelectedIndex = CalendarTime.ChoiceFor(original?.StartUtc, sourceZone) is { } startChoice ? (int)startChoice + 1 : 0;
        endOccurrence.SelectedIndex = CalendarTime.ChoiceFor(original?.EndUtc, sourceZone) is { } endChoice ? (int)endChoice + 1 : 0;
        bool Repeated(DatePicker date, TextBox time)
        {
            if (allDay.IsChecked == true || zone.SelectedItem is not ZoneChoice { Id: not null } choice ||
                date.SelectedDate is not DateTime day || !TimeOnly.TryParseExact(time.Text.Trim(), "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var clock)) return false;
            return CalendarTime.FindZone(choice.Id).IsAmbiguousTime(DateOnly.FromDateTime(day).ToDateTime(clock));
        }
        void UpdateOccurrences()
        {
            zone.IsEnabled = allDay.IsChecked != true;
            allDayHelp.Visibility = allDay.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            startOccurrence.Visibility = startOccurrenceLabel.Visibility = Repeated(startDate, startTime) ? Visibility.Visible : Visibility.Collapsed;
            endOccurrence.Visibility = endOccurrenceLabel.Visibility = Repeated(endDate, endTime) ? Visibility.Visible : Visibility.Collapsed;
        }
        startDate.SelectedDateChanged += (_, _) => UpdateOccurrences(); endDate.SelectedDateChanged += (_, _) => UpdateOccurrences();
        startTime.TextChanged += (_, _) => UpdateOccurrences(); endTime.TextChanged += (_, _) => UpdateOccurrences();
        zone.SelectionChanged += (_, _) => { startOccurrence.SelectedIndex = endOccurrence.SelectedIndex = 0; UpdateOccurrences(); };
        allDay.Checked += (_, _) => UpdateOccurrences(); allDay.Unchecked += (_, _) => UpdateOccurrences();
        UpdateOccurrences();
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
                var fromClock = DateOnly.FromDateTime(from).ToDateTime(fromTime);
                var toClock = DateOnly.FromDateTime(to).ToDateTime(toTime);
                if (allDay.IsChecked != true)
                {
                    fromClock = CalendarTime.KeepSubminutePrecision(fromClock, sourceEvent?.Start);
                    toClock = CalendarTime.KeepSubminutePrecision(toClock, sourceEvent?.End);
                }
                var item = new CalendarEvent(original?.Id ?? Guid.NewGuid(), title.Text.Trim(), fromClock,
                    toClock, allDay.IsChecked == true, location.Text.Trim(), notes.Text);
                if (!item.AllDay && zone.SelectedItem is not ZoneChoice) throw new InvalidOperationException("Choose an event time zone or Floating.");
                if (!item.AllDay && zone.SelectedItem is ZoneChoice { Id: not null } choice)
                    item = CalendarTime.Anchor(item, CalendarTime.FindZone(choice.Id),
                        startOccurrence.SelectedIndex > 0 ? (RepeatedTimeChoice)(startOccurrence.SelectedIndex - 1) : null,
                        endOccurrence.SelectedIndex > 0 ? (RepeatedTimeChoice)(endOccurrence.SelectedIndex - 1) : null);
                item.Validate(); Result = item; DialogResult = true;
            }
            catch (Exception problem) { error.Text = problem.Message; }
        };
    }
    private static ComboBox OccurrencePicker() => new() { ItemsSource = new[] { "Choose an occurrence…", "First occurrence (earlier instant)", "Second occurrence (later instant)" }, SelectedIndex = 0 };
}
