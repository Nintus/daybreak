using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private string? filterSignature;
    private void RefreshCalendarFilters()
    {
        var signature = string.Join("\n", document.CalendarList.Select(c => $"{c.Id}|{c.Name}|{c.Color}|{hiddenCalendars.Contains(c.Id)}"));
        if (signature == filterSignature) return;
        filterSignature = signature; calendarFilters.Children.Clear();
        foreach (var calendar in document.CalendarList)
        {
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = NativeTheme.CalendarColor(calendar.Color), Margin = new Thickness(0, 0, 7, 0) });
            label.Children.Add(new TextBlock { Text = calendar.Name, MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = NativeTheme.Ink, VerticalAlignment = VerticalAlignment.Center });
            var toggle = new CheckBox { Content = label, IsChecked = !hiddenCalendars.Contains(calendar.Id), Margin = new Thickness(5, 7, 18, 7),
                ToolTip = "Show or hide this calendar (view filter only)" };
            System.Windows.Automation.AutomationProperties.SetName(toggle, $"Show {calendar.Name}");
            void Filter(bool visible)
            {
                if (visible) hiddenCalendars.Remove(calendar.Id); else hiddenCalendars.Add(calendar.Id);
                SaveViewPreferences(); Refresh();
            }
            toggle.Checked += (_, _) => Filter(true); toggle.Unchecked += (_, _) => Filter(false); calendarFilters.Children.Add(toggle);
        }
    }
    private string ViewPreferencesPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(store.Path)!, "hidden-calendars.json");
    private void LoadViewPreferences()
    {
        try
        {
            if (System.IO.File.Exists(ViewPreferencesPath) && new System.IO.FileInfo(ViewPreferencesPath).Length < 50000)
                hiddenCalendars.UnionWith(System.Text.Json.JsonSerializer.Deserialize<string[]>(System.IO.File.ReadAllText(ViewPreferencesPath)) ?? []);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
    }
    private void SaveViewPreferences()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ViewPreferencesPath)!);
            var temporary = ViewPreferencesPath + ".tmp";
            System.IO.File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(hiddenCalendars.Where(id => document.CalendarList.Any(c => c.Id == id)).ToArray()));
            System.IO.File.Move(temporary, ViewPreferencesPath, true);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, $"The view filter works for this session, but could not be saved.\n\n{error.Message}", "Calendar visibility"); }
    }
    private void ManageCalendars()
    {
        var dialog = new CalendarsDialog(document) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } calendars)
            if (Save(document with { Version = 3, Calendars = calendars }, "manage calendars")) Refresh();
    }
}
