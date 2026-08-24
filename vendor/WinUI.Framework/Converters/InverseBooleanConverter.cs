using Microsoft.UI.Xaml.Data;

namespace WinUI.Framework.Converters;

/// <summary>Inverts a <see cref="bool"/> value. Useful for disabled/enabled logic.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;
}
