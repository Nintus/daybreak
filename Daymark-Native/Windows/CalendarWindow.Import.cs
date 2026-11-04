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
}
