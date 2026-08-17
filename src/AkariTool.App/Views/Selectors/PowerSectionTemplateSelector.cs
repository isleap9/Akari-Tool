using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.Views.Selectors;

/// <summary>
/// Picks the template for a Power-page display item: the bespoke Plan Selector +
/// Persist Indicator card for <see cref="PowerPlanSectionViewModel"/>, or the shared
/// tweak-section card for a <see cref="TweakSectionViewModel"/>. Lets the bespoke
/// section render at the TOP of the same ItemsControl, above the catalog sections
/// (net8's PowerTab.Build order).
/// </summary>
public sealed partial class PowerSectionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SectionTemplate { get; set; }        // shared TweakSectionTemplate
    public DataTemplate? PlanSelectorTemplate { get; set; }   // bespoke, in PowerPage.xaml

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        PowerPlanSectionViewModel => PlanSelectorTemplate,
        TweakSectionViewModel => SectionTemplate,
        _ => SectionTemplate,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
