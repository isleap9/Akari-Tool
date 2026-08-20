using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

public interface ISettingStateReader
{
    bool ReadToggleState(SettingDefinition setting);
    int ReadSelectionIndex(SettingDefinition setting);

    /// <summary>
    /// Reads the live AC/DC value of a NumericRange PowerCfg setting, in DISPLAY units
    /// (e.g. Minutes / % / Milliseconds). For Separate settings both sides are returned;
    /// non-Separate settings return both values too (the caller uses acValue). Non-PowerCfg
    /// settings return (null, null).
    /// </summary>
    (int? acValue, int? dcValue) ReadNumericValue(SettingDefinition setting);
}
