using Garrard.Mcp.Explorer.Core.Domain.Certificates;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// One-click certificate rotation: generate a successor, upload it to every app registration
/// the old certificate was uploaded to, repoint referencing connections and HTTP API
/// definitions, optionally remove the old key credentials in Azure, and mark the old
/// certificate superseded. Partial failure is safe — the old certificate keeps working
/// until every step that replaces it has succeeded.
/// </summary>
public interface ICertificateRenewalService
{
    Task<OperationResult> RenewAsync(string certificateName, bool removeOldKeyCredential, CancellationToken cancellationToken = default);
}
