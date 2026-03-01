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
- AES-256-GCM encryption with Argon2id key derivation
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
