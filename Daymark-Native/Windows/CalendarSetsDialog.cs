using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class CalendarSetsDialog : Window
{
    private readonly CalendarViewSettings original;
    private readonly IReadOnlyList<CalendarDefinition> calendars;
    private readonly IReadOnlyList<CalendarDefinition> local;
    private readonly string[] visible;
    private readonly ObservableCollection<CalendarSet> sets;
    private readonly ListBox list = new() { DisplayMemberPath = nameof(CalendarSet.Name), Height = 190 };
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Error };
    public CalendarViewSettings? Result { get; private set; }

    public CalendarSetsDialog(CalendarViewSettings original, IReadOnlyList<CalendarDefinition> calendars,
        IReadOnlyList<CalendarDefinition> local, ISet<string> hidden)
    {
        this.original = original;
        this.calendars = calendars;
        this.local = local;
        visible = calendars.Where(calendar => !hidden.Contains(calendar.Id)).Select(calendar => calendar.Id).ToArray();
        sets = new(original.Sets);
        Title = "Calendar sets";
        Width = 550; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Surface; Foreground = NativeTheme.Ink; FontSize = 14;
        NativeTheme.Attach(this);
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Calendar sets", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Save groups of visible calendars and choose where new events go in each set.", Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 14) });
        list.ItemsSource = sets;
        list.Background = NativeTheme.Subtle;
        list.ItemContainerStyle = NativeTheme.Style("AgendaItem");
        System.Windows.Automation.AutomationProperties.SetName(list, "Saved calendar sets");
        panel.Children.Add(list);
        var editActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 10) };
        var add = new Button { Content = "_New from current view…" };
        var edit = new Button { Content = "_Edit…" };
        var remove = new Button { Content = "_Delete set" };
        foreach (var button in new[] { add, edit, remove })
        {
            button.Margin = new Thickness(0, 0, 6, 0);
            editActions.Children.Add(button);
        }
        panel.Children.Add(editActions);
        panel.Children.Add(error);
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var save = new Button { Content = "_Save changes" };
        var apply = new Button { Content = "_Use selected set", Style = NativeTheme.Style("PrimaryButton") };
        foreach (var button in new[] { cancel, save, apply })
        {
            button.Margin = new Thickness(6, 0, 0, 0);
            footer.Children.Add(button);
        }
        panel.Children.Add(footer);

        void UpdateActions() => edit.IsEnabled = remove.IsEnabled = apply.IsEnabled = list.SelectedItem is CalendarSet;
        list.SelectionChanged += (_, _) => UpdateActions();
        list.SelectedItem = sets.FirstOrDefault(set => set.Id == original.ActiveSetId) ?? sets.FirstOrDefault();
        UpdateActions();
        add.Click += (_, _) => Edit(null);
        edit.Click += (_, _) => { if (list.SelectedItem is CalendarSet set) Edit(set); };
        remove.Click += (_, _) => { if (list.SelectedItem is CalendarSet set) { sets.Remove(set); error.Text = ""; } };
        save.Click += (_, _) => Finish(sets.Any(set => set.Id == original.ActiveSetId) ? original.ActiveSetId : null);
        apply.Click += (_, _) => { if (list.SelectedItem is CalendarSet set) Finish(set.Id); };
    }

    private void Edit(CalendarSet? existing)
    {
        if (existing is null && sets.Count == 40) { error.Text = "Keep at most 40 calendar sets."; return; }
        var seed = existing ?? new CalendarSet(Guid.NewGuid(), "", visible, original.ActiveSet?.DefaultCalendarId, original.ActiveSet?.ShowTasks ?? true);
        var editor = new CalendarSetEditor(seed, calendars, local, sets.Where(set => set.Id != seed.Id).Select(set => set.Name)) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is not { } set) return;
        if (existing is null) sets.Add(set);
        else sets[sets.IndexOf(existing)] = set;
        list.SelectedItem = set;
        error.Text = "";
    }

    private void Finish(Guid? active)
    {
        var result = original with { Sets = sets.ToList(), ActiveSetId = active };
        try { result.Validate(); Result = result; DialogResult = true; }
        catch (InvalidDataException exception) { error.Text = exception.Message; }
    }
}

internal sealed class CalendarSetEditor : Window
{
    private sealed record DefaultChoice(string Name, string? Id);
    public CalendarSet? Result { get; private set; }

    public CalendarSetEditor(CalendarSet seed, IReadOnlyList<CalendarDefinition> calendars,
        IReadOnlyList<CalendarDefinition> local, IEnumerable<string> usedNames)
    {
        Title = "Edit calendar set";
        Width = 500; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Surface; Foreground = NativeTheme.Ink; FontSize = 14;
        NativeTheme.Attach(this);
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Field(string label, Control control)
        {
            panel.Children.Add(new Label { Content = label, Target = control, Margin = new Thickness(0, 6, 0, 0) });
            panel.Children.Add(control);
        }
        var name = new TextBox { Text = seed.Name, MaxLength = 64 };
        Field("_Name", name);
        var membership = new ListBox
        {
            ItemsSource = calendars, DisplayMemberPath = nameof(CalendarDefinition.Name), SelectionMode = SelectionMode.Multiple, Height = 195,
            Background = NativeTheme.Subtle, ItemContainerStyle = NativeTheme.Style("AgendaItem")
        };
        var selectedIds = seed.VisibleCalendarIds.ToHashSet(StringComparer.Ordinal);
        foreach (var calendar in calendars.Where(item => selectedIds.Contains(item.Id))) membership.SelectedItems.Add(calendar);
        var availableIds = calendars.Select(calendar => calendar.Id).ToHashSet(StringComparer.Ordinal);
        var unavailable = seed.VisibleCalendarIds.Where(id => !availableIds.Contains(id)).ToArray();
        Field("_Included calendars (select each one)", membership);
        if (unavailable.Length > 0)
            panel.Children.Add(new TextBlock { Text = $"{unavailable.Length:N0} unavailable calendars will stay in this set.", Foreground = NativeTheme.Muted, TextWrapping = TextWrapping.Wrap });
        var defaults = new ComboBox { DisplayMemberPath = nameof(DefaultChoice.Name) };
        void RefreshDefaults(string? preferred)
        {
            var picked = membership.SelectedItems.OfType<CalendarDefinition>().Select(calendar => calendar.Id).ToHashSet(StringComparer.Ordinal);
            var choices = new List<DefaultChoice> { new("First visible local calendar", null) };
            choices.AddRange(local.Where(calendar => picked.Contains(calendar.Id)).Select(calendar => new DefaultChoice(calendar.Name, calendar.Id)));
            defaults.ItemsSource = choices;
            defaults.SelectedItem = choices.FirstOrDefault(choice => choice.Id == preferred) ?? choices[0];
        }
        RefreshDefaults(seed.DefaultCalendarId);
        membership.SelectionChanged += (_, _) => RefreshDefaults((defaults.SelectedItem as DefaultChoice)?.Id);
        Field("_Default for new events", defaults);
        var tasks = new CheckBox { Content = "Show due _tasks in Agenda", IsChecked = seed.ShowTasks, Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(tasks);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Error, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 6, 0) });
        var save = new Button { Content = "_Save set", Style = NativeTheme.Style("PrimaryButton"), IsDefault = true };
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        save.Click += (_, _) =>
        {
            var set = seed with
            {
                Name = name.Text.Trim(), VisibleCalendarIds = membership.SelectedItems.OfType<CalendarDefinition>().Select(item => item.Id).Concat(unavailable).ToArray(),
                DefaultCalendarId = (defaults.SelectedItem as DefaultChoice)?.Id, ShowTasks = tasks.IsChecked == true
            };
            try
            {
                if (usedNames.Contains(set.Name, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a different name for this set.");
                new CalendarViewSettings([set], []).Validate();
                Result = set; DialogResult = true;
            }
            catch (InvalidDataException exception) { error.Text = exception.Message; }
        };
        Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
