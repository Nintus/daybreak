using Daymark.Core;

namespace Daymark.Windows;

internal static class EventPresentation
{
    public static string Summary(CalendarEvent item)
    {
        if (item.AllDay) return item.End.Date - item.Start.Date == TimeSpan.FromDays(1) ? "All day"
            : $"All day · {item.Start:MMM d} – {item.End.AddDays(-1):MMM d}";
        var time = item.Start.Date == item.End.Date ? $"{item.Start:t} – {item.End:t}"
            : $"{item.Start:MMM d} {item.Start:t} – {item.End:MMM d} {item.End:t}";
        if (item.TimeZoneId is null) return time + "\nFloating time";
        var zone = CalendarTime.FindZone(item.TimeZoneId);
        var offset = zone.GetUtcOffset(item.StartUtc!.Value);
        var endOffset = zone.GetUtcOffset(item.EndUtc!.Value);
        var city = item.TimeZoneId.Split('/').Last().Replace('_', ' ');
        string Label(DateTime utc, TimeSpan value) => new DateTimeOffset(utc).ToOffset(value).ToString("zzz");
        return time + $"\n{city} · UTC{Label(item.StartUtc.Value, offset)}" +
            (offset != endOffset ? $" → UTC{Label(item.EndUtc!.Value, endOffset)}" : "");
    }
}
