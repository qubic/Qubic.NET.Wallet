using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Qubic.Services;
using Qubic.Services.Features;
using Qubic.Services.Networks;
using Qubic.Services.Signing;

namespace Qubic.Net.Wallet.LocalApi;

/// <summary>
/// Local JSON-RPC signing endpoint for scripts and tests (desktop only, opt-in feature).
/// Listens on 127.0.0.1 only, requires a bearer token, and offers the same method set as
/// WalletConnect (qubic_requestAccounts, qubic_sendQubic, qubic_signTransaction,
/// qubic_sendTransaction, qubic_sign, qubic_sendAsset) plus qubic_signRaw (raw message
/// convention) and qubic_status. Every signature goes through the signing review, except
/// public dev keys on a proven core-lite devnet when "auto-approve dev accounts" is enabled.
/// The URL and token are written to local-endpoint.json in the wallet's storage directory.
/// </summary>
public sealed class LocalSigningEndpoint : IAsyncDisposable
{
    private const string TokenKey = "localEndpoint.token";
    private const string PortKey = "localEndpoint.port";

    private readonly FeatureService _features;
    private readonly QubicSettingsService _settings;
    private readonly ExternalRequestService _external;
    private readonly NetworkProfileService _profiles;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private WebApplication? _app;
    private FileStream? _instanceLock;
    private CancellationTokenSource? _waitForLock;

    public string? Url { get; private set; }
    public string? LastError { get; private set; }
    public bool IsRunning => _app != null;
    /// <summary>Another wallet window on the same data folder runs the endpoint; this one takes over when it stops.</summary>
    public bool IsInOtherInstance { get; private set; }
    public int RequestCount { get; private set; }
    public DateTime? LastRequestUtc { get; private set; }

    public event Action? OnChanged;

    public LocalSigningEndpoint(FeatureService features, QubicSettingsService settings,
        ExternalRequestService external, NetworkProfileService profiles)
    {
        _features = features;
        _settings = settings;
        _external = external;
        _profiles = profiles;
        _features.OnChanged += f =>
        {
            if (f != WalletFeature.LocalSigningEndpoint) return;
            _ = _features.IsEnabled(f) ? StartAsync() : StopAsync();
        };
    }

    public string Token
    {
        get
        {
            var t = _settings.GetCustom<string>(TokenKey);
            if (string.IsNullOrEmpty(t)) { t = NewToken(); _settings.SetCustom(TokenKey, t); }
            return t;
        }
    }

    /// <summary>Fixed port (0 = random free port on each start).</summary>
    public int Port
    {
        get => _settings.GetCustom<int?>(PortKey) ?? 0;
        set => _settings.SetCustom(PortKey, Math.Clamp(value, 0, 65535));
    }

    public string ConnectionFile => Path.Combine(_settings.StorageDirectory, "local-endpoint.json");

    public async Task RegenerateTokenAsync()
    {
        _settings.SetCustom(TokenKey, NewToken());
        if (IsRunning) { await StopAsync(); await StartAsync(); }
        Raise();
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public async Task StartAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_app != null || !_features.IsEnabled(WalletFeature.LocalSigningEndpoint)) return;
            // One endpoint per data folder: local-endpoint.json can only describe one of them.
            if (!TryAcquireInstanceLock())
            {
                IsInOtherInstance = true;
                WaitForInstanceLock();
                return;
            }
            IsInOtherInstance = false;
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
            var app = builder.Build();
            app.MapPost("/", Handle);
            app.MapPost("/qubic", Handle);
            await app.StartAsync();
            _app = app;
            Url = app.Urls.FirstOrDefault();
            LastError = null;
            WriteConnectionFile();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _app = null;
            ReleaseInstanceLock();
        }
        finally
        {
            _lifecycle.Release();
            Raise();
        }
    }

    public async Task StopAsync()
    {
        CancelWaitForLock();
        await _lifecycle.WaitAsync();
        try
        {
            if (_app != null)
            {
                try { await _app.StopAsync(); } catch { }
                await _app.DisposeAsync();
                _app = null;
            }
            Url = null;
            IsInOtherInstance = false;
            // The connection file belongs to whichever window holds the lock.
            if (_instanceLock != null)
            {
                try { File.Delete(ConnectionFile); } catch { }
                ReleaseInstanceLock();
            }
        }
        finally
        {
            _lifecycle.Release();
            Raise();
        }
    }

    private bool TryAcquireInstanceLock()
    {
        if (_instanceLock != null) return true;
        try
        {
            Directory.CreateDirectory(_settings.StorageDirectory);
            _instanceLock = new FileStream(Path.Combine(_settings.StorageDirectory, "local-endpoint.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private void ReleaseInstanceLock()
    {
        try { _instanceLock?.Dispose(); } catch { }
        _instanceLock = null;
    }

    private void WaitForInstanceLock()
    {
        if (_waitForLock != null) return;
        var cts = _waitForLock = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(10), cts.Token); }
            catch (OperationCanceledException) { return; }
            if (Interlocked.CompareExchange(ref _waitForLock, null, cts) != cts) return;
            cts.Dispose();
            if (IsInOtherInstance && _features.IsEnabled(WalletFeature.LocalSigningEndpoint))
                await StartAsync();
        });
    }

    private void CancelWaitForLock()
    {
        var cts = Interlocked.Exchange(ref _waitForLock, null);
        if (cts == null) return;
        try { cts.Cancel(); cts.Dispose(); } catch { }
    }

    private void WriteConnectionFile()
    {
        try
        {
            Directory.CreateDirectory(_settings.StorageDirectory);
            var json = JsonSerializer.Serialize(new { url = Url, token = Token, pid = Environment.ProcessId });
            File.WriteAllText(ConnectionFile, json);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(ConnectionFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { /* the token is also shown in the UI */ }
    }

    private async Task Handle(HttpContext ctx)
    {
        // Only same-machine callers with the token; no CORS — browsers cannot call this.
        var auth = ctx.Request.Headers.Authorization.ToString();
        var expected = "Bearer " + Token;
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(auth), System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (ctx.Request.Headers.ContainsKey("Origin"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        JsonNode? id = null;
        object response;
        try
        {
            var body = await JsonNode.ParseAsync(ctx.Request.Body) as JsonObject
                ?? throw new ExternalRequestException(-32600, "Invalid request");
            id = body["id"]?.DeepClone();
            var method = body["method"]?.GetValue<string>() ?? throw new ExternalRequestException(-32600, "Missing method");
            var p = body["params"] switch
            {
                JsonObject o => o,
                JsonArray { Count: > 0 } a when a[0] is JsonObject first => first,
                _ => new JsonObject()
            };
            RequestCount++;
            LastRequestUtc = DateTime.UtcNow;
            Raise();
            var client = ctx.Request.Headers.UserAgent.ToString();
            var context = new SigningContext(SigningOrigin.LocalEndpoint, Describe(method),
                string.IsNullOrEmpty(client) ? "local client" : client, null);
            var result = await Dispatch(method, p, context, ctx.RequestAborted);
            response = new { jsonrpc = "2.0", id, result };
        }
        catch (ExternalRequestException ex)
        {
            response = new { jsonrpc = "2.0", id, error = new { code = ex.Code, message = ex.Message } };
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            response = new { jsonrpc = "2.0", id, error = new { code = ExternalRequestException.InvalidParams, message = ex.Message } };
        }
        catch (Exception ex)
        {
            response = new { jsonrpc = "2.0", id, error = new { code = ExternalRequestException.InternalError, message = ex.Message } };
        }
        await ctx.Response.WriteAsJsonAsync(response);
    }

    private async Task<object?> Dispatch(string method, JsonObject p, SigningContext context, CancellationToken ct)
    {
        switch (method)
        {
            case "qubic_status":
                return new
                {
                    network = _profiles.Active.Name,
                    chainId = _profiles.Active.ChainId,
                    accounts = _external.SignableAccounts().Select(a => new { address = a.Identity, name = a.Label })
                };
            case "qubic_requestAccounts":
            {
                var accounts = await _external.GetAccountsAsync(_external.SignableAccounts().Select(a => a.Identity), ct);
                return accounts.Select(a => new
                {
                    address = a.Address, name = a.Name, amount = a.Amount,
                    assets = a.Assets.Select(x => new { assetName = x.AssetName, issuerIdentity = x.IssuerIdentity, ownedAmount = x.OwnedAmount, managingContractIndex = x.ManagingContractIndex })
                });
            }
            case "qubic_sendQubic":
            {
                var amount = Long(p, "amount") ?? throw new ExternalRequestException(ExternalRequestException.InvalidParams, "amount is required.");
                if (amount <= 0) throw new ExternalRequestException(ExternalRequestException.InvalidParams, "amount must be positive.");
                var r = await _external.SendTransactionAsync(new ExternalTxParams(Str(p, "from") ?? "", Str(p, "to") ?? "", amount), context, ct);
                return new { transactionId = r.TransactionId, tick = r.Tick };
            }
            case "qubic_signTransaction":
            case "qubic_sendTransaction":
            {
                byte[]? payload = Str(p, "payload") is { Length: > 0 } b64 ? Convert.FromBase64String(b64) : null;
                var inputType = Long(p, "inputType") ?? 0;
                var tick = Long(p, "tick");
                var txp = new ExternalTxParams(Str(p, "from") ?? "", Str(p, "to") ?? "", Long(p, "amount") ?? 0,
                    tick is > 0 ? (uint)tick : null, checked((ushort)inputType), payload);
                var r = method == "qubic_sendTransaction"
                    ? await _external.SendTransactionAsync(txp, context, ct)
                    : await _external.SignTransactionAsync(txp, context, ct);
                return new { transactionId = r.TransactionId, tick = r.Tick, signedTransaction = r.RawBase64, txBytesHex = r.RawHex };
            }
            case "qubic_sign":
            {
                var s = await _external.SignMessageWalletConnectAsync(Str(p, "from") ?? "", Str(p, "message") ?? "", context, ct);
                return new { signedData = s.SignedDataBase64, digest = s.DigestBase64, signature = s.SignatureBase64 };
            }
            case "qubic_signRaw":
            {
                var message = Str(p, "message") ?? throw new ExternalRequestException(ExternalRequestException.InvalidParams, "message is required.");
                var sig = await _external.SignMessageRawAsync(Str(p, "from") ?? "", System.Text.Encoding.UTF8.GetBytes(message), context, ct);
                return new { signature = Convert.ToBase64String(sig), signatureHex = Convert.ToHexString(sig).ToLowerInvariant() };
            }
            case "qubic_sendAsset":
            {
                var r = await _external.SendAssetAsync(Str(p, "from") ?? "", Str(p, "to") ?? "", Str(p, "assetName") ?? "",
                    Str(p, "issuer") ?? "", Long(p, "amount") ?? 0, context, ct);
                return new { transactionId = r.TransactionId, tick = r.Tick };
            }
            default:
                throw new ExternalRequestException(-32601, $"Method not found: {method}");
        }
    }

    private static string Describe(string method) => method switch
    {
        "qubic_sendQubic" => "Send QU (local client)",
        "qubic_signTransaction" => "Sign a transaction (local client)",
        "qubic_sendTransaction" => "Sign and broadcast a transaction (local client)",
        "qubic_sign" or "qubic_signRaw" => "Sign a message (local client)",
        "qubic_sendAsset" => "Send asset shares (local client)",
        _ => method
    };

    private static string? Str(JsonObject p, string name) => p[name]?.ToString();

    private static long? Long(JsonObject p, string name)
    {
        var s = Str(p, name);
        if (string.IsNullOrEmpty(s)) return null;
        return long.TryParse(s, out var v) ? v : throw new FormatException($"{name} must be an integer.");
    }

    private void Raise()
    {
        try { OnChanged?.Invoke(); } catch { }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
