using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class McpRunbook
{
    public string Version { get; set; } = "1";
    public string? DefaultConnection { get; set; }
    public bool ContinueOnAssertFailure { get; set; }
    public List<McpRunbookConnection> Connections { get; set; } = [];
    public List<McpRunbookStep> Steps { get; set; } = [];
    public McpRunbookSchedule Schedule { get; set; } = new();
}

public sealed class McpRunbookConnection
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public McpRunbookAuth Auth { get; set; } = new();
}

public sealed class McpRunbookAuth
{
    public string Type { get; set; } = "bearer";

    public string? Token { get; set; }
    public KeyVaultSecretReference? TokenFromKeyVault { get; set; }

    public string? HeaderName { get; set; }
    public string? ApiKey { get; set; }
    public string? Prefix { get; set; }
    public KeyVaultSecretReference? ApiKeyFromKeyVault { get; set; }

    public string? Username { get; set; }
    public string? Password { get; set; }
    public KeyVaultSecretReference? PasswordFromKeyVault { get; set; }

    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public KeyVaultSecretReference? ClientIdFromKeyVault { get; set; }
    public string? ClientSecret { get; set; }
    public KeyVaultSecretReference? ClientSecretFromKeyVault { get; set; }
    public string? Scope { get; set; }
    public string? AuthorityHost { get; set; }

    public List<McpRunbookHeader> Headers { get; set; } = [];
}

public sealed class McpRunbookHeader
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? AuthorizationType { get; set; }
}

public sealed class McpRunbookStep
{
    public string Id { get; set; } = string.Empty;
    public string Tool { get; set; } = string.Empty;
    public string? Connection { get; set; }
    public Dictionary<string, object?> Params { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public RunbookAssertion? Assert { get; set; }
    public bool? ContinueOnAssertFailure { get; set; }
    public int MaxRetries { get; set; }
    public bool ContinueOnError { get; set; }
}

public sealed class McpRunbookSchedule
{
    public int? Repeat { get; set; } = 1;
    public int? EverySeconds { get; set; }
    public int? ForSeconds { get; set; }
}
