using System.Globalization;
using Qubic.Services;

namespace Qubic.Net.Wallet.Localization;

/// <summary>One supported UI language.</summary>
public sealed record AppLanguage(string Code, string EnglishName, string NativeName);

/// <summary>
/// Chooses and persists the UI language. Only the <see cref="CultureInfo.CurrentUICulture"/>
/// is switched (which resource strings resolve to); number and date formatting are left on the
/// invariant culture so amount parsing stays predictable regardless of language.
/// </summary>
public sealed class LanguageService(QubicSettingsService settings)
{
    private const string Key = "ui.language";

    public static readonly IReadOnlyList<AppLanguage> Supported =
    [
        new("en", "English", "English"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
        new("ru", "Russian", "Русский"),
        new("zh", "Chinese", "中文"),
    ];

    /// <summary>The saved language code, defaulting to English.</summary>
    public string CurrentCode
    {
        get
        {
            var saved = settings.GetCustom<string>(Key);
            return Supported.Any(l => l.Code == saved) ? saved! : "en";
        }
    }

    public AppLanguage Current => Supported.First(l => l.Code == CurrentCode);

    /// <summary>Applies the saved language to this thread and future threads. Call once at startup.</summary>
    public void ApplyAtStartup()
    {
        var culture = new CultureInfo(CurrentCode);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Persists a new language. The caller reloads the app so every component re-renders.</summary>
    public void Save(string code)
    {
        if (!Supported.Any(l => l.Code == code)) return;
        settings.SetCustom(Key, code);
        ApplyAtStartup();
    }
}
