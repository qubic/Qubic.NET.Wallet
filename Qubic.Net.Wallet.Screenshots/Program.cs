using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Qubic.Net.Wallet.Screenshots;

class Program
{
    static string _outputDir = "output";
    static string? _seed;       // funded demo seed (55 chars)
    static int _index = 0;
    static int _pace = 2500;       // delay after each action (typing, clicking)
    static int _holdScene = 5000;  // hold on important screens for narration
    static int _holdTab = 3000;    // hold on each tab
    static int _chapterGap = 4000; // pause between chapters for transition
    static Stopwatch _clock = Stopwatch.StartNew();
    static List<(TimeSpan Time, string Chapter, string File)> _log = new();

    static bool HasSeed => _seed is { Length: 55 };

    // ── Episode definitions ──────────────────────────────────────────────
    record Episode(int Number, string Name, string Slug, bool NeedsSeed, bool NeedsConnect,
                   Func<IPage, Task>[] Chapters);

    static Episode[] BuildEpisodes() => new Episode[]
    {
        new(1, "Getting Started",        "01_getting_started",    false, false, new Func<IPage, Task>[]
            { Chapter01_EmptyState, Chapter02_SeedSetup, Chapter03_Connect, Chapter04_Dashboard }),
        new(2, "Sending & Receiving",    "02_sending_receiving",  true,  true,  new Func<IPage, Task>[]
            { Chapter05_Send, Chapter06_SendToMany, Chapter07_Receive }),
        new(3, "Encrypted Vault",        "03_encrypted_vault",    true,  false, new Func<IPage, Task>[]
            { Chapter19_Vault }),
        new(4, "Assets & QX Trading",    "04_assets_qx",          true,  true,  new Func<IPage, Task>[]
            { Chapter08_Assets, Chapter09_QxTrading }),
        new(5, "DeFi Suite",             "05_defi",               true,  true,  new Func<IPage, Task>[]
            { Chapter10_QSwap, Chapter11_QEarn, Chapter12_MSVault }),
        new(6, "Governance & Auctions",  "06_governance",         true,  true,  new Func<IPage, Task>[]
            { Chapter13_Voting, Chapter14_ScAuctions }),
        new(7, "History & Monitoring",   "07_history",            true,  true,  new Func<IPage, Task>[]
            { Chapter15_TxHistory, Chapter16_LogEvents }),
        new(8, "Tools & Settings",       "08_tools_settings",     true,  false, new Func<IPage, Task>[]
            { Chapter17_SignVerify, Chapter18_Settings }),
    };

    static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        _outputDir = GetArg(args, "--output") ?? "output";
        _seed = GetArg(args, "--seed");
        if (int.TryParse(GetArg(args, "--pace"), out var p)) _pace = p;
        if (int.TryParse(GetArg(args, "--hold"), out var h)) _holdScene = h;
        if (int.TryParse(GetArg(args, "--hold-tab"), out var ht)) _holdTab = ht;
        if (int.TryParse(GetArg(args, "--chapter-gap"), out var cg)) _chapterGap = cg;
        var headless = args.Contains("--headless");
        var skipVideo = args.Contains("--no-video");
        var noCursor = args.Contains("--no-cursor");
        var remoteUrl = GetArg(args, "--url");
        var episodeArg = GetArg(args, "--episode");

        if (_seed != null && _seed.Length != 55)
        {
            Console.Error.WriteLine($"Error: --seed must be exactly 55 characters (got {_seed.Length}).");
            return 1;
        }

        // Parse --episode
        var allEpisodes = BuildEpisodes();
        List<Episode> selectedEpisodes;

        if (episodeArg == null || episodeArg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            selectedEpisodes = allEpisodes.ToList();
        }
        else
        {
            selectedEpisodes = new();
            foreach (var part in episodeArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var num) && num >= 1 && num <= allEpisodes.Length)
                    selectedEpisodes.Add(allEpisodes[num - 1]);
                else
                {
                    Console.Error.WriteLine($"Error: Invalid episode '{part}'. Use 1-{allEpisodes.Length}, comma-separated, or 'all'.");
                    return 1;
                }
            }
        }

        Directory.CreateDirectory(_outputDir);

        // Install Playwright browsers
        Console.WriteLine("Installing Playwright browsers...");
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0) { Console.Error.WriteLine("Failed to install browsers."); return 1; }

        // Determine auth URL — either from --url or by launching the wallet
        string authUrl;
        Process? walletProcess = null;

        if (remoteUrl != null)
        {
            authUrl = remoteUrl;
            Console.WriteLine($"Connecting to: {authUrl}");
        }
        else
        {
            var walletProject = Path.GetFullPath(
                args.FirstOrDefault(a => !a.StartsWith("--")) ??
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Qubic.Net.Wallet"));

            Console.WriteLine($"Starting wallet: {walletProject}");
            (walletProcess, var parsedUrl) = await StartWallet(walletProject);
            if (walletProcess == null || parsedUrl == null)
            {
                Console.Error.WriteLine("Failed to start wallet or parse auth URL.");
                return 1;
            }
            authUrl = parsedUrl;
            Console.WriteLine($"Wallet running at: {authUrl}");
        }

        Console.WriteLine($"Mode: {(HasSeed ? "LIVE (funded seed)" : "DEMO (generated seed)")}");
        Console.WriteLine($"Timing: pace={_pace}ms, hold={_holdScene}ms, hold-tab={_holdTab}ms, chapter-gap={_chapterGap}ms");

        var isSingleMonolith = episodeArg == null;
        Console.WriteLine(isSingleMonolith
            ? "Recording: single monolith video (all chapters)"
            : $"Recording: {selectedEpisodes.Count} episode(s) — {string.Join(", ", selectedEpisodes.Select(e => $"Ep{e.Number}"))}");

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = headless,
                SlowMo = headless ? 0 : 50,
            });

            if (isSingleMonolith)
            {
                // ── Original mode: single context, all chapters, one video ──
                await RunMonolith(browser, authUrl, skipVideo, noCursor);
            }
            else
            {
                // ── Episode mode: separate context (and video) per episode ──
                foreach (var ep in selectedEpisodes)
                {
                    _index = 0; // reset screenshot index per episode
                    _clock.Restart();
                    _log.Clear();

                    Console.WriteLine();
                    Console.WriteLine($"═══ Episode {ep.Number}: {ep.Name} ═══");

                    var epOutputDir = Path.Combine(_outputDir, ep.Slug);
                    Directory.CreateDirectory(epOutputDir);
                    var prevOutputDir = _outputDir;
                    _outputDir = epOutputDir;

                    var contextOptions = new BrowserNewContextOptions
                    {
                        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                        ColorScheme = ColorScheme.Dark,
                    };
                    if (!skipVideo)
                    {
                        contextOptions.RecordVideoDir = Path.Combine(epOutputDir, "video");
                        contextOptions.RecordVideoSize = new RecordVideoSize { Width = 1920, Height = 1080 };
                    }

                    await using var context = await browser.NewContextAsync(contextOptions);
                    var page = await context.NewPageAsync();

                    // Authenticate
                    await page.GotoAsync(authUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
                    try { await page.WaitForURLAsync("**/", new() { Timeout = 10000 }); }
                    catch { /* may already be on / */ }
                    await WaitForBlazor(page);

                    if (!noCursor) await InjectCursorHighlight(page);

                    // ── Episode setup: enter seed and/or connect if needed ──
                    if (ep.NeedsSeed && ep.Number != 1)
                    {
                        await SetupSeed(page);
                    }
                    if (ep.NeedsConnect)
                    {
                        await ConnectToNetwork(page);
                    }
                    // Vault episode needs seed + navigate to Settings/Vault
                    if (ep.Number == 3)
                    {
                        await Nav(page, "settings");
                        await ClickTab(page, "Vault");
                    }

                    // ── Run episode chapters ──
                    foreach (var chapter in ep.Chapters)
                    {
                        await chapter(page);
                    }

                    // Close context to finalize video
                    await context.CloseAsync();

                    // Write per-episode timing log
                    var logPath = Path.Combine(epOutputDir, "chapters.log");
                    await File.WriteAllLinesAsync(logPath,
                        _log.Select(e => $"[{e.Time:mm\\:ss}] {e.Chapter,-30} → {e.File}"));

                    Console.WriteLine($"  Episode {ep.Number} done — {_index} screenshots in {epOutputDir}/");

                    _outputDir = prevOutputDir;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"All done! Output saved to {Path.GetFullPath(_outputDir)}/");
        }
        finally
        {
            if (walletProcess is { HasExited: false })
            {
                walletProcess.Kill(entireProcessTree: true);
                walletProcess.Dispose();
            }
        }
        return 0;
    }

    /// <summary>Run all chapters as a single monolith video (original behavior when no --episode is specified).</summary>
    static async Task RunMonolith(IBrowser browser, string authUrl, bool skipVideo, bool noCursor)
    {
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            ColorScheme = ColorScheme.Dark,
        };
        if (!skipVideo)
        {
            contextOptions.RecordVideoDir = Path.Combine(_outputDir, "video");
            contextOptions.RecordVideoSize = new RecordVideoSize { Width = 1920, Height = 1080 };
        }

        await using var context = await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();

        await page.GotoAsync(authUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        try { await page.WaitForURLAsync("**/", new() { Timeout = 10000 }); }
        catch { }
        await WaitForBlazor(page);

        if (!noCursor) await InjectCursorHighlight(page);

        await Chapter01_EmptyState(page);
        await Chapter02_SeedSetup(page);
        await Chapter03_Connect(page);
        await Chapter04_Dashboard(page);
        await Chapter05_Send(page);
        await Chapter06_SendToMany(page);
        await Chapter07_Receive(page);
        await Chapter08_Assets(page);
        await Chapter09_QxTrading(page);
        await Chapter10_QSwap(page);
        await Chapter11_QEarn(page);
        await Chapter12_MSVault(page);
        await Chapter13_Voting(page);
        await Chapter14_ScAuctions(page);
        await Chapter15_TxHistory(page);
        await Chapter16_LogEvents(page);
        await Chapter17_SignVerify(page);
        await Chapter18_Settings(page);
        await Chapter19_Vault(page);

        await context.CloseAsync();

        var logPath = Path.Combine(_outputDir, "chapters.log");
        await File.WriteAllLinesAsync(logPath,
            _log.Select(e => $"[{e.Time:mm\\:ss}] {e.Chapter,-30} → {e.File}"));

        Console.WriteLine();
        Console.WriteLine($"Done! {_index} screenshots saved to {Path.GetFullPath(_outputDir)}/");
        Console.WriteLine($"Timing log: {logPath}");
        if (!skipVideo)
            Console.WriteLine($"Video saved to {Path.GetFullPath(Path.Combine(_outputDir, "video"))}/");
    }

    /// <summary>Set up seed for episodes that start with a seed already active.</summary>
    static async Task SetupSeed(IPage page)
    {
        if (HasSeed)
        {
            await EnterSeedInTopBar(page, _seed!);
        }
        else
        {
            // Generate and use a random seed
            await page.ClickAsync("button:has-text('Generate New Seed')");
            await Task.Delay(500);
            await page.CheckAsync("#seedSavedCheck");
            await Task.Delay(300);
            await page.ClickAsync("button:has-text('Use This Seed')");
            await Task.Delay(500);
            await WaitForBlazor(page);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("""
            Qubic.Net Wallet — Screenshot & Video Automation

            Usage:
              Qubic.Net.Wallet.Screenshots [options]

            Options:
              --url <url>          Connect to an already-running wallet (e.g. http://host:port?token=xxx)
              --seed <seed>        55-char funded seed for live transaction demos
              --output <dir>       Output directory for screenshots and video (default: output)
              --episode <spec>     Record specific episodes instead of one monolith video.
                                   Values: all, or comma-separated numbers (1-8).
                                   Omit this flag to record a single continuous video.
              --pace <ms>          Delay after each action — click, type (default: 2500)
              --hold <ms>          Hold time on important screens for narration (default: 5000)
              --hold-tab <ms>      Hold time on each tab within a page (default: 3000)
              --chapter-gap <ms>   Pause between chapters for transitions (default: 4000)
              --headless           Run browser without visible window
              --no-video           Skip video recording, only take screenshots
              --no-cursor          Disable cursor highlight overlay
              -h, --help           Show this help

            Episodes:
              1  Getting Started       — Empty state, seed setup, connect, dashboard
              2  Sending & Receiving   — Send QU, Send to Many, Receive/QR
              3  Encrypted Vault       — Create vault, manage seeds, address book
              4  Assets & QX Trading   — Asset portfolio, order book, Issue/Transfer/Ask/Bid
              5  DeFi Suite            — QSwap, QEarn staking, MSVault multi-sig
              6  Governance & Auctions — Voting, SC Auctions
              7  History & Monitoring  — Transaction history, log events
              8  Tools & Settings      — Sign/Verify, settings tabs

            Examples:
              # Single monolith video (default — no --episode flag):
              Qubic.Net.Wallet.Screenshots --url "http://host:port?token=xxx" --seed "abcde..."

              # All episodes as separate videos:
              Qubic.Net.Wallet.Screenshots --url "http://..." --seed "abc..." --episode all

              # Just episode 1 (Getting Started):
              Qubic.Net.Wallet.Screenshots --url "http://..." --episode 1

              # Episodes 2 and 3:
              Qubic.Net.Wallet.Screenshots --url "http://..." --seed "abc..." --episode 2,3

              # Fast screenshots only:
              Qubic.Net.Wallet.Screenshots --url "http://..." --no-video --pace 500 --hold 500
            """);
    }

    // ── Wallet process management ──────────────────────────────────────

    static async Task<(Process?, string?)> StartWallet(string projectPath)
    {
        string fileName, arguments;
        if (projectPath.EndsWith(".exe") || (File.Exists(projectPath) && !projectPath.EndsWith(".csproj")))
        {
            fileName = projectPath;
            arguments = "--server";
        }
        else
        {
            fileName = "dotnet";
            arguments = $"run --project \"{projectPath}\" -- --server";
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName, Arguments = arguments,
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            Environment = { ["DOTNET_ENVIRONMENT"] = "Development" }
        };
        var process = Process.Start(psi);
        if (process == null) return (null, null);
        var authUrl = await ReadAuthUrl(process, TimeSpan.FromSeconds(60));
        return (process, authUrl);
    }

    static async Task<string?> ReadAuthUrl(Process process, TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        var urlPattern = new Regex(@"Open in browser:\s*(http\S+)");
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cts.Token);
                if (line == null) break;
                Console.WriteLine($"  [wallet] {line}");
                var match = urlPattern.Match(line);
                if (match.Success) return match.Groups[1].Value;
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    static async Task Screenshot(IPage page, string chapter, string name)
    {
        var fileName = $"{_index:D3}_{chapter}_{name}.png";
        var filePath = Path.Combine(_outputDir, fileName);
        await page.ScreenshotAsync(new() { Path = filePath, FullPage = false });
        _log.Add((_clock.Elapsed, $"{chapter}/{name}", fileName));
        Console.WriteLine($"  [{_index:D3}] [{_clock.Elapsed:mm\\:ss}] {chapter}/{name}");
        _index++;
    }

    static async Task ScreenshotAndHold(IPage page, string chapter, string name, int? holdMs = null)
    {
        await Screenshot(page, chapter, name);
        await Task.Delay(holdMs ?? _holdScene);
    }

    static async Task Nav(IPage page, string href)
    {
        await page.ClickAsync($"a.nav-link[href='/{href}']");
        await Pace();
        await WaitForBlazor(page);
    }

    static async Task ClickTab(IPage page, string tabText)
    {
        await page.ClickAsync($"ul.nav-tabs a.nav-link:has-text('{tabText}')");
        await Pace();
    }

    static async Task ShowTab(IPage page, string chapter, string tabText)
    {
        await ClickTab(page, tabText);
        await Screenshot(page, chapter, $"tab_{tabText.ToLower().Replace(" ", "_")}");
        await Task.Delay(_holdTab);
    }

    static Task ChapterBreak() => Task.Delay(_chapterGap);
    static Task Pace() => Task.Delay(_pace);

    /// <summary>Injects a click ripple indicator into the page, captured by video recording.</summary>
    static async Task InjectCursorHighlight(IPage page)
    {
        await page.EvaluateAsync("""
            () => {
                if (document.getElementById('pw-click-style')) return;

                // ── Click ripple only ──
                document.addEventListener('click', e => {
                    const ripple = document.createElement('div');
                    Object.assign(ripple.style, {
                        position: 'fixed', zIndex: '999998', pointerEvents: 'none',
                        left: e.clientX + 'px', top: e.clientY + 'px',
                        width: '0', height: '0', borderRadius: '50%',
                        border: '2.5px solid rgba(255, 75, 75, 0.8)',
                        transform: 'translate(-50%, -50%)',
                        animation: 'pw-ripple 0.5s ease-out forwards'
                    });
                    document.body.appendChild(ripple);
                    setTimeout(() => ripple.remove(), 600);
                }, true);

                // Ripple animation
                const style = document.createElement('style');
                style.id = 'pw-click-style';
                style.textContent = `
                    @keyframes pw-ripple {
                        0%   { width: 0;    height: 0;    opacity: 1; border-width: 2.5px; }
                        100% { width: 60px; height: 60px; opacity: 0; border-width: 1px; }
                    }
                `;
                document.head.appendChild(style);
            }
        """);
    }

    static async Task WaitForBlazor(IPage page)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "() => document.querySelector('.blazor-error-ui')?.style.display !== 'block'",
                null, new() { Timeout = 5000 });
        }
        catch { }
        await Task.Delay(300);
    }

    static string? GetArg(string[] args, string key)
    {
        var idx = Array.IndexOf(args, key);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    /// <summary>Enter the seed in the top-bar input and click Unlock.</summary>
    static async Task EnterSeedInTopBar(IPage page, string seed)
    {
        var seedInput = page.Locator("input[placeholder*='Enter 55-char seed']").First;
        await seedInput.WaitForAsync(new() { Timeout = 5000 });
        await seedInput.FillAsync(seed);
        await Pace();
        var unlockBtn = page.Locator("button:has-text('Unlock')").First;
        await unlockBtn.ClickAsync();
        await Pace();
        await WaitForBlazor(page);
    }

    /// <summary>Click Connect in the top bar and wait for the epoch/tick badge.</summary>
    static async Task ConnectToNetwork(IPage page)
    {
        var connectBtn = page.Locator("button:has-text('Connect')").First;
        if (await connectBtn.IsVisibleAsync())
        {
            await connectBtn.ClickAsync();
            // Wait for connection (epoch badge appears)
            try
            {
                await page.Locator("span.badge-qb-success.mono").WaitForAsync(
                    new() { Timeout = 15000 });
            }
            catch { /* may not connect in time — continue anyway */ }
            await Pace();
        }
    }

    // ── Chapters ────────────────────────────────────────────────────────

    static async Task Chapter01_EmptyState(IPage page)
    {
        // Narrator: "This is the Qubic.Net Wallet. On the left is the navigation sidebar..."
        await ScreenshotAndHold(page, "01_intro", "home_empty");
        await ChapterBreak();
    }

    static async Task Chapter02_SeedSetup(IPage page)
    {
        if (HasSeed)
        {
            // ── Live mode: show seed generation briefly, then enter the funded seed ──

            // Show "Generate New Seed" feature
            await page.ClickAsync("button:has-text('Generate New Seed')");
            await Pace();
            await ScreenshotAndHold(page, "02_seed", "generated", 3000);

            // Reveal it
            var eyeBtn = page.Locator("button[title='Reveal']").First;
            if (await eyeBtn.IsVisibleAsync())
            {
                await eyeBtn.ClickAsync();
                await Pace();
                // Narrator: "The wallet can generate a new seed. Write it down and store it safely."
                await ScreenshotAndHold(page, "02_seed", "revealed");
            }

            // Now enter the real funded seed in the top bar instead
            // First, click "Generate Another" or navigate home to reset, then use top bar
            // Simplest: just enter the seed in the top-bar input directly
            // The top-bar seed input is always visible when no seed is active
            // But we just clicked "Generate" which shows the generated seed panel...
            // The top-bar input should still be there since we didn't click "Use This Seed"

            // Narrator: "For this demo, we'll use a funded seed to show live transactions."
            await EnterSeedInTopBar(page, _seed!);
            await ScreenshotAndHold(page, "02_seed", "funded_active", 3000);
        }
        else
        {
            // ── Demo mode: generate and use a random seed ──
            await page.ClickAsync("button:has-text('Generate New Seed')");
            await Pace();
            await ScreenshotAndHold(page, "02_seed", "generated");

            var eyeBtn = page.Locator("button[title='Reveal']").First;
            if (await eyeBtn.IsVisibleAsync())
            {
                await eyeBtn.ClickAsync();
                await Pace();
                await ScreenshotAndHold(page, "02_seed", "revealed");
            }

            await page.CheckAsync("#seedSavedCheck");
            await Pace();
            await ScreenshotAndHold(page, "02_seed", "confirmed", 3000);

            await page.ClickAsync("button:has-text('Use This Seed')");
            await Pace();
            await WaitForBlazor(page);
        }

        await ChapterBreak();
    }

    static async Task Chapter03_Connect(IPage page)
    {
        // Narrator: "Select a backend and click Connect to reach the Qubic network."
        await ScreenshotAndHold(page, "03_connect", "before", 2000);

        await ConnectToNetwork(page);

        // Narrator: "The epoch and tick appear in the top bar — you're connected."
        await ScreenshotAndHold(page, "03_connect", "connected");
        await ChapterBreak();
    }

    static async Task Chapter04_Dashboard(IPage page)
    {
        await page.ClickAsync("a.nav-link[href='/']");
        await Pace();
        await WaitForBlazor(page);

        if (HasSeed)
        {
            // Click "Load Balance" to fetch the real balance
            var loadBtn = page.Locator("button:has-text('Load Balance')").First;
            if (await loadBtn.IsVisibleAsync())
            {
                await loadBtn.ClickAsync();
                await Task.Delay(3000); // wait for balance RPC call
                await WaitForBlazor(page);
            }
        }

        // Narrator: "The dashboard shows your identity, balance, quick actions, assets, and open orders."
        await ScreenshotAndHold(page, "04_dashboard", "overview");
        await ChapterBreak();
    }

    static async Task Chapter05_Send(IPage page)
    {
        await Nav(page, "send");
        await ScreenshotAndHold(page, "05_send", "empty_form");

        if (HasSeed)
        {
            // ── Live: send 1 QU to self ──
            // Get own identity from the top bar badge
            var identityBadge = page.Locator("span.badge-qb-cyan.mono").First;
            string? selfIdentity = null;
            if (await identityBadge.IsVisibleAsync())
            {
                // Copy identity via the copy button, then read from clipboard
                // Alternatively, read the tooltip or full text
                // Simpler: use page.EvaluateAsync to read from the DOM
                selfIdentity = await page.EvaluateAsync<string?>(
                    "() => document.querySelector('.qb-seed-bar code')?.textContent?.trim()");
            }

            var destInput = page.Locator("input.mono[placeholder*='IDENTITY'], input.mono[placeholder*='identity']").First;
            if (await destInput.IsVisibleAsync() && selfIdentity != null)
            {
                // Narrator: "Let's send 1 QU to our own address to demonstrate the full flow."
                await destInput.FillAsync(selfIdentity);
                await Pace();
            }

            var amountInput = page.Locator("input[type='number']").First;
            if (await amountInput.IsVisibleAsync())
            {
                await amountInput.FillAsync("1");
                await Pace();
            }

            await ScreenshotAndHold(page, "05_send", "filled_live");

            // Click Preview
            var previewBtn = page.Locator("button:has-text('Preview Transaction')").First;
            if (await previewBtn.IsVisibleAsync())
            {
                await previewBtn.ClickAsync();
                await Pace();
                await WaitForBlazor(page);
                // Narrator: "Preview shows the destination, amount, tick, and fee."
                await ScreenshotAndHold(page, "05_send", "preview");
            }

            // Click "Sign & Broadcast"
            var signBtn = page.Locator("button:has-text('Sign & Broadcast')").First;
            if (await signBtn.IsVisibleAsync())
            {
                await signBtn.ClickAsync();
                await Task.Delay(3000); // wait for broadcast
                await WaitForBlazor(page);
                // Narrator: "The transaction is signed locally and broadcast to the network."
                await ScreenshotAndHold(page, "05_send", "broadcast_success");
            }
        }
        else
        {
            // ── Demo: fill form without sending ──
            var destInput = page.Locator("input.mono[placeholder*='IDENTITY'], input.mono[placeholder*='identity']").First;
            if (await destInput.IsVisibleAsync())
            {
                await destInput.FillAsync("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
                await Pace();
            }
            var amountInput = page.Locator("input[type='number']").First;
            if (await amountInput.IsVisibleAsync())
            {
                await amountInput.FillAsync("1000");
                await Pace();
            }
            await ScreenshotAndHold(page, "05_send", "filled_form");
        }

        await ChapterBreak();
    }

    static async Task Chapter06_SendToMany(IPage page)
    {
        await Nav(page, "send-many");
        await ScreenshotAndHold(page, "06_send_many", "empty");

        // Add recipients
        for (int i = 0; i < 2; i++)
        {
            var addBtn = page.Locator("button:has-text('Add Recipient')");
            if (await addBtn.IsVisibleAsync())
            {
                await addBtn.ClickAsync();
                await Task.Delay(500);
            }
        }

        if (HasSeed)
        {
            // ── Live: fill real addresses and amounts, then send ──
            // Get own identity
            var selfIdentity = await page.EvaluateAsync<string?>(
                "() => document.querySelector('.qb-seed-bar code')?.textContent?.trim()");

            // Fill 3 recipient rows (they start at index 0)
            var addrInputs = page.Locator("input.mono[placeholder*='IDENTITY'], input.mono[placeholder*='identity']");
            var amtInputs = page.Locator("input[type='number'][placeholder*='Amount'], input[type='number'][min='0']");
            var addrCount = await addrInputs.CountAsync();

            for (int i = 0; i < Math.Min(addrCount, 3); i++)
            {
                if (selfIdentity != null)
                    await addrInputs.Nth(i).FillAsync(selfIdentity);
                await amtInputs.Nth(i).FillAsync("1");
                await Task.Delay(300);
            }
            await Pace();

            // Narrator: "We'll send 1 QU to three recipients using a single QUTIL transaction."
            await ScreenshotAndHold(page, "06_send_many", "filled_live");

            // Sign & Broadcast
            var signBtn = page.Locator("button:has-text('Sign & Broadcast')").First;
            if (await signBtn.IsVisibleAsync() && await signBtn.IsEnabledAsync())
            {
                await signBtn.ClickAsync();
                await Task.Delay(3000);
                await WaitForBlazor(page);
                // Narrator: "The batch transaction is broadcast to the network."
                await ScreenshotAndHold(page, "06_send_many", "broadcast_success");
            }
        }
        else
        {
            await ScreenshotAndHold(page, "06_send_many", "recipients_added");

            var importBtn = page.Locator("button:has-text('Import from Text')");
            if (await importBtn.IsVisibleAsync())
            {
                await importBtn.ClickAsync();
                await Pace();
                await ScreenshotAndHold(page, "06_send_many", "import_text");
                var cancelBtn = page.Locator("button:has-text('Cancel')").First;
                if (await cancelBtn.IsVisibleAsync())
                    await cancelBtn.ClickAsync();
            }
        }

        await ChapterBreak();
    }

    static async Task Chapter07_Receive(IPage page)
    {
        await Nav(page, "receive");
        await Pace();
        // Narrator: "The Receive page shows your QR code and copyable address."
        await ScreenshotAndHold(page, "07_receive", "qr_code");
        await ChapterBreak();
    }

    static async Task Chapter08_Assets(IPage page)
    {
        await Nav(page, "assets");
        await ScreenshotAndHold(page, "08_assets", "overview");
        await ChapterBreak();
    }

    static async Task Chapter09_QxTrading(IPage page)
    {
        await Nav(page, "qx");
        await ScreenshotAndHold(page, "09_qx", "overview");
        foreach (var tab in new[] { "Issue", "Transfer", "Ask", "Bid", "Rights" })
            await ShowTab(page, "09_qx", tab);
        await ChapterBreak();
    }

    static async Task Chapter10_QSwap(IPage page)
    {
        await Nav(page, "qswap");
        await ScreenshotAndHold(page, "10_qswap", "overview");
        foreach (var tab in new[] { "Issue", "Transfer", "Pool", "Liquidity", "Swap" })
            await ShowTab(page, "10_qswap", tab);
        await ChapterBreak();
    }

    static async Task Chapter11_QEarn(IPage page)
    {
        await Nav(page, "qearn");
        await ScreenshotAndHold(page, "11_qearn", "overview");
        foreach (var tab in new[] { "Lock", "Query" })
            await ShowTab(page, "11_qearn", tab);
        await ChapterBreak();
    }

    static async Task Chapter12_MSVault(IPage page)
    {
        await Nav(page, "msvault");
        await ScreenshotAndHold(page, "12_msvault", "overview");
        foreach (var tab in new[] { "Register", "Deposit", "Release", "Assets", "Query" })
            await ShowTab(page, "12_msvault", tab);
        await ChapterBreak();
    }

    static async Task Chapter13_Voting(IPage page)
    {
        await Nav(page, "voting");
        await ScreenshotAndHold(page, "13_voting", "overview");
        foreach (var tab in new[] { "Browse", "Vote", "Results", "Create" })
            await ShowTab(page, "13_voting", tab);
        await ChapterBreak();
    }

    static async Task Chapter14_ScAuctions(IPage page)
    {
        await Nav(page, "sc-auctions");
        await ScreenshotAndHold(page, "14_sc_auctions", "overview");
        foreach (var tab in new[] { "Browse", "Status", "Bid" })
            await ShowTab(page, "14_sc_auctions", tab);
        await ChapterBreak();
    }

    static async Task Chapter15_TxHistory(IPage page)
    {
        await Nav(page, "history");

        if (HasSeed)
        {
            // Narrator: "Our transactions appear here — you can track their status in real time."
            await ScreenshotAndHold(page, "15_history", "tracked_with_tx");

            // Check if any row shows "Pending" or "Confirmed"
            var statusBadge = page.Locator("span.badge:has-text('Pending'), span.badge:has-text('Confirmed')").First;
            if (await statusBadge.IsVisibleAsync())
            {
                // Narrator: "Each transaction shows its status — Pending, Confirmed, or Failed."
                await ScreenshotAndHold(page, "15_history", "tx_status");
            }

            // Expand a transaction row to show details
            var detailsBtn = page.Locator("button:has-text('Details')").First;
            if (await detailsBtn.IsVisibleAsync())
            {
                await detailsBtn.ClickAsync();
                await Pace();
                // Narrator: "Expand any transaction to see full details, raw data, and a Repeat button."
                await ScreenshotAndHold(page, "15_history", "tx_expanded");
            }
        }
        else
        {
            await ScreenshotAndHold(page, "15_history", "tracked");
        }

        var allTab = page.Locator("ul.nav-tabs a.nav-link:has-text('All Transactions')");
        if (await allTab.IsVisibleAsync())
        {
            await allTab.ClickAsync();
            await Pace();
            await ScreenshotAndHold(page, "15_history", "all_transactions");
        }

        await ChapterBreak();
    }

    static async Task Chapter16_LogEvents(IPage page)
    {
        await Nav(page, "logs");
        await ScreenshotAndHold(page, "16_logs", "overview");
        await ChapterBreak();
    }

    static async Task Chapter17_SignVerify(IPage page)
    {
        await Nav(page, "sign-verify");
        await ScreenshotAndHold(page, "17_sign_verify", "empty");

        var msgInput = page.Locator("textarea").First;
        if (await msgInput.IsVisibleAsync())
        {
            await msgInput.FillAsync("Hello, Qubic! This is a signed message.");
            await Pace();
            await ScreenshotAndHold(page, "17_sign_verify", "message_entered", 3000);

            var signBtn = page.Locator("button:has-text('Sign')").First;
            if (await signBtn.IsVisibleAsync())
            {
                await signBtn.ClickAsync();
                await Pace();
                await WaitForBlazor(page);
                await ScreenshotAndHold(page, "17_sign_verify", "signed");
            }
        }
        await ChapterBreak();
    }

    static async Task Chapter18_Settings(IPage page)
    {
        await Nav(page, "settings");
        await ScreenshotAndHold(page, "18_settings", "general");
        foreach (var tab in new[] { "Connection", "Transactions", "Labels", "Data", "Vault" })
            await ShowTab(page, "18_settings", tab);
        await ChapterBreak();
    }

    static async Task Chapter19_Vault(IPage page)
    {
        // We're on Settings/Vault tab from the previous chapter

        // ── Step 1: Create vault with the current seed ──
        var pathInput = page.Locator("input[placeholder*='Path to save vault file']").First;
        if (!await pathInput.IsVisibleAsync()) return;

        var vaultPath = Path.Combine(Path.GetTempPath(), "qubic-demo-vault.dat");
        await pathInput.FillAsync(vaultPath);
        await Pace();

        // Fill seed — use the funded seed if provided, otherwise a dummy
        var seedInput = page.Locator("input[placeholder*='Enter 55-char seed']").First;
        if (await seedInput.IsVisibleAsync())
            await seedInput.FillAsync(_seed ?? "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabc");
        await Pace();

        var labelInput = page.Locator("input[placeholder*='e.g. Main']").First;
        if (await labelInput.IsVisibleAsync())
            await labelInput.FillAsync(HasSeed ? "Main Wallet" : "Demo Wallet");
        await Pace();

        // Password + confirm (last two password fields)
        var pwInputs = page.Locator("input[type='password']");
        var count = await pwInputs.CountAsync();
        if (count >= 2)
        {
            await pwInputs.Nth(count - 2).FillAsync("DemoPassword123!");
            await Task.Delay(500);
            await pwInputs.Nth(count - 1).FillAsync("DemoPassword123!");
        }
        await Pace();

        // Narrator: "Enter a file path, seed, label, and a strong password..."
        await ScreenshotAndHold(page, "19_vault", "create_filled");

        var createBtn = page.Locator("button:has-text('Create Vault')").First;
        if (await createBtn.IsEnabledAsync())
        {
            await createBtn.ClickAsync();
            await Pace();
            await WaitForBlazor(page);
            await Task.Delay(2000);
            // Narrator: "The vault is created with AES-256-GCM encryption and Argon2id key derivation."
            await ScreenshotAndHold(page, "19_vault", "created");
        }

        // ── Step 2: Show the entry ──
        var entriesHeader = page.Locator("text=Vault Entries");
        if (await entriesHeader.IsVisibleAsync())
            await ScreenshotAndHold(page, "19_vault", "one_entry");

        // ── Step 3: Add a second seed ──
        var addSeedHeader = page.Locator("div.card-header:has-text('Add Seed to Vault')");
        if (await addSeedHeader.IsVisibleAsync())
        {
            await addSeedHeader.ScrollIntoViewIfNeededAsync();
            await Pace();

            var addSeedInput = page.Locator("input[placeholder*='Enter or generate a 55-char seed']").First;

            if (HasSeed)
            {
                // For live mode: generate a fresh seed for the second entry
                var generateBtn = page.Locator("button:has-text('Generate')").First;
                if (await generateBtn.IsVisibleAsync())
                {
                    await generateBtn.ClickAsync();
                    await Pace();
                }
            }
            else
            {
                // Demo mode: also generate
                var generateBtn = page.Locator("button:has-text('Generate')").First;
                if (await generateBtn.IsVisibleAsync())
                {
                    await generateBtn.ClickAsync();
                    await Pace();
                }
            }

            // Reveal seed
            var revealBtn = page.Locator("button[title='Reveal']").First;
            if (await revealBtn.IsVisibleAsync())
            {
                await revealBtn.ClickAsync();
                await Pace();
            }

            // Label
            var addLabel = page.Locator("input[placeholder*='e.g. Trading']").First;
            if (await addLabel.IsVisibleAsync())
                await addLabel.FillAsync("Trading");
            await Pace();

            // Narrator: "Click Generate for a new seed, give it a label, and add it to the vault."
            await ScreenshotAndHold(page, "19_vault", "add_second_seed");

            var addEntryBtn = page.Locator("button:has-text('Add Entry')").First;
            if (await addEntryBtn.IsVisibleAsync() && await addEntryBtn.IsEnabledAsync())
            {
                await addEntryBtn.ClickAsync();
                await Pace();
                await WaitForBlazor(page);
                await Task.Delay(1000);
            }

            // Scroll to entries to show both
            if (await entriesHeader.IsVisibleAsync())
            {
                await entriesHeader.ScrollIntoViewIfNeededAsync();
                await Pace();
            }
            // Narrator: "The vault now holds two identities — switch between them from the top bar."
            await ScreenshotAndHold(page, "19_vault", "two_entries");
        }

        // ── Step 4: Add contacts ──
        var addContactHeader = page.Locator("div.card-header:has-text('Add Contact')");
        if (await addContactHeader.IsVisibleAsync())
        {
            await addContactHeader.ScrollIntoViewIfNeededAsync();
            await Pace();

            var contactLabel = page.Locator("input[placeholder*='e.g. Exchange']").First;
            var contactAddr = page.Locator("input[placeholder*='IDENTITY']").First;

            if (await contactLabel.IsVisibleAsync() && await contactAddr.IsVisibleAsync())
            {
                // Contact 1
                await contactLabel.FillAsync("Qearn Rewards");
                await Pace();
                await contactAddr.FillAsync("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
                await Pace();
                await ScreenshotAndHold(page, "19_vault", "add_contact_filled", 3000);

                var addContactBtn = page.Locator("button:has-text('Add Contact')").First;
                if (await addContactBtn.IsVisibleAsync())
                {
                    await addContactBtn.ClickAsync();
                    await Pace();
                    await WaitForBlazor(page);
                    await Task.Delay(500);
                }

                // Contact 2
                if (await contactLabel.IsVisibleAsync())
                    await contactLabel.FillAsync("Team Treasury");
                await Pace();
                if (await contactAddr.IsVisibleAsync())
                    await contactAddr.FillAsync("BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
                await Pace();

                addContactBtn = page.Locator("button:has-text('Add Contact')").First;
                if (await addContactBtn.IsVisibleAsync())
                {
                    await addContactBtn.ClickAsync();
                    await Pace();
                    await WaitForBlazor(page);
                    await Task.Delay(500);
                }
            }

            // Show address book
            var addressBookHeader = page.Locator("div.card-header:has-text('Address Book')");
            if (await addressBookHeader.IsVisibleAsync())
            {
                await addressBookHeader.ScrollIntoViewIfNeededAsync();
                await Pace();
                // Narrator: "Saved contacts appear as autocomplete in every address field across the app."
                await ScreenshotAndHold(page, "19_vault", "contacts_added");
            }
        }

        // ── Step 5: Change password section ──
        var changePwHeader = page.Locator("text=Change Vault Password");
        if (await changePwHeader.IsVisibleAsync())
        {
            await changePwHeader.ScrollIntoViewIfNeededAsync();
            await Pace();
            await ScreenshotAndHold(page, "19_vault", "change_password", 3000);
        }

        // Clean up
        var tempVault = Path.Combine(Path.GetTempPath(), "qubic-demo-vault.dat");
        if (File.Exists(tempVault))
            try { File.Delete(tempVault); } catch { }
    }
}
