namespace Garrard.Mcp.Explorer.Core.Domain.LlmModels;

public sealed class LlmModelDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = "OpenAI";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
