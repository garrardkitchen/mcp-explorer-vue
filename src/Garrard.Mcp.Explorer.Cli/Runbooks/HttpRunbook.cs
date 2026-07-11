using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class HttpRunbook
{
    public string Version { get; set; } = "1";
    public string? DefaultConnection { get; set; }
    public bool ContinueOnAssertFailure { get; set; }
    public List<HttpRunbookConnection> Connections { get; set; } = [];
    public List<HttpRunbookStep> Steps { get; set; } = [];
    public RunbookSchedule Schedule { get; set; } = new();
}

public sealed class HttpRunbookConnection
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public HttpRunbookAuth Auth { get; set; } = new();
}

public sealed class HttpRunbookAuth
{
    public string Type { get; set; } = "none";

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

    public List<HttpRunbookHeader> Headers { get; set; } = [];
}

public sealed class HttpRunbookHeader
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class HttpRunbookStep
{
    public string Id { get; set; } = string.Empty;
    public string? EndpointId { get; set; }
    public string? Endpoint { get; set; }
    public Dictionary<string, object?> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public RunbookAssertion? Assert { get; set; }
    public bool? ContinueOnAssertFailure { get; set; }
    public int MaxRetries { get; set; }
    public bool ContinueOnError { get; set; }
    public bool UseLocalhost { get; set; }
}
