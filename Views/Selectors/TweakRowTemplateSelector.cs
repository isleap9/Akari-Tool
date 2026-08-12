using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.Views.Selectors;

/// <summary>
/// Picks the row DataTemplate by input kind. This is the MVVM replacement for the
/// if/else inside net8's AddTweakRow — one render path, two leaf templates, so
/// every tab that reuses the rendering layer stays visually identical for free
/// (the leverage the imperative factory had).
/// </summary>
public sealed partial class TweakRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? DropdownTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        DropdownTweakViewModel => DropdownTemplate,
        ToggleTweakViewModel => ToggleTemplate,
        _ => ToggleTemplate,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}

/// <summary>
/// Picks the badge pill template by badge kind. Each kind gets its own template so
/// its colours are plain {ThemeResource} references that re-resolve live on a
/// theme switch — a converter returning Brush objects would not.
/// </summary>
public sealed partial class TweakBadgeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }
    public DataTemplate? PreferenceTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not TweakBadgeViewModel badge) return CustomTemplate;
        return badge.Kind switch
        {
            TweakBadgeKind.Recommended => RecommendedTemplate,
            TweakBadgeKind.Default => DefaultTemplate,
            TweakBadgeKind.Preference => PreferenceTemplate,
            _ => CustomTemplate,
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
