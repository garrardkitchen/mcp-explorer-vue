using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Garrard.Mcp.Explorer.Cli.Runbooks;

public sealed class HttpRunbookParser
{
    private readonly IDeserializer _deserializer;

    public HttpRunbookParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithDuplicateKeyChecking()
            .Build();
    }

    public HttpRunbook Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        return _deserializer.Deserialize<HttpRunbook>(yaml)
               ?? throw new InvalidOperationException("Runbook file is empty or invalid.");
    }
}
