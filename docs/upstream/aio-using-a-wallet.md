# Using a wallet with the dev kit

The Qubic.Net Wallet (desktop and Android) can talk to this kit directly, so you can fund identities,
send QU and call your contract's procedures with a UI instead of `qubic-cli`.

## Connect

1. Start the kit (`./scripts/up.sh`). Bob must be running (it is part of every setup, with or without the explorer).
2. In the wallet, pick the network **AIO (this machine)** in the header (desktop) or under *More → Networks* (Android).

The preset uses:

| Method | Endpoint | Used for |
|---|---|---|
| Bob | `http://127.0.0.1:40420` | ticks, balances, broadcast, contract queries, receipts, logs |
| core-lite HTTP (RPC) | `http://127.0.0.1:41841` | asset listings and IPOs (Bob cannot list assets) |
| Direct | `127.0.0.1:31841` | off by default — the testnet node has only 4 inbound slots and Bob uses one |

The wallet only ever talks to the endpoints of the selected network; it never falls back to `rpc.qubic.org`.
*Settings → Networks* shows where each operation goes.

The wallet identifies the chain from its computor list: on this kit it shows **core-lite devnet**.
(Bob's `qubic_chainId` reports mainnet on this kit, so the wallet does not rely on it.)

### Kit on another machine

core-lite's ports are bound to `127.0.0.1` on the kit's host. Tunnel them and use the "this machine" preset:

```bash
ssh -L 40420:localhost:40420 -L 41841:localhost:41841 user@kit-host
```

Android emulator: use host `10.0.2.2` instead of `127.0.0.1` (edit the network).
Android phone over USB: `adb reverse tcp:40420 tcp:40420 && adb reverse tcp:41841 tcp:41841`.

## Funding

The default genesis gives the 676 public computor seeds 10B QU each. With **Developer tools** enabled
(*Settings → Features*), the wallet lists these dev accounts and has a **faucet** that sends QU from them
to any identity. It also issues QX assets from a dev account and prints the exact `tools/spectrum_update` /
`tools/universe_issue` commands for genesis funding.

> The dev seeds are public. Never use them — or send funds to them — on mainnet. The wallet warns if you try.

## Calling your contract

Open **Contract Console**, import your header (`core-lite/src/contracts/MyContract.h`) with the contract
index you registered in `contract_def.h`, and you get typed forms for every function and procedure.
The signing review shows the decoded input of each call before you sign, and *Check result* reads Bob's
receipt after the target tick.

## Scripts and tests

The desktop wallet can open a local JSON-RPC signing endpoint (*Settings → Features → Local signing endpoint*)
on `127.0.0.1` with a bearer token. Methods match the WalletConnect set (`qubic_sendTransaction`,
`qubic_signTransaction`, `qubic_sign`, …). With "auto-approve dev accounts", requests signed by the public dev
keys on a core-lite devnet are signed without a prompt — handy for automated contract tests.

## Things to know

- **Separate seeds.** Qubic transactions carry no network id and this kit uses mainnet-range tick numbers.
  A transaction signed here could also be valid on mainnet. Use seeds that hold nothing on mainnet;
  the wallet warns when a seed has signed on both.
- **Chain resets.** After `./scripts/down.sh --wipe && ./scripts/up.sh` the chain restarts at the same ticks.
  The wallet notices and clears its cached history for this network.
- **Target tick.** The testnet accepts transactions at most 32 ticks ahead. The wallet's default offset (7) is fine;
  "execute in ~N seconds" mode adapts to `TICK_DURATION_MS`.
