using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class AgendaView : UserControl
{
    private const int MaximumRows = 2500;
    private sealed record Row(string Title, string Detail, DateOnly Date, CalendarEvent? Event = null, CalendarTask? Task = null)
    {
        public bool IsHeading => Event is null && Task is null;
        public string AccessibleName => $"{Title}. {Detail}";
    }

    public AgendaView(IReadOnlyList<AgendaDay> days, Action<DateOnly> goToDay, Action<CalendarEvent> openEvent,
        Action<CalendarTask> openTask, Action<CalendarTask> completeTask)
    {
        var root = new DockPanel();
        Content = root;
        if (days.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "Nothing on the agenda for these two weeks. Add an event or give a task a due date.",
                Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 12, 8, 0)
            });
            return;
        }

        var totalRows = days.Sum(day => 1 + day.Events.Count + day.Tasks.Count);
        var rows = new List<Row>(Math.Min(totalRows, MaximumRows));
        foreach (var day in days)
        {
            if (rows.Count == MaximumRows) break;
            var events = $"{day.Events.Count:N0} {(day.Events.Count == 1 ? "event" : "events")}";
            var tasks = $"{day.Tasks.Count:N0} {(day.Tasks.Count == 1 ? "task" : "tasks")} due";
            rows.Add(new(day.Date.ToString("dddd, MMMM d"), $"{events} · {tasks}", day.Date));
            foreach (var item in day.Events.Take(MaximumRows - rows.Count))
            {
                var time = item.AllDay ? EventPresentation.Summary(item) : $"{item.Start:t} – {item.End:t}";
                if (item.End.Date != item.Start.Date && !item.AllDay) time = $"{item.Start:MMM d, t} – {item.End:MMM d, t}";
                var detail = $"{time} · {CalendarPalette.Name(item.CalendarId)}";
                if (!string.IsNullOrWhiteSpace(item.Location)) detail += $" · {item.Location}";
                rows.Add(new(item.Title, detail, day.Date, Event: item));
            }
            foreach (var task in day.Tasks.Take(MaximumRows - rows.Count))
                rows.Add(new(task.Title, task.IsImportant ? "Task due · Important" : "Task due", day.Date, Task: task));
        }

        if (totalRows > MaximumRows)
        {
            var limit = new TextBlock { Text = $"Showing the first {MaximumRows:N0} agenda rows. Use Day view to see a busier date.", Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(limit, Dock.Top); root.Children.Add(limit);
        }
        var list = PlanningList.Create("Two-week agenda", grouped: true);
        list.ItemsSource = rows;
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var open = new Button { Content = "_Open selected", IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
        var done = new Button { Content = "_Complete task", Visibility = Visibility.Collapsed };
        buttons.Children.Add(open); buttons.Children.Add(done);
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        root.Children.Add(list);

        void Open()
        {
            if (list.SelectedItem is not Row row) return;
            if (row.Event is { } item) openEvent(item);
            else if (row.Task is { } task) openTask(task);
            else goToDay(row.Date);
        }
        list.SelectionChanged += (_, _) =>
        {
            var row = list.SelectedItem as Row;
            open.IsEnabled = row is not null;
            open.Content = row?.Task is not null ? "_Edit task" : row?.Event is not null ? "_Open event" : "Open _day";
            done.Visibility = row?.Task is null ? Visibility.Collapsed : Visibility.Visible;
        };
        open.Click += (_, _) => Open();
        done.Click += (_, _) => { if ((list.SelectedItem as Row)?.Task is { } task) completeTask(task); };
        list.MouseDoubleClick += (_, args) =>
        {
            if (args.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(list, source) is ListBoxItem) Open();
        };
        list.KeyDown += (_, args) => { if (args.Key == Key.Enter) { args.Handled = true; Open(); } };
    }
}
