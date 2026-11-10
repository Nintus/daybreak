using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Daymark.Core;

namespace Daymark.Migration;

// Import only schedules that the native model can faithfully represent. A series is
// accepted or rejected as a whole, including its exclusions and moved occurrences.
public static class IcalendarImporter
{
    private sealed record Property(string Name, Dictionary<string, string> Parameters, string Value);
    private sealed record Stamp(DateTime Wall, bool AllDay, string? Zone, DateTime? Utc);
    private static InvalidDataException Unsupported(string message) => new(message);
    public static CalendarImport Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 20_000_000) throw Unsupported("Choose a calendar file under 20 MB.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return Parse(reader.ReadToEnd(), Path.GetFileNameWithoutExtension(path));
    }
    public static CalendarImport Parse(string text, string fallbackName)
    {
        if (text.Length > 20_000_000) throw Unsupported("Calendar file exceeds 20 MB.");
        var components = new Stack<string>(); var calendar = new List<Property>(); var rows = new List<List<Property>>();
        List<Property>? row = null; var alarms = false; var otherComponents = false; var roots = 0;
        foreach (var line in Unfold(text.TrimStart('\uFEFF')))
        {
            if (line.Length == 0) continue;
            var p = ReadProperty(line);
            if (p.Name == "BEGIN")
            {
                var kind = p.Value.ToUpperInvariant();
                if (components.Count == 0 && kind != "VCALENDAR") throw Unsupported("Expected an iCalendar VCALENDAR file.");
                if (kind == "VCALENDAR" && (components.Count != 0 || ++roots != 1)) throw Unsupported("Choose a file containing one calendar.");
                if (kind == "VEVENT" && (components.Count != 1 || components.Peek() != "VCALENDAR")) throw Unsupported("Invalid event nesting.");
                if (kind == "VEVENT") row = [];
                if (kind == "VALARM") alarms = true;
                if (kind is "VTODO" or "VJOURNAL" or "VFREEBUSY") otherComponents = true;
                components.Push(kind); if (components.Count > 10) throw Unsupported("Calendar nesting exceeds the import limit."); continue;
            }
            if (p.Name == "END")
            {
                if (components.Count == 0 || components.Pop() != p.Value.ToUpperInvariant()) throw Unsupported("Unbalanced calendar components.");
                if (p.Value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase))
                { rows.Add(row!); row = null; if (rows.Count > 10000) throw Unsupported("Import at most 10,000 event records per file."); }
                continue;
            }
            if (components.Count == 0) throw Unsupported("Content outside the calendar.");
            if (components.Peek() == "VEVENT") row!.Add(p);
            else if (components.Count == 1) calendar.Add(p);
        }
        if (roots != 1 || components.Count != 0) throw Unsupported("Incomplete iCalendar file.");
        if (One(calendar, "VERSION")?.Value != "2.0") throw Unsupported("Only iCalendar version 2.0 is supported.");
        if (One(calendar, "METHOD") is { Value: var method } && !method.Equals("PUBLISH", StringComparison.OrdinalIgnoreCase)) throw Unsupported("Import a calendar export, rather than a meeting request/cancellation message.");
        var name = Unescape(One(calendar, "X-WR-CALNAME")?.Value ?? fallbackName).Trim();
        if (name.Length == 0) name = "Imported calendar";
        // Calendar name + UID keeps a repeated export stable across paths and file renames.
        var definition = new CalendarDefinition("import-" + ImportIdentity.Guid("calendar:" + name).ToString("N"), name[..Math.Min(name.Length, 80)], "Teal");
        var warnings = new List<string>(); var events = new List<CalendarEvent>();
        if (alarms) warnings.Add("Reminders from the calendar file were skipped. You can set reminders in Daymark after importing.");
        if (otherComponents) warnings.Add("Tasks, journals and free/busy components are not imported.");
        if (text.Contains("BEGIN:VTIMEZONE", StringComparison.OrdinalIgnoreCase)) warnings.Add("Time zones use the computer's installed rules. Custom VTIMEZONE definitions are not converted; unknown zone IDs are skipped.");
        if (rows.Any(r => r.Any(p => p.Name is "ATTENDEE" or "ORGANIZER" or "ATTACH" or "TRANSP" or "STATUS" or "CLASS" or "CATEGORIES" or "URL")))
            warnings.Add("Imported events are local copies. Invitations, attendees, attachments, links, categories, privacy and free/busy settings are not transferred.");
        if (rows.Any(r => One(r, "UID") is null)) warnings.Add("Events without UID were skipped because they cannot be safely identified on reimport.");
        foreach (var group in rows.Where(r => One(r, "UID") is not null).GroupBy(r => One(r, "UID")!.Value, StringComparer.Ordinal))
        {
            var label = Unescape(group.First().FirstOrDefault(p => p.Name == "SUMMARY")?.Value ?? "Untitled event"); label = label[..Math.Min(label.Length, 120)];
            try
            {
                if (group.Key.Length is 0 or > 1000) throw Unsupported("invalid event UID");
                var list = Group(group.ToList(), definition, group.Key);
                new CalendarDocument(3, list, [definition]).Validate();
                if (events.Count + list.Count > 10000) throw Unsupported("the file exceeds 10,000 imported records including occurrence changes");
                events.AddRange(list);
            }
            catch (Exception e) when (e is InvalidDataException or FormatException or OverflowException or ArgumentOutOfRangeException)
            { warnings.Add($"Skipped “{label}”: {e.Message}"); }
        }
        return new(definition, events, warnings.Distinct().ToList());
    }
    public static CalendarDocument Merge(CalendarDocument current, IEnumerable<CalendarImport> selected, out int added)
    {
        var events = current.Events.ToList(); var calendars = current.CalendarList.ToList();
        var ids = events.Select(e => e.Id).ToHashSet(); var existingSeries = events.Where(e => e.SeriesId is null).Select(e => e.Id).ToHashSet(); added = 0;
        foreach (var import in selected)
        {
            // Preserve local edits and exceptions as a unit when the same series is imported again.
            var fresh = import.Events.Where(e => !existingSeries.Contains(e.SeriesId ?? e.Id) && ids.Add(e.Id)).ToList();
            if (fresh.Count == 0) continue;
            if (!calendars.Any(c => c.Id == import.Calendar.Id)) calendars.Add(import.Calendar);
            events.AddRange(fresh); added += fresh.Count;
            foreach (var e in fresh.Where(e => e.SeriesId is null)) existingSeries.Add(e.Id);
        }
        var next = new CalendarDocument(3, events, calendars); next.Validate(); return next;
    }
    private static List<CalendarEvent> Group(List<List<Property>> rows, CalendarDefinition calendar, string uid)
    {
        if (rows.Any(r => r.Any(p => p.Name is "RDATE" or "EXRULE" || p.Name == "RECURRENCE-ID" && p.Parameters.ContainsKey("RANGE")))) throw Unsupported("additional recurrence dates or THISANDFUTURE changes need a more complete recurrence engine");
        var masters = rows.Where(r => One(r, "RECURRENCE-ID") is null).ToList();
        if (masters.Count != 1) throw Unsupported("missing or duplicate series master (export the latest calendar state)");
        var properties = masters[0];
        if (One(properties, "STATUS")?.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true) return [];
        var id = ImportIdentity.Guid(calendar.Id + ":" + uid); var master = Event(properties, id, calendar.Id);
        if (One(properties, "RRULE") is { } rule) master = master with { Recurrence = Rule(rule.Value, master) };
        var changes = rows.Where(r => One(r, "RECURRENCE-ID") is not null).ToList();
        var excludes = properties.Where(p => p.Name == "EXDATE").ToList();
        if (master.Recurrence is null && (changes.Count > 0 || excludes.Count > 0)) throw Unsupported("recurrence changes have no supported repeat rule");
        var result = new List<CalendarEvent> { master }; var overrides = new Dictionary<DateTime, CalendarEvent>();
        CalendarEvent Occurrence(Stamp stamp)
        {
            if (stamp.AllDay != master.AllDay || (stamp.Zone is null) != (master.TimeZoneId is null)) throw Unsupported("recurrence dates do not match the series time type");
            var wall = stamp.Utc is { } utc ? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, CalendarTime.FindZone(master.TimeZoneId!)), DateTimeKind.Unspecified) : stamp.Wall;
            var day = DateOnly.FromDateTime(wall);
            if (day == DateOnly.MaxValue) throw Unsupported("recurrence date exceeds the supported range");
            var zone = master.TimeZoneId is null ? TimeZoneInfo.Utc : CalendarTime.FindZone(master.TimeZoneId);
            var occurrence = Recurrence.InRange(new(3, [master]), day, 1, zone).Events.SingleOrDefault(e => e.OriginalStart == wall) ?? throw Unsupported("exception date does not occur in this series");
            return occurrence with { Id = ImportIdentity.Guid(calendar.Id + ":" + uid + ":" + wall.ToString("O", CultureInfo.InvariantCulture)) };
        }
        foreach (var p in excludes)
            foreach (var value in p.Value.Split(',')) { if (overrides.Count >= 1000) throw Unsupported("the series exceeds 1,000 occurrence changes"); var occurrence = Occurrence(Date(p with { Value = value })); overrides[occurrence.OriginalStart!.Value] = occurrence with { IsCancelled = true }; }
        var changed = new HashSet<DateTime>();
        foreach (var r in changes)
        {
            if (overrides.Count >= 1000) throw Unsupported("the series exceeds 1,000 occurrence changes");
            if (One(r, "RRULE") is not null) throw Unsupported("an individual occurrence has its own repeat rule");
            var occurrence = Occurrence(Date(One(r, "RECURRENCE-ID")!));
            if (!changed.Add(occurrence.OriginalStart!.Value)) throw Unsupported("duplicate occurrence revisions");
            var cancelled = One(r, "STATUS")?.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true;
            var replacement = cancelled ? occurrence with { IsCancelled = true } : Event(r, occurrence.Id, calendar.Id, master) with { SeriesId = master.Id, OriginalStart = occurrence.OriginalStart };
            overrides[occurrence.OriginalStart.Value] = replacement;
        }
        result.AddRange(overrides.Values); return result;
    }
    private static CalendarEvent Event(List<Property> row, Guid id, string calendar, CalendarEvent? defaults = null)
    {
        var start = Date(One(row, "DTSTART") ?? throw Unsupported("missing DTSTART"));
        var endProperty = One(row, "DTEND"); var duration = One(row, "DURATION");
        if (endProperty is not null && duration is not null) throw Unsupported("DTEND and DURATION cannot both be present");
        Stamp end;
        if (endProperty is not null) end = Date(endProperty);
        else
        {
            var durationText = duration?.Value;
            if (durationText is not null && Regex.IsMatch(durationText, @"^P[0-9]+W$")) durationText = "P" + checked(int.Parse(durationText[1..^1], CultureInfo.InvariantCulture) * 7) + "D";
            var span = durationText is not null ? System.Xml.XmlConvert.ToTimeSpan(durationText) : start.AllDay ? TimeSpan.FromDays(1) : defaults is not null ? (defaults.EndUtc - defaults.StartUtc ?? defaults.End - defaults.Start) : TimeSpan.Zero;
            if (span <= TimeSpan.Zero || start.AllDay && span.Ticks % TimeSpan.TicksPerDay != 0) throw Unsupported("zero-duration events or fractional all-day durations cannot be represented");
            // Zoned DURATION containing calendar days can differ at DST boundaries: do not flatten it.
            if (start.Zone is not null && duration is not null && Regex.IsMatch(duration.Value, @"P[0-9]+[WD]")) throw Unsupported("zoned durations expressed in days/weeks need calendar-duration support");
            if (start.Utc is { } utc)
            {
                var endUtc = utc.Add(span); var wall = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(endUtc, CalendarTime.FindZone(start.Zone!)), DateTimeKind.Unspecified);
                end = new(wall, false, start.Zone, endUtc);
            }
            else end = start with { Wall = start.Wall.Add(span) };
        }
        if (start.AllDay != end.AllDay || (start.Zone is null) != (end.Zone is null)) throw Unsupported("start and end use incompatible date/time types");
        if (start.Zone is not null) end = end with { Wall = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(end.Utc!.Value, CalendarTime.FindZone(start.Zone)), DateTimeKind.Unspecified) };
        string Field(string name, string fallback) => One(row, name) is { } p ? Unescape(p.Value) : fallback;
        var item = new CalendarEvent(id, Field("SUMMARY", defaults?.Title ?? "Untitled event"), start.Wall, end.Wall, start.AllDay,
            Field("LOCATION", defaults?.Location ?? ""), Field("DESCRIPTION", defaults?.Notes ?? ""),
            start.Zone, start.Utc, end.Utc, calendar);
        item.Validate(); return item;
    }
    private static RecurrenceRule Rule(string text, CalendarEvent master)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var piece in text.ToUpperInvariant().Split(';'))
        {
            var pair = piece.Split('=', 2); if (pair.Length != 2 || !values.TryAdd(pair[0], pair[1])) throw Unsupported("invalid RRULE");
        }
        string? Value(string name) => values.GetValueOrDefault(name);
        if (values.Keys.Any(k => k is not ("FREQ" or "INTERVAL" or "COUNT" or "UNTIL" or "BYDAY" or "BYMONTHDAY" or "BYMONTH" or "BYSETPOS" or "WKST"))) throw Unsupported("repeat rule uses unsupported constraints");
        var frequency = Value("FREQ") switch { "DAILY" => RecurrenceFrequency.Daily, "WEEKLY" => RecurrenceFrequency.Weekly, "MONTHLY" => RecurrenceFrequency.Monthly, "YEARLY" => RecurrenceFrequency.Yearly, _ => throw Unsupported("unsupported repeat frequency") };
        var interval = Value("INTERVAL") is { } i ? int.Parse(i, CultureInfo.InvariantCulture) : 1;
        var weekdays = 0; var monthly = MonthlyPattern.DayOfMonth;
        var codes = new[] { "SU", "MO", "TU", "WE", "TH", "FR", "SA" }; var byday = Value("BYDAY");
        if (Value("WKST") is { } wk && wk != "MO" && (frequency != RecurrenceFrequency.Weekly || interval > 1)) throw Unsupported("repeat rule uses a different week anchor");
        if (frequency == RecurrenceFrequency.Weekly && byday is not null)
        {
            foreach (var code in byday.Split(',')) { var day = Array.IndexOf(codes, code); if (day < 0) throw Unsupported("unsupported weekly weekday"); weekdays |= 1 << day; }
        }
        else if (frequency == RecurrenceFrequency.Monthly && byday is not null)
        {
            var day = codes[(int)master.Start.DayOfWeek]; var ordinal = (master.Start.Day - 1) / 7 + 1;
            if (byday == ordinal + day && Value("BYSETPOS") is null || byday == day && Value("BYSETPOS") == ordinal.ToString(CultureInfo.InvariantCulture)) monthly = MonthlyPattern.OrdinalWeekday;
            else if (byday == "-1" + day && Value("BYSETPOS") is null || byday == day && Value("BYSETPOS") == "-1") monthly = MonthlyPattern.LastWeekday;
            else throw Unsupported("monthly weekday pattern cannot be represented");
        }
        else if (byday is not null) throw Unsupported("weekday constraint cannot be represented for this frequency");
        if (Value("BYSETPOS") is not null && (frequency != RecurrenceFrequency.Monthly || byday is null)) throw Unsupported("unsupported BYSETPOS");
        if (Value("BYMONTHDAY") is { } monthday && (frequency is not (RecurrenceFrequency.Monthly or RecurrenceFrequency.Yearly) || monthly != MonthlyPattern.DayOfMonth || monthday != master.Start.Day.ToString(CultureInfo.InvariantCulture))) throw Unsupported("repeat day-of-month differs from its start date");
        if (Value("BYMONTH") is { } month && (frequency != RecurrenceFrequency.Yearly || month != master.Start.Month.ToString(CultureInfo.InvariantCulture))) throw Unsupported("repeat month cannot be represented");
        int? count = Value("COUNT") is { } c ? int.Parse(c, CultureInfo.InvariantCulture) : null; DateOnly? until = null;
        if (Value("UNTIL") is { } u)
        {
            var stamp = Date(new("UNTIL", u.Length == 8 ? new() { ["VALUE"] = "DATE" } : new(), u));
            if (master.AllDay != stamp.AllDay || !master.AllDay && (master.TimeZoneId is null) != (stamp.Zone is null)) throw Unsupported("UNTIL does not match the series time type");
            var wall = stamp.Utc is { } utc ? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, CalendarTime.FindZone(master.TimeZoneId!)), DateTimeKind.Unspecified) : stamp.Wall;
            until = DateOnly.FromDateTime(wall);
            if (!master.AllDay && wall.TimeOfDay < master.Start.TimeOfDay) until = until.Value.AddDays(-1);
        }
        var result = new RecurrenceRule(frequency, interval, weekdays, monthly, count, until); result.Validate(master.Start); return result;
    }
    private static Stamp Date(Property property)
    {
        if (property.Parameters.Keys.Any(k => k is not ("VALUE" or "TZID"))) throw Unsupported("unsupported date parameter");
        var date = property.Parameters.GetValueOrDefault("VALUE") == "DATE";
        if (property.Parameters.GetValueOrDefault("VALUE") is { } type && type is not ("DATE" or "DATE-TIME")) throw Unsupported("unsupported date value type");
        var utc = property.Value.EndsWith('Z'); var zone = property.Parameters.GetValueOrDefault("TZID");
        if ((date || utc) && zone is not null) throw Unsupported("TZID is invalid on a date or UTC timestamp");
        if (!DateTime.TryParseExact(property.Value, date ? "yyyyMMdd" : utc ? "yyyyMMdd'T'HHmmss'Z'" : "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)) throw Unsupported("invalid calendar date/time");
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        if (date) return new(wall, true, null, null);
        if (utc) return new(wall, false, "Etc/UTC", DateTime.SpecifyKind(wall, DateTimeKind.Utc));
        if (zone is null) return new(wall, false, null, null);
        var resolved = CalendarTime.FindZone(zone); return new(wall, false, CalendarTime.ZoneId(resolved), CalendarTime.Resolve(wall, resolved));
    }
    private static Property? One(List<Property> properties, string name)
    {
        var matches = properties.Where(p => p.Name == name).Take(2).ToList();
        if (matches.Count > 1) throw Unsupported("duplicate " + name + " property"); return matches.FirstOrDefault();
    }
    private static IEnumerable<string> Unfold(string text)
    {
        string? prior = null;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith(' ') || line.StartsWith('\t'))
            { if (prior is null) throw Unsupported("invalid folded line"); prior += line[1..]; if (prior.Length > 100000) throw Unsupported("calendar line exceeds 100 KB"); }
            else { if (prior is not null) yield return prior; prior = line; if (line.Length > 100000) throw Unsupported("calendar line exceeds 100 KB"); }
        }
        if (prior is not null) yield return prior;
    }
    private static Property ReadProperty(string line)
    {
        var quoted = false; var colon = -1;
        for (var i = 0; i < line.Length; i++) { if (line[i] == '"') quoted = !quoted; if (line[i] == ':' && !quoted) { colon = i; break; } }
        if (colon <= 0) throw Unsupported("invalid calendar content line");
        var parts = new List<string>(); var from = 0; quoted = false;
        for (var i = 0; i < colon; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            if (line[i] == ';' && !quoted) { parts.Add(line[from..i]); from = i + 1; }
        }
        parts.Add(line[from..colon]); var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in parts.Skip(1))
        {
            var pair = part.Split('=', 2); if (pair.Length != 2 || !parameters.TryAdd(pair[0].ToUpperInvariant(), pair[1].Trim('"'))) throw Unsupported("invalid calendar parameter");
        }
        return new(parts[0].ToUpperInvariant(), parameters, line[(colon + 1)..]);
    }
    private static string Unescape(string text) => Regex.Replace(text, @"\\([nN,;\\])", m => m.Groups[1].Value is "n" or "N" ? "\n" : m.Groups[1].Value);
}
