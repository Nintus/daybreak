namespace Daymark.Core;

public static class CalendarChanges
{
    public static CalendarEvent SourceOccurrence(CalendarDocument document, CalendarEvent displayed)
    {
        if (displayed.SeriesId is not { } series) return document.Events.Single(e => e.Id == displayed.Id);
        var original = displayed.OriginalStart!.Value;
        var changed = document.Events.SingleOrDefault(e => e.SeriesId == series && e.OriginalStart == original);
        if (changed is not null) return changed;
        var master = document.Events.Single(e => e.Id == series);
        var day = DateOnly.FromDateTime(original);
        var zone = master.TimeZoneId is null ? TimeZoneInfo.Utc : CalendarTime.FindZone(master.TimeZoneId);
        var occurrence = Recurrence.InRange(new(3, [master]), day, 1, zone).Events.Single(e => e.OriginalStart == original);
        return occurrence with { Id = Guid.NewGuid() };
    }

    public static bool ScheduleChanged(CalendarEvent before, CalendarEvent after) =>
        before.Start != after.Start || before.End != after.End || before.StartUtc != after.StartUtc || before.EndUtc != after.EndUtc ||
        before.TimeZoneId != after.TimeZoneId || before.AllDay != after.AllDay || Normalize(before) != Normalize(after);

    private static RecurrenceRule? Normalize(CalendarEvent item) => item.Recurrence is { Frequency: RecurrenceFrequency.Weekly, Weekdays: 0 } rule
        ? rule with { Weekdays = 1 << (int)item.Start.DayOfWeek } : item.Recurrence;

    public static CalendarDocument SaveEvent(CalendarDocument document, CalendarEvent item, bool resetExceptions = false)
    {
        var prior = document.Events.SingleOrDefault(e => e.Id == item.Id);
        var children = document.Events.Where(e => e.SeriesId == item.Id).ToList();
        if (prior is not null && children.Count > 0 && ScheduleChanged(prior, item) && !resetExceptions)
            throw new InvalidDataException("Changing the series schedule requires resetting its individual occurrence changes.");
        var events = document.Events.Where(e => e.Id != item.Id &&
            !(resetExceptions && e.SeriesId == item.Id) &&
            !(item.SeriesId is not null && e.SeriesId == item.SeriesId && e.OriginalStart == item.OriginalStart))
            .Select(e => e.SeriesId == item.Id ? e with { CalendarId = item.CalendarId } : e).ToList();
        events.Add(item);
        var next = document.WithEvents(events); next.Validate(); return next;
    }

    public static CalendarDocument CancelOccurrence(CalendarDocument document, CalendarEvent displayed)
    {
        var occurrence = SourceOccurrence(document, displayed);
        if (occurrence.SeriesId is null) throw new InvalidDataException("This event is not part of a series.");
        return SaveEvent(document, occurrence with { IsCancelled = true });
    }

    public static CalendarDocument Move(CalendarDocument document, CalendarEvent displayed, DateOnly date, TimeOnly time,
        TimeZoneInfo displayZone, RepeatedTimeChoice? choice = null)
    {
        if (date.Year is < 2 or > 9998) throw new InvalidDataException("Choose a date between years 2 and 9998.");
        var source = SourceOccurrence(document, displayed);
        if (source.Recurrence is not null || source.IsCancelled)
            throw new InvalidDataException("Choose a visible occurrence to move.");

        var start = date.ToDateTime(source.AllDay ? TimeOnly.MinValue : time);
        CalendarEvent moved;
        if (source.TimeZoneId is { } zoneId)
        {
            var instant = CalendarTime.Resolve(start, displayZone, choice);
            var duration = source.EndUtc!.Value - source.StartUtc!.Value;
            moved = CalendarTime.InZone(source with { StartUtc = instant, EndUtc = instant.Add(duration) }, CalendarTime.FindZone(zoneId))
                with { TimeZoneId = zoneId };
        }
        else
        {
            moved = source with { Start = start, End = start.Add(source.End - source.Start) };
        }
        return ScheduleChanged(source, moved) ? SaveEvent(document, moved) : document;
    }

    public static CalendarDocument DeleteEventOrSeries(CalendarDocument document, Guid id)
    {
        var next = document.WithEvents(document.Events.Where(e => e.Id != id && e.SeriesId != id));
        if (document.TaskList.Any(task => task.ScheduledEventId == id))
            next = next with { Tasks = document.TaskList.Select(task => task.ScheduledEventId == id ? task with { ScheduledEventId = null } : task).ToList() };
        next.Validate(); return next;
    }
}
