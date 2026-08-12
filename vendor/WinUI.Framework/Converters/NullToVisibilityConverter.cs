using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinUI.Framework.Converters;

/// <summary>
/// Collapses the target when the value is null.
/// Pass a non-empty parameter (e.g. "invert") to collapse when the value is NOT null instead.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNull = value is null;
        if (parameter is string s && !string.IsNullOrEmpty(s))
        {
            isNull = !isNull;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
