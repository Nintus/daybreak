using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Daymark.Core;

namespace Daymark.Windows;

public sealed class WeekView : UserControl
{
    public double VerticalOffset => timeline.VerticalOffset;
    private readonly ScrollViewer timeline;
    private const string MoveFormat = "Daymark.TimelineMove";
    private sealed record MoveDrag(CalendarEvent Event, double OffsetMinutes);
    private MoveDrag? dragging;
    private Border? activeMarker;

    public WeekView(CalendarDocument document, DateOnly selected, Action<DateOnly> selectDay,
        Action<CalendarEvent> editEvent, Action<DateOnly, TimeOnly> createEvent, double offset, bool singleDay = false, DateOnly? today = null,
        Action<CalendarEvent, DateOnly, TimeOnly>? moveEvent = null, Func<CalendarEvent, bool>? canMove = null)
    {
        var first = singleDay ? selected : WeekLayout.StartOfWeek(selected);
        var dayCount = singleDay ? 1 : 7;
        var minimumWidth = singleDay ? 240 : 640;
        var events = document.Days(first, dayCount);
        var root = new DockPanel { MinWidth = minimumWidth, Width = minimumWidth };
        var outer = new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        outer.SizeChanged += (_, e) => root.Width = Math.Max(minimumWidth, e.NewSize.Width);
        Content = outer;
        var header = Columns(dayCount);
        header.Margin = new Thickness(0, 0, SystemParameters.VerticalScrollBarWidth, 0);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        Add(header, new TextBlock { Text = "All day", FontSize = 10, Foreground = NativeTheme.Muted, Margin = new Thickness(2, 47, 0, 0) }, 0);
        for (var i = 0; i < dayCount; i++)
        {
            var date = first.AddDays(i);
            var stack = new StackPanel();
            var heading = new Button { Content = new TextBlock { Text = date.ToString("ddd\nMMM d"), TextAlignment = TextAlignment.Center, FontSize = 12 },
                Style = NativeTheme.Style("CalendarDay"), Padding = new Thickness(4, 8, 4, 8),
                BorderBrush = NativeTheme.Line, BorderThickness = new Thickness(0, 0, 1, 1),
                Foreground = date == selected ? NativeTheme.AccentInk : NativeTheme.Muted,
                Background = date == selected ? NativeTheme.Soft : NativeTheme.Surface,
                FontWeight = date == (today ?? DateOnly.FromDateTime(DateTime.Today)) ? FontWeights.Bold : FontWeights.Normal };
            System.Windows.Automation.AutomationProperties.SetName(heading, $"Select {date:D}");
            heading.Click += (_, _) => selectDay(date); stack.Children.Add(heading);
            var allDay = new StackPanel();
            foreach (var item in events[i].Where(e => e.AllDay)) allDay.Children.Add(EventButton(item, editEvent));
            stack.Children.Add(new ScrollViewer { Content = allDay, MaxHeight = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Add(header, stack, i + 1);
        }
        var grid = Columns(dayCount);
        timeline = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        root.Children.Add(timeline);
        var hours = new Canvas { Height = 1440 };
        for (var hour = 0; hour < 24; hour++)
        {
            var label = new TextBlock { Text = $"{hour:00}:00", FontSize = 10, Foreground = NativeTheme.Muted };
            Canvas.SetTop(label, hour * 60); hours.Children.Add(label);
        }
        Add(grid, hours, 0);
        for (var i = 0; i < dayCount; i++)
        {
            var date = first.AddDays(i);
            var canvas = new Canvas { Height = 1440, ClipToBounds = true,
                Background = date == selected ? NativeTheme.Subtle : NativeTheme.Surface };
            var slots = WeekLayout.TimedDay(events[i], date);
            var lines = new List<Line>();
            for (var hour = 0; hour < 24; hour++)
            {
                var line = new Line { Y1 = hour * 60, Y2 = hour * 60, Stroke = NativeTheme.Line, StrokeThickness = 1, IsHitTestVisible = false };
                lines.Add(line);
                canvas.Children.Add(line);
            }
            var buttons = slots.Select(slot =>
            {
                var button = EventButton(slot.Event, editEvent);
                button.Height = Math.Min(1440 - slot.StartMinute, Math.Max(20, slot.EndMinute - slot.StartMinute - 2));
                Canvas.SetTop(button, slot.StartMinute);
                if (moveEvent is not null && canMove?.Invoke(slot.Event) == true) EnableDrag(button, slot, date);
                canvas.Children.Add(button);
                return (Slot: slot, Button: button);
            }).ToList();
            canvas.SizeChanged += (_, _) =>
            {
                foreach (var line in lines) line.X2 = canvas.ActualWidth;
                foreach (var (slot, button) in buttons)
                {
                    button.Width = Math.Max(1, canvas.ActualWidth / slot.ColumnCount - 5);
                    Canvas.SetLeft(button, 2 + slot.Column * canvas.ActualWidth / slot.ColumnCount);
                }
            };
            if (moveEvent is not null) EnableDrop(canvas, date, moveEvent);
            canvas.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount != 2 || e.OriginalSource != canvas) return;
                var minute = Math.Clamp((int)(e.GetPosition(canvas).Y / 30) * 30, 0, 1410);
                createEvent(date, new TimeOnly(minute / 60, minute % 60)); e.Handled = true;
            };
            Add(grid, new Border { Child = canvas, BorderBrush = NativeTheme.Line, BorderThickness = new Thickness(0, 0, 1, 0) }, i + 1);
        }
        Loaded += (_, _) => timeline.ScrollToVerticalOffset(offset);
    }

    private void EnableDrag(Button button, WeekSlot slot, DateOnly date)
    {
        Point? pressed = null;
        button.PreviewMouseLeftButtonDown += (_, args) => pressed = args.GetPosition(button);
        button.PreviewMouseLeftButtonUp += (_, _) => pressed = null;
        button.LostMouseCapture += (_, _) => pressed = null;
        button.PreviewMouseMove += (_, args) =>
        {
            if (pressed is not { } start || args.LeftButton != MouseButtonState.Pressed || dragging is not null) return;
            var cursor = args.GetPosition(button);
            if (Math.Abs(cursor.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(cursor.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            pressed = null;
            args.Handled = true;
            var clippedMinutes = Math.Max(0, (date.ToDateTime(TimeOnly.MinValue) - slot.Event.Start).TotalMinutes);
            var withinEvent = Math.Clamp(start.Y, 0, Math.Max(0, slot.EndMinute - slot.StartMinute));
            dragging = new(slot.Event, clippedMinutes + withinEvent);
            button.ReleaseMouseCapture();
            try { DragDrop.DoDragDrop(button, new DataObject(MoveFormat, "move", false), DragDropEffects.Move); }
            finally
            {
                dragging = null;
                if (activeMarker is not null) activeMarker.Visibility = Visibility.Collapsed;
                activeMarker = null;
            }
        };
    }

    private void EnableDrop(Canvas canvas, DateOnly date, Action<CalendarEvent, DateOnly, TimeOnly> moveEvent)
    {
        canvas.AllowDrop = true;
        var caption = new TextBlock { Foreground = NativeTheme.OnAccent, FontSize = 11, FontWeight = FontWeights.SemiBold };
        var marker = new Border
        {
            Child = caption, Background = NativeTheme.Accent, Padding = new Thickness(4, 1, 4, 1),
            CornerRadius = new CornerRadius(3), IsHitTestVisible = false, Visibility = Visibility.Collapsed
        };
        Canvas.SetLeft(marker, 2);
        Panel.SetZIndex(marker, 10);
        canvas.Children.Add(marker);

        bool Accepts(DragEventArgs args) => dragging is not null && args.Data.GetDataPresent(MoveFormat, false);
        int Minute(DragEventArgs args) => WeekLayout.SnapMoveMinute(args.GetPosition(canvas).Y - dragging!.OffsetMinutes);
        canvas.DragOver += (_, args) =>
        {
            args.Handled = true;
            args.Effects = Accepts(args) ? DragDropEffects.Move : DragDropEffects.None;
            if (args.Effects == DragDropEffects.None) return;
            var y = args.GetPosition(timeline).Y;
            if (y < 22) timeline.ScrollToVerticalOffset(timeline.VerticalOffset - 15);
            else if (y > timeline.ActualHeight - 22) timeline.ScrollToVerticalOffset(timeline.VerticalOffset + 15);
            var minute = Minute(args);
            caption.Text = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute)).ToString("t");
            Canvas.SetTop(marker, minute);
            if (activeMarker != marker && activeMarker is not null) activeMarker.Visibility = Visibility.Collapsed;
            activeMarker = marker;
            marker.Visibility = Visibility.Visible;
        };
        canvas.DragLeave += (_, _) => marker.Visibility = Visibility.Collapsed;
        canvas.Drop += (_, args) =>
        {
            marker.Visibility = Visibility.Collapsed;
            args.Handled = true;
            if (!Accepts(args)) { args.Effects = DragDropEffects.None; return; }
            var item = dragging!.Event;
            var time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Minute(args)));
            args.Effects = DragDropEffects.Move;
            Dispatcher.InvokeAsync(() => moveEvent(item, date, time), DispatcherPriority.Background);
        };
    }

    private static Grid Columns(int dayCount)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        for (var i = 0; i < dayCount; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        return grid;
    }
    private static void Add(Grid grid, UIElement element, int column) { Grid.SetColumn(element, column); grid.Children.Add(element); }
    private static Button EventButton(CalendarEvent item, Action<CalendarEvent> edit)
    {
        var description = $"{item.Title}\n{CalendarTime.Describe(item)}\n{item.Location}";
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = item.Title, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeights.SemiBold, FontSize = 12 });
        if (!item.AllDay) content.Children.Add(new TextBlock { Text = $"{item.Start:t} – {item.End:t}", FontSize = 10, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        var button = new Button { Content = content, ToolTip = description, Style = NativeTheme.Style("CalendarEvent"), ClipToBounds = true,
            Background = item.AllDay ? NativeTheme.TealSoft : NativeTheme.Soft, Foreground = CalendarPalette.Color(item.CalendarId),
            BorderBrush = CalendarPalette.Color(item.CalendarId), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top };
        System.Windows.Automation.AutomationProperties.SetName(button, description);
        button.Click += (_, _) => edit(item);
        return button;
    }
}
