using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI.Framework.Controls;

/// <summary>
/// A reusable page header showing a <see cref="Title"/> and an optional
/// <see cref="Subtitle"/>. Demonstrates a custom control with
/// <see cref="DependencyProperty"/> bindings inside the control template.
/// Usage: <c>&lt;controls:PageHeader Title="{x:Bind ViewModel.Title}" Subtitle="..." /&gt;</c>
/// </summary>
public sealed partial class PageHeader : UserControl
{
    public PageHeader()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the main heading text.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(PageHeader),
        new PropertyMetadata(string.Empty));

    /// <summary>Gets or sets the secondary descriptive text.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Identifies the <see cref="Subtitle"/> dependency property.</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(PageHeader),
        new PropertyMetadata(string.Empty));
}
