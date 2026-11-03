namespace Daymark.Core;

public enum RepeatedTimeChoice { Earlier, Later }

public static class CalendarTime
{
    public static DateOnly Today(TimeZoneInfo zone, DateTime? utcNow = null) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, zone));
    public static DateTime KeepSubminutePrecision(DateTime entered, DateTime? original) =>
        original is { } prior && prior.AddTicks(-(prior.Ticks % TimeSpan.TicksPerMinute)) == entered ? prior : entered;
    public static string ZoneId(TimeZoneInfo zone) => TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var id) ? id : zone.Id;

    public static TimeZoneInfo FindZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new InvalidDataException($"The time zone '{id}' is unavailable on this computer.", error); }
    }

    public static DateTime Resolve(DateTime wallTime, TimeZoneInfo zone, RepeatedTimeChoice? choice = null)
    {
        if (wallTime.Kind != DateTimeKind.Unspecified) throw new InvalidDataException("Enter a clock time without a UTC suffix.");
        if (zone.IsInvalidTime(wallTime))
            throw new InvalidDataException($"{wallTime:g} does not exist in {ZoneId(zone)} because the clocks move forward. Choose another time.");
        if (!zone.IsAmbiguousTime(wallTime)) return TimeZoneInfo.ConvertTimeToUtc(wallTime, zone);
        if (choice is null || !Enum.IsDefined(choice.Value))
            throw new InvalidDataException($"{wallTime:g} occurs twice in {ZoneId(zone)}. Choose the first or second occurrence.");
        var instants = zone.GetAmbiguousTimeOffsets(wallTime).Select(offset => new DateTimeOffset(wallTime, offset).UtcDateTime).Order().ToArray();
        return choice == RepeatedTimeChoice.Earlier ? instants[0] : instants[^1];
    }

    public static CalendarEvent Anchor(CalendarEvent item, TimeZoneInfo zone,
        RepeatedTimeChoice? startChoice = null, RepeatedTimeChoice? endChoice = null)
    {
        if (item.AllDay) throw new InvalidDataException("All-day events use dates, not time zones.");
        var anchored = item with { TimeZoneId = ZoneId(zone), StartUtc = Resolve(item.Start, zone, startChoice), EndUtc = Resolve(item.End, zone, endChoice) };
        anchored.Validate(); return anchored;
    }

    public static CalendarEvent InZone(CalendarEvent item, TimeZoneInfo zone) => item.TimeZoneId is null ? item : item with
    {
        Start = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(item.StartUtc!.Value, zone), DateTimeKind.Unspecified),
        End = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(item.EndUtc!.Value, zone), DateTimeKind.Unspecified),
        TimeZoneId = ZoneId(zone)
    };

    public static RepeatedTimeChoice? ChoiceFor(DateTime? utc, TimeZoneInfo zone)
    {
        if (utc is null) return null;
        var wall = TimeZoneInfo.ConvertTimeFromUtc(utc.Value, zone);
        if (!zone.IsAmbiguousTime(wall)) return null;
        return Resolve(wall, zone, RepeatedTimeChoice.Earlier) == utc ? RepeatedTimeChoice.Earlier : RepeatedTimeChoice.Later;
    }

    public static string Describe(CalendarEvent item)
    {
        if (item.AllDay) return $"{item.Start:d} · All day";
        if (item.TimeZoneId is null) return $"{item.Start:g} – {item.End:g} · Floating time";
        var zone = FindZone(item.TimeZoneId);
        string Offset(DateTime utc) => new DateTimeOffset(utc).ToOffset(zone.GetUtcOffset(utc)).ToString("zzz");
        return $"{item.Start:g} UTC{Offset(item.StartUtc!.Value)} – {item.End:g} UTC{Offset(item.EndUtc!.Value)}\n{item.TimeZoneId}";
    }
}
