using System.Net;

namespace Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

public sealed record DevTunnelUserState(
    bool IsLoggedIn,
    string? UserName,
    string? Provider)
{
    public bool IsAvailable { get; init; } = true;
    public string? Detail { get; init; }
}

public sealed record CreateDevTunnelRequest(
    string Name,
    TunnelAccess Access,
    bool DeleteOnExit);

public sealed record DevTunnelHostUpdate(
    DateTime TimestampUtc,
    string OutputLine,
    TunnelStatus Status,
    Uri? TunnelUri);

public sealed record WebhookCaptureRequest(
    string TunnelId,
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string> Headers,
    string? ContentType,
    string? RemoteIp,
    byte[] Body);

public sealed record ReplayWebhookRequest(
    string TunnelId,
    string EventId,
    Uri TargetUri,
    string? MethodOverride,
    IReadOnlyDictionary<string, string>? HeadersOverride,
    string? BodyTextOverride,
    string? BodyBase64Override,
    string? ContentTypeOverride,
    IReadOnlyList<IPAddress> AllowedAddresses);

public sealed record ReplayWebhookResult(
    int StatusCode,
    string? ReasonPhrase,
    IReadOnlyDictionary<string, string> Headers,
    string? BodyText,
    long BodySize,
    TimeSpan Duration);
