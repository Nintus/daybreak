using Daymark.Core;

internal static class DailyWorkflowTests
{
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject, string folder)
    {
        var today = new DateOnly(2026, 11, 6);
        var zone = CalendarTime.FindZone("America/Chicago");
        static DateTime Utc(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);
        static CalendarEvent Timed(int day, int hour, int reminder) => new(Guid.NewGuid(), "Focus",
            new(2026, 11, day, hour, 0, 0), new(2026, 11, day, hour + 1, 0, 0), ReminderMinutes: reminder);

        test("Quick Add keeps the title and parses an explicit repeat and reminder", () =>
        {
            var draft = QuickEventParser.Parse("Coffee at the library next Monday at 9:30 am for 45m every week remind 15m before", today, 60, "work");
            check(draft.Title == "Coffee at the library" && draft.CalendarId == "work" && !draft.AllDay);
            check(draft.Start == new DateTime(2026, 11, 9, 9, 30, 0) && draft.End == new DateTime(2026, 11, 9, 10, 15, 0));
            check(draft.Recurrence?.Frequency == RecurrenceFrequency.Weekly && draft.ReminderMinutes == 15 && draft.StartUtc is null);
        });

        test("Quick Add handles all-day dates, overnight times and the configured default length", () =>
        {
            var allDay = QuickEventParser.Parse("Anniversary 2026-11-10 every year remind at  start", today, 90, "personal");
            check(allDay.AllDay && allDay.End == allDay.Start.AddDays(1) && allDay.ReminderMinutes == 0 && allDay.Recurrence?.Frequency == RecurrenceFrequency.Yearly);
            var overnight = QuickEventParser.Parse("Night shift tomorrow at 11pm to 02:00", today, 90, "personal");
            check(overnight.Start == new DateTime(2026, 11, 7, 23, 0, 0) && overnight.End == new DateTime(2026, 11, 8, 2, 0, 0));
            var lunch = QuickEventParser.Parse("Lunch today at noon", today, 90, "personal");
            check(lunch.End == lunch.Start.AddMinutes(90));
        });

        test("Quick Add rejects invalid dates, times and mismatched weekday repeats", () =>
        {
            foreach (var phrase in new[] { "Lunch sometime", "Trip 2026-02-30", "Work today at 26:00", "Call today at 9am to 9am", "Focus tomorrow at 9am every weekday", "Dentist today remind 8d before" })
                reject(() => QuickEventParser.Parse(phrase, today, 60, "personal"));
        });

        test("reminder planner follows recurring source time across DST and honors skipped and moved occurrences", () =>
        {
            var source = CalendarTime.FindZone("America/New_York");
            var series = CalendarTime.Anchor(Timed(13, 9, 15) with
            {
                Start = new DateTime(2026, 3, 6, 9, 0, 0), End = new DateTime(2026, 3, 6, 10, 0, 0),
                Recurrence = new(RecurrenceFrequency.Daily, Count: 5)
            }, source);
            var moved = CalendarTime.Anchor(series with
            {
                Id = Guid.NewGuid(), Recurrence = null, SeriesId = series.Id, OriginalStart = series.Start.AddDays(1),
                Start = new DateTime(2026, 3, 8, 11, 0, 0), End = new DateTime(2026, 3, 8, 12, 0, 0)
            }, source);
            var skipped = CalendarTime.Anchor(moved with
            {
                Id = Guid.NewGuid(), OriginalStart = series.Start.AddDays(2), IsCancelled = true,
                Start = new DateTime(2026, 3, 8, 9, 0, 0), End = new DateTime(2026, 3, 8, 10, 0, 0)
            }, source);
            var document = new CalendarDocument(3, [series, moved, skipped]);
            document.Validate();
            var now = new DateTime(2026, 3, 8, 12, 44, 0, DateTimeKind.Utc);
            var expected = new DateTime(2026, 3, 8, 14, 45, 0, DateTimeKind.Utc);
            var state = new Dictionary<ReminderKey, DateTime?>();
            check(ReminderPlanner.Plan(document, now, zone, state).NextCheckUtc == expected);
            var due = ReminderPlanner.Plan(document, expected, zone, state).Due.Single();
            check(due.Event.Id == moved.Id && due.Key.OriginalStart == moved.OriginalStart);
            var nextDay = new DateTime(2026, 3, 9, 12, 45, 0, DateTimeKind.Utc);
            check(ReminderPlanner.Plan(document, nextDay, zone, state).Due.Single().Event.StartUtc?.Hour == 13);
        });

        test("reminder snooze and dismissal survive reload while edited triggers get a new alert", () =>
        {
            var item = CalendarTime.Anchor(Timed(10, 9, 15), zone);
            var document = new CalendarDocument(3, [item]);
            var now = Utc(10, 14, 45);
            var store = new ReminderStateStore(Path.Combine(folder, ReminderStateStore.FileName));
            var state = store.Load();
            var alert = ReminderPlanner.Plan(document, now, TimeZoneInfo.Utc, state).Due.Single();
            check(alert.Key.TriggerUtc == now);
            store.Record(state, alert.Key, now.AddMinutes(5), now);
            state = store.Load();
            var waiting = ReminderPlanner.Plan(document, now.AddMinutes(4), zone, state);
            check(waiting.Due.Count == 0 && waiting.NextCheckUtc == now.AddMinutes(5));
            check(ReminderPlanner.Plan(document, now.AddMinutes(5), zone, state).Due.Single().Key == alert.Key);
            store.Record(state, alert.Key, null, now.AddMinutes(5));
            state = store.Load();
            check(ReminderPlanner.Plan(document, now.AddMinutes(6), zone, state).Due.Count == 0);
            var edited = document.WithEvents([item with { ReminderMinutes = 10 }]);
            check(ReminderPlanner.Plan(edited, now.AddMinutes(6), zone, state).Due.Single().Key != alert.Key);
        });

        test("all-day reminders follow device morning and expired events stay quiet", () =>
        {
            var item = Timed(10, 9, 1440) with { AllDay = true, Start = new(2026, 11, 10), End = new(2026, 11, 11) };
            var document = new CalendarDocument(3, [item]);
            var state = new Dictionary<ReminderKey, DateTime?>();
            check(ReminderPlanner.Plan(document, Utc(9, 14, 59), zone, state).NextCheckUtc == Utc(9, 15));
            check(ReminderPlanner.Plan(document, Utc(9, 15, 3), zone, state).Due.Single().Event.AllDay);
            check(ReminderPlanner.Plan(document, Utc(10, 4), zone, state).Due.Count == 0);
            check(ReminderPlanner.Plan(new(3, [Timed(10, 9, 0)]), Utc(10, 17), zone, state).Due.Count == 0);
            check(ReminderPlanner.Plan(CalendarDocument.Empty(), Utc(10, 17), zone, state).NextCheckUtc is null);
            var springDay = item with { Start = new(2026, 3, 8), End = new(2026, 3, 9) };
            var beforeDst = new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc);
            check(ReminderPlanner.Plan(new(3, [springDay]), beforeDst, zone, state).Due.Single().Key.TriggerUtc == beforeDst);
        });

        test("native backup and history keep reminder settings and old events stay silent", () =>
        {
            var path = Path.Combine(folder, "reminder-calendar.json");
            var store = new CalendarStore(path);
            var old = Timed(10, 9, 0) with { ReminderMinutes = null };
            store.Save(new(3, [old]));
            var prior = File.ReadAllText(path);
            check(!prior.Contains("ReminderMinutes", StringComparison.Ordinal));
            var history = new CalendarHistory(store.Load());
            history.Commit(history.Current.WithEvents([old with { ReminderMinutes = 0 }]), "set reminder", store.Save);
            check(store.Load().Version == 4 && store.Load().Events.Single().ReminderMinutes == 0);
            check(File.ReadAllText(path + ".v3.bak") == prior);
            check(history.Undo(store.Save) && store.Load().Version == 3 && store.Load().Events.Single().ReminderMinutes is null);
        });
    }
}
