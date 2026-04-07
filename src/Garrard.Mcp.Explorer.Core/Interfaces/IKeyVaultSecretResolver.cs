using Garrard.Mcp.Explorer.Core.Domain.Connections;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

/// <summary>
/// Resolves a <see cref="KeyVaultSecretReference"/> to its secret value at runtime
/// using <c>DefaultAzureCredential</c>. The secret value is never persisted.
/// </summary>
public interface IKeyVaultSecretResolver
{
    /// <summary>
    /// Fetches the current value of the secret identified by <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The vault name and secret name to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value as a plain string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the vault or secret cannot be found, or when credentials are unavailable.
    /// </exception>
    Task<string> ResolveAsync(KeyVaultSecretReference reference, CancellationToken cancellationToken = default);
}
