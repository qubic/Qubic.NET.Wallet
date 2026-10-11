# Plan: Qubic.Net Wallet support for the AIO Qubic Dev Kit

Status: agreed direction · 2026-10-11 · repos: Qubic.Net (Services/Bob/Core/Crypto), Qubic.Net.Wallet (desktop), Qubic.Net.Wallet.Mobile

## 1. Goal

The wallet connects to a local or remote AIO kit (`github.com/qubic/aio-qubic-dev-kit`) through
**bob** and does everything there that it does on mainnet. It never touches a public service
it was not configured for. The user switches between network profiles (mainnet, AIO, test
networks, custom) at runtime and can load any vault on any of them.

AIO was pitched as "Core · Faucet · Wallet · RPC" but ships neither a wallet nor a faucet. This
plan fills both gaps and adds the signing and dApp features users expect from a modern wallet.
Every non-core feature is **opt-in**.

## 2. Ground truth

**AIO (upstream `d3d900d`, 2026-09-21)**

- bob JSON-RPC `POST :40420/qubic` and WebSocket `ws://:40420/ws/qubic`. Bob implements
  broadcast (`qubic_broadcastTransaction`), balances, transfers, assets, tx/receipt lookup,
  logs, computors, `querySmartContract` and subscriptions.
- core-lite P2P is on `127.0.0.1:31841`. The testnet allows only 4 inbound slots, and bob holds one.
- core-lite HTTP is on `127.0.0.1:41841` and serves rpc.qubic.org-compatible `/live/v1` and
  `/query/v1` paths. It is optional; the wallet does not need it.
- Genesis gives the 676 public computor seeds (`broadcastedComputorSeeds`) 10B QU each.
- Ticks share the mainnet number range (TICK 70,550,000+).
- `TICK_DURATION_MS` can be 0–30000.
- The pending-tx pool holds 32 ticks.
- A core-lite restart resets the chain to genesis.
- Bob's `qubic_chainId` is hardcoded to `qubic:mainnet`.

**Wallet today** (see the code references in section 9)

- One global backend setting, `Rpc | Bob | DirectNetwork`, with no notion of a network.
- In Bob mode the following still go to the RPC URL, which defaults to `rpc.qubic.org`:
  broadcast, tx lookup, assets and IPOs (`QubicBackendService.cs:219-272`), tx-history sync
  (`WalletSyncService.cs:155-168`) and tracker confirmation.
- Other mainnet services are called regardless of backend: the `static.qubic.org` bundle
  (labels, contract fees), `explorer.qubic.org` links, and TickDrift (polls all three sources).
- `BobClient` has no broadcast method.

## 3. Network profiles

### 3.1 Model

```
NetworkProfile {
  Id, Name, Kind: Mainnet | Testnet | Devnet(AIO) | Custom
  Methods: ordered list, 1..n of
     Bob    { Url, UseWebSocket }
     Direct { Host, Port, PeerDiscovery: bool }
     Rpc    { Url }
  ExplorerTxUrlTemplate?          // e.g. http://127.0.0.1/tx/{hash}; null = no link
  StaticBundleUrl?                // null = no labels/fees bundle
  ExpectedFingerprint?            // see 3.4
  TickPolicy                      // see 3.5
}
```

Built-in presets (editable copies, not hardwired):

| Preset | Methods (priority order) | Extras |
|---|---|---|
| **Mainnet** | Rpc rpc.qubic.org → Bob bobnet.qubic.li → Direct corenet.qubic.li:21841 | static bundle, explorer.qubic.org |
| **AIO (this machine)** | Bob http://127.0.0.1:40420 (WS on) | Direct 127.0.0.1:31841 available but **off** by default (only 4 inbound slots on testnet); peer discovery off; explorer template `http://127.0.0.1/…` if the explorer profile runs; no static bundle |
| **AIO (remote host)** | Bob http://&lt;host&gt;:40420 | same; hint to SSH-tunnel the localhost-bound ports |
| **Custom** | user-defined | — |

Existing users migrate automatically. The current flat settings become a profile named
"Mainnet (migrated)" that keeps their URLs and backend choice.

### 3.2 Strict capability routing (no silent fallback)

Each wallet operation is a **capability**. A capability resolves to the first method in the
profile that supports it, and **only within the active profile**:

| Capability | Bob | Direct | Rpc |
|---|---|---|---|
| Tick / epoch | `getTickNumber`, `getCurrentEpoch`, WS `newTicks` | ✓ | ✓ |
| Balance | `getBalance` | ✓ | ✓ |
| **Broadcast** | `broadcastTransaction` (**add to client**) | ✓ | ✓ |
| Tx by hash / receipt | `getTransactionByHash`, `getTransactionReceipt` | tick check only | ✓ |
| Tx history | `getTransfers`/`getQuTransfers` + logs | ✗ | `getTransactionsForIdentity` |
| Assets owned/possessed/issued | `getAssets`, `getAssetBalance` | ✓ (RespondOwnedAssets etc.) | ✓ |
| SC function query | `querySmartContract` | ✓ | ✓ |
| Computor list | `getComputors` | ✓ | ✓ |
| Logs / events | `getLogs`, `findLogIds`, ranges, WS `logs` | ✗ | partial |
| Active IPOs / IPO status | ✗ (verify in Phase 0) | ✓ | ✓ |
| Peers, special commands, oracle | ✗ | ✓ | ✗ |

Rules:

1. A client that the profile does not define is **null**. There are no default URLs inside
   `QubicBackendService` or the clients any more.
2. If no method can serve a capability, the call throws `CapabilityUnavailableException`. The
   UI catches it and shows "Not available on this network: add a Direct method to enable IPO
   status" instead of an error. Pages whose required capability is missing are disabled in the
   navigation, with a tooltip.
3. Settings shows the resolved routing table (for example "Broadcast → Bob") so users can see
   where each call goes.
4. Mainnet-only data sources (static bundle, explorer, remote labels) belong to the profile.
   Profiles without them simply have no labels, links or bundle-sourced fees. Contract fees
   then come from the chain (`querySmartContract` on the fee function, or bob) or are entered
   by the user.
5. **TickDrift compares only the methods inside the active profile** and is off when the
   profile has only one method.
6. **Egress guard (test + runtime)**:
   - An `HttpMessageHandler` and a socket wrapper reject any host that is not in the active
     profile, and log the attempt. In Debug builds it throws; in Release it logs and blocks.
   - An integration test runs every service against an AIO-only profile with the guard in
     throwing mode. That test is the acceptance gate for "AIO works without RPC".

### 3.3 Switching profiles

- A selector in the top bar (desktop) and a header menu (mobile) shows the active profile with
  its own colour/badge. Devnet and testnet profiles get a striped header that cannot be
  mistaken for mainnet.
- Switching stops subscriptions, disposes the clients, swaps the per-profile storage partition
  (sync DB, tx tracker, cached history), rebuilds the clients and re-subscribes. The vault
  stays unlocked; seeds are independent of networks.
- Contacts and send-many templates are shared across profiles (they live in the vault).
  History and tracker state are per profile.

### 3.4 Network fingerprint and chain-reset detection

The chain id cannot be trusted (bob hardcodes it), so the wallet proves which network it is on:

- **core-lite devnet**: the epoch computor list (`qubic_getComputors`) equals the identities
  derived from the public `broadcastedComputorSeeds`.
  - The wallet ships the 676 derived public identities.
  - A unit test re-derives them from the seed list, so the list cannot drift.
- **Mainnet**: anything else whose computor list is signed by the mainnet arbitrator.
- On a mismatch with the profile's `ExpectedFingerprint`, show a prominent warning that the
  user can override (warn, don't block).
- **Chain reset**:
  - Persist (epoch, initial tick of epoch, computor-list digest) per profile.
  - A change, or the tick going backwards, marks the profile "chain reset".
  - Then purge that profile's history and tracker and tell the user.
  - Required for AIO, where `down --wipe && up` restarts at the same tick numbers.

### 3.5 Tick targeting

- **Fixed offset** (current behaviour, `current + N`) stays the default and works on AIO.
  TickOffset becomes per profile.
- **New: time-based mode** for every profile, mainnet included. The user picks "execute in
  ~X seconds".
  - Tick duration is an EMA over observed ticks (bob WS `newTicks` gives exact ticks).
  - Target = current + ceil(X / tickDuration), minimum 2.
- Either way, warn (don't block) when the target is beyond the network's pending-tx window:
  32 ticks on core-lite testnet, about 3 minutes' worth on mainnet. Such a tx would be dropped.
- The tracker's "Unknown"/auto-resend threshold (target+20) gains a time floor, so 0 ms or
  30 s ticks don't trigger premature or endless resends.

## 4. Cross-network safety

Qubic transactions and message signatures have no chain id, and AIO ticks overlap mainnet's
range. **A tx signed on AIO is valid on mainnet** if mainnet has not passed the target tick,
and the same holds the other way round. Signed messages are valid on every network; this is
accepted and not mitigated.

Vaults are not tied to networks: **the user can load any vault on any profile.** Mitigations
are therefore warnings attached to seeds:

1. **Seed network tags**: the vault records, per seed, which profile kinds it has signed on.
   Signing on a devnet or testnet with a seed that has signed on mainnet, or the reverse,
   shows a one-time-per-session confirm that explains the replay risk.
2. **Public devnet seeds**: an identity that is in the public computor set gets a "PUBLIC DEV
   KEY" warning whenever it is used on a mainnet profile, or whenever someone tries to receive
   funds to it there.
3. Each signing preview (S1) shows the network badge and the absolute target tick.

## 5. AIO developer features ("easy wins")

Gated behind the **Developer tools** feature (section 7). They appear only when the fingerprint
says core-lite devnet, or the user forces them on for a Custom profile.

- **Dev accounts**: the 676 computor identities plus core-lite `customSeeds`, with live
  balances. They are in-memory signers that are never written to a vault.
- **Faucet**: "Fund identity X with N QU" signs and broadcasts a transfer from the richest dev
  account, rotating between accounts. No server is involved.
- **Asset helpers**:
  - QX `IssueAsset` from a dev account, at runtime.
  - Generate the exact `tools/spectrum_update` / `universe_issue` / `universe_update` command
    lines for genesis-time funding of vault identities.
- **Contract registry from the chain**: names and indices come from bob or core-lite, not the
  static bundle. This covers contracts the developer added.
- **Generic Contract Console**:
  - Choose a contract, a function or procedure number, an amount and an input struct.
  - The field editor handles `uint*`, `sint*`, `id`, `bit` and `Array<T,N>`.
  - Run a function query and see the decoded output, or send a procedure and see the tx
    receipt plus contract logs (bob `getTransactionReceipt` / `getLogs`).
- **Header import**: point the console at `core-lite/src/contracts/MyContract.h`. The existing
  Qubic.Core contract generator, exposed as a runtime model, extracts the `*_input`/`*_output`
  structs and the `REGISTER_USER_FUNCTION/PROCEDURE` ids and builds typed forms.
- **Epoch panel**: current epoch and ticks until `EPOCH_TICK_CAPACITY`, plus the F7 hint
  (`docker attach`). The wallet does not drive core-lite's stdin.

## 6. Signing features

Already present and unchanged: transfers, send-many, asset transfer and management rights,
QX/QEarn/QSwap/IPO/voting/MSVault calls, raw message sign/verify, the Argon2id vault and
biometric unlock (mobile).

| # | Feature | Gate | Scope |
|---|---|---|---|
| S1 | **Signing preview** (clear signing) | always on | One review sheet for every signature: network badge, source seed (with its tags from section 4), destination, amount, contract name + procedure, decoded input fields (from the section 5 metadata; raw hex as fallback), target tick and ETA. All in-app flows and S3–S6 go through it. |
| S2 | **Raw message sign/verify** | always on | Kept exactly as is: no prefix, compatible with the JS tool. |
| S3 | **Sign-In with Qubic** | opt-in | Structured, human-readable login message: domain, identity, statement, URI, network, nonce, issued-at, expiry. The wallet parses it and shows it field by field; it signs with S2 primitives. Verifier helper in Qubic.Crypto. First consumers: Community Fund board votes and qubic.li logins. Publish the message grammar as a short spec. |
| S4 | **WalletConnect v2 (wallet side)** | opt-in | Namespace `qubic`, methods `qubic_requestAccounts`, `qubic_signTransaction`, `qubic_sendQubic`, `qubic_sign` (exactly what qubic-typescript `connectors/walletconnect.ts` sends). Chains `qubic:mainnet`, plus `qubic:testnet` for test and dev profiles. Session list with per-dApp revoke; every request goes through S1. |
| S5 | **Local signing endpoint** (desktop) | opt-in | Builds on the existing `--server` mode (127.0.0.1, random port, token). Same method set as S4 over JSON-RPC, so SC test scripts can ask the wallet to sign. Every request needs approval in S1, except an optional "auto-approve for dev accounts on devnet profiles" sub-toggle. |
| S6 | **Payment-request URI + QR** | always on (parsing), generation in Receive | `qubic:<ID>?amount=…&asset=<NAME>.<ISSUER>&tick=…&input=<hex>&inputType=<n>&memo=…`. The mobile scanner pre-fills Send from it; today it accepts only a bare address. |

## 7. Feature selection on start

The first launch, and every launch after an update that adds opt-in features, shows a
**Features** screen. It lists each feature with a description, what it does, what it
connects to and its risk. The user ticks what they want, and can change it later in
Settings → Features.

| Feature | Default | Effect when off |
|---|---|---|
| Remote labels (static.qubic.org) | off | No bundle fetch at all (today: `RemoteLabelsEnabled`) |
| Developer tools (dev accounts, faucet, contract console) | off | Pages are not registered |
| Sign-In with Qubic (S3) | off | Messages are rejected with an explanation |
| WalletConnect (S4) | off | No relay connection opened, no pairing UI |
| Local signing endpoint (S5) | off | No port bound |
| ↳ auto-approve dev accounts | off | — |

"Off" means the feature is **not running**: no sockets, no listeners, no background fetches.
It does not just mean "hidden". The choice is stored in app settings, not in the vault, so it
applies to whichever vault is loaded.

## 8. Library changes (Qubic.Net → NuGet, then the apps)

- **Qubic.Bob**:
  - `BroadcastTransactionAsync` on the HTTP and WS clients.
  - HTTP parity with WS (`getComputors`, `findLogIds`, `getTickLogRanges`, `getLogsByIdRange`,
    `getEndEpochLogs`, `getAssets`, `getQuTransfers`).
- **Qubic.Services**:
  - `NetworkProfile` plus a profile store with migration.
  - Capability router and `CapabilityUnavailableException`; remove URL defaults from
    `QubicBackendService` and the clients.
  - Egress guard.
  - Per-profile storage partition.
  - Fingerprint, chain-reset detection and the bundled public-identity list.
  - `TickPolicy` (fixed / timed, EMA, pool-window warning).
  - Bob-based history sync in `WalletSyncService`.
  - Feature-flag service.
- **Qubic.Core**:
  - Contract metadata runtime model, generator reuse and header import.
  - Generic payload encoder/decoder.
- **Qubic.Crypto**: SIWQ message builder, parser and verifier.
- **Apps**:
  - Profile selector and badge, Settings → Networks / Features.
  - First-run Features screen, S1 review sheet, developer pages, WalletConnect, URI handling.
  - Android network-security config allowing cleartext only to loopback and RFC1918 hosts.

## 9. Bugs (fixed now, uncommitted)

| Bug | Fix |
|---|---|
| `BalanceService` refreshed only when `Tick % 30 == 0`, so it missed refreshes when polling skipped tick numbers (any tick rate ≠ poll rate, mainnet included) | `Qubic.Net/src/Qubic.Services/BalanceService.cs`: time-based, at most one tick-driven refresh per 30 s, measured from the last refresh of any kind. Needs a Services NuGet bump before the apps get it. |
| Mobile Settings wrote connection fields straight to `QubicSettingsService`; `QubicBackendService` reads them only in its constructor, so changes needed an app restart | `Qubic.Net.Wallet.Mobile/Components/Pages/Settings.razor`: every connection change goes through `QubicBackendService` and calls `ApplySettings()`, as on desktop. Superseded by profiles in Phase 1, but correct until then. |

Both build cleanly (Qubic.Services Release; Mobile `net10.0-android` Debug). They have not
been run on a device or emulator.

## 10. Upstream asks

- **core-bob**: when `is-testnet` is true, make `qubic_chainId` return a testnet id
  (`qubic:testnet`) instead of the hardcoded mainnet constant. The wallet will display it,
  but keeps relying on the fingerprint (3.4), because the id is self-reported.
- **aio-qubic-dev-kit**:
  - A "Using a wallet" docs page.
  - Optional `BIND_ADDR` for the localhost-bound ports. bob is already on 0.0.0.0, which is
    inconsistent.
  - A README note that the computor seeds are public and dev-only.

## 11. Phases

| Phase | Content | Exit criterion |
|---|---|---|
| **0 — Spike** (1–2 d, remote box: lean AIO needs ~21 GB RAM) | Run AIO lean. Point today's wallet at bob only with the egress guard in log mode. List every leak to rpc.qubic.org / static / explorer. Confirm IPO data availability via bob. | Leak list + capability matrix confirmed |
| **1 — Profiles & routing** | Profiles, presets, migration, switcher + badge, capability router, egress guard (test gate), bob broadcast + history sync, per-profile storage, chain-reset detection, per-profile TickOffset | The full egress-guarded integration test passes on an AIO profile. Send, QX issue/order and MSVault work on AIO from desktop and mobile (emulator via `adb reverse`). Mainnet behaviour is unchanged. |
| **2 — Safety & timing** | Fingerprint, seed network tags + warnings, public dev key warning, time-based tick mode, pool-window warning, tracker time floor | Replay warnings fire in both directions. Time mode is correct at 0.5 s, 1 s and 5 s ticks and on mainnet. |
| **3 — Features screen + dev tools** | Feature-flag service, first-run screen, dev accounts, faucet, asset helpers, on-chain contract registry, Contract Console + header import, epoch panel | A fresh AIO user funds a vault seed and calls their own new contract without qubic-cli |
| **4 — Signing** | S1 review sheet (all flows), S3 SIWQ + spec, S6 URI/QR | Every signature goes through S1. Community Fund verifies a SIWQ login. |
| **5 — dApp connectivity** | S4 WalletConnect, S5 local endpoint | An `@qubic/react` sample dApp on `qubic:testnet` signs through the wallet against AIO. A test script signs through S5. |
