# Developer tools

The *Developer* section of the menu appears when the **Developer tools** feature is on. Dev Tools works
on test networks only. The Contract Console works on every network.

## Dev Tools

![Developer tools](../screenshots/0.7.0/04-dev-tools.png)

### Dev accounts

core-lite's genesis gives each of its public computor seeds 10 billion QU. Dev Tools lists these
accounts with their live balances. **use** makes an account the active seed for the session, with no
vault needed.

These seeds are published in the core-lite source. Anyone can sign with them. The wallet shows red
warnings when a dev key signs on Mainnet or when funds are sent to one there.

### Faucet

Sends QU from a dev account to any identity. The transfer goes through the normal signing review.

![Faucet review](../screenshots/0.7.0/11-faucet-review.png)

### Issue a QX asset

Issues a new asset through QX, signed by a chosen dev account. The node charges the QX issuance fee.
The new asset appears under *Assets* for that account after the target tick.

### Genesis funding (AIO kit)

Prints the AIO kit commands that write a balance, and optionally an asset, into the genesis state files:

```bash
./tools/spectrum_update states/spectrum.<epoch> <IDENTITY> <AMOUNT>
./tools/universe_issue states/universe.<epoch> <IDENTITY> <ASSET> <SHARES>
./scripts/down.sh --wipe && ./scripts/up.sh
```

This restarts the chain from genesis. The wallet detects the reset and clears that network's
cached history.

## Contract Console

Calls any smart contract function (read) or procedure (transaction) through a typed form.

![Contract Console](../screenshots/0.7.0/06-contract-console.png)

- **Contract**: all built-in contracts, plus contracts you imported.
- **Function (read)** queries the contract through the active network and decodes the output struct.
- **Procedure (tx)** encodes the input struct, then opens the signing review with every field decoded.
  You can send QU with the call (e.g. for fees) and change the target tick.
- **Check result** reads Bob's execution receipt for the broadcast transaction once the target tick
  has passed (needs a network with Bob).
- **Contracts on this node** lists the contracts the network reports, with their epoch and state size.

### Importing your own contract

![Imported contract](../screenshots/0.7.0/13-contract-import.png)

Paste your contract header, or load the `.h` file, and enter the contract index you registered in
`contract_def.h`. The wallet parses:

- the contract struct (`struct NAME : public ContractBase`),
- every `X_input` / `X_output` struct, including nested structs, arrays and `id` / `bit` / `sint*` / `uint*` fields,
- the `REGISTER_USER_FUNCTION(name, id)` and `REGISTER_USER_PROCEDURE(name, id)` ids.

The layout follows the MSVC rules that core uses (`/Zp8` packing), so the encoded bytes match what
your contract expects. Warnings are shown for anything the parser skipped.

Imports are kept **per network**. A new import with the same index replaces the old one.
The same metadata decodes calls to that contract in the signing review, so a dApp calling your
contract through WalletConnect is decoded as well.

Asset names stored in `uint64` fields (e.g. `assetName`) are shown and entered as text (`QX`, `MYTOKEN`).
