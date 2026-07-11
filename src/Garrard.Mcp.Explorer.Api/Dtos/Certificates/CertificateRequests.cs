namespace Garrard.Mcp.Explorer.Api.Dtos.Certificates;

public sealed record GenerateCertificateApiRequest(
    string Name,
    string? SubjectCn,
    int KeySize = 2048,
    int ValidityMonths = 12,
    string? PfxPassword = null);

public sealed record UploadCertificateRequest(string AppId);

public sealed record ExportPfxRequest(string Password);

public sealed record TestTokenRequest(string TenantId, string ClientId, string Scope);

public sealed record RemoveKeyCredentialRequest(string AppId, string KeyId);

public sealed record CreateCsrApiRequest(string Name, string? SubjectCn, int KeySize = 2048);

public sealed record ImportIssuedCertificateRequest(string CertificatePem);
