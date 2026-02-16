# Qubic.Net Wallet

Cross-platform desktop wallet for the [Qubic](https://qubic.org) network. Runs as a native desktop window via [Photino.Blazor](https://github.com/AhLamm/photino.Blazor), or as a Blazor Server app in the browser with `--server`.

> [!IMPORTANT]
> **Seed Safety** — The Wallet never shares or sends your seed to the network. Your seed is only held locally in memory while the application is running. Close the app when you are not actively using it. Qubic will never contact you to ask for your seed — **DO NOT SHARE your seed with anyone.**

## Features

- **Dashboard** — identity, QU balance, network status, owned assets, and open orders at a glance
- **Send / Receive** — single transfers and batch "Send to Many"
- **Assets** — view and transfer QX-listed tokens
- **QX Trading** — place and cancel asset orders on the Qubic DEX
- **Qearn Staking** — lock/unlock QU for staking rewards
- **Qswap DEX** — decentralized token swap interface
- **MSVault** — multi-signature vault management (create, deposit, release, approve/deny)
- **Transaction History** — full history with local encrypted storage and sync
- **Sign / Verify** — sign messages with your seed and verify signatures
- **Settings** — backend selection (RPC / Bob / Direct TCP), peer management, label sources, database import/export

## Running Pre-Built Releases

Download the latest release for your platform from the [Releases](https://github.com/qubic/Qubic.Net.Wallet/releases) page.

> [!IMPORTANT]
> **Always verify the SHA-256 hashes** to ensure files have not been tampered with.

**Verify the zip download** against the `.zip.sha256` file published alongside each release:

```bash
# Windows (PowerShell)
Get-FileHash Qubic.Net.Wallet-win-x64.zip -Algorithm SHA256

# macOS / Linux
sha256sum -c Qubic.Net.Wallet-linux-x64.zip.sha256
```

**Verify the binary** after extracting — each zip contains a `.sha256` file for the binary:

```bash
# Windows (PowerShell)
Get-FileHash Qubic.Net.Wallet.exe -Algorithm SHA256

# macOS / Linux
sha256sum -c Qubic.Net.Wallet.sha256
```

### Windows

1. Download and extract `Qubic.Net.Wallet-win-x64.zip`
2. Open the `Qubic.Net.Wallet-win-x64` folder and run `Qubic.Net.Wallet.exe`

To run in server mode (opens in browser instead of native window):

```
Qubic.Net.Wallet.exe --server
```

### macOS

Requires **macOS 12 (Monterey)** or later.

1. Download the zip for your architecture:
   - **Apple Silicon** (M1/M2/M3/M4): `Qubic.Net.Wallet-osx-arm64.zip`
   - **Intel**: `Qubic.Net.Wallet-osx-x64.zip`
2. Extract and run:

```bash
unzip Qubic.Net.Wallet-osx-arm64.zip
cd Qubic.Net.Wallet-osx-arm64
chmod +x Qubic.Net.Wallet
codesign --force --deep -s - Qubic.Net.Wallet
xattr -d com.apple.quarantine Qubic.Net.Wallet
./Qubic.Net.Wallet
```

> **Tip:** If macOS Gatekeeper blocks the app, you can also right-click (or Control-click) the binary in Finder and select **Open** to bypass the warning.

### Linux

Desktop mode requires **GLIBC 2.38+** and **WebKitGTK**:

```bash
# Ubuntu/Debian
sudo apt install libwebkit2gtk-4.1-0
```

1. Download and extract `Qubic.Net.Wallet-linux-x64.zip`
2. Run:

```bash
cd Qubic.Net.Wallet-linux-x64
chmod +x Qubic.Net.Wallet
./Qubic.Net.Wallet
```

If desktop mode is not supported on your system, the app automatically falls back to server mode. To run in server mode directly:

```bash
./Qubic.Net.Wallet --server
```

## Running From Source

Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone --recursive https://github.com/qubic/Qubic.Net.Wallet.git
cd Qubic.Net.Wallet

# Desktop mode (native window)
dotnet run

# Server mode (opens in browser)
dotnet run -- --server
```

> **Note:** The `--recursive` flag is required to fetch the [Qubic.Net](https://github.com/qubic/Qubic.Net) submodule under `deps/`.

## Release Builds

```bash
./publish.sh
```

Produces zip archives with SHA-256 hashes for:
- `win-x64`
- `osx-x64`
- `osx-arm64`
- `linux-x64`

## Security

- Your 55-character seed is held **in memory only** for the current session — it is never written to disk
- The local database is encrypted with [SQLCipher](https://www.zetetic.net/sqlcipher/) using a key derived from your seed
- In server mode, access is protected by a one-time session token (HttpOnly cookie, localhost only)
- The database file is useless without the seed

> [!WARNING]
> **Server mode** uses unencrypted HTTP on localhost. A local proxy or other software on the same machine could intercept the communication between your browser and the app, including your seed. Prefer **desktop mode** when possible. Only use `--server` on machines you fully trust.

## Architecture

- **UI**: Blazor components with Bootstrap 5 dark theme
- **Desktop**: Photino.Blazor (WebView2 on Windows, WebKit on macOS/Linux)
- **Server fallback**: Blazor Server with auto-launched browser (`--server` flag, or automatic fallback if native libraries are unavailable)
- **Storage**: SQLCipher-encrypted SQLite per identity
- **Backends**: RPC, Bob, or direct TCP to Qubic nodes
- **Libraries**: [Qubic.Net](https://github.com/qubic/Qubic.Net) included as a git submodule under `deps/`
