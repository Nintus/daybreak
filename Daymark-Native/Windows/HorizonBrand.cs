using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Daymark.Windows;

internal static class HorizonBrand
{
    public const string Primary = "#7C83FF", Apricot = "#F3B785";
    private static readonly Geometry Mark = CreateMark();
    public static readonly FontFamily Wordmark = new(new Uri("pack://application:,,,/"), "./Daymark;component/Assets/#Manrope SemiBold");
    private static Geometry CreateMark()
    {
        var outer = Geometry.Parse("M6,4 H28 C65,4 65,60 28,60 H6 Z");
        var sun = Geometry.Parse("M14,34 A18,18 0 0 1 50,34 Z");
        var cuts = new GeometryGroup(); cuts.Children.Add(sun); cuts.Children.Add(new RectangleGeometry(new Rect(6, 34, 52, 2)));
        var mark = new CombinedGeometry(GeometryCombineMode.Exclude, outer, cuts); mark.Freeze(); return mark;
    }
    public static FrameworkElement Logo(double size) => new Viewbox { Width = size, Height = size,
        Child = new Canvas { Width = 64, Height = 64, Children = { new System.Windows.Shapes.Path { Data = Mark, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Primary)) } } } };
    public static ImageSource Icon => BitmapFrame.Create(new Uri("pack://application:,,,/Daymark;component/Assets/Daymark.ico"));
}
