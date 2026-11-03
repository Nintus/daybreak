using Daymark.Core;

namespace Daymark.Windows;

public sealed record ZoneChoice(string? Id, string Label)
{
    public static IReadOnlyList<ZoneChoice> All { get; } = new[] { new ZoneChoice(null, "Floating — same clock time everywhere") }
        .Concat(TimeZoneInfo.GetSystemTimeZones().Select(zone => new ZoneChoice(CalendarTime.ZoneId(zone), $"{CalendarTime.ZoneId(zone)} · {zone.DisplayName}"))).ToArray();
}
