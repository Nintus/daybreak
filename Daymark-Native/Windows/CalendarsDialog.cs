using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class CalendarsDialog : Window
{
    public List<CalendarDefinition>? Result { get; private set; }
    public CalendarsDialog(CalendarDocument document)
    {
        Title = "Manage calendars"; Width = 520; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Surface; Foreground = NativeTheme.Ink; FontSize = 14; NativeTheme.Attach(this);
        var calendars = new ObservableCollection<CalendarDefinition>(document.CalendarList);
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Your calendars", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Keep work, personal plans, and other schedules organized.", Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 16) });
        var list = new ListBox { ItemsSource = calendars, DisplayMemberPath = "Name", Height = 170, Background = NativeTheme.Subtle, Foreground = NativeTheme.Ink,
            BorderBrush = NativeTheme.Line, ItemContainerStyle = NativeTheme.Style("AgendaItem") }; panel.Children.Add(list);
        var name = new TextBox { MaxLength = 80 }; var color = new ComboBox { ItemsSource = CalendarDefinition.Colors, SelectedIndex = 0 };
        panel.Children.Add(new Label { Content = "_Name", Target = name, Margin = new Thickness(0, 10, 0, 0) }); panel.Children.Add(name);
        panel.Children.Add(new Label { Content = "_Color", Target = color, Margin = new Thickness(0, 8, 0, 0) }); panel.Children.Add(color);
        System.Windows.Automation.AutomationProperties.SetName(name, "Calendar name"); System.Windows.Automation.AutomationProperties.SetName(color, "Calendar color");
        var error = new TextBlock { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        var row = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) }; panel.Children.Add(row);
        var update = new Button { Content = "_Update selected", Margin = new Thickness(0, 0, 7, 0) };
        var add = new Button { Content = "_Add calendar", Margin = new Thickness(0, 0, 7, 0) };
        var remove = new Button { Content = "Remove empty" };
        row.Children.Add(update); row.Children.Add(add); row.Children.Add(remove); panel.Children.Add(error);
        void Selection()
        {
            if (list.SelectedItem is not CalendarDefinition selected) return;
            name.Text = selected.Name; color.SelectedItem = selected.Color;
            remove.IsEnabled = calendars.Count > 1 && document.Events.All(e => e.CalendarId != selected.Id);
        }
        list.SelectionChanged += (_, _) => Selection();
        void Change(bool create)
        {
            try
            {
                var selected = list.SelectedItem as CalendarDefinition;
                if (!create && selected is null) return;
                var value = new CalendarDefinition(create ? Guid.NewGuid().ToString("N") : selected!.Id, name.Text.Trim(), (string?)color.SelectedItem ?? "Blue"); value.Validate();
                if (create) { if (calendars.Count >= 100) throw new InvalidDataException("Keep at most 100 calendars."); calendars.Add(value); }
                else calendars[calendars.IndexOf(selected!)] = value;
                list.SelectedItem = value; error.Text = "";
            }
            catch (Exception problem) { error.Text = problem.Message; }
        }
        update.Click += (_, _) => Change(false); add.Click += (_, _) => Change(true);
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not CalendarDefinition selected || !remove.IsEnabled) return;
            if (MessageBox.Show(this, $"Remove the empty calendar “{selected.Name}”?", "Remove calendar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            calendars.Remove(selected); list.SelectedIndex = 0;
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        actions.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var save = new Button { Content = "_Save calendars", Style = NativeTheme.Style("PrimaryButton") }; actions.Children.Add(save); panel.Children.Add(actions);
        save.Click += (_, _) =>
        {
            // Apply the currently edited row too; Save must not silently ignore typed changes.
            Change(false);
            if (error.Text.Length > 0) return;
            try { var next = document with { Version = 3, Calendars = calendars.ToList() }; next.Validate(); Result = next.Calendars; DialogResult = true; }
            catch (Exception problem) { error.Text = problem.Message; }
        };
        list.SelectedIndex = 0;
    }
}
