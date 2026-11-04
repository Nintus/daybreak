using System.IO;

namespace Daymark.Windows;

internal sealed class EventDefaults(string directory)
{
    private readonly string path = Path.Combine(directory, "default-event-minutes.txt");
    public int Minutes
    {
        get
        {
            try { return File.Exists(path) && new FileInfo(path).Length <= 20 && int.TryParse(File.ReadAllText(path), out var minutes) && minutes is >= 1 and <= 1440 ? minutes : 60; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 60; }
        }
    }
    public void Save(int minutes)
    {
        if (minutes is < 1 or > 1440) throw new InvalidDataException("Default duration must be between 1 and 1,440 minutes.");
        Directory.CreateDirectory(directory); var temporary = path + ".tmp";
        try { File.WriteAllText(temporary, minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
