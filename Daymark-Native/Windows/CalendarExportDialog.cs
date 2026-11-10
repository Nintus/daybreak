using System.IO;
using System.Windows;
using System.Windows.Controls;
using Daymark.Core;
using Daymark.Migration;
using Microsoft.Win32;

namespace Daymark.Windows;

internal sealed class CalendarExportDialog : Window
{
    public int ExportedCount { get; private set; }

    public CalendarExportDialog(CalendarDocument document, DateOnly selected, TimeZoneInfo displayZone)
    {
        Title = "Export calendar";
        Width = 480;
        FontSize = 14;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = NativeTheme.Background;
        Foreground = NativeTheme.Ink;
        NativeTheme.Attach(this);

        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = panel;
        panel.Children.Add(new TextBlock
        {
            Text = "Export an iCalendar file", FontSize = 22, FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Choose one local calendar and a date range. Repeating events are saved as individual occurrences.",
            TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 14)
        });

        void Field(string label, Control control, Panel parent)
        {
            parent.Children.Add(new Label { Content = label, Target = control, Padding = new Thickness(0, 5, 0, 4) });
            parent.Children.Add(control);
        }

        var calendars = new ComboBox
        {
            ItemsSource = document.CalendarList, DisplayMemberPath = nameof(CalendarDefinition.Name), SelectedIndex = 0
        };
        Field("_Calendar", calendars, panel);
        var dates = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        dates.ColumnDefinitions.Add(new ColumnDefinition());
        dates.ColumnDefinitions.Add(new ColumnDefinition());
        var fromPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        var throughPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(throughPanel, 1);
        dates.Children.Add(fromPanel);
        dates.Children.Add(throughPanel);
        panel.Children.Add(dates);

        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Error };
        DatePicker Picker(int day)
        {
            var picker = new DatePicker
            {
                SelectedDate = new DateTime(selected.Year, selected.Month, day),
                DisplayDateStart = new DateTime(2, 1, 1),
                DisplayDateEnd = new DateTime(9998, 12, 31)
            };
            picker.DateValidationError += (_, args) =>
            {
                args.ThrowException = false;
                picker.SelectedDate = null;
                error.Text = "Choose a valid date.";
            };
            return picker;
        }
        var from = Picker(1);
        var through = Picker(DateTime.DaysInMonth(selected.Year, selected.Month));
        Field("_From", from, fromPanel);
        Field("_Through (inclusive)", through, throughPanel);
        panel.Children.Add(new TextBlock
        {
            Text = $"Date range uses {displayZone.DisplayName}.",
            TextWrapping = TextWrapping.Wrap, Foreground = NativeTheme.Muted, Margin = new Thickness(0, 8, 0, 8)
        });
        panel.Children.Add(error);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var export = new Button { Content = "_Export…", IsDefault = true, Style = NativeTheme.Style("PrimaryButton") };
        actions.Children.Add(cancel);
        actions.Children.Add(export);
        panel.Children.Add(actions);

        var saving = false;
        Closing += (_, args) => args.Cancel = saving;
        export.Click += async (_, _) =>
        {
            if (saving) return;
            error.Text = "";
            if (calendars.SelectedItem is not CalendarDefinition calendar ||
                from.SelectedDate is not { } first || through.SelectedDate is not { } last || last < first)
            {
                error.Text = "Choose a calendar and a valid date range.";
                return;
            }
            if ((last - first).Days >= IcalendarExporter.MaximumDays)
            {
                error.Text = $"Choose a range of up to {IcalendarExporter.MaximumDays:N0} days.";
                return;
            }

            var picker = new SaveFileDialog
            {
                FileName = "daymark-calendar.ics", DefaultExt = ".ics", Filter = "iCalendar file (*.ics)|*.ics"
            };
            if (picker.ShowDialog(this) != true) return;

            saving = true;
            panel.IsEnabled = false;
            export.Content = "Exporting…";
            try
            {
                ExportedCount = await Task.Run(() => IcalendarExporter.Save(document, calendar.Id,
                    DateOnly.FromDateTime(first), DateOnly.FromDateTime(last), displayZone, picker.FileName));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                error.Text = exception.Message;
            }
            finally
            {
                saving = false;
                panel.IsEnabled = true;
                export.Content = "_Export…";
            }
            if (ExportedCount > 0) DialogResult = true;
        };
    }
}
