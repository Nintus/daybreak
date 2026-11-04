using Daymark.Core;

internal static class RecurrenceTests
{
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject, string folder)
    {
        CalendarEvent Event(DateTime start, RecurrenceRule rule, bool allDay = false) => new(Guid.NewGuid(), "Series", start,
            start.AddHours(allDay ? 24 : 1), allDay, Recurrence: rule);
        List<CalendarEvent> Expand(CalendarEvent item, DateOnly first, int days, params CalendarEvent[] exceptions) =>
            Recurrence.InRange(new(3, [item, .. exceptions]), first, days, TimeZoneInfo.Utc).Events.OrderBy(e => e.Start).ToList();
        var daily = Event(new(2026, 1, 1, 9, 0, 0), new(RecurrenceFrequency.Daily, 2, Count: 4));
        test("daily interval and count apply to the entire series, not each queried window", () =>
        {
            check(Expand(daily, new(2026, 1, 1), 20).Select(e => e.Start.Day).SequenceEqual(new[] { 1, 3, 5, 7 }));
            check(Expand(daily, new(2026, 1, 6), 20).Single().Start.Day == 7);
        });
        test("weekly rules support multiple weekdays and Monday-based interval anchoring", () =>
        {
            var weekly = Event(new(2026, 11, 3, 9, 0, 0), new(RecurrenceFrequency.Weekly, 2, (1 << 2) | (1 << 4), Count: 5));
            check(Expand(weekly, new(2026, 11, 1), 28).Select(e => e.Start.Day).SequenceEqual(new[] { 3, 5, 17, 19 }));
            check(Expand(weekly, new(2026, 12, 1), 7).Single().Start.Day == 1);
        });
        test("monthly dates skip short months instead of drifting from the 31st", () =>
        {
            var monthly = Event(new(2026, 1, 31, 9, 0, 0), new(RecurrenceFrequency.Monthly, Count: 4));
            check(Expand(monthly, new(2026, 1, 1), 250).Select(e => e.Start.Month).SequenceEqual(new[] { 1, 3, 5, 7 }));
        });
        test("ordinal and last-weekday monthly rules retain their calendar position", () =>
        {
            var second = Event(new(2026, 1, 13, 9, 0, 0), new(RecurrenceFrequency.Monthly, MonthlyPattern: MonthlyPattern.OrdinalWeekday, Count: 3));
            check(Expand(second, new(2026, 1, 1), 90).Select(e => e.Start.Day).SequenceEqual(new[] { 13, 10, 10 }));
            var last = Event(new(2026, 1, 27, 9, 0, 0), new(RecurrenceFrequency.Monthly, MonthlyPattern: MonthlyPattern.LastWeekday, Count: 3));
            check(Expand(last, new(2026, 1, 1), 90).Select(e => e.Start.Day).SequenceEqual(new[] { 27, 24, 31 }));
        });
        test("yearly leap-day series skip non-leap years and preserve count", () =>
        {
            var leap = Event(new(2024, 2, 29, 9, 0, 0), new(RecurrenceFrequency.Yearly, Count: 2));
            check(Expand(leap, new(2025, 1, 1), 1096).Count == 0);
            check(Expand(leap, new(2028, 1, 1), 366).Single().Start == new DateTime(2028, 2, 29, 9, 0, 0));
        });
        test("recurrence until includes its source date and excludes later dates", () =>
        {
            var until = daily with { Recurrence = new(RecurrenceFrequency.Daily, Until: new(2026, 1, 3)) };
            check(Expand(until, new(2026, 1, 1), 7).Count == 3);
        });
        test("unbounded old series seek directly into a modern visible range", () =>
        {
            var ancient = Event(new(1, 1, 1, 9, 0, 0), new(RecurrenceFrequency.Daily));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            check(Expand(ancient, new(2026, 11, 1), 42).Count == 42);
            check(watch.Elapsed < TimeSpan.FromSeconds(2));
        });
        var ny = CalendarTime.FindZone("America/New_York");
        test("recurring meetings keep source clock time across daylight-saving changes", () =>
        {
            var master = CalendarTime.Anchor(Event(new(2026, 3, 1, 9, 0, 0), new(RecurrenceFrequency.Weekly, Count: 3)), ny);
            var list = Expand(master, new(2026, 3, 1), 22);
            check(list.Select(e => e.Start.Hour).SequenceEqual(new[] { 14, 13, 13 }));
            check(list.All(e => e.EndUtc - e.StartUtc == TimeSpan.FromHours(1)));
            check(list.Select(e => e.OriginalStart!.Value.Hour).All(h => h == 9));
        });
        test("nonexistent recurring local times are skipped without consuming count", () =>
        {
            var gap = CalendarTime.Anchor(Event(new(2026, 3, 1, 2, 30, 0), new(RecurrenceFrequency.Weekly, Count: 3)), ny);
            check(Expand(gap, new(2026, 3, 1), 30).Select(e => e.OriginalStart!.Value.Day).SequenceEqual(new[] { 1, 15, 22 }));
        });
        test("recurring repeated hours use a deterministic first occurrence and fixed elapsed duration", () =>
        {
            var fold = CalendarTime.Anchor(Event(new(2026, 10, 25, 1, 30, 0), new(RecurrenceFrequency.Weekly, Count: 3)), ny);
            var repeated = Expand(fold, new(2026, 11, 1), 1).Single();
            check(repeated.StartUtc == new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc));
            check(repeated.EndUtc - repeated.StartUtc == TimeSpan.FromHours(1));
        });
        test("all-day recurring ranges remain dates when viewed across the date line", () =>
        {
            var master = Event(new(2026, 11, 3), new(RecurrenceFrequency.Weekly, Count: 2), true) with { End = new(2026, 11, 5) };
            var doc = Recurrence.InRange(new(3, [master]), new(2026, 11, 1), 20, CalendarTime.FindZone("Pacific/Auckland"));
            check(doc.Events.Count == 2 && doc.Events.All(e => e.Start.Hour == 0 && e.End - e.Start == TimeSpan.FromDays(2)));
            check(doc.On(new(2026, 11, 5)).Count() == 0);
        });
        test("overnight occurrences are included when they start before the visible range", () =>
        {
            var overnight = Event(new(2026, 11, 2, 23, 30, 0), new(RecurrenceFrequency.Daily, Count: 2));
            check(Expand(overnight, new(2026, 11, 3), 1).Count == 2);
        });
        var masterDoc = new CalendarDocument(3, [daily]);
        var original = Expand(daily, new(2026, 1, 1), 1).Single();
        var exception = CalendarChanges.SourceOccurrence(masterDoc, original) with { Title = "Moved", Start = new(2026, 2, 8, 11, 0, 0), End = new(2026, 2, 8, 12, 0, 0) };
        var movedDoc = CalendarChanges.SaveEvent(masterDoc, exception);
        test("moving one occurrence suppresses its original and includes its destination range", () =>
        {
            check(Recurrence.InRange(movedDoc, new(2026, 1, 1), 1, TimeZoneInfo.Utc).Events.Count == 0);
            var moved = Recurrence.InRange(movedDoc, new(2026, 2, 8), 1, TimeZoneInfo.Utc).Events.Single();
            check(moved.Title == "Moved" && moved.OriginalStart == original.OriginalStart && moved.SeriesId == daily.Id);
        });
        test("cancelling a moved occurrence retains a tombstone and never resurrects its original", () =>
        {
            var moved = Recurrence.InRange(movedDoc, new(2026, 2, 8), 1, TimeZoneInfo.Utc).Events.Single();
            var cancelled = CalendarChanges.CancelOccurrence(movedDoc, moved);
            check(cancelled.Events.Count == 2 && cancelled.Events.Single(e => e.SeriesId is not null).IsCancelled);
            check(Recurrence.InRange(cancelled, new(2026, 1, 1), 60, TimeZoneInfo.Utc).Events.Count == 3);
        });
        test("series title changes retain exceptions while schedule changes require explicit reset", () =>
        {
            var renamed = CalendarChanges.SaveEvent(movedDoc, daily with { Title = "Renamed" });
            check(renamed.Events.Any(e => e.Title == "Moved"));
            var shifted = daily with { Start = daily.Start.AddHours(1), End = daily.End.AddHours(1) };
            reject(() => CalendarChanges.SaveEvent(movedDoc, shifted));
            check(CalendarChanges.SaveEvent(movedDoc, shifted, true).Events.Count == 1);
            check(CalendarChanges.DeleteEventOrSeries(movedDoc, daily.Id).Events.Count == 0);
        });
        test("exceptions reject orphan series duplicate original identities and non-occurrence dates", () =>
        {
            reject(() => new CalendarDocument(3, [exception]).Validate());
            reject(() => new CalendarDocument(3, [daily, exception, exception with { Id = Guid.NewGuid() }]).Validate());
            reject(() => new CalendarDocument(3, [daily, exception with { OriginalStart = daily.Start.AddDays(1) }]).Validate());
        });
        test("recurrence rejects invalid bounds weekday patterns and unsupported values", () =>
        {
            foreach (var rule in new[] { new RecurrenceRule((RecurrenceFrequency)99), new(RecurrenceFrequency.Daily, 0),
                new(RecurrenceFrequency.Daily, Count: 0), new(RecurrenceFrequency.Daily, Count: 10001),
                new(RecurrenceFrequency.Daily, Count: 3, Until: new(2026, 1, 3)), new(RecurrenceFrequency.Weekly, Weekdays: 128),
                new(RecurrenceFrequency.Daily, Weekdays: 1), new(RecurrenceFrequency.Daily, Until: new(2025, 1, 1)) })
                reject(() => (daily with { Recurrence = rule }).Validate());
            reject(() => new CalendarDocument(2, [daily]).Validate());
        });
        test("version-2 migration is read-only until save and keeps a permanent original backup", () =>
        {
            var path = Path.Combine(folder, "v2.json");
            var single = daily with { Recurrence = null };
            var json = System.Text.Json.JsonSerializer.Serialize(new CalendarDocument(2, [single]));
            File.WriteAllText(path, json);
            var store = new CalendarStore(path); var loaded = store.Load();
            check(loaded.Version == 3 && loaded.CalendarList.Single().Id == "personal" && File.ReadAllText(path) == json);
            store.Save(movedDoc); store.Save(masterDoc);
            check(File.ReadAllText(path + ".v2.bak") == json);
            check(CalendarStore.Read(path + ".bak").Events.Any(e => e.SeriesId is not null));
        });
        test("recurrence and exception edits round-trip and undo as a complete series", () =>
        {
            var store = new CalendarStore(Path.Combine(folder, "series.json"));
            var history = new CalendarHistory(masterDoc);
            history.Commit(movedDoc, "move occurrence", store.Save);
            check(store.Load().Events.SequenceEqual(movedDoc.Events));
            check(history.Undo(store.Save) && store.Load().Events.Count == 1);
            check(history.Redo(store.Save) && store.Load().Events.Count == 2);
        });
        test("calendar identity validates references colors and duplicate definitions", () =>
        {
            var work = new CalendarDefinition("work", "Work", "Teal");
            var doc = new CalendarDocument(3, [daily with { CalendarId = "work" }], [CalendarDefinition.Personal, work]); doc.Validate();
            check(Recurrence.InRange(doc, new(2026, 1, 1), 7, TimeZoneInfo.Utc, new HashSet<string> { "work" }).Events.Count == 0);
            reject(() => (doc with { Calendars = [CalendarDefinition.Personal] }).Validate());
            reject(() => (doc with { Calendars = [work, work] }).Validate());
            reject(() => (work with { Color = "invalid" }).Validate());
        });
        test("moving a series to another calendar moves its exception records atomically", () =>
        {
            var doc = movedDoc with { Calendars = [CalendarDefinition.Personal, new("work", "Work")] };
            var moved = CalendarChanges.SaveEvent(doc, daily with { CalendarId = "work" });
            check(moved.Events.All(e => e.CalendarId == "work"));
        });
        test("series expansion retains source wall time when seed instant snapshots differ", () =>
        {
            var master = CalendarTime.Anchor(Event(new(2026, 3, 1, 9, 0, 0), new(RecurrenceFrequency.Weekly, Count: 2)), ny);
            master = master with { StartUtc = master.StartUtc!.Value.AddHours(1), EndUtc = master.EndUtc!.Value.AddHours(1) };
            master.Validate();
            check(Recurrence.InRange(new(3, [master]), new(2026, 3, 1), 15, ny).Events.All(e => e.Start.Hour == 9));
        });
        test("search finds recurring exceptions and preserves all-time single event search", () =>
        {
            var old = daily with { Id = Guid.NewGuid(), Recurrence = null, Title = "Historic needle", Start = new(2001, 1, 1, 9, 0, 0), End = new(2001, 1, 1, 10, 0, 0) };
            var doc = movedDoc.WithEvents([.. movedDoc.Events, old]);
            check(CalendarSearch.Find(doc, "Moved", new(2026, 1, 1), TimeZoneInfo.Utc).Single().OriginalStart == daily.Start);
            check(CalendarSearch.Find(doc, "needle", new(2026, 1, 1), TimeZoneInfo.Utc).Single().Start.Year == 2001);
            check(CalendarSearch.Find(doc, "Series", new(2026, 1, 1), TimeZoneInfo.Utc, new HashSet<string> { "personal" }).Count == 0);
        });
        test("representative dense range expansion stays bounded to visible occurrences", () =>
        {
            var events = Enumerable.Range(0, 1000).Select(i => daily with { Id = Guid.NewGuid(), Recurrence = new(RecurrenceFrequency.Daily), Title = "Calendar " + i }).ToList();
            var expanded = Recurrence.InRange(new(3, events), new(2026, 11, 1), 42, TimeZoneInfo.Utc);
            check(expanded.Events.Count == 42000 && expanded.Days(new(2026, 11, 1), 42).All(day => day.Count == 1000));
        });
        test("explicit default weekdays do not falsely reset a series schedule", () =>
        {
            var implicitDay = daily with { Recurrence = new(RecurrenceFrequency.Weekly) };
            var explicitDay = implicitDay with { Recurrence = new(RecurrenceFrequency.Weekly, Weekdays: 1 << (int)daily.Start.DayOfWeek) };
            check(!CalendarChanges.ScheduleChanged(implicitDay, explicitDay));
        });
        test("calendar history snapshots do not expose mutable calendar collections", () =>
        {
            var doc = CalendarDocument.Empty(); var history = new CalendarHistory(doc);
            doc.Calendars!.Add(new("work", "Work"));
            var snapshot = history.Current; snapshot.Calendars!.Clear();
            check(history.Current.CalendarList.Count == 1);
        });
    }
}
