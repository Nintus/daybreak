using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Daymark.Windows;

internal static class PlanningList
{
    public static ListBox Create(string accessibleName)
    {
        var list = new ListBox { Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
        System.Windows.Automation.AutomationProperties.SetName(list, accessibleName);
        ScrollViewer.SetCanContentScroll(list, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);

        var row = new FrameworkElementFactory(typeof(StackPanel));
        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new Binding("Title"));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        title.SetValue(TextBlock.FontSizeProperty, 14.0);
        title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        row.AppendChild(title);
        var detail = new FrameworkElementFactory(typeof(TextBlock));
        detail.SetBinding(TextBlock.TextProperty, new Binding("Detail"));
        detail.SetValue(TextBlock.FontSizeProperty, 12.0);
        detail.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));
        detail.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        if (!SystemParameters.HighContrast) detail.SetValue(TextBlock.ForegroundProperty, NativeTheme.Muted);
        row.AppendChild(detail);
        list.ItemTemplate = new DataTemplate { VisualTree = row };
        list.ItemContainerStyle = NativeTheme.Style("AgendaItem");
        return list;
    }
}
