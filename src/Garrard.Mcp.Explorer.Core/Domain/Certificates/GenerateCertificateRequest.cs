namespace Garrard.Mcp.Explorer.Core.Domain.Certificates;

/// <summary>Request to generate a self-signed client certificate in the local store.</summary>
public sealed record GenerateCertificateRequest
{
    /// <summary>Store name; must match the certificate name allow-list (lowercase, digits, hyphens).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Subject common name; defaults to <c>mcp-explorer-{Name}</c> when empty.</summary>
    public string? SubjectCn { get; init; }

    /// <summary>RSA key size: 2048 or 4096.</summary>
    public int KeySize { get; init; } = 2048;

    /// <summary>Validity period: 6, 12, or 24 months.</summary>
    public int ValidityMonths { get; init; } = 12;

    /// <summary>When set, a password-protected <c>cert.pfx</c> bundle is also written.</summary>
    public string? PfxPassword { get; init; }
}

/// <summary>Request to create a certificate signing request (CA-issued flow).</summary>
public sealed record CreateCsrRequest
{
    public string Name { get; init; } = string.Empty;
    public string? SubjectCn { get; init; }
    public int KeySize { get; init; } = 2048;
}
