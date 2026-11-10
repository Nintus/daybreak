using System.IO;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private void ImportFromApps()
    {
        new ImportDialog(Path.GetDirectoryName(store.Path)!, () => document,
            next => Save(next, "Calendar import")) { Owner = this }.ShowDialog();
        Refresh();
    }

    private void ExportCalendar()
    {
        var dialog = new CalendarExportDialog(document, selected, displayZone) { Owner = this };
        if (dialog.ShowDialog() == true)
            status.Text = $"Exported {dialog.ExportedCount:N0} {(dialog.ExportedCount == 1 ? "event" : "events")} to an iCalendar file";
    }
}
