using Garrard.Mcp.Explorer.Core.Domain.Preferences;

namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface IUserPreferencesStore
{
    Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
    string StoragePath { get; }
}
