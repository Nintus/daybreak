using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private string? filterSignature;
    private sealed record SetChoice(string Name, Guid? Id = null, bool All = false);
    private readonly ComboBox calendarSetPicker = new() { DisplayMemberPath = nameof(SetChoice.Name), Width = 152 };
    private CalendarViewSettings visibility = CalendarViewSettings.Empty;
    private CalendarViewSettings? renderedSets;
    private bool changingSetPicker;
    private CalendarViewSettingsStore ViewSettingsStore => new(Path.GetDirectoryName(store.Path)!);
    private string DefaultCalendarId() => visibility.DefaultCalendar(document.CalendarList, hiddenCalendars);

    private void BuildCalendarSetPicker()
    {
        calendarSetPicker.Margin = new Thickness(6, 0, 6, 0);
        calendarSetPicker.VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetName(calendarSetPicker, "Calendar set");
        calendarSetPicker.SelectionChanged += (_, _) =>
        {
            if (changingSetPicker || calendarSetPicker.SelectedItem is not SetChoice choice) return;
            SaveViewPreferences(visibility with { ActiveSetId = choice.Id, CustomHidden = choice.All ? [] : visibility.CustomHidden });
        };
    }

    private void ApplyCalendarVisibility()
    {
        hiddenCalendars.Clear();
        hiddenCalendars.UnionWith(visibility.HiddenCalendars(ViewDocument.CalendarList));
        if (ReferenceEquals(renderedSets, visibility)) return;
        changingSetPicker = true;
        try
        {
            var choices = new List<SetChoice> { new("All calendars", All: true), new("Custom visibility") };
            choices.AddRange(visibility.Sets.Select(set => new SetChoice(set.Name, set.Id)));
            calendarSetPicker.ItemsSource = choices;
            calendarSetPicker.SelectedItem = visibility.ActiveSetId is { } id ? choices.Single(choice => choice.Id == id)
                : visibility.CustomHidden.Length == 0 ? choices[0] : choices[1];
            renderedSets = visibility;
        }
        finally { changingSetPicker = false; }
    }

    private void RefreshCalendarFilters()
    {
        var signature = string.Join("\n", ViewDocument.CalendarList.Select(c => $"{c.Id}|{c.Name}|{c.Color}|{hiddenCalendars.Contains(c.Id)}"));
        if (signature == filterSignature) return;
        filterSignature = signature; calendarFilters.Children.Clear();
        foreach (var calendar in ViewDocument.CalendarList)
        {
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = NativeTheme.CalendarColor(calendar.Color), Margin = new Thickness(0, 0, 7, 0) });
            label.Children.Add(new TextBlock { Text = calendar.Name, MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = NativeTheme.Ink, VerticalAlignment = VerticalAlignment.Center });
            var toggle = new CheckBox { Content = label, IsChecked = !hiddenCalendars.Contains(calendar.Id), Margin = new Thickness(5, 7, 18, 7),
                ToolTip = "Show or hide this calendar (view filter only)" };
            System.Windows.Automation.AutomationProperties.SetName(toggle, $"Show {calendar.Name}");
            void Filter(bool visible)
            {
                var next = new HashSet<string>(hiddenCalendars, StringComparer.Ordinal);
                if (visible) next.Remove(calendar.Id); else next.Add(calendar.Id);
                SaveViewPreferences(visibility with { ActiveSetId = null, CustomHidden = next.Order(StringComparer.Ordinal).ToArray() });
            }
            toggle.Checked += (_, _) => Filter(true); toggle.Unchecked += (_, _) => Filter(false); calendarFilters.Children.Add(toggle);
        }
    }
    private void LoadViewPreferences()
    {
        try { visibility = ViewSettingsStore.Load(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { }
    }

    private void SaveViewPreferences(CalendarViewSettings next)
    {
        try
        {
            next.Validate();
            visibility = next;
            ViewSettingsStore.Save(next);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            renderedSets = null;
            MessageBox.Show(this, $"Calendar visibility preferences could not be saved.\n\n{error.Message}", "Calendar sets");
        }
        Refresh();
    }

    private void ManageCalendarSets()
    {
        var dialog = new CalendarSetsDialog(visibility, ViewDocument.CalendarList, document.CalendarList, hiddenCalendars) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } result) SaveViewPreferences(result);
    }
    private void ManageCalendars()
    {
        var dialog = new CalendarsDialog(document) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } calendars)
            if (Save(document with { Version = 3, Calendars = calendars }, "manage calendars")) Refresh();
    }
}
