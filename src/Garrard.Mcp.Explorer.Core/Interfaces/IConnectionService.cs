using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Represents an active MCP connection with its discovered capabilities.
/// </summary>
public interface IActiveConnection
{
    string Name { get; }
    string Endpoint { get; }
    bool IsConnected { get; }
    IReadOnlyList<ActiveTool> Tools { get; }
    IReadOnlyList<ActivePrompt> Prompts { get; }
    IReadOnlyList<ActiveResource> Resources { get; }
    IReadOnlyList<ActiveResourceTemplate> ResourceTemplates { get; }
}

public sealed record ActiveTool(string Name, string Description, object? InputSchema, string? IconUrl = null);
public sealed record ActivePrompt(string Name, string? Description, IReadOnlyList<PromptArgument> Arguments, string? IconUrl = null);
public sealed record PromptArgument(string Name, string? Description, bool Required);
public sealed record ActiveResource(string Uri, string Name, string? Description, string? MimeType, string? IconUrl = null);
public sealed record ActiveResourceTemplate(string UriTemplate, string Name, string? Description, string? IconUrl = null);

public interface IConnectionService
{
    Task<IActiveConnection> ConnectAsync(ConnectionDefinition definition, CancellationToken cancellationToken = default);
    IReadOnlyList<IActiveConnection> GetActiveConnections();
    IActiveConnection? GetConnection(string name);
    Task DisconnectAsync(string name, CancellationToken cancellationToken = default);
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);
    Task<string> InvokeToolAsync(string connectionName, string toolName, Dictionary<string, object?> parameters, CancellationToken cancellationToken = default);
    Task<string> ExecutePromptAsync(string connectionName, string promptName, Dictionary<string, string> arguments, CancellationToken cancellationToken = default);
    Task<string> ReadResourceAsync(string connectionName, string uri, CancellationToken cancellationToken = default);
}
