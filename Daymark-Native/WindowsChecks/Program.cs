using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;
using Daymark.Windows;

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
            window.Close(); app.Shutdown();
            Console.WriteLine("20 Windows checks passed");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }
    private static void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private sealed class WindowLifetime(Window window) : IDisposable { public Window Window => window; public void Dispose() => window.Close(); }
}
