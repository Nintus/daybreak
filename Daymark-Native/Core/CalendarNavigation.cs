namespace Daymark.Core;

public enum CalendarViewMode { Month, Week, Day, Agenda }

public static class CalendarNavigation
{
    public static DateOnly Move(DateOnly selected, CalendarViewMode mode, int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        try
        {
            var next = mode switch
            {
                CalendarViewMode.Month => selected.AddMonths(direction),
                CalendarViewMode.Week => selected.AddDays(7 * direction),
                CalendarViewMode.Agenda => selected.AddDays(CalendarAgenda.Days * direction),
                _ => selected.AddDays(direction)
            };
            // Month grids require surrounding weeks, even when switching from Day.
            return next.Year is >= 2 and <= 9998 ? next : selected;
        }
        catch (ArgumentOutOfRangeException) { return selected; }
    }
}
