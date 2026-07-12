using System.Text.RegularExpressions;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Security rails for the certificate store: name allow-listing, path containment,
/// restrictive unix file modes, and a defensive <c>.gitignore</c>.
/// </summary>
internal static partial class CertificateFileSecurity
{
    // Lowercase alphanumerics and hyphens, 1-64 chars, no leading/trailing hyphen —
    // same shape as the Key Vault name allow-list used elsewhere in the codebase.
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$")]
    private static partial Regex NameRegex();

    public static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && NameRegex().IsMatch(name);

    public static void ValidateName(string? name)
    {
        if (!IsValidName(name))
            throw new ArgumentException(
                "Certificate name must be 1-64 lowercase letters, digits, or hyphens (no leading/trailing hyphen).",
                nameof(name));
    }

    /// <summary>Resolves the directory for a certificate name and guards against path traversal.</summary>
    public static string ResolveCertificateDirectory(string certsRoot, string name)
    {
        ValidateName(name);
        var fullRoot = Path.GetFullPath(certsRoot);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, name));
        if (!candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"Certificate name '{name}' resolves outside the certificate store.", nameof(name));
        return candidate;
    }

    /// <summary>Restricts a sensitive file (private key, PFX) to owner read/write. No-op on Windows.</summary>
    public static void HardenFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Restricts a certificate directory to owner access. No-op on Windows.</summary>
    public static void HardenDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>
    /// Writes a <c>.gitignore</c> containing <c>*</c> at the store root so certificates
    /// can never be committed even if the data dir lives inside a repository.
    /// </summary>
    public static void EnsureGitIgnore(string certsRoot)
    {
        var path = Path.Combine(certsRoot, ".gitignore");
        if (!File.Exists(path))
            File.WriteAllText(path, "*\n");
    }
}
