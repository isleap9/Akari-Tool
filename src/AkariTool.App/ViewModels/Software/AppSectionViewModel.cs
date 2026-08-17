using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// One category section of app cards (net8 <c>SoftwareTab.CardSection</c>: header +
/// responsive grid + card list).
///
/// net8 rebuilt the UniformGrid's Children on every search keystroke because a
/// UniformGrid still reserves cells for collapsed children. The same constraint
/// applies to <c>UniformGridLayout</c>, so <see cref="VisibleCards"/> is likewise
/// refilled rather than the cards being collapsed in place.
/// </summary>
public sealed partial class AppSectionViewModel : ObservableObject
{
    public string Title { get; }

    /// <summary>Every card in this section, in catalog order.</summary>
    public IReadOnlyList<AppCardViewModel> AllCards { get; }

    /// <summary>The cards currently passing the search filter — what the grid renders.</summary>
    public ObservableCollection<AppCardViewModel> VisibleCards { get; } = [];

    /// <summary>Header + grid hide together when nothing in the section matches.</summary>
    [ObservableProperty] public partial bool IsVisible { get; set; } = true;

    public AppSectionViewModel(string title, IReadOnlyList<AppCardViewModel> cards)
    {
        Title = title;
        AllCards = cards;
        foreach (var c in cards) VisibleCards.Add(c);
    }

    /// <summary>
    /// Applies the search filter. Match test is verbatim from net8
    /// <c>SoftwareTab.ApplySearch</c>: case-insensitive Contains over Name OR
    /// Description; an empty query matches everything.
    /// </summary>
    public void ApplySearch(string query)
    {
        bool empty = query.Length == 0;

        VisibleCards.Clear();
        bool any = false;
        foreach (var card in AllCards)
        {
            card.Visible = empty ||
                card.App.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                card.App.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (card.Visible)
            {
                VisibleCards.Add(card);
                any = true;
            }
        }

        IsVisible = any;
    }
}
