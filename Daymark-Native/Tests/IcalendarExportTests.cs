using System.Text;
using Daymark.Core;
using Daymark.Migration;

internal static class IcalendarExportTests
{
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject, string folder)
    {
        var day = new DateOnly(2026, 3, 8);
        var start = day.ToDateTime(new TimeOnly(9, 0));
        var path = Path.Combine(folder, "daymark-export.ics");
        var zone = CalendarTime.FindZone("America/New_York");

        test("ICS export round-trips Unicode, all-day dates and anchored instants for one calendar", () =>
        {
            var notes = string.Concat(Enumerable.Repeat("🌙 café, room; A\\B", 12)) + "\nBEGIN:VEVENT";
            var floating = new CalendarEvent(Guid.NewGuid(), "Floating", start, start.AddHours(1), Notes: notes);
            var anchored = CalendarTime.Anchor(floating with { Id = Guid.NewGuid(), Title = "Overnight", Start = start.Date.AddHours(-1), End = start.Date.AddHours(1), Notes = "" }, zone);
            var allDay = new CalendarEvent(Guid.NewGuid(), "Trip", start.Date.AddDays(-1), start.Date.AddDays(1), AllDay: true);
            var privateEvent = floating with { Id = Guid.NewGuid(), CalendarId = "other", Title = "Other calendar" };
            var document = new CalendarDocument(3, [floating, anchored, allDay, privateEvent], [CalendarDefinition.Personal, new("other", "Other")]);

            check(IcalendarExporter.Save(document, "personal", day, day, zone, path) == 3);
            var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
            check(text.StartsWith("BEGIN:VCALENDAR\r\n", StringComparison.Ordinal) && text.Contains("\r\n "));
            check(text.Split("\r\n").All(line => Encoding.UTF8.GetByteCount(line) <= 75 && !line.Contains('\n')));
            var read = IcalendarImporter.Read(path);
            check(read.Warnings.Count == 0 && read.Events.Count == 3);
            check(read.Events.Any(item => item.Title == "Floating" && item.StartUtc is null && item.Notes == notes));
            check(read.Events.Any(item => item.Title == "Overnight" && item.StartUtc == anchored.StartUtc && item.EndUtc == anchored.EndUtc));
            check(read.Events.Any(item => item.AllDay && item.Start == allDay.Start && item.End == allDay.End));
        });

        test("ICS export keeps moved occurrences, skips cancellations and uses stable occurrence IDs", () =>
        {
            var seed = start.AddDays(-2);
            var series = CalendarTime.Anchor(new(Guid.NewGuid(), "Daily", seed, seed.AddHours(1),
                Recurrence: new(RecurrenceFrequency.Daily, Count: 5)), zone);
            var moved = CalendarTime.Anchor(new(Guid.NewGuid(), "Moved", start.AddDays(1).AddHours(4), start.AddDays(1).AddHours(5),
                SeriesId: series.Id, OriginalStart: seed), zone);
            var skipped = CalendarTime.Anchor(new(Guid.NewGuid(), "Skipped", start.AddDays(-1), start.AddDays(-1).AddHours(1),
                SeriesId: series.Id, OriginalStart: start.AddDays(-1), IsCancelled: true), zone);
            var document = new CalendarDocument(3, [series, moved, skipped]);
            document.Validate();

            check(IcalendarExporter.Save(document, "personal", day.AddDays(-1), day.AddDays(1), zone, path) == 3);
            var first = IcalendarImporter.Read(path).Events;
            check(first.All(item => item.Recurrence is null) && first.Any(item => item.Title == "Moved" && item.StartUtc == moved.StartUtc));
            check(first.Any(item => item.Title == "Daily" && item.StartUtc == new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc)));
            check(IcalendarExporter.Save(document, "personal", day.AddDays(1), day.AddDays(2), zone, path) == 3);
            var second = IcalendarImporter.Read(path).Events;
            check(first.Select(item => item.Id).Intersect(second.Select(item => item.Id)).Count() == 2);
        });

        test("failed ICS export leaves the previous destination intact", () =>
        {
            File.WriteAllText(path, "previous file");
            var tooShort = new CalendarEvent(Guid.NewGuid(), "Fractional event", start, start.AddMilliseconds(100));
            var document = new CalendarDocument(3, [tooShort]);
            reject(() => IcalendarExporter.Save(document, "personal", day, day, zone, path));
            check(File.ReadAllText(path) == "previous file");
        });
    }
}
