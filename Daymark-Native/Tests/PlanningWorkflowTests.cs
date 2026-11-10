using Daymark.Core;
using Daymark.Migration;

internal static class PlanningWorkflowTests
{
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject, string folder)
    {
        var today = new DateOnly(2026, 11, 10);
        var start = today.ToDateTime(new TimeOnly(9, 0));
        var task = new CalendarTask(Guid.NewGuid(), "Finish proposal", today, IsImportant: true);
        var block = new CalendarEvent(Guid.NewGuid(), "Write proposal", start, start.AddHours(1));

        test("tasks round-trip in native backups and undo restores a previous format without losing events", () =>
        {
            var store = new CalendarStore(Path.Combine(folder, "tasks-calendar.json"));
            store.Save(new(3, [block with { ReminderMinutes = 10 }]));
            var prior = File.ReadAllText(store.Path);
            var history = new CalendarHistory(store.Load());
            history.Commit(CalendarTasks.Save(history.Current, task), "add task", store.Save);
            check(store.Load().Version == 5 && store.Load().TaskList.Single() == task && store.Load().Events.Single().ReminderMinutes == 10);
            check(File.ReadAllText(store.Path + ".v4.bak") == prior);
            var leaked = history.Current; leaked.Tasks!.Clear();
            check(history.Current.TaskList.Single() == task);
            check(history.Undo(store.Save) && store.Load().Version == 4 && store.Load().TaskList.Count == 0);
            check(history.Redo(store.Save) && store.Load().TaskList.Single() == task);
        });

        test("scheduling a task links one timed event and deleting either side keeps the other", () =>
        {
            var initial = CalendarTasks.Save(CalendarDocument.Empty(), task);
            var scheduled = CalendarTasks.Schedule(initial, task.Id, block);
            check(scheduled.Events.Single() == block && scheduled.TaskList.Single().ScheduledEventId == block.Id);
            reject(() => CalendarTasks.Schedule(scheduled, task.Id, block with { Id = Guid.NewGuid() }));
            reject(() => CalendarChanges.SaveEvent(scheduled, block with { Recurrence = new(RecurrenceFrequency.Weekly) }));
            var noBlock = CalendarChanges.DeleteEventOrSeries(scheduled, block.Id);
            check(noBlock.Events.Count == 0 && noBlock.TaskList.Single().ScheduledEventId is null);
            var noTask = CalendarTasks.Remove(scheduled, task.Id);
            check(noTask.TaskList.Count == 0 && noTask.Events.Single() == block);
            var history = new CalendarHistory(initial);
            history.Commit(scheduled, "schedule task", _ => { });
            check(history.Undo(_ => { }) && history.Current.Events.Count == 0 && history.Current.TaskList.Single().ScheduledEventId is null);
        });

        test("event import preserves tasks and their existing scheduled block", () =>
        {
            var scheduled = CalendarTasks.Schedule(CalendarTasks.Save(CalendarDocument.Empty(), task), task.Id, block);
            var other = new CalendarDefinition("imported", "Imported");
            var import = new CalendarImport(other, [block with { Id = Guid.NewGuid(), CalendarId = other.Id }], []);
            var merged = IcalendarImporter.Merge(scheduled, [import], out var added);
            check(added == 1 && merged.TaskList.Single().ScheduledEventId == block.Id && merged.Events.Contains(block));
        });

        test("task filters distinguish overdue, upcoming, undated and completed work", () =>
        {
            var old = task with { Id = Guid.NewGuid(), DueDate = today.AddDays(-1) };
            var later = task with { Id = Guid.NewGuid(), DueDate = today.AddDays(1) };
            var inbox = task with { Id = Guid.NewGuid(), DueDate = null };
            var done = task with { Id = Guid.NewGuid(), IsCompleted = true };
            var tasks = new[] { task, old, later, inbox, done };
            check(CalendarTasks.Filter(tasks, TaskFilter.Today, today).SequenceEqual([old, task]));
            check(CalendarTasks.Filter(tasks, TaskFilter.Upcoming, today).Single() == later);
            check(CalendarTasks.Filter(tasks, TaskFilter.Inbox, today).Single() == inbox);
            check(CalendarTasks.Filter(tasks, TaskFilter.Completed, today).Single() == done);
        });

        test("two-week agenda keeps an overlapping event once and places open deadlines on their date", () =>
        {
            var trip = block with { Id = Guid.NewGuid(), Start = start.Date.AddDays(-1), End = start.Date.AddDays(2), AllDay = true };
            var last = today.AddDays(CalendarAgenda.Days - 1);
            var finalTask = task with { Id = Guid.NewGuid(), DueDate = last };
            var hidden = block with { Id = Guid.NewGuid(), CalendarId = "hidden" };
            var document = new CalendarDocument(3, [trip, block, hidden], [CalendarDefinition.Personal, new("hidden", "Hidden")]);
            var projected = Recurrence.InRange(document, today, CalendarAgenda.Days, TimeZoneInfo.Utc, new HashSet<string> { "hidden" });
            var agenda = CalendarAgenda.Group(projected, [task, finalTask, task with { Id = Guid.NewGuid(), IsCompleted = true }, task with { Id = Guid.NewGuid(), DueDate = last.AddDays(1) }], today);
            check(agenda.Count == 2 && agenda[0].Date == today && agenda[0].Events.SequenceEqual([trip, block]) && agenda[0].Tasks.Single() == task);
            check(agenda[1].Date == last && agenda[1].Tasks.Single() == finalTask);
            check(CalendarNavigation.Move(today, CalendarViewMode.Agenda, 1) == last.AddDays(1));
        });

        test("calendar sets migrate legacy visibility and persist defaults across reload", () =>
        {
            var path = Path.Combine(folder, "view-settings");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, CalendarViewSettingsStore.LegacyFileName), "[\"work\"]");
            var store = new CalendarViewSettingsStore(path);
            var initial = store.Load();
            var work = new CalendarDefinition("work", "Work");
            var remote = new CalendarDefinition("connected", "Connected");
            var local = new[] { CalendarDefinition.Personal, work };
            var all = new[] { CalendarDefinition.Personal, work, remote };
            check(initial.HiddenCalendars(all).SetEquals([work.Id]));
            var set = new CalendarSet(Guid.NewGuid(), "Focus", [work.Id, remote.Id], work.Id, ShowTasks: false);
            store.Save(initial with { Sets = [set], ActiveSetId = set.Id });
            var loaded = store.Load();
            check(loaded.ActiveSet is { ShowTasks: false } && loaded.HiddenCalendars(all).SetEquals(["personal"]));
            check(loaded.DefaultCalendar(local, loaded.HiddenCalendars(all)) == work.Id);
            check((loaded with { ActiveSetId = null }).HiddenCalendars(all).SetEquals([work.Id]));
            reject(() => store.Save(loaded with { Sets = [set with { DefaultCalendarId = "personal" }] }));
            check(store.Load().ActiveSet?.DefaultCalendarId == work.Id);
        });

        test("moving an anchored event keeps its source zone and elapsed duration through a repeated hour", () =>
        {
            var source = CalendarTime.FindZone("America/Chicago");
            var display = CalendarTime.FindZone("America/New_York");
            var anchored = CalendarTime.Anchor(block, source);
            var document = new CalendarDocument(3, [anchored]);
            var shown = CalendarTime.InZone(anchored, display);
            var foldDay = new DateOnly(2026, 11, 1);
            var foldTime = new TimeOnly(1, 30);
            reject(() => CalendarChanges.Move(document, shown, foldDay, foldTime, display));
            var moved = CalendarChanges.Move(document, shown, foldDay, foldTime, display, RepeatedTimeChoice.Later).Events.Single();
            check(moved.Id == anchored.Id && moved.TimeZoneId == anchored.TimeZoneId);
            check(moved.StartUtc == new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc) && moved.EndUtc - moved.StartUtc == TimeSpan.FromHours(1));
            reject(() => CalendarChanges.Move(document, shown, new(2026, 3, 8), new(2, 30), display));
        });

        test("moving a recurring occurrence creates one exception and preserves its reminder and series", () =>
        {
            var series = block with { Recurrence = new(RecurrenceFrequency.Daily, Count: 3), ReminderMinutes = 30 };
            var document = new CalendarDocument(3, [series]);
            var shown = Recurrence.InRange(document, today, 1, TimeZoneInfo.Utc).Events.Single();
            var moved = CalendarChanges.Move(document, shown, today.AddDays(1), new(14, 15), TimeZoneInfo.Utc);
            var movedOccurrence = moved.Events.Single(item => item.SeriesId is not null);
            check(moved.Events.Contains(series) && movedOccurrence.OriginalStart == start && movedOccurrence.ReminderMinutes == 30);
            var next = Recurrence.InRange(moved, today, 3, TimeZoneInfo.Utc).Events;
            check(next.Count == 3 && next.All(item => item.Start.Date != start.Date));
            check(next.Any(item => item.Start.TimeOfDay == TimeSpan.FromMinutes(855)) && next.Count(item => item.Start.Hour == 9) == 2);
        });
    }
}
