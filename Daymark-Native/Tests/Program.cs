using Daymark.Core;

var count = 0;
void Test(string name, Action action) { action(); count++; Console.WriteLine($"PASS {name}"); }
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }
var folder = Path.Combine(Path.GetTempPath(), "daymark-native-tests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    var item = new CalendarEvent(Guid.NewGuid(), "Test café", new DateTime(2026, 10, 8, 9, 0, 0), new DateTime(2026, 10, 8, 10, 0, 0), Location: "Studio", Notes: "Line one\nLine two");
    var store = new CalendarStore(Path.Combine(folder, "calendar.json"));
    Test("missing file starts empty", () => Check(store.Load().Events.Count == 0));
    Test("round-trip preserves all event fields", () => { store.Save(new(1, [item])); Check(store.Load().Events.Single() == item); });
    var changed = item with { Title = "Changed", End = item.End.AddHours(1) };
    Test("save keeps previous-state backup", () => { store.Save(new(1, [changed])); Check(store.Load().Events.Single() == changed); Check(CalendarStore.Read(store.Path + ".bak").Events.Single() == item); });
    Test("invalid save leaves existing data intact", () => { Reject(() => store.Save(new(1, [changed with { End = changed.Start }]))); Check(store.Load().Events.Single() == changed); });
    Test("duplicate IDs are rejected", () => Reject(() => new CalendarDocument(1, [item, item]).Validate()));
    Test("unknown backup version is rejected", () => Reject(() => new CalendarDocument(2, [item]).Validate()));
    Test("midnight boundary excludes previous day's ending event", () => { var midnight = item with { End = new DateTime(2026, 10, 9) }; Check(new CalendarDocument(1, [midnight]).On(new DateOnly(2026, 10, 9)).Count() == 0); });
    Test("multi-day all-day event uses exclusive end", () => { var days = item with { Start = new DateTime(2026, 10, 8), End = new DateTime(2026, 10, 10), AllDay = true }; days.Validate(); var doc = new CalendarDocument(1, [days]); Check(doc.On(new(2026, 10, 8)).Count() == 1 && doc.On(new(2026, 10, 9)).Count() == 1 && doc.On(new(2026, 10, 10)).Count() == 0); });
    Test("all-day event rejects non-midnight times", () => Reject(() => (item with { AllDay = true }).Validate()));
    Test("UTC values are rejected by local-time model", () => Reject(() => (item with { Start = DateTime.SpecifyKind(item.Start, DateTimeKind.Utc) }).Validate()));
    Test("visible days match individual day queries across boundaries and ordering", () =>
    {
        var first = new DateOnly(2026, 9, 28);
        var events = new List<CalendarEvent>
        {
            item,
            item with { Id = Guid.NewGuid(), Title = "Earlier", Start = item.Start.AddHours(-1) },
            item with { Id = Guid.NewGuid(), Start = new(2026, 9, 1), End = new(2026, 12, 1), AllDay = true },
            item with { Id = Guid.NewGuid(), Start = new(2026, 10, 8), End = new(2026, 10, 9), AllDay = true },
            item with { Id = Guid.NewGuid(), Start = new(2026, 9, 27, 23, 0, 0), End = new(2026, 9, 28) },
            item with { Id = Guid.NewGuid(), Start = new(2026, 11, 8, 23, 0, 0), End = new(2026, 11, 9, 1, 0, 0) }
        };
        var doc = new CalendarDocument(1, events); doc.Validate();
        var days = doc.Days(first, 42);
        Check(days.Count == 42);
        for (var i = 0; i < 42; i++) Check(days[i].SequenceEqual(doc.On(first.AddDays(i))));
    });
    Test("visible days handle empty calendars and reject invalid ranges", () =>
    {
        Check(CalendarDocument.Empty().Days(new(2026, 1, 1), 42).All(day => day.Count == 0));
        foreach (var count in new[] { 0, -1, 367 })
        {
            try { CalendarDocument.Empty().Days(new(2026, 1, 1), count); throw new Exception("Expected range rejection"); }
            catch (ArgumentOutOfRangeException) { }
        }
    });
    Test("backup export and restore preserve state", () => { var path = Path.Combine(folder, "backup.json"); CalendarStore.Export(new(1, [item]), path); store.Save(CalendarStore.Read(path)); Check(store.Load().Events.Single() == item); });
    Test("weeks start Monday across year and leap-day boundaries", () =>
    {
        Check(WeekLayout.StartOfWeek(new(2027, 1, 3)) == new DateOnly(2026, 12, 28));
        Check(WeekLayout.StartOfWeek(new(2024, 2, 29)) == new DateOnly(2024, 2, 26));
        Check(WeekLayout.StartOfWeek(new(2026, 11, 2)) == new DateOnly(2026, 11, 2));
    });
    Test("overlapping appointments use distinct columns with equal group widths", () =>
    {
        var later = item with { Id = Guid.NewGuid(), Start = item.Start.AddMinutes(30), End = item.End.AddMinutes(30) };
        var last = item with { Id = Guid.NewGuid(), Start = item.Start.AddHours(3), End = item.End.AddHours(3) };
        var slots = WeekLayout.TimedDay([last, later, item], DateOnly.FromDateTime(item.Start));
        Check(slots.Count == 3 && slots[0].ColumnCount == 2 && slots[1].ColumnCount == 2);
        Check(slots[0].Column != slots[1].Column && slots[2].ColumnCount == 1);
    });
    Test("adjacent appointments share a full-width column", () =>
    {
        var next = item with { Id = Guid.NewGuid(), Start = item.End, End = item.End.AddHours(1) };
        Check(WeekLayout.TimedDay([item, next], DateOnly.FromDateTime(item.Start)).All(s => s.Column == 0 && s.ColumnCount == 1));
    });
    Test("overnight segments clip to each day and exclude midnight end", () =>
    {
        var night = item with { Start = new(2026, 10, 8, 23, 30, 0), End = new(2026, 10, 9, 1, 0, 0) };
        var first = WeekLayout.TimedDay([night], new(2026, 10, 8)).Single();
        var second = WeekLayout.TimedDay([night], new(2026, 10, 9)).Single();
        Check(first.StartMinute == 1410 && first.EndMinute == 1440 && second.StartMinute == 0 && second.EndMinute == 60);
        Check(WeekLayout.TimedDay([night with { End = new(2026, 10, 9) }], new(2026, 10, 9)).Count == 0);
    });
    Test("all-day events stay outside the timed layout", () =>
        Check(WeekLayout.TimedDay([item with { AllDay = true, Start = item.Start.Date, End = item.Start.Date.AddDays(2) }], new(2026, 10, 8)).Count == 0));
    Test("short appointments reserve selectable display space without changing data", () =>
    {
        var brief = item with { End = item.Start.AddMinutes(1) };
        var next = brief with { Id = Guid.NewGuid(), Start = brief.Start.AddMinutes(5), End = brief.End.AddMinutes(5) };
        var slots = WeekLayout.TimedDay([brief, next], DateOnly.FromDateTime(item.Start));
        Check(slots.All(s => s.ColumnCount == 2) && slots[0].EndMinute - slots[0].StartMinute == 1);
        Check(brief.End == brief.Start.AddMinutes(1));
    });
    Test("overlap chains reuse available columns without colliding", () =>
    {
        var chain = Enumerable.Range(0, 50).Select(i => item with { Id = Guid.NewGuid(),
            Start = item.Start.AddMinutes(i * 5), End = item.Start.AddMinutes(i * 5 + 60) }).ToList();
        var slots = WeekLayout.TimedDay(chain, DateOnly.FromDateTime(item.Start));
        Check(slots.Count == 50 && slots.All(s => s.ColumnCount == 12));
        foreach (var a in slots)
        foreach (var b in slots.Where(b => b.Event.Id != a.Event.Id && b.StartMinute < a.EndMinute && b.EndMinute > a.StartMinute))
            Check(a.Column != b.Column);
    });
    Test("create edit delete undo and redo persist complete event data", () =>
    {
        var history = new CalendarHistory(CalendarDocument.Empty());
        history.Commit(new(1, [item]), "create", store.Save);
        history.Commit(new(1, [changed]), "edit", store.Save);
        history.Commit(CalendarDocument.Empty(), "delete", store.Save);
        Check(history.UndoDescription == "delete" && history.Undo(store.Save));
        Check(store.Load().Events.Single() == changed);
        Check(history.Undo(store.Save) && store.Load().Events.Single() == item);
        Check(history.Undo(store.Save) && store.Load().Events.Count == 0 && !history.CanUndo);
        Check(history.Redo(store.Save) && store.Load().Events.Single() == item);
        Check(history.Redo(store.Save) && store.Load().Events.Single() == changed);
        Check(history.Redo(store.Save) && store.Load().Events.Count == 0 && !history.CanRedo);
    });
    Test("backup restore can be undone and redone as a whole calendar", () =>
    {
        var history = new CalendarHistory(new(1, [item]));
        var incoming = new CalendarDocument(1, [changed, item with { Id = Guid.NewGuid() }]);
        history.Commit(incoming, "restore backup", store.Save);
        Check(history.Undo(store.Save) && store.Load().Events.SequenceEqual(new[] { item }));
        Check(history.Redo(store.Save) && store.Load().Events.SequenceEqual(incoming.Events));
    });
    Test("failed commit leaves current data and redo available", () =>
    {
        var history = new CalendarHistory(new(1, [item]));
        history.Commit(new(1, [changed]), "edit", store.Save); history.Undo(store.Save);
        try { history.Commit(CalendarDocument.Empty(), "delete", _ => throw new IOException("Disk full")); throw new Exception("Expected failure"); }
        catch (IOException) { }
        Check(history.Current.Events.Single() == item && history.CanRedo && !history.CanUndo);
        Check(store.Load().Events.Single() == item && history.Redo(store.Save));
    });
    Test("failed undo and redo do not consume history", () =>
    {
        var history = new CalendarHistory(new(1, [item]));
        history.Commit(new(1, [changed]), "edit", store.Save);
        try { history.Undo(_ => throw new IOException("Disk full")); throw new Exception("Expected failure"); } catch (IOException) { }
        Check(history.Current.Events.Single() == changed && history.CanUndo && !history.CanRedo);
        Check(history.Undo(store.Save));
        try { history.Redo(_ => throw new IOException("Disk full")); throw new Exception("Expected failure"); } catch (IOException) { }
        Check(history.Current.Events.Single() == item && history.CanRedo && !history.CanUndo);
        Check(history.Redo(store.Save) && store.Load().Events.Single() == changed);
    });
    Test("history snapshots cannot be changed through exposed lists", () =>
    {
        var initial = new CalendarDocument(1, [item]); var history = new CalendarHistory(initial);
        initial.Events.Clear(); history.Current.Events.Clear();
        var next = new CalendarDocument(1, [changed]); history.Commit(next, "edit", _ => { }); next.Events.Clear();
        Check(history.Current.Events.Single() == changed);
        Check(history.Undo(_ => { }) && history.Current.Events.Single() == item);
    });
    Test("new edits clear redo but no-op saves preserve it", () =>
    {
        var history = new CalendarHistory(new(1, [item]));
        history.Commit(new(1, [changed]), "edit", _ => { }); history.Undo(_ => { });
        history.Commit(new(1, [item]), "unchanged", _ => throw new Exception("No-op must not write"));
        Check(history.CanRedo);
        history.Commit(CalendarDocument.Empty(), "delete", _ => { }); Check(!history.CanRedo);
    });
    Test("history retains only the configured most recent changes", () =>
    {
        var history = new CalendarHistory(CalendarDocument.Empty(), 3);
        for (var i = 1; i <= 5; i++) history.Commit(new(1, [item with { Title = i.ToString() }]), "edit", _ => { });
        var undos = 0; while (history.Undo(_ => { })) undos++;
        Check(undos == 3 && history.Current.Events.Single().Title == "2");
    });
    Test("invalid changes are rejected before persistence or history changes", () =>
    {
        var history = new CalendarHistory(new(1, [item]));
        Reject(() => history.Commit(new(1, [item with { End = item.Start }]), "invalid", _ => throw new Exception("Must not write")));
        Check(history.Current.Events.Single() == item && !history.CanUndo && !history.CanRedo);
    });
    Test("day week and month navigation preserve selection and cross date boundaries", () =>
    {
        Check(CalendarNavigation.Move(new(2024, 2, 28), CalendarViewMode.Day, 1) == new DateOnly(2024, 2, 29));
        Check(CalendarNavigation.Move(new(2026, 12, 31), CalendarViewMode.Day, 1) == new DateOnly(2027, 1, 1));
        Check(CalendarNavigation.Move(new(2026, 12, 30), CalendarViewMode.Week, 1) == new DateOnly(2027, 1, 6));
        Check(CalendarNavigation.Move(new(2026, 1, 31), CalendarViewMode.Month, 1) == new DateOnly(2026, 2, 28));
        Check(CalendarNavigation.Move(new(2026, 11, 3), CalendarViewMode.Month, -1) == new DateOnly(2026, 10, 3));
    });
    Test("navigation stops at supported calendar limits", () =>
    {
        Check(CalendarNavigation.Move(new(2, 1, 1), CalendarViewMode.Day, -1) == new DateOnly(2, 1, 1));
        Check(CalendarNavigation.Move(new(9998, 12, 31), CalendarViewMode.Month, 1) == new DateOnly(9998, 12, 31));
    });
    Test("corrupt file is not overwritten on load", () => { File.WriteAllText(store.Path, "bad json"); try { store.Load(); throw new Exception("Expected failure"); } catch (System.Text.Json.JsonException) { } Check(File.ReadAllText(store.Path) == "bad json"); });
    Check(!Directory.GetFiles(folder, "*.tmp").Any());
    Console.WriteLine($"{count} tests passed");
}
finally { Directory.Delete(folder, true); }
