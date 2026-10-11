# v0.7.0

## Features
- **Networks.** The single "backend" selector is replaced by network profiles: Mainnet, "AIO (this machine)" and "AIO (remote host)" are built in, and you can add your own. Each network has one or more methods (Bob, Direct, RPC) in priority order; switch networks from the header bar. Every network keeps its own history database, asset list, imported contracts and tick settings.
- **AIO Qubic Dev Kit support.** The AIO preset talks to the kit's Bob (port 40420), with core-lite's own HTTP API (41841) for what Bob cannot list (assets, IPOs). Direct (31841) is available but off by default.
- **No silent fallback.** Every operation is served only by a method of the active network. If none can serve it, the page says so ("not available on this network") instead of quietly asking rpc.qubic.org. A connection guard blocks any host that is not part of the active network and shows what it blocked. *Settings → Networks* lists where each operation goes, and lets you pick the method per operation (or leave it on "Auto", the priority order).
- **Network check.** The wallet identifies the chain it is connected to: a core-lite devnet (its computors are the public dev seeds), or a chain signed by the mainnet arbitrator. A network that contradicts its settings gets a red warning. (Bob's chain id cannot be used for this — it reports "mainnet" on AIO too.)
- **Chain reset detection.** When an AIO chain is wiped and restarted, the wallet notices that the ticks went backwards and clears that network's cached history.
- **Target tick by time.** Besides the fixed tick offset, a network can use "execute in ~N seconds", based on the measured tick rate. A target beyond what the network accepts (32 ticks on test networks) shows a warning.
- **Signing review.** Every signature — in the wallet, from dev tools, WalletConnect or the local endpoint — now goes through one review sheet. It shows the network, the decoded contract call, the target tick with ETA, and warnings. Send no longer has its own separate confirm step.
- **Replay warnings.** Qubic transactions carry no network id. The wallet records which network class each vault seed has signed on, and warns before the same seed signs on Mainnet and a test network. Public core-lite dev keys used on Mainnet, or funds sent to them, get a red warning.
- **Optional features, opt-in.** A Features screen on first start (and after updates that add options) lets you choose what to enable: remote labels, developer tools, Sign-In with Qubic, WalletConnect and the local signing endpoint. Features that are off do not run at all.
- **Developer tools** (devnets only): public dev accounts with balances, a faucet, QX asset issuance from a dev account, and the AIO commands for genesis funding.
- **Contract Console:** call any contract function or procedure with a typed form. Import your own contract's C++ header to get forms and decoded signing previews for it.
- **Sign-In with Qubic:** structured login messages are recognised, shown field by field and checked (domain, network, expiry, nonce).
- **Payment requests:** Receive can create a `qubic:` payment link / QR code with an amount and message. Send accepts pasted payment links and warns if the link is for a different network.
- **WalletConnect** (opt-in): pair with dApps using the official Qubic method set (`qubic_requestAccounts`, `qubic_sendQubic`, `qubic_signTransaction`, `qubic_sendTransaction`, `qubic_sign`, `qubic_sendAsset`). Requires a free WalletConnect Cloud project id.
- **Local signing endpoint** (opt-in, desktop): JSON-RPC on 127.0.0.1 with a bearer token, for scripts and contract tests. Optional auto-approval applies only to public dev keys on a proven core-lite devnet.

## Fixes
- Balances refreshed only when the tick number was a multiple of 30, so refreshes were skipped whenever polling missed that tick. Balances now refresh every 30 seconds.
- Fresh builds could fail to open the encrypted history database ("e_sqlite3 doesn't support encryption"): a newer plain-SQLite package displaced SQLCipher. Qubic.Services now references `Microsoft.Data.Sqlite.Core` only, so SQLCipher is the sole provider.
- Transaction tracking marked transactions "Unknown" purely by tick count. It now also waits at least 30 seconds, so fast devnets don't trigger premature resends.

## Notes
- Existing settings are migrated into the "Mainnet" network on first start; your endpoints, preferred backend and tick offset are kept. Mainnet keeps its existing history database files.
- Requires `Qubic.Services 1.6.0` and `Qubic.WalletConnect 1.0.0`.

---

# v0.6.4

## Features
- **Switch vault files from the header bar.** The lock screen now lists recently used vault files next to the password box — pick one and the wallet re-points at it, still locked, ready for that vault's password. A folder button opens any other vault file. Previously this meant going to Settings, disconnecting the vault, and opening the other file by path.
- With no vault connected, an "Open vault..." dropdown offers the vault files used before, so you can get back into one without going through Settings.
- Vault files that share a file name are disambiguated by their containing folder in the list.
- Switching always clears the session first (vault locked, seed cleared), so nothing from the previous vault stays active behind the new lock screen.

## Fixes
- **Locking could leave the vault unlocked.** Lock (manual and auto-lock) cleared the seed session *before* locking the vault: it stopped the sync service and closed the encrypted database first, which can block for seconds or throw, and when it threw the vault was never locked. The result was a wallet that looked locked but wasn't — the vault's labels and identities stayed listed on the home screen and could be activated again without a password. The vault is now locked first, the session torn down afterwards, and each teardown step is isolated so one failure cannot skip the rest. Applies equally to Disconnect Vault and Delete Vault in Settings.
- The locked state is now rendered before the session teardown runs, so Lock reacts immediately instead of appearing to do nothing while the sync service shuts down.
- Picking the vault that is already open (from the header dropdown) now takes you to its password prompt and ends the current session, instead of doing nothing.
- A vault created from Settings left the wallet with the vault unlocked, no identity active and no way to lock it short of restarting. The header now shows a Lock button in that state.

## Notes
- The list of recently used vault files is stored in the wallet's settings file (`vault_recent_paths`) as plain file paths. It holds up to 8 entries, drops paths whose file no longer exists, and is removed for a vault you delete. No vault or on-disk vault format changes.

---

# v0.6.3

## Security — please update immediately
- **FourQ low-order public-key forgery (CVE-class, exploited in the wild).** The verify path admitted public keys lying in FourQ's 392-element cofactor subgroup — most importantly the identity point `{1,0,0,0}`, which is also the QX contract address. For these keys an attacker can produce a deterministic signature that verifies for any message without knowing a private key. This was used on the live network to move QU from the identity point to attacker-controlled wallets. Patched in `Qubic.Crypto 1.5.2` (mirrors qubic/core PR #921 "hotfix fourq library") and pulled in transitively via `Qubic.Services 1.5.2`. Verify now rejects (a) the identity-encoded public key (fast-path) and (b) any decoded public key whose order divides the cofactor, via the `[392]·A == O` check. Eight real network-forgery transactions are pinned as regression tests.
- No vault, on-disk, or wire-format changes. Pure verify-side hardening; legacy/legitimate signatures keep verifying unchanged.

---

# v0.6.2

## Security
- **FourQ signature canonicalisation (CVE-class fix).** The verify path used to admit any scalar `S < 2^246`, but the curve order `N` is also less than `2^246`. Attackers could replay a valid signature `S` as its twin `S' = S + N` — same `(R, pubkey, message)`, so verify still succeeded, but the on-chain tx hash differed, bypassing dedup and enabling **double-execution of the same intent**. Patched in `Qubic.Crypto 1.5.1` (mirrors qubic/core commit `05f7348`) and pulled in transitively via `Qubic.Services 1.5.1`. No vault or on-disk format changes — pure verify-side hardening.

---

# v0.6.1

## Security
- **Vault key derivation upgraded to Argon2id** (`m=64 MiB, t=3, p=1`). Existing vaults written with PBKDF2-HMAC-SHA256 (600 000 iterations) continue to open and silently re-encrypt to Argon2id on the next save (add seed, rename, change password, etc.).
- **New vault file format (v2)** carries an explicit `Version`, `Kdf`, and `KdfParams` in the envelope so future KDF/parameter changes can be rolled out without breaking existing files. Legacy v1 envelopes (no `Version` field) remain readable.
- **Password validator replaced with [zxcvbn](https://github.com/dropbox/zxcvbn) strength estimation.** The old "12 chars + 1 upper + 1 lower + 1 digit + 1 special" recipe pushed users toward predictable patterns (`Password1234!`). The new check requires length ≥ 12 **and** zxcvbn score ≥ 3.
- Fixed README and earlier release notes that incorrectly claimed Argon2id was already in use — those versions were PBKDF2.

## UI
- **Live password-strength meter** on all vault password fields (create vault, change password). Color-coded progress bar, strength label (Very weak → Strong), zxcvbn warning, and concrete improvement suggestions appear as you type. An estimated crack time ("offline slow hashing, 10⁴ guesses/sec") is shown for fair-and-above passwords so the trade-off is visible.

> ⚠️ Vaults saved by v0.6.1 cannot be opened by v0.5.x or earlier (the older builds don't know the Argon2id KDF). Keep a backup of `vault.dat` until you've confirmed the upgrade is working for you.

---

# v0.5.0

## Upgrade
- Upgraded to **.NET 10** runtime and SDK
- Synced Qubic.Net library to latest upstream (v1.279.0.3 core, contract codegen updates)

## Fixes
- Fixed **blazor.web.js 404** in server fallback mode (added `RequiresAspNetWebAssets` and build-time copy target)
- Fixed **CSS not loading** in server mode (removed `MapStaticAssets` interference)
- Fixed deprecated `Rfc2898DeriveBytes` constructor (SYSLIB0060) — now uses static `Pbkdf2()` method

## Improvements
- **Central BalanceService** — header and home page always show the same balance, auto-refreshes every 30 ticks and on identity switch

## Downloads

| Platform | File |
|----------|------|
| Windows x64 | `Qubic.Net.Wallet-0.5.0-win-x64.zip` |
| macOS Intel | `Qubic.Net.Wallet-0.5.0-osx-x64.zip` |
| macOS Apple Silicon | `Qubic.Net.Wallet-0.5.0-osx-arm64.zip` |
| Linux x64 | `Qubic.Net.Wallet-0.5.0-linux-x64.zip` |

Verify downloads with the `.sha256` files included alongside each zip.

---

# v0.4.0

## New Features

### Watchlist
- Dedicated watchlist page for monitoring addresses you don't own
- Add any 60-character identity with a custom label
- View QU balance and owned assets for each watched address
- Compact and extended view modes
- Accessible from the sidebar navigation

### Light Theme
- Full light theme support across the entire application
- Navigation sidebar, navbar, and all cards adapt to the selected theme
- Dual logo: dark logo for dark theme, monochrome logo for light theme
- Theme selection is now persisted and applied on app startup (no longer resets to dark)

## Improvements

- **Vault UI restructured** — Consolidated vault settings tab from 7 separate cards to 3: Seeds (with inline add form), Address Book (with inline add form), and Vault Settings (collapsible change password + delete)
- Improved error boundary handling in layout

## Downloads

| Platform | File |
|----------|------|
| Windows x64 | `Qubic.Net.Wallet-0.4.0-win-x64.zip` |
| macOS Intel | `Qubic.Net.Wallet-0.4.0-osx-x64.zip` |
| macOS Apple Silicon | `Qubic.Net.Wallet-0.4.0-osx-arm64.zip` |
| Linux x64 | `Qubic.Net.Wallet-0.4.0-linux-x64.zip` |

Verify downloads with the `.sha256` files included alongside each zip.

---

# v0.3.1

## New Features

### Tick Drift Detection
- Monitors all three backends (RPC, Bob, Direct Network) every 60 seconds
- Displays a warning badge in the navbar when tick drift exceeds the configured threshold
- Detailed per-backend tick comparison table on the Sync Manager page showing tick, status, and last check time
- Configurable drift threshold in Settings > Connection (default: 15 ticks)

### Optimized Log Sync with Indexed Fetch
- Three-step indexed log catch-up replaces full epoch scan: `findLogIds` > `getTickLogRanges` > `getLogsByIdRange`
- Catches up logs from current epoch minus 2 through the previous epoch before subscribing for live updates
- Snapshot-based watermark ensures subscription starts near real-time after indexed fetch
- Progress bar with percentage shown on the Log Events page during catch-up

### Tx Hash Display Component
- Reusable component with clickable explorer link and copy button
- Applied consistently across all pages: Send, Transaction History, Log Events, Assets, QX Trading, Qswap, Qearn, SC Auctions, MSVault, Voting, Send to Many

## Improvements

- Addresses in the Log Events table are now copyable
- Log sync progress bar with catch-up percentage and epoch indicator
- Bob log subscription catch-up progress from server is now visualized

## Bug Fixes

- Fixed initial sync being skipped for user-entered seeds; skip-sync policy now only applies to freshly generated seeds
- Fixed `qubic_getLogsByIdRange` parameter handling (3rd param is `endLogId`, not batch size)
- Fixed epoch info `endTick` returning 0 for past epochs by adding fallback chain

## Downloads

| Platform | File |
|----------|------|
| Windows x64 | `Qubic.Net.Wallet-0.3.1-win-x64.zip` |
| macOS Intel | `Qubic.Net.Wallet-0.3.1-osx-x64.zip` |
| macOS Apple Silicon | `Qubic.Net.Wallet-0.3.1-osx-arm64.zip` |
| Linux x64 | `Qubic.Net.Wallet-0.3.1-linux-x64.zip` |

Verify downloads with the `.sha256` files included alongside each zip.

---

# v0.3.0

## New Features

### Encrypted Vault
- Password-protected vault file stores multiple seeds and an address book
- AES-256-GCM encryption with PBKDF2-HMAC-SHA256 (600 000 iterations) key derivation; later versions migrated this to Argon2id
- Create, unlock, lock, and delete vault from the top bar
- Switch between stored seeds without re-entering them

### Address Book
- Save contacts with custom labels to the vault
- Manage contacts in Settings (add, rename, delete)
- All address input fields across the app now feature autocomplete with saved identities and contacts
- Save unknown addresses directly from any address input dropdown
- After successful transactions, prompts to save the destination address

### Send to Many Templates
- Save the current recipient list as a named template
- Load, overwrite, and delete templates
- Templates support recipients with zero amounts (address-only lists)
- Stored in the encrypted vault alongside seeds and contacts

### QR Code on Receive Page
- QR code displayed for your identity address
- Copy QR as PNG image to clipboard

### Community Voting
- View active proposals and their options
- Cast votes with your identity

### SC Auctions
- View available smart contract auction slots
- Place bids on smart contract slots

### Settings Page
- Consolidated settings: backend selection, peer management, label sources
- Theme toggle (light/dark)
- Address book management
- Database import/export

## Improvements

- Unified `AddressInput` component with autocomplete across all pages (Send, Send to Many, Assets, QX Trading, Qswap, MSVault, Voting)
- Consistent address display format (`AAAA...ZZZZ`) throughout the app
- Send to Many: import recipients from text (paste tab/comma-separated lists)

## Downloads

| Platform | File |
|----------|------|
| Windows x64 | `Qubic.Net.Wallet-0.3.0-win-x64.zip` |
| macOS Intel | `Qubic.Net.Wallet-0.3.0-osx-x64.zip` |
| macOS Apple Silicon | `Qubic.Net.Wallet-0.3.0-osx-arm64.zip` |
| Linux x64 | `Qubic.Net.Wallet-0.3.0-linux-x64.zip` |

Verify downloads with the `.sha256` files included alongside each zip.
