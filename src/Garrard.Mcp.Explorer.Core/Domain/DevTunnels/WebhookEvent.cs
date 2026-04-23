namespace Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

public sealed record WebhookEvent(
    string Id,
    string TunnelId,
    DateTime ReceivedAtUtc,
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string> Headers,
    string? ContentType,
    long BodySize,
    string? BodyText,
    string? BodyBase64,
    string? ContentEncoding,
    string? RemoteIp,
    bool Truncated);
