using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// File-based client-certificate store under <c>&lt;dataDir&gt;/certs/&lt;name&gt;/</c>.
/// Each certificate folder holds <c>cert.pem</c> (public), <c>key.pem</c> (PKCS#8, 0600),
/// optional <c>cert.pfx</c> (0600), optional <c>csr.pem</c>, and <c>metadata.json</c>.
/// Generation uses BCL crypto (<see cref="CertificateRequest"/>) — no external tooling.
/// </summary>
public sealed class CertificateService : ICertificateService
{
    private const string CertFileName = "cert.pem";
    private const string KeyFileName = "key.pem";
    private const string PfxFileName = "cert.pfx";
    private const string CsrFileName = "csr.pem";
    private const string MetadataFileName = "metadata.json";

    private static readonly int[] AllowedKeySizes = [2048, 4096];
    private static readonly int[] AllowedValidityMonths = [6, 12, 24];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    private readonly IUserPreferencesStore _preferencesStore;
    private readonly IHttpApiStore _httpApiStore;
    private readonly ILogger<CertificateService> _logger;
    private readonly CertificateAuditLog _audit;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string CertificatesDirectory { get; }

    public CertificateService(
        string? settingsFilePath,
        IUserPreferencesStore preferencesStore,
        IHttpApiStore httpApiStore,
        ILogger<CertificateService>? logger = null)
    {
        _preferencesStore = preferencesStore;
        _httpApiStore = httpApiStore;
        _logger = logger ?? NullLogger<CertificateService>.Instance;

        CertificatesDirectory = ResolveCertsRoot(settingsFilePath);
        _audit = new CertificateAuditLog(CertificatesDirectory);
    }

    /// <summary>Certs live alongside settings.json so the same mounted volume persists both.</summary>
    private static string ResolveCertsRoot(string? settingsFilePath)
    {
        string baseDirectory;
        if (!string.IsNullOrWhiteSpace(settingsFilePath))
        {
            baseDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsFilePath))!;
        }
        else
        {
            var appData = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            baseDirectory = Path.Combine(appData, "McpExplorer");
        }

        return Path.Combine(baseDirectory, "certs");
    }

    // ── Generation ───────────────────────────────────────────────────────────

    public async Task<OperationResult> GenerateSelfSignedAsync(GenerateCertificateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var steps = new List<StepResult>();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? certDirectory = null;
            if (!TryStep(steps, "validate", "Validate request", () =>
                {
                    CertificateFileSecurity.ValidateName(request.Name);
                    if (!AllowedKeySizes.Contains(request.KeySize))
                        throw new ArgumentException($"Key size must be one of: {string.Join(", ", AllowedKeySizes)}.");
                    if (!AllowedValidityMonths.Contains(request.ValidityMonths))
                        throw new ArgumentException($"Validity must be one of: {string.Join(", ", AllowedValidityMonths)} months.");
                    certDirectory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, request.Name);
                    if (Directory.Exists(certDirectory))
                        throw new InvalidOperationException($"A certificate named '{request.Name}' already exists.");
                }))
            {
                return FailRemaining(steps, ["generate-key", "create-cert", "write-files", "save-metadata"], GenerateStepLabels, request);
            }

            RSA? rsa = null;
            X509Certificate2? certificate = null;
            try
            {
                if (!TryStep(steps, "generate-key", GenerateStepLabels["generate-key"](request), () =>
                    {
                        rsa = RSA.Create(request.KeySize);
                    }))
                {
                    return FailRemaining(steps, ["create-cert", "write-files", "save-metadata"], GenerateStepLabels, request);
                }

                var subjectCn = string.IsNullOrWhiteSpace(request.SubjectCn) ? $"mcp-explorer-{request.Name}" : request.SubjectCn.Trim();
                if (!TryStep(steps, "create-cert", GenerateStepLabels["create-cert"](request), () =>
                    {
                        certificate = CreateSelfSigned(rsa!, subjectCn, request.ValidityMonths);
                    }))
                {
                    return FailRemaining(steps, ["write-files", "save-metadata"], GenerateStepLabels, request);
                }

                CertificateInfo? info = null;
                if (!TryStep(steps, "write-files", GenerateStepLabels["write-files"](request), () =>
                    {
                        WriteCertificateFiles(certDirectory!, certificate!, rsa!, request.PfxPassword);
                    }))
                {
                    CleanupDirectory(certDirectory);
                    return FailRemaining(steps, ["save-metadata"], GenerateStepLabels, request);
                }

                if (!TryStep(steps, "save-metadata", GenerateStepLabels["save-metadata"](request), () =>
                    {
                        info = BuildInfo(request.Name, certificate!, request.KeySize, CertificateSource.SelfSigned,
                            CertificateState.Active, hasPfx: !string.IsNullOrEmpty(request.PfxPassword));
                        WriteMetadata(certDirectory!, info);
                    }))
                {
                    CleanupDirectory(certDirectory);
                    return OperationResult.Failed(steps);
                }

                await _audit.AppendAsync("create", request.Name,
                    $"subject=CN={subjectCn}; keySize={request.KeySize}; validityMonths={request.ValidityMonths}",
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Generated self-signed certificate {Name} ({Subject})", request.Name, subjectCn);
                return OperationResult.Succeeded(steps, info);
            }
            finally
            {
                certificate?.Dispose();
                rsa?.Dispose();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static readonly Dictionary<string, Func<GenerateCertificateRequest, string>> GenerateStepLabels = new()
    {
        ["generate-key"] = r => $"Generate RSA key pair ({r.KeySize}-bit)",
        ["create-cert"] = r => $"Create self-signed certificate (X.509, SHA-256, {r.ValidityMonths} months)",
        ["write-files"] = _ => "Write certificate files to the store",
        ["save-metadata"] = _ => "Save metadata and audit entry",
    };

    private static X509Certificate2 CreateSelfSigned(RSA rsa, string subjectCn, int validityMonths)
    {
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={subjectCn}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        // Client authentication EKU — the standard profile for Entra ID client-credential certs.
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.2")], critical: false));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5); // tolerate clock skew
        var notAfter = DateTimeOffset.UtcNow.AddMonths(validityMonths);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static void WriteCertificateFiles(string certDirectory, X509Certificate2 certificate, RSA rsa, string? pfxPassword)
    {
        Directory.CreateDirectory(certDirectory);
        CertificateFileSecurity.HardenDirectory(certDirectory);

        File.WriteAllText(Path.Combine(certDirectory, CertFileName),
            PemEncoding.WriteString("CERTIFICATE", certificate.RawData));

        var keyPath = Path.Combine(certDirectory, KeyFileName);
        File.WriteAllText(keyPath, PemEncoding.WriteString("PRIVATE KEY", rsa.ExportPkcs8PrivateKey()));
        CertificateFileSecurity.HardenFile(keyPath);

        if (!string.IsNullOrEmpty(pfxPassword))
        {
            var pfxPath = Path.Combine(certDirectory, PfxFileName);
            File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pfx, pfxPassword));
            CertificateFileSecurity.HardenFile(pfxPath);
        }
    }

    // ── Listing / reading ────────────────────────────────────────────────────

    public Task<IReadOnlyList<CertificateInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<CertificateInfo>();
        if (!Directory.Exists(CertificatesDirectory))
            return Task.FromResult<IReadOnlyList<CertificateInfo>>(results);

        foreach (var directory in Directory.EnumerateDirectories(CertificatesDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = ReadMetadata(directory);
            if (info is not null) results.Add(info);
        }

        // Soonest expiry first; certs without an expiry (pending CSRs) last.
        results.Sort(static (a, b) => (a.NotAfter ?? DateTimeOffset.MaxValue).CompareTo(b.NotAfter ?? DateTimeOffset.MaxValue));
        return Task.FromResult<IReadOnlyList<CertificateInfo>>(results);
    }

    public Task<CertificateInfo?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var directory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, name);
        return Task.FromResult(Directory.Exists(directory) ? ReadMetadata(directory) : null);
    }

    public async Task<IReadOnlyList<CertificateInfo>> GetExpiringAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(days);
        var all = await ListAsync(cancellationToken).ConfigureAwait(false);
        return all
            .Where(c => c.State != CertificateState.Superseded && c.NotAfter is not null && c.NotAfter <= cutoff)
            .ToList();
    }

    public async Task<string> GetPublicCertPemAsync(string name, CancellationToken cancellationToken = default)
    {
        var certPath = RequireFile(name, CertFileName);
        return await File.ReadAllTextAsync(certPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ExportPfxAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        using var certificate = await LoadWithPrivateKeyAsync(name, cancellationToken).ConfigureAwait(false);
        var bytes = certificate.Export(X509ContentType.Pfx, password);
        await _audit.AppendAsync("export-pfx", name, "password-protected PFX exported", cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    public Task<X509Certificate2> LoadWithPrivateKeyAsync(string name, CancellationToken cancellationToken = default)
    {
        var certPath = RequireFile(name, CertFileName);
        var keyPath = RequireFile(name, KeyFileName);
        return Task.FromResult(X509Certificate2.CreateFromPemFile(certPath, keyPath));
    }

    // ── Usage / delete ───────────────────────────────────────────────────────

    public async Task<CertificateUsage> GetUsageAsync(string name, CancellationToken cancellationToken = default)
    {
        CertificateFileSecurity.ValidateName(name);

        var preferences = await _preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connectionNames = preferences.Connections
            .Where(c => string.Equals(c.AzureCredentials?.CertificateRef?.CertificateName, name, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .ToList();

        var definitions = await _httpApiStore.GetAllDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        var httpApiNames = definitions
            .Where(d => string.Equals(d.AzureCredentials?.CertificateRef?.CertificateName, name, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Name)
            .ToList();

        return new CertificateUsage
        {
            CertificateName = name,
            ConnectionNames = connectionNames,
            HttpApiNames = httpApiNames,
        };
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var directory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, name);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(directory))
                throw new FileNotFoundException($"Certificate '{name}' does not exist.");

            var usage = await GetUsageAsync(name, cancellationToken).ConfigureAwait(false);
            if (usage.IsInUse)
                throw new CertificateInUseException(usage);

            Directory.Delete(directory, recursive: true);
            await _audit.AppendAsync("delete", name, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Deleted certificate {Name}", name);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Upload records ───────────────────────────────────────────────────────

    public async Task RecordUploadAsync(string name, CertificateUploadRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await MutateMetadataAsync(name, info =>
        {
            // Idempotent: replace any previous record for the same app registration.
            var updated = info.UploadedTo.Where(u => !string.Equals(u.AppId, record.AppId, StringComparison.OrdinalIgnoreCase)).ToList();
            updated.Add(record);
            return info with { UploadedTo = updated };
        }, cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync("upload", name, $"appId={record.AppId}; displayName={record.DisplayName}; keyId={record.KeyId}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveUploadRecordAsync(string name, string keyId, CancellationToken cancellationToken = default)
    {
        await MutateMetadataAsync(name, info => info with
        {
            UploadedTo = info.UploadedTo.Where(u => !string.Equals(u.KeyId, keyId, StringComparison.OrdinalIgnoreCase)).ToList()
        }, cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync("remove-key-credential", name, $"keyId={keyId}", cancellationToken).ConfigureAwait(false);
    }

    // ── CSR flow ─────────────────────────────────────────────────────────────

    public async Task<OperationResult> CreateCsrAsync(CreateCsrRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var steps = new List<StepResult>();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? certDirectory = null;
            if (!TryStep(steps, "validate", "Validate request", () =>
                {
                    CertificateFileSecurity.ValidateName(request.Name);
                    if (!AllowedKeySizes.Contains(request.KeySize))
                        throw new ArgumentException($"Key size must be one of: {string.Join(", ", AllowedKeySizes)}.");
                    certDirectory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, request.Name);
                    if (Directory.Exists(certDirectory))
                        throw new InvalidOperationException($"A certificate named '{request.Name}' already exists.");
                }))
            {
                return OperationResult.Failed(steps);
            }

            using var rsa = RSA.Create(request.KeySize);
            var subjectCn = string.IsNullOrWhiteSpace(request.SubjectCn) ? $"mcp-explorer-{request.Name}" : request.SubjectCn.Trim();

            CertificateInfo? info = null;
            if (!TryStep(steps, "create-csr", "Create private key and signing request", () =>
                {
                    var csrRequest = new CertificateRequest(
                        new X500DistinguishedName($"CN={subjectCn}"), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                    Directory.CreateDirectory(certDirectory!);
                    CertificateFileSecurity.HardenDirectory(certDirectory!);

                    var keyPath = Path.Combine(certDirectory!, KeyFileName);
                    File.WriteAllText(keyPath, PemEncoding.WriteString("PRIVATE KEY", rsa.ExportPkcs8PrivateKey()));
                    CertificateFileSecurity.HardenFile(keyPath);

                    File.WriteAllText(Path.Combine(certDirectory!, CsrFileName),
                        PemEncoding.WriteString("CERTIFICATE REQUEST", csrRequest.CreateSigningRequest()));

                    info = new CertificateInfo
                    {
                        Name = request.Name,
                        Subject = $"CN={subjectCn}",
                        KeySize = request.KeySize,
                        Source = CertificateSource.CsrIssued,
                        State = CertificateState.CsrPending,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    WriteMetadata(certDirectory!, info);
                }))
            {
                CleanupDirectory(certDirectory);
                return OperationResult.Failed(steps);
            }

            await _audit.AppendAsync("csr", request.Name, $"subject=CN={subjectCn}; keySize={request.KeySize}", cancellationToken).ConfigureAwait(false);
            return OperationResult.Succeeded(steps, info);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> GetCsrPemAsync(string name, CancellationToken cancellationToken = default)
    {
        var csrPath = RequireFile(name, CsrFileName);
        return await File.ReadAllTextAsync(csrPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> ImportIssuedCertificateAsync(string name, string certificatePem, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePem);
        var steps = new List<StepResult>();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, name);
            var existing = Directory.Exists(directory) ? ReadMetadata(directory) : null;

            if (!TryStep(steps, "validate", "Validate pending CSR", () =>
                {
                    if (existing is null)
                        throw new FileNotFoundException($"Certificate '{name}' does not exist.");
                    if (existing.State != CertificateState.CsrPending)
                        throw new InvalidOperationException($"Certificate '{name}' has no pending CSR.");
                }))
            {
                return OperationResult.Failed(steps);
            }

            X509Certificate2? certificate = null;
            try
            {
                if (!TryStep(steps, "verify-key", "Verify the issued certificate matches the stored private key", () =>
                    {
                        using var publicCert = X509Certificate2.CreateFromPem(certificatePem);
                        using var rsa = RSA.Create();
                        rsa.ImportFromPem(File.ReadAllText(Path.Combine(directory, KeyFileName)));
                        // Throws when the public key does not correspond to the private key.
                        certificate = publicCert.CopyWithPrivateKey(rsa);
                    }))
                {
                    return OperationResult.Failed(steps, existing);
                }

                CertificateInfo? info = null;
                if (!TryStep(steps, "write-cert", "Write issued certificate and update metadata", () =>
                    {
                        File.WriteAllText(Path.Combine(directory, CertFileName),
                            PemEncoding.WriteString("CERTIFICATE", certificate!.RawData));

                        info = BuildInfo(name, certificate, existing!.KeySize, CertificateSource.CsrIssued,
                            CertificateState.Active, hasPfx: false) with
                        {
                            CreatedAt = existing.CreatedAt,
                            UploadedTo = existing.UploadedTo,
                        };
                        WriteMetadata(directory, info);
                    }))
                {
                    return OperationResult.Failed(steps, existing);
                }

                await _audit.AppendAsync("import-issued", name, $"thumbprintSha1={info!.ThumbprintSha1}", cancellationToken).ConfigureAwait(false);
                return OperationResult.Succeeded(steps, info);
            }
            finally
            {
                certificate?.Dispose();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Import (export/import bundles, Key Vault) ────────────────────────────

    public async Task<CertificateInfo> ImportAsync(
        string name,
        string certificatePem,
        string privateKeyPem,
        byte[]? pfxBytes,
        CertificateSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePem);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, name);
            if (Directory.Exists(directory))
                throw new InvalidOperationException($"A certificate named '{name}' already exists.");

            using var publicCert = X509Certificate2.CreateFromPem(certificatePem);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            using var certificate = publicCert.CopyWithPrivateKey(rsa); // validates the pair

            Directory.CreateDirectory(directory);
            CertificateFileSecurity.HardenDirectory(directory);

            File.WriteAllText(Path.Combine(directory, CertFileName),
                PemEncoding.WriteString("CERTIFICATE", certificate.RawData));

            var keyPath = Path.Combine(directory, KeyFileName);
            File.WriteAllText(keyPath, PemEncoding.WriteString("PRIVATE KEY", rsa.ExportPkcs8PrivateKey()));
            CertificateFileSecurity.HardenFile(keyPath);

            if (pfxBytes is { Length: > 0 })
            {
                var pfxPath = Path.Combine(directory, PfxFileName);
                File.WriteAllBytes(pfxPath, pfxBytes);
                CertificateFileSecurity.HardenFile(pfxPath);
            }

            var info = BuildInfo(name, certificate, rsa.KeySize, source, CertificateState.Active,
                hasPfx: pfxBytes is { Length: > 0 });
            WriteMetadata(directory, info);

            await _audit.AppendAsync("import", name, $"source={source}; thumbprintSha1={info.ThumbprintSha1}", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Imported certificate {Name} (source {Source})", name, source);
            return info;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Audit ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<CertificateAuditEntry>> ReadAuditAsync(string? name = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (name is not null) CertificateFileSecurity.ValidateName(name);
        return _audit.ReadAsync(name, limit, cancellationToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryStep(List<StepResult> steps, string id, string label, Action action)
    {
        try
        {
            action();
            steps.Add(new StepResult(id, label, StepStatus.Succeeded));
            return true;
        }
        catch (Exception ex)
        {
            steps.Add(new StepResult(id, label, StepStatus.Failed, ex.Message));
            return false;
        }
    }

    private static OperationResult FailRemaining(
        List<StepResult> steps,
        string[] remainingIds,
        Dictionary<string, Func<GenerateCertificateRequest, string>> labels,
        GenerateCertificateRequest? request = null)
    {
        foreach (var id in remainingIds)
        {
            var label = request is not null && labels.TryGetValue(id, out var factory) ? factory(request) : id;
            steps.Add(new StepResult(id, label, StepStatus.Skipped));
        }
        return OperationResult.Failed(steps);
    }

    private static CertificateInfo BuildInfo(
        string name, X509Certificate2 certificate, int keySize,
        CertificateSource source, CertificateState state, bool hasPfx)
    {
        return new CertificateInfo
        {
            Name = name,
            Subject = certificate.Subject,
            KeySize = keySize,
            Source = source,
            State = state,
            CreatedAt = DateTimeOffset.UtcNow,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
            ThumbprintSha1 = certificate.Thumbprint,
            ThumbprintSha256 = Convert.ToHexString(SHA256.HashData(certificate.RawData)),
            HasPfx = hasPfx,
        };
    }

    private async Task MutateMetadataAsync(string name, Func<CertificateInfo, CertificateInfo> mutate, CancellationToken cancellationToken)
    {
        var directory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, name);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var info = ReadMetadata(directory)
                       ?? throw new FileNotFoundException($"Certificate '{name}' does not exist.");
            WriteMetadata(directory, mutate(info));
        }
        finally
        {
            _lock.Release();
        }
    }

    private CertificateInfo? ReadMetadata(string certDirectory)
    {
        var path = Path.Combine(certDirectory, MetadataFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<CertificateInfo>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping certificate with malformed metadata at {Path}", path);
            return null;
        }
    }

    private void WriteMetadata(string certDirectory, CertificateInfo info)
    {
        EnsureStoreRoot();
        var path = Path.Combine(certDirectory, MetadataFileName);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(info, SerializerOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private void EnsureStoreRoot()
    {
        Directory.CreateDirectory(CertificatesDirectory);
        CertificateFileSecurity.EnsureGitIgnore(CertificatesDirectory);
    }

    private string RequireFile(string name, string fileName)
    {
        var directory = CertificateFileSecurity.ResolveCertificateDirectory(CertificatesDirectory, name);
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Certificate '{name}' has no {fileName}.");
        return path;
    }

    private static void CleanupDirectory(string? directory)
    {
        if (directory is null || !Directory.Exists(directory)) return;
        try { Directory.Delete(directory, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
