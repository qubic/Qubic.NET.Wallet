using Qubic.Services;
using Qubic.Services.Storage;

namespace Qubic.Net.Wallet.Services;

/// <summary>
/// Makes a vault entry the active seed: session, history database, transaction tracker and the
/// remembered "last identity". The one place every page uses to activate a vault identity.
/// </summary>
public sealed class VaultActivationService(
    SeedSessionService seed,
    WalletStorageService storage,
    TransactionTrackerService tracker,
    TickMonitorService tickMonitor,
    QubicSettingsService settings)
{
    public const string LastIdentityKey = "vault_last_identity";

    public void Activate(VaultEntry entry)
    {
        seed.SetSeed(entry.Seed, entry.Label);
        storage.SetSeed(entry.Seed, entry.Identity, tickMonitor.Epoch);
        tracker.SetEncryptionKey(entry.Seed);
        settings.SetCustom(LastIdentityKey, entry.Identity);
    }
}
