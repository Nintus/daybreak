using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Daymark.Windows;

internal enum AppThemeMode { System, Light, Dark }

internal static class NativeTheme
{
    private sealed class Swatch : System.ComponentModel.INotifyPropertyChanged
    {
        private Color value;
        public Color Value { get => value; set { this.value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private static readonly Dictionary<string, Swatch> swatches = new();
    private static readonly Dictionary<string, SolidColorBrush> brushes = new();
    private static string settingsPath = "";
    public static AppThemeMode Mode { get; private set; } = AppThemeMode.System;
    public static bool IsDark { get; private set; }
    public static event Action? Changed;
    private static Brush Get(string key)
    {
        if (brushes.TryGetValue(key, out var brush)) return brush;
        var swatch = new Swatch(); swatches[key] = swatch;
        brush = new SolidColorBrush();
        // Binding prevents WPF templates from freezing the shared palette brushes.
        System.Windows.Data.BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty,
            new System.Windows.Data.Binding(nameof(Swatch.Value)) { Source = swatch });
        brushes[key] = brush; return brush;
    }
    public static Brush Background => Get("Background");
    public static Brush Error => Get("Error");
    public static Brush OnAccent => Get("OnAccent");
    public static Brush Surface => Get("Surface");
    public static Brush Ink => Get("Ink");
    public static Brush Muted => Get("Muted");
    public static Brush Line => Get("Line");
    public static Brush Accent => Get("Accent");
    public static Brush AccentInk => Get("AccentInk");
    public static Brush Soft => Get("Soft");
    public static Brush Subtle => Get("Subtle");
    public static Brush Teal => Get("Teal");
    public static Brush TealSoft => Get("TealSoft");
    public static Style? Style(string key) => Application.Current.TryFindResource(key) as Style;

    public static void Apply(Application app, string path)
    {
        settingsPath = path;
        try { if (System.IO.File.Exists(path) && Enum.TryParse<AppThemeMode>(System.IO.File.ReadAllText(path).Trim(), out var mode) && Enum.IsDefined(mode)) Mode = mode; }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { /* A preference cannot prevent opening the calendar. */ }
        Refresh();
        if (!SystemParameters.HighContrast) app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Resources));
        Microsoft.Win32.UserPreferenceChangedEventHandler onPreferenceChanged = (_, _) =>
        {
            if (app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(() => { if (Mode == AppThemeMode.System) Refresh(); });
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += onPreferenceChanged;
        app.Exit += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= onPreferenceChanged;
    }
    public static void Select(AppThemeMode mode)
    {
        // Persist before changing the UI, so a failed write leaves the preference intact.
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath)!);
        var temporary = settingsPath + ".tmp";
        System.IO.File.WriteAllText(temporary, mode.ToString());
        System.IO.File.Move(temporary, settingsPath, true);
        Mode = mode; Refresh();
    }
    public static void Refresh()
    {
        var dark = Mode == AppThemeMode.Dark || Mode == AppThemeMode.System && SystemIsDark();
        IsDark = dark && !SystemParameters.HighContrast;
        var colors = new Dictionary<string, (string Light, string Dark)>
        {
            ["Error"] = ("#B42318", "#FF9C96"), ["OnAccent"] = ("#FFFFFF", "#10151F"), ["Background"] = ("#F4F6FA", "#10151F"), ["Surface"] = ("#FFFFFF", "#191F2C"),
            ["Ink"] = ("#182338", "#E9EEF7"), ["Muted"] = ("#5D6C82", "#A6B3C8"),
            ["Line"] = ("#E5EAF1", "#354052"), ["Accent"] = ("#2563EB", "#78A7FF"),
            ["AccentInk"] = ("#2454A5", "#B5D0FF"), ["Soft"] = ("#EDF4FF", "#233752"),
            ["Subtle"] = ("#F8FAFD", "#1D2635"), ["Teal"] = ("#167269", "#83D8C8"),
            ["TealSoft"] = ("#EAF6F2", "#193D3B"), ["Hover"] = ("#F1F5FB", "#2A3548"),
            ["Pressed"] = ("#E5EDF8", "#354660"), ["SelectedBorder"] = ("#99B9EE", "#78A7FF")
        };
        foreach (var (key, pair) in colors)
        {
            var brush = Get(key);
            var color = (Color)ColorConverter.ConvertFromString(IsDark ? pair.Dark : pair.Light);
            if (SystemParameters.HighContrast) color = key is "Error" or "Ink" or "Muted" or "Line" or "AccentInk" or "Teal" ? SystemColors.WindowTextColor
                : key == "OnAccent" ? SystemColors.HighlightTextColor : key is "Accent" or "SelectedBorder" ? SystemColors.HighlightColor : SystemColors.WindowColor;
            swatches[key].Value = color;
            Application.Current.Resources["Theme" + key] = brush;
        }
        Changed?.Invoke();
    }
    public static void Attach(Window window)
    {
        void UpdateTitleBar()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var dark = IsDark ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        }
        window.SourceInitialized += (_, _) => UpdateTitleBar();
        Changed += UpdateTitleBar;
        window.Closed += (_, _) => Changed -= UpdateTitleBar;
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private static bool SystemIsDark()
    {
        try { return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
        catch (Exception e) when (e is System.Security.SecurityException or System.IO.IOException or UnauthorizedAccessException) { return false; }
    }

    private const string Resources = """
    <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:p="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework">


      <Style TargetType="Button">
        <Setter Property="Background" Value="{DynamicResource ThemeSurface}"/><Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource ThemeLine}"/><Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="12,7"/><Setter Property="FontSize" Value="13"/>
        <Setter Property="HorizontalContentAlignment" Value="Center"/><Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource ThemeHover}"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource ThemePressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Chrome" Property="BorderThickness" Value="2"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.42"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Style.Resources><SolidColorBrush x:Key="ThemeHover" Color="#1D4ED8"/><SolidColorBrush x:Key="ThemePressed" Color="#1E40AF"/></Style.Resources>
        <Setter Property="Background" Value="#2563EB"/><Setter Property="BorderBrush" Value="#2563EB"/><Setter Property="Foreground" Value="White"/><Setter Property="FontWeight" Value="SemiBold"/>
      </Style>
      <Style x:Key="CalendarDay" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
          <Border x:Name="Cell" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Stretch" VerticalAlignment="Stretch"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Cell" Property="Background" Value="{DynamicResource ThemeHover}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Cell" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Cell" Property="BorderThickness" Value="2"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style x:Key="CalendarEvent" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="{DynamicResource ThemeSoft}"/><Setter Property="Foreground" Value="{DynamicResource ThemeAccentInk}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource ThemeAccent}"/><Setter Property="BorderThickness" Value="3,0,0,0"/>
        <Setter Property="Padding" Value="7,4"/><Setter Property="FontSize" Value="12"/>
        <Setter Property="HorizontalContentAlignment" Value="Left"/><Setter Property="VerticalContentAlignment" Value="Top"/>
      </Style>
      <Style x:Key="ViewSegment" TargetType="RadioButton">
        <Setter Property="Foreground" Value="{DynamicResource ThemeMuted}"/><Setter Property="FontSize" Value="13"/><Setter Property="Padding" Value="13,7"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
          <Border x:Name="Segment" CornerRadius="7" BorderThickness="1" BorderBrush="Transparent" Background="Transparent">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Segment" Property="Background" Value="{DynamicResource ThemeHover}"/></Trigger>
            <Trigger Property="IsChecked" Value="True"><Setter TargetName="Segment" Property="Background" Value="{DynamicResource ThemeSurface}"/><Setter TargetName="Segment" Property="BorderBrush" Value="{DynamicResource ThemeLine}"/><Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="FontWeight" Value="SemiBold"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Segment" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Segment" Property="BorderThickness" Value="2"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="TextBox">
        <Setter Property="Background" Value="{DynamicResource ThemeSurface}"/><Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="BorderBrush" Value="{DynamicResource ThemeLine}"/><Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,7"/><Setter Property="FontSize" Value="13"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
          <Border x:Name="Field" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <Grid><ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" Focusable="False" VerticalScrollBarVisibility="{TemplateBinding VerticalScrollBarVisibility}" HorizontalScrollBarVisibility="{TemplateBinding HorizontalScrollBarVisibility}"/>
              <TextBlock x:Name="Hint" Text="{TemplateBinding Tag}" Foreground="{DynamicResource ThemeMuted}" Margin="{TemplateBinding Padding}" IsHitTestVisible="False" Visibility="Collapsed"/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="Text" Value=""><Setter TargetName="Hint" Property="Visibility" Value="Visible"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Field" Property="BorderBrush" Value="#2563EB"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>

      <Style x:Key="AgendaItem" TargetType="ListBoxItem">
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Padding" Value="12"/><Setter Property="Margin" Value="0,0,0,9"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem">
          <Border x:Name="Card" CornerRadius="10" Background="{DynamicResource ThemeSubtle}" BorderBrush="{DynamicResource ThemeLine}" BorderThickness="1" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Card" Property="Background" Value="{DynamicResource ThemeHover}"/></Trigger>
            <Trigger Property="IsSelected" Value="True"><Setter TargetName="Card" Property="Background" Value="{DynamicResource ThemeSoft}"/><Setter TargetName="Card" Property="BorderBrush" Value="{DynamicResource ThemeSelectedBorder}"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Card" Property="BorderBrush" Value="#2563EB"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>

      <Style TargetType="ComboBoxItem">
        <Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="Background" Value="{DynamicResource ThemeSurface}"/><Setter Property="Padding" Value="10,7"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="Row" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border>
          <ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Row" Property="Background" Value="{DynamicResource ThemeSoft}"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter Property="FontWeight" Value="SemiBold"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="ComboBox">
        <Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="Background" Value="{DynamicResource ThemeSurface}"/><Setter Property="BorderBrush" Value="{DynamicResource ThemeLine}"/><Setter Property="FontSize" Value="12"/><Setter Property="MinHeight" Value="34"/>
        <Setter Property="ScrollViewer.CanContentScroll" Value="True"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
          <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
            <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border x:Name="Chrome" CornerRadius="7" BorderThickness="1" BorderBrush="{DynamicResource ThemeLine}" Background="{DynamicResource ThemeSurface}"><Grid><TextBlock Text="⌄" Foreground="{DynamicResource ThemeMuted}" HorizontalAlignment="Right" Margin="0,0,10,0" VerticalAlignment="Center"/></Grid></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource ThemeHover}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}" Margin="10,6,30,6" VerticalAlignment="Center" IsHitTestVisible="False"/>
          <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False"><Border MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="{TemplateBinding MaxDropDownHeight}" Background="{DynamicResource ThemeSurface}" BorderBrush="{DynamicResource ThemeLine}" BorderThickness="1" CornerRadius="7" Padding="3"><ScrollViewer CanContentScroll="True" VerticalScrollBarVisibility="Auto"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup>
        </Grid><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter Property="BorderBrush" Value="{DynamicResource ThemeAccent}"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="MenuItem">
        <Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="Padding" Value="9,6"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="MenuItem"><Grid>
          <Border x:Name="Row" Background="Transparent" CornerRadius="5" Padding="{TemplateBinding Padding}"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
            <TextBlock x:Name="Check" Text="✓" Width="22" Visibility="Collapsed" Foreground="{DynamicResource ThemeAccent}"/>
            <ContentPresenter Grid.Column="1" ContentSource="Header" RecognizesAccessKey="True"/>
            <TextBlock Grid.Column="2" Text="{TemplateBinding InputGestureText}" Foreground="{DynamicResource ThemeMuted}" Margin="24,0,0,0"/>
          </Grid></Border>
          <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsSubmenuOpen}" Placement="Right" AllowsTransparency="True" Focusable="False"><Border Background="{DynamicResource ThemeSurface}" BorderBrush="{DynamicResource ThemeLine}" BorderThickness="1" CornerRadius="8" Padding="5" MinWidth="170"><StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Cycle"/></Border></Popup>
        </Grid><ControlTemplate.Triggers>
          <Trigger Property="Role" Value="TopLevelHeader"><Setter TargetName="PART_Popup" Property="Placement" Value="Bottom"/></Trigger>
          <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Row" Property="Background" Value="{DynamicResource ThemeHover}"/></Trigger>
          <Trigger Property="IsSubmenuOpen" Value="True"><Setter TargetName="Row" Property="Background" Value="{DynamicResource ThemeSoft}"/></Trigger>
          <Trigger Property="IsCheckable" Value="True"><Setter TargetName="Check" Property="Visibility" Value="Visible"/><Setter TargetName="Check" Property="Opacity" Value="0"/></Trigger>
          <Trigger Property="IsChecked" Value="True"><Setter TargetName="Check" Property="Opacity" Value="1"/></Trigger>
          <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
        </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="Separator"><Setter Property="Margin" Value="6,4"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator"><Border Height="1" Background="{DynamicResource ThemeLine}"/></ControlTemplate></Setter.Value></Setter></Style>
      <Style x:Key="DateField" TargetType="DatePickerTextBox" BasedOn="{StaticResource {x:Type TextBox}}"/>
      <Style x:Key="PopupDay" TargetType="p:CalendarDayButton" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Padding" Value="4"/><Setter Property="Margin" Value="1"/><Setter Property="BorderThickness" Value="0"/><Setter Property="MinWidth" Value="30"/><Setter Property="MinHeight" Value="28"/>
        <Style.Triggers>
          <Trigger Property="IsInactive" Value="True"><Setter Property="Foreground" Value="{DynamicResource ThemeMuted}"/></Trigger>
          <Trigger Property="IsToday" Value="True"><Setter Property="BorderThickness" Value="1"/><Setter Property="BorderBrush" Value="{DynamicResource ThemeAccent}"/></Trigger>
          <Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="{DynamicResource ThemeSoft}"/><Setter Property="Foreground" Value="{DynamicResource ThemeAccentInk}"/><Setter Property="FontWeight" Value="Bold"/></Trigger>
          <Trigger Property="IsBlackedOut" Value="True"><Setter Property="Opacity" Value="0.4"/></Trigger>
        </Style.Triggers>
      </Style>
      <Style x:Key="PopupMonth" TargetType="p:CalendarButton" BasedOn="{StaticResource {x:Type Button}}"><Setter Property="Margin" Value="2"/><Setter Property="Padding" Value="6"/>
        <Style.Triggers><Trigger Property="IsInactive" Value="True"><Setter Property="Foreground" Value="{DynamicResource ThemeMuted}"/></Trigger><Trigger Property="HasSelectedDays" Value="True"><Setter Property="Background" Value="{DynamicResource ThemeSoft}"/></Trigger></Style.Triggers>
      </Style>
      <Style x:Key="PopupCalendarItem" TargetType="p:CalendarItem">
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="p:CalendarItem"><ControlTemplate.Resources><DataTemplate x:Key="{x:Static p:CalendarItem.DayTitleTemplateResourceKey}"><TextBlock Text="{Binding}" Foreground="{DynamicResource ThemeMuted}" HorizontalAlignment="Center" FontSize="11" Margin="0,6"/></DataTemplate></ControlTemplate.Resources>
          <Border Background="{DynamicResource ThemeSurface}" BorderBrush="{DynamicResource ThemeLine}" BorderThickness="1" CornerRadius="10" Padding="10">
            <Grid x:Name="PART_Root" Width="266">
              <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition/></Grid.RowDefinitions>
              <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="34"/><ColumnDefinition/><ColumnDefinition Width="34"/></Grid.ColumnDefinitions><Button x:Name="PART_PreviousButton" Content="‹" Padding="5"/><Button x:Name="PART_HeaderButton" Grid.Column="1" Padding="5" FontWeight="SemiBold"/><Button x:Name="PART_NextButton" Grid.Column="2" Content="›" Padding="5"/></Grid>
              <Grid x:Name="PART_MonthView" Grid.Row="1"><Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions></Grid>
              <Grid x:Name="PART_YearView" Grid.Row="1" Height="210" Visibility="Collapsed"><Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions></Grid>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <DataTrigger Binding="{Binding DisplayMode, RelativeSource={RelativeSource AncestorType=Calendar}}" Value="Year"><Setter TargetName="PART_MonthView" Property="Visibility" Value="Collapsed"/><Setter TargetName="PART_YearView" Property="Visibility" Value="Visible"/></DataTrigger>
            <DataTrigger Binding="{Binding DisplayMode, RelativeSource={RelativeSource AncestorType=Calendar}}" Value="Decade"><Setter TargetName="PART_MonthView" Property="Visibility" Value="Collapsed"/><Setter TargetName="PART_YearView" Property="Visibility" Value="Visible"/></DataTrigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style x:Key="PopupCalendar" TargetType="Calendar">
        <Setter Property="CalendarDayButtonStyle" Value="{StaticResource PopupDay}"/><Setter Property="CalendarButtonStyle" Value="{StaticResource PopupMonth}"/><Setter Property="CalendarItemStyle" Value="{StaticResource PopupCalendarItem}"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Calendar"><StackPanel x:Name="PART_Root"><p:CalendarItem x:Name="PART_CalendarItem" Style="{TemplateBinding CalendarItemStyle}"/></StackPanel></ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="DatePicker">
        <Setter Property="FirstDayOfWeek" Value="Monday"/>
        <Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="CalendarStyle" Value="{StaticResource PopupCalendar}"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="DatePicker"><Grid x:Name="PART_Root"><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="36"/></Grid.ColumnDefinitions>
          <p:DatePickerTextBox x:Name="PART_TextBox" Style="{StaticResource DateField}"/>
          <Button x:Name="PART_Button" Grid.Column="1" Margin="4,0,0,0" Padding="4" Focusable="False" AutomationProperties.Name="Show calendar"><TextBlock Text="&#xE787;" FontFamily="Segoe MDL2 Assets"/></Button>
          <Popup x:Name="PART_Popup" Placement="Bottom" StaysOpen="False" AllowsTransparency="True"/>
        </Grid></ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="Label"><Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/></Style>
      <Style TargetType="CheckBox"><Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/></Style>
      <Style TargetType="ToolTip"><Setter Property="Background" Value="{DynamicResource ThemeSurface}"/><Setter Property="Foreground" Value="{DynamicResource ThemeInk}"/><Setter Property="BorderBrush" Value="{DynamicResource ThemeLine}"/></Style>

      <Style x:Key="ScrollPage" TargetType="RepeatButton"><Setter Property="Focusable" Value="False"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></Setter.Value></Setter></Style>
      <Style TargetType="ScrollBar">
        <Setter Property="Background" Value="{DynamicResource ThemeSubtle}"/><Setter Property="Width" Value="12"/><Setter Property="MinWidth" Value="0"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="{TemplateBinding Background}">
          <Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="{TemplateBinding Orientation}" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" ViewportSize="{TemplateBinding ViewportSize}">
            <Track.DecreaseRepeatButton><RepeatButton x:Name="Decrease" Command="ScrollBar.PageUpCommand" Style="{StaticResource ScrollPage}"/></Track.DecreaseRepeatButton>
            <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border x:Name="Grip" CornerRadius="4" Margin="3" Background="{DynamicResource ThemeMuted}" Opacity="0.6"/><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Grip" Property="Opacity" Value="1"/></Trigger><Trigger Property="IsDragging" Value="True"><Setter TargetName="Grip" Property="Background" Value="{DynamicResource ThemeAccent}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
            <Track.IncreaseRepeatButton><RepeatButton x:Name="Increase" Command="ScrollBar.PageDownCommand" Style="{StaticResource ScrollPage}"/></Track.IncreaseRepeatButton>
          </Track>
        </Grid><ControlTemplate.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/><Setter TargetName="Decrease" Property="Command" Value="ScrollBar.PageLeftCommand"/><Setter TargetName="Increase" Property="Command" Value="ScrollBar.PageRightCommand"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        <Style.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter Property="Width" Value="Auto"/><Setter Property="Height" Value="12"/></Trigger></Style.Triggers>
      </Style>
    </ResourceDictionary>
    """;
}
