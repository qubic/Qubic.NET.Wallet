namespace Qubic.Net.Wallet.Services;

/// <summary>
/// Relaunches the desktop app. Used when a setting (e.g. the UI language) can only be applied
/// cleanly at startup: an in-process Blazor host keeps the culture it captured when it started,
/// so a page reload alone does not switch it. In <c>--server</c> mode no restarter is configured
/// and callers fall back to a forced reload instead.
/// </summary>
public sealed class AppRestartService
{
    private Action? _restart;

    /// <summary>True when running as a desktop app that can relaunch itself.</summary>
    public bool IsSupported => _restart != null;

    /// <summary>Wired up once in desktop startup.</summary>
    public void Configure(Action restart) => _restart = restart;

    public void Restart() => _restart?.Invoke();
}
