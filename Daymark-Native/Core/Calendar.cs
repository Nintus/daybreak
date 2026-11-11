using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daymark.Core;

public sealed record CalendarEvent(Guid Id, string Title, DateTime Start, DateTime End,
    bool AllDay = false, string Location = "", string Notes = "", string? TimeZoneId = null,
    DateTime? StartUtc = null, DateTime? EndUtc = null, string CalendarId = "personal",
    RecurrenceRule? Recurrence = null, Guid? SeriesId = null, DateTime? OriginalStart = null, bool IsCancelled = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ReminderMinutes = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CalendarId) || CalendarId.Length > 200) throw new InvalidDataException("Choose a valid calendar.");
        if (SeriesId is not null && (SeriesId == Guid.Empty || OriginalStart is not { Kind: DateTimeKind.Unspecified } || Recurrence is not null) ||
            SeriesId is null && (OriginalStart is not null || IsCancelled)) throw new InvalidDataException("Invalid recurring occurrence identity.");
        Recurrence?.Validate(Start);
        if (ReminderMinutes is < 0 or > ReminderPlanner.MaximumLeadMinutes)
            throw new InvalidDataException("Choose a reminder between the event start and one week before it.");
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Title) || Title.Length > 300)
            throw new InvalidDataException("Give the event a title of at most 300 characters.");
        if (TimeZoneId is null)
        {
            if (StartUtc is not null || EndUtc is not null) throw new InvalidDataException("UTC instants require an event time zone.");
            if (End <= Start) throw new InvalidDataException("The end must be after the start.");
        }
        else
        {
            if (AllDay || string.IsNullOrWhiteSpace(TimeZoneId) || TimeZoneId.Length > 200)
                throw new InvalidDataException("Choose a valid time zone for a timed event.");
            CalendarTime.FindZone(TimeZoneId);
            if (StartUtc is not { Kind: DateTimeKind.Utc } || EndUtc is not { Kind: DateTimeKind.Utc } || EndUtc <= StartUtc)
                throw new InvalidDataException("Timed events require UTC instants with the end after the start.");
        }
        if (Start.Kind != DateTimeKind.Unspecified || End.Kind != DateTimeKind.Unspecified)
            throw new InvalidDataException("Event clock times must not contain a UTC suffix or offset.");
        if (AllDay && (Start.TimeOfDay != TimeSpan.Zero || End.TimeOfDay != TimeSpan.Zero))
            throw new InvalidDataException("All-day events must begin and end at midnight.");
        if (Location is null || Notes is null || Location.Length > 1000 || Notes.Length > 10000)
            throw new InvalidDataException("Event location or notes are invalid or too long.");
    }
}

public sealed record CalendarDefinition(string Id, string Name, string Color = "Blue")
{
    public static CalendarDefinition Personal => new("personal", "My calendar");
    public static IReadOnlyList<string> Colors { get; } = Array.AsReadOnly(new[] { "Blue", "Teal", "Violet", "Orange", "Rose" });
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 200 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || !Colors.Contains(Color))
            throw new InvalidDataException("Calendars need a name of at most 80 characters and a supported color.");
    }
}

public sealed record CalendarDocument(int Version, List<CalendarEvent> Events, List<CalendarDefinition>? Calendars = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CalendarTask>? Tasks = null)
{
    public static CalendarDocument Empty() => new(3, [], [CalendarDefinition.Personal]);
    [JsonIgnore] public IReadOnlyList<CalendarDefinition> CalendarList => Calendars ?? [CalendarDefinition.Personal];
    [JsonIgnore] public IReadOnlyList<CalendarTask> TaskList => Tasks ?? [];
    public CalendarDocument Upgrade() => this with
    {
        // Older previews must reject a file whose data they would discard on save.
        Version = TaskList.Count > 0 ? 5 : Events.Any(item => item.ReminderMinutes.HasValue) ? 4 : 3,
        Calendars = [.. CalendarList],
        Tasks = TaskList.Count == 0 ? null : [.. TaskList]
    };
    public CalendarDocument InZone(TimeZoneInfo zone) => this with { Events = Events.Select(e => CalendarTime.InZone(e, zone)).ToList() };
    public void Validate()
    {
        if (Version is < 1 or > 5 || Events is null || Events.Count > 100000)
            throw new InvalidDataException("This is not a supported Daymark native backup.");
        if (CalendarList.Count is < 1 or > 100) throw new InvalidDataException("Keep between 1 and 100 calendars.");
        var calendarIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var calendar in CalendarList)
        {
            if (calendar is null) throw new InvalidDataException("Invalid calendar.");
            calendar.Validate(); if (!calendarIds.Add(calendar.Id)) throw new InvalidDataException("Duplicate calendar ID.");
        }
        if (Version < 3 && Calendars is not null) throw new InvalidDataException("Calendars require backup version 3.");
        var ids = new HashSet<Guid>();
        foreach (var item in Events)
        {
            if (item is null) throw new InvalidDataException("Invalid event in backup.");
            item.Validate();
            if (Version == 1 && item.TimeZoneId is not null) throw new InvalidDataException("Time zones require backup version 2.");
            if (Version < 3 && (item.Recurrence is not null || item.SeriesId is not null || item.CalendarId != "personal" || Calendars is not null))
                throw new InvalidDataException("Calendars and recurrence require backup version 3.");
            if (!calendarIds.Contains(item.CalendarId)) throw new InvalidDataException("An event refers to a missing calendar.");
            if (!ids.Add(item.Id)) throw new InvalidDataException("Duplicate event ID in backup.");
        }
        var masters = Events.Where(e => e.Recurrence is not null).ToDictionary(e => e.Id);
        var occurrenceKeys = new HashSet<(Guid, DateTime)>();
        foreach (var exception in Events.Where(e => e.SeriesId is not null))
        {
            if (!masters.TryGetValue(exception.SeriesId!.Value, out var master) || exception.CalendarId != master.CalendarId ||
                exception.OriginalStart < master.Start || !occurrenceKeys.Add((master.Id, exception.OriginalStart!.Value)) ||
                !Recurrence.IsScheduled(master, exception.OriginalStart.Value))
                throw new InvalidDataException("Invalid or duplicate recurring exception.");
        }

        if (TaskList.Count > 10_000) throw new InvalidDataException("Keep at most 10,000 tasks.");
        if (TaskList.Count == 0) return;
        var taskIds = new HashSet<Guid>();
        var linkedEvents = new HashSet<Guid>();
        var eligibleBlocks = Events.Where(item => !item.AllDay && item.SeriesId is null && item.Recurrence is null).Select(item => item.Id).ToHashSet();
        foreach (var task in TaskList)
        {
            if (task is null) throw new InvalidDataException("Invalid task in backup.");
            task.Validate();
            if (!taskIds.Add(task.Id)) throw new InvalidDataException("Duplicate task ID in backup.");
            if (task.ScheduledEventId is { } block && (!eligibleBlocks.Contains(block) || !linkedEvents.Add(block)))
                throw new InvalidDataException("Each task needs its own timed event for its time block. Unlink the task before changing the event to all-day or repeating.");
        }
    }
    public CalendarDocument WithEvents(IEnumerable<CalendarEvent> events) => this with { Version = 3, Events = events.ToList(), Calendars = [.. CalendarList] };
    public IEnumerable<CalendarEvent> On(DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        return Events.Where(e => e.Start < end && e.End > start)
            .OrderByDescending(e => e.AllDay).ThenBy(e => e.Start).ThenBy(e => e.Title);
    }

    // Scan the calendar once, then distribute overlapping events into the visible days.
    public IReadOnlyList<IReadOnlyList<CalendarEvent>> Days(DateOnly first, int count)
    {
        if (count < 1 || count > 366 || first.DayNumber > DateOnly.MaxValue.DayNumber - count)
            throw new ArgumentOutOfRangeException(nameof(count));
        var start = first.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(count);
        var days = Enumerable.Range(0, count).Select(_ => new List<CalendarEvent>()).ToArray();
        foreach (var item in Events.Where(e => e.Start < end && e.End > start)
                     .OrderByDescending(e => e.AllDay).ThenBy(e => e.Start).ThenBy(e => e.Title))
        {
            var from = Math.Max(0, DateOnly.FromDateTime(item.Start).DayNumber - first.DayNumber);
            var through = Math.Min(count - 1, DateOnly.FromDateTime(item.End.AddTicks(-1)).DayNumber - first.DayNumber);
            for (var i = from; i <= through; i++) days[i].Add(item);
        }
        return days;
    }
}

public sealed class CalendarStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Path { get; } = path;
    public CalendarDocument Load() => File.Exists(Path) ? Read(Path) : CalendarDocument.Empty();
    public static CalendarDocument Read(string path) => ReadDocument(path).Upgrade();
    private static CalendarDocument ReadDocument(string path)
    {
        if (new FileInfo(path).Length > 20_000_000) throw new InvalidDataException("Choose a backup under 20 MB.");
        var document = JsonSerializer.Deserialize<CalendarDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The calendar file is empty.");
        document.Validate();
        return document;
    }
    public void Save(CalendarDocument document)
    {
        document.Validate();
        document = document.Upgrade();
        var oldVersion = File.Exists(Path) ? ReadDocument(Path).Version : document.Version;
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(directory, $".daymark-{Guid.NewGuid():N}.tmp");
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            if (data.Length > 20_000_000) throw new InvalidDataException("The calendar exceeds the 20 MB backup limit; your existing file has not been changed.");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            if (oldVersion < document.Version && !File.Exists(Path + $".v{oldVersion}.bak")) PreserveLegacy(directory, oldVersion);
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void PreserveLegacy(string directory, int version)
    {
        var temporary = System.IO.Path.Combine(directory, $".daymark-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var original = File.OpenRead(Path))
            using (var backup = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { original.CopyTo(backup); backup.Flush(flushToDisk: true); }
            File.Move(temporary, Path + $".v{version}.bak");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Export(CalendarDocument document, string path)
    {
        document.Validate();
        new CalendarStore(path).Save(document);
    }
}
