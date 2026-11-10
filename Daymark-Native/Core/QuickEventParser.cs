using System.Globalization;
using System.Text.RegularExpressions;

namespace Daymark.Core;

public static class QuickEventParser
{
    private const string Weekday = "monday|tuesday|wednesday|thursday|friday|saturday|sunday";
    private const string Clock = @"(?:noon|midnight|[0-9]{1,2}(?::[0-9]{2})?\s*(?:am|pm)|[0-9]{1,2}:[0-9]{2})";
    private const string Duration = @"[0-9]{1,4}\s*(?:minutes?|mins?|m|hours?|hrs?|h|days?|d)";
    private static readonly Regex Phrase = new(
        $@"\A(?<title>.+?)\s+(?<date>today|tomorrow|(?:next\s+)?(?:{Weekday})|[0-9]{{4}}-[0-9]{{2}}-[0-9]{{2}})" +
        $@"(?:\s+(?:all\s+day|(?:at\s+)?(?<start>{Clock})(?:\s+(?:to\s+(?<end>{Clock})|for\s+(?<duration>{Duration})))?))?" +
        $@"(?:\s+every\s+(?<repeat>weekday|day|week|month|year))?(?:\s+remind\s+(?<reminder>at\s+start|{Duration}\s+before))?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static CalendarEvent Parse(string input, DateOnly today, int defaultDurationMinutes, string calendarId)
    {
        if (input.Length > 600 || input.Contains('\n') || input.Contains('\r'))
            throw new InvalidDataException("Enter a single phrase of up to 600 characters.");
        var match = Phrase.Match(input.Trim());
        if (!match.Success)
            throw new InvalidDataException("Start with a title and date, for example Lunch tomorrow at noon for 45m.");

        string Group(string name) => match.Groups[name].Value.ToLowerInvariant();
        var date = ReadDate(Group("date"), today);
        var clock = Group("start");
        var allDay = clock.Length == 0;
        var from = date.ToDateTime(allDay ? TimeOnly.MinValue : ReadClock(clock));
        DateTime to;
        if (allDay) to = from.AddDays(1);
        else
        {
            var end = Group("end");
            if (end.Length > 0)
            {
                to = date.ToDateTime(ReadClock(end));
                if (to < from) to = to.AddDays(1);
            }
            else
            {
                var duration = Group("duration");
                var minutes = duration.Length == 0 ? defaultDurationMinutes : ReadMinutes(duration);
                if (minutes is < 1 or > 1440)
                    throw new InvalidDataException("Choose an event duration between one minute and 24 hours.");
                to = from.AddMinutes(minutes);
            }
        }

        var repeat = Group("repeat") switch
        {
            "day" => new RecurrenceRule(RecurrenceFrequency.Daily),
            "weekday" => new RecurrenceRule(RecurrenceFrequency.Weekly, Weekdays: 0b0111110),
            "week" => new RecurrenceRule(RecurrenceFrequency.Weekly),
            "month" => new RecurrenceRule(RecurrenceFrequency.Monthly),
            "year" => new RecurrenceRule(RecurrenceFrequency.Yearly),
            _ => null
        };
        var reminder = Group("reminder");
        int? lead = reminder.Length == 0 ? null : reminder.StartsWith("at", StringComparison.Ordinal) ? 0 : ReadMinutes(reminder[..^"before".Length].TrimEnd());
        if (to.Year > 9998) throw new InvalidDataException("Choose an event end date between years 2 and 9998.");
        var draft = new CalendarEvent(Guid.NewGuid(), match.Groups["title"].Value.Trim(), from, to,
            AllDay: allDay, CalendarId: calendarId, Recurrence: repeat, ReminderMinutes: lead);
        draft.Validate();
        return draft;
    }

    private static DateOnly ReadDate(string input, DateOnly today)
    {
        if (today.Year is < 2 or > 9998) throw new InvalidDataException("Choose a date between years 2 and 9998.");
        var date = input switch
        {
            "today" => today,
            "tomorrow" => today.AddDays(1),
            _ => ReadAbsoluteOrWeekday(input, today)
        };
        if (date.Year is < 2 or > 9998) throw new InvalidDataException("Choose a date between years 2 and 9998.");
        return date;
    }

    private static DateOnly ReadAbsoluteOrWeekday(string input, DateOnly today)
    {
        var nextWeek = input.StartsWith("next ", StringComparison.Ordinal) || input.StartsWith("next\t", StringComparison.Ordinal);
        var name = nextWeek ? input[4..].TrimStart() : input;
        if (Enum.TryParse<DayOfWeek>(name, ignoreCase: true, out var weekday))
        {
            if (!nextWeek) return today.AddDays(((int)weekday - (int)today.DayOfWeek + 7) % 7);
            var monday = WeekLayout.StartOfWeek(today).AddDays(7);
            return monday.AddDays(((int)weekday + 6) % 7);
        }
        if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        throw new InvalidDataException("Choose a valid date such as 2026-12-15.");
    }

    private static TimeOnly ReadClock(string input)
    {
        if (input == "noon") return new TimeOnly(12, 0);
        if (input == "midnight") return TimeOnly.MinValue;
        if (TimeOnly.TryParseExact(input, ["H:mm", "h tt", "htt", "h:mm tt", "h:mmtt"],
            CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out var clock)) return clock;
        throw new InvalidDataException("Choose a valid time such as 9am or 14:30.");
    }

    private static int ReadMinutes(string input)
    {
        var index = 0;
        while (index < input.Length && char.IsAsciiDigit(input[index])) index++;
        var count = int.Parse(input.AsSpan(0, index), CultureInfo.InvariantCulture);
        return input[index..].TrimStart()[0] switch { 'd' => count * 1440, 'h' => count * 60, _ => count };
    }
}
