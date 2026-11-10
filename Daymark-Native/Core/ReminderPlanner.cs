namespace Daymark.Core;

public readonly record struct ReminderKey(Guid EventOrSeriesId, DateTime? OriginalStart, DateTime TriggerUtc);
public sealed record ReminderAlert(ReminderKey Key, CalendarEvent Event, DateTime DueUtc, DateTime ExpiresUtc);
public sealed record ReminderPlan(IReadOnlyList<ReminderAlert> Due, DateTime? NextCheckUtc);

public static class ReminderPlanner
{
    public const int MaximumLeadMinutes = 7 * 24 * 60;
    private static readonly TimeSpan CatchUp = TimeSpan.FromHours(12);

    // A null disposition dismisses this trigger. A timestamp postpones it.
    public static ReminderPlan Plan(CalendarDocument document, DateTime nowUtc, TimeZoneInfo deviceZone,
        IReadOnlyDictionary<ReminderKey, DateTime?> dispositions)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The reminder clock must use UTC.", nameof(nowUtc));

        var enabled = document.Events.Where(item => !item.IsCancelled && item.ReminderMinutes.HasValue).ToList();
        if (enabled.Count == 0) return new([], null);

        // Keep every override of an enabled series so a moved or skipped occurrence
        // suppresses its original, even when the override has reminders turned off.
        var enabledIds = enabled.Select(item => item.SeriesId ?? item.Id).ToHashSet();
        var selected = document.WithEvents(document.Events.Where(item => enabledIds.Contains(item.SeriesId ?? item.Id)));
        var today = CalendarTime.Today(deviceZone, nowUtc);
        var first = DateOnly.FromDayNumber(Math.Max(0, today.DayNumber - 2));
        var days = Math.Min(12, DateOnly.MaxValue.DayNumber - first.DayNumber);
        var occurrences = Recurrence.InRange(selected, first, days, deviceZone).Events;
        var nextCheck = nowUtc.AddDays(1);
        var due = new List<ReminderAlert>();

        foreach (var item in occurrences)
        {
            if (item.ReminderMinutes is not { } minutes) continue;
            var wall = item.AllDay ? item.Start.AddHours(9) : item.Start;
            var start = item.StartUtc ?? ResolveWall(wall, deviceZone, RepeatedTimeChoice.Earlier);
            var end = item.EndUtc ?? ResolveWall(item.End, deviceZone, RepeatedTimeChoice.Later);
            if (start is not { } startUtc || end is not { } endUtc || endUtc <= nowUtc) continue;
            var reference = item.AllDay ? wall : startUtc;
            if (reference.Ticks < TimeSpan.FromMinutes(minutes).Ticks) continue;
            var offset = reference.AddMinutes(-minutes);
            var scheduled = item.AllDay ? ResolveWall(offset, deviceZone, RepeatedTimeChoice.Earlier) : offset;
            if (scheduled is not { } trigger) continue;
            var key = new ReminderKey(item.SeriesId ?? item.Id, item.OriginalStart, trigger);
            var effective = trigger;
            if (dispositions.TryGetValue(key, out var disposition))
            {
                if (disposition is null) continue;
                effective = disposition.Value;
            }
            if (effective >= endUtc || effective < nowUtc - CatchUp) continue;
            if (effective > nowUtc)
            {
                if (effective < nextCheck) nextCheck = effective;
                continue;
            }

            due.Add(new(key, item, effective, endUtc));
            var staleAt = effective + CatchUp + TimeSpan.FromSeconds(1);
            if (staleAt < nextCheck) nextCheck = staleAt;
            if (endUtc < nextCheck) nextCheck = endUtc;
        }

        return new(due.OrderBy(item => item.DueUtc).ThenBy(item => item.Event.Start).ToList(), nextCheck);
    }

    private static DateTime? ResolveWall(DateTime wall, TimeZoneInfo zone, RepeatedTimeChoice choice) =>
        zone.IsInvalidTime(wall) ? null : CalendarTime.Resolve(wall, zone, choice);
}
