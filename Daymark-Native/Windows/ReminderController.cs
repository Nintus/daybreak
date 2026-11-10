using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Daymark.Core;
using Microsoft.Win32;

namespace Daymark.Windows;

internal sealed class ReminderController : IDisposable
{
    private readonly ReminderStateStore store;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timer;
    private readonly bool testCalendar;
    private readonly HashSet<ReminderKey> presented = [];
    private Dictionary<ReminderKey, DateTime?> dispositions = [];
    private CalendarDocument document = CalendarDocument.Empty();
    private ReminderWindow? window;
    private bool openedByUser;
    private bool disposed;
    private bool updateQueued;
    private string? historyError;

    public ReminderController(string directory, Dispatcher dispatcher, bool testCalendar)
    {
        this.dispatcher = dispatcher;
        this.testCalendar = testCalendar;
        store = new ReminderStateStore(Path.Combine(directory, ReminderStateStore.FileName));
        try { dispositions = store.Load(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            historyError = "Reminder history could not be read. Recent reminders may appear again.";
        }

        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        timer.Tick += (_, _) => Replan();
        SystemEvents.TimeChanged += TimeChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
    }

    public void Update(CalendarDocument next)
    {
        document = next;
        if (updateQueued || disposed || dispatcher.HasShutdownStarted) return;
        updateQueued = true;
        dispatcher.InvokeAsync(() =>
        {
            updateQueued = false;
            Replan();
        }, DispatcherPriority.Background);
    }

    public void Open()
    {
        openedByUser = true;
        ShowWindow();
        Replan();
        window?.Activate();
    }

    private void Replan()
    {
        timer.Stop();
        if (disposed) return;
        var now = DateTime.UtcNow;
        ReminderPlan plan;
        try { plan = ReminderPlanner.Plan(document, now, TimeZoneInfo.Local, dispositions); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException)
        {
            window?.SetError($"Daymark could not schedule reminders. {error.Message}");
            return;
        }

        presented.IntersectWith(plan.Due.Select(item => item.Key));
        var newAlert = false;
        foreach (var alert in plan.Due)
            if (presented.Add(alert.Key)) newAlert = true;
        if (newAlert) ShowWindow();
        if (plan.Due.Count == 0 && !openedByUser) window?.Close();
        else window?.Render(plan.Due, historyError);

        if (plan.NextCheckUtc is not { } next) return;
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(250, (next - DateTime.UtcNow).TotalMilliseconds));
        timer.Start();
    }

    private void ShowWindow()
    {
        if (window is not null) return;
        window = new ReminderWindow(testCalendar, Record);
        window.Closed += (_, _) =>
        {
            window = null;
            openedByUser = false;
        };
        window.Show();
    }

    private void Record(ReminderAlert alert, bool snooze)
    {
        var now = DateTime.UtcNow;
        DateTime? next = snooze ? now.AddMinutes(5) : null;
        if (alert.ExpiresUtc <= (next ?? now))
        {
            Replan();
            if (snooze) window?.SetError("Fewer than five minutes remain before this event ends.");
            return;
        }
        try
        {
            dispositions = store.Record(dispositions, alert.Key, next, now);
            historyError = null;
            Replan();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            window?.SetError("Daymark could not save the reminder change. Try again.");
        }
    }

    private void TimeChanged(object? sender, EventArgs args) => ClockChanged();

    private void PowerChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Resume) ClockChanged();
    }

    private void ClockChanged()
    {
        if (dispatcher.HasShutdownStarted) return;
        dispatcher.InvokeAsync(() =>
        {
            if (disposed) return;
            TimeZoneInfo.ClearCachedData();
            Replan();
        }, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        SystemEvents.TimeChanged -= TimeChanged;
        SystemEvents.PowerModeChanged -= PowerChanged;
        window?.Close();
    }
}
