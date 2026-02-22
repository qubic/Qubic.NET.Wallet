# Qubic.Net Wallet — Video Tutorial Script

> **Target length:** ~15–20 minutes
> **Format:** Screen recording with voiceover narration
> **Resolution:** 1920×1080, dark theme enabled

---

## INTRO (0:00 – 0:30)

**[Screen: Wallet logo / title card]**

> Welcome to the Qubic.Net Wallet video tutorial. In this guide, we'll walk through every feature of the wallet — from setting up your first identity to trading assets on the Qubic DEX, staking with QEarn, and managing a multi-signature vault.
>
> The Qubic.Net Wallet runs as a native desktop application on Windows, macOS, and Linux. It can also run in your browser using server mode. Let's get started.

---

## CHAPTER 1: Installation & First Launch (0:30 – 1:30)

**[Screen: GitHub Releases page]**

> Head to the Releases page on GitHub and download the zip for your platform — Windows x64, macOS Intel, macOS Apple Silicon, or Linux x64.
>
> Always verify the SHA-256 hash of the downloaded zip against the `.sha256` file published alongside it. This ensures the file hasn't been tampered with.

**[Screen: Extract zip, run executable]**

> Extract the zip and run the executable. On macOS, you may need to right-click and select Open to bypass Gatekeeper. On Linux, make sure WebKitGTK is installed.
>
> The wallet opens as a native desktop window. If native mode isn't available on your system, it automatically falls back to server mode, which opens in your browser.

**[Screen: Wallet opens — empty state, no seed entered]**

> This is the main interface. On the left you have the navigation sidebar. At the top is the seed bar where you'll enter or unlock your identity. Let's start by creating a new wallet.

---

## CHAPTER 2: Creating a New Wallet (1:30 – 3:00)

**[Screen: Home page — "Get Started" and "Create New Wallet" panels]**

> On the Home page, you'll see two panels. The right panel lets you generate a brand new seed.

**[Action: Click "Generate New Seed"]**

> Click Generate New Seed. The wallet creates a random 55-character lowercase seed. This seed is your master key — it controls your identity and all your funds.

**[Screen: Seed displayed with eye toggle and copy button]**

> You can click the eye icon to reveal the seed, and the copy button to copy it to your clipboard.

> **Important: Write down your seed on paper and store it somewhere safe. If you lose your seed, your funds are gone forever. Never share it with anyone.**

**[Action: Check "I have written down my seed", click "Use This Seed"]**

> Check the confirmation box and click Use This Seed. Your identity is now active — you can see it in the top bar.

**[Screen: Top bar showing identity badge]**

> The top bar now shows your truncated identity address. Click it to copy the full 60-character address to your clipboard.

---

## CHAPTER 3: Connecting to the Network (3:00 – 4:00)

**[Screen: Top bar — backend selector and connect button]**

> Before you can check your balance or make transactions, you need to connect to the Qubic network. The wallet supports three backends:
>
> - **RPC** — connects to a Qubic RPC server. This is the default and recommended for most users.
> - **Bob** — connects to a Bob JSON-RPC node.
> - **Direct** — connects directly to a Qubic node via TCP. This is the most decentralized option but requires a reachable node.

**[Action: Select RPC, click Connect]**

> Select your preferred backend and click Connect. You'll see the current epoch and tick appear in the top bar, confirming you're connected.

---

## CHAPTER 4: The Dashboard (4:00 – 5:00)

**[Screen: Home page — dashboard with balance, assets, orders]**

> Once connected with an active seed, the Home page becomes your dashboard. At the top you see your full identity and QU balance. Click Load Balance to refresh it.
>
> Below that are Quick Action buttons for Send, Receive, Assets, QX Trading, and History.
>
> The dashboard also shows your owned assets and any open orders on the QX exchange — giving you a snapshot of your portfolio at a glance.

---

## CHAPTER 5: Sending QU (5:00 – 6:30)

**[Screen: Navigate to Send page]**

> Click Send in the sidebar. The From field shows your identity and current balance.

**[Action: Enter destination address and amount]**

> Enter the destination identity — you can type it manually, or select from your address book if you've saved contacts. Enter the amount of QU to send. The quick-fill buttons let you quickly set 10%, 25%, 50%, or 100% of your balance.

**[Screen: Target Tick field]**

> The Target Tick is set automatically — the wallet picks the current tick plus an offset to give the network time to process your transaction.

**[Action: Click "Preview Transaction"]**

> Click Preview Transaction to review the details — destination, amount, tick, and any fees.

**[Action: Click "Sign & Broadcast"]**

> Click Sign & Broadcast. The wallet signs the transaction locally with your seed and sends it to the network. You'll see the transaction ID and a link to track it in your history.
>
> If the destination address isn't in your address book, the wallet offers to save it as a contact.

---

## CHAPTER 6: Send to Many (6:30 – 8:00)

**[Screen: Navigate to Send to Many page]**

> Send to Many lets you send QU to up to 25 recipients in a single transaction using the QUTIL SendToManyV1 contract.

**[Action: Add multiple recipients]**

> Add recipients one by one — each with an address and amount. Or click Import from Text to paste a list of addresses and amounts, one per line, separated by commas or tabs.

**[Screen: Template management — Save As, Load]**

> If you have a vault set up, you can save the current recipient list as a named template. This is handy for recurring payments — load a template, adjust amounts if needed, and send.

**[Action: Click "Sign & Broadcast"]**

> Review the total, sign, and broadcast — just like a single send.

---

## CHAPTER 7: Receiving QU (8:00 – 8:30)

**[Screen: Navigate to Receive page]**

> The Receive page displays your identity as a QR code. Anyone can scan this to get your address.
>
> Below the QR code is your full identity in a copyable format. Click Copy Address to copy it, or Copy QR to copy the QR code image to your clipboard — handy for pasting into a chat or document.

---

## CHAPTER 8: The Encrypted Vault (8:30 – 10:30)

**[Screen: Navigate to Settings → Vault tab]**

> The Vault is one of the most important features. It lets you store multiple seeds and an address book in a single encrypted file, protected by a password you choose.

### Creating a Vault

**[Action: Fill in Create Vault form]**

> Enter a file path — or click the folder icon to browse for a location. Then enter the seed you want to store, give it a label like "Main Wallet", and set a strong password.

**[Action: Click "Create Vault"]**

> Click Create Vault. The wallet creates an AES-256-GCM encrypted file using Argon2id for key derivation. This is military-grade encryption.

### Managing Seeds

**[Screen: Vault Entries table]**

> Your vault can hold multiple seeds. Click Add Entry to store another seed with its own label. You can rename or delete entries at any time.

### Switching Identities

**[Screen: Top bar — identity switcher dropdown]**

> When your vault has multiple entries, the top bar shows a dropdown. Click it to switch between identities instantly — no need to re-enter seeds.

### Address Book

**[Screen: Address Book section in Vault tab]**

> The vault also stores your contacts. Add addresses with labels, and they'll appear as autocomplete suggestions in every address field across the app — Send, Send to Many, Assets, QX Trading, everywhere.
>
> You can also save addresses on the fly: after a successful transaction, the wallet offers to save the destination.

### Locking and Unlocking

**[Screen: Top bar — Lock button]**

> Click Lock in the top bar to secure your vault. All seeds are cleared from memory. To resume, enter your vault password and click Unlock.

### Quick Save from Top Bar

**[Screen: Top bar — "Save to Vault" button and modal]**

> If you enter a seed directly without a vault, the top bar shows a Save to Vault button. Click it to create a new vault file or add the seed to an existing vault — right from the top bar.

---

## CHAPTER 9: Assets (10:30 – 11:30)

**[Screen: Navigate to Assets page]**

> The Assets page shows all tokens owned by an identity. Enter any identity address and click Lookup Assets.
>
> You'll see three categories: Owned assets, Possessed assets, and Issued assets.

**[Action: Click action buttons on an owned asset]**

> For your own assets, you can directly place sell or buy orders, transfer to another address, view the order book, or manage rights — all from action buttons on each row.

**[Screen: Order Book modal]**

> Click Orders to view the live order book for any asset. The asks and bids are shown side by side. Click a row to pre-fill a trade.

---

## CHAPTER 10: QX Trading (11:30 – 13:00)

**[Screen: Navigate to QX Trading page]**

> QX is the Qubic decentralized exchange. The QX Trading page gives you full access.

**[Screen: Open Orders panel]**

> At the top, you see your open orders with cancel buttons for each.

**[Action: Switch between tabs — Issue, Transfer, Ask, Bid, Rights]**

> The tabs give you everything:
>
> - **Issue** — create a new asset with a name, supply, and decimal places.
> - **Transfer** — send assets to another address.
> - **Ask** — place a sell order at your price.
> - **Bid** — place a buy order. The total cost is calculated automatically.
> - **Rights** — transfer asset management rights to a smart contract.

**[Screen: QX Market Overview]**

> Below the tabs is the Market Overview — a table showing every traded asset with best ask, best bid, depth, and spread. Click Load to refresh it.

---

## CHAPTER 11: QSwap DEX (13:00 – 13:45)

**[Screen: Navigate to QSwap page]**

> QSwap is the automated market maker on Qubic. You can create liquidity pools, add or remove liquidity, and swap tokens.
>
> Select an asset, choose your swap direction, enter amounts, and click Swap. The interface handles the contract calls for you.

---

## CHAPTER 12: QEarn Staking (13:45 – 14:30)

**[Screen: Navigate to QEarn page]**

> QEarn lets you lock QU for staking rewards over a 52-week period.

**[Screen: Staking Summary table]**

> The summary shows all your active locks — the epoch, amount, current week, estimated reward, and an early unlock option for each.

**[Action: Lock tab — enter amount, click Lock]**

> To stake, enter an amount and click Lock. Your QU is locked for 52 weeks, earning yield each week. You can unlock early, but the reward is reduced.

**[Action: Query tab — select function]**

> The Query tab lets you explore staking data — lock info per epoch, your lock status as a visual 52-week bitmask, global stats, and more.

---

## CHAPTER 13: MSVault — Multi-Signature (14:30 – 15:30)

**[Screen: Navigate to MSVault page]**

> MSVault provides multi-signature vaults on Qubic. Multiple owners must approve before funds can be released — ideal for teams and organizations.

**[Screen: Your Vaults panel]**

> The top panel shows your vaults with balances, owner counts, and required approvals. Pending releases show who has approved and who hasn't.

**[Action: Register tab — add owners and set threshold]**

> To create a vault, go to the Register tab. Enter a name, add owner addresses, set the number of required approvals, and register.

**[Screen: Deposit and Release tabs]**

> Use the Deposit tab to fund the vault, and the Release tab to propose a withdrawal. Other owners approve releases from their own wallets. Once the threshold is met, the funds are released.

---

## CHAPTER 14: Voting & SC Auctions (15:30 – 16:15)

**[Screen: Navigate to Voting page]**

> The Voting page lets you participate in community governance. Browse active proposals, cast your vote, view results with progress bars, or create a new poll.

**[Screen: Navigate to SC Auctions page]**

> SC Auctions shows available smart contract slots. Browse active auctions, check bid status, and place bids to secure a slot.

---

## CHAPTER 15: Transaction History & Logs (16:15 – 17:00)

**[Screen: Navigate to Tx History page]**

> Transaction History has two tabs. The Tracked tab shows transactions you've made from this wallet, with live status updates — Pending, Confirmed, or Failed.

**[Action: Expand a transaction row]**

> Expand any transaction to see full details, raw data, and a Repeat Transaction button to re-send with the same parameters.

**[Screen: All Transactions tab]**

> The All Transactions tab shows your complete history synced from the network — both sent and received. Filter by direction, type, or tick range.

**[Screen: Navigate to Log Events page]**

> Log Events shows contract-level events for your identity — detailed logs synced from Bob, with type badges, amounts, and raw body data.

---

## CHAPTER 16: Sign & Verify (17:00 – 17:30)

**[Screen: Navigate to Sign / Verify page]**

> Sign & Verify lets you cryptographically sign messages with your seed and verify signatures from others.

**[Action: Type a message, click Sign]**

> Type a message and click Sign. The wallet produces a JSON object with your identity, the message, and the signature. Share this to prove you control the identity.

**[Action: Paste signed JSON, click Verify]**

> To verify, paste the signed JSON and click Verify. The wallet checks the signature and tells you if it's valid.

---

## CHAPTER 17: Settings (17:30 – 18:30)

**[Screen: Navigate to Settings page]**

> Settings is organized into tabs.

**[Screen: General tab]**

> **General** — switch between Light and Dark theme.

**[Screen: Connection tab]**

> **Connection** — configure your default backend, RPC URL, Bob URL, and direct node address.

**[Screen: Transactions tab]**

> **Transactions** — adjust the auto-tick offset and enable auto-resend for failed transactions.

**[Screen: Labels tab]**

> **Labels** — toggle remote label resolution, which shows human-readable names for known addresses like exchanges and contracts.

**[Screen: Data tab]**

> **Data** — export or import your wallet database. The database is encrypted with your seed, so only you can read it.

**[Screen: Vault tab]**

> **Vault** — manage your encrypted vault file, change its location, update the password, or disconnect.

---

## CHAPTER 18: Security Best Practices (18:30 – 19:15)

**[Screen: Title card — "Security"]**

> A few important security notes:
>
> - Your seed is held **in memory only** during the session. It is never written to disk unencrypted.
> - The encrypted vault uses AES-256-GCM with Argon2id key derivation — the gold standard for password-based encryption.
> - Your local database is encrypted with SQLCipher, keyed to your seed.
> - In server mode, access is protected by a one-time session token. But prefer desktop mode when possible — server mode uses unencrypted HTTP on localhost.
> - **Never share your seed.** Qubic will never ask for it. Anyone with your seed has full control of your funds.

---

## OUTRO (19:15 – 19:45)

**[Screen: Wallet dashboard / closing card]**

> That covers everything in the Qubic.Net Wallet — from basic sends and receives to encrypted vaults, DEX trading, staking, multi-sig, and governance.
>
> The wallet is open source and available on GitHub. If you have questions or want to contribute, check the repository.
>
> Thanks for watching, and welcome to Qubic.
