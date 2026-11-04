using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Daymark.Core;

namespace Daymark.Windows;

public sealed partial class CalendarWindow
{
    private readonly TextBlock agendaCount = new() { Foreground = NativeTheme.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel emptyAgenda = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock emptyTitle = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = NativeTheme.Ink, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 14, 0, 6) };
    private readonly TextBlock emptyDescription = new() { FontSize = 12, Foreground = NativeTheme.Muted, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 220 };

    private void BuildLayout(bool testCalendar)
    {
        var root = new DockPanel { Margin = new Thickness(24, 12, 24, 14) }; Content = root;
        var preferences = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        DockPanel.SetDock(preferences, Dock.Top); root.Children.Add(preferences);
        var zones = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(zones, Dock.Right); preferences.Children.Add(zones);
        zones.Children.Add(new TextBlock { Text = "TIME ZONE", Foreground = NativeTheme.Muted, FontSize = 10,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        var displayPicker = new ComboBox { ItemsSource = new[] { new ZoneChoice("@device", "Device time zone (automatic)") }
                .Concat(ZoneChoice.All.Where(choice => choice.Id is not null)).ToArray(), DisplayMemberPath = "Label", SelectedValuePath = "Id",
            Width = 275, IsTextSearchEnabled = true, SelectedValue = "@device", ToolTip = "Display time zone — event instants stay fixed" };
        System.Windows.Automation.AutomationProperties.SetName(displayPicker, "Display time zone");
        displayPicker.SelectionChanged += (_, _) =>
        {
            if (displayPicker.SelectedItem is not ZoneChoice { Id: not null } choice) return;
            followDeviceZone = choice.Id == "@device"; displayZone = followDeviceZone ? TimeZoneInfo.Local : CalendarTime.FindZone(choice.Id); Refresh();
        };
        zones.Children.Add(displayPicker);

        var menu = new Menu { Background = Brushes.Transparent, Foreground = NativeTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(MenuAction("_New event", NewEvent, "Ctrl+N")); file.Items.Add(MenuAction("_Go to date…", GoToDate, "Ctrl+G")); file.Items.Add(new Separator());
        file.Items.Add(MenuAction("Calendar _accounts…", ManageAccounts)); file.Items.Add(new Separator());
        file.Items.Add(MenuAction("Save _backup…", Export, "Ctrl+B")); file.Items.Add(MenuAction("_Restore backup…", Restore));
        file.Items.Add(new Separator()); file.Items.Add(MenuAction("E_xit", Close)); menu.Items.Add(file);
        var editMenu = new MenuItem { Header = "_Edit" }; undoItem.CommandTarget = redoItem.CommandTarget = this;
        editMenu.Items.Add(undoItem); editMenu.Items.Add(redoItem); menu.Items.Add(editMenu);
        var appearance = new MenuItem { Header = "_Appearance" };
        foreach (var mode in Enum.GetValues<AppThemeMode>())
        {
            var option = new MenuItem { Header = "_" + NativeTheme.Name(mode), IsCheckable = true, IsChecked = NativeTheme.Mode == mode };
            option.Click += (_, _) =>
            {
                try { NativeTheme.Select(mode); }
                catch (Exception error) { MessageBox.Show(this, $"Your appearance preference could not be saved.\n\n{error.Message}", "Appearance", MessageBoxButton.OK, MessageBoxImage.Error); }
                foreach (var sibling in appearance.Items.OfType<MenuItem>().Where(i => i.Tag is AppThemeMode)) sibling.IsChecked = (AppThemeMode)sibling.Tag == NativeTheme.Mode;
            };
            option.Tag = mode; appearance.Items.Add(option);
        }
        appearance.Items.Add(new Separator());
        appearance.Items.Add(MenuAction("_Theme studio…", () => new ThemeDialog { Owner = this }.ShowDialog()));
        var reducedMotion = new MenuItem { Header = "_Reduce motion", IsCheckable = true, IsChecked = NativeMotion.Reduced,
            ToolTip = "Disable Daymark transitions. Windows animation and high-contrast preferences always take priority." };
        reducedMotion.Click += (_, _) =>
        {
            try { NativeMotion.SetReduced(reducedMotion.IsChecked); }
            catch (Exception) { reducedMotion.IsChecked = NativeMotion.Reduced; MessageBox.Show(this, "The motion preference could not be saved. Check access to the app data folder.", "Appearance"); }
        };
        appearance.Items.Add(reducedMotion);
        appearance.SubmenuOpened += (_, _) => { foreach (var entry in appearance.Items.OfType<MenuItem>().Where(i => i.Tag is AppThemeMode)) entry.IsChecked = (AppThemeMode)entry.Tag == NativeTheme.Mode; };
        menu.Items.Add(appearance);
        var help = new MenuItem { Header = "_Help" };
        help.Items.Add(MenuAction("Time _zones", () => MessageBox.Show(this,
            "The display time zone changes how timed events appear. Each anchored event keeps its own source zone and UTC instants.\n\nFloating events keep their clock times in every zone. Choose a time zone in an event to anchor it. All-day events keep their dates.", "Time zones", MessageBoxButton.OK, MessageBoxImage.Information)));
        help.Items.Add(MenuAction("_About Daymark", () => MessageBox.Show(this,
            $"Daymark {BuildInfo.Version}\nNative Windows preview\n\n{(testCalendar ? "Test calendar" : "Personal calendar")}\nData: {store.Path}\n\nGoogle/Microsoft read-only sync requires your OAuth app setup. Two-way editing and reminders are not available yet.",
            "About Daymark", MessageBoxButton.OK, MessageBoxImage.Information))); menu.Items.Add(help); preferences.Children.Add(menu);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (_, _) => NavigateHistory(false),
            (_, e) => { e.CanExecute = history.CanUndo; e.Handled = true; }));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (_, _) => NavigateHistory(true),
            (_, e) => { e.CanExecute = history.CanRedo; e.Handled = true; }));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 22) }; DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        var views = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(new Border { Background = NativeTheme.Line, CornerRadius = new CornerRadius(9), Padding = new Thickness(3),
            Margin = new Thickness(0, 0, 14, 0), Child = views });
        foreach (var mode in Enum.GetValues<CalendarViewMode>())
        {
            var segment = new RadioButton { Content = mode.ToString(), GroupName = "CalendarView", IsChecked = mode == viewMode,
                Style = NativeTheme.Style("ViewSegment") };
            System.Windows.Automation.AutomationProperties.SetName(segment, $"{mode} view");
            segment.Checked += (_, _) => { viewMode = mode; Refresh(); }; views.Children.Add(segment);
        }
        search.Width = 195; search.MinWidth = 0; search.Tag = "Search events…"; search.FontSize = 13;
        search.ToolTip = "Search event titles, locations, and notes";
        System.Windows.Automation.AutomationProperties.SetName(search, "Search events");
        search.TextChanged += (_, _) => { searchDelay.Stop(); searchDelay.Start(); }; actions.Children.Add(search);
        var create = Button("＋ _New event", (_, _) => NewEvent()); create.Style = NativeTheme.Style("PrimaryButton"); create.Padding = new Thickness(15, 9, 15, 9); actions.Children.Add(create);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var logo = HorizonBrand.Logo(40); logo.Margin = new Thickness(0, 0, 10, 0); logo.IsHitTestVisible = false;
        brand.Children.Add(logo);
        brand.Children.Add(new TextBlock { Text = "Daymark", FontFamily = HorizonBrand.Wordmark, FontSize = 27, Foreground = NativeTheme.Ink, VerticalAlignment = VerticalAlignment.Center });
        if (testCalendar) brand.Children.Add(new TextBlock { Text = "TEST", Foreground = NativeTheme.Muted, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }); header.Children.Add(brand);

        var calendarsRow = new DockPanel { Margin = new Thickness(0, -8, 0, 14) }; DockPanel.SetDock(calendarsRow, Dock.Top); root.Children.Add(calendarsRow);
        var manage = Button("Manage calendars…", (_, _) => ManageCalendars()); DockPanel.SetDock(manage, Dock.Right); calendarsRow.Children.Add(manage);
        calendarsRow.Children.Add(new ScrollViewer { Content = calendarFilters, MaxHeight = 66, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

        var footer = new DockPanel { Margin = new Thickness(2, 12, 2, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(status);
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); root.Children.Add(body);
        body.SizeChanged += (_, e) =>
        {
            body.ColumnDefinitions[1].Width = new GridLength(e.NewSize.Width < 1000 ? 260 : 300);
            search.Width = e.NewSize.Width < 1000 ? 150 : 195;
        };
        var calendar = new DockPanel();
        body.Children.Add(new Border { Background = NativeTheme.Surface, BorderBrush = NativeTheme.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 18, 0), Child = calendar });
        var navigation = new DockPanel { Margin = new Thickness(0, 0, 0, 20) }; DockPanel.SetDock(navigation, Dock.Top); calendar.Children.Add(navigation);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(controls, Dock.Right); navigation.Children.Add(controls);
        var previous = Button("‹", (_, _) => ChangePeriod(-1)); previous.Width = 32; previous.FontSize = 20; previous.Padding = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(previous, "Previous period"); controls.Children.Add(previous);
        controls.Children.Add(Button("_Today", (_, _) => { selected = CalendarTime.Today(displayZone); month = new(selected.Year, selected.Month, 1); search.Clear(); Refresh(); }));
        var next = Button("›", (_, _) => ChangePeriod(1)); next.Width = 32; next.FontSize = 20; next.Padding = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(next, "Next period"); controls.Children.Add(next); navigation.Children.Add(monthTitle);
        for (var i = 0; i < 7; i++) days.ColumnDefinitions.Add(new ColumnDefinition());
        days.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); for (var i = 0; i < 6; i++) days.RowDefinitions.Add(new RowDefinition());
        calendar.Children.Add(calendarContent);

        var side = new DockPanel(); var sideCard = new Border { Background = NativeTheme.Surface, BorderBrush = NativeTheme.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Child = side }; Grid.SetColumn(sideCard, 1); body.Children.Add(sideCard);
        var dayHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        dayHeader.Children.Add(new TextBlock { Text = "SCHEDULE", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = NativeTheme.Muted });
        dayHeader.Children.Add(dayTitle); dayHeader.Children.Add(agendaCount); DockPanel.SetDock(dayHeader, Dock.Top); side.Children.Add(dayHeader);
        var eventActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        eventActions.Children.Add(edit); eventActions.Children.Add(delete); DockPanel.SetDock(eventActions, Dock.Bottom); side.Children.Add(eventActions);
        if (document.Events.Any(item => !item.AllDay && item.TimeZoneId is null))
        {
            var notice = new TextBlock { Text = "Floating events keep their clock time when you change zones.", FontSize = 11, Foreground = NativeTheme.Muted,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(notice, Dock.Bottom); side.Children.Add(notice);
        }
        var agendaHost = new Grid(); side.Children.Add(agendaHost); agendaHost.Children.Add(agenda);
        emptyAgenda.Children.Add(new Border { Width = 54, Height = 54, CornerRadius = new CornerRadius(27), Background = NativeTheme.Subtle, HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = "\uE787", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 23, Foreground = NativeTheme.Muted,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center } });
        emptyAgenda.Children.Add(emptyTitle); emptyAgenda.Children.Add(emptyDescription); agendaHost.Children.Add(emptyAgenda);
        agenda.SelectionChanged += (_, _) =>
        {
            var connected = agenda.SelectedItem is CalendarEvent item && IsProviderEvent(item);
            edit.Content = connected ? "_View event" : "_Edit event";
            edit.IsEnabled = agenda.SelectedItem is CalendarEvent; delete.IsEnabled = agenda.SelectedItem is CalendarEvent && !connected;
        };
        agenda.MouseDoubleClick += (_, _) => Edit();
        var template = new DataTemplate(typeof(CalendarEvent));
        var row = new FrameworkElementFactory(typeof(StackPanel));
        var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); title.SetValue(TextBlock.FontSizeProperty, 13.0); title.SetValue(TextBlock.ForegroundProperty, NativeTheme.Ink); title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); row.AppendChild(title);
        var time = new FrameworkElementFactory(typeof(TextBlock)); time.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = new EventDescription(() => search.Text.Trim().Length > 0) });
        time.SetValue(TextBlock.FontSizeProperty, 12.0); time.SetValue(TextBlock.ForegroundProperty, NativeTheme.Muted); time.SetValue(TextBlock.MarginProperty, new Thickness(0, 6, 0, 0));
        time.SetValue(TextBlock.LineHeightProperty, 18.0); time.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); row.AppendChild(time);
        if (SystemParameters.HighContrast)
        {
            foreach (var text in new[] { title, time }) text.SetBinding(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1) });
        }
        template.VisualTree = row; agenda.ItemTemplate = template; agenda.ItemContainerStyle = NativeTheme.Style("AgendaItem");
        ScrollViewer.SetHorizontalScrollBarVisibility(agenda, ScrollBarVisibility.Disabled);
    }

    private static MenuItem MenuAction(string title, Action action, string gesture = "")
    {
        var item = new MenuItem { Header = title, InputGestureText = gesture }; item.Click += (_, _) => action(); return item;
    }
}
