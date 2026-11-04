using System.Text.Json;
using Daymark.Core;
using Daymark.Migration;

internal static class MigrationTests
{
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject, string folder)
    {
        string Wrap(string events, string extra = "") => "BEGIN:VCALENDAR\nVERSION:2.0\n" + extra + events + "\nEND:VCALENDAR";
        string Event(string extra = "", string start = "DTSTART:20261104T090000", string end = "DTEND:20261104T100000", string uid = "event-a") =>
            $"BEGIN:VEVENT\nUID:{uid}\nSUMMARY:Café\\, planning\n{start}\n{end}\n{extra}\nEND:VEVENT";
        CalendarImport Parse(string events) => IcalendarImporter.Parse(Wrap(events), "Calendar");
        test("ICS unfolds text without importing alarm fields as event text", () =>
        {
            var result = Parse(Event("DESCRIPTION:First\\nSecond\\;line\n continued\nLOCATION:Studio\\, A\nBEGIN:VALARM\nDESCRIPTION:Alarm text\nEND:VALARM"));
            check(result.Events.Single().Notes == "First\nSecond;linecontinued" && result.Events[0].Location == "Studio, A" && result.Events[0].Title == "Café, planning");
            check(result.Warnings.Any(w => w.Contains("Reminders")));
        });
        test("ICS all-day dates retain exclusive end and default single day", () =>
        {
            var all = Parse(Event(start: "DTSTART;VALUE=DATE:20261104", end: "DTEND;VALUE=DATE:20261107")).Events.Single();
            var one = Parse(Event(start: "DTSTART;VALUE=DATE:20261104", end: "")).Events.Single();
            check(all.AllDay && all.End == new DateTime(2026, 11, 7) && one.End == new DateTime(2026, 11, 5));
        });
        test("ICS floating and UTC timestamps remain distinct", () =>
        {
            var floating = Parse(Event()).Events.Single(); var utc = Parse(Event(start: "DTSTART:20261104T090000Z", end: "DTEND:20261104T100000Z")).Events.Single();
            check(floating.StartUtc is null && floating.TimeZoneId is null && utc.StartUtc == new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
        });
        test("ICS TZID including quoted parameters anchors source wall clocks", () =>
        {
            var e = Parse(Event(start: "DTSTART;TZID=\"America/New_York\":20261104T090000", end: "DTEND;TZID=America/New_York:20261104T100000")).Events.Single();
            check(e.Start.Hour == 9 && e.StartUtc!.Value.Hour == 14 && e.EndUtc!.Value.Hour == 15);
        });
        test("ICS accepts zoned start with UTC end without shifting wall time", () =>
        {
            var e = Parse(Event(start: "DTSTART;TZID=America/New_York:20261104T090000", end: "DTEND:20261104T150000Z")).Events.Single();
            check(e.End.Hour == 10 && e.EndUtc!.Value.Hour == 15);
        });
        test("ICS unknown zones, DST gaps and ambiguous seeds are reported and skipped", () =>
        {
            foreach (var pair in new[] { ("Unknown/Zone", "20261104T090000", "20261104T100000"), ("America/New_York", "20260308T023000", "20260308T040000"), ("America/New_York", "20261101T013000", "20261101T030000") })
            {
                var result = Parse(Event(start: $"DTSTART;TZID={pair.Item1}:{pair.Item2}", end: $"DTEND;TZID={pair.Item1}:{pair.Item3}")); check(result.Events.Count == 0 && result.Warnings.Count > 0);
            }
        });
        test("ICS supports elapsed duration and all-day week duration", () =>
        {
            check(Parse(Event("DURATION:PT90M", end: "")).Events.Single().End == new DateTime(2026, 11, 4, 10, 30, 0));
            check(Parse(Event("DURATION:P2W", start: "DTSTART;VALUE=DATE:20261104", end: "")).Events.Single().End == new DateTime(2026, 11, 18));
            check(Parse(Event("DURATION:P1D", start: "DTSTART;TZID=America/New_York:20261104T090000", end: "")).Events.Count == 0);
        });
        test("ICS rejects incompatible dates, duplicate properties and zero-duration events", () =>
        {
            foreach (var e in new[] { Event(end: ""), Event(end: "DTEND;VALUE=DATE:20261105"), Event("DTSTART:20261105T090000"), Event("DURATION:PT1H") }) check(Parse(e).Events.Count == 0);
        });
        test("ICS weekly repeat weekdays preserve count and source time", () =>
        {
            var e = Parse(Event("RRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=4")).Events.Single();
            var dates = Recurrence.InRange(new(3, [e]), new(2026, 11, 4), 30, TimeZoneInfo.Utc).Events.Select(x => x.Start.Day).ToArray();
            check(dates.SequenceEqual(new[] { 4, 9, 11, 16 }));
        });
        test("ICS monthly ordinal, last weekday and yearly rules stay structured", () =>
        {
            check(Parse(Event("RRULE:FREQ=MONTHLY;BYDAY=1WE;COUNT=5")).Events.Single().Recurrence!.MonthlyPattern == MonthlyPattern.OrdinalWeekday);
            check(Parse(Event("RRULE:FREQ=MONTHLY;BYDAY=WE;BYSETPOS=-1", start: "DTSTART:20261125T090000", end: "DTEND:20261125T100000")).Events.Single().Recurrence!.MonthlyPattern == MonthlyPattern.LastWeekday);
            check(Parse(Event("RRULE:FREQ=YEARLY;BYMONTH=11;BYMONTHDAY=4")).Events.Single().Recurrence!.Frequency == RecurrenceFrequency.Yearly);
        });
        test("ICS exclusions and moved occurrences preserve series identity", () =>
        {
            var result = Parse(Event("RRULE:FREQ=DAILY;COUNT=5\nEXDATE:20261105T090000") + "\n" + Event("RECURRENCE-ID:20261106T090000", start: "DTSTART:20261106T140000", end: "DTEND:20261106T150000"));
            check(result.Events.Count == 3 && result.Warnings.Count == 0);
            var view = Recurrence.InRange(new(3, result.Events, [result.Calendar]), new(2026, 11, 4), 6, TimeZoneInfo.Utc);
            check(view.Events.Count == 4 && view.Events.Any(e => e.Start.Hour == 14 && e.OriginalStart!.Value.Hour == 9) && !view.Events.Any(e => e.Start.Day == 5));
        });
        test("ICS cancelled occurrence may omit DTSTART and DTEND", () =>
        {
            var result = Parse(Event("RRULE:FREQ=DAILY;COUNT=3") + "\nBEGIN:VEVENT\nUID:event-a\nRECURRENCE-ID:20261105T090000\nSTATUS:CANCELLED\nEND:VEVENT");
            check(result.Events.Count == 2 && result.Events.Single(e => e.SeriesId is not null).IsCancelled);
        });
        test("ICS UTC recurrence-id maps to the master source zone", () =>
        {
            var result = Parse(Event("RRULE:FREQ=DAILY;COUNT=3\nEXDATE:20261105T140000Z", start: "DTSTART;TZID=America/New_York:20261104T090000", end: "DTEND;TZID=America/New_York:20261104T100000"));
            check(result.Events.Count == 2 && result.Events.Single(e => e.SeriesId is not null).OriginalStart == new DateTime(2026, 11, 5, 9, 0, 0));
        });
        test("ICS UTC UNTIL excludes a final date before the scheduled clock time", () =>
        {
            var result = Parse(Event("RRULE:FREQ=DAILY;UNTIL=20261106T135959Z", start: "DTSTART;TZID=America/New_York:20261104T090000", end: "DTEND;TZID=America/New_York:20261104T100000"));
            check(result.Events.Single().Recurrence!.Until == new DateOnly(2026, 11, 5));
        });
        test("ICS unsupported recurrence constraints reject the whole series", () =>
        {
            foreach (var rule in new[] { "FREQ=HOURLY", "FREQ=DAILY;BYHOUR=9,10", "FREQ=WEEKLY;INTERVAL=2;WKST=SU", "FREQ=MONTHLY;BYMONTHDAY=-1", "FREQ=MONTHLY;BYDAY=MO,TU", "FREQ=YEARLY;BYMONTH=1,11", "FREQ=DAILY;COUNT=10001", "FREQ=DAILY;COUNT=5;UNTIL=20261201T090000" })
            { var result = Parse(Event("RRULE:" + rule)); check(result.Events.Count == 0 && result.Warnings.Count > 0); }
            foreach (var constraint in new[] { "RDATE:20261106T090000", "EXRULE:FREQ=DAILY" }) check(Parse(Event("RRULE:FREQ=DAILY\n" + constraint)).Events.Count == 0);
        });
        test("ICS invalid overrides do not leave an unmodified master behind", () =>
        {
            foreach (var id in new[] { "RECURRENCE-ID;RANGE=THISANDFUTURE:20261105T090000", "RECURRENCE-ID:20261105T110000" })
                check(Parse(Event("RRULE:FREQ=DAILY") + "\n" + Event(id, uid: "event-a")).Events.Count == 0);
            check(Parse(Event() + "\n" + Event()).Events.Count == 0);
        });
        test("ICS import skips unsupported groups while retaining valid groups", () =>
        {
            var result = Parse(Event("RRULE:FREQ=HOURLY") + "\n" + Event(uid: "supported")); check(result.Events.Count == 1 && result.Warnings.Count == 1);
        });
        test("ICS reimport preserves locally edited series and exceptions", () =>
        {
            var result = Parse(Event("RRULE:FREQ=DAILY;COUNT=3\nEXDATE:20261105T090000"));
            var first = IcalendarImporter.Merge(CalendarDocument.Empty(), [result], out var added); check(added == 2);
            var edited = first.WithEvents(first.Events.Select(e => e with { Title = "Local edit" }));
            var second = IcalendarImporter.Merge(edited, [result, result], out added);
            check(added == 0 && second.Events.Count == 2 && second.Events.All(e => e.Title == "Local edit") && second.CalendarList.Count == 2);
        });
        test("ICS import uses calendar name plus UID across renamed files", () =>
        {
            var first = IcalendarImporter.Parse(Wrap(Event(), "X-WR-CALNAME:Work\n"), "one"); var second = IcalendarImporter.Parse(Wrap(Event(), "X-WR-CALNAME:Work\n"), "two");
            check(first.Events[0].Id == second.Events[0].Id && first.Calendar == second.Calendar);
            check(first.Events[0].Id != Parse(Event()).Events[0].Id);
        });
        test("ICS imports are one undoable change and leave the export intact", () =>
        {
            var path = Path.Combine(folder, "export.ics"); var source = Wrap(Event()); File.WriteAllText(path, source);
            var imported = IcalendarImporter.Read(path); var history = new CalendarHistory(CalendarDocument.Empty()); var store = new CalendarStore(Path.Combine(folder, "imported-calendar.json"));
            history.Commit(IcalendarImporter.Merge(history.Current, [imported], out _), "Calendar import", store.Save);
            check(history.Undo(store.Save) && store.Load().Events.Count == 0 && File.ReadAllText(path) == source);
        });
        test("ICS malformed structure, calendar requests and oversized lines are rejected", () =>
        {
            foreach (var text in new[] { "not a calendar", "BEGIN:VCALENDAR\nVERSION:2.0", Wrap(Event(), "METHOD:REQUEST\n"), Wrap(Event()) + "\n" + Wrap(Event()), Wrap(Event("DESCRIPTION:" + new string('x', 100001))) }) reject(() => IcalendarImporter.Parse(text, "Test"));
        });
        test("account inference uses real provider domains and strips URL credentials", () =>
        {
            var a = ImportIdentity.Account("Test", "Work", "person@example.com", "https://user:password@caldav.icloud.com/private?token=secret")!;
            check(a.Provider == "iCloud" && a.Server == "https://caldav.icloud.com");
            check(ImportIdentity.Account("Test", "", "person@example.com", "https://google.com.evil.example", "caldav")!.Provider == "CalDAV");
            check(ImportIdentity.Account("Test", "", "person@gmail.com", "")!.Provider == "Google");
            check(ImportIdentity.Account("Test", "", "person@example.com", "", "exchange")!.Provider == "Microsoft");
            check(ImportIdentity.Account("Test", "", "bad\nlogin", "") is null);
        });
        test("account drafts deduplicate known providers across installed apps", () =>
        {
            var a = ImportIdentity.Account("Fantastical", "Personal", "person@gmail.com", "https://apidata.googleusercontent.com")!;
            var b = ImportIdentity.Account("Thunderbird", "Personal", "PERSON@gmail.com", "https://www.googleapis.com")!;
            var result = ImportIdentity.Deduplicate([a, b]); check(result.Count == 1 && result[0].Source.Contains("Fantastical") && result[0].Source.Contains("Thunderbird"));
        });
        test("unknown mail identity merges with a discovered organization calendar", () =>
        {
            var a = ImportIdentity.Account("Thunderbird", "", "person@example.com", "")!;
            var b = ImportIdentity.Account("Fantastical", "", "person@example.com", "https://outlook.office365.com", "exchange")!;
            var result = ImportIdentity.Deduplicate([a, b]); check(result.Count == 1 && result[0].Provider == "Microsoft");
        });
        test("account draft persistence is idempotent and contains no credential URLs", () =>
        {
            var path = Path.Combine(folder, "profiles.json"); var store = new AccountDraftStore(path);
            var a = ImportIdentity.Account("Fantastical", "Personal", "person@icloud.com", "https://caldav.icloud.com/path?secret=ignored")!;
            store.Merge([a, a]); store.Merge([a]); check(store.Read().Count == 1 && !File.ReadAllText(path).Contains("ignored")); store.Remove(a.Id); check(store.Read().Count == 0);
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { a with { Login = null! } })); reject(() => store.Read());
        });
        test("Thunderbird parser reads whitelisted preferences without evaluating code", () =>
        {
            var text = "user_pref(\"calendar.registry.id.username\", \"person@gmail.com\");\nuser_pref(\"calendar.registry.id.uri\", \"https://www.googleapis.com/calendar/secret?token=x\");\nuser_pref(\"calendar.registry.id.name\", \"Work\\u0020calendar\");\nuser_pref(\"password\", \"ignored\");\nuser_pref(\"extensions.activeThemeID\", \"thunderbird-dark@mozilla.org\");\nmalicious();";
            var report = new DiscoveryReport([], [], [], []); ThunderbirdPreferences.AddTo(report, text, "Thunderbird");
            check(report.Accounts.Single().Login == "person@gmail.com" && report.Accounts[0].Name == "Work calendar" && report.Settings.Single().Theme == "Dark" && !ThunderbirdPreferences.Parse(text).ContainsKey("password"));
        });
        test("Fantastical binary plist imports metadata and skips subscription identity", () =>
        {
            var bytes = Convert.FromBase64String("YnBsaXN0MDDSAQIDDlJzMVJzMtUEBQYHCAkKCwwNVE5hbWVYUGFzc3dvcmRZU2VydmVyVVJMVFR5cGVYVXNlcm5hbWVkAEMAYQBmAOldZG8tbm90LWltcG9ydF8QMmh0dHBzOi8vY2FsZGF2LmljbG91ZC5jb20vcHJpdmF0ZT90b2tlbj1ub3QtY29waWVkVmNhbGRhdl8QEnBlcnNvbkBleGFtcGxlLmNvbdIHCA8QWWZsZXhpYml0c18QGHN1YnNjcmlwdGlvbkBleGFtcGxlLmNvbQgNEBMeIyw2O0RNW5CXrLG7AAAAAAAAAQEAAAAAAAAAEQAAAAAAAAAAAAAAAAAAANY=");
            var accounts = BinaryPlist.Accounts(bytes, "Fantastical"); check(accounts.Count == 1 && accounts[0].Name == "Café" && accounts[0].Provider == "iCloud");
            check(!JsonSerializer.Serialize(BinaryPlist.Read(bytes)).Contains("do-not-import"));
            foreach (var cut in new[] { 0, 8, 39, bytes.Length - 10 }) reject(() => BinaryPlist.Read(bytes[..cut]));
        });
        test("classic Outlook metadata decodes Unicode and does not request credential fields", () =>
        {
            var values = new Dictionary<string, object?> { ["Email"] = System.Text.Encoding.Unicode.GetBytes("person@example.com\0"), ["Service Name"] = "MSEMS", ["Account Name"] = "Organization" };
            var requested = new List<string>(); var account = OutlookProfileIdentity.Read(key => { requested.Add(key); return values.GetValueOrDefault(key); });
            check(account?.Provider == "Microsoft" && account.Login == "person@example.com" && requested.All(k => k is "Email" or "SMTP Server" or "Service Name" or "Account Name"));
        });
        test("Fantastical XML settings import only supported duration and explicit standard theme", () =>
        {
            var report = new DiscoveryReport([], [], [], []);
            FantasticalPreferences.AddTo(report, "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"https://example.invalid/never-fetched\"><plist xml:space=\"preserve\"><dict>\n<key>DefaultEventDuration</key>\n<integer>2700</integer><key>LightThemeFile</key><string>dark.theme</string><key>DarkThemeFile</key><string>dark.theme</string><key>FirstWeekday</key><integer>1</integer></dict></plist>");
            check(report.Settings.Any(s => s.DefaultDurationMinutes == 45) && report.Settings.Any(s => s.Theme == "Dark") && report.Notes.Count == 1);
            var unsupported = new DiscoveryReport([], [], [], []); FantasticalPreferences.AddTo(unsupported, "<plist><dict><key>DefaultEventDuration</key><integer>99</integer><key>LightThemeFile</key><string>custom.theme</string><key>DarkThemeFile</key><string>custom.theme</string></dict></plist>"); check(unsupported.Settings.Count == 0);
        });
        test("installed app scan discovers profile ICS and leaves original files unchanged", () =>
        {
            var roaming = Path.Combine(folder, "roaming"); var local = Path.Combine(folder, "local"); var profile = Path.Combine(roaming, "Thunderbird", "Profiles", "test.default"); Directory.CreateDirectory(profile);
            var prefs = "user_pref(\"mail.identity.id1.useremail\", \"person@outlook.com\");\nuser_pref(\"extensions.activeThemeID\", \"thunderbird-light@mozilla.org\");";
            File.WriteAllText(Path.Combine(profile, "prefs.js"), prefs); File.WriteAllText(Path.Combine(profile, "calendar.ics"), Wrap(Event()));
            var report = InstalledCalendarDiscovery.Scan(roaming, local, false);
            check(report.Accounts.Any(a => a.Login == "person@outlook.com") && report.CalendarFiles.Contains(Path.Combine(profile, "calendar.ics")) && report.Settings.Single().Theme == "Light");
            check(File.ReadAllText(Path.Combine(profile, "prefs.js")) == prefs);
        });
    }
}
