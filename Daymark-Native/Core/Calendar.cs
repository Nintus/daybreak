using System.Text.Json;

namespace Daymark.Core;

public sealed record CalendarEvent(Guid Id, string Title, DateTime Start, DateTime End,
    bool AllDay = false, string Location = "", string Notes = "")
{
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Title) || Title.Length > 300)
            throw new InvalidDataException("Give the event a title of at most 300 characters.");
        if (End <= Start) throw new InvalidDataException("The end must be after the start.");
        if (Start.Kind != DateTimeKind.Unspecified || End.Kind != DateTimeKind.Unspecified)
            throw new InvalidDataException("This preview supports local dates and times only.");
        if (AllDay && (Start.TimeOfDay != TimeSpan.Zero || End.TimeOfDay != TimeSpan.Zero))
            throw new InvalidDataException("All-day events must begin and end at midnight.");
        if (Location is null || Notes is null || Location.Length > 1000 || Notes.Length > 10000)
            throw new InvalidDataException("Event location or notes are invalid or too long.");
    }
}

public sealed record CalendarDocument(int Version, List<CalendarEvent> Events)
{
    public static CalendarDocument Empty() => new(1, []);
    public void Validate()
    {
        if (Version != 1 || Events is null || Events.Count > 100000)
            throw new InvalidDataException("This is not a supported Daymark native backup.");
        var ids = new HashSet<Guid>();
        foreach (var item in Events)
        {
            if (item is null) throw new InvalidDataException("Invalid event in backup.");
            item.Validate();
            if (!ids.Add(item.Id)) throw new InvalidDataException("Duplicate event ID in backup.");
        }
    }
    public IEnumerable<CalendarEvent> On(DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        return Events.Where(e => e.Start < end && e.End > start)
            .OrderByDescending(e => e.AllDay).ThenBy(e => e.Start).ThenBy(e => e.Title);
    }
}

public sealed class CalendarStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Path { get; } = path;
    public CalendarDocument Load() => File.Exists(Path) ? Read(Path) : CalendarDocument.Empty();
    public static CalendarDocument Read(string path)
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
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(directory, $".daymark-{Guid.NewGuid():N}.tmp");
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Export(CalendarDocument document, string path)
    {
        document.Validate();
        new CalendarStore(path).Save(document);
    }
}
