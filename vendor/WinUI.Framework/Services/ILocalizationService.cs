namespace WinUI.Framework.Services;

/// <summary>
/// Loads UI strings from the app's <c>Strings/&lt;lang&gt;/Resources.resw</c>
/// files and supports switching the runtime language without restarting.
/// </summary>
public interface ILocalizationService
{
    /// <summary>The currently active BCP-47 language tag (e.g. "en-US").</summary>
    string CurrentLanguage { get; }

    /// <summary>Raised after the active language changes.</summary>
    event EventHandler<string>? LanguageChanged;

    /// <summary>Gets a localized string by key; returns the key itself when missing.</summary>
    string GetString(string key);

    /// <summary>Indexer sugar for <see cref="GetString"/>.</summary>
    string this[string key] { get; }

    /// <summary>Switches the runtime language and raises <see cref="LanguageChanged"/>.</summary>
    void SetLanguage(string languageTag);
}
