using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Declarative replacement for TweakSectionViewModel: one titled section owning its
/// <see cref="SettingItemViewModel"/> rows. Bulk-bar / pending-pill logic is not ported
/// here yet (Phase 3b-ii is VM scaffolding only).
/// </summary>
public sealed partial class SettingSectionViewModel : ObservableObject
{
    public SettingSectionViewModel(string title, IEnumerable<SettingItemViewModel> items)
    {
        Title = title;
        foreach (var item in items)
            Items.Add(item);
    }

    public string Title { get; }

    public ObservableCollection<SettingItemViewModel> Items { get; } = new();

    public void ApplySearch(string query)
    {
        foreach (var item in Items)
            item.IsVisible = string.IsNullOrWhiteSpace(query) || item.MatchesSearch(query);
    }

    public bool HasVisibleItems => Items.Any(i => i.IsVisible);
}
