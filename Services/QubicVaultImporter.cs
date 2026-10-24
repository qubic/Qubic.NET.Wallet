using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qubic.Net.Wallet.Services;

/// <summary>One account recovered from an external (web / mobile) Qubic vault file.</summary>
public sealed record ImportedVaultSeed(string Alias, string Identity, string? Seed, bool WatchOnly);

/// <summary>
/// Reads the "Qubic Vault file" exported by the Qubic web wallet and the official mobile
/// (Flutter) wallet — both produce the identical format. The file is UTF-8 JSON
/// <c>{ salt, iv, cipher }</c> (base64): AES-GCM (key = PBKDF2-SHA256(password, salt, 100000))
/// over an inner <c>{ privateKey, configuration }</c>. <c>privateKey</c> is an AES-GCM-wrapped
/// RSA-OAEP-4096 key (wrap key = PBKDF2-SHA256(password, 16 zero bytes, 100000), 12 zero-byte IV),
/// and each account's <c>encryptedSeed</c> is RSA-OAEP(SHA-256) of its 55-character seed.
/// This reader only decrypts; it never writes the external file.
/// </summary>
public static class QubicVaultImporter
{
    private const int Pbkdf2Iterations = 100_000;
    private const int GcmTagBytes = 16;

    /// <summary>True if the bytes parse as a Qubic vault file envelope (not a check of the password).</summary>
    public static bool LooksLikeVaultFile(byte[] fileBytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(fileBytes);
            var r = doc.RootElement;
            return r.TryGetProperty("salt", out _) && r.TryGetProperty("iv", out _) && r.TryGetProperty("cipher", out _);
        }
        catch { return false; }
    }

    /// <summary>
    /// Decrypts the vault and returns every account. Accounts without a seed (watch-only) are
    /// returned with <see cref="ImportedVaultSeed.Seed"/> null. Throws <see cref="VaultImportException"/>
    /// on a wrong password or an unreadable file.
    /// </summary>
    public static IReadOnlyList<ImportedVaultSeed> Import(byte[] fileBytes, string password)
    {
        JsonElement envelope;
        try { using var doc = JsonDocument.Parse(fileBytes); envelope = doc.RootElement.Clone(); }
        catch { throw new VaultImportException("This is not a Qubic vault file."); }

        byte[] salt = B64(envelope, "salt");
        byte[] iv = B64(envelope, "iv");
        byte[] cipher = B64(envelope, "cipher");

        byte[] innerBytes = AesGcmOpen(DeriveAesKey(password, salt), iv, cipher);
        using var inner = JsonDocument.Parse(innerBytes);
        var root = inner.RootElement;

        using var rsa = UnwrapRsaKey(B64(root, "privateKey"), password);

        var result = new List<ImportedVaultSeed>();
        if (root.TryGetProperty("configuration", out var cfg) && cfg.TryGetProperty("seeds", out var seeds)
            && seeds.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in seeds.EnumerateArray())
            {
                var alias = s.TryGetProperty("alias", out var a) ? a.GetString() ?? "" : "";
                var identity = s.TryGetProperty("publicId", out var p) ? p.GetString() ?? "" : "";
                var watchOnly = s.TryGetProperty("isOnlyWatch", out var w) && w.ValueKind == JsonValueKind.True;
                var enc = s.TryGetProperty("encryptedSeed", out var e) ? e.GetString() : null;

                string? seed = null;
                if (!watchOnly && !string.IsNullOrEmpty(enc))
                {
                    try { seed = Encoding.UTF8.GetString(rsa.Decrypt(Convert.FromBase64String(enc), RSAEncryptionPadding.OaepSHA256)); }
                    catch { seed = null; } // one bad entry must not abort the whole import
                }
                result.Add(new ImportedVaultSeed(alias, identity, seed, watchOnly || seed == null));
            }
        }
        return result;
    }

    private static byte[] DeriveAesKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);

    // WebCrypto AES-GCM appends the 16-byte tag to the ciphertext.
    private static byte[] AesGcmOpen(byte[] key, byte[] iv, byte[] cipherAndTag)
    {
        if (cipherAndTag.Length < GcmTagBytes) throw new VaultImportException("Vault file is corrupt.");
        var ct = cipherAndTag.AsSpan(0, cipherAndTag.Length - GcmTagBytes);
        var tag = cipherAndTag.AsSpan(cipherAndTag.Length - GcmTagBytes);
        var plain = new byte[ct.Length];
        try
        {
            using var gcm = new AesGcm(key, GcmTagBytes);
            gcm.Decrypt(iv, ct, tag, plain);
        }
        catch (CryptographicException) { throw new VaultImportException("Wrong password, or this is not a Qubic vault file."); }
        return plain;
    }

    private static RSA UnwrapRsaKey(byte[] wrapped, string password)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), new byte[16], Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        var jwkBytes = AesGcmOpen(key, new byte[12], wrapped);   // decrypts to the private key as a JWK JSON
        using var jwk = JsonDocument.Parse(jwkBytes);
        var j = jwk.RootElement;
        var p = new RSAParameters
        {
            Modulus = JwkField(j, "n"), Exponent = JwkField(j, "e"), D = JwkField(j, "d"),
            P = JwkField(j, "p"), Q = JwkField(j, "q"), DP = JwkField(j, "dp"), DQ = JwkField(j, "dq"), InverseQ = JwkField(j, "qi"),
        };
        var rsa = RSA.Create();
        rsa.ImportParameters(p);
        return rsa;
    }

    private static byte[] JwkField(JsonElement j, string name) =>
        j.TryGetProperty(name, out var v) && v.GetString() is { } s ? Base64Url(s)
            : throw new VaultImportException("Vault private key is malformed.");

    private static byte[] B64(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.GetString() is { } s ? Convert.FromBase64String(s)
            : throw new VaultImportException("This is not a Qubic vault file.");

    private static byte[] Base64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

/// <summary>A vault file could not be read or the password was wrong.</summary>
public sealed class VaultImportException(string message) : Exception(message);
