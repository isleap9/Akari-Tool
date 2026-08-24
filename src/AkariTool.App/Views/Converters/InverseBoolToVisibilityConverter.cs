using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariTool.Views.Converters;

/// <summary>true → Collapsed, false/incompatible → Visible (inverse of BoolToVis).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
