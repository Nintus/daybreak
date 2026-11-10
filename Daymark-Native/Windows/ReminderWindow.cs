using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class ReminderWindow : Window
{
    private const int PageSize = 30;
    private readonly StackPanel events = new();
    private readonly TextBlock summary = new() { Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock error = new() { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap };
    private readonly Action<ReminderAlert, bool> record;

    public ReminderWindow(bool testCalendar, Action<ReminderAlert, bool> record)
    {
        this.record = record;
        Title = testCalendar ? "Daymark reminders · Test calendar" : "Daymark reminders";
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = NativeTheme.Surface;
        Foreground = NativeTheme.Ink;
        FontSize = 14;
        var work = SystemParameters.WorkArea;
        Width = Math.Min(440, work.Width);
        Height = Math.Min(430, work.Height);
        Left = Math.Max(work.Left, work.Right - Width - 16);
        Top = Math.Max(work.Top, work.Bottom - Height - 16);
        WindowStartupLocation = WindowStartupLocation.Manual;
        NativeTheme.Attach(this);

        var root = new DockPanel { Margin = new Thickness(18) };
        Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = testCalendar ? "Reminders · Test calendar" : "Reminders", FontSize = 22, FontWeight = FontWeights.SemiBold });
        header.Children.Add(summary);
        header.Children.Add(error);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = events, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
    }

    public void Render(IReadOnlyList<ReminderAlert> due, string? historyError)
    {
        summary.Text = due.Count switch
        {
            0 => "No reminders are due.",
            1 => "1 reminder due",
            > PageSize => $"Showing the first {PageSize} of {due.Count:N0} reminders",
            _ => $"{due.Count:N0} reminders due"
        };
        error.Text = historyError ?? "";
        events.Children.Clear();
        foreach (var alert in due.Take(PageSize))
        {
            var item = alert.Event;
            var row = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
            row.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock
            {
                Text = item.AllDay ? $"{item.Start:ddd, MMM d} · All day" : $"{item.Start:ddd, MMM d, t}",
                Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6)
            });
            var actions = new WrapPanel();
            var dismiss = new Button { Content = "Dismiss", Margin = new Thickness(0, 0, 8, 0) };
            dismiss.Click += (_, _) => record(alert, false);
            System.Windows.Automation.AutomationProperties.SetName(dismiss, $"Dismiss {item.Title} reminder");
            var snooze = new Button { Content = "Snooze 5 min", IsEnabled = alert.ExpiresUtc > DateTime.UtcNow.AddMinutes(5) };
            snooze.Click += (_, _) => record(alert, true);
            System.Windows.Automation.AutomationProperties.SetName(snooze, $"Snooze {item.Title} reminder for five minutes");
            actions.Children.Add(dismiss);
            actions.Children.Add(snooze);
            row.Children.Add(actions);
            events.Children.Add(row);
        }
    }

    public void SetError(string message) => error.Text = message;
}
