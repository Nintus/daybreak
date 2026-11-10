using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;

namespace Daymark.Windows;

internal sealed class TaskEditor : Window
{
    public CalendarTask? Result { get; private set; }

    public TaskEditor(CalendarTask? task, DateOnly today)
    {
        Title = task is null ? "New task" : "Edit task";
        Width = 490;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Surface;
        Foreground = NativeTheme.Ink;
        FontSize = 14;
        NativeTheme.Attach(this);

        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = Title, FontSize = 23, FontWeight = FontWeights.SemiBold });
        void Field(string label, Control field)
        {
            panel.Children.Add(new Label { Content = label, Target = field, Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(field);
        }

        var title = new TextBox { Text = task?.Title ?? "", MaxLength = 300 };
        Field("_Title", title);
        var due = new DatePicker
        {
            SelectedDate = task?.DueDate?.ToDateTime(TimeOnly.MinValue), DisplayDate = (task?.DueDate ?? today).ToDateTime(TimeOnly.MinValue),
            DisplayDateStart = new DateTime(2, 1, 1), DisplayDateEnd = new DateTime(9998, 12, 31)
        };
        Field("_Due date (optional)", due);
        var invalidDate = false;
        var error = new TextBlock { Foreground = NativeTheme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        due.SelectedDateChanged += (_, _) => { invalidDate = false; error.Text = ""; };
        due.DateValidationError += (_, args) =>
        {
            args.ThrowException = false;
            invalidDate = true;
            error.Text = "Choose a valid due date or clear it.";
        };
        var clear = new Button { Content = "Clear due date", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) };
        clear.Click += (_, _) => { due.SelectedDate = null; due.Text = ""; invalidDate = false; error.Text = ""; };
        panel.Children.Add(clear);
        var important = new CheckBox { Content = "Mark as _important", IsChecked = task?.IsImportant == true, Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(important);
        var notes = new TextBox { Text = task?.Notes ?? "", MaxLength = 10000, Height = 88, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Field("_Notes", notes);
        panel.Children.Add(error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var save = new Button { Content = "_Save task", IsDefault = true, Style = NativeTheme.Style("PrimaryButton") };
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        save.Click += (_, _) =>
        {
            if (invalidDate) return;
            var value = (task ?? new CalendarTask(Guid.NewGuid(), "")) with
            {
                Title = title.Text.Trim(), DueDate = due.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null,
                IsImportant = important.IsChecked == true, Notes = notes.Text
            };
            try { value.Validate(); Result = value; DialogResult = true; }
            catch (InvalidDataException exception) { error.Text = exception.Message; }
        };
        Loaded += (_, _) => { title.Focus(); title.SelectAll(); };
    }
}
