namespace WinUI.Framework.Services;

/// <summary>
/// Typed access to persisted per-user application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>Gets a setting value, or <paramref name="defaultValue"/> when missing or unreadable.</summary>
    T Get<T>(string key, T defaultValue = default!);

    /// <summary>Sets a setting value. Setting <c>null</c> removes the key.</summary>
    void Set<T>(string key, T? value);

    /// <summary>Whether a setting with the given key exists.</summary>
    bool Contains(string key);

    /// <summary>Removes a setting with the given key.</summary>
    void Remove(string key);
}
