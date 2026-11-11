namespace Daymark.Core;

public sealed record WeekSlot(CalendarEvent Event, double StartMinute, double EndMinute, int Column, int ColumnCount);

public static class WeekLayout
{
    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static int SnapMoveMinute(double minute) => (int)Math.Clamp(Math.Round(minute / 15, MidpointRounding.AwayFromZero) * 15, 0, 1425);

    // Short appointments occupy at least 20 minutes of display space so they remain selectable.
    public static IReadOnlyList<WeekSlot> TimedDay(IEnumerable<CalendarEvent> events, DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var slots = events.Where(e => !e.AllDay && e.Start < end && e.End > start)
            .Select(e => new WeekSlot(e, Math.Max(0, (e.Start - start).TotalMinutes),
                Math.Min(1440, (e.End - start).TotalMinutes), 0, 1))
            .OrderBy(e => e.StartMinute).ThenByDescending(e => e.EndMinute).ThenBy(e => e.Event.Id).ToList();
        var result = new List<WeekSlot>();
        var group = new List<WeekSlot>();
        var columnEnds = new List<double>();
        double groupEnd = -1;
        void FinishGroup()
        {
            result.AddRange(group.Select(slot => slot with { ColumnCount = columnEnds.Count }));
            group.Clear(); columnEnds.Clear();
        }
        foreach (var slot in slots)
        {
            if (slot.StartMinute >= groupEnd) FinishGroup();
            var displayEnd = Math.Min(1440, Math.Max(slot.EndMinute, slot.StartMinute + 20));
            var column = columnEnds.FindIndex(value => value <= slot.StartMinute);
            if (column < 0) { column = columnEnds.Count; columnEnds.Add(displayEnd); }
            else columnEnds[column] = displayEnd;
            group.Add(slot with { Column = column });
            groupEnd = Math.Max(group.Count == 1 ? -1 : groupEnd, displayEnd);
        }
        FinishGroup();
        return result;
    }
}
