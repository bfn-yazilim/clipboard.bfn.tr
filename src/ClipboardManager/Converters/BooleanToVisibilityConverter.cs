using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipboardManager.Converters;

/// <summary>
/// bool -> Visibility. WPF'in yerlesik converter'i ile ayni; farkli isim uzayinda
/// tutarak bagimliligi azaltir ve tersine cevirme parametresi desteğini basitlestirir.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}
