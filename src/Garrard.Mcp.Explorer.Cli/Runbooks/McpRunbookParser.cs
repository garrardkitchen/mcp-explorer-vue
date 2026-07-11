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
        var runbook = _deserializer.Deserialize<McpRunbook>(yaml)
                      ?? throw new InvalidOperationException("Runbook file is empty or invalid.");

        // YamlDotNet overwrites property initializers with null for explicit empty keys
        // (e.g. a bare "connections:" line) — normalize so validation reports errors instead of throwing.
        runbook.Connections = runbook.Connections?.Where(c => c is not null).ToList() ?? [];
        runbook.Steps = runbook.Steps?.Where(s => s is not null).ToList() ?? [];
        runbook.Schedule ??= new RunbookSchedule();

        foreach (var connection in runbook.Connections)
        {
            connection.Auth ??= new McpRunbookAuth();
            connection.Auth.Headers ??= [];
        }

        foreach (var step in runbook.Steps)
            step.Params ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return runbook;
    }
}
