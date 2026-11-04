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
        heading.Children.Add(new TextBlock { Text = "Choose calendars, then Refresh selected. Events are downloaded for the past 3 months and next 15 months and remain available offline. Refresh is manual; Daymark does not poll in the background or upload edits.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 0, 0, 12) });
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(message);
        footer.Children.Add(buttons);
        Add(buttons, "Account setup…", Setup);
        Add(buttons, "Connect Google", () => Run(ct => service.ConnectAsync(CalendarProviderKind.Google, ct)));
        Add(buttons, "Connect Microsoft", () => Run(ct => service.ConnectAsync(CalendarProviderKind.Microsoft, ct)));
        var cancel = new Button { Content = "Cancel operation", Margin = new Thickness(3), Padding = new Thickness(9, 7, 9, 7) }; cancel.Click += (_, _) => operation?.Cancel(); footer.Children.Add(cancel);
        root.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Closing += (_, e) => { if (operation is not null) { operation.Cancel(); e.Cancel = true; message.Text = "Cancelling… wait for the current operation to stop, then close this window."; } };
        Loaded += (_, _) => Run(_ => Task.CompletedTask);
    }
    private static void Add(Panel panel, string title, Action action)
    {
        var button = new Button { Content = title, Margin = new Thickness(3), Padding = new Thickness(9, 7, 9, 7) }; button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private async void Run(Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        operation = new CancellationTokenSource(TimeSpan.FromMinutes(5)); buttons.IsEnabled = rows.IsEnabled = false;
        message.Text = "Working… If a browser opens, complete sign-in and consent there. You can cancel at any time.";
        try
        {
            await Task.Run(() => action(operation.Token));
            var accounts = await Task.Run(service.Accounts);
            Render(accounts); message.Text = "Ready. Connected events are read-only. Disconnect removes the local cache and saved tokens; it does not delete provider events or revoke consent on the provider website.";
        }
        catch (Exception error) { message.Text = AccountService.ErrorMessage(error); }
        finally { operation.Dispose(); operation = null; buttons.IsEnabled = rows.IsEnabled = true; }
    }
    private void Render(IReadOnlyList<(ConnectedAccount Account, List<RemoteCalendar> Calendars)> accounts)
    {
        rows.Children.Clear();
        foreach (var draft in service.ImportedAccounts.Read())
        {
            var section = new StackPanel { Margin = new Thickness(12) };
            rows.Children.Add(new Border { Child = section, Background = NativeTheme.Surface, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 12) });
            section.Children.Add(new TextBlock { Text = draft.Name, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            section.Children.Add(new TextBlock { Text = $"{draft.Login} · {draft.Provider}\nImported from {draft.Source} · Sign-in required", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 6, 0, 8) });
            var actions = new WrapPanel(); section.Children.Add(actions);
            if (draft.Provider is "Google" or "Unknown") Add(actions, "Sign in with Google", () => Run(ct => service.ConnectAsync(CalendarProviderKind.Google, ct, draft)));
            if (draft.Provider is "Microsoft" or "Unknown") Add(actions, "Sign in with Microsoft", () => Run(ct => service.ConnectAsync(CalendarProviderKind.Microsoft, ct, draft)));
            if (draft.Provider is "iCloud" or "CalDAV") section.Children.Add(new TextBlock { Text = "Profile saved. Connecting iCloud and other CalDAV accounts is not available yet.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted });
            Add(actions, "Remove profile", () => Run(_ => { service.ImportedAccounts.Remove(draft.Id); return Task.CompletedTask; }));
        }
        if (accounts.Count == 0) rows.Children.Add(new TextBlock { Text = "No accounts connected. Open Account setup to configure your OAuth applications, then connect a provider.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 14) });
        foreach (var (account, calendars) in accounts)
        {
            var section = new StackPanel { Margin = new Thickness(12) };
            rows.Children.Add(new Border { Child = section, Background = NativeTheme.Surface, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 12) });
            section.Children.Add(new TextBlock { Text = account.Label, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            var toggles = new List<(string Id, CheckBox Toggle)>();
            foreach (var calendar in calendars)
            {
                var toggle = new CheckBox { Content = calendar.Name, IsChecked = account.Selection.Contains(calendar.Id, StringComparer.Ordinal), Margin = new Thickness(0, 7, 0, 0), ToolTip = "Download this calendar for read-only viewing" };
                section.Children.Add(toggle); toggles.Add((calendar.Id, toggle));
            }
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) }; section.Children.Add(actions);
            Add(actions, "Refresh selected", () =>
            {
                var selected = toggles.Where(t => t.Toggle.IsChecked == true).Select(t => t.Id).ToArray();
                Run(ct => service.RefreshAsync(account, selected, ct));
            });
            Add(actions, "Disconnect…", () =>
            {
                if (MessageBox.Show(this, "Remove this connection, its downloaded events and saved credentials from this PC? Provider events will remain on the server.", "Disconnect account", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Run(_ => service.ForgetAsync(account));
            });
        }
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
