using System.Collections.Generic;
using AkariTool.App.Features.Common.Models;

namespace AkariTool.App.Features.Common.Models;

/// <summary>
/// A section in the Technical Details panel, grouping rows of the same type.
/// </summary>
public sealed record TechnicalDetailSection(
    DetailRowType Type,
    string Label,
    bool StartsExpanded,
    IReadOnlyList<TechnicalDetailRow> Rows)
{
    public int Count => Rows.Count;
}