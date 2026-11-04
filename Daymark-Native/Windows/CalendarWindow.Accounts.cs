using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;
using Daymark.Providers;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private AccountService? accountService;
    private IReadOnlyList<CachedCalendar> providerCalendars = [];
    private CalendarDocument? combinedLocal, combinedView;
    private CalendarDocument ViewDocument
    {
        get
        {
            if (combinedView is null || !ReferenceEquals(combinedLocal, document))
            {
                combinedLocal = document;
                combinedView = new(3, [.. document.Events, .. providerCalendars.SelectMany(c => c.Events)],
                    [.. document.CalendarList, .. providerCalendars.Select(c => c.Calendar)]);
            }
            return combinedView;
        }
    }
    private bool IsProviderEvent(CalendarEvent item) => providerCalendars.Any(c => c.Calendar.Id == item.CalendarId);
    private async Task LoadAccountsAsync()
    {
        try
        {
            var loaded = await Task.Run(() =>
            {
                var service = new AccountService(Path.GetDirectoryName(store.Path)!);
                return (Service: service, Calendars: service.Read());
            });
            if (!IsLoaded) return;
            accountService = loaded.Service;
            if (loaded.Calendars.Count == 0) return;
            providerCalendars = loaded.Calendars; combinedView = null; Refresh();
        }
        catch (Exception error) { status.Text = "Local calendar available · " + AccountService.ErrorMessage(error); }
    }
    private async void ManageAccounts()
    {
        if (accountService is null) { MessageBox.Show(this, "The account cache is not available yet. Your local calendar still works.", "Calendar accounts"); return; }
        new AccountsDialog(accountService) { Owner = this }.ShowDialog();
        try
        {
            var cached = await Task.Run(accountService.Read);
            if (!IsLoaded) return;
            providerCalendars = cached; combinedView = null; Refresh();
        }
        catch (Exception error) { status.Text = AccountService.ErrorMessage(error); }
    }
    private void ShowProviderEvent(CalendarEvent item)
    {
        var window = new Window { Title = "Connected event · read-only", Width = 570, Height = 520, MinWidth = 440, MinHeight = 350,
            Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = NativeTheme.Background, Foreground = NativeTheme.Ink };
        NativeTheme.Attach(window);
        var root = new DockPanel { Margin = new Thickness(22) }; window.Content = root;
        var close = new Button { Content = "Close", IsCancel = true, Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close); close.Click += (_, _) => window.Close();
        root.Children.Add(new TextBox { Text = $"{item.Title}\n\n{EventPresentation.Summary(item)}\n{CalendarPalette.Name(item.CalendarId)}\n\n{item.Location}\n\n{item.Notes}\n\nEdit this event in Google or Microsoft, then use Calendar accounts → Refresh selected. Two-way editing is not enabled yet.",
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0) });
        window.ShowDialog();
    }
    private string CacheStatus => providerCalendars.Count == 0 ? "" :
        $" · Connected calendars: read-only cache {providerCalendars.Max(c => c.StartUtc):MMM d, yyyy} – {providerCalendars.Min(c => c.EndUtc):MMM d, yyyy} · oldest refresh {providerCalendars.Min(c => c.RetrievedUtc).ToLocalTime():g}";
}
