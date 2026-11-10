using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace Daymark.Windows;

internal static class PlanningList
{
    public static ListBox Create(string accessibleName, bool grouped = false)
    {
        var list = new ListBox { Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
        AutomationProperties.SetName(list, accessibleName);
        ScrollViewer.SetCanContentScroll(list, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);

        var row = new FrameworkElementFactory(typeof(StackPanel));
        var title = new FrameworkElementFactory(typeof(TextBlock), "PlanningTitle");
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
        if (!SystemParameters.HighContrast)
        {
            title.SetValue(TextBlock.ForegroundProperty, NativeTheme.Ink);
            detail.SetValue(TextBlock.ForegroundProperty, NativeTheme.Muted);
        }
        row.AppendChild(detail);
        var template = new DataTemplate { VisualTree = row };
        if (grouped && !SystemParameters.HighContrast)
        {
            var heading = new DataTrigger { Binding = new Binding("IsHeading"), Value = true };
            heading.Setters.Add(new Setter(TextBlock.ForegroundProperty, NativeTheme.AccentInk, "PlanningTitle"));
            template.Triggers.Add(heading);
        }
        list.ItemTemplate = template;
        var itemStyle = new Style(typeof(ListBoxItem), NativeTheme.Style("AgendaItem"));
        itemStyle.Setters.Add(new Setter(AutomationProperties.NameProperty, new MultiBinding
        {
            StringFormat = "{0}. {1}", Bindings = { new Binding("Title"), new Binding("Detail") }
        }));
        list.ItemContainerStyle = itemStyle;
        return list;
    }
}
