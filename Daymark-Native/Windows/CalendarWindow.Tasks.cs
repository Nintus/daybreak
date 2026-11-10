using System.IO;
using System.Windows;
using Daymark.Core;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private void OpenTasks()
    {
        var dialog = new TasksDialog(() => document, Save, DefaultCalendarId, selected, displayZone,
            new EventDefaults(Path.GetDirectoryName(store.Path)!).Minutes) { Owner = this };
        dialog.ShowDialog();
        Refresh();
    }

    private void EditAgendaTask(CalendarTask task)
    {
        var editor = new TaskEditor(task, CalendarTime.Today(displayZone)) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } updated) SaveAgendaTask(updated, "edit task");
    }

    private void SaveAgendaTask(CalendarTask task, string description)
    {
        try { if (Save(CalendarTasks.Save(document, task), description)) Refresh(); }
        catch (InvalidDataException exception) { MessageBox.Show(this, exception.Message, "Task change", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
