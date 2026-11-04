using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Daymark.Core;
using Microsoft.Win32;

namespace Daymark.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var testCalendar = args.Length == 1 && args[0] == "--test-calendar";
        if (args.Length > 0 && !testCalendar)
        {
            MessageBox.Show("Supported option: --test-calendar (opens a separate disposable calendar).", "Daymark");
            return;
        }
        using var instance = new Mutex(true, testCalendar ? "Local\\Daymark.Native.Calendar.Test" : "Local\\Daymark.Native.Calendar", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Daymark is already running. Use its existing window.", "Daymark");
            return;
        }
        var app = new Application();
        var dataDirectory = testCalendar ? Path.Combine(AppContext.BaseDirectory, "TestCalendar")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaymarkNative");
        var store = new CalendarStore(Path.Combine(dataDirectory, "calendar.json"));
        try { NativeTheme.Apply(app, Path.Combine(dataDirectory, "appearance.txt")); app.Run(new CalendarWindow(store, store.Load(), testCalendar)); }
        catch (Exception error)
        {
            MessageBox.Show($"Daymark could not start. Your calendar has not been replaced.\n\n{error.Message}\n\nData: {store.Path}\nPrevious save: {store.Path}.bak",
                "Daymark — startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed partial class CalendarWindow : Window
{
    private readonly CalendarStore store;
    private CalendarDocument document;
    private CalendarDocument displayed;
    private TimeZoneInfo displayZone = TimeZoneInfo.Local;
    private bool followDeviceZone = true;
    private readonly CalendarHistory history;
    private readonly MenuItem undoItem = new() { Command = ApplicationCommands.Undo };
    private readonly MenuItem redoItem = new() { Command = ApplicationCommands.Redo };
    private DateOnly selected = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly month;
    private readonly Grid days = new();
    private readonly ContentControl calendarContent = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch };
    private CalendarViewMode viewMode;
    private double weekOffset = 8 * 60;
    private readonly TextBlock monthTitle = new() { FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = NativeTheme.Ink,
        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock dayTitle = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = NativeTheme.Ink, Margin = new Thickness(0, 6, 0, 4) };
    private readonly ListBox agenda = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly TextBlock status = new() { Foreground = NativeTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox search = new() { MinWidth = 180, Padding = new Thickness(8), Margin = new Thickness(0, 0, 12, 0) };
    private readonly Button edit;
    private readonly Button delete;
    private bool refreshing;
    private DateOnly? renderedToday;
    private (DateOnly First, CalendarViewMode Mode)? renderedRange;
    private readonly System.Windows.Threading.DispatcherTimer searchDelay = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly HashSet<string> hiddenCalendars = new(StringComparer.Ordinal);
    private readonly System.Windows.Controls.WrapPanel calendarFilters = new();

    public CalendarWindow(CalendarStore store, CalendarDocument document, bool testCalendar = false)
    {
        this.store = store;
        history = new CalendarHistory(document);
        this.document = history.Current;
        displayed = this.document.InZone(displayZone);
        month = new DateOnly(selected.Year, selected.Month, 1);
        Title = "Daymark" + (testCalendar ? " — Test calendar" : "");
        Width = 1240; Height = 820; MinWidth = 860; MinHeight = 620;
        Background = NativeTheme.Background; Foreground = NativeTheme.Ink;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        edit = Button("_Edit event", (_, _) => Edit());
        delete = Button("_Delete", (_, _) => Delete());
        LoadViewPreferences(); BuildLayout(testCalendar); NativeTheme.Attach(this);
        InputBindings.Add(new KeyBinding(new ActionCommand(NewEvent), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(Export), Key.B, ModifierKeys.Control));
        Activated += (_, _) =>
        {
            NativeTheme.Refresh();
            var zoneChanged = false;
            if (followDeviceZone)
            {
                TimeZoneInfo.ClearCachedData();
                var currentZone = TimeZoneInfo.Local;
                zoneChanged = currentZone.Id != displayZone.Id || !currentZone.HasSameRules(displayZone);
                displayZone = currentZone;
            }
            if (zoneChanged || renderedToday != CalendarTime.Today(displayZone)) Refresh();
        };
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); RefreshAgenda(); NativeMotion.Reveal(agenda); };
        Closed += (_, _) => searchDelay.Stop();
        Loaded += async (_, _) => await LoadAccountsAsync();
        InputBindings.Add(new KeyBinding(new ActionCommand(() => { search.Focus(); search.SelectAll(); }), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(GoToDate), Key.G, ModifierKeys.Control));
        Refresh();
    }
    private static Button Button(string label, RoutedEventHandler action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(3, 0, 3, 0) };
        button.Click += action; return button;
    }
    private void ChangePeriod(int amount)
    {
        selected = CalendarNavigation.Move(selected, viewMode, amount);
        month = new(selected.Year, selected.Month, 1); search.Clear(); Refresh();
    }
    private void Refresh()
    {
        searchDelay.Stop();
        try { RefreshView(); }
        catch (Exception error) when (error is InvalidDataException or ArgumentOutOfRangeException)
        {
            refreshing = false; status.Text = error.Message;
            calendarContent.Content = new TextBlock { Text = "This view could not be expanded. Choose a shorter date range or check the calendar data.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Error };
            agenda.ItemsSource = null;
        }
    }
    private void RefreshView()
    {
        refreshing = true;
        renderedToday = CalendarTime.Today(displayZone);
        var visibleFirst = viewMode == CalendarViewMode.Month ? month.AddDays(-(((int)month.DayOfWeek + 6) % 7))
            : viewMode == CalendarViewMode.Day ? selected : WeekLayout.StartOfWeek(selected);
        var nextRange = (visibleFirst, viewMode);
        var transition = renderedRange is not null && renderedRange != nextRange;
        displayed = Recurrence.InRange(ViewDocument, visibleFirst, viewMode == CalendarViewMode.Month ? 42 : viewMode == CalendarViewMode.Day ? 1 : 7, displayZone, hiddenCalendars);
        CalendarPalette.Set(ViewDocument.CalendarList); RefreshCalendarFilters();
        if (calendarContent.Content is WeekView current) weekOffset = current.VerticalOffset;
        if (viewMode != CalendarViewMode.Month)
        {
            var first = WeekLayout.StartOfWeek(selected);
            monthTitle.Text = viewMode == CalendarViewMode.Day ? selected.ToString("ddd, MMM d, yyyy")
                : $"{first:MMM d} – {first.AddDays(6):MMM d, yyyy}";
            monthTitle.FontSize = 20;
            calendarContent.Content = new WeekView(displayed, selected,
                date => { selected = date; month = new(date.Year, date.Month, 1); search.Clear(); Refresh(); },
                item => ShowEditor(item),
                (date, time) => { selected = date; month = new(date.Year, date.Month, 1); search.Clear(); Refresh(); ShowEditor(null, time); },
                weekOffset, viewMode == CalendarViewMode.Day, CalendarTime.Today(displayZone));
        }
        else
        {
            calendarContent.Content = days;
            monthTitle.FontSize = 26;
            RefreshMonth();
        }
        refreshing = false; RefreshAgenda(); status.Text = $"Saved on this PC · {CalendarTime.ZoneId(displayZone)}" + CacheStatus;
        undoItem.Header = history.CanUndo ? $"_Undo {history.UndoDescription}" : "_Undo";
        redoItem.Header = history.CanRedo ? $"_Redo {history.RedoDescription}" : "_Redo";
        CommandManager.InvalidateRequerySuggested();
        renderedRange = nextRange;
        if (transition) NativeMotion.Reveal(calendarContent);
    }
    private void SelectDay(DateOnly date)
    {
        if (selected == date && search.Text.Length == 0) return;
        selected = date; search.Clear(); searchDelay.Stop();
        if (viewMode == CalendarViewMode.Month && date.Year == month.Year && date.Month == month.Month)
        {
            foreach (var cell in days.Children.OfType<MonthDayButton>()) cell.SelectDay(cell.Date == date);
            RefreshAgenda(); NativeMotion.Reveal(agenda);
        }
        else { month = new(date.Year, date.Month, 1); Refresh(); }
    }
    private void RefreshMonth()
    {
        days.Children.Clear(); monthTitle.Text = month.ToString("MMMM yyyy");
        var labels = new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
        for (var i = 0; i < 7; i++) { var label = new TextBlock { Text = labels[i], FontSize = 10, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 0, 4, 12), Foreground = NativeTheme.Muted }; Grid.SetColumn(label, i); days.Children.Add(label); }
        var start = month.AddDays(-(((int)month.DayOfWeek + 6) % 7));
        var visibleDays = displayed.Days(start, 42);
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var items = visibleDays[i];
            var button = new MonthDayButton(date, date.Month == month.Month, date == selected, date == renderedToday, items,
                () => SelectDay(date));
            Grid.SetRow(button, i / 7 + 1); Grid.SetColumn(button, i % 7); days.Children.Add(button);
        }
    }
    private void RefreshAgenda()
    {
        try { RefreshAgendaView(); }
        catch (InvalidDataException error)
        {
            agenda.ItemsSource = null; agenda.Visibility = Visibility.Collapsed; emptyAgenda.Visibility = Visibility.Visible;
            emptyTitle.Text = "Refine your search"; emptyDescription.Text = error.Message; edit.IsEnabled = delete.IsEnabled = false;
        }
    }
    private void RefreshAgendaView()
    {
        if (refreshing) return;
        var query = search.Text.Trim();
        dayTitle.Text = query.Length == 0 ? selected.ToString("dddd, MMM d") : "Search results";
        var items = query.Length == 0 ? displayed.On(selected).ToList() : CalendarSearch.Find(ViewDocument, query, selected, displayZone, hiddenCalendars).ToList();
        agenda.ItemsSource = items;
        agendaCount.Text = query.Length > 0 ? $"{items.Count}{(items.Count == 200 ? "+" : "")} results\nRepeats: past month + next year"
            : $"{items.Count} {(items.Count == 1 ? "event" : "events")}";
        agenda.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        emptyAgenda.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        emptyTitle.Text = query.Length > 0 ? "No matching events" : "Nothing scheduled";
        emptyDescription.Text = query.Length > 0 ? "Try another title, location, or note. Repeating events are searched from one month ago through the next year." : hiddenCalendars.Count > 0 ? "Check your visible calendars or add an event." : "Add an event to plan this day.";
        edit.IsEnabled = delete.IsEnabled = false;
    }
    private void NewEvent() => ShowEditor(null);
    private void Edit() { if (agenda.SelectedItem is CalendarEvent item) ShowEditor(item); }
    private void ShowEditor(CalendarEvent? original, TimeOnly? initialTime = null)
    {
        if (original is not null && IsProviderEvent(original)) { ShowProviderEvent(original); return; }
        if (original?.SeriesId is { } series)
        {
            var scope = new SeriesScopeDialog("Edit") { Owner = this };
            if (scope.ShowDialog() != true) return;
            original = scope.EntireSeries ? document.Events.Single(e => e.Id == series) : CalendarChanges.SourceOccurrence(document, original);
        }
        else if (original is not null) original = document.Events.Single(e => e.Id == original.Id);
        var draft = original;
        var selectionBeforeEdit = selected;
        while (true)
        {
            var editor = new EventEditor(selected, draft, initialTime, displayZone, document.CalendarList, document.CalendarList.FirstOrDefault(c => !hiddenCalendars.Contains(c.Id))?.Id,
                new EventDefaults(Path.GetDirectoryName(store.Path)!).Minutes) { Owner = this };
            if (editor.ShowDialog() != true || editor.Result is null) return;
            draft = editor.Result;
            var reset = original is not null && document.Events.Any(e => e.SeriesId == original.Id) && CalendarChanges.ScheduleChanged(original, draft);
            if (reset && MessageBox.Show(this, "Changing this series schedule resets its individual occurrence changes and skipped occurrences. Continue? You can undo the whole change.",
                "Change series schedule", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) continue;
            CalendarDocument next;
            try { next = CalendarChanges.SaveEvent(document, draft, reset); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Invalid event", MessageBoxButton.OK, MessageBoxImage.Warning); continue; }
            if (!Save(next, original is null ? "create event" : draft.SeriesId is not null ? "edit occurrence" : "edit event")) continue;
            selected = original is not null && draft.Recurrence is not null ? selectionBeforeEdit : DateOnly.FromDateTime(CalendarTime.InZone(draft, displayZone).Start); month = new(selected.Year, selected.Month, 1);
            search.Clear(); Refresh(); agenda.Focus(); return;
        }
    }
    private bool Save(CalendarDocument next, string description)
    {
        try { history.Commit(next, description, store.Save); document = history.Current; return true; }
        catch (Exception error) { MessageBox.Show(this, $"Your change was not saved.\n\n{error.Message}", "Save failed", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    private void NavigateHistory(bool redo)
    {
        var description = redo ? history.RedoDescription : history.UndoDescription;
        try
        {
            var changed = redo ? history.Redo(store.Save) : history.Undo(store.Save);
            if (!changed) return;
            document = history.Current; Refresh();
            status.Text = $"{(redo ? "Redid" : "Undid")} {description} · Saved on this PC";
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"Your calendar and undo history have not changed.\n\n{error.Message}",
                "Could not save history change", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void Delete()
    {
        if (agenda.SelectedItem is not CalendarEvent item) return;
        if (IsProviderEvent(item)) { ShowProviderEvent(item); return; }
        var entire = true;
        if (item.SeriesId is not null)
        {
            var scope = new SeriesScopeDialog("Delete") { Owner = this };
            if (scope.ShowDialog() != true) return;
            entire = scope.EntireSeries;
        }
        var label = item.SeriesId is not null ? entire ? "the entire repeating series" : "this occurrence" : $"“{item.Title}”";
        if (MessageBox.Show(this, $"Delete {label}? You can undo this change.", "Delete event", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var next = item.SeriesId is not null && !entire ? CalendarChanges.CancelOccurrence(document, item)
            : CalendarChanges.DeleteEventOrSeries(document, item.SeriesId ?? item.Id);
        if (Save(next, entire ? "delete event or series" : "skip occurrence")) Refresh();
    }
    private void Export()
    {
        var picker = new SaveFileDialog { FileName = "daymark-native-backup.json", Filter = "Daymark native backup (*.json)|*.json" };
        if (picker.ShowDialog(this) != true) return;
        if (SameDataPath(picker.FileName)) { MessageBox.Show(this, "Choose a backup location outside the app's data files."); return; }
        try { CalendarStore.Export(document, picker.FileName); status.Text = "Backup saved"; }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private bool SameDataPath(string path) => string.Equals(Path.GetFullPath(path), Path.GetFullPath(store.Path), StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetFullPath(path), Path.GetFullPath(store.Path + ".bak"), StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetFullPath(path), Path.GetFullPath(store.Path + ".v1.bak"), StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetFullPath(path), Path.GetFullPath(store.Path + ".v2.bak"), StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(path).StartsWith(Path.Combine(Path.GetDirectoryName(store.Path)!, "credentials") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(path).StartsWith("accounts.sqlite", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetDirectoryName(store.Path), StringComparison.OrdinalIgnoreCase);
    private void Restore()
    {
        var picker = new OpenFileDialog { Filter = "Daymark native backup (*.json)|*.json" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var incoming = CalendarStore.Read(picker.FileName);
            if (MessageBox.Show(this, "Replace your current native calendar with this backup? Export a backup first if you need the current events.", "Restore calendar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (Save(incoming, "restore backup")) { search.Clear(); Refresh(); }
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

public sealed class EventDescription(Func<bool>? includeDate = null) : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var e = (CalendarEvent)value;
        return (includeDate?.Invoke() == true ? $"{e.Start:ddd, MMM d, yyyy}\n" : "") + EventPresentation.Summary(e) + $"\n{CalendarPalette.Name(e.CalendarId)}" + (e.SeriesId is null ? "" : " · Repeating") + (string.IsNullOrWhiteSpace(e.Location) ? "" : $"\n{e.Location}");
    }
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
public sealed class ActionCommand(Action action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
