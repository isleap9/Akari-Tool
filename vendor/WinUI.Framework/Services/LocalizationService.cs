using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="ILocalizationService"/> backed by MRT Core
/// (<see cref="Microsoft.Windows.ApplicationModel.Resources"/>). Reads strings
/// from the app's <c>Strings/&lt;lang&gt;/Resources.resw</c> files and switches
/// the runtime language via a <see cref="ResourceContext"/> pinned to the
/// requested language, without restarting the app.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager = new();
    private ResourceContext _context;

    public LocalizationService()
    {
        // ApplicationLanguages requires package identity and throws in unpackaged
        // (self-contained) apps, so every read/write is guarded with a fallback.
        CurrentLanguage = GetCurrentLanguage();
        _context = CreateContext(_resourceManager, CurrentLanguage);
    }

    public string CurrentLanguage { get; private set; }

    public event EventHandler<string>? LanguageChanged;

    public string GetString(string key)
    {
        try
        {
            // The resw map is named "Resources", so look up "Resources/<key>"
            // with the language pinned by the current ResourceContext. Dotted
            // resw keys ("Localization.Hello") compile to nested resource map
            // subtrees, so the separator must be a slash ("Localization/Hello").
            var path = "Resources/" + key.Replace('.', '/');
            var candidate = _resourceManager.MainResourceMap.GetValue(path, _context);
            return candidate?.ValueAsString ?? key;
        }
        catch
        {
            return key;
        }
    }

    public string this[string key] => GetString(key);

    public void SetLanguage(string languageTag)
    {
        if (languageTag == CurrentLanguage)
        {
            return;
        }

        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        }
        catch
        {
            // Package identity not available; the pinned ResourceContext below
            // still localizes strings resolved through this service.
        }

        _context = CreateContext(_resourceManager, languageTag);
        CurrentLanguage = languageTag;
        LanguageChanged?.Invoke(this, languageTag);
    }

    private static string GetCurrentLanguage()
    {
        try
        {
            var overrideTag = ApplicationLanguages.PrimaryLanguageOverride;
            if (!string.IsNullOrWhiteSpace(overrideTag))
            {
                return overrideTag;
            }
        }
        catch
        {
            // Fall through to the system language.
        }

        return GetSystemLanguage();
    }

    private static string GetSystemLanguage()
    {
        try
        {
            return Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault() ?? "en-US";
        }
        catch
        {
            return "en-US";
        }
    }

    private static ResourceContext CreateContext(ResourceManager manager, string languageTag)
    {
        var context = manager.CreateResourceContext();
        context.QualifierValues["Language"] = languageTag;
        return context;
    }
}
