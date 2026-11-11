namespace Daymark.Core;

public sealed record CalendarTask(Guid Id, string Title, DateOnly? DueDate = null, bool IsCompleted = false,
    bool IsImportant = false, string Notes = "", Guid? ScheduledEventId = null)
{
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Title) || Title.Length > 300)
            throw new InvalidDataException("Give the task a title of at most 300 characters.");
        if (Notes is null || Notes.Length > 10000)
            throw new InvalidDataException("Task notes can be at most 10,000 characters.");
        if (DueDate is { Year: < 2 or > 9998 })
            throw new InvalidDataException("Choose a task due date between years 2 and 9998.");
    }
}

public enum TaskFilter { Today, Upcoming, Inbox, AllOpen, Completed }

public static class CalendarTasks
{
    public static IEnumerable<CalendarTask> Filter(IEnumerable<CalendarTask> tasks, TaskFilter filter, DateOnly today)
    {
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        return tasks.Where(task => filter switch
        {
            TaskFilter.Completed => task.IsCompleted,
            TaskFilter.Today => !task.IsCompleted && task.DueDate <= today,
            TaskFilter.Upcoming => !task.IsCompleted && task.DueDate > today,
            TaskFilter.Inbox => !task.IsCompleted && task.DueDate is null,
            _ => !task.IsCompleted
        }).OrderBy(task => task.DueDate is null).ThenBy(task => task.DueDate)
          .ThenByDescending(task => task.IsImportant).ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase);
    }

    public static CalendarDocument Save(CalendarDocument document, CalendarTask task)
    {
        var tasks = document.TaskList.ToList();
        var index = tasks.FindIndex(item => item.Id == task.Id);
        if (index < 0) tasks.Add(task);
        else tasks[index] = task;
        var next = document with { Tasks = tasks };
        next.Validate();
        return next;
    }

    public static CalendarDocument Schedule(CalendarDocument document, Guid taskId, CalendarEvent block)
    {
        var task = document.TaskList.SingleOrDefault(item => item.Id == taskId)
            ?? throw new InvalidDataException("This task no longer exists.");
        if (task.IsCompleted || task.ScheduledEventId is not null)
            throw new InvalidDataException("Choose an open task without a time block.");
        return Save(document.WithEvents(document.Events.Append(block)), task with { ScheduledEventId = block.Id });
    }

    public static CalendarDocument Remove(CalendarDocument document, Guid taskId)
    {
        var next = document with { Tasks = document.TaskList.Where(task => task.Id != taskId).ToList() };
        next.Validate();
        return next;
    }
}
