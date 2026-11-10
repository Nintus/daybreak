using System.IO;
using System.Windows;
using Daymark.Core;
using Forms = System.Windows.Forms;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private const string TraySetting = "keep-in-tray.txt";
    private ReminderController? reminders;
    private Forms.NotifyIcon? tray;
    private Forms.ContextMenuStrip? trayMenu;
    private System.Drawing.Icon? trayImage;
    private bool quitRequested;
    private string TraySettingPath => Path.Combine(Path.GetDirectoryName(store.Path)!, TraySetting);

    private void InitializeReminders(bool testCalendar)
    {
        Loaded += (_, _) =>
        {
            if (reminders is not null) return;
            reminders = new ReminderController(Path.GetDirectoryName(store.Path)!, Dispatcher, testCalendar);
            reminders.Update(document);
            if (File.Exists(TraySettingPath))
            {
                try { CreateTrayIcon(); }
                catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
                {
                    status.Text = "Daymark could not start in the notification area. Closing the window will quit.";
                }
            }
        };
        Closing += (_, args) =>
        {
            if (quitRequested || tray is null || Dispatcher.HasShutdownStarted) return;
            args.Cancel = true;
            Hide();
        };
        Closed += (_, _) =>
        {
            reminders?.Dispose();
            RemoveTrayIcon();
        };
    }

    private void ToggleTray(bool keepRunning)
    {
        try
        {
            if (keepRunning)
            {
                CreateTrayIcon();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(TraySettingPath)!);
                    using var setting = File.Open(TraySettingPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                }
                catch
                {
                    RemoveTrayIcon();
                    throw;
                }
            }
            else
            {
                File.Delete(TraySettingPath);
                RemoveTrayIcon();
            }
            status.Text = keepRunning ? "Daymark will keep running in the notification area when you close this window." : "Closing the window will quit Daymark.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            MessageBox.Show(this, "Daymark could not change its notification area setting.\n\n" + error.Message,
                "Reminders", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateTrayIcon()
    {
        if (tray is not null) return;
        try
        {
            var resource = Application.GetResourceStream(new Uri("/Daymark;component/Assets/Daymark.ico", UriKind.Relative));
            using var stream = resource?.Stream;
            using var icon = stream is null ? null : new System.Drawing.Icon(stream);
            trayImage = (System.Drawing.Icon)(icon ?? System.Drawing.SystemIcons.Application).Clone();
            trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add("Open Daymark", null, (_, _) => Dispatcher.InvokeAsync(ShowCalendarWindow));
            trayMenu.Items.Add("Quick Add…", null, (_, _) => Dispatcher.InvokeAsync(() =>
            {
                ShowCalendarWindow();
                if (IsEnabled) QuickAdd();
            }));
            trayMenu.Items.Add("Reminders…", null, (_, _) => Dispatcher.InvokeAsync(() => reminders?.Open()));
            trayMenu.Items.Add("Quit Daymark", null, (_, _) => Dispatcher.InvokeAsync(QuitDaymark));
            tray = new Forms.NotifyIcon
            {
                Icon = trayImage,
                Text = Title,
                ContextMenuStrip = trayMenu
            };
            tray.DoubleClick += (_, _) => Dispatcher.InvokeAsync(ShowCalendarWindow);
            tray.Visible = true;
        }
        catch
        {
            RemoveTrayIcon();
            throw;
        }
    }

    private void RemoveTrayIcon()
    {
        tray?.Dispose();
        tray = null;
        trayMenu?.Dispose();
        trayMenu = null;
        trayImage?.Dispose();
        trayImage = null;
    }

    private void ShowCalendarWindow()
    {
        if (quitRequested) return;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void QuitDaymark()
    {
        if (quitRequested) return;
        quitRequested = true;
        Close();
    }
}
