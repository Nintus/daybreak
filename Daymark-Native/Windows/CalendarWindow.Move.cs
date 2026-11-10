using System.IO;
using System.Windows;
using Daymark.Core;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private void MoveSelectedEvent()
    {
        if (agenda.SelectedItem is not CalendarEvent item || IsProviderEvent(item)) return;
        var dialog = new EventMoveDialog(item, displayZone) { Owner = this };
        if (dialog.ShowDialog() == true) MoveEvent(item, dialog.DestinationDate, dialog.DestinationTime);
    }

    private void MoveEvent(CalendarEvent item, DateOnly date, TimeOnly time)
    {
        if (IsProviderEvent(item)) return;
        try
        {
            var target = date.ToDateTime(item.AllDay ? TimeOnly.MinValue : time);
            if (target == item.Start) return;
            RepeatedTimeChoice? choice = null;
            if (item.TimeZoneId is not null && displayZone.IsAmbiguousTime(target))
            {
                var answer = MessageBox.Show(this, $"{target:g} occurs twice in this time zone. Choose Yes for the earlier time or No for the later time.",
                    "Choose which time to use", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer is not (MessageBoxResult.Yes or MessageBoxResult.No)) return;
                choice = answer == MessageBoxResult.Yes ? RepeatedTimeChoice.Earlier : RepeatedTimeChoice.Later;
            }
            var next = CalendarChanges.Move(document, item, date, time, displayZone, choice);
            if (ReferenceEquals(next, document) || !Save(next, item.SeriesId is null ? "move event" : "move occurrence")) return;
            selected = date;
            month = new(date.Year, date.Month, 1);
            search.Clear();
            Refresh();
            status.Text = $"Moved to {target:g} · Ctrl+Z to undo";
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(this, error.Message, "Could not move event", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
