using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Dtos.Connections;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ConnectionsController(IConnectionService connectionService, IUserPreferencesStore preferencesStore) : ControllerBase
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
        return Ok(new { name = connection.Name, endpoint = connection.Endpoint, isHealthy = connection.IsHealthy, toolCount = connection.Tools.Count });
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
