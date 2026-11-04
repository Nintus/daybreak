using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class RecurrenceFields : StackPanel
{
    private readonly ComboBox frequency = new() { ItemsSource = new[] { "Does not repeat", "Daily", "Weekly", "Monthly", "Yearly" }, SelectedIndex = 0 };
    private readonly TextBox interval = new() { Text = "1", Width = 65, MaxLength = 3 };
    private readonly StackPanel details = new();
    private readonly WrapPanel weekdays = new();
    private readonly CheckBox[] days = new CheckBox[7];
    private readonly ComboBox monthly = new() { ItemsSource = new[] { "On this day of the month", "On this ordinal weekday", "On the last weekday of this name" }, SelectedIndex = 0 };
    private readonly ComboBox ending = new() { ItemsSource = new[] { "Never", "After a number of occurrences", "On a date (inclusive)" }, SelectedIndex = 0 };
    private readonly TextBox count = new() { Text = "10", MaxLength = 5 };
    private readonly DatePicker until = new();

    public RecurrenceFields(RecurrenceRule? rule, DateTime start, bool occurrence)
    {
        Children.Add(new TextBlock { Text = occurrence ? "This occurrence belongs to a repeating series." : "Repeat", Margin = new Thickness(0, 12, 0, 5), Foreground = NativeTheme.Muted });
        if (occurrence) return;
        Children.Add(frequency); Children.Add(details);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(new TextBlock { Text = "Every", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) }); row.Children.Add(interval); details.Children.Add(row);
        var labels = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        foreach (var day in new[] { 1, 2, 3, 4, 5, 6, 0 })
        {
            days[day] = new CheckBox { Content = labels[day], Margin = new Thickness(0, 8, 12, 4), IsChecked = rule?.Weekdays is > 0 ? (rule.Weekdays & (1 << day)) != 0 : day == (int)start.DayOfWeek };
            weekdays.Children.Add(days[day]);
        }
        details.Children.Add(weekdays); monthly.Margin = new Thickness(0, 8, 0, 0); details.Children.Add(monthly);
        details.Children.Add(new TextBlock { Text = "Ends", Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 5) });
        details.Children.Add(ending); count.Margin = until.Margin = new Thickness(0, 7, 0, 0); details.Children.Add(count); details.Children.Add(until);
        frequency.SelectedIndex = rule is null ? 0 : (int)rule.Frequency + 1; interval.Text = (rule?.Interval ?? 1).ToString();
        monthly.SelectedIndex = (int)(rule?.MonthlyPattern ?? MonthlyPattern.DayOfMonth);
        ending.SelectedIndex = rule?.Count is not null ? 1 : rule?.Until is not null ? 2 : 0;
        count.Text = (rule?.Count ?? 10).ToString(); until.SelectedDate = (rule?.Until ?? DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, DateOnly.FromDateTime(start).DayNumber + 90))).ToDateTime(TimeOnly.MinValue);
        UpdateStart(start);
        frequency.SelectionChanged += (_, _) => Update(); ending.SelectionChanged += (_, _) => Update(); Update();
        until.DateValidationError += (_, args) => { args.ThrowException = false; until.SelectedDate = null; };
        System.Windows.Automation.AutomationProperties.SetName(frequency, "Repeat frequency");
        System.Windows.Automation.AutomationProperties.SetName(interval, "Repeat interval");
        System.Windows.Automation.AutomationProperties.SetName(ending, "Repeat ending");
        System.Windows.Automation.AutomationProperties.SetName(count, "Occurrence count");
        System.Windows.Automation.AutomationProperties.SetName(until, "Last recurrence date");
        System.Windows.Automation.AutomationProperties.SetName(monthly, "Monthly pattern");
    }
    public void UpdateStart(DateTime start)
    {
        var index = monthly.SelectedIndex;
        var ordinal = (start.Day - 1) / 7 + 1;
        var suffix = ordinal switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", _ => "fifth" };
        monthly.ItemsSource = new[] { $"On day {start.Day} of the month", $"On the {suffix} {start:dddd}", $"On the last {start:dddd}" };
        monthly.SelectedIndex = Math.Max(0, index);
    }
    private void Update()
    {
        details.Visibility = frequency.SelectedIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        weekdays.Visibility = frequency.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        monthly.Visibility = frequency.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        count.Visibility = ending.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        until.Visibility = ending.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }
    public RecurrenceRule? Read(DateTime start)
    {
        if (frequency.SelectedIndex == 0) return null;
        if (!int.TryParse(interval.Text, out var step)) throw new InvalidDataException("Enter a repeat interval between 1 and 999.");
        int? total = null;
        if (ending.SelectedIndex == 1)
        {
            if (!int.TryParse(count.Text, out var value)) throw new InvalidDataException("Enter the number of occurrences.");
            total = value;
        }
        DateOnly? last = null;
        if (ending.SelectedIndex == 2) last = until.SelectedDate is { } date ? DateOnly.FromDateTime(date) : throw new InvalidDataException("Choose the last repeat date.");
        var mask = frequency.SelectedIndex == 2 ? Enumerable.Range(0, 7).Where(d => days[d].IsChecked == true).Sum(d => 1 << d) : 0;
        if (frequency.SelectedIndex == 2 && mask == 0) throw new InvalidDataException("Choose at least one weekday.");
        var rule = new RecurrenceRule((RecurrenceFrequency)(frequency.SelectedIndex - 1), step, mask,
            frequency.SelectedIndex == 3 ? (MonthlyPattern)monthly.SelectedIndex : MonthlyPattern.DayOfMonth, total, last);
        rule.Validate(start); return rule;
    }
}
