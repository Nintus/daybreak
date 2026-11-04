namespace Daymark.Core;

public static class CalendarSearch
{
    // Unbounded series have no finite 'all results'. Search their nearby past and next year,
    // while retaining all-time search for individual appointments.
    public static IReadOnlyList<CalendarEvent> Find(CalendarDocument document, string query, DateOnly anchor,
        TimeZoneInfo zone, ISet<string>? hidden = null)
    {
        query = query.Trim();
        if (query.Length == 0) return [];
        bool Matches(CalendarEvent e) => (e.Title + "\n" + e.Location + "\n" + e.Notes).Contains(query, StringComparison.OrdinalIgnoreCase);
        var seriesIds = document.Events.Where(Matches).Select(e => e.SeriesId ?? e.Id).ToHashSet();
        var matchingSeries = document.WithEvents(document.Events.Where(e => seriesIds.Contains(e.SeriesId ?? e.Id)));
        var first = DateOnly.FromDayNumber(Math.Max(0, anchor.DayNumber - 31));
        var days = Math.Min(397, DateOnly.MaxValue.DayNumber - first.DayNumber);
        var repeats = Recurrence.InRange(matchingSeries, first, days, zone, hidden).Events.Where(e => e.SeriesId is not null && Matches(e));
        return document.Events.Where(e => e.SeriesId is null && e.Recurrence is null && Matches(e) && hidden?.Contains(e.CalendarId) != true)
            .Select(e => CalendarTime.InZone(e, zone)).Concat(repeats)
            .OrderBy(e => e.Start).ThenBy(e => e.Title).Take(200).ToArray();
    }
}
