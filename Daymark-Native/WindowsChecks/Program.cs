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
            var auth = new AccountAuthorization(folder);
            auth.Configure(new("fixture.apps.googleusercontent.com", "synthetic-secret", "11111111-1111-1111-1111-111111111111"));
            Check(new AccountAuthorization(folder).Applications.GoogleClientSecret == "synthetic-secret", "OAuth setup survives a protected-cache round trip");
            Check(!Directory.GetFiles(Path.Combine(folder, "credentials")).Any(p => System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(p)).Contains("synthetic-secret")), "OAuth setup is not written as plaintext");
            using var dialog = new WindowLifetime(new AccountsDialog(new AccountService(folder)));
            Check(dialog.Window.Content is DockPanel, "native account manager constructs with an isolated cache");
            window.Close(); app.Shutdown();
            Console.WriteLine("7 Windows checks passed");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }
    private static void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private sealed class WindowLifetime(Window window) : IDisposable { public Window Window => window; public void Dispose() => window.Close(); }
}
