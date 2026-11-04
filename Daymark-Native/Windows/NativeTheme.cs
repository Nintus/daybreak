using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Daymark.Windows;

internal static class NativeTheme
{
    private static SolidColorBrush Brush(string value) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!; brush.Freeze(); return brush; }
    public static Brush Background { get; } = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush("#F4F6FA");
    public static Brush Surface { get; } = SystemColors.WindowBrush;
    public static Brush Ink { get; } = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brush("#182338");
    public static Brush Muted { get; } = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brush("#5D6C82");
    public static Brush Line { get; } = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brush("#E5EAF1");
    public static Brush Accent { get; } = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brush("#2563EB");
    public static Brush AccentInk { get; } = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brush("#2454A5");
    public static Brush Soft { get; } = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush("#EDF4FF");
    public static Brush Subtle { get; } = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush("#F8FAFD");
    public static Brush Teal { get; } = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brush("#167269");
    public static Brush TealSoft { get; } = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush("#EAF6F2");
    public static Style? Style(string key) => Application.Current.TryFindResource(key) as Style;

    public static void Apply(Application app)
    {
        // Retain the system's own control templates and colors in high contrast.
        if (SystemParameters.HighContrast) return;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Resources));
    }

    private const string Resources = """
    <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <SolidColorBrush x:Key="ButtonHover" Color="#F1F5FB"/>
      <SolidColorBrush x:Key="ButtonPressed" Color="#E5EDF8"/>
      <Style TargetType="Button">
        <Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="#26354B"/>
        <Setter Property="BorderBrush" Value="#DCE3ED"/><Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="12,7"/><Setter Property="FontSize" Value="13"/>
        <Setter Property="HorizontalContentAlignment" Value="Center"/><Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource ButtonHover}"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource ButtonPressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Chrome" Property="BorderThickness" Value="2"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.42"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Style.Resources><SolidColorBrush x:Key="ButtonHover" Color="#1D4ED8"/><SolidColorBrush x:Key="ButtonPressed" Color="#1E40AF"/></Style.Resources>
        <Setter Property="Background" Value="#2563EB"/><Setter Property="BorderBrush" Value="#2563EB"/><Setter Property="Foreground" Value="White"/><Setter Property="FontWeight" Value="SemiBold"/>
      </Style>
      <Style x:Key="CalendarDay" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
          <Border x:Name="Cell" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Stretch" VerticalAlignment="Stretch"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Cell" Property="Background" Value="#F2F6FD"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Cell" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Cell" Property="BorderThickness" Value="2"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style x:Key="CalendarEvent" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="#EAF2FF"/><Setter Property="Foreground" Value="#2454A5"/>
        <Setter Property="BorderBrush" Value="#6697EF"/><Setter Property="BorderThickness" Value="3,0,0,0"/>
        <Setter Property="Padding" Value="7,4"/><Setter Property="FontSize" Value="12"/>
        <Setter Property="HorizontalContentAlignment" Value="Left"/><Setter Property="VerticalContentAlignment" Value="Top"/>
      </Style>
      <Style x:Key="ViewSegment" TargetType="RadioButton">
        <Setter Property="Foreground" Value="#5D6C82"/><Setter Property="FontSize" Value="13"/><Setter Property="Padding" Value="13,7"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
          <Border x:Name="Segment" CornerRadius="7" BorderThickness="1" BorderBrush="Transparent" Background="Transparent">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Segment" Property="Background" Value="#F9FBFE"/></Trigger>
            <Trigger Property="IsChecked" Value="True"><Setter TargetName="Segment" Property="Background" Value="White"/><Setter TargetName="Segment" Property="BorderBrush" Value="#DCE3ED"/><Setter Property="Foreground" Value="#182338"/><Setter Property="FontWeight" Value="SemiBold"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Segment" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Segment" Property="BorderThickness" Value="2"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="TextBox">
        <Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="#182338"/><Setter Property="BorderBrush" Value="#DCE3ED"/><Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,7"/><Setter Property="FontSize" Value="13"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
          <Border x:Name="Field" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <Grid><ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" Focusable="False" VerticalScrollBarVisibility="{TemplateBinding VerticalScrollBarVisibility}" HorizontalScrollBarVisibility="{TemplateBinding HorizontalScrollBarVisibility}"/>
              <TextBlock x:Name="Hint" Text="{TemplateBinding Tag}" Foreground="#68758A" Margin="{TemplateBinding Padding}" IsHitTestVisible="False" Visibility="Collapsed"/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="Text" Value=""><Setter TargetName="Hint" Property="Visibility" Value="Visible"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Field" Property="BorderBrush" Value="#2563EB"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="ComboBox"><Setter Property="FontSize" Value="12"/><Setter Property="Padding" Value="8,5"/><Setter Property="Foreground" Value="#344359"/><Setter Property="MinHeight" Value="30"/></Style>
      <Style x:Key="AgendaItem" TargetType="ListBoxItem">
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Padding" Value="12"/><Setter Property="Margin" Value="0,0,0,9"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem">
          <Border x:Name="Card" CornerRadius="10" Background="#F7F9FC" BorderBrush="#ECF0F5" BorderThickness="1" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Card" Property="Background" Value="#EFF4FD"/></Trigger>
            <Trigger Property="IsSelected" Value="True"><Setter TargetName="Card" Property="Background" Value="#EAF2FF"/><Setter TargetName="Card" Property="BorderBrush" Value="#99B9EE"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Card" Property="BorderBrush" Value="#2563EB"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
    </ResourceDictionary>
    """;
}
