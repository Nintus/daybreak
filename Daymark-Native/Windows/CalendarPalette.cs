using System.Windows.Media;
using Daymark.Core;

namespace Daymark.Windows;

internal static class CalendarPalette
{
    private static IReadOnlyDictionary<string, CalendarDefinition> definitions = new Dictionary<string, CalendarDefinition>();
    public static void Set(IReadOnlyList<CalendarDefinition> calendars) => definitions = calendars.ToDictionary(c => c.Id);
    public static string Name(string id) => definitions.TryGetValue(id, out var calendar) ? calendar.Name : "My calendar";
    public static Brush Color(string id) => NativeTheme.CalendarColor(definitions.TryGetValue(id, out var calendar) ? calendar.Color : "Blue");
}
