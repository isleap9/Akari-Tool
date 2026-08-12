using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using AkariTool.Tabs;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// One selectable app card. MVVM port of net8 <c>SoftwareTab.Cards.BuildAppCard</c>
/// — the imperative Border/CheckBox/badge tree becomes bound state, the DataTemplate
/// draws it.
///
/// NOT a tweak row: this wraps an <see cref="AppDefinition"/> (the Software tab's own
/// identity space) and NEVER touches TweakRegistry. Selection lives on the underlying
/// definition, exactly as net8 did, so the service layer (which reads
/// <c>a.IsSelected</c>) works unchanged.
/// </summary>
public sealed partial class AppCardViewModel : ObservableObject
{
    public AppDefinition App { get; }

    /// <summary>True while the card passes the current search filter (net8 AppCard.Visible).</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// net8 <c>BuildAppCard(app, isWindowsApps)</c>. The flag exists solely to gate the
    /// "Permanent" pill, which net8 renders only on the Windows Apps panel. Defaults to
    /// false so the External Apps call site is unchanged.
    /// </summary>
    public AppCardViewModel(AppDefinition app, bool isWindowsApps = false)
    {
        App = app;
        _isWindowsApps = isWindowsApps;

        // net8 kept the checkbox/border in sync by hand after every mutation; here
        // the definition's own PropertyChanged drives the bound visuals instead.
        App.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(AppDefinition.IsSelected):
                    OnPropertyChanged(nameof(IsSelected));
                    OnPropertyChanged(nameof(BorderBrush));
                    break;
                case nameof(AppDefinition.IsInstalled):
                    OnPropertyChanged(nameof(IsInstalled));
                    break;
            }
        };
    }

    // ── Static content (verbatim from the definition) ─────────────────────────

    public string Name => App.Name;
    public string Description => App.Description;
    public string Letter => App.Name[0].ToString().ToUpperInvariant();
    public string? WebsiteUrl => App.WebsiteUrl;
    public bool HasWebsite => !string.IsNullOrEmpty(App.WebsiteUrl);

    private readonly bool _isWindowsApps;

    /// <summary>Warning pill — net8 renders it from <c>HasInstabilityWarning</c>.</summary>
    public bool HasWarning => App.HasInstabilityWarning;

    /// <summary>
    /// "Permanent" pill — net8's exact gate: <c>isWindowsApps &amp;&amp;
    /// !app.CanBeReinstalled</c>. Always false on the External Apps panel.
    /// </summary>
    public bool IsPermanent => _isWindowsApps && !App.CanBeReinstalled;

    // ── Live state ────────────────────────────────────────────────────────────

    public bool IsSelected
    {
        get => App.IsSelected;
        set => App.IsSelected = value;   // raises through the definition
    }

    public bool IsInstalled => App.IsInstalled;

    /// <summary>
    /// Hover highlight. net8 mutated <c>card.BorderBrush</c> directly from
    /// PointerEntered/Exited; a local value would permanently break the binding here,
    /// so hover is state on the VM and the brush is derived — same three-way result
    /// (selected &gt; hovered &gt; resting).
    /// </summary>
    [ObservableProperty] public partial bool IsHovered { get; set; }

    partial void OnIsHoveredChanged(bool value) => OnPropertyChanged(nameof(BorderBrush));

    /// <summary>Crimson accent when selected, hairline otherwise (net8 SyncVisual).</summary>
    public Brush BorderBrush => Res(
        App.IsSelected ? "AccentFillColorDefaultBrush"
        : IsHovered ? "ControlStrokeColorSecondaryBrush"
        : "CardStrokeColorDefaultBrush");

    // ── Icon (async, cosmetic) ────────────────────────────────────────────────

    [ObservableProperty] public partial BitmapImage? Icon { get; set; }

    partial void OnIconChanged(BitmapImage? value) => OnPropertyChanged(nameof(HasIcon));

    public bool HasIcon => Icon is not null;

    /// <summary>
    /// Fire-and-forget icon load. Verbatim in spirit from net8
    /// <c>SoftwareTab.LoadIconAsync</c>: the crimson letter tile stays as the
    /// fallback and any failure is swallowed — icons must never crash the tab.
    /// Must be started on the UI thread (BitmapImage is UI-thread affine).
    /// </summary>
    public async Task LoadIconAsync()
    {
        try
        {
            var bmp = await AppIconService.GetIconAsync(App);
            if (bmp is not null) Icon = bmp;
        }
        catch { /* icons are cosmetic — must never crash the tab */ }
    }

    /// <summary>Click-anywhere selection toggle (net8 card.Tapped).</summary>
    public void Toggle() => IsSelected = !IsSelected;

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
