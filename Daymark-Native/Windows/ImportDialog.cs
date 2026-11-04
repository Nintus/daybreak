using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;
using Daymark.Migration;
using Microsoft.Win32;

namespace Daymark.Windows;

internal sealed class ImportDialog : Window
{
    private readonly Func<CalendarDocument> current;
    private readonly Func<CalendarDocument, bool> save;
    private readonly AccountDraftStore accounts;
    private readonly StackPanel profiles = new(), files = new();
    private readonly TextBox details = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 90, MaxHeight = 170 };
    private readonly TextBlock state = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 8) };
    private readonly ComboBox theme = new() { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox duration = new() { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
    private readonly EventDefaults defaults;
    private readonly Button scan = new() { Content = "Scan installed apps", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3) };
    private readonly Button browse = new() { Content = "Add .ics files…", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3) };
    private readonly Button apply = new() { Content = "Import selected", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3) };
    private readonly List<(AccountDraft Draft, CheckBox Check)> choices = [];
    private readonly Dictionary<string, (CalendarImport Import, CheckBox Check)> calendars = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> notes = [];
    private readonly Func<DiscoveryReport> discover;
    private bool busy;
    public ImportDialog(string directory, Func<CalendarDocument> current, Func<CalendarDocument, bool> save, Func<DiscoveryReport>? discover = null)
    {
        this.current = current; this.save = save; accounts = new(Path.Combine(directory, "imported-accounts.json"));
        defaults = new(directory);
        this.discover = discover ?? (() => InstalledCalendarDiscovery.Scan(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        Title = "Import calendars and accounts"; Width = 760; Height = Math.Min(820, SystemParameters.WorkArea.Height - 32); MinWidth = 570; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = NativeTheme.Background; Foreground = NativeTheme.Ink; NativeTheme.Attach(this);
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "Make yourself at home", FontSize = 25, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "Bring accounts and calendars from Fantastical, Outlook and Thunderbird into Daymark.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8), Foreground = NativeTheme.Muted });
        header.Children.Add(new TextBlock { Text = "Review before importing. Accounts need fresh sign-in; passwords and tokens stay in their original apps. Calendar files become local copies. Original apps and files are left unchanged.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted });
        var tools = new WrapPanel { Margin = new Thickness(0, 12, 0, 8) }; tools.Children.Add(scan); tools.Children.Add(browse); header.Children.Add(tools);
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(state);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; actions.Children.Add(apply);
        var close = new Button { Content = "Close", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3) }; actions.Children.Add(close); close.Click += (_, _) => Close(); footer.Children.Add(actions);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        void Heading(string title) => body.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 15, 0, 8) });
        Heading("Accounts · sign-in required"); body.Children.Add(profiles);
        Heading("Calendar files"); body.Children.Add(files);
        body.Children.Add(new TextBlock { Text = "Repeated imports keep existing events and local edits. Importing a server export as well as connecting its account can show duplicate events; use exports for local calendars or select one source.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 0) });
        Heading("Compatible settings"); theme.Items.Add("Keep my current appearance"); theme.SelectedIndex = 0; body.Children.Add(theme);
        duration.Items.Add("Keep my default event duration"); duration.SelectedIndex = 0; body.Children.Add(duration);
        System.Windows.Automation.AutomationProperties.SetName(theme, "Import appearance");
        System.Windows.Automation.AutomationProperties.SetName(duration, "Import default event duration");
        Heading("Import details and limitations"); body.Children.Add(details);
        scan.Click += (_, _) => Run(Scan); browse.Click += (_, _) => Browse(); apply.Click += (_, _) => Run(Apply);
        Closing += (_, e) => { if (busy) { e.Cancel = true; state.Text = "Wait for the current import operation to finish before closing."; } };
        Loaded += (_, _) => Run(Scan);
        RefreshDetails();
    }
    private async void Run(Func<Task> action)
    {
        if (busy) return; busy = true; scan.IsEnabled = browse.IsEnabled = apply.IsEnabled = false; profiles.IsEnabled = files.IsEnabled = theme.IsEnabled = duration.IsEnabled = false;
        state.Text = "Working…";
        try { await action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or System.Text.DecoderFallbackException or ArgumentException)
        { state.Text = "Could not complete this operation. " + (e is InvalidDataException ? e.Message : "Check file access and choose a valid local .ics export. Earlier successful imports are retained."); }
        finally { busy = false; scan.IsEnabled = browse.IsEnabled = apply.IsEnabled = true; profiles.IsEnabled = files.IsEnabled = theme.IsEnabled = duration.IsEnabled = true; }
    }
    private async Task Scan()
    {
        var report = await Task.Run(discover);
        var saved = await Task.Run(accounts.Read); var selected = choices.Where(c => c.Check.IsChecked == true).Select(c => c.Draft.Id).ToHashSet();
        var hadChoices = choices.Count > 0; profiles.Children.Clear(); choices.Clear();
        foreach (var draft in report.Accounts)
        {
            var existing = saved.Any(a => a.Id == draft.Id);
            var check = new CheckBox { Content = new TextBlock { Text = $"{draft.Login} · {draft.Provider}\n{draft.Name} · {draft.Source}" + (existing ? " · already imported" : ""), TextWrapping = TextWrapping.Wrap }, IsChecked = !existing && (!hadChoices || selected.Contains(draft.Id)), IsEnabled = !existing, Margin = new Thickness(0, 5, 0, 9) };
            profiles.Children.Add(check); choices.Add((draft, check));
        }
        if (choices.Count == 0) profiles.Children.Add(new TextBlock { Text = "No supported account metadata found. You can still import .ics exports or connect an account from Calendar accounts.", TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted });
        theme.Items.Clear(); theme.Items.Add("Keep my current appearance"); theme.SelectedIndex = 0;
        duration.Items.Clear(); duration.Items.Add("Keep my default event duration"); duration.SelectedIndex = 0;
        foreach (var setting in report.Settings.Distinct())
        {
            if (setting.Theme is "Light" or "Dark") theme.Items.Add(new ComboBoxItem { Content = $"{setting.Theme} · from {setting.Source}", Tag = setting.Theme });
            if (setting.DefaultDurationMinutes is { } minutes) duration.Items.Add(new ComboBoxItem { Content = $"{minutes} minutes · from {setting.Source}", Tag = minutes });
        }
        notes.Clear(); notes.AddRange(report.Notes); await AddFiles(report.CalendarFiles); RefreshDetails();
        state.Text = $"Found {report.Accounts.Count} account profiles and {calendars.Count} readable calendar files. Choose what to import.";
    }
    private void Browse()
    {
        var picker = new OpenFileDialog { Title = "Import calendar files", Filter = "iCalendar files (*.ics)|*.ics", Multiselect = true, CheckFileExists = true };
        if (picker.ShowDialog(this) == true) Run(async () => { await AddFiles(picker.FileNames); state.Text = "Calendar preview ready. Review details, then choose Import selected."; });
    }
    private async Task AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).Take(100))
        {
            if (calendars.ContainsKey(path)) continue;
            if (calendars.Count >= 100) { notes.Add("At most 100 calendar files can be reviewed at once."); break; }
            try
            {
                var import = await Task.Run(() => IcalendarImporter.Read(path));
                var check = new CheckBox { Content = new TextBlock { Text = $"{import.Calendar.Name} · {import.Events.Count(e => e.SeriesId is null)} events/series, {import.Events.Count(e => e.SeriesId is not null)} occurrence changes" + (import.Warnings.Count > 0 ? $" · {import.Warnings.Count} notices" : "") + "\n" + path, TextWrapping = TextWrapping.Wrap }, IsChecked = import.Events.Count > 0, IsEnabled = import.Events.Count > 0, Margin = new Thickness(0, 5, 0, 9) };
                calendars.Add(path, (import, check)); files.Children.Add(check);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.DecoderFallbackException or FormatException)
            { notes.Add(Path.GetFileName(path) + ": could not import this file. " + (e is InvalidDataException ? e.Message : "Check access and export a valid UTF-8 iCalendar file.")); }
        }
        RefreshDetails();
    }
    private void RefreshDetails() => details.Text = string.Join("\n\n", notes.Concat(calendars.SelectMany(c => c.Value.Import.Warnings.Select(w => Path.GetFileName(c.Key) + ": " + w))).DefaultIfEmpty("Compatible event fields: title, start/end, all-day dates, location, notes, installed time zones and supported repeat rules. Unsupported schedules are skipped with a notice. Week-start, notification and proprietary app settings are not changed."));
    private async Task Apply()
    {
        var selected = calendars.Values.Where(c => c.Check.IsChecked == true).Select(c => c.Import).ToList();
        // IsEnabled inherits the busy panel state; it is not the selection state.
        var drafts = choices.Where(c => c.Check.IsChecked == true).Select(c => c.Draft).ToList();
        var appearance = (theme.SelectedItem as ComboBoxItem)?.Tag as string;
        var minutes = (duration.SelectedItem as ComboBoxItem)?.Tag as int?;
        var snapshot = current();
        var merged = await Task.Run(() => { var document = IcalendarImporter.Merge(snapshot, selected, out var added); return (Document: document, Added: added); });
        if (merged.Added == 0 && drafts.Count == 0 && appearance is null && minutes is null) { state.Text = "Nothing new selected. Existing events and profiles are kept."; return; }
        // Each category has its own durable save. Report completed categories if a later one fails.
        var completed = new List<string>();
        try
        {
            if (drafts.Count > 0)
            {
                await Task.Run(() => accounts.Merge(drafts)); completed.Add($"{drafts.Count} account profiles saved");
                foreach (var choice in choices.Where(c => drafts.Any(d => d.Id == c.Draft.Id))) { choice.Check.IsChecked = false; choice.Check.IsEnabled = false; }
            }
            if (merged.Added > 0)
            {
                if (!save(merged.Document)) { state.Text = string.Join("; ", completed.Append("calendar save failed; retry after checking the data folder")); return; }
                completed.Add($"{merged.Added} event/occurrence records imported (Undo available)");
            }
            if (appearance is "Light" or "Dark") { NativeTheme.Select(Enum.Parse<AppThemeMode>(appearance)); completed.Add("appearance applied"); theme.SelectedIndex = 0; }
            if (minutes is { } length) { defaults.Save(length); completed.Add("default duration applied"); duration.SelectedIndex = 0; }
            state.Text = string.Join("; ", completed) + ". Open File → Calendar accounts to sign in.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        { state.Text = string.Join("; ", completed.Append("remaining items could not be saved; check access and retry")) + ". Successful imports are retained and will not be duplicated."; }
    }
}
