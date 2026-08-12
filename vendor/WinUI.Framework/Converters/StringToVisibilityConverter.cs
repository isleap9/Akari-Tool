using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinUI.Framework.Converters;

/// <summary>
/// Collapses the target when the value is null or an empty string.
/// Useful to hide placeholder text that has not been set yet.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
