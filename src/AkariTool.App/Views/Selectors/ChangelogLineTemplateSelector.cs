using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels;

namespace AkariTool.Views.Selectors;

/// <summary>
/// Picks a changelog line's DataTemplate by kind (header / bullet / paragraph), so the
/// Settings "What's new" card renders GitHub markdown as scannable blocks instead of one
/// flat paragraph. Structured parsing happens in <see cref="SettingsViewModel.ParseChangelog"/>;
/// this only maps the already-classified <see cref="ChangelogLine.Kind"/> to a template.
/// </summary>
public sealed partial class ChangelogLineTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? BulletTemplate { get; set; }
    public DataTemplate? ParagraphTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not ChangelogLine line) return ParagraphTemplate;
        return line.Kind switch
        {
            ChangelogLineKind.Header => HeaderTemplate,
            ChangelogLineKind.Bullet => BulletTemplate,
            _ => ParagraphTemplate,
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
