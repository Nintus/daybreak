using System.Windows;
using System.Windows.Controls;
using Daymark.Core;
using Daymark.Providers;
using Daymark.Migration;

namespace Daymark.Windows;

internal sealed class AccountsDialog : Window
{
    private readonly AccountService service;
    private readonly StackPanel rows = new();
    private readonly StackPanel buttons = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 12, 0, 8) };
    private readonly Button cancel = new() { Content = "Cancel operation", Margin = new Thickness(3),
        Padding = new Thickness(9, 7, 9, 7), Visibility = Visibility.Collapsed };
    private readonly Dictionary<Guid, Dictionary<string, bool>> pendingSelections = [];
    private CancellationTokenSource? operation;
    public AccountsDialog(AccountService service)
    {
        this.service = service;
        Title = "Calendar accounts"; Width = 680; Height = Math.Min(700, SystemParameters.WorkArea.Height - 32); MinWidth = 540; MinHeight = 400;
        Background = NativeTheme.Background; Foreground = NativeTheme.Ink; WindowStartupLocation = WindowStartupLocation.CenterOwner; NativeTheme.Attach(this);
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var heading = new StackPanel(); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        heading.Children.Add(new TextBlock { Text = "Calendar accounts", FontSize = 24, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "Google and Microsoft · read-only sync", Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 10) });
        heading.Children.Add(new TextBlock { Text = "Choose calendars and select Refresh selected to save your choices and download events for offline use. Refresh is manual and covers the past 3 months and next 15 months.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 0, 0, 12) });
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(message);
        footer.Children.Add(buttons);
        Add(buttons, "Account setup…", Setup);
        Add(buttons, "Connect Google", () => Connect(CalendarProviderKind.Google));
        Add(buttons, "Connect Microsoft", () => Connect(CalendarProviderKind.Microsoft));
        cancel.Click += (_, _) => CancelOperation();
        footer.Children.Add(cancel);
        root.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Closing += (_, e) =>
        {
            if (operation is null) return;
            e.Cancel = true;
            CancelOperation();
        };
        Loaded += (_, _) => Run("Loading accounts…", "", (_, _) => Task.CompletedTask);
    }
    private static void Add(Panel panel, string title, Action action)
    {
        var button = new Button { Content = title, Margin = new Thickness(3), Padding = new Thickness(9, 7, 9, 7) }; button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private void Connect(CalendarProviderKind provider, AccountDraft? draft = null) =>
        Run("Complete sign-in and consent in your browser.", "Account connected. Choose calendars to download.",
            (ct, _) => service.ConnectAsync(provider, ct, draft), canCancel: true);
    private void CancelOperation()
    {
        if (operation is null || cancel.Visibility != Visibility.Visible || !cancel.IsEnabled) return;
        cancel.IsEnabled = false;
        message.Text = "Cancelling…";
        operation.Cancel();
    }
    private async void Run(string starting, string success, Func<CancellationToken, IProgress<AccountRefreshProgress>, Task> action,
        bool canCancel = false, Guid? savedAccount = null)
    {
        if (operation is not null) return;
        using var current = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        operation = current;
        buttons.IsEnabled = rows.IsEnabled = false;
        cancel.Visibility = canCancel ? Visibility.Visible : Visibility.Collapsed;
        cancel.IsEnabled = canCancel;
        message.Text = starting;
        var acceptingProgress = true;
        var progress = new Progress<AccountRefreshProgress>(update =>
        {
            if (!acceptingProgress || !ReferenceEquals(operation, current) || (current.IsCancellationRequested && update.CanCancel)) return;
            message.Text = update.Message;
            cancel.IsEnabled = update.CanCancel;
        });
        string result;
        try
        {
            await Task.Run(() => action(current.Token, progress));
            if (savedAccount is { } id) pendingSelections.Remove(id);
            result = success;
        }
        catch (Exception error) { result = AccountService.ErrorMessage(error); }
        acceptingProgress = false;
        cancel.IsEnabled = false;
        try
        {
            var accounts = await Task.Run(service.Overview);
            Render(accounts);
            message.Text = result;
        }
        catch (Exception)
        {
            message.Text = (result + " Account details could not be loaded. Close and reopen Calendar accounts to retry.").Trim();
        }
        finally
        {
            operation = null;
            cancel.Visibility = Visibility.Collapsed;
            buttons.IsEnabled = rows.IsEnabled = true;
        }
    }
    internal void Render(AccountOverview overview)
    {
        rows.Children.Clear();
        foreach (var draft in overview.Imported)
        {
            var section = new StackPanel { Margin = new Thickness(12) };
            rows.Children.Add(new Border { Child = section, Background = NativeTheme.Surface, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 12) });
            section.Children.Add(new TextBlock { Text = draft.Name, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            section.Children.Add(new TextBlock { Text = $"{draft.Login} · {draft.Provider}\nImported from {draft.Source} · Sign-in required", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 6, 0, 8) });
            var actions = new WrapPanel(); section.Children.Add(actions);
            if (draft.Provider is "Google" or "Unknown") Add(actions, "Sign in with Google", () => Connect(CalendarProviderKind.Google, draft));
            if (draft.Provider is "Microsoft" or "Unknown") Add(actions, "Sign in with Microsoft", () => Connect(CalendarProviderKind.Microsoft, draft));
            if (draft.Provider is "iCloud" or "CalDAV") section.Children.Add(new TextBlock { Text = "Profile saved. Connecting iCloud and other CalDAV accounts is not available yet.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted });
            Add(actions, "Remove profile", () => Run("Removing profile…", "Imported profile removed.",
                (_, _) => { service.ImportedAccounts.Remove(draft.Id); return Task.CompletedTask; }));
        }
        if (overview.Connected.Count == 0) rows.Children.Add(new TextBlock { Text = "No accounts connected. Open Account setup to configure your OAuth applications, then connect a provider.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 14) });
        foreach (var (account, calendars, coverage, lastIssue, retryUtc) in overview.Connected)
        {
            var section = new StackPanel { Margin = new Thickness(12) };
            rows.Children.Add(new Border { Child = section, Background = NativeTheme.Surface, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 12) });
            section.Children.Add(new TextBlock { Text = account.Label, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            section.Children.Add(new TextBlock { Text = account.Provider == CalendarProviderKind.Google ? "Google" : "Microsoft",
                Foreground = NativeTheme.Muted, Margin = new Thickness(0, 3, 0, 5) });
            if (lastIssue is not null)
            {
                var retry = retryUtc is { } utc ? $"\nRetry after {utc.ToLocalTime():g}." : "";
                var retained = coverage.Count > 0 ? "\nPreviously downloaded events remain available." : "";
                section.Children.Add(new TextBlock { Text = $"Refresh did not finish\n{lastIssue}{retry}{retained}",
                    Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 7) });
            }
            if (!pendingSelections.TryGetValue(account.Id, out var pending))
                pendingSelections[account.Id] = pending = new(StringComparer.Ordinal);
            var saved = account.Selection.ToHashSet(StringComparer.Ordinal);
            if (calendars.Count == 0)
                section.Children.Add(new TextBlock { Text = "The provider returned no calendars.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted });
            var toggles = new List<(string Id, CheckBox Toggle)>();
            foreach (var calendar in calendars)
            {
                var detail = coverage.TryGetValue(calendar.Id, out var cached) ? CoverageText(cached) : "No offline download";
                var toggle = new CheckBox { Content = new TextBlock { Text = calendar.Name, TextWrapping = TextWrapping.Wrap },
                    IsChecked = pending.GetValueOrDefault(calendar.Id, saved.Contains(calendar.Id)),
                    Margin = new Thickness(0, 8, 0, 0), ToolTip = "Download this calendar for read-only viewing" };
                System.Windows.Automation.AutomationProperties.SetName(toggle, calendar.Name);
                System.Windows.Automation.AutomationProperties.SetHelpText(toggle, detail);
                toggle.Click += (_, _) =>
                {
                    if (toggle.IsChecked == saved.Contains(calendar.Id)) pending.Remove(calendar.Id);
                    else pending[calendar.Id] = toggle.IsChecked == true;
                };
                section.Children.Add(toggle);
                section.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted,
                    Margin = new Thickness(22, 3, 0, 4) });
                toggles.Add((calendar.Id, toggle));
            }
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) }; section.Children.Add(actions);
            Add(actions, "Refresh selected", () =>
            {
                var selected = toggles.Where(t => t.Toggle.IsChecked == true).Select(t => t.Id).ToArray();
                var success = selected.Length == 0 ? $"Offline calendar selection cleared for {account.Label}."
                    : $"{(selected.Length == 1 ? "1 calendar" : $"{selected.Length} calendars")} refreshed for {account.Label}.";
                Run($"Checking calendars for {account.Label}…", success,
                    (ct, progress) => service.RefreshAsync(account, selected, ct, progress), canCancel: true, savedAccount: account.Id);
            });
            Add(actions, "Disconnect…", () =>
            {
                if (MessageBox.Show(this, "Remove this connection, its downloaded events and saved credentials from this PC? Provider events will remain on the server.", "Disconnect account", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Run("Disconnecting…", "Account disconnected.", (_, _) => service.ForgetAsync(account), savedAccount: account.Id);
            });
        }
    }
    private static string CoverageText(CachedCalendarCoverage coverage)
    {
        var count = coverage.EventCount == 1 ? "1 event" : $"{coverage.EventCount:N0} events";
        return $"{count} cached · Refreshed {coverage.RetrievedUtc.ToLocalTime():g}\n" +
            $"Coverage {coverage.StartUtc:d} – {coverage.EndUtc.AddTicks(-1):d} UTC";
    }
    private void Setup()
    {
        try { new AccountSetupDialog(service.Authorization) { Owner = this }.ShowDialog(); }
        catch (Exception error) { message.Text = AccountService.ErrorMessage(error); }
    }
}

internal sealed class AccountSetupDialog : Window
{
    public AccountSetupDialog(AccountAuthorization authorization)
    {
        Title = "Account setup"; Width = 570; Height = 530; MinWidth = 500; MinHeight = 430; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Background; Foreground = NativeTheme.Ink; NativeTheme.Attach(this);
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var form = new StackPanel(); root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        form.Children.Add(new TextBlock { Text = "Connect your own OAuth applications", FontSize = 21, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
        form.Children.Add(new TextBlock { Text = "Follow docs/ACCOUNT_SETUP.md in the download. Google needs a Desktop app registration with Calendar API enabled. Microsoft needs a public desktop application with http://localhost redirect. Sign-in opens your system browser; Daymark never asks for an account password.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 12, 0, 12) });
        var settings = authorization.Applications;
        TextBox Field(string name, string value)
        {
            form.Children.Add(new TextBlock { Text = name, Margin = new Thickness(0, 10, 0, 6) });
            var field = new TextBox { Text = value }; System.Windows.Automation.AutomationProperties.SetName(field, name); form.Children.Add(field); return field;
        }
        var google = Field("Google desktop client ID", settings.GoogleClientId);
        form.Children.Add(new TextBlock { Text = "Google desktop client secret (from downloaded client JSON)", Margin = new Thickness(0, 10, 0, 6), TextWrapping = TextWrapping.Wrap });
        var secret = new PasswordBox { Password = settings.GoogleClientSecret, Padding = new Thickness(8) }; form.Children.Add(secret); System.Windows.Automation.AutomationProperties.SetName(secret, "Google desktop client secret");
        var microsoft = Field("Microsoft application (client) ID", settings.MicrosoftClientId);
        var error = new TextBlock { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) }; form.Children.Add(error);
        var save = new Button { Content = "Save setup", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(4) }; actions.Children.Add(save);
        save.Click += (_, _) => { try { authorization.Configure(new(google.Text.Trim(), secret.Password.Trim(), microsoft.Text.Trim())); DialogResult = true; } catch (Exception e) { error.Text = AccountService.ErrorMessage(e); } };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(4) }; actions.Children.Add(cancel);
    }
}
