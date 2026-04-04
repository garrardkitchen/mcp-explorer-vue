using System.Reflection;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Interfaces;
using ModelContextProtocol.Client;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Internal wrapper around a live <see cref="ConnectionContext"/> that exposes the
/// <see cref="IActiveConnection"/> contract to application code.
/// </summary>
internal sealed class ActiveConnection : IActiveConnection, IAsyncDisposable
{
    internal ConnectionContext Context { get; }

    private IReadOnlyList<ActiveTool>? _tools;
    private IReadOnlyList<ActivePrompt>? _prompts;
    private IReadOnlyList<ActiveResource>? _resources;
    private IReadOnlyList<ActiveResourceTemplate>? _resourceTemplates;

    internal ActiveConnection(ConnectionContext context)
    {
        Context = context;
    }

    public string Name => Context.Name;
    public string Endpoint => Context.Endpoint;
    public bool IsConnected => true;

    public IReadOnlyList<ActiveTool> Tools => _tools ??= Context.Tools
        .Select(t =>
        {
            var proto = GetProtocolObject(t, "ProtocolTool");
            return new ActiveTool(
                t.Name,
                t.Description ?? string.Empty,
                TryGetSchema(proto, "InputSchema"),
                McpIconHelper.GetBestIconUrl(proto),
                TryGetSchema(proto, "OutputSchema"),
                TryGetAnnotations(proto));
        })
        .ToList();

    public IReadOnlyList<ActivePrompt> Prompts => _prompts ??= Context.Prompts
        .Select(p => new ActivePrompt(
            p.Name,
            p.Description,
            ExtractPromptArguments(p),
            McpIconHelper.GetBestIconUrl(GetProtocolObject(p, "ProtocolPrompt"))))
        .ToList();

    public IReadOnlyList<ActiveResource> Resources => _resources ??= Context.Resources
        .Select(r => new ActiveResource(
            r.Uri,
            r.Name,
            r.Description,
            r.MimeType,
            McpIconHelper.GetBestIconUrl(GetProtocolObject(r, "ProtocolResource"))))
        .ToList();

    public IReadOnlyList<ActiveResourceTemplate> ResourceTemplates => _resourceTemplates ??= Context.ResourceTemplates
        .Select(t => new ActiveResourceTemplate(
            t.UriTemplate,
            t.Name,
            t.Description,
            McpIconHelper.GetBestIconUrl(GetProtocolObject(t, "ProtocolResourceTemplate"))))
        .ToList();

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync().ConfigureAwait(false);
    }

    private static object? TryGetSchema(object? proto, string propertyName)
    {
        if (proto is null) return null;
        try { return proto.GetType().GetProperty(propertyName)?.GetValue(proto); }
        catch { return null; }
    }

    private static ToolAnnotations? TryGetAnnotations(object? proto)
    {
        if (proto is null) return null;
        try
        {
            var ann = proto.GetType().GetProperty("Annotations")?.GetValue(proto);
            if (ann is null) return null;
            var t = ann.GetType();
            return new ToolAnnotations(
                GetProp<string?>(ann, t, "Title"),
                GetProp<bool?>(ann, t, "ReadOnlyHint"),
                GetProp<bool?>(ann, t, "DestructiveHint"),
                GetProp<bool?>(ann, t, "IdempotentHint"),
                GetProp<bool?>(ann, t, "OpenWorldHint"));
        }
        catch { return null; }
    }

    private static T? GetProp<T>(object obj, Type type, string name)
    {
        try { return (T?)type.GetProperty(name)?.GetValue(obj); }
        catch { return default; }
    }

    /// <summary>
    /// Retrieves the protocol-layer object (e.g. <c>ProtocolTool</c>) from an MCP client
    /// capability wrapper using reflection. Returns <c>null</c> if the property is absent or
    /// throws.
    /// </summary>
    private static object? GetProtocolObject(object source, string propertyName)
    {
        try
        {
            return source.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(source);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<PromptArgument> ExtractPromptArguments(McpClientPrompt prompt)
    {
        try
        {
            var protocolPrompt = GetProtocolObject(prompt, "ProtocolPrompt");

            if (protocolPrompt is null) return [];

            var argsProp = protocolPrompt.GetType()
                .GetProperty("Arguments", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (argsProp?.GetValue(protocolPrompt) is not System.Collections.IEnumerable argList) return [];

            var result = new List<PromptArgument>();
            foreach (var arg in argList)
            {
                if (arg is null) continue;
                var argType = arg.GetType();
                var name = argType.GetProperty("Name")?.GetValue(arg) as string ?? string.Empty;
                var desc = argType.GetProperty("Description")?.GetValue(arg) as string;
                var req = argType.GetProperty("Required")?.GetValue(arg) as bool? ?? false;
                result.Add(new PromptArgument(name, desc, req));
            }
            return result;
        }
        catch
        {
            return [];
        }
    }
}
