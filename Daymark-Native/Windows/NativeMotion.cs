using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;

namespace Daymark.Windows;

// Finite opacity transitions only: no layout animation, perpetual timers or input delays.
internal static class NativeMotion
{
    private sealed record Active(long Id, RoutedEventHandler Unloaded);
    private static readonly Dictionary<FrameworkElement, Active> active = [];
    private static string preferencePath = "";
    private static long sequence;
    public static bool Reduced { get; private set; }
    public static bool Allowed(bool windowsAnimations, bool highContrast, bool reduced) => windowsAnimations && !highContrast && !reduced;
    public static bool Enabled => Allowed(SystemParameters.ClientAreaAnimation, SystemParameters.HighContrast, Reduced);
    internal static int ActiveCount => active.Count;
    public static void Initialize(Application app, string path)
    {
        preferencePath = path;
        try { Reduced = File.Exists(path) && new FileInfo(path).Length < 32 && File.ReadAllText(path).Trim() == "reduced"; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Reduced = false; }
        PropertyChangedEventHandler changed = (_, _) =>
        {
            if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(() => { if (!Enabled) StopAll(); });
        };
        SystemParameters.StaticPropertyChanged += changed;
        app.Exit += (_, _) => { SystemParameters.StaticPropertyChanged -= changed; StopAll(); };
    }
    public static void SetReduced(bool reduced)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(preferencePath)!);
        var temporary = preferencePath + ".tmp";
        File.WriteAllText(temporary, reduced ? "reduced" : "system"); File.Move(temporary, preferencePath, true);
        Reduced = reduced; if (!Enabled) StopAll();
    }
    public static void Reveal(FrameworkElement target)
    {
        Stop(target);
        if (!Enabled || !target.IsLoaded || !target.IsVisible) return;
        var opacity = target.Opacity;
        var id = ++sequence;
        RoutedEventHandler unloaded = (_, _) => Stop(target);
        active[target] = new(id, unloaded); target.Unloaded += unloaded;
        var animation = new DoubleAnimation(opacity * .88, opacity, TimeSpan.FromMilliseconds(120))
        {
            FillBehavior = FillBehavior.Stop, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(animation, 30);
        animation.Completed += (_, _) => { if (active.TryGetValue(target, out var current) && current.Id == id) Stop(target); };
        target.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
    public static void Stop(FrameworkElement target)
    {
        if (!active.Remove(target, out var previous)) return;
        target.Unloaded -= previous.Unloaded;
        target.BeginAnimation(UIElement.OpacityProperty, null);
    }
    public static void StopAll() { foreach (var target in active.Keys.ToArray()) Stop(target); }
}
