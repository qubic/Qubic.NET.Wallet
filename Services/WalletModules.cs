using Qubic.Services;

namespace Qubic.Net.Wallet.Services;

/// <summary>Optional wallet pages the user can show or hide (default: all shown).</summary>
public enum WalletModule
{
    SendMany,
    Assets,
    Qx,
    Qswap,
    Qearn,
    Msvault,
    ScAuctions,
    Voting,
    Governance,
    Ccf,
    Watchlist,
    SignVerify,
}

/// <summary>
/// Describes a toggleable page. <paramref name="SectionKey"/> groups pages under the same
/// headings used in the navigation menu. <paramref name="TitleKey"/> is a localization key for
/// the page name (reusing the nav labels); when null, <paramref name="TitleLiteral"/> is shown
/// as-is (product names that are not translated). <paramref name="DescKey"/> is the one-line description.
/// </summary>
public sealed record ModuleInfo(
    WalletModule Module,
    string Route,
    string Icon,
    string SectionKey,
    string? TitleKey,
    string? TitleLiteral,
    string DescKey);

/// <summary>
/// Shows/hides optional wallet pages. Choices are stored in app settings (not in the vault),
/// default to visible, and drive the navigation menu. Hiding a page only removes it from the
/// menu; it does not disable any background service.
/// </summary>
public sealed class ModuleVisibilityService(QubicSettingsService settings)
{
    public event Action? OnChanged;

    private static string Key(WalletModule m) => "module." + m;

    /// <summary>Visible unless the user explicitly turned it off.</summary>
    public bool IsEnabled(WalletModule m) => settings.GetCustom<bool?>(Key(m)) ?? true;

    public void SetEnabled(WalletModule m, bool enabled)
    {
        if (IsEnabled(m) == enabled) return;
        settings.SetCustom(Key(m), enabled);
        OnChanged?.Invoke();
    }

    public static readonly IReadOnlyList<ModuleInfo> All =
    [
        new(WalletModule.SendMany,   "/send-many",   "bi-send-plus",        "Nav_Section_Transactions", "Nav_SendToMany", null,      "ModDesc_SendMany"),
        new(WalletModule.Assets,     "/assets",      "bi-grid-3x3-gap",     "Nav_Section_Assets",       "Nav_Assets",     null,      "ModDesc_Assets"),
        new(WalletModule.Qx,         "/qx",          "bi-shop",             "Nav_Section_Defi",         "Nav_QxTrading",  null,      "ModDesc_Qx"),
        new(WalletModule.Qswap,      "/qswap",       "bi-arrow-left-right", "Nav_Section_Defi",         null,             "QSwap",   "ModDesc_Qswap"),
        new(WalletModule.Qearn,      "/qearn",       "bi-piggy-bank",       "Nav_Section_Defi",         null,             "QEarn",   "ModDesc_Qearn"),
        new(WalletModule.Msvault,    "/msvault",     "bi-safe",             "Nav_Section_Defi",         null,             "MSVault", "ModDesc_Msvault"),
        new(WalletModule.ScAuctions, "/sc-auctions", "bi-graph-up-arrow",   "Nav_Section_Defi",         "Nav_ScAuctions", null,      "ModDesc_ScAuctions"),
        new(WalletModule.Voting,     "/voting",      "bi-chat-square-dots", "Nav_Section_Defi",         "Nav_Voting",     null,      "ModDesc_Voting"),
        new(WalletModule.Governance, "/governance",  "bi-bank",             "Nav_Section_Defi",         "Nav_Governance", null,      "ModDesc_Governance"),
        new(WalletModule.Ccf,        "/ccf",         "bi-cash-stack",       "Nav_Section_Defi",         "Nav_Ccf",        null,      "ModDesc_Ccf"),
        new(WalletModule.Watchlist,  "/watchlist",   "bi-eye",              "Nav_Section_History",      "Nav_Watchlist",  null,      "ModDesc_Watchlist"),
        new(WalletModule.SignVerify, "/sign-verify", "bi-pen",              "Nav_Section_Tools",        "Nav_SignVerify", null,      "ModDesc_SignVerify"),
    ];
}
