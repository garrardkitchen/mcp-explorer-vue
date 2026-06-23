using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class McpRunbookParser
{
    private readonly IDeserializer _deserializer;

    public McpRunbookParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithDuplicateKeyChecking()
            .Build();
    }

    public McpRunbook Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        return _deserializer.Deserialize<McpRunbook>(yaml)
               ?? throw new InvalidOperationException("Runbook file is empty or invalid.");
    }
}
