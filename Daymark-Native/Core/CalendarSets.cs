using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daymark.Core;

public sealed record CalendarSet(Guid Id, string Name, string[] VisibleCalendarIds, string? DefaultCalendarId = null, bool ShowTasks = true);

public sealed record CalendarViewSettings(List<CalendarSet> Sets, string[] CustomHidden, Guid? ActiveSetId = null)
{
    public static CalendarViewSettings Empty => new([], []);
    [JsonIgnore] public CalendarSet? ActiveSet => Sets.FirstOrDefault(set => set.Id == ActiveSetId);

    public void Validate()
    {
        if (Sets is null || Sets.Count > 40 || CustomHidden is null || !ValidIds(CustomHidden))
            throw new InvalidDataException("Keep at most 40 sets and use valid calendar IDs.");
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in Sets)
        {
            if (set is null || set.Id == Guid.Empty || !ids.Add(set.Id) || string.IsNullOrWhiteSpace(set.Name) ||
                set.Name.Length > 64 || !names.Add(set.Name.Trim()) || set.VisibleCalendarIds is null ||
                set.VisibleCalendarIds.Length == 0 || !ValidIds(set.VisibleCalendarIds))
                throw new InvalidDataException("Give each set a unique name of up to 64 characters and include at least one calendar.");
            if (set.DefaultCalendarId is { } id && !set.VisibleCalendarIds.Contains(id, StringComparer.Ordinal))
                throw new InvalidDataException("Include the default calendar in this set.");
        }
        if (ActiveSetId is { } active && !ids.Contains(active))
            throw new InvalidDataException("The active calendar set does not exist.");
    }

    public HashSet<string> HiddenCalendars(IEnumerable<CalendarDefinition> available)
    {
        if (ActiveSet is not { } set) return CustomHidden.ToHashSet(StringComparer.Ordinal);
        var visible = set.VisibleCalendarIds.ToHashSet(StringComparer.Ordinal);
        return available.Select(calendar => calendar.Id).Where(id => !visible.Contains(id)).ToHashSet(StringComparer.Ordinal);
    }

    public string DefaultCalendar(IReadOnlyList<CalendarDefinition> local, ISet<string> hidden)
    {
        var preferred = ActiveSet?.DefaultCalendarId;
        if (preferred is not null && !hidden.Contains(preferred) && local.Any(calendar => calendar.Id == preferred)) return preferred;
        return local.FirstOrDefault(calendar => !hidden.Contains(calendar.Id))?.Id ?? local[0].Id;
    }

    private static bool ValidIds(string[] ids) => ids.Length <= 5000 &&
        ids.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 200) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length;
}

public sealed class CalendarViewSettingsStore(string directory)
{
    public const string FileName = "calendar-sets.json";
    public const string LegacyFileName = "hidden-calendars.json";
    private const int MaximumBytes = 2_000_000;
    private string PathFor(string name) => Path.Combine(directory, name);

    public CalendarViewSettings Load()
    {
        var path = PathFor(FileName);
        CalendarViewSettings settings;
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MaximumBytes) throw new InvalidDataException("Calendar set preferences are too large.");
            settings = JsonSerializer.Deserialize<CalendarViewSettings>(stream) ?? throw new InvalidDataException("Calendar set preferences are empty.");
        }
        else
        {
            var legacy = PathFor(LegacyFileName);
            if (!File.Exists(legacy)) return CalendarViewSettings.Empty;
            using var stream = File.OpenRead(legacy);
            if (stream.Length >= 50000) throw new InvalidDataException("Calendar visibility preferences are too large.");
            var hidden = JsonSerializer.Deserialize<string[]>(stream) ?? [];
            settings = new([], hidden.Distinct(StringComparer.Ordinal).ToArray());
        }
        settings.Validate();
        return settings;
    }

    public void Save(CalendarViewSettings settings)
    {
        settings.Validate();
        var data = JsonSerializer.SerializeToUtf8Bytes(settings);
        if (data.Length > MaximumBytes) throw new InvalidDataException("Calendar set preferences exceed 2 MB.");
        Directory.CreateDirectory(directory);
        var temporary = PathFor($".daymark-sets-{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, PathFor(FileName), overwrite: true);
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
