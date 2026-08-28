using Garrard.Mcp.Explorer.Core.Domain.LlmModels;

namespace Garrard.Tests.Mcp.Explorer.Core.Domain.LlmModels;

public sealed class LlmModelDefinitionTests
{
    [Fact]
    public void Constructor_DefaultsToDefaultAzureCredential()
    {
        var model = new LlmModelDefinition();

        Assert.Equal(LlmAuthenticationMode.DefaultAzureCredential, model.AuthenticationMode);
        Assert.Equal(FoundryAgentInvocationMode.VersionedAgent, model.AgentInvocationMode);
        Assert.Empty(model.AgentName);
        Assert.Empty(model.AgentVersion);
    }
}
