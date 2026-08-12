using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinUI.Framework.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to <see cref="Visibility"/>.
/// Set <see cref="Invert"/> to true for the inverse mapping.
/// Use in XAML: <c>&lt;converters:BooleanToVisibilityConverter x:Key="BoolToVis" /&gt;</c>
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>When true, Visible maps to false (i.e. collapse when the value is true).</summary>
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        if (Invert)
        {
            visible = !visible;
        }

        return visible;
    }
}
