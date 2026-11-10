using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;
using Daymark.Windows;
using Daymark.Migration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var folder = Path.Combine(Path.GetTempPath(), "daymark-wpf-checks-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            NativeTheme.Apply(app, Path.Combine(folder, "appearance.txt")); NativeTheme.Select(AppThemeMode.Dark);
            var changes = 0; NativeTheme.Changed += () => changes++;
            var brush = NativeTheme.Ink;
            for (var i = 0; i < 100; i++) NativeTheme.Refresh();
            Check(changes == 0 && ReferenceEquals(brush, NativeTheme.Ink), "100 identical theme refreshes preserve palette resources without notifications");
            NativeTheme.Select(AppThemeMode.Light);
            Check(changes == 1 && ReferenceEquals(brush, NativeTheme.Ink), "a theme change updates the existing shared palette exactly once");
            foreach (var basis in Enum.GetValues<ThemeBase>())
            foreach (var accent in new[] { "#000000", "#FFFFFF", "#FFFF00", "#FF0000", "#00FF00", "#0000FF", "#7986FF", "#E9A1BE" })
            {
                var palette = ThemePalette.Build(basis, accent);
                if (ThemePalette.Contrast(palette["Accent"], palette["OnAccent"]) < 4.5 || ThemePalette.Contrast(palette["AccentInk"], palette["Soft"]) < 4.5 || ThemePalette.Contrast(palette["Muted"], palette["Surface"]) < 4.5 || ThemePalette.Contrast(palette["Muted"], palette["Background"]) < 4.5) throw new Exception("Unreadable generated palette");
                if (basis == ThemeBase.Oled && (palette["Background"] != System.Windows.Media.Colors.Black || palette["Surface"] != System.Windows.Media.Colors.Black)) throw new Exception("OLED surfaces must be pure black");
            }
            Check(true, "extreme accent colors retain readable generated text and pure black OLED surfaces");
            var preferencePath = Path.Combine(folder, "appearance.txt");
            var previousBytes = File.ReadAllText(preferencePath);
            var previousSettings = NativeTheme.Settings;
            NativeTheme.Preview(new(AppThemeMode.Custom, "#63C4A0", ThemeBase.Oled));
            Check(File.ReadAllText(preferencePath) == previousBytes, "theme previews never persist settings");
            NativeTheme.Preview(previousSettings);
            using (var themes = new WindowLifetime(new ThemeDialog())) { NativeTheme.Preview(new(AppThemeMode.Oled)); }
            Check(NativeTheme.Settings == previousSettings, "closing the theme studio without saving restores the original palette");
            NativeTheme.Save(new(AppThemeMode.Custom, "#E9A1BE", ThemeBase.Dark));
            var stored = System.Text.Json.JsonSerializer.Deserialize<ThemeSettings>(File.ReadAllText(preferencePath));
            Check(stored == NativeTheme.Settings, "custom accent and base persist together");
            NativeTheme.Select(AppThemeMode.Light);
            var store = new CalendarStore(Path.Combine(folder, "calendar.json"));
            var window = new CalendarWindow(store, CalendarDocument.Empty(), true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var grid = (Grid)typeof(CalendarWindow).GetField("days", flags)!.GetValue(window)!;
            var cell = grid.Children[7];
            var activate = typeof(Window).GetMethod("OnActivated", flags)!;
            for (var i = 0; i < 100; i++) activate.Invoke(window, [EventArgs.Empty]);
            Check(ReferenceEquals(cell, grid.Children[7]), "100 unchanged activations preserve the existing month visual tree");
            typeof(CalendarWindow).GetField("renderedToday", flags)!.SetValue(window, new DateOnly(2000, 1, 1));
            activate.Invoke(window, [EventArgs.Empty]);
            Check(!ReferenceEquals(cell, grid.Children[7]), "activation still refreshes after the calendar day changes");
            var selection = typeof(CalendarWindow).GetMethod("SelectDay", flags)!;
            var currentMonth = (DateOnly)typeof(CalendarWindow).GetField("month", flags)!.GetValue(window)!;
            var stableCell = grid.Children[7];
            var nextDay = grid.Children.OfType<MonthDayButton>().First(c => c.Date.Month == currentMonth.Month && c.Date.Day == 15).Date;
            selection.Invoke(window, [nextDay]);
            Check(ReferenceEquals(stableCell, grid.Children[7]) && (DateOnly)typeof(CalendarWindow).GetField("selected", flags)!.GetValue(window)! == nextDay,
                "selecting a day within the month reuses the existing visual tree");
            selection.Invoke(window, [currentMonth.AddMonths(1)]);
            Check(!ReferenceEquals(stableCell, grid.Children[7]), "selecting a date in another month rebuilds the visible range");
            foreach (var animations in new[] { false, true }) foreach (var highContrast in new[] { false, true }) foreach (var reduced in new[] { false, true })
                if (NativeMotion.Allowed(animations, highContrast, reduced) != (animations && !highContrast && !reduced)) throw new Exception("Motion policy failed");
            Check(true, "Windows animation, high contrast and app reduced-motion preferences gate transitions");
            var motionSurface = new Border { Opacity = .75 };
            var motionWindow = new Window { Content = motionSurface, Width = 160, Height = 120, ShowInTaskbar = false };
            motionWindow.Show(); NativeMotion.SetReduced(false);
            var enabled = NativeMotion.Enabled; NativeMotion.Reveal(motionSurface); NativeMotion.Reveal(motionSurface);
            Check(NativeMotion.ActiveCount == (enabled ? 1 : 0), "rapid transitions replace the previous animation without accumulating clocks");
            NativeMotion.SetReduced(true);
            Check(NativeMotion.ActiveCount == 0 && !motionSurface.HasAnimatedProperties && motionSurface.Opacity == .75 && File.ReadAllText(Path.Combine(folder, "motion.txt")) == "reduced",
                "reducing motion immediately removes clocks, preserves opacity and persists the preference");
            NativeMotion.Reveal(motionSurface);
            Check(NativeMotion.ActiveCount == 0 && !motionSurface.HasAnimatedProperties, "reduced motion does not schedule an animation");
            NativeMotion.SetReduced(false); NativeMotion.Reveal(motionSurface);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var deadline = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            deadline.Tick += (_, _) => { deadline.Stop(); frame.Continue = false; }; deadline.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Check(NativeMotion.ActiveCount == 0 && !motionSurface.HasAnimatedProperties && motionSurface.Opacity == .75,
                "completed transitions release clocks and return to the element's base opacity");
            NativeMotion.Reveal(motionSurface); motionWindow.Close();
            Check(NativeMotion.ActiveCount == 0, "closing an animating surface releases its motion state");
            Check(new System.Windows.Media.Typeface(HorizonBrand.Wordmark, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var brandFont) && brandFont.FontUri.ToString().Contains("manrope", StringComparison.OrdinalIgnoreCase), "bundled Horizon wordmark font resolves");
            Check(((System.Windows.Media.Imaging.BitmapFrame)HorizonBrand.Icon).PixelWidth > 0 && HorizonBrand.Logo(40) is Viewbox, "Horizon window icon and scalable mark load from app resources");
            var auth = new AccountAuthorization(folder);
            auth.Configure(new("fixture.apps.googleusercontent.com", "synthetic-secret", "11111111-1111-1111-1111-111111111111"));
            Check(new AccountAuthorization(folder).Applications.GoogleClientSecret == "synthetic-secret", "OAuth setup survives a protected-cache round trip");
            Check(!Directory.GetFiles(Path.Combine(folder, "credentials")).Any(p => System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(p)).Contains("synthetic-secret")), "OAuth setup is not written as plaintext");
            var googleStore = new ProtectedAccountFiles(Path.Combine(folder, "google-token-fixture"));
            var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse { AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh", ExpiresInSeconds = 3600, IssuedUtc = DateTime.UtcNow };
            googleStore.StoreAsync("fixture", token).GetAwaiter().GetResult();
            var restored = googleStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>("fixture").GetAwaiter().GetResult();
            Check(restored.AccessToken == token.AccessToken && restored.RefreshToken == token.RefreshToken && restored.ExpiresInSeconds == 3600 && restored.IssuedUtc == token.IssuedUtc,
                "Google token material and expiry survive the protected store round trip");
            using var dialog = new WindowLifetime(new AccountsDialog(new AccountService(folder)));
            Check(dialog.Window.Content is DockPanel, "native account manager constructs with an isolated cache");
            var draft = ImportIdentity.Account("Thunderbird", "Work", "fixture@outlook.com", "")!;
            var migrationFolder = Path.Combine(folder, "migration"); Directory.CreateDirectory(migrationFolder);
            var ics = Path.Combine(migrationFolder, "fixture.ics");
            File.WriteAllText(ics, "BEGIN:VCALENDAR\nVERSION:2.0\nBEGIN:VEVENT\nUID:fixture\nSUMMARY:Imported planning\nDTSTART:20261104T090000\nDTEND:20261104T100000\nEND:VEVENT\nEND:VCALENDAR");
            var importedDocument = CalendarDocument.Empty(); var importSaves = 0;
            var appearanceBeforeImport = NativeTheme.Settings;
            var report = new DiscoveryReport([draft], [ics], [new("Thunderbird", "Dark"), new("Fantastical", DefaultDurationMinutes: 45)], ["Synthetic migration preview"]);
            using (var importer = new WindowLifetime(new ImportDialog(migrationFolder, () => importedDocument, next => { importedDocument = next; importSaves++; return true; }, () => report)))
            {
                importer.Window.Show();
                var type = typeof(ImportDialog);
                bool Idle() => importer.Window.IsLoaded && !(bool)type.GetField("busy", flags)!.GetValue(importer.Window)!;
                PumpUntil(Idle);
                var profileRows = (StackPanel)type.GetField("profiles", flags)!.GetValue(importer.Window)!;
                var fileRows = (StackPanel)type.GetField("files", flags)!.GetValue(importer.Window)!;
                var importButton = (Button)type.GetField("apply", flags)!.GetValue(importer.Window)!;
                Check(profileRows.Children.Count == 1 && fileRows.Children.Count == 1 && importSaves == 0 && !File.Exists(Path.Combine(migrationFolder, "imported-accounts.json")), "migration preview lists identities and ICS without persisting them");
                Check(NativeTheme.Settings == appearanceBeforeImport, "discovered appearance never changes the theme before import");
                ((ComboBox)type.GetField("theme", flags)!.GetValue(importer.Window)!).SelectedIndex = 1;
                ((ComboBox)type.GetField("duration", flags)!.GetValue(importer.Window)!).SelectedIndex = 1;
                importer.Window.Dispatcher.Invoke(() => importButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                PumpUntil(Idle);
                Check(importSaves == 1 && importedDocument.Events.Single().Title == "Imported planning" && new AccountDraftStore(Path.Combine(migrationFolder, "imported-accounts.json")).Read().Single() == draft && NativeTheme.Mode == AppThemeMode.Dark && new EventDefaults(migrationFolder).Minutes == 45, "selected migration persists events, sign-in drafts and compatible settings");
                importer.Window.Dispatcher.Invoke(() => importButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                PumpUntil(Idle);
                Check(importSaves == 1 && importedDocument.Events.Count == 1, "repeating import does not duplicate events or re-save the calendar");
            }
            var migratedAccounts = new AccountsDialog(new AccountService(migrationFolder));
            typeof(AccountsDialog).GetMethod("Render", flags | BindingFlags.DeclaredOnly)!.Invoke(migratedAccounts, [Array.Empty<(Daymark.Providers.ConnectedAccount, List<RemoteCalendar>)>()]);
            var accountRows = (StackPanel)typeof(AccountsDialog).GetField("rows", flags)!.GetValue(migratedAccounts)!;
            Check(accountRows.Children.OfType<Border>().Count() == 1, "imported account appears in the account manager before sign-in"); migratedAccounts.Close();
            using (var editor = new WindowLifetime(new EventEditor(new(2026, 11, 4), null, new(9, 0), defaultDurationMinutes: new EventDefaults(migrationFolder).Minutes)))
            {
                var endField = (TextBox)typeof(EventEditor).GetField("endTime", flags)!.GetValue(editor.Window)!;
                Check(endField.Text == "09:45", "new-event editor uses the imported default duration");
            }
            var phraseDay = new DateOnly(2026, 11, 4);
            var preset = QuickEventParser.Parse("Team review today at 9am for 45m every week remind 27m before", phraseDay, 60, "personal");
            var review = new EventEditor(phraseDay, null, defaultZone: TimeZoneInfo.Utc, preset: preset);
            review.Loaded += (_, _) => review.Dispatcher.InvokeAsync(() =>
            {
                FindButtons(review).Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (review.Result is null) review.Close();
            });
            Check(review.ShowDialog() == true && review.Result is { ReminderMinutes: 27, StartUtc: not null } saved &&
                saved.Title == preset.Title && saved.Start == preset.Start && saved.End == preset.End && saved.Recurrence?.Frequency == RecurrenceFrequency.Weekly,
                "reviewing a Quick Add draft uses the native editor and preserves its timing, repeat and custom reminder");

            var reminderFolder = Path.Combine(folder, "reminders"); Directory.CreateDirectory(reminderFolder);
            File.WriteAllText(Path.Combine(reminderFolder, "keep-in-tray.txt"), "");
            var dueUtc = DateTime.UtcNow.AddSeconds(3);
            var dueWall = DateTime.SpecifyKind(dueUtc, DateTimeKind.Unspecified);
            var reminderEvent = CalendarTime.Anchor(new(Guid.NewGuid(), "Test tray reminder", dueWall, dueWall.AddMinutes(30), ReminderMinutes: 0), TimeZoneInfo.Utc);
            var trayWindow = new CalendarWindow(new CalendarStore(Path.Combine(reminderFolder, "calendar.json")), new(3, [reminderEvent]), true);
            var closed = false;
            trayWindow.Closed += (_, _) => closed = true;
            trayWindow.Show();
            PumpUntil(() => trayWindow.IsLoaded);
            trayWindow.Close();
            Check(!trayWindow.IsVisible && !closed, "closing an opted-in calendar hides it to the notification area");
            PumpUntil(() => app.Windows.OfType<ReminderWindow>().Any(popup => popup.IsVisible));
            var reminderWindow = app.Windows.OfType<ReminderWindow>().Single();
            Check(!trayWindow.IsVisible && reminderWindow.IsVisible, "a scheduled reminder window opens while the calendar is hidden");
            FindButtons(reminderWindow).Single(button => (string?)button.Content == "Snooze 5 min").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var reminderState = new ReminderStateStore(Path.Combine(reminderFolder, ReminderStateStore.FileName)).Load();
            Check(reminderState.Values.Single() > DateTime.UtcNow && !reminderWindow.IsVisible, "snoozing a reminder persists its new deadline and closes the alert");
            typeof(CalendarWindow).GetMethod("QuitDaymark", flags)!.Invoke(trayWindow, null);
            Check(closed && typeof(CalendarWindow).GetField("tray", flags)!.GetValue(trayWindow) is null, "explicit quit closes a hidden calendar and releases its tray icon");
            CheckPlanningWorkflow(folder, flags);
            window.Close(); app.Shutdown();
            Console.WriteLine("38 Windows checks passed");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }
    private static void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static IEnumerable<Button> FindButtons(DependencyObject root) => FindElements<Button>(root);

    private static IEnumerable<T> FindElements<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T element) yield return element;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in FindElements<T>(child)) yield return descendant;
    }

    private static void CheckPlanningWorkflow(string folder, BindingFlags flags)
    {
        var planningFolder = Path.Combine(folder, "planner");
        Directory.CreateDirectory(planningFolder);
        var day = CalendarTime.Today(TimeZoneInfo.Local);
        var start = day.ToDateTime(new TimeOnly(9, 0));
        var work = new CalendarDefinition("work", "Work");
        var workEvent = new CalendarEvent(Guid.NewGuid(), "Work session", start, start.AddHours(1), CalendarId: work.Id);
        var personalEvent = workEvent with { Id = Guid.NewGuid(), Title = "Personal session", CalendarId = "personal" };
        var task = new CalendarTask(Guid.NewGuid(), "Prepare notes", day);
        var snapshot = new CalendarDocument(3, [workEvent, personalEvent], [CalendarDefinition.Personal, work], [task]);
        var settings = new CalendarViewSettingsStore(planningFolder);
        var focus = new CalendarSet(Guid.NewGuid(), "Work focus", [work.Id], work.Id, ShowTasks: false);
        settings.Save(new([focus], [], focus.Id));
        var store = new CalendarStore(Path.Combine(planningFolder, "calendar.json"));
        store.Save(snapshot);
        var planner = new CalendarWindow(store, store.Load(), true);
        planner.Show();
        PumpUntil(() => planner.IsLoaded);
        var type = typeof(CalendarWindow);
        var views = (Dictionary<CalendarViewMode, RadioButton>)type.GetField("viewButtons", flags)!.GetValue(planner)!;
        var content = (ContentControl)type.GetField("calendarContent", flags)!.GetValue(planner)!;
        views[CalendarViewMode.Agenda].IsChecked = true;
        int AgendaRows() => FindElements<ListBox>((DependencyObject)content.Content).Single().Items.Count;
        Check(AgendaRows() == 2 && (string?)type.GetMethod("DefaultCalendarId", flags)!.Invoke(planner, null) == work.Id,
            "an active calendar set limits Agenda rows and routes new events to its default calendar");
        var selector = (ComboBox)type.GetField("calendarSetPicker", flags)!.GetValue(planner)!;
        selector.SelectedIndex = 0;
        Check(AgendaRows() == 4 && settings.Load().ActiveSetId is null, "switching to all calendars restores events and due tasks in Agenda");

        type.GetMethod("MoveEvent", flags)!.Invoke(planner, [workEvent, day.AddDays(1), new TimeOnly(14, 15)]);
        Check(store.Load().Events.Single(item => item.Id == workEvent.Id).Start == day.AddDays(1).ToDateTime(new TimeOnly(14, 15)) &&
            store.Load().TaskList.Single() == task, "the reschedule action saves the new start and preserves task data");
        planner.Close();

        snapshot = store.Load();
        var tasks = new TasksDialog(() => snapshot, (next, _) => { store.Save(next); snapshot = next; return true; }, () => work.Id, day, TimeZoneInfo.Utc, 45);
        tasks.Show();
        PumpUntil(() => tasks.IsLoaded);
        FindElements<ListBox>(tasks).Single().SelectedIndex = 0;
        tasks.Dispatcher.InvokeAsync(() =>
        {
            var editor = Application.Current.Windows.OfType<EventEditor>().Single(window => window.IsVisible);
            FindButtons(editor).Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (editor.Result is null) editor.Close();
        }, System.Windows.Threading.DispatcherPriority.Background);
        FindButtons(tasks).Single(button => Equals(button.Content, "_Schedule time…")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var linked = store.Load().TaskList.Single();
        Check(linked.ScheduledEventId is { } block && store.Load().Events.Any(item => item.Id == block && item.CalendarId == work.Id && item.End - item.Start == TimeSpan.FromMinutes(45)),
            "scheduling a task uses the event editor and persists a linked time block");
        FindButtons(tasks).Single(button => Equals(button.Content, "_Complete")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(store.Load().TaskList.Single().IsCompleted && store.Load().Events.Count == 3, "completing a task keeps its scheduled block on the calendar");
        tasks.Close();
    }
    private static void PumpUntil(Func<bool> done)
    {
        var frame = new System.Windows.Threading.DispatcherFrame(); var expiry = DateTime.UtcNow.AddSeconds(10);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (done() || DateTime.UtcNow > expiry) { timer.Stop(); frame.Continue = false; } }; timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame); if (!done()) throw new Exception("Windows operation timed out");
    }
    private sealed class WindowLifetime(Window window) : IDisposable { public Window Window => window; public void Dispose() => window.Close(); }
}
