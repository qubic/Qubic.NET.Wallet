using Qubic.Core;
using Qubic.Core.Entities;

namespace Qubic.Net.Wallet.Helpers;

public static class IdentityValidation
{
    /// <summary>
    /// Validates a Qubic identity string including checksum verification.
    /// Returns null if valid, error message if invalid.
    /// Returns null for empty/null input (not yet filled).
    /// </summary>
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Length != 60)
            return $"Must be 60 characters ({v.Length} entered)";
        if (!v.ToUpperInvariant().All(c => c is >= 'A' and <= 'Z'))
            return "Must be uppercase A-Z only";
        if (!QubicIdentity.TryParse(v, out _))
            return "Invalid checksum";
        return null;
    }

    /// <summary>
    /// Validates a destination that can be either a 60-char identity or a contract index (1-1023).
    /// Returns null if valid, error message if invalid.
    /// </summary>
    public static string? ValidateDestination(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (int.TryParse(v, out var idx))
            return idx is >= 1 and <= 1023 ? null : "Contract index must be 1-1023";
        return Validate(v);
    }

    /// <summary>
    /// Resolves a destination string to a <see cref="QubicIdentity"/>.
    /// Accepts either a 60-character identity or a contract index (1-1023).
    /// Throws <see cref="ArgumentException"/> if the value is invalid.
    /// </summary>
    public static QubicIdentity ResolveIdentity(string value)
    {
        var v = value.Trim();
        if (int.TryParse(v, out var idx))
            return QubicIdentity.FromPublicKey(QubicContracts.GetContractPublicKey(idx));
        return QubicIdentity.FromIdentity(v);
    }

    /// <summary>
    /// Returns Bootstrap CSS class for identity input validation state.
    /// </summary>
    public static string CssClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var v = value.Trim();
        if (v.Length < 60) return "";
        return Validate(v) == null ? "is-valid" : "is-invalid";
    }

    /// <summary>
    /// Returns Bootstrap CSS class for destination input (identity or contract index).
    /// </summary>
    public static string DestCssClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var v = value.Trim();
        if (int.TryParse(v, out _)) return ValidateDestination(v) == null ? "is-valid" : "is-invalid";
        if (v.Length < 60) return "";
        return ValidateDestination(v) == null ? "is-valid" : "is-invalid";
    }
}
