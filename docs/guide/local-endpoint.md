# Local signing endpoint

The desktop wallet can open a small JSON-RPC server so that scripts, test runners and local tools
can ask it to sign. Your seed stays inside the wallet. Enable **Local signing endpoint** in
*Settings → Features*, then open *Tools → Local Endpoint*.

![Local endpoint](../screenshots/0.7.0/14-local-endpoint.png)

## Security model

- Listens on **127.0.0.1 only**, on a random port (or the port you set).
- Every request needs `Authorization: Bearer <token>`. **New token** replaces the token.
- Requests that carry an `Origin` header are refused, so web pages in a browser cannot call it.
- Every signing request opens the signing review, which shows the caller's `User-Agent`.
  The only exception is **Auto-approve dev accounts** (see below).

## Connecting

The URL and token are shown on the page and written to `local-endpoint.json` in the wallet's data folder:

| OS | Default location |
|---|---|
| Linux | `~/.local/share/QubicWallet/local-endpoint.json` |
| Windows | `%LOCALAPPDATA%\QubicWallet\local-endpoint.json` |
| macOS | shown on the page |

```json
{ "url": "http://127.0.0.1:53817/", "token": "…", "pid": 12345 }
```

```bash
F=~/.local/share/QubicWallet/local-endpoint.json
curl -s "$(jq -r .url $F)" \
  -H "Authorization: Bearer $(jq -r .token $F)" -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"qubic_status"}'
```

Requests are JSON-RPC 2.0. `params` is an object, or an array whose first element is that object.

## Methods

| Method | Params | Result |
|---|---|---|
| `qubic_status` | — | `network`, `chainId`, `accounts[]` (`address`, `name`) |
| `qubic_requestAccounts` | — | accounts with `amount` and `assets` |
| `qubic_sendQubic` | `from`, `to`, `amount` | `transactionId`, `tick` |
| `qubic_signTransaction` | `from`, `to`, `amount`, optional `tick`, `inputType`, `payload` (base64) | `transactionId`, `tick`, `signedTransaction` (base64), `txBytesHex` |
| `qubic_sendTransaction` | same as above | same, after broadcast |
| `qubic_sign` | `from`, `message` | `signedData`, `digest`, `signature` (WalletConnect convention, prefixed) |
| `qubic_signRaw` | `from`, `message` | `signature` (base64), `signatureHex`: raw signature over the UTF-8 bytes, no prefix |
| `qubic_sendAsset` | `from`, `to`, `assetName`, `issuer`, `amount` | `transactionId`, `tick` |

`from` must be an account the wallet can sign with: the active seed, or a seed in the unlocked vault.
To sign with a public dev account, make it the active seed first (**use** in Dev Tools). Errors use JSON-RPC error objects. A rejected
review returns an error, and so does an unknown `from`.

### Example: call a contract procedure from a test

```bash
# Procedure 1 of contract #30 with a 48-byte input, signed by dev account 0 (made active with "use")
curl -s "$EP" -H "Authorization: Bearer $TK" -H 'Content-Type: application/json' -d '{
  "jsonrpc":"2.0","id":1,"method":"qubic_sendTransaction",
  "params":{"from":"SINUBYSBZKBSVEFQDZBQWUEJWRXCXOZNKPHIXDZWRBKXDSPJEHFAMBACXHUN",
            "to":"<CONTRACT 30 IDENTITY>","amount":0,"inputType":1,"payload":"<base64 input>"}}'
```

## Auto-approve dev accounts

For unattended contract tests, enable **Auto-approve dev accounts**. Requests are then signed without
a prompt only when **all** of these are true:

- the request comes from the local endpoint,
- the signing account is one of the public core-lite dev keys,
- the active network is not Mainnet and the network check has identified it as a core-lite devnet, and
- the review has no red warning.

Requests for your own seeds, and every request on any other network, still open the review.
