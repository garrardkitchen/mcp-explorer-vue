namespace Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

public sealed record DevTunnel(
    string Id,
    string Name,
    TunnelAccess Access,
    TunnelStatus Status,
    Uri? TunnelUri,
    Uri? WebhookUri,
    DateTime CreatedAtUtc,
    DateTime? LastStartedAtUtc,
    DateTime? LastStoppedAtUtc,
    string? LastError,
    bool DeleteOnExit,
    int RestartCount,
    string? CliTunnelId = null);
