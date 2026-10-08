using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Daymark.Core;
using Microsoft.Win32;

namespace Daymark.Windows;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var instance = new Mutex(true, "Local\\Daymark.Native.Calendar", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Daymark is already running. Use its existing window.", "Daymark");
            return;
        }
        var app = new Application();
        var store = new CalendarStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DaymarkNative", "calendar.json"));
        try { app.Run(new CalendarWindow(store, store.Load())); }
        catch (Exception error)
        {
            MessageBox.Show($"Daymark could not start. Your calendar has not been replaced.\n\n{error.Message}\n\nData: {store.Path}\nPrevious save: {store.Path}.bak",
                "Daymark — startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class CalendarWindow : Window
{
    private readonly CalendarStore store;
    private CalendarDocument document;
    private DateOnly selected = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly month;
    private readonly Grid days = new();
    private readonly TextBlock monthTitle = new() { FontSize = 26, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock dayTitle = new() { FontSize = 20, Margin = new Thickness(0, 0, 0, 16) };
    private readonly ListBox agenda = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly TextBlock status = new() { Foreground = Brushes.DimGray, Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBox search = new() { MinWidth = 180, Padding = new Thickness(8), Margin = new Thickness(0, 0, 12, 0) };
    private readonly Button edit;
    private readonly Button delete;
    private bool refreshing;

    public CalendarWindow(CalendarStore store, CalendarDocument document)
    {
        this.store = store;
        this.document = document;
        month = new DateOnly(selected.Year, selected.Month, 1);
        Title = "Daymark — Native Windows Preview";
        Width = 1100; Height = 760; MinWidth = 780; MinHeight = 560;
        Background = new SolidColorBrush(Color.FromRgb(248, 247, 244));
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel { Margin = new Thickness(24) };
        Content = root;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        search.ToolTip = "Search event titles, locations, and notes";
        System.Windows.Automation.AutomationProperties.SetName(search, "Search events");
        search.TextChanged += (_, _) => RefreshAgenda();
        actions.Children.Add(search);
        actions.Children.Add(Button("_New event", (_, _) => NewEvent()));
        header.Children.Add(new TextBlock { Text = "Daymark", FontSize = 28, FontWeight = FontWeights.SemiBold });

        var footer = new DockPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var backup = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(backup, Dock.Right); footer.Children.Add(backup);
        backup.Children.Add(Button("Save _backup", (_, _) => Export()));
        backup.Children.Add(Button("_Restore backup", (_, _) => Restore()));
        footer.Children.Add(status);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310) }); root.Children.Add(body);
        var calendar = new DockPanel { Margin = new Thickness(0, 0, 24, 0) }; body.Children.Add(calendar);
        var navigation = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        DockPanel.SetDock(navigation, Dock.Top); calendar.Children.Add(navigation);
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(controls, Dock.Right); navigation.Children.Add(controls);
        controls.Children.Add(Button("Previous", (_, _) => ChangeMonth(-1)));
        controls.Children.Add(Button("_Today", (_, _) => { selected = DateOnly.FromDateTime(DateTime.Today); month = new(selected.Year, selected.Month, 1); search.Clear(); Refresh(); }));
        controls.Children.Add(Button("Next", (_, _) => ChangeMonth(1)));
        navigation.Children.Add(monthTitle);
        for (var i = 0; i < 7; i++) days.ColumnDefinitions.Add(new ColumnDefinition());
        days.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < 6; i++) days.RowDefinitions.Add(new RowDefinition());
        calendar.Children.Add(days);
        var side = new DockPanel { Margin = new Thickness(8, 0, 0, 0) }; Grid.SetColumn(side, 1); body.Children.Add(side);
        DockPanel.SetDock(dayTitle, Dock.Top); side.Children.Add(dayTitle);
        var eventActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(eventActions, Dock.Bottom); side.Children.Add(eventActions);
        edit = Button("_Edit event", (_, _) => Edit()); delete = Button("_Delete event", (_, _) => Delete());
        eventActions.Children.Add(edit); eventActions.Children.Add(delete);
        agenda.SelectionChanged += (_, _) => { edit.IsEnabled = delete.IsEnabled = agenda.SelectedItem is CalendarEvent; };
        agenda.MouseDoubleClick += (_, _) => Edit();
        agenda.DisplayMemberPath = nameof(CalendarEvent.Title);
        side.Children.Add(agenda);
        var template = new DataTemplate(typeof(CalendarEvent));
        var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.MarginProperty, new Thickness(0, 6, 0, 8));
        var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title")); title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); row.AppendChild(title);
        var time = new FrameworkElementFactory(typeof(TextBlock)); time.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = new EventDescription() }); time.SetValue(TextBlock.FontSizeProperty, 12.0); time.SetValue(TextBlock.ForegroundProperty, Brushes.DimGray); time.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); row.AppendChild(time);
        template.VisualTree = row; agenda.DisplayMemberPath = ""; agenda.ItemTemplate = template;
        var itemStyle = new Style(typeof(ListBoxItem)); itemStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); agenda.ItemContainerStyle = itemStyle;
        InputBindings.Add(new KeyBinding(new ActionCommand(NewEvent), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(Export), Key.B, ModifierKeys.Control));
        Refresh();
    }
    private static Button Button(string label, RoutedEventHandler action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(3, 0, 3, 0) };
        button.Click += action; return button;
    }
    private void ChangeMonth(int amount) { month = month.AddMonths(amount); selected = month; Refresh(); }
    private void Refresh()
    {
        refreshing = true;
        days.Children.Clear(); monthTitle.Text = month.ToString("MMMM yyyy");
        var labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        for (var i = 0; i < 7; i++) { var label = new TextBlock { Text = labels[i], HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 0, 4, 10), Foreground = Brushes.DimGray }; Grid.SetColumn(label, i); days.Children.Add(label); }
        var start = month.AddDays(-(((int)month.DayOfWeek + 6) % 7));
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = date.Day.ToString(), FontSize = 18, FontWeight = date == DateOnly.FromDateTime(DateTime.Today) ? FontWeights.Bold : FontWeights.Normal });
            var items = document.On(date).ToList();
            foreach (var item in items.Take(2)) panel.Children.Add(new TextBlock { Text = item.Title, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
            if (items.Count > 2) panel.Children.Add(new TextBlock { Text = $"+{items.Count - 2} more", FontSize = 11 });
            var button = new Button { Content = panel, Padding = new Thickness(8), Margin = new Thickness(2), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                Background = date == selected ? new SolidColorBrush(Color.FromRgb(224, 218, 249)) : Brushes.White,
                Foreground = date.Month == month.Month ? Brushes.Black : Brushes.Gray,
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 221, 226)), BorderThickness = new Thickness(1), ToolTip = date.ToString("D") };
            System.Windows.Automation.AutomationProperties.SetName(button, $"{date:D}, {items.Count} events");
            button.Click += (_, _) => { selected = date; search.Clear(); Refresh(); };
            Grid.SetRow(button, i / 7 + 1); Grid.SetColumn(button, i % 7); days.Children.Add(button);
        }
        refreshing = false; RefreshAgenda(); status.Text = "Saved on this PC · Local-time calendar";
    }
    private void RefreshAgenda()
    {
        if (refreshing) return;
        var query = search.Text.Trim();
        dayTitle.Text = query.Length == 0 ? selected.ToString("ddd, MMM d") : "Search results";
        agenda.ItemsSource = query.Length == 0 ? document.On(selected).ToList() : document.Events
            .Where(e => (e.Title + "\n" + e.Location + "\n" + e.Notes).Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Start).ToList();
        edit.IsEnabled = delete.IsEnabled = false;
    }
    private void NewEvent() => ShowEditor(null);
    private void Edit() { if (agenda.SelectedItem is CalendarEvent item) ShowEditor(item); }
    private void ShowEditor(CalendarEvent? original)
    {
        var draft = original;
        while (true)
        {
            var editor = new EventEditor(selected, draft) { Owner = this };
            if (editor.ShowDialog() != true || editor.Result is null) return;
            draft = editor.Result;
            var events = document.Events.Where(e => e.Id != draft.Id).ToList(); events.Add(draft);
            if (!Save(new CalendarDocument(1, events))) continue;
            selected = DateOnly.FromDateTime(draft.Start); month = new(selected.Year, selected.Month, 1);
            search.Clear(); Refresh(); return;
        }
    }
    private bool Save(CalendarDocument next)
    {
        try { store.Save(next); document = next; return true; }
        catch (Exception error) { MessageBox.Show(this, $"Your change was not saved.\n\n{error.Message}", "Save failed", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    private void Delete()
    {
        if (agenda.SelectedItem is not CalendarEvent item) return;
        if (MessageBox.Show(this, $"Delete “{item.Title}”?", "Delete event", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (Save(new CalendarDocument(1, document.Events.Where(e => e.Id != item.Id).ToList()))) Refresh();
    }
    private void Export()
    {
        var picker = new SaveFileDialog { FileName = "daymark-native-backup.json", Filter = "Daymark native backup (*.json)|*.json" };
        if (picker.ShowDialog(this) != true) return;
        if (SameDataPath(picker.FileName)) { MessageBox.Show(this, "Choose a backup location outside the app's data files."); return; }
        try { CalendarStore.Export(document, picker.FileName); status.Text = "Backup saved"; }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private bool SameDataPath(string path) => string.Equals(Path.GetFullPath(path), Path.GetFullPath(store.Path), StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetFullPath(path), Path.GetFullPath(store.Path + ".bak"), StringComparison.OrdinalIgnoreCase);
    private void Restore()
    {
        var picker = new OpenFileDialog { Filter = "Daymark native backup (*.json)|*.json" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var incoming = CalendarStore.Read(picker.FileName);
            if (MessageBox.Show(this, "Replace your current native calendar with this backup? Export a backup first if you need the current events.", "Restore calendar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (Save(incoming)) { search.Clear(); Refresh(); }
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

public sealed class EventDescription : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var e = (CalendarEvent)value;
        return (e.AllDay ? $"{e.Start:d} · All day" : $"{e.Start:g} – {e.End:g}") + (string.IsNullOrWhiteSpace(e.Location) ? "" : $"\n{e.Location}");
    }
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
public sealed class ActionCommand(Action action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
