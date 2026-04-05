using Garrard.Mcp.Explorer.Core.Domain.Chat;
using Garrard.Mcp.Explorer.Core.Domain.Connections;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;

namespace Garrard.Mcp.Explorer.Core.Domain.Preferences;

public sealed record UserPreferences
{
    public string? SelectedConnectionName { get; init; }
    public List<ConnectionDefinition> Connections { get; init; } = [];
    public List<ConnectionGroup> ConnectionGroups { get; init; } = [];
    public List<string> FavoriteConnections { get; init; } = [];
    public List<string> FavoriteTools { get; init; } = [];
    public bool ShowFavoritesFirst { get; init; }
    public List<string> FavoritePrompts { get; init; } = [];
    public bool ShowPromptFavoritesFirst { get; init; }
    public List<string> FavoriteResources { get; init; } = [];
    public bool ShowResourceFavoritesFirst { get; init; }
    public List<string> FavoriteResourceTemplates { get; init; } = [];
    public bool ShowResourceTemplateFavoritesFirst { get; init; }
    public List<LlmModelDefinition> LlmModels { get; init; } = [];
    public string? SelectedLlmModelName { get; init; }
    public SensitiveFieldConfiguration SensitiveFieldConfig { get; init; } = new();
    public List<WorkflowDefinition> Workflows { get; init; } = [];
    public List<WorkflowExecution> WorkflowExecutions { get; init; } = [];
    public bool ShowConnectionTimestamps { get; init; }
    public string ConnectionSortOrder { get; init; } = "name";
    public bool ShowConnectionGroups { get; init; } = true;
    public string Theme { get; init; } = "command-dark";
    public List<ChatSession> ChatSessions { get; init; } = [];
    public Dictionary<string, List<string>> ParameterHistory { get; init; } = [];
}
