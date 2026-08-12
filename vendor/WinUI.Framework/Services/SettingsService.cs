using System.Text.Json;
using Windows.Storage;

namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="ISettingsService"/> backed by <see cref="ApplicationData.LocalSettings"/>
/// when the app has package identity (packaged), or a JSON file under
/// <c>%LOCALAPPDATA%</c> when it does not (unpackaged).
/// </summary>
public class SettingsService : ISettingsService
{
    private const string FileStoreFolder = "WinUI.Framework";
    private const string FileStoreName = "settings.json";

    private readonly ApplicationDataContainer? _localSettings;
    private readonly Dictionary<string, object?>? _fileSettings;
    private readonly string? _filePath;

    public SettingsService()
    {
        try
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }
        catch
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                FileStoreFolder);
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, FileStoreName);
            _fileSettings = LoadFile(_filePath);
        }
    }

    public T Get<T>(string key, T defaultValue = default!)
    {
        object? value = _localSettings is not null
            ? (_localSettings.Values.TryGetValue(key, out var stored) ? stored : null)
            : (_fileSettings!.TryGetValue(key, out var fileStored) ? fileStored : null);

        if (value is null)
        {
            return defaultValue;
        }

        var target = typeof(T);
        if (value is T typed)
        {
            return typed;
        }

        try
        {
            if (target.IsEnum)
            {
                return (T)Enum.Parse(target, value.ToString()!);
            }

            return (T)Convert.ChangeType(value, target);
        }
        catch
        {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T? value)
    {
        if (value is null)
        {
            Remove(key);
            return;
        }

        // Enums are stored as their name so they round-trip through both stores.
        object stored = typeof(T).IsEnum ? value.ToString()! : value;

        if (_localSettings is not null)
        {
            _localSettings.Values[key] = stored;
            return;
        }

        _fileSettings![key] = stored;
        SaveFile();
    }

    public bool Contains(string key)
        => _localSettings is not null
            ? _localSettings.Values.ContainsKey(key)
            : _fileSettings!.ContainsKey(key);

    public void Remove(string key)
    {
        if (_localSettings is not null)
        {
            _localSettings.Values.Remove(key);
            return;
        }

        if (_fileSettings!.Remove(key))
        {
            SaveFile();
        }
    }

    private static Dictionary<string, object?> LoadFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path)) ?? new();
            }
        }
        catch
        {
            // Corrupt or unreadable settings; start fresh.
        }

        return new();
    }

    private void SaveFile()
    {
        try
        {
            File.WriteAllText(_filePath!, JsonSerializer.Serialize(_fileSettings));
        }
        catch
        {
            // Ignore persistence failures.
        }
    }
}
