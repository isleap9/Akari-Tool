using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

public interface ISettingStateReader
{
    bool ReadToggleState(SettingDefinition setting);
    int ReadSelectionIndex(SettingDefinition setting);
}
