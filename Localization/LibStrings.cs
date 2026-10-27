using Microsoft.Extensions.Localization;
using Qubic.Services.Features;
using Qubic.Services.Networks;
using Qubic.Services.Signing;

namespace Qubic.Net.Wallet.Localization;

/// <summary>
/// Localizes strings that originate in the <c>Qubic.Services</c> library (capability names,
/// optional-feature texts, signing-review warning titles). The library stays culture-agnostic
/// and exposes stable identifiers (enums, warning codes); this maps them to the wallet's own
/// resource table, falling back to the library's English text when a key is missing.
/// </summary>
public static class LibStrings
{
    public static string Cap(IStringLocalizer<AppStrings> loc, Capability c)
    {
        var s = loc[$"Cap_{c}"];
        return s.ResourceNotFound ? Capabilities.DisplayName(c) : s.Value;
    }

    public static string FeatureTitle(IStringLocalizer<AppStrings> loc, FeatureInfo f) => Lookup(loc, $"Feat_{f.Feature}_Title", f.Title);
    public static string FeatureDescription(IStringLocalizer<AppStrings> loc, FeatureInfo f) => Lookup(loc, $"Feat_{f.Feature}_Desc", f.Description);
    public static string FeatureConnectsTo(IStringLocalizer<AppStrings> loc, FeatureInfo f) => Lookup(loc, $"Feat_{f.Feature}_Connects", f.ConnectsTo);
    public static string FeatureRisk(IStringLocalizer<AppStrings> loc, FeatureInfo f) => Lookup(loc, $"Feat_{f.Feature}_Risk", f.Risk);

    /// <summary>Localized warning title by its stable <see cref="SigningWarning.Code"/>; English title as fallback.</summary>
    public static string WarningTitle(IStringLocalizer<AppStrings> loc, SigningWarning w)
    {
        if (w.Code is not { } code) return w.Title;
        return Lookup(loc, "Warn_" + CodeToKey(code), w.Title);
    }

    private static string Lookup(IStringLocalizer<AppStrings> loc, string key, string fallback)
    {
        var s = loc[key];
        return s.ResourceNotFound ? fallback : s.Value;
    }

    // "network-mismatch" -> "NetworkMismatch"
    private static string CodeToKey(string code) =>
        string.Concat(code.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
