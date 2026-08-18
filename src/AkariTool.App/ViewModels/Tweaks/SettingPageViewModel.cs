using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Declarative replacement for TweakPageViewModel. Builds its sections from
/// <see cref="SettingGroup"/> records instead of TweakDefinition catalog arrays. No
/// TweakRegistry bracketing yet (Phase 3b-ii) — that wiring lands in a later phase.
/// </summary>
public abstract partial class SettingPageViewModel : ViewModelBase
{
    protected readonly ISettingStateReader _stateReader;
    protected readonly ISettingOperationExecutor _executor;
    protected readonly TweakDialogs _dialogs;

    private volatile bool _built;

    protected SettingPageViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
    {
        _stateReader = stateReader;
        _executor = executor;
        _dialogs = dialogs;
    }

    public abstract string NavTag { get; }
    public abstract string NavLabel { get; }

    protected abstract IReadOnlyList<SettingGroup> BuildSettingGroups();

    public ObservableCollection<SettingSectionViewModel> Sections { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplySearch(value);

    private void ApplySearch(string query)
    {
        foreach (var section in Sections)
            section.ApplySearch(query);
    }

    public void Build()
    {
        if (_built) return;

        lock (this)
        {
            if (_built) return;

            foreach (var group in BuildSettingGroups())
            {
                IEnumerable<SettingItemViewModel> items =
                    group.Settings.Select(s => new SettingItemViewModel(s, _stateReader, _executor, _dialogs));
                Sections.Add(new SettingSectionViewModel(group.Name, items));
            }

            _built = true;
        }
    }
}
