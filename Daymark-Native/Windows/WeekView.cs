using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Daymark.Core;

namespace Daymark.Windows;

public sealed class WeekView : UserControl
{
    public double VerticalOffset => timeline.VerticalOffset;
    private readonly ScrollViewer timeline;
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(224, 218, 249));

    public WeekView(CalendarDocument document, DateOnly selected, Action<DateOnly> selectDay,
        Action<CalendarEvent> editEvent, Action<DateOnly, TimeOnly> createEvent, double offset, bool singleDay = false, DateOnly? today = null)
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
        Add(header, new TextBlock { Text = "All day", FontSize = 11, Margin = new Thickness(2, 48, 0, 0) }, 0);
        for (var i = 0; i < dayCount; i++)
        {
            var date = first.AddDays(i);
            var stack = new StackPanel();
            var heading = new Button { Content = date.ToString("ddd\nMMM d"), Padding = new Thickness(4, 8, 4, 8),
                Background = date == selected ? Accent : Brushes.White,
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
            var label = new TextBlock { Text = $"{hour:00}:00", FontSize = 11, Foreground = Brushes.DimGray };
            Canvas.SetTop(label, hour * 60); hours.Children.Add(label);
        }
        Add(grid, hours, 0);
        for (var i = 0; i < dayCount; i++)
        {
            var date = first.AddDays(i);
            var canvas = new Canvas { Height = 1440, ClipToBounds = true,
                Background = date == selected ? new SolidColorBrush(Color.FromRgb(247, 244, 255)) : Brushes.White };
            var slots = WeekLayout.TimedDay(events[i], date);
            void Draw()
            {
                canvas.Children.Clear();
                for (var hour = 0; hour < 24; hour++)
                    canvas.Children.Add(new Line { X1 = 0, X2 = canvas.ActualWidth, Y1 = hour * 60, Y2 = hour * 60,
                        Stroke = Brushes.Gainsboro, StrokeThickness = 1, IsHitTestVisible = false });
                foreach (var slot in slots)
                {
                    var button = EventButton(slot.Event, editEvent);
                    button.Width = Math.Max(1, canvas.ActualWidth / slot.ColumnCount - 3);
                    button.Height = Math.Min(1440 - slot.StartMinute, Math.Max(20, slot.EndMinute - slot.StartMinute));
                    Canvas.SetTop(button, slot.StartMinute);
                    Canvas.SetLeft(button, slot.Column * canvas.ActualWidth / slot.ColumnCount);
                    canvas.Children.Add(button);
                }
            }
            canvas.SizeChanged += (_, _) => Draw();
            canvas.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount != 2 || e.OriginalSource != canvas) return;
                var minute = Math.Clamp((int)(e.GetPosition(canvas).Y / 30) * 30, 0, 1410);
                createEvent(date, new TimeOnly(minute / 60, minute % 60)); e.Handled = true;
            };
            Add(grid, new Border { Child = canvas, BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 1, 0) }, i + 1);
        }
        Loaded += (_, _) => timeline.ScrollToVerticalOffset(offset);
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
        var button = new Button { Content = new TextBlock { Text = item.Title, TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap }, ToolTip = description, Background = Accent,
            Padding = new Thickness(3, 1, 3, 1), FontSize = 11, HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top };
        System.Windows.Automation.AutomationProperties.SetName(button, description);
        button.Click += (_, _) => edit(item);
        return button;
    }
}
