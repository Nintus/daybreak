namespace Daymark.Core;

public enum RecurrenceFrequency { Daily, Weekly, Monthly, Yearly }
public enum MonthlyPattern { DayOfMonth, OrdinalWeekday, LastWeekday }

// Weekday bits use DayOfWeek's Sunday=0 ordering. Weeks are anchored on Monday.
public sealed record RecurrenceRule(RecurrenceFrequency Frequency, int Interval = 1,
    int Weekdays = 0, MonthlyPattern MonthlyPattern = MonthlyPattern.DayOfMonth,
    int? Count = null, DateOnly? Until = null)
{
    public void Validate(DateTime start)
    {
        if (!Enum.IsDefined(Frequency) || !Enum.IsDefined(MonthlyPattern) || Interval is < 1 or > 999 ||
            Weekdays is < 0 or > 127 || Count is < 1 or > 10000 || Count is not null && Until is not null ||
            Until < DateOnly.FromDateTime(start))
            throw new InvalidDataException("Choose a valid repeat interval and either an end date or 1–10,000 occurrences.");
        if (Frequency != RecurrenceFrequency.Weekly && Weekdays != 0 ||
            Frequency != RecurrenceFrequency.Monthly && MonthlyPattern != MonthlyPattern.DayOfMonth)
            throw new InvalidDataException("The repeat options do not match the selected frequency.");
        if (Frequency == RecurrenceFrequency.Weekly && Weekdays != 0 && (Weekdays & (1 << (int)start.DayOfWeek)) == 0)
            throw new InvalidDataException("The repeat weekdays must include the event's start day.");
        if (Frequency == RecurrenceFrequency.Monthly && MonthlyPattern == MonthlyPattern.LastWeekday && start.Day + 7 <= DateTime.DaysInMonth(start.Year, start.Month))
            throw new InvalidDataException("To repeat on the last weekday, choose a start date on the last occurrence of that weekday in its month.");
    }

    public string Describe() => $"Every {(Interval == 1 ? "" : Interval + " ")}{Frequency switch { RecurrenceFrequency.Daily => Interval == 1 ? "day" : "days", RecurrenceFrequency.Weekly => Interval == 1 ? "week" : "weeks", RecurrenceFrequency.Monthly => Interval == 1 ? "month" : "months", _ => Interval == 1 ? "year" : "years" }}" +
        (Count is { } count ? $" · {count} occurrences" : Until is { } until ? $" · through {until:d}" : "");
}

public static class Recurrence
{
    public static bool IsScheduled(CalendarEvent master, DateTime original)
    {
        if (master.Recurrence is null || original.Kind != DateTimeKind.Unspecified) return false;
        var zone = master.TimeZoneId is null ? TimeZoneInfo.Utc : CalendarTime.FindZone(master.TimeZoneId);
        var day = DateOnly.FromDateTime(original);
        if (day == DateOnly.MaxValue) return false;
        return InRange(new(3, [master]), day, 1, zone).Events.Any(e => e.OriginalStart == original);
    }

    public static CalendarDocument InRange(CalendarDocument document, DateOnly first, int days, TimeZoneInfo displayZone,
        ISet<string>? hiddenCalendars = null)
    {
        if (days is < 1 or > 1096 || first.DayNumber > DateOnly.MaxValue.DayNumber - days)
            throw new ArgumentOutOfRangeException(nameof(days));
        var from = first.ToDateTime(TimeOnly.MinValue);
        var through = from.AddDays(days);
        var output = new List<CalendarEvent>();
        var exceptions = document.Events.Where(e => e.SeriesId is not null).ToLookup(e => e.SeriesId!.Value);
        void Add(CalendarEvent item)
        {
            if (item.IsCancelled || hiddenCalendars?.Contains(item.CalendarId) == true) return;
            var local = CalendarTime.InZone(item, displayZone);
            if (local.Start >= through || local.End <= from) return;
            if (output.Count >= 100000) throw new InvalidDataException("Too many occurrences in this view. Use a shorter date range.");
            output.Add(local);
        }
        foreach (var item in document.Events.Where(e => e.SeriesId is null))
        {
            if (item.Recurrence is null) { Add(item); continue; }
            item.Recurrence.Validate(item.Start);
            if (hiddenCalendars?.Contains(item.CalendarId) == true) continue;
            var overrides = exceptions[item.Id].ToDictionary(e => e.OriginalStart!.Value);
            // A moved exception can fall inside this range while its original date falls outside it.
            foreach (var replacement in overrides.Values) Add(replacement);
            var zone = item.TimeZoneId is null ? null : CalendarTime.FindZone(item.TimeZoneId);
            // A series repeats its source wall-clock schedule. Its seed UTC instants
            // supply elapsed duration; a timezone-rule update must not move every 9am meeting.
            var sourceStart = item.Start;
            var duration = zone is null ? item.End - item.Start : item.EndUtc!.Value - item.StartUtc!.Value;
            // UTC offsets are bounded by a day. Include duration for overnight/multi-day overlap.
            var lookback = (int)Math.Min(3660000, Math.Ceiling(duration.TotalDays) + 2);
            var earliest = DateOnly.FromDayNumber(Math.Max(0, first.DayNumber - lookback));
            var latest = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, first.DayNumber + days + 2));
            var ordinal = 0;
            foreach (var date in Candidates(sourceStart, item.Recurrence, item.Recurrence.Count is null ? earliest : DateOnly.FromDateTime(sourceStart), latest))
            {
                var wall = date.ToDateTime(TimeOnly.FromDateTime(sourceStart));
                if (zone is not null && zone.IsInvalidTime(wall)) continue; // RFC-style skipped local dates don't consume COUNT.
                ordinal++;
                if (item.Recurrence.Count is { } count && ordinal > count) break;
                if (overrides.ContainsKey(wall)) continue;
                var occurrence = item with { Recurrence = null, SeriesId = item.Id, OriginalStart = wall, Start = wall };
                try
                {
                    if (zone is null) occurrence = occurrence with { End = wall.Add(duration) };
                    else
                    {
                        var choice = CalendarTime.ChoiceFor(item.StartUtc, zone) ?? RepeatedTimeChoice.Earlier;
                        var utc = CalendarTime.Resolve(wall, zone, choice);
                        var end = utc.Add(duration);
                        occurrence = occurrence with { StartUtc = utc, EndUtc = end,
                            End = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(end, zone), DateTimeKind.Unspecified) };
                    }
                }
                catch (ArgumentOutOfRangeException) { continue; }
                Add(occurrence);
            }
        }
        return document with { Events = output };
    }

    private static IEnumerable<DateOnly> Candidates(DateTime start, RecurrenceRule rule, DateOnly first, DateOnly last)
    {
        var seed = DateOnly.FromDateTime(start);
        var end = rule.Until is { } until && until < last ? until : last;
        if (end < seed) yield break;
        if (rule.Frequency is RecurrenceFrequency.Daily or RecurrenceFrequency.Weekly)
        {
            var anchor = rule.Frequency == RecurrenceFrequency.Weekly ? seed.DayNumber - (((int)seed.DayOfWeek + 6) % 7) : seed.DayNumber;
            var period = rule.Frequency == RecurrenceFrequency.Daily ? rule.Interval : rule.Interval * 7;
            var offset = Math.Max(0, (first.DayNumber - anchor) / period);
            for (long cycle = anchor + (long)offset * period; cycle <= end.DayNumber; cycle += period)
            {
                var span = rule.Frequency == RecurrenceFrequency.Weekly ? 7 : 1;
                for (var d = 0; d < span && cycle + d <= end.DayNumber; d++)
                {
                    var date = DateOnly.FromDayNumber((int)cycle + d);
                    if (date < seed || date < first) continue;
                    var mask = rule.Weekdays == 0 ? 1 << (int)seed.DayOfWeek : rule.Weekdays;
                    if (span == 1 || (mask & (1 << (int)date.DayOfWeek)) != 0) yield return date;
                }
            }
            yield break;
        }
        var stride = rule.Frequency == RecurrenceFrequency.Yearly ? rule.Interval * 12 : rule.Interval;
        var seedMonth = (seed.Year - 1) * 12 + seed.Month - 1;
        var firstMonth = (first.Year - 1) * 12 + first.Month - 1;
        var offsetMonth = Math.Max(0, (firstMonth - seedMonth) / stride);
        var lastMonth = (end.Year - 1) * 12 + end.Month - 1;
        for (var month = seedMonth + offsetMonth * stride; month <= lastMonth; month += stride)
        {
            var year = month / 12 + 1; var number = month % 12 + 1;
            var max = DateTime.DaysInMonth(year, number); var day = seed.Day;
            if (rule.Frequency == RecurrenceFrequency.Monthly && rule.MonthlyPattern != MonthlyPattern.DayOfMonth)
            {
                var firstDay = new DateOnly(year, number, 1);
                day = 1 + (((int)seed.DayOfWeek - (int)firstDay.DayOfWeek + 7) % 7);
                day += rule.MonthlyPattern == MonthlyPattern.LastWeekday ? (max - day) / 7 * 7 : (seed.Day - 1) / 7 * 7;
            }
            if (day > max) continue; // January 31 doesn't become February 28; leap dates skip non-leap years.
            var date = new DateOnly(year, number, day);
            if (date >= first && date >= seed && date <= end) yield return date;
        }
    }
}
