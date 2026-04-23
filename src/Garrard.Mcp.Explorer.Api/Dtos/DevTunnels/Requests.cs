using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

namespace Garrard.Mcp.Explorer.Api.Dtos.DevTunnels;

public sealed record CreateDevTunnelDto(
    string Name,
    TunnelAccess Access = TunnelAccess.Anonymous,
    bool DeleteOnExit = false);

public sealed record ReplayWebhookDto(
    string TargetUrl,
    string? MethodOverride = null,
    Dictionary<string, string>? HeadersOverride = null,
    string? BodyTextOverride = null,
    string? BodyBase64Override = null,
    string? ContentTypeOverride = null);
