using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class MonthDayButton : Button
{
    public MonthDayButton(DateOnly date, bool inMonth, bool selected, bool today,
        IReadOnlyList<CalendarEvent> events, Action select)
    {
        Style = NativeTheme.Style("CalendarDay");
        Padding = new Thickness(8); Margin = new Thickness(0); ClipToBounds = true;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        BorderBrush = NativeTheme.Line; BorderThickness = new Thickness(0, 0, 1, 1);
        if (selected && SystemParameters.HighContrast) { BorderBrush = NativeTheme.Accent; BorderThickness = new Thickness(2); }
        Background = selected ? NativeTheme.Soft : inMonth ? NativeTheme.Surface : NativeTheme.Subtle;
        var content = new Grid(); content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());
        var heading = new DockPanel { Height = 28 };
        var count = new TextBlock { Text = events.Count.ToString(), FontSize = 10, Foreground = NativeTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
        DockPanel.SetDock(count, Dock.Right); heading.Children.Add(count);
        heading.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), HorizontalAlignment = HorizontalAlignment.Left,
            Background = today ? NativeTheme.Accent : Brushes.Transparent,
            Child = new TextBlock { Text = date.Day.ToString(), FontSize = 13, FontWeight = today || selected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = today ? SystemColors.HighlightTextBrush : inMonth ? NativeTheme.Ink : NativeTheme.Muted,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center } });
        content.Children.Add(heading);
        var list = new StackPanel { ClipToBounds = true }; Grid.SetRow(list, 1); content.Children.Add(list);
        Content = content;
        ToolTip = $"{date:D}\n{events.Count} events";
        System.Windows.Automation.AutomationProperties.SetName(this, $"{date:D}, {events.Count} events");
        var previousSlots = -1;
        SizeChanged += (_, e) =>
        {
            var slots = Math.Clamp((int)((e.NewSize.Height - 44) / 18), 0, 3);
            if (slots == previousSlots) return; previousSlots = slots; list.Children.Clear();
            var visible = Math.Min(events.Count, slots);
            count.Text = $"+{events.Count - visible}";
            count.ToolTip = "Select this day to see the full schedule.";
            count.Visibility = events.Count > visible ? Visibility.Visible : Visibility.Collapsed;
            foreach (var item in events.Take(visible))
                list.Children.Add(new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(0, 3, 0, 0),
                    Background = item.AllDay ? NativeTheme.TealSoft : NativeTheme.Soft,
                    Child = new TextBlock { Text = item.Title, FontSize = 11, Foreground = item.AllDay ? NativeTheme.Teal : NativeTheme.AccentInk,
                        TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = item.Title } });
        };
        Click += (_, _) => select();
    }
}
