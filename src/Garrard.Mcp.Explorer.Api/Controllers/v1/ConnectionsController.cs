using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Dtos.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ConnectionsController(
    IConnectionService connectionService,
    IUserPreferencesStore preferencesStore,
    IConnectionExportService exportService,
    ICertificateService certificateService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        return Ok(prefs.Connections);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        return Ok(prefs.ConnectionGroups.OrderBy(g => g.Name));
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] ConnectionGroup group, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        if (prefs.ConnectionGroups.Any(g => string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A group named '{group.Name}' already exists." });
        var updated = prefs with { ConnectionGroups = [..prefs.ConnectionGroups, group] };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return CreatedAtAction(nameof(GetGroups), new { }, group);
    }

    [HttpPut("groups/{name}")]
    public async Task<IActionResult> UpdateGroup(string name, [FromBody] ConnectionGroup group, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var existing = prefs.ConnectionGroups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return NotFound();

        // Check for name collision when renaming
        if (!string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase) &&
            prefs.ConnectionGroups.Any(g => string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { error = $"A group named '{group.Name}' already exists." });
        }

        // Cascade rename to any connections that reference the old group name
        var connections = prefs.Connections;
        if (!string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var c in connections.Where(c => string.Equals(c.GroupName, name, StringComparison.OrdinalIgnoreCase)))
                c.GroupName = group.Name;
        }

        var updated = prefs with
        {
            ConnectionGroups = prefs.ConnectionGroups
                .Select(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase) ? group : g)
                .ToList(),
            Connections = connections
        };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return Ok(group);
    }

    [HttpDelete("groups/{name}")]
    public async Task<IActionResult> DeleteGroup(string name, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var updated = prefs with
        {
            ConnectionGroups = prefs.ConnectionGroups
                .Where(g => !string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConnectionRequest request, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        if (prefs.Connections.Any(c => string.Equals(c.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A connection named '{request.Name}' already exists." });

        var definition = MapFromRequest(request);
        var updated = prefs with { Connections = [..prefs.Connections, definition] };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, definition);
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] CreateConnectionRequest request, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var existing = prefs.Connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return NotFound();

        // If the caller is renaming to a different name, ensure it doesn't collide with another entry
        if (!string.Equals(request.Name, name, StringComparison.OrdinalIgnoreCase) &&
            prefs.Connections.Any(c => string.Equals(c.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { error = $"A connection named '{request.Name}' already exists." });
        }

        var updated = MapFromRequest(request);
        updated.CreatedAt = existing.CreatedAt;
        updated.LastUpdatedAt = DateTime.UtcNow;

        var connections = prefs.Connections
            .Select(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) ? updated : c)
            .ToList();
        await preferencesStore.SaveAsync(prefs with { Connections = connections }, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var updated = prefs with
        {
            Connections = prefs.Connections
                .Where(c => !string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
        await preferencesStore.SaveAsync(updated, cancellationToken);
        return NoContent();
    }

    [HttpPost("{name}/connect")]
    public async Task<IActionResult> Connect(string name, CancellationToken cancellationToken)
    {
        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var definition = prefs.Connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (definition is null) return NotFound();
        var connection = await connectionService.ConnectAsync(definition, cancellationToken);
        return Ok(new { name = connection.Name, endpoint = connection.Endpoint, isConnected = connection.IsConnected, isHealthy = connection.IsHealthy, toolCount = connection.Tools.Count });
    }

    [HttpPost("{name}/disconnect")]
    public async Task<IActionResult> Disconnect(string name, CancellationToken cancellationToken)
    {
        await connectionService.DisconnectAsync(name, cancellationToken);
        return NoContent();
    }

    [HttpGet("active")]
    public IActionResult GetActive()
    {
        var active = connectionService.GetActiveConnections()
            .Select(c => new { c.Name, c.Endpoint, c.IsConnected, c.IsHealthy, toolCount = c.Tools.Count });
        return Ok(active);
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportConnectionsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        if (request.Names is null || request.Names.Count == 0)
            return BadRequest(new { error = "Select at least one connection to export." });

        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var selected = prefs.Connections
            .Where(c => request.Names.Any(n => string.Equals(n, c.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (selected.Count == 0)
            return NotFound(new { error = "None of the requested connections were found." });

        // Optionally bundle the certificates the selected connections reference.
        // Private keys only ever travel inside the AES-256-GCM encrypted payload.
        var certificates = new List<Core.Domain.Certificates.ExportedCertificate>();
        if (request.IncludeCertificates)
        {
            var certNames = selected
                .Select(c => c.AzureCredentials?.CertificateRef?.CertificateName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var certName in certNames)
            {
                try { certificates.Add(await certificateService.ExportForBundleAsync(certName, cancellationToken)); }
                catch (FileNotFoundException) { /* referenced cert missing on disk — export the connections anyway */ }
            }
        }

        var payload = exportService.Encrypt(selected, certificates, request.Password);
        var json = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", "connections-export.json");
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportConnectionsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        if (request.Payload is null)
            return BadRequest(new { error = "Export payload is required." });

        var payload = new Core.Interfaces.ConnectionExportPayload
        {
            Version = request.Payload.Version,
            Salt    = request.Payload.Salt,
            Nonce   = request.Payload.Nonce,
            Data    = request.Payload.Data
        };

        Core.Interfaces.ConnectionExportBundle bundle;
        try
        {
            bundle = exportService.DecryptBundle(payload, request.Password);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var decrypted = bundle.Connections;

        var prefs = await preferencesStore.LoadAsync(cancellationToken);
        var existingNames = prefs.Connections.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<ConnectionDefinition>();
        foreach (var conn in decrypted)
        {
            if (string.IsNullOrWhiteSpace(conn.Name)) continue;

            var finalName = conn.Name;
            if (existingNames.Contains(finalName))
            {
                var version = 2;
                while (existingNames.Contains($"{conn.Name} (v{version})"))
                    version++;
                finalName = $"{conn.Name} (v{version})";
            }

            var imported = new ConnectionDefinition
            {
                Name               = finalName,
                Endpoint           = conn.Endpoint,
                AuthenticationMode = conn.AuthenticationMode,
                Headers            = conn.Headers,
                AzureCredentials   = conn.AzureCredentials,
                OAuthOptions       = conn.OAuthOptions,
                Note               = conn.Note,
                GroupName          = conn.GroupName,
                CreatedAt          = DateTime.UtcNow
            };
            toAdd.Add(imported);
            existingNames.Add(finalName);
        }

        if (toAdd.Count > 0)
        {
            var updated = prefs with { Connections = [..prefs.Connections, ..toAdd] };
            await preferencesStore.SaveAsync(updated, cancellationToken);
        }

        // Bundled certificates: write to the local store; existing names are kept as-is.
        var certificatesImported = 0;
        var certificatesSkipped = 0;
        foreach (var cert in bundle.Certificates)
        {
            try
            {
                await certificateService.ImportAsync(
                    cert.Name,
                    cert.CertPem,
                    cert.KeyPem,
                    cert.PfxBase64 is null ? null : Convert.FromBase64String(cert.PfxBase64),
                    cert.Source,
                    cancellationToken);
                certificatesImported++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException or System.Security.Cryptography.CryptographicException)
            {
                certificatesSkipped++; // already exists or malformed entry — connections still import
            }
        }

        return Ok(new { imported = toAdd.Count, total = decrypted.Count, certificatesImported, certificatesSkipped });
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static ConnectionDefinition MapFromRequest(CreateConnectionRequest r) => new()
    {
        Name = r.Name,
        Endpoint = r.Endpoint,
        Note = r.Note ?? string.Empty,
        GroupName = r.GroupName,
        AuthenticationMode = r.AuthenticationMode,
        Headers = r.Headers ?? [],
        AzureCredentials = r.AzureCredentials,
        OAuthOptions = r.OAuthOptions
    };
}
