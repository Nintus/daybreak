namespace Daymark.Core;

public sealed record AgendaDay(DateOnly Date, IReadOnlyList<CalendarEvent> Events, IReadOnlyList<CalendarTask> Tasks);

public static class CalendarAgenda
{
    public const int Days = 14;

    public static IReadOnlyList<AgendaDay> Group(CalendarDocument projected, IReadOnlyList<CalendarTask> tasks, DateOnly first)
    {
        var from = first.ToDateTime(TimeOnly.MinValue);
        var through = from.AddDays(Days);
        var events = projected.Events.Where(item => !item.IsCancelled && item.Start < through && item.End > from)
            .ToLookup(item => item.Start < from ? first : DateOnly.FromDateTime(item.Start));
        var deadlines = tasks.Where(task => !task.IsCompleted && task.DueDate.HasValue).ToLookup(task => task.DueDate!.Value);

        var result = new List<AgendaDay>();
        for (var offset = 0; offset < Days; offset++)
        {
            var date = first.AddDays(offset);
            var dayEvents = events[date].OrderByDescending(item => item.AllDay).ThenBy(item => item.Start).ThenBy(item => item.Title).ToArray();
            var dayTasks = deadlines[date].OrderByDescending(task => task.IsImportant).ThenBy(task => task.Title).ToArray();
            if (dayEvents.Length + dayTasks.Length > 0) result.Add(new(date, dayEvents, dayTasks));
        }
        return result;
    }
}
