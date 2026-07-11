using System.Text;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;

namespace Garrard.Mcp.Explorer.Infrastructure.Certificates;

/// <summary>
/// Append-only JSONL audit log for certificate operations (<c>certs/audit.jsonl</c>).
/// </summary>
internal sealed class CertificateAuditLog(string certsRoot)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _filePath = Path.Combine(certsRoot, "audit.jsonl");
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AppendAsync(string action, string certificateName, string? details = null, CancellationToken cancellationToken = default)
    {
        var entry = new CertificateAuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            CertificateName = certificateName,
            Details = details,
        };

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(certsRoot);
            var json = JsonSerializer.Serialize(entry, SerializerOptions);
            await File.AppendAllTextAsync(_filePath, json + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<CertificateAuditEntry>> ReadAsync(string? certificateName = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath)) return [];

            var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var entries = new List<CertificateAuditEntry>(lines.Length);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<CertificateAuditEntry>(line, SerializerOptions);
                    if (entry is null) continue;
                    if (certificateName is not null &&
                        !string.Equals(entry.CertificateName, certificateName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    entries.Add(entry);
                }
                catch (JsonException)
                {
                    // Skip malformed lines — the log must never block reads.
                }
            }

            return entries
                .OrderByDescending(e => e.Timestamp)
                .Take(Math.Max(1, limit))
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }
}
