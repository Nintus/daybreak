using System.Text.Json;

namespace Daymark.Core;

public sealed class ReminderStateStore(string path)
{
    public const string FileName = "reminder-state.json";
    private const int MaximumEntries = 10_000;
    private sealed record Entry(ReminderKey Key, DateTime? SnoozedUntilUtc);

    public Dictionary<ReminderKey, DateTime?> Load()
    {
        if (!File.Exists(path)) return [];
        using var stream = File.OpenRead(path);
        if (stream.Length > 2_000_000) throw new InvalidDataException("Reminder history is too large.");
        var entries = JsonSerializer.Deserialize<List<Entry?>>(stream)
            ?? throw new InvalidDataException("Reminder history is empty.");
        if (entries.Count > MaximumEntries) throw new InvalidDataException("Reminder history contains too many entries.");
        var result = new Dictionary<ReminderKey, DateTime?>();
        foreach (var entry in entries)
        {
            if (entry is null || entry.Key.EventOrSeriesId == Guid.Empty || entry.Key.TriggerUtc.Kind != DateTimeKind.Utc ||
                entry.Key.OriginalStart is { Kind: not DateTimeKind.Unspecified } ||
                entry.SnoozedUntilUtc is { Kind: not DateTimeKind.Utc } ||
                !result.TryAdd(entry.Key, entry.SnoozedUntilUtc))
                throw new InvalidDataException("Reminder history contains an invalid entry.");
        }
        return result;
    }

    public Dictionary<ReminderKey, DateTime?> Record(IReadOnlyDictionary<ReminderKey, DateTime?> current,
        ReminderKey key, DateTime? snoozedUntilUtc, DateTime nowUtc)
    {
        var oldest = nowUtc.AddDays(-2);
        var next = current.Where(entry => (entry.Value ?? entry.Key.TriggerUtc) >= oldest).ToDictionary();
        next[key] = snoozedUntilUtc;
        if (next.Count > MaximumEntries) throw new InvalidDataException("Reminder history is full. Older entries are cleared automatically.");
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".daymark-reminders-{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                JsonSerializer.Serialize(stream, next.Select(entry => new Entry(entry.Key, entry.Value)).ToArray());
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
        return next;
    }
}
