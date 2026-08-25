using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IWindowsRegistryService
{
    bool ApplySetting(RegistrySetting setting, bool enable);
    bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue);
    bool ApplySetting(RegistrySetting setting, bool enable, bool useDefaultValue);

    object? GetValue(string keyPath, string valueName) => Microsoft.Win32.Registry.GetValue(keyPath, valueName, null);
    string[] GetSubKeyNames(string keyPath);

    /// <summary>True when the key (hive-prefixed full path) exists. Winhance parity.</summary>
    bool KeyExists(string keyPath);

    /// <summary>Deletes a key (hive-prefixed full path) recursively. Winhance parity.</summary>
    bool DeleteKey(string keyPath);
}
