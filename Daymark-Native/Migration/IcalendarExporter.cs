using System.Globalization;
using System.Text;
using Daymark.Core;

namespace Daymark.Migration;

public static class IcalendarExporter
{
    public const int MaximumDays = 1096;
    private const int MaximumEvents = 10_000;
    private const int MaximumBytes = 20_000_000;

    public static int Save(CalendarDocument document, string calendarId, DateOnly first, DateOnly last,
        TimeZoneInfo displayZone, string path)
    {
        if (!Path.GetExtension(path).Equals(".ics", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a file with the .ics extension.");

        var days = last.DayNumber - first.DayNumber + 1;
        if (days is < 1 or > MaximumDays || last == DateOnly.MaxValue)
            throw new InvalidDataException($"Choose an export range of 1 to {MaximumDays:N0} days.");

        var calendar = document.CalendarList.SingleOrDefault(item => item.Id == calendarId)
            ?? throw new InvalidDataException("Choose a local calendar to export.");
        var selected = new CalendarDocument(3, document.Events.Where(item => item.CalendarId == calendarId).ToList(), [calendar]);
        var occurrences = Recurrence.InRange(selected, first, days, displayZone).Events;
        if (occurrences.Count == 0)
            throw new InvalidDataException("This calendar has no events in the selected date range.");
        if (occurrences.Count > MaximumEvents)
            throw new InvalidDataException($"Choose a shorter range to export up to {MaximumEvents:N0} events per file.");

        var destination = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".daymark-export-{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var writer = new StreamWriter(stream, new UTF8Encoding(false, true));
                WriteCalendar(writer, calendar, occurrences);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
        return occurrences.Count;
    }

    private static void WriteCalendar(TextWriter writer, CalendarDefinition calendar, List<CalendarEvent> occurrences)
    {
        var totalBytes = 0;
        void Line(string value)
        {
            totalBytes += WriteFoldedLine(writer, value);
            if (totalBytes > MaximumBytes)
                throw new InvalidDataException("Choose a shorter range to keep the export under 20 MB.");
        }

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Daymark//Calendar Export//EN");
        Line("X-WR-CALNAME:" + Escape(calendar.Name));
        var stamp = Format(DateTime.UtcNow, "yyyyMMdd'T'HHmmss'Z'");
        foreach (var item in occurrences.OrderBy(item => item.Start).ThenBy(item => item.Id))
        {
            var uid = item.SeriesId is { } series && item.OriginalStart is { } original
                ? FormattableString.Invariant($"{series:N}-{original.Ticks}@daymark")
                : $"{item.Id:N}@daymark";
            var start = item.StartUtc ?? item.Start;
            var end = item.EndUtc ?? item.End;
            var format = item.AllDay ? "yyyyMMdd" : item.StartUtc is null ? "yyyyMMdd'T'HHmmss" : "yyyyMMdd'T'HHmmss'Z'";
            var startValue = Format(start, format);
            var endValue = Format(end, format);
            if (startValue == endValue)
                throw new InvalidDataException($"“{item.Title}” starts and ends in the same second. Adjust its duration before exporting.");

            Line("BEGIN:VEVENT");
            Line("UID:" + uid);
            Line("DTSTAMP:" + stamp);
            var valueType = item.AllDay ? ";VALUE=DATE:" : ":";
            Line("DTSTART" + valueType + startValue);
            Line("DTEND" + valueType + endValue);
            Line("SUMMARY:" + Escape(item.Title));
            if (item.Location.Length > 0) Line("LOCATION:" + Escape(item.Location));
            if (item.Notes.Length > 0) Line("DESCRIPTION:" + Escape(item.Notes));
            Line("END:VEVENT");
        }
        Line("END:VCALENDAR");
    }

    private static string Format(DateTime date, string format) => date.ToString(format, CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        if (value.Any(c => c is < ' ' and not ('\t' or '\r' or '\n') or '\u007f'))
            throw new InvalidDataException("An exported name or event contains an unsupported control character.");
        return value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
            .Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n");
    }

    private static int WriteFoldedLine(TextWriter writer, string line)
    {
        var start = 0;
        var offset = 0;
        var lineBytes = 0;
        var totalBytes = 2;
        foreach (var rune in line.EnumerateRunes())
        {
            if (lineBytes + rune.Utf8SequenceLength > 75)
            {
                writer.Write(line.AsSpan(start, offset - start));
                writer.Write("\r\n ");
                start = offset;
                lineBytes = 1;
                totalBytes += 3;
            }
            lineBytes += rune.Utf8SequenceLength;
            totalBytes += rune.Utf8SequenceLength;
            offset += rune.Utf16SequenceLength;
        }
        writer.Write(line.AsSpan(start));
        writer.Write("\r\n");
        return totalBytes;
    }
}
