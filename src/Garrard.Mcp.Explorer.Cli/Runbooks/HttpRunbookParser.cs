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
        var runbook = _deserializer.Deserialize<HttpRunbook>(yaml)
                      ?? throw new InvalidOperationException("Runbook file is empty or invalid.");

        runbook.Connections ??= [];
        runbook.Steps ??= [];
        runbook.Connections = runbook.Connections.Where(c => c is not null).ToList();
        runbook.Steps = runbook.Steps.Where(s => s is not null).ToList();
        runbook.Schedule ??= new HttpRunbookSchedule();

        foreach (var connection in runbook.Connections)
        {
            connection.Auth ??= new HttpRunbookAuth();
            connection.Auth.Headers ??= [];
        }

        foreach (var step in runbook.Steps)
            step.Inputs ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return runbook;
    }
}
