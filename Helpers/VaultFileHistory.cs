using Qubic.Services;

namespace Qubic.Net.Wallet.Helpers;

/// <summary>
/// Tracks recently used vault files so the user can switch between them
/// straight from the header bar instead of re-pointing the vault in settings.
/// The list is stored in settings; the vault files themselves are untouched.
/// </summary>
public static class VaultFileHistory
{
    private const string Key = "vault_recent_paths";
    private const int MaxEntries = 8;

    /// <summary>
    /// Recently used vault files, most recent first. Paths that no longer
    /// exist on disk are dropped.
    /// </summary>
    public static List<string> Get(QubicSettingsService settings)
    {
        var stored = settings.GetCustom<List<string>>(Key) ?? [];
        var result = new List<string>();
        foreach (var path in stored)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (result.Any(p => IsSame(p, path))) continue;
            if (!File.Exists(path)) continue;
            result.Add(path);
            if (result.Count >= MaxEntries) break;
        }
        return result;
    }

    /// <summary>Moves the given vault file to the front of the list.</summary>
    public static void Add(QubicSettingsService settings, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var list = Get(settings);
        list.RemoveAll(p => IsSame(p, path));
        list.Insert(0, path);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        settings.SetCustom(Key, list);
    }

    /// <summary>Drops a vault file from the list (deleted or moved away).</summary>
    public static void Remove(QubicSettingsService settings, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var list = Get(settings);
        if (list.RemoveAll(p => IsSame(p, path)) == 0) return;
        settings.SetCustom(Key, list);
    }

    /// <summary>Short display name for a vault file (file name without extension).</summary>
    public static string DisplayName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    /// <summary>
    /// Display names for a set of vault files, prefixed with the containing folder
    /// where the file names alone would collide (several "vault.dat" files).
    /// </summary>
    public static Dictionary<string, string> DisplayNames(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        var result = new Dictionary<string, string>();
        foreach (var path in list)
        {
            var name = DisplayName(path);
            var collides = list.Any(other => !IsSame(other, path) && DisplayName(other) == name);
            if (collides)
            {
                var folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
                if (!string.IsNullOrEmpty(folder)) name = $"{folder}/{name}";
            }
            result[path] = name;
        }
        return result;
    }

    /// <summary>True when both paths point at the same vault file.</summary>
    public static bool IsSame(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
