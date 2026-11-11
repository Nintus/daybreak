using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class TasksDialog : Window
{
    private sealed record FilterChoice(TaskFilter Filter, string Label);
    private sealed record Row(CalendarTask Task, string Title, string Detail)
    {
        public string AccessibleName => $"{Title}. {Detail}";
    }
    private readonly Func<CalendarDocument> current;
    private readonly Func<CalendarDocument, string, bool> save;
    private readonly Func<string> defaultCalendar;
    private readonly TimeZoneInfo zone;
    private readonly DateOnly selectedDate;
    private readonly DateOnly today;
    private readonly int defaultMinutes;
    private readonly ListBox list = PlanningList.Create("Task list");
    private readonly ComboBox filter = new() { DisplayMemberPath = nameof(FilterChoice.Label), MinWidth = 130 };
    private readonly TextBox search = new() { MaxLength = 200, MinWidth = 180, Tag = "Search tasks…" };
    private readonly TextBlock summary = new() { Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock error = new() { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap };
    private readonly Button complete = new();
    private readonly Button schedule = new();
    private readonly Button unlink = new() { Content = "Unlink time block" };
    private readonly Button edit = new() { Content = "_Edit task" };
    private readonly Button remove = new() { Content = "_Delete task" };
    private CalendarDocument? indexed;
    private Dictionary<Guid, CalendarEvent> timeBlocks = [];

    public TasksDialog(Func<CalendarDocument> current, Func<CalendarDocument, string, bool> save,
        Func<string> defaultCalendar, DateOnly selectedDate, TimeZoneInfo zone, int defaultMinutes)
    {
        this.current = current;
        this.save = save;
        this.defaultCalendar = defaultCalendar;
        this.selectedDate = selectedDate;
        this.zone = zone;
        this.defaultMinutes = defaultMinutes;
        today = CalendarTime.Today(zone);
        Title = "Tasks";
        Width = 720; Height = Math.Min(610, SystemParameters.WorkArea.Height); MinWidth = 515; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Surface; Foreground = NativeTheme.Ink; FontSize = 14;
        NativeTheme.Attach(this);

        var root = new DockPanel { Margin = new Thickness(24) };
        Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock { Text = "Tasks", FontSize = 24, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "Capture work, set a deadline, or reserve time on your calendar.", Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 10) });
        var filters = new WrapPanel();
        filter.ItemsSource = new[] { new FilterChoice(TaskFilter.AllOpen, "All open"), new(TaskFilter.Today, "Today & overdue"), new(TaskFilter.Upcoming, "Upcoming"), new(TaskFilter.Inbox, "Inbox"), new(TaskFilter.Completed, "Completed") };
        filter.SelectedIndex = 0;
        filter.Margin = new Thickness(0, 0, 8, 5);
        search.Margin = new Thickness(0, 0, 8, 5);
        System.Windows.Automation.AutomationProperties.SetName(filter, "Task filter");
        System.Windows.Automation.AutomationProperties.SetName(search, "Search tasks");
        var add = new Button { Content = "＋ _New task", Style = NativeTheme.Style("PrimaryButton"), Margin = new Thickness(0, 0, 0, 5) };
        add.Click += (_, _) => Edit(null);
        filters.Children.Add(filter); filters.Children.Add(search); filters.Children.Add(add);
        header.Children.Add(filters);
        header.Children.Add(summary);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);

        var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(error);
        var buttons = new WrapPanel();
        var close = new Button { Content = "Close", IsCancel = true };
        foreach (var button in new[] { complete, schedule, edit, unlink, remove, close })
        {
            button.Margin = new Thickness(0, 4, 6, 0);
            buttons.Children.Add(button);
        }
        footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(list);

        filter.SelectionChanged += (_, _) => Render();
        search.TextChanged += (_, _) => Render();
        list.SelectionChanged += (_, _) => UpdateActions();
        list.MouseDoubleClick += (_, _) => { if (Selected is { } task) Edit(task); };
        list.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter || Selected is not { } task) return;
            args.Handled = true;
            Edit(task);
        };
        edit.Click += (_, _) => { if (Selected is { } task) Edit(task); };
        complete.Click += (_, _) =>
        {
            if (Selected is { } task) Commit(() => CalendarTasks.Save(current(), task with { IsCompleted = !task.IsCompleted }), task.IsCompleted ? "reopen task" : "complete task", task.Id);
        };
        schedule.Click += (_, _) => { if (Selected is { } task) EditBlock(task); };
        unlink.Click += (_, _) =>
        {
            if (Selected is { } task) Commit(() => CalendarTasks.Save(current(), task with { ScheduledEventId = null }), "unlink task time block", task.Id);
        };
        remove.Click += (_, _) => Remove();
        Render();
    }

    private CalendarTask? Selected => (list.SelectedItem as Row)?.Task;

    private void Render(Guid? preferred = null)
    {
        var keep = preferred ?? Selected?.Id;
        var snapshot = current();
        var query = search.Text.Trim();
        var selectedFilter = (filter.SelectedItem as FilterChoice)?.Filter ?? TaskFilter.AllOpen;
        if (!ReferenceEquals(indexed, snapshot))
        {
            var blockIds = snapshot.TaskList.Select(task => task.ScheduledEventId).OfType<Guid>().ToHashSet();
            timeBlocks = snapshot.Events.Where(item => blockIds.Contains(item.Id)).ToDictionary(item => item.Id);
            indexed = snapshot;
        }
        var rows = CalendarTasks.Filter(snapshot.TaskList, selectedFilter, today)
            .Where(task => query.Length == 0 || task.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || task.Notes.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(task =>
            {
                var detail = task.DueDate is { } due ? $"{(!task.IsCompleted && due < today ? "Overdue" : "Due")} {due:ddd, MMM d, yyyy}" : "No due date";
                if (task.ScheduledEventId is { } id && timeBlocks.TryGetValue(id, out var block))
                    detail += $" · Time block {CalendarTime.InZone(block, zone).Start:g}";
                return new Row(task, task.IsImportant ? "★ " + task.Title : task.Title, detail);
            }).ToList();
        list.ItemsSource = rows;
        if (keep is not null) list.SelectedItem = rows.FirstOrDefault(row => row.Task.Id == keep);
        summary.Text = rows.Count == 0 ? "No tasks match this view." : $"{rows.Count:N0} {(rows.Count == 1 ? "task" : "tasks")}";
        UpdateActions();
    }

    private void UpdateActions()
    {
        var task = Selected;
        edit.IsEnabled = remove.IsEnabled = complete.IsEnabled = task is not null;
        complete.Content = task?.IsCompleted == true ? "_Reopen" : "_Complete";
        unlink.IsEnabled = task?.ScheduledEventId is not null;
        schedule.IsEnabled = task is not null && (!task.IsCompleted || task.ScheduledEventId is not null);
        schedule.Content = task?.ScheduledEventId is null ? "_Schedule time…" : "Edit time _block…";
    }

    private void Commit(Func<CalendarDocument> change, string description, Guid? preferred = null)
    {
        try
        {
            if (!save(change(), description)) return;
            error.Text = "";
            Render(preferred);
        }
        catch (InvalidDataException exception) { error.Text = exception.Message; }
    }

    private void Edit(CalendarTask? task)
    {
        var dialog = new TaskEditor(task, today) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } result) return;
        if (task is null) { filter.SelectedIndex = 0; search.Clear(); }
        Commit(() => CalendarTasks.Save(current(), result), task is null ? "add task" : "edit task", result.Id);
    }

    private void EditBlock(CalendarTask task)
    {
        var snapshot = current();
        var block = snapshot.Events.SingleOrDefault(item => item.Id == task.ScheduledEventId);
        var date = task.DueDate is { } due && due >= today ? due : selectedDate;
        var start = date.ToDateTime(new TimeOnly(9, 0));
        var preset = block is null ? new CalendarEvent(Guid.NewGuid(), task.Title, start, start.AddMinutes(defaultMinutes), CalendarId: defaultCalendar()) : null;
        var editor = new EventEditor(date, block, defaultZone: zone, calendars: snapshot.CalendarList,
            defaultCalendarId: defaultCalendar(), preset: preset, timeBlock: true) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is not { } result) return;
        if (block is null) Commit(() => CalendarTasks.Schedule(current(), task.Id, result), "schedule task", task.Id);
        else Commit(() => CalendarChanges.SaveEvent(current(), result), "edit task time block", task.Id);
    }

    private void Remove()
    {
        if (Selected is not { } task) return;
        var note = task.ScheduledEventId is null ? "" : " Its time block will stay on the calendar.";
        if (MessageBox.Show(this, $"Delete “{task.Title}”?{note}", "Delete task", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            Commit(() => CalendarTasks.Remove(current(), task.Id), "delete task");
    }
}
